using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

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
            var delayTimeSpan = ctx.GetInput<TimeSpan>();
            var maxRetries = 10;
            var count = 0;
            // The following loop will execute for as long as the garage door remains open
            // up to max number of retries.
            while(true)
            {
                DateTime timer = ctx.CurrentUtcDateTime.Add(delayTimeSpan);
                log.LogInformation($"Setting timer to expire at {timer.ToLocalTime()}");
                await ctx.CreateTimer(timer, CancellationToken.None);
                log.LogInformation("Timer fired!");

                try
                {
                    using (await ctx.LockAsync(EntityId))
                    {
                        log.LogInformation("Entity lock acquired.");
                        var currentState = await ctx.CallEntityAsync<string>(EntityId, "read", null);
                        log.LogInformation($"Current state is {currentState}.");
                        // If the door is closed already, then don't do anything.
                        if (currentState.ToLowerInvariant() == "closed")
                        {
                            log.LogInformation("Looks like the door was already closed. Will skip sending text message.");
                            break;
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

                if (count >= maxRetries)
                {
                    log.LogInformation("Reached max retry count, but the garage door is still open. Will stop checking now.");
                    break;
                }
                log.LogInformation("Door is still open. Will schedule another timer to check again.");
                count++;
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
                        new KeyValuePair<string, string>("From", "your_Twilio_phone_number_with_country_code"),
                        new KeyValuePair<string, string>("To", "your_phone_number_with_country_code"),
                        new KeyValuePair<string, string>("Body", "Did you forget to close the garage door?")
                    });
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var authBytes = Convert.ToBase64String(Encoding.GetEncoding("ASCII").GetBytes($"your_TwilioAccountSID:{accountToken}"));
                    var authHeader = new AuthenticationHeaderValue("Basic", authBytes);
                    client.DefaultRequestHeaders.Authorization = authHeader;

                    log.LogInformation("Calling Twilio API.");
                    using (var resp = await client.PostAsync("https://api.twilio.com/2010-04-01/Accounts/your_TwilioAccountSID/Messages.json", content))
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
            [DurableClient]IDurableEntityClient client,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var updatedState = req.RequestUri.ParseQueryString().Get("state");
            log.LogInformation($"Received request to update status to {updatedState}.");

            var currentState = await client.ReadEntityStateAsync<string>(EntityId);
            if (currentState.EntityExists && currentState.EntityState == updatedState)
            {
                log.LogInformation($"Door status is already {currentState.EntityState}. Will not start a new orchestrator instance.");
                HttpResponseMessage httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"Door status is already {currentState.EntityState}.")
                };
                return httpResponseMessage;
            }

            // Update the entity.
            await client.SignalEntityAsync(EntityId, "update", updatedState);
            log.LogInformation($"Updated status to {updatedState}.");
            if (updatedState.ToLowerInvariant() == "closed")
            {
                HttpResponseMessage httpResponseMessage = new HttpResponseMessage(HttpStatusCode.NoContent);
                return httpResponseMessage;
            }

            var delay = Environment.GetEnvironmentVariable("TimerDelayMinutes");
            var delayTimeSpan = TimeSpan.FromMinutes(Convert.ToInt32(delay));
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync<TimeSpan>("Main", null, delayTimeSpan);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}