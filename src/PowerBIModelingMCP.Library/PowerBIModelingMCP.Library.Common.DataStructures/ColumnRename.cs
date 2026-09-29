using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ColumnRename : ObjectRenameBase
{
	[Required]
	public required string TableName { get; set; }
}
