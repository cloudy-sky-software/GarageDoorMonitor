using System;
using System.Text.RegularExpressions;

using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;

namespace Infra
{
    public sealed class FunctionAppParts
    {
        public Account Account { get; }
        public Blob Blob { get; }
        public Container Container { get; }
        public Plan Plan { get; }
        public FunctionAppArgs FunctionAppArgs { get; }
        public Output<string> RootPath { get; }

        public FunctionAppParts(Account account, Container container, Blob blob, Plan plan, FunctionAppArgs functionAppArgs, Output<string> rootPath) =>
            (Account, Container, Blob, Plan, FunctionAppArgs, RootPath) = (account, container, blob, plan, functionAppArgs, rootPath);
    }

    public abstract class PackagedFunctionApp : ComponentResource
    {
        private Regex StorageAccountNameCleanerRegex;
        private Regex StorageContainerNameCleanerRegex;
        private const string FUNCTION_APP_STABLE_RUNTIME_VERSION = "~2";

        /**
        * Storage account where the FunctionApp's zipbBlob is uploaded to.
        */
        public Account Account { get; private set; }
        /**
         * Storage container where the FunctionApp's zipbBlob is uploaded to.
         */
        public Container Container { get; private set; }
        /**
         * The blob containing all the code for this FunctionApp.
         */
        public Blob Blob { get; private set; }
        /**
         * The plan this Function App runs under.
         */
        public Plan Plan { get; private set; }
        /**
         * The Function App which contains the functions from the archive.
         */
        public FunctionApp FunctionApp { get; private set; }
        /**
         * Root HTTP endpoint of the Function App.
         */
        public Output<string> Endpoint { get; private set; }

        public PackagedFunctionApp(string type, string name, ArchiveFunctionAppArgs args, ComponentResourceOptions? opts) : base(type, name, opts)
        {
            StorageAccountNameCleanerRegex = new Regex(@"[^a-zA-Z0-9]");
            StorageContainerNameCleanerRegex = new Regex(@"[^a-zA-Z0-9-]");

            var parentOpts = new CustomResourceOptions { Parent = this };
            var parts = createFunctionAppParts(name, args, parentOpts);

            this.Account = parts.Account;
            this.Container = parts.Container;
            this.Blob = parts.Blob;
            this.Plan = parts.Plan;
            this.FunctionApp = new FunctionApp(name, parts.FunctionAppArgs, parentOpts);
            this.Endpoint = getEndpoint(this.FunctionApp, parts.RootPath);
            this.RegisterOutputs();
        }
        
        private Output<string> getEndpoint(FunctionApp app, Output<string> rootPath)
        {
            return Output.Format($"https://{app.DefaultHostname}/{rootPath}");
        }

        private Input<string> getResourceGroupName(ResourceGroup? resourceGroup, Input<string>? resourceGroupName)
        {
            if (resourceGroup == null && resourceGroupName == null)
            {
                throw new Exception("Either resourceGroup or resourceGroupName must be provided.");
            }

            if (resourceGroup != null)
            {
                return resourceGroup.Name;
            }

            return resourceGroupName!;
        }

        private T createIfUndefined<T>(T? resource, Func<T>? resourceCreatorFunc)
            where T : Resource
        {
            if (resource == null && resourceCreatorFunc == null)
            {
                throw new Exception("Either a resource or a function that can create it must be provided");
            }

            if (resource != null)
            {
                return resource;
            }
            return resourceCreatorFunc!();
        }

        private string makeSafeName(Regex regex, string prefix, int substrIndex)
        {
            var cleaned = regex.Replace(prefix, "");
            // Account name needs to be at max 24 chars (minus the extra 8 random chars);
            // not exceed the max length of 24.
            // Name must be alphanumeric.
            return cleaned.ToLower().Substring(0, Math.Min(cleaned.Length, substrIndex));
        }

        private Input<string> getOrDefault(Input<string>? arg, Input<string> defaultValue)
        {
            return arg ?? defaultValue;
        }

        private InputMap<string> combineAppSettings(Blob zipBlob, Account account, ArchiveFunctionAppArgs args)
        {
            var codeBlobUrl = SharedAccessSignature.SignedBlobReadUrl(zipBlob, account);
            var settings = args.AppSettings;

            if (settings == null)
            {
                settings = new InputMap<string>();
            }
            settings.Add("WEBSITE_RUN_FROM_PACKAGE", codeBlobUrl);
            settings.Add("WEBSITE_NODE_DEFAULT_VERSION", "~10");
            return settings;
        }

        private FunctionAppParts createFunctionAppParts(string name, ArchiveFunctionAppArgs args, CustomResourceOptions? opts = null)
        {
            if (args.Archive == null)
            {
                throw new ArgumentNullException("Deployment [archive] must be provided.");
            }

            var resourceGroupName = getResourceGroupName(args.ResourceGroup, args.ResourceGroupName);

            var plan = createIfUndefined(args.Plan, () =>
            {
                return new Plan(name, new PlanArgs
                {

                    ResourceGroupName = resourceGroupName,
                    Location = args.Location,
                    Kind = "FunctionApp",
                    Sku = new PlanSkuArgs
                    {
                        Tier = "Dynamic",
                        Size = "Y1",
                    },
                }, opts);
            });

            var account = createIfUndefined(args.Account, () =>
            {
                return new Account(makeSafeName(StorageAccountNameCleanerRegex, name, 24 - 8), new AccountArgs
                {
                    ResourceGroupName = resourceGroupName,
                    Location = args.Location,
                    AccountKind = "StorageV2",
                    AccountTier = "Standard",
                    AccountReplicationType = "LRS",
                }, opts);
            });

            var container = createIfUndefined(args.Container, () =>
            {
                return new Container(makeSafeName(StorageContainerNameCleanerRegex, name, 63 - 8), new ContainerArgs
                {
                    StorageAccountName = account.Name,
                    ContainerAccessType = "private",
                }, opts);
            });

            var zipBlob = new Blob(name, new BlobArgs
            {
                StorageAccountName = account.Name,
                StorageContainerName = container.Name,
                Type = "Block",
                Source = args.Archive
            }, opts);

            var functionArgs = new FunctionAppArgs
            {
                ResourceGroupName = resourceGroupName,
                Location = args.Location,

                ClientAffinityEnabled = args.ClientAffinityEnabled,
                EnableBuiltinLogging = args.EnableBuiltinLogging,
                Enabled = args.Enabled,
                SiteConfig = args.SiteConfig,
                Identity = args.Identity,
                Name = args.Name,
                //AuthSettings = args.AuthSettings,

                HttpsOnly = args.HttpsOnly,
                AppServicePlanId = plan.Id,
                StorageConnectionString = account.PrimaryConnectionString,
                Version = getOrDefault(args.Version, FUNCTION_APP_STABLE_RUNTIME_VERSION),

                AppSettings = combineAppSettings(zipBlob, account, args),
            };

            if (args.ConnectionStrings != null)
            {
                functionArgs.ConnectionStrings = args.ConnectionStrings;
            }
            if (args.Tags != null)
            {
                functionArgs.Tags = args.Tags;
            }

            var routePrefix = args.HostSettings?.Extensions?.Http?.RoutePrefix;
            var rootPath = Output.Format($"{ routePrefix ?? "api" }/");

            return new FunctionAppParts(account, container, zipBlob, plan, functionArgs, rootPath);
        }
    }

    public sealed class ArchiveFunctionApp : PackagedFunctionApp
    {
        public ArchiveFunctionApp(string name, ArchiveFunctionAppArgs args, ComponentResourceOptions? opts = null) : base("azure:appservice:ArchiveFunctionApp", name, args, opts)
        {

        }
    }
}
