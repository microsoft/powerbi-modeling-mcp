using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Core;

public class TraceStartRequest
{
	public List<string>? Events { get; set; }

	public bool FilterCurrentSessionOnly { get; set; } = true;
}
