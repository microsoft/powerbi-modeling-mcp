namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class BimSerializeResult : ResultBase
{
	public string FilePath { get; set; } = string.Empty;

	public string DatabaseName { get; set; } = string.Empty;

	public string? Message { get; set; }
}
