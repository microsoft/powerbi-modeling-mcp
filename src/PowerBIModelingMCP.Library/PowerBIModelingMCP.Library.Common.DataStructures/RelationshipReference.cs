using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class RelationshipReference
{
	[Required]
	public required string Name { get; set; }
}
