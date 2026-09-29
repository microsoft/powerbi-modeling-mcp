using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DataSourceBase
{
	public string? Name { get; set; }

	public string? Description { get; set; }

	public string? ConnectionString { get; set; }

	public string? ImpersonationMode { get; set; }

	public string? Password { get; set; }

	public string? Account { get; set; }

	public int? MaxConnections { get; set; }

	public string? Isolation { get; set; }

	public int? Timeout { get; set; }

	public string? Provider { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
