using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarRename : ObjectRenameBase
{
	[Description("Search hint to locate calendar")]
	public string? TableName { get; set; }
}
