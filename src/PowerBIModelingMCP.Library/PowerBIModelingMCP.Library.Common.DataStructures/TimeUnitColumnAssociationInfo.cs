using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TimeUnitColumnAssociationInfo
{
	[Required]
	[Description("Year, Semester, Quarter, Month, Week, Date, or a compound form like MonthOfYear, DayOfWeek, etc.")]
	public required string TimeUnit { get; set; }

	public string? PrimaryColumnName { get; set; }

	public List<string> AssociatedColumns { get; set; } = new List<string>();
}
