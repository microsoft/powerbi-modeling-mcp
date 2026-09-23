using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TablePermissionGet : TablePermissionDefinition
{
	public string? State { get; set; }

	public string? ErrorMessage { get; set; }

	public DateTime? ModifiedTime { get; set; }
}
