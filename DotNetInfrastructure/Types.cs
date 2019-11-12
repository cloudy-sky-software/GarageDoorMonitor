using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;

namespace Infra
{
    /**
     * Host settings specific to the HTTP plugin.
     *
     * For more details see https://docs.microsoft.com/en-us/azure/azure-functions/functions-host-json#http
     */
    public sealed class HttpHostExtensions
    {
        /** The route prefix that applies to all routes. Use an empty string to remove the default prefix. */
        public string? RoutePrefix { get; set; }

        /** The maximum number of outstanding requests that are held at any given time. */
        public int? MaxOutstandingRequests { get; set; }

        /** The maximum number of http functions that will be executed in parallel. */
        public int? MaxConcurrentRequests { get; set; }

        /**
         * When enabled, this setting causes the request processing pipeline to periodically check system performance
         * counters like connections/threads/processes/memory/cpu/etc. and if any of those counters are over a built-in
         * high threshold (80%), requests will be rejected with a 429 "Too Busy" response until the counter(s) return
         * to normal levels.
         */
        public bool DynamicThrottlesEnabled { get; set; }
    }

    public sealed class FunctionAppExtensions
    {
        public HttpHostExtensions? Http { get; set; }
    }

    public sealed class FunctionAppHostSettings
    {
        public FunctionAppExtensions? Extensions { get; set; }
    }

    public class FunctionAppArgsBase
    {
        /**
        * The storage account to use where the zip-file blob for the FunctionApp will be located. If
        * not provided, a new storage account will create. It will be a 'Standard', 'LRS', 'StorageV2'
        * account.
        */
        public Account? Account { get; set; }

        /**
         * A key-value pair of App Settings.
         */
        public InputMap<string>? AppSettings { get; set; }

        /**
         * Should the Function App send session affinity cookies, which route client requests in the same session to the same instance?
         */
        public Input<bool>? ClientAffinityEnabled { get; set; }

        /**
         * Options to control which files and packages are included with the serialized FunctionApp code.
         */
        //readonly codePathOptions?: pulumi.runtime.CodePathOptions;

        /**
         * An `connection_string` block as defined below.
         */
        public InputList<FunctionAppConnectionStringsArgs>? ConnectionStrings { get; set; }

        /**
         * The container to use where the zip-file blob for the FunctionApp will be located. If not
         * provided, the root container of the storage account will be used.
         */
        public Container? Container { get; set; }

        /**
         * Should the built-in logging of this Function App be enabled? Defaults to `true`.
         */
        public Input<bool>? EnableBuiltinLogging { get; set; }

        /**
         * Is the Function App enabled?
         */
        public Input<bool>? Enabled { get; set; }

        /**
         * Host configuration options.
         */
        public FunctionAppHostSettings? HostSettings { get; set; }

        /**
         * Can the Function App only be accessed via HTTPS? Defaults to `false`.
         */
        public Input<bool>? HttpsOnly { get; set; }

        /**
         * An `identity` block as defined below.
         */
        public Input<FunctionAppIdentityArgs>? Identity { get; set; }

        /**
         * Specifies the supported Azure location where the resource exists. Changing this forces a new resource to be created.
         */
        public Input<string>? Location { get; set; }

        /**
         * The name of the Function App.
         */
        public Input<string>? Name { get; set; }

        /**
         * Controls the value of WEBSITE_NODE_DEFAULT_VERSION in `appSettings`.  If not provided,
         * defaults to `8.11.1`.
         */
        public Input<string>? NodeVersion { get; set; }

        /**
         * The App Service Plan within which to create this Function App. Changing this forces a new
         * resource to be created.
         *
         * If not provided, a default "Consumption" plan will be created.  See:
         * https://docs.microsoft.com/en-us/azure/azure-functions/functions-scale#consumption-plan for
         * more details.
         */
        public Plan? Plan { get; set; }

        /**
         * The resource group in which to create the event subscription. [resourceGroup] takes precedence over [resourceGroupName].
         */
        public ResourceGroup? ResourceGroup { get; set; }

        /**
         * The name of the resource group in which to create the event subscription. [resourceGroup] takes precedence over [resourceGroupName].
         * Either [resourceGroupName] or [resourceGroup] must be supplied.
         */
        public Input<string>? ResourceGroupName { get; set; }

        /**
         * A `site_config` object as defined below.
         */
        public Input<FunctionAppSiteConfigArgs>? SiteConfig { get; set; }

        /**
         * A mapping of tags to assign to the resource.
         */
        public InputMap<object>? Tags { get; set; }

        /**
         * The runtime version associated with the Function App. Defaults to `~2`.
         */
        public Input<string>? Version;
    }

    public sealed class ArchiveFunctionAppArgs : FunctionAppArgsBase
    {
        public Archive? Archive { get; set; }
    }
}