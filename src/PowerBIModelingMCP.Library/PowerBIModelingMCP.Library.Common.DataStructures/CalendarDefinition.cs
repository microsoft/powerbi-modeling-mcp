using System.Collections.Generic;
using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarDefinition : CalendarBase
{
	[Description("Only used during Create")]
	public List<CalendarColumnGroupDefinition>? InitialColumnGroups { get; set; }
}
