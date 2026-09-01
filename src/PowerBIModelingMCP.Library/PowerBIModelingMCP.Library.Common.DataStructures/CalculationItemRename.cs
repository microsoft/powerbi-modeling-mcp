using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationItemRename : ObjectRenameBase
{
	[Required]
	public required string CalculationGroupName { get; set; }
}
