using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveReference
{
	[Required]
	public required string Name { get; set; }
}
