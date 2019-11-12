# Garage Door Monitor using IFTTT + Azure Durable Functions + Pulumi

This is an application that can be used to create IFTTT applets that receive garage door status updates from myQ (or any supported automated garage door opener.)

## Pre-release features

Even though Durable Functions is already GA (generally available), durable entities is currently in public preview. As such, the C# project uses pre-release binaries. Pre-release software means that the APIs can contain breaking changes with each new release. So proceed with caution before adapting this to some other important production application.

See https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-preview for more information on breaking changes.

## DotNet Core Support in Pulumi

Pulumi announced the [public preview](https://www.pulumi.com/blog/pulumi-dotnet-core/) of their SDK for dotnet core 3.0. Even though the dotnet support is preview, most of the SDK is pretty mature for most of your infrastructure needs. I have added a C# verrsion (checkout the `DotNetInfrastructure` C# project when you open the Solution) of the same infrastructure that I originally wrote using TypeScript.

In order to use the C# version to deploy the infrastructure, you should publish the Function App project. There is a publish profile configured in the project.

Simply run:

```
dotnet publish .\GarageDoorMonitor\ /p:PublishProfile=.\GarageDoorMonitor\Properties\PublishProfiles\FolderProfile.pubxml
```

Once the Function App project is published, you can run `pulumi up` from the `DotNetInfrastructure` project folder or by passing `--cwd DotNetInfrastructure` flag to `pulumi up`.

## Development

To run the durable functions locally, see the README file under the `GarageDoorMonitor` folder.

## Deploy

See the README file under the `infrastructure` folder.
