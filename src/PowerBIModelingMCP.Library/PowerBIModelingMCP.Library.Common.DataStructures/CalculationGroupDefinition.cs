using System.Collections.Generic;
using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationGroupDefinition : CalculationGroupBase
{
	[Description("Only used during Create")]
	public List<CalculationItemDefinition>? CalculationItems { get; set; }
}
