using System;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TraceContext
{
	public string TraceName { get; set; } = string.Empty;

	public DateTime StartTime { get; set; } = DateTime.UtcNow;

	public required Trace Trace { get; set; }

	public required ITabularServer Server { get; set; }

	public bool IsActive { get; set; }

	public bool IsPaused { get; set; }

	public List<string> SubscribedEvents { get; set; } = new List<string>();

	public List<CapturedTraceEvent> CapturedEvents { get; set; } = new List<CapturedTraceEvent>();

	public int TotalEventsCaptured { get; set; }

	public int TotalEventsDiscarded { get; set; }

	public bool FilterCurrentSessionOnly { get; set; } = true;
}
