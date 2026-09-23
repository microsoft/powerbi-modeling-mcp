using System;
using Azure.Core;
using Azure.Identity;

namespace PowerBIModelingMCP.Console;

public class ManagedIdentityAuthService : TokenCredentialAuthService
{
	protected override string CredentialDisplayName => "managed identity authentication";

	protected override string CredentialUnavailableMessage => "Managed identity authentication is unavailable. Configure workload identity (AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_FEDERATED_TOKEN_FILE) or run in an Azure environment with managed identity enabled.";

	protected override string AuthenticationFailedMessage => "Managed identity authentication failed. Verify federated credential or managed identity configuration and Power BI API permissions.";

	protected override TokenCredential CreateCredential()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
		ManagedIdentityCredential managedIdentityCredential = (string.IsNullOrWhiteSpace(environmentVariable) ? new ManagedIdentityCredential() : new ManagedIdentityCredential(environmentVariable));
		return new ChainedTokenCredential(new WorkloadIdentityCredential(), managedIdentityCredential);
	}
}
