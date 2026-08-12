using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public abstract class ObjectRenameBase
{
	[Required]
	public required string CurrentName { get; set; }

	[Required]
	public required string NewName { get; set; }
}
