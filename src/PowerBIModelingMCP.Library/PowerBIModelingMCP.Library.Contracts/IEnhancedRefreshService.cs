using System.Threading;
using System.Threading.Tasks;

namespace PowerBIModelingMCP.Library.Contracts;

public interface IEnhancedRefreshService
{
	Task<EnhancedRefreshResult> StartRefreshAsync(string? workspaceId, string datasetId, string refreshType = "automatic", string? tableName = null, string? partitionName = null, CancellationToken cancellationToken = default(CancellationToken));

	Task<EnhancedRefreshStatusResult> GetRefreshStatusAsync(string? workspaceId, string datasetId, string requestId, CancellationToken cancellationToken = default(CancellationToken));

	Task CancelRefreshAsync(string? workspaceId, string datasetId, string requestId, CancellationToken cancellationToken = default(CancellationToken));
}
