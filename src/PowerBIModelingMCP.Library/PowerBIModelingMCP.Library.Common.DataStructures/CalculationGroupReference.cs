using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationGroupReference
{
	[Required]
	public required string Name { get; set; }
}
