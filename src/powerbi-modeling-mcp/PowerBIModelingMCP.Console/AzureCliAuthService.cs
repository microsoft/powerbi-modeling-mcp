using System;
using Azure.Core;
using Azure.Identity;

namespace PowerBIModelingMCP.Console;

public class AzureCliAuthService : TokenCredentialAuthService
{
	protected override string CredentialDisplayName => "Azure CLI";

	protected override string CredentialUnavailableMessage => "Azure CLI credential is unavailable.";

	protected override string AuthenticationFailedMessage => "Azure CLI authentication failed.";

	protected override TokenCredential CreateCredential()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
		AzureCliCredentialOptions azureCliCredentialOptions = new AzureCliCredentialOptions();
		if (!string.IsNullOrWhiteSpace(environmentVariable))
		{
			azureCliCredentialOptions.TenantId = environmentVariable;
		}
		return new AzureCliCredential(azureCliCredentialOptions);
	}
}
