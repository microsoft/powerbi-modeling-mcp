using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PartitionRename
{
	[Required]
	public required string TableName { get; set; }

	[Description("Optional if table has only one partition")]
	public string? CurrentName { get; set; }

	[Required]
	public required string NewName { get; set; }
}
