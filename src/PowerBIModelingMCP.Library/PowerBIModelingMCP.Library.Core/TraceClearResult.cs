namespace PowerBIModelingMCP.Library.Core;

public class TraceClearResult
{
	public string TraceName { get; set; } = string.Empty;

	public int EventsCleared { get; set; }

	public string Status { get; set; } = string.Empty;
}
