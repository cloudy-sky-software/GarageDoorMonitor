# Garage Door Monitor using IFTTT + Azure Durable Functions + Pulumi

This is an application that can be used to create IFTTT applets that receive garage door status updates from myQ (or any supported automated garage door opener.)

## Pre-release features

Even though Durable Functions is already GA (generally available), durable entities is currently in public preview. As such, the C# project uses pre-release binaries. Pre-release software means that the APIs can contain breaking changes with each new release. So proceed with caution before adapting this to some other important production application.

See https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-preview for more information on breaking changes.

## Development

To run the durable functions locally, see the README file under the `GarageDoorMonitor` folder.

## Deploy

See the README file under the `infrastructure` folder.
