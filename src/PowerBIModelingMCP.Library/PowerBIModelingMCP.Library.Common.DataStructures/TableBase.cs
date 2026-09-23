using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TableBase
{
	public string? Name { get; set; }

	public string? DataCategory { get; set; }

	public string? Description { get; set; }

	public bool? IsHidden { get; set; }

	public bool? ShowAsVariationsOnly { get; set; }

	public bool? IsPrivate { get; set; }

	public int? AlternateSourcePrecedence { get; set; }

	public bool? ExcludeFromModelRefresh { get; set; }

	public string? LineageTag { get; set; }

	public string? SourceLineageTag { get; set; }

	public bool? SystemManaged { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
