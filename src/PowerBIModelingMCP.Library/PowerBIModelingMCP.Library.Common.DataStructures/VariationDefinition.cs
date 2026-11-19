using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class VariationDefinition
{
	public string RelationshipName { get; set; } = string.Empty;

	public string HiddenTableName { get; set; } = string.Empty;

	public string HierarchyName { get; set; } = string.Empty;

	public List<string> HierarchyLevelNames { get; set; } = new List<string>();
}
