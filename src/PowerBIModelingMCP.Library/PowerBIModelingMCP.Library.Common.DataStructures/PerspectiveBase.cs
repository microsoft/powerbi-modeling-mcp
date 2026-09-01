using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveBase
{
	public string? Name { get; set; }

	public string? Description { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<KeyValuePair<string, string>>? ExtendedProperties { get; set; }
}
