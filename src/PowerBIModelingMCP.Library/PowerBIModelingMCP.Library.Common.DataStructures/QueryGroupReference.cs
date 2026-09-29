using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class QueryGroupReference
{
	[Required]
	public required string Name { get; set; }
}
