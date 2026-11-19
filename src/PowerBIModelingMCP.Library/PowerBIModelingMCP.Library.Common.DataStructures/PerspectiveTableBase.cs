using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveTableBase
{
	public string? Name { get; set; }

	public string? TableName { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<KeyValuePair<string, string>>? ExtendedProperties { get; set; }

	public bool? IncludeAll { get; set; }
}
