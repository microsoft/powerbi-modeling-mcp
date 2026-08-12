using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class UserHierarchyReference
{
	[Required]
	public required string TableName { get; set; }

	[Required]
	public required string HierarchyName { get; set; }
}
