using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationItemBase
{
	public string? Name { get; set; }

	public string? Description { get; set; }

	public string? Expression { get; set; }

	public int? Ordinal { get; set; }

	public string? FormatStringExpression { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }
}
