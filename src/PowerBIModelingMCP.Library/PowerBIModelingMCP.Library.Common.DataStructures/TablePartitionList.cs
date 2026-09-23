using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TablePartitionList
{
	public required string TableName { get; set; }

	public required List<PartitionList> Partitions { get; set; }
}
