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

public abstract class TokenCredentialAuthService : IAuthService
{
	private static readonly string[] RequiredScopes = new string[1] { "https://analysis.windows.net/powerbi/api/.default" };

	private TokenCredential? _credential;

	private readonly object _credentialLock = new object();

	protected abstract string CredentialDisplayName { get; }

	protected abstract string CredentialUnavailableMessage { get; }

	protected abstract string AuthenticationFailedMessage { get; }

	public TokenCredential TokenCredential
	{
		get
		{
			lock (_credentialLock)
			{
				if (_credential == null)
				{
					_credential = CreateCredential();
				}
				return _credential;
			}
		}
		internal set
		{
			_credential = value;
		}
	}

	protected abstract TokenCredential CreateCredential();

	public async Task<Microsoft.AnalysisServices.AccessToken> GetAccessTokenAsync(bool clearCredential = false, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (clearCredential)
		{
			ClearCredential();
		}
		try
		{
			TokenRequestContext requestContext = new TokenRequestContext(RequiredScopes);
			Azure.Core.AccessToken accessToken = await TokenCredential.GetTokenAsync(requestContext, cancellationToken);
			return new Microsoft.AnalysisServices.AccessToken(accessToken.Token, accessToken.ExpiresOn);
		}
		catch (CredentialUnavailableException ex)
		{
			throw new McpExceptionWithSource(CredentialUnavailableMessage + " Error: " + ex.Message, ex, ErrorSource.User, CredentialUnavailableMessage);
		}
		catch (AuthenticationFailedException ex2)
		{
			throw new McpExceptionWithSource(AuthenticationFailedMessage + " Error: " + ex2.Message, ex2, ErrorSource.User, AuthenticationFailedMessage);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex4)
		{
			throw new McpExceptionWithSource("Failed to acquire access token via " + CredentialDisplayName + ". Error: " + ex4.Message, ex4, ErrorSource.System, "Failed to acquire access token via " + CredentialDisplayName + ".");
		}
	}

	public void ClearCredential()
	{
		lock (_credentialLock)
		{
			_credential = null;
		}
	}
}
