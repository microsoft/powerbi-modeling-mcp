using System.Collections.Generic;
using System.ComponentModel;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class DaxQueryExecuteMetadata
{
	public IList<string>? Warnings { get; set; }

	[Description("Populated when GetExecutionMetrics is true")]
	public QueryExecutionMetrics? ExecutionMetrics { get; set; }
}
