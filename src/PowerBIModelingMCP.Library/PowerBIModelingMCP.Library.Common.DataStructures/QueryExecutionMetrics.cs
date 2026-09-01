namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class QueryExecutionMetrics
{
	public CalculatedExecutionMetrics? CalculatedExecutionMetrics { get; set; }

	public ReportedExecutionMetrics? ReportedExecutionMetrics { get; set; }
}
