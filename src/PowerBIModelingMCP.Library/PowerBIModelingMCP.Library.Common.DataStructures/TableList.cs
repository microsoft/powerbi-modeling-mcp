using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TableList : ObjectListBase
{
	public int? ColumnCount { get; set; }

	public int? MeasureCount { get; set; }

	public int? HierarchyCount { get; set; }

	public int? PartitionCount { get; set; }

	public int? CalendarCount { get; set; }

	public bool? IsHidden { get; set; }

	public bool? IsPrivate { get; set; }

	public bool? ShowAsVariationsOnly { get; set; }

	public bool? IsCalculationGroup { get; set; }

	[Description("Import, DirectQuery, DirectLake, Dual. For hybrid tables, this may be a comma-separated combination (e.g. \"Import,DirectQuery\").")]
	public string? StorageMode { get; set; }
}
