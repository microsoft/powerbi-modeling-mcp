using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class QueryExecutionStatistics : ResultBase
{
	public string? ErrorMessage { get; set; }

	public long TotalDuration { get; set; }

	public long TotalCpuTime { get; set; }

	public DateTime? QueryStartDateTime { get; set; }

	public DateTime? QueryEndDateTime { get; set; }

	public long TotalVertipaqDuration { get; set; }

	public long TotalVertipaqCpuTime { get; set; }

	public int TotalVertipaqQueryCount { get; set; }

	public int TotalVertipaqCacheMatches { get; set; }

	public long TotalDirectQueryDuration { get; set; }

	public int TotalDirectQueryCount { get; set; }

	public string? ActivityId { get; set; }

	public string? QueryText { get; set; }

	public List<CapturedTraceEvent>? DetailedEvents { get; set; }
}
