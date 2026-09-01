using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class HierarchyDefinition : HierarchyBase
{
	public List<LevelDefinition>? Levels { get; set; }
}
