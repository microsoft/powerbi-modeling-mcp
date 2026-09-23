using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ColumnList : ObjectListBase
{
	public string? DataType { get; set; }

	public bool? IsCalculated { get; set; }

	public string? DisplayFolder { get; set; }

	public bool? IsHidden { get; set; }

	[Description("Aggregation function: Sum, Average, Min, Max, Count, DistinctCount, None")]
	public string? SummarizeBy { get; set; }

	public string? FormatString { get; set; }
}
