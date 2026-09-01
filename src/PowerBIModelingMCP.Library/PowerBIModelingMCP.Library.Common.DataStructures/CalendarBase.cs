using System;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarBase
{
	[Required]
	public required string Name { get; set; }

	public string? Description { get; set; }

	[Required]
	public required string TableName { get; set; }

	public string? LineageTag { get; set; }

	public string? SourceLineageTag { get; set; }

	public DateTime? ModifiedTime { get; set; }
}
