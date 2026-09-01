using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class BindingInfo
{
	public string Type { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public string? ConnectionId { get; set; }

	public string? TargetDataSourceReferenceName { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
