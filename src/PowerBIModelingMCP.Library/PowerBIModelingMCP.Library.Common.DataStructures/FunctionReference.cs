using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class FunctionReference
{
	[Required]
	public required string Name { get; set; }
}
