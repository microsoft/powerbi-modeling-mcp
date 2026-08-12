using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class NamedExpressionList : ObjectListBase
{
	[Description("M for PowerQuery, DAX for calculations")]
	public string? Kind { get; set; }

	public string? QueryGroupName { get; set; }
}
