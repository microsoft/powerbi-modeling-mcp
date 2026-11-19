using System;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public class TransactionContext
{
	public string TransactionId { get; set; } = Guid.NewGuid().ToString();

	public DateTime StartTime { get; set; } = DateTime.UtcNow;

	public List<string> Operations { get; set; } = new List<string>();

	public List<PendingAuditEntry> PendingAuditEntries { get; set; } = new List<PendingAuditEntry>();

	public required ITabularServer Server { get; set; }

	public required Database Database { get; set; }
}
