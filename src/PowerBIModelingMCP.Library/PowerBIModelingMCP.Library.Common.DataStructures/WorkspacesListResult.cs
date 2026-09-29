using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class WorkspacesListResult
{
	public List<FabricWorkspaceGet> Workspaces { get; set; } = new List<FabricWorkspaceGet>();

	public int Count { get; set; }
}
