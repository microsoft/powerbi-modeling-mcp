using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Contracts;

public class EnhancedRefreshResult : ResultBase
{
	public string? RequestId { get; set; }

	public string Message { get; set; } = string.Empty;
}
