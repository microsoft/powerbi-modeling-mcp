namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DatabaseCreate : DatabaseBase
{
	public string? ModelName { get; set; }

	public bool? IsOffline { get; set; }
}
