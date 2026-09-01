using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalculationGroupList : ObjectListBase
{
	public List<CalculationItemList>? CalculationItems { get; set; }

	public bool? IsHidden { get; set; }
}
