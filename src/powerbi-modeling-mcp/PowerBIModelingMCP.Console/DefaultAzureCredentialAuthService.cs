using System;
using Azure.Core;
using Azure.Identity;

namespace PowerBIModelingMCP.Console;

public class DefaultAzureCredentialAuthService : TokenCredentialAuthService
{
	protected override string CredentialDisplayName => "DefaultAzureCredential";

	protected override string CredentialUnavailableMessage => "DefaultAzureCredential could not find any available credentials. Ensure at least one credential source is configured (environment variables, managed identity, Azure CLI, etc.).";

	protected override string AuthenticationFailedMessage => "DefaultAzureCredential authentication failed. Verify your credential configuration and Power BI API permissions.";

	protected override TokenCredential CreateCredential()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
		DefaultAzureCredentialOptions defaultAzureCredentialOptions = new DefaultAzureCredentialOptions();
		if (!string.IsNullOrWhiteSpace(environmentVariable))
		{
			defaultAzureCredentialOptions.TenantId = environmentVariable;
		}
		return new DefaultAzureCredential(defaultAzureCredentialOptions);
	}
}
