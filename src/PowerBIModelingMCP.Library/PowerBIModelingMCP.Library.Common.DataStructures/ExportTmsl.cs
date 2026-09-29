using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ExportTmsl : ExportOptionsBase
{
	[Description("Create, CreateOrReplace, Alter, Delete, Refresh")]
	public string? TmslOperationType { get; set; }

	[Description("Full, ClearValues, Calculate, DataOnly, Automatic, Add, Defragment. For Refresh only")]
	public string? RefreshType { get; set; }

	[Description("Default: false")]
	public bool? IncludeRestricted { get; set; }

	[Description("Default: true")]
	public bool FormatJson { get; set; } = true;
}
