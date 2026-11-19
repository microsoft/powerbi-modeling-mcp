using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationItemGet : CalculationItemBase
{
	public string? CalculationGroupName { get; set; }

	public string? State { get; set; }

	public string? ErrorMessage { get; set; }

	public string? FormatStringExpressionState { get; set; }

	public string? FormatStringExpressionErrorMessage { get; set; }

	public DateTime? ModifiedTime { get; set; }
}
