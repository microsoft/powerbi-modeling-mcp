using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TableMeasureList
{
	public required string TableName { get; set; }

	public required List<MeasureList> Measures { get; set; }
}
