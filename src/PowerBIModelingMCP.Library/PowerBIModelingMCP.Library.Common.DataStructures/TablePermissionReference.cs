using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TablePermissionReference
{
	[Required]
	public required string RoleName { get; set; }

	[Required]
	public required string TableName { get; set; }
}
