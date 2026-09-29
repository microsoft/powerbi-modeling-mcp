using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmslOperationResult : ResultBase
{
	public string TmslScript { get; set; } = string.Empty;

	public TmslOperationType OperationType { get; set; }

	public string ObjectName { get; set; } = string.Empty;

	public string ObjectType { get; set; } = string.Empty;

	public string? ErrorMessage { get; set; }

	public ErrorSource? ErrorSource { get; set; }

	public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
