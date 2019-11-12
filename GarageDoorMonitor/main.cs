using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace GarageDoorMonitor
{
    public static class Main
    {

        private static EntityId EntityId = new EntityId("GarageDoor", "Status");

        [FunctionName("Main")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
            ILogger log)
        {
            var delay = Environment.GetEnvironmentVariable("TimerDelayMinutes");
            var delayTimeSpan = TimeSpan.FromMinutes(Convert.ToInt32(delay));
            DateTime timer = ctx.CurrentUtcDateTime.Add(delayTimeSpan);
            log.LogInformation($"Setting timer to expire at {timer.ToLocalTime().ToString()}");
            await ctx.CreateTimer(timer, CancellationToken.None);

            try
            {
                // The use of a critical block, though optional, is recommended here.
                // Updates to durable entities are serial, by default.
                // Having the lock ensures that the entity state we are reading is guaranteed to
                // be the current value of the entity.
                using (await ctx.LockAsync(EntityId))
                {
                    var currentState = await ctx.CallEntityAsync<string>(EntityId, "read", null);
                    log.LogInformation($"Current state is {currentState}.");
                    // If the door is closed already, then don't do anything.
                    if (currentState.ToLowerInvariant() == "closed")
                    {
                        log.LogInformation("Looks like the door was already closed. Will skip sending text message.");
                        return;
                    }
                    await ctx.CallActivityAsync("SendTextMessage", null);
                }
            }
            catch (LockingRulesViolationException ex)
            {
                log.LogError(ex, "Failed to lock/call the entity.");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unexpected exception occurred.");
            }
        }

        [FunctionName("SendTextMessage")]
        public static async Task SendTextMessage([ActivityTrigger] IDurableActivityContext ctx, ILogger log)
        {
            var accountToken = Environment.GetEnvironmentVariable("TwilioAccountToken");
            if (accountToken == "")
            {
                log.LogError("Cannot call Twilio API without the account token env var.");
                return;
            }

            using (var client = new HttpClient())
            {
                try
                {
                    log.LogInformation("Preparing Twilio API call.");
                    var content = new FormUrlEncodedContent(new[] {
                        // You will need to buy a new number from Twilio here: https://www.twilio.com/console/sms/services.
                        // Each number typically costs about $1/month + an additional insignificant amount per outgoing message.
                        new KeyValuePair<string, string>("From", "your_Twilio_phone_number_with_country_code"),
                        new KeyValuePair<string, string>("To", "your_phone_number_with_country_code"),
                        new KeyValuePair<string, string>("Body", "Did you forget to close the garage door?")
                    });
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var authBytes = Convert.ToBase64String(Encoding.GetEncoding("ASCII").GetBytes($"yourAccountSID:{accountToken}"));
                    var authHeader = new AuthenticationHeaderValue("Basic", authBytes);
                    client.DefaultRequestHeaders.Authorization = authHeader;

                    log.LogInformation("Calling Twilio API.");
                    using (var resp = await client.PostAsync("https://api.twilio.com/2010-04-01/Accounts/yourAccountSID/Messages.json", content))
                    {
                        if (!resp.IsSuccessStatusCode)
                        {
                            log.LogError(new Exception("Twilio API call failed"), "Unexpected response code received: {StatusCode}.", resp.StatusCode);
                        }
                    }
                    log.LogInformation("Finished calling Twilio API.");
                }
                catch (ArgumentException ex)
                {
                    log.LogError(ex, "Exception occurred while trying to call the Twilio API.");
                }
            }
        }

        [FunctionName("GarageDoor")]
        public static void GarageDoorStatusEntity([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName)
            {
                case "update":
                    ctx.SetState(ctx.GetInput<string>());
                    break;
            }

            var currentState = ctx.GetState<string>();
            ctx.Return(currentState);
        }

        [FunctionName("Main_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")]HttpRequestMessage req,
            [DurableClient]IDurableEntityClient entityClient,
            [DurableClient]IDurableOrchestrationClient client,
            ILogger log)
        {
            var updatedState = req.RequestUri.ParseQueryString().Get("state");
            log.LogInformation($"Received request to update status to {updatedState}.");

            // ReadEntityStateAsync may return a stale entity value.
            // See the important note under this section:
            // https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-preview#accessing-entities-from-clients
            var currentState = await entityClient.ReadEntityStateAsync<string>(EntityId);
            if (currentState.EntityExists && currentState.EntityState == updatedState)
            {
                log.LogInformation($"Door status is already {currentState.EntityState}. Will not start a new orchestrator instance.");
                HttpResponseMessage httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"Door status is already {currentState.EntityState}.")
                };
                return httpResponseMessage;
            }

            // Update the entity. This is a fire-and-forget call. There is no guarantee that the update
            // completed even though the task completes. But the update is guaranteed to eventually complete.
            // For request-response style communication with entities, consider using an orchestration
            // to even update the entity.
            // See: https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-preview#accessing-entities-from-orchestrations
            await entityClient.SignalEntityAsync(EntityId, "update", updatedState);
            log.LogInformation($"Updated status to {updatedState}.");
            if (updatedState.ToLowerInvariant() == "closed")
            {
                HttpResponseMessage httpResponseMessage = new HttpResponseMessage(HttpStatusCode.NoContent);
                return httpResponseMessage;
            }

            // Door is opened, so let's create an orchestration instance to handle the rest of the work.
            string instanceId = await client.StartNewAsync("Main", null);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}