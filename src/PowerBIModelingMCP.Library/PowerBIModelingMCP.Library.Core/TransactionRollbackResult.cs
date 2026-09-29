using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Core;

public class TransactionRollbackResult
{
	public string TransactionId { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public int OperationCount { get; set; }

	public double Duration { get; set; }

	public List<string> Operations { get; set; } = new List<string>();

	public string TransactionType { get; set; } = string.Empty;
}
