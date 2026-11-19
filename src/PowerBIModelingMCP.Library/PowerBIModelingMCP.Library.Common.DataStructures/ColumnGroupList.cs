using System.Collections.Generic;
using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ColumnGroupList : ObjectListBase
{
	[Description("TimeRelated or TimeUnitAssociation")]
	public string? GroupType { get; set; }

	public List<string>? ColumnNames { get; set; }

	[Description("For TimeUnitAssociation")]
	public string? PrimaryColumnName { get; set; }
}
