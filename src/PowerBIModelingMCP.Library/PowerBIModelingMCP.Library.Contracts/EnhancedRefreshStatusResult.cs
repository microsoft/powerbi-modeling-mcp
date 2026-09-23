namespace PowerBIModelingMCP.Library.Contracts;

public class EnhancedRefreshStatusResult
{
	public string Status { get; set; } = string.Empty;

	public string? RequestId { get; set; }

	public string? StartTime { get; set; }

	public string? EndTime { get; set; }

	public string Message { get; set; } = string.Empty;
}
