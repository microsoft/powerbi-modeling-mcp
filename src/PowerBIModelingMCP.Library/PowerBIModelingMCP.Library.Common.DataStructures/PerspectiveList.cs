namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveList : ObjectListBase
{
	public int? TableCount { get; set; }

	public int? MeasureCount { get; set; }

	public int? ColumnCount { get; set; }

	public int? HierarchyCount { get; set; }
}
