using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Core;

public class DaxQueryExecute
{
	public required string Query { get; set; }

	public int? TimeoutSeconds { get; set; }

	public int? MaxRows { get; set; }

	public bool ReturnRows { get; set; } = true;

	public DaxQueryImpersonationOptions? Impersonation { get; set; }
}
