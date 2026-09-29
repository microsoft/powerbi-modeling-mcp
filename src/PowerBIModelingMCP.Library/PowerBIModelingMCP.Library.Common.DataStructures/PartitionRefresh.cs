using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PartitionRefresh
{
	[Required]
	public required string TableName { get; set; }

	[Description("Optional if table has only one partition")]
	public string? Name { get; set; }

	[Description("Automatic, Full, ClearValues, Calculate, DataOnly, Defragment. Default: Automatic")]
	public string? RefreshType { get; set; } = "Automatic";
}
