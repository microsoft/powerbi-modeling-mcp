using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Core;

public class TransactionStatusResult
{
	public string? TransactionId { get; set; }

	public string Status { get; set; } = string.Empty;

	public string? StartTime { get; set; }

	public double? Duration { get; set; }

	public int? OperationCount { get; set; }

	public List<string>? Operations { get; set; }

	public string? TransactionType { get; set; }
}
