using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class HierarchyList : ObjectListBase
{
	public List<LevelList> Levels { get; set; } = new List<LevelList>();

	public string? DisplayFolder { get; set; }

	public bool? IsHidden { get; set; }
}
