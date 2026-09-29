using Azure.Core;
using Azure.Identity;

namespace PowerBIModelingMCP.Console;

public class ServicePrincipalAuthService : TokenCredentialAuthService
{
	protected override string CredentialDisplayName => "service principal authentication";

	protected override string CredentialUnavailableMessage => "Service principal credentials are not properly configured. Verify AZURE_CLIENT_ID, AZURE_TENANT_ID, and either AZURE_CLIENT_SECRET or AZURE_CLIENT_CERTIFICATE_PATH are set correctly.";

	protected override string AuthenticationFailedMessage => "Service principal authentication failed. Verify your credentials and Power BI API permissions.";

	protected override TokenCredential CreateCredential()
	{
		return new EnvironmentCredential();
	}
}
