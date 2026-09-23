namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DatabaseOperationResult
{
	public string State { get; set; } = string.Empty;

	public string? ErrorMessage { get; set; }

	public string DatabaseName { get; set; } = string.Empty;

	public bool HasChanges { get; set; }
}
