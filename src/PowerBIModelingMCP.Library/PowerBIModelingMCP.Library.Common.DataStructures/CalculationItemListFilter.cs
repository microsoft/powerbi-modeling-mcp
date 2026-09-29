using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationItemListFilter : ListFilterBase
{
	[Required]
	[Description("Name of the calculation group")]
	public required string CalculationGroupName { get; set; }
}
