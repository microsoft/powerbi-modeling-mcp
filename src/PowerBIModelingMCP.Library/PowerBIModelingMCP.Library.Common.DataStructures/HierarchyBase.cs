using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class HierarchyBase
{
	public string? TableName { get; set; }

	public string? Name { get; set; }

	public string? Description { get; set; }

	public bool? IsHidden { get; set; }

	public string? DisplayFolder { get; set; }

	public string? HideMembers { get; set; }

	public string? LineageTag { get; set; }

	public string? SourceLineageTag { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
