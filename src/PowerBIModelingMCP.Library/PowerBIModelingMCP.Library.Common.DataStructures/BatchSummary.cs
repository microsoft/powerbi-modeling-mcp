using System;
using System.Text.Json.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class BatchSummary
{
	[JsonPropertyOrder(1)]
	public int TotalItems { get; set; }

	[JsonPropertyOrder(2)]
	public int SuccessCount { get; set; }

	[JsonPropertyOrder(3)]
	public int FailureCount { get; set; }

	[JsonPropertyOrder(4)]
	public TimeSpan ExecutionTime { get; set; }
}
