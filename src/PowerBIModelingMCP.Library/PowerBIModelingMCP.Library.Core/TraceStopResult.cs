namespace PowerBIModelingMCP.Library.Core;

public class TraceStopResult
{
	public string TraceName { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public double Duration { get; set; }

	public int TotalEventsCaptured { get; set; }

	public int TotalEventsDiscarded { get; set; }
}
