using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public abstract class ListFilterBase
{
	[Description("Default: 200")]
	public int? MaxResults { get; set; } = 200;
}
