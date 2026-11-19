using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveColumnIdentifier
{
	[Required]
	public required string TableName { get; set; }

	[Required]
	public required string ColumnName { get; set; }
}
