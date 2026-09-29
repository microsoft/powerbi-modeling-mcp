using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarList : ObjectListBase
{
	public string? TableName { get; set; }

	public List<ColumnGroupList>? ColumnGroups { get; set; }
}
