using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationGroupBase
{
	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public bool? IsHidden { get; set; }

	public int? Precedence { get; set; }

	public CalculationGroupExpressionInfo? MultipleOrEmptySelectionExpression { get; set; }

	public CalculationGroupExpressionInfo? NoSelectionExpression { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }
}
