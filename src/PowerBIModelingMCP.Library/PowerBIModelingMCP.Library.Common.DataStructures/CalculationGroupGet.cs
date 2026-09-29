using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationGroupGet : CalculationGroupBase
{
	public DateTime? ModifiedTime { get; set; }

	public DateTime? StructureModifiedTime { get; set; }

	public List<CalculationItemGet> CalculationItems { get; set; } = new List<CalculationItemGet>();
}
