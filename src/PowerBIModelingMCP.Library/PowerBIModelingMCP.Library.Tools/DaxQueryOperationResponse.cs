using System.ComponentModel;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class DaxQueryOperationResponse : OperationResponseBase
{
	[Description("Populated when GetExecutionMetrics is true")]
	public QueryExecutionMetrics? ExecutionMetrics { get; set; }
}
