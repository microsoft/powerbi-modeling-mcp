using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Core;

public class TracePauseResult
{
	public string TraceName { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public DateTime StartTime { get; set; }

	public double Duration { get; set; }

	public int EventsCaptured { get; set; }

	public int EventsDiscarded { get; set; }

	public List<string> SubscribedEvents { get; set; } = new List<string>();

	public List<string>? Warnings { get; set; }

	public bool FilterCurrentSessionOnly { get; set; }
}
