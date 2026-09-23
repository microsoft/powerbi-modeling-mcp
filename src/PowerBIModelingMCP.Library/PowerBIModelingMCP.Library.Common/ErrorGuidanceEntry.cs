namespace PowerBIModelingMCP.Library.Common;

public sealed class ErrorGuidanceEntry
{
	public int ErrorCode { get; init; }

	public string Name { get; init; } = string.Empty;

	public string Category { get; init; } = string.Empty;

	public string Guidance { get; init; } = string.Empty;

	public string DoNotDo { get; init; } = string.Empty;
}
