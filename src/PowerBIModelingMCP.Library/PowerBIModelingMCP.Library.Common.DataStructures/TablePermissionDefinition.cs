using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TablePermissionDefinition
{
	public string? RoleName { get; set; }

	public string? TableName { get; set; }

	public string? FilterExpression { get; set; }

	public string? MetadataPermission { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
