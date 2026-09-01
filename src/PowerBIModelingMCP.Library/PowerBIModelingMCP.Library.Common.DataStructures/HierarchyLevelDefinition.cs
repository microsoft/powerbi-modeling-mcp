using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class HierarchyLevelDefinition : UserHierarchyReference
{
	[Required]
	public required string Name { get; set; }

	[Description("Required for create, optional for update")]
	public string? ColumnName { get; set; }

	public string? Description { get; set; }

	[Description("0-based")]
	public int? Ordinal { get; set; }

	public string? LineageTag { get; set; }

	public string? SourceLineageTag { get; set; }

	[Description("null=skip, empty=clear, values=set/replace")]
	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	[Description("null=skip, empty=clear, values=set/replace")]
	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
