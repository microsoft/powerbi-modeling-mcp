using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PartitionList : ObjectListBase
{
	public required string TableName { get; set; }

	[Description("M, Calculated, Query, PolicyRange, Entity")]
	public string? SourceType { get; set; }

	[Description("Import, DirectQuery, etc.")]
	public string? Mode { get; set; }

	public string? State { get; set; }
}
