using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class HierarchyLevelRenameDefinition : UserHierarchyReference
{
	[Required]
	public required string CurrentLevelName { get; set; }

	[Required]
	public required string NewLevelName { get; set; }
}
