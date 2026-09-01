namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationItemOperationResult
{
	public string State { get; set; } = string.Empty;

	public string? ErrorMessage { get; set; }

	public string CalculationItemName { get; set; } = string.Empty;

	public string CalculationGroupName { get; set; } = string.Empty;

	public int Ordinal { get; set; }

	public bool HasChanges { get; set; }
}
