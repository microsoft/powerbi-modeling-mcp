using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class RolePermissionListFilter : ListFilterBase
{
	[Required]
	public required string RoleName { get; set; }
}
