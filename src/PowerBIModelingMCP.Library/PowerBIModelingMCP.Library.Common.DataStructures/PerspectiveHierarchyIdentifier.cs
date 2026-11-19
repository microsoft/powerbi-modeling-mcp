using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveHierarchyIdentifier
{
	[Required]
	public required string TableName { get; set; }

	[Required]
	public required string HierarchyName { get; set; }
}
