using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationGroupOperationResult
{
	public string CalculationGroupName { get; set; } = string.Empty;

	public int CalculationItemCount { get; set; }

	public List<CalculationItemOperationResult> CalculationItems { get; set; } = new List<CalculationItemOperationResult>();

	public bool HasChanges { get; set; }
}
