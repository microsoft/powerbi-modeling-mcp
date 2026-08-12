namespace PowerBIModelingMCP.Library.Core;

public class ActiveTransactionInfo
{
	public string TransactionId { get; set; } = string.Empty;

	public string StartTime { get; set; } = string.Empty;

	public double Duration { get; set; }

	public int OperationCount { get; set; }

	public string Database { get; set; } = string.Empty;

	public string Server { get; set; } = string.Empty;

	public bool IsCurrent { get; set; }

	public string TransactionType { get; set; } = string.Empty;
}
