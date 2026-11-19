using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ModelRoleBase
{
	public string? Name { get; set; }

	public string? Description { get; set; }

	public string? ModelPermission { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
