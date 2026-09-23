using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DataSourceOperationResult
{
	public string DataSourceName { get; set; } = string.Empty;

	public string? ErrorMessage { get; set; }

	public List<string>? Warnings { get; set; }

	public bool HasChanges { get; set; }
}
