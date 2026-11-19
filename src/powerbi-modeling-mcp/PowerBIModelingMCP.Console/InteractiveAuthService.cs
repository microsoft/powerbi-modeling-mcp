using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.AnalysisServices;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Console;

public class InteractiveAuthService(ModelingClientConfig config) : IAuthService
{
	private TokenCredential? _credential;

	private readonly object _lock = new object();

	public TokenCredential TokenCredential
	{
		get
		{
			lock (_lock)
			{
				if (_credential == null)
				{
					InteractiveBrowserCredentialOptions interactiveBrowserCredentialOptions = new InteractiveBrowserCredentialOptions
					{
						AuthorityHost = new Uri(config.AuthorityHost),
						ClientId = config.ClientId
					};
					string text = Environment.GetEnvironmentVariable("PBI_MODELING_MCP_TENANT_ID") ?? Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
					if (!string.IsNullOrWhiteSpace(text))
					{
						interactiveBrowserCredentialOptions.TenantId = text;
					}
					_credential = new InteractiveBrowserCredential(interactiveBrowserCredentialOptions);
				}
				return _credential;
			}
		}
		internal set
		{
			_credential = value;
		}
	}

	public async Task<Microsoft.AnalysisServices.AccessToken> GetAccessTokenAsync(bool clearCredential = false, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			if (clearCredential)
			{
				ClearCredential();
			}
			TokenCredential tokenCredential = TokenCredential;
			TokenRequestContext requestContext = new TokenRequestContext(config.Scopes);
			Azure.Core.AccessToken accessToken = await tokenCredential.GetTokenAsync(requestContext, cancellationToken);
			return new Microsoft.AnalysisServices.AccessToken(accessToken.Token, accessToken.ExpiresOn);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2)
		{
			throw new McpExceptionWithSource("Failed to acquire access token via interactive browser authentication. Ensure you have permissions and can log in through the browser. Error: " + ex2.Message, ex2, ErrorSource.System, "Failed to acquire access token via interactive browser authentication.");
		}
	}

	public void ClearCredential()
	{
		lock (_lock)
		{
			_credential = null;
		}
	}
}
