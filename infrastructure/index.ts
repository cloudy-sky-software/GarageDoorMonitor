import * as pulumi from "@pulumi/pulumi";
import * as azure from "@pulumi/azure";
import * as path from "path";

import { buildFunctionsProject} from "./projectBuilder";

const namePrefix = "grge-mon";
const config = new pulumi.Config();
const twilioAccountToken = config.requireSecret("twilioAccountToken");

buildFunctionsProject(path.join("..", "GarageDoorMonitor"));

const resourceGroup = new azure.core.ResourceGroup(`${namePrefix}-group`);

const kv = new azure.keyvault.KeyVault(`${namePrefix}-vault`, {
    resourceGroupName: resourceGroup.name,
    skuName: "standard",
    tenantId: azure.config.tenantId!,
    accessPolicies: [{
        tenantId: azure.config.tenantId!,
        // The current principal has to be granted permissions to Key Vault so that it can actually add and then remove
        // secrets to/from the Key Vault. Otherwise, 'pulumi up' and 'pulumi destroy' operations will fail.
        //
        // NOTE: This object ID value is NOT what you see in the Azure AD's App Registration screen.
        // Run `az ad sp show` from the Azure CLI to list the correct Object ID to use here.
        objectId: "your-SP-object-ID",
        secretPermissions: ["delete", "get", "list", "set"],
    }],
});

const twilioSecret = new azure.keyvault.Secret(`${namePrefix}-twil`, {
    keyVaultId: kv.id,
    value: twilioAccountToken,
});

const twilioSecretUri = pulumi.interpolate`${twilioSecret.vaultUri}secrets/${twilioSecret.name}/${twilioSecret.version}`;

const appInsights = new azure.appinsights.Insights(`${namePrefix}-ai`, {
    applicationType: "web",
    resourceGroupName: resourceGroup.name,
});

const durableFunctionApp = new azure.appservice.ArchiveFunctionApp(`${namePrefix}-funcs`, {
    resourceGroup,
    archive: new pulumi.asset.FileArchive("../GarageDoorMonitor/bin/Debug/netcoreapp2.1/publish"),
    appSettings: {
        "runtime": "dotnet",
        "TwilioAccountToken": pulumi.interpolate`@Microsoft.KeyVault(SecretUri=${twilioSecretUri})`,
        "APPINSIGHTS_INSTRUMENTATIONKEY": pulumi.interpolate`${appInsights.instrumentationKey}`,
        "TimerDelayMinutes": config.getNumber("timerDelayMinutes") || 2,
    },
    httpsOnly: true,
    identity: {
        type: "SystemAssigned"
    }
});

// Now that the app is created, update the access policies of the keyvault and
// grant the principalId of the function app access to the vault.
const principalId = durableFunctionApp.functionApp.identity.apply(id => id.principalId);

// Grant App Service access to KV secrets
const appAccessPolicy = new azure.keyvault.AccessPolicy(`${namePrefix}-app-policy`, {
   keyVaultId: kv.id,
   tenantId: azure.config.tenantId!,
   objectId: principalId,
   secretPermissions: ["get"],
}, { dependsOn: durableFunctionApp });

export const webhookUrl = durableFunctionApp.endpoint;
