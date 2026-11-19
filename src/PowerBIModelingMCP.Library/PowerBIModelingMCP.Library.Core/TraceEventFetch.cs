using System.Collections.Generic;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Core;

public class TraceEventFetch
{
	public string TraceName { get; set; } = string.Empty;

	public int EventCount { get; set; }

	public bool Cleared { get; set; }

	public List<CapturedTraceEvent> Events { get; set; } = new List<CapturedTraceEvent>();
}
