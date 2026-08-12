using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TableGet : TableBase
{
	public ulong? ID { get; set; }

	public ModeType? Mode { get; set; }

	public List<string> Columns { get; set; } = new List<string>();

	public List<string> Measures { get; set; } = new List<string>();

	public List<string> Hierarchies { get; set; } = new List<string>();

	public List<PartitionGet> PartitionDetails { get; set; } = new List<PartitionGet>();
}
