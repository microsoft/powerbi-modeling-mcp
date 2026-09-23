using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class HierarchyLevelReference : UserHierarchyReference
{
	[Required]
	public required string LevelName { get; set; }
}
