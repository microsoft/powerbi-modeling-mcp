using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class QueryGroupBase
{
	public string? Description { get; set; }

	public string? Folder { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }
}
