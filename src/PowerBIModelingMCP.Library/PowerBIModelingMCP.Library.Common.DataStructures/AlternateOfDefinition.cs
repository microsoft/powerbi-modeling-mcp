using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class AlternateOfDefinition
{
	public string? BaseTable { get; set; }

	public string? BaseColumn { get; set; }

	public string? Summarization { get; set; }

	public List<KeyValuePair<string, string>> Annotations { get; set; } = new List<KeyValuePair<string, string>>();
}
