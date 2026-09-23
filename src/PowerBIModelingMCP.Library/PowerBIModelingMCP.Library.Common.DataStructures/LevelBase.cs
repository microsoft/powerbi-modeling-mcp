using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class LevelBase
{
	public string? Name { get; set; }

	public string? Description { get; set; }

	public int? Ordinal { get; set; }

	public string? ColumnName { get; set; }

	public string? LineageTag { get; set; }

	public string? SourceLineageTag { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
