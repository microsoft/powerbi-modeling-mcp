using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ModelRoleReference
{
	[Required]
	public required string Name { get; set; }
}
