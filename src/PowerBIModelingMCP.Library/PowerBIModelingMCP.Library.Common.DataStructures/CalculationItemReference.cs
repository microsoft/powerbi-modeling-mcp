using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationItemReference
{
	[Required]
	public required string CalculationGroupName { get; set; }

	[Required]
	public required string Name { get; set; }
}
