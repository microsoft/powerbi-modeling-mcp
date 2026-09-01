using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CalendarColumnGroupDefinition
{
	[Description("Required for Create/Update/Delete/Get")]
	public string? CalendarName { get; set; }

	[Description("The table containing the calendar")]
	public string? TableName { get; set; }

	[Required]
	[Description("TimeRelated or TimeUnitAssociation")]
	public required string GroupType { get; set; }

	[Description("Required for TimeUnitAssociation: Year, Semester, Quarter, Month, Week, Date, or a compound form like MonthOfYear, DayOfWeek, etc.")]
	public string? TimeUnit { get; set; }

	[Description("When GroupType='TimeRelated'")]
	public TimeRelatedColumnGroupInfo? TimeRelatedGroup { get; set; }

	[Description("When GroupType='TimeUnitAssociation'")]
	public TimeUnitColumnAssociationInfo? TimeUnitAssociation { get; set; }

	[Description("Read-only")]
	public DateTime? ModifiedTime { get; set; }
}
