using System.Collections.Generic;
using System.Threading.Tasks;

using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Azure.KeyVault;
using Pulumi.Azure.KeyVault.Inputs;
using Pulumi.Azure.AppInsights;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.AppService;

using Infra;
using System.IO;
using System;

class Program
{
    private const string NamePrefix = "grge-mon";
    private static Config Config = new Config();
    private static readonly Output<string> TwilioAccountToken = Config.RequireSecret("twilioAccountToken");

    static Task<int> Main()
    {
        return Deployment.RunAsync(async () => await CreateResources());
    }

    static Input<string> GetIntConfigOrDefault(string configName, int defaultValue)
    {
        var delay = Config.GetInt32(configName) ?? defaultValue;
        return System.Convert.ToString(delay);
    }

    static async Task<Dictionary<string, object?>> CreateResources()
    {
        var clientConfig = await GetClientConfig.InvokeAsync();
        var tenantId = clientConfig.TenantId;

        var resourceGroup = new ResourceGroup($"{ NamePrefix }-group");

        var kv = new KeyVault($"{ NamePrefix }-vault", new KeyVaultArgs { 
            ResourceGroupName = resourceGroup.Name,
            SkuName = "standard",
            TenantId = tenantId,
             AccessPolicies =
            {
                new KeyVaultAccessPolicyArgs {
                    TenantId = tenantId,
                    // TODO: CHANGE ME!
                    // The current principal has to be granted permissions to Key Vault so that it can actually add and then remove
                    // secrets to/from the Key Vault. Otherwise, 'pulumi up' and 'pulumi destroy' operations will fail.-                    //
                    // NOTE: This object ID value is NOT what you see in the Azure AD's App Registration screen.
                    // Run `az ad sp show` from the Azure CLI to list the correct Object ID to use here.
                    ObjectId = "your-SP-object-ID",
                    SecretPermissions = new InputList<string>{ "delete", "get", "list", "set" },
                }
            }
        });

        var twilioSecret = new Secret($"{ NamePrefix }-twil", new SecretArgs 
        {
            KeyVaultId = kv.Id,
            Value = TwilioAccountToken,
        });

        var appInsights = new Insights($"{ NamePrefix }-ai", new InsightsArgs
        {
            ApplicationType = "web",
            ResourceGroupName = resourceGroup.Name,
        });

        var durableFunctionApp = new ArchiveFunctionApp($"{ NamePrefix }-funcs", new ArchiveFunctionAppArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Archive = new FileArchive($"./bin/Debug/netcoreapp3.1/GarageDoorMonitor/publish"),
            AppSettings = new InputMap<string>
            {
                { "runtime", "dotnet" },
                { "FUNCTIONS_EXTENSION_VERSION", "~3" },
                { "TwilioAccountToken", Output.Format($"@Microsoft.KeyVault(SecretUri ={ twilioSecret.Id })") },
                { "APPINSIGHTS_INSTRUMENTATIONKEY", Output.Format($"{ appInsights.InstrumentationKey }")},
                { "TimerDelayMinutes",  GetIntConfigOrDefault("timerDelayMinutes", 2) },
            },
            HttpsOnly = true,
            Identity = new FunctionAppIdentityArgs { Type = "SystemAssigned" },
        });

        // Now that the app is created, update the access policies of the keyvault and
        // grant the principalId of the function app access to the vault.
        var principalId = durableFunctionApp.FunctionApp.Identity.Apply(id => id.PrincipalId ?? "0c4825d9-3901-40a8-ab89-ad4e3aeeadd9");

        // Grant App Service access to KV secrets
        var appAccessPolicy = new AccessPolicy($"{ NamePrefix }-app-policy", new AccessPolicyArgs
        {
            KeyVaultId = kv.Id,
            TenantId = tenantId,
            ObjectId = principalId,
            SecretPermissions = new InputList<string> { "get" },
        });

        return new Dictionary<string, object?> 
        {
            { "webhookUrl", durableFunctionApp.Endpoint },
        };
    }
}
