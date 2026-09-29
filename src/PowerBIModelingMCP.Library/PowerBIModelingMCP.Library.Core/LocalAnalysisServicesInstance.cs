using System;

namespace PowerBIModelingMCP.Library.Core;

public class LocalAnalysisServicesInstance
{
	public int ProcessId { get; set; }

	public int Port { get; set; }

	public string ConnectionString { get; set; } = string.Empty;

	public string? ParentProcessName { get; set; }

	public string? ParentWindowTitle { get; set; }

	public DateTime StartTime { get; set; }
}
