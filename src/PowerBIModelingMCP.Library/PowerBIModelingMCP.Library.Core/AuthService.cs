using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.AnalysisServices;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class AuthService
{
	private sealed class UninitializedAuthService : IAuthService
	{
		public static readonly UninitializedAuthService Instance = new UninitializedAuthService();

		public TokenCredential TokenCredential
		{
			get
			{
				throw NotInitialized();
			}
		}

		private static InvalidOperationException NotInitialized()
		{
			return new InvalidOperationException("AuthService has not been initialized. Call AuthService.Initialize() during host startup.");
		}

		public Task<Microsoft.AnalysisServices.AccessToken> GetAccessTokenAsync(bool clearCredential = false, CancellationToken cancellationToken = default(CancellationToken))
		{
			throw NotInitialized();
		}

		public void ClearCredential()
		{
			throw NotInitialized();
		}
	}

	private static volatile IAuthService _authServiceCore = UninitializedAuthService.Instance;

	public static TokenCredential TokenCredential => _authServiceCore.TokenCredential;

	public static IAuthService Instance => _authServiceCore;

	public static void Initialize(IAuthService authServiceCore)
	{
		ArgumentNullException.ThrowIfNull(authServiceCore, "authServiceCore");
		_authServiceCore = authServiceCore;
	}

	public static Task<Microsoft.AnalysisServices.AccessToken> GetAccessTokenAsync(bool clearCredential = false, CancellationToken cancellationToken = default(CancellationToken))
	{
		return _authServiceCore.GetAccessTokenAsync(clearCredential, cancellationToken);
	}

	public static void ClearCredential()
	{
		_authServiceCore.ClearCredential();
	}
}
