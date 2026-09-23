using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ColumnReference
{
	[Required]
	public required string TableName { get; set; }

	[Required]
	public required string Name { get; set; }
}
