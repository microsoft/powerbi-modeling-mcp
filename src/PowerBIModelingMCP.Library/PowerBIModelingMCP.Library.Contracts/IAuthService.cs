using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.AnalysisServices;

namespace PowerBIModelingMCP.Library.Contracts;

public interface IAuthService
{
	TokenCredential TokenCredential { get; }

	Task<Microsoft.AnalysisServices.AccessToken> GetAccessTokenAsync(bool clearCredential = false, CancellationToken cancellationToken = default(CancellationToken));

	void ClearCredential();
}
