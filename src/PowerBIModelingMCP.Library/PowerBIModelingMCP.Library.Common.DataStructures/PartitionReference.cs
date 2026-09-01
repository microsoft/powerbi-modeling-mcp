using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PartitionReference
{
	[Required]
	public required string TableName { get; set; }

	[Description("Optional if table has only one partition")]
	public string? Name { get; set; }
}
