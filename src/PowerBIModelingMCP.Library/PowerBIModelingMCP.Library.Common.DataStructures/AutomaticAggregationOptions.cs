namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class AutomaticAggregationOptions
{
	public long? AggregationTableMaxRows { get; set; }

	public long? AggregationTableSizeLimit { get; set; }

	public long? DetailTableMinRows { get; set; }

	public double? QueryCoverage { get; set; }
}
