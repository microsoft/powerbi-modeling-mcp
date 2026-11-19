using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TableReference
{
	[Required]
	public required string Name { get; set; }
}
