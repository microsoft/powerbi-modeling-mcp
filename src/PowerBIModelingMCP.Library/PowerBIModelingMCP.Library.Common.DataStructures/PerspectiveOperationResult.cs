namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveOperationResult : ResultBase
{
	public string PerspectiveName { get; set; } = string.Empty;

	public string? Message { get; set; }

	public bool HasChanges { get; set; }
}
