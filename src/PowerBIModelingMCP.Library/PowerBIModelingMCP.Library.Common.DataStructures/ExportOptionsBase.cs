using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public abstract class ExportOptionsBase
{
	[Description("Maximum characters to return (-1=no limit, 0=don't return, >0=limit). Default: 10000")]
	public int MaxReturnCharacters { get; set; } = 10000;
}
