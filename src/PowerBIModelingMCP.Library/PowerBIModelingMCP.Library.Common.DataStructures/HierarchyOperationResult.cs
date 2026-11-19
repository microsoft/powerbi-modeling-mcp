using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class HierarchyOperationResult
{
	public string? State { get; set; }

	public string? HierarchyName { get; set; }

	public string? TableName { get; set; }

	public int LevelCount { get; set; }

	public List<string> LevelNames { get; set; } = new List<string>();

	public bool HasChanges { get; set; }
}
