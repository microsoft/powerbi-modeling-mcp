using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ItemsListResult
{
	public List<FabricItemGet> Items { get; set; } = new List<FabricItemGet>();

	public int Count { get; set; }
}
