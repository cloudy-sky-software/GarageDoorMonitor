# Pulumi App for deploying Garage Door Monitor Durable Functions

## Configuration

* `twilioAccountToken` - The Twilio account token used for accessing the REST API. See https://support.twilio.com/hc/en-us/articles/223136027-Auth-Tokens-and-How-to-Change-Them.
* `timerDelayMinutes` - The delay in minutes that the orchestration client will create a timer before checking the entity state and sending you a text message.
* Azure credentials for the Pulumi CLI need to be configured using a supported auth mechanism. See [this](https://www.pulumi.com/docs/reference/clouds/azure/setup/) page for configuring the Azure provider.

## Deploy

* Install the Pulumi CLI from https://www.pulumi.com/docs/reference/install/.
* `npm install` to restore the npm dependencies.
* Signup for a free (no credit card required) Pulumi account [here](https://app.pulumi.com/signup).
* `pulumi login` to login to your Pulumi account.
* `pulumi up` to deploy the durable function app and its dependencies to Azure. 
