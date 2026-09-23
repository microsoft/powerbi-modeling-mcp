using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Core;

public class ClearCacheResult : ResultBase
{
	public string? ErrorMessage { get; set; }

	public string DatabaseName { get; set; } = string.Empty;

	public string ConnectionName { get; set; } = string.Empty;

	public int RowsAffected { get; set; }
}
