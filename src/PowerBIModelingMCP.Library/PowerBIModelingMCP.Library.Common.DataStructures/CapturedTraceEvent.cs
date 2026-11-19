using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CapturedTraceEvent
{
	public string EventClassName { get; set; } = string.Empty;

	public string? EventSubclassName { get; set; }

	public string? TextData { get; set; }

	public string? DatabaseName { get; set; }

	public string? ActivityId { get; set; }

	public string? RequestId { get; set; }

	public string? SessionId { get; set; }

	public string? ApplicationName { get; set; }

	public DateTime? CurrentTime { get; set; }

	public DateTime? StartTime { get; set; }

	public long? Duration { get; set; }

	public long? CpuTime { get; set; }

	public DateTime? EndTime { get; set; }

	public string? NTUserName { get; set; }

	public string? RequestProperties { get; set; }

	public string? RequestParameters { get; set; }

	public string? ObjectName { get; set; }

	public string? ObjectPath { get; set; }

	public string? ObjectReference { get; set; }

	public string? Spid { get; set; }

	public long? IntegerData { get; set; }

	public long? ProgressTotal { get; set; }

	public string? ObjectId { get; set; }

	public string? Error { get; set; }

	public long NetParallelDuration { get; set; }

	public bool InternalBatchEvent { get; set; }
}
