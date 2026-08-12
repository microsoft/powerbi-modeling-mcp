namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarColumnGroupOperationResult
{
	public string CalendarName { get; set; } = string.Empty;

	public string GroupType { get; set; } = string.Empty;

	public int GroupIndex { get; set; }

	public int ColumnCount { get; set; }

	public string? TimeUnit { get; set; }

	public string? PrimaryColumnName { get; set; }
}
