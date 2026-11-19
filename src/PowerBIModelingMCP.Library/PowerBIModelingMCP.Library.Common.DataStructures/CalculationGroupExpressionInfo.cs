using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationGroupExpressionInfo
{
	public string? Expression { get; set; }

	public string? Description { get; set; }

	public string? FormatStringExpression { get; set; }

	public string? State { get; set; }

	public string? ErrorMessage { get; set; }

	public DateTime? ModifiedTime { get; set; }
}
