using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class NamedExpressionReference
{
	[Required]
	public required string Name { get; set; }
}
