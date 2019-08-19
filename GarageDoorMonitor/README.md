# Durable Functions in an Azure Function App

This project contains C# functions that use the Durable Functions extensions of the Azure Functions platform. Learn more [here](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview).

## Development

Make sure you have installed dotnet core for your platform from the official [downloads](https://dotnet.microsoft.com/download) page.

To run the `GarageDoorMonitor` project locally, download the Community Edition of VS 2019 from [here](https://visualstudio.microsoft.com/downloads/). You can refer to the quickstart tutorial [here](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-create-first-csharp).

### local.settings.json

Create a new file call local.settings.json and enter these values.

**Note**: This file is ignored because the app settings for the deployed version are set by Pulumi. Sensitive values are loaded from an Azure KeyVault. See `infrastructure/index.ts`.

```js
{
  "IsEncrypted": false,
  "Values": {
    // Azure Storage emulator is automatically downloaded and started when you run this project from VS 2019.
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    // Replace with your own account's token.
    "TwilioAccountToken": "fake",
    "TimerDelayMinutes": "2"
  }
}
```
