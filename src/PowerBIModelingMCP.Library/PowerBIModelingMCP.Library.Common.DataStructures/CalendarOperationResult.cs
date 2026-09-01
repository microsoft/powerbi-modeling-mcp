using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarOperationResult
{
	public string CalendarName { get; set; } = string.Empty;

	public string TableName { get; set; } = string.Empty;

	public int ColumnGroupCount { get; set; }

	public List<CalendarColumnGroupOperationResult> ColumnGroups { get; set; } = new List<CalendarColumnGroupOperationResult>();
}
