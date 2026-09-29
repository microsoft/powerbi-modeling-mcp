using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarColumnGroupListFilter : ListFilterBase
{
	[Required]
	[Description("Name of the calendar whose column groups to list")]
	public required string CalendarName { get; set; }

	[Description("Search hint")]
	public string? TableName { get; set; }
}
