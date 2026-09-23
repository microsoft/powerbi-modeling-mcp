using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Core;

internal class TraceEventDefinition
{
	public string Name { get; set; } = string.Empty;

	public string Category { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public HashSet<string> Columns { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
