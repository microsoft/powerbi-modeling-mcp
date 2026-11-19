using System.ComponentModel;
using Microsoft.AnalysisServices.Tabular;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmslOperationRequest
{
	public TmslOperationType OperationType { get; set; }

	[Description("Full, ClearValues, Calculate, DataOnly, Automatic, Add, Defragment. For Refresh only")]
	public RefreshType? RefreshType { get; set; }

	public bool IncludeRestricted { get; set; }
}
