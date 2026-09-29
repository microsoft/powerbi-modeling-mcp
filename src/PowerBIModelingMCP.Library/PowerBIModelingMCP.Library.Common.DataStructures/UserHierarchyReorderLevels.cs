using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class UserHierarchyReorderLevels : UserHierarchyReference
{
	[Required]
	public required List<string> LevelNamesInOrder { get; set; }
}
