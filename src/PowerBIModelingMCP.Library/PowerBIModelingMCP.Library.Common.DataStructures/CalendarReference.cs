using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarReference
{
	[Required]
	public required string Name { get; set; }

	[Required]
	public required string TableName { get; set; }
}
