using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TableColumnList
{
	public required string TableName { get; set; }

	public required List<ColumnList> Columns { get; set; }
}
