using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationItemReorder
{
	[Required]
	public required string CalculationGroupName { get; set; }

	[Required]
	public required List<string> ItemNamesInOrder { get; set; }
}
