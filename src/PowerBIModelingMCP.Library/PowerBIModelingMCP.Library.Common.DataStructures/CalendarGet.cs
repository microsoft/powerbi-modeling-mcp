using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarGet : CalendarBase
{
	public List<CalendarColumnGroupDefinition> CalendarColumnGroups { get; set; } = new List<CalendarColumnGroupDefinition>();
}
