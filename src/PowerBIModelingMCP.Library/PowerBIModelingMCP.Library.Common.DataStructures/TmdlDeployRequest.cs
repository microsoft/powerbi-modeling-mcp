using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmdlDeployRequest
{
	[Required]
	public required string SourceConnectionName { get; set; }

	[Required]
	public required string TargetConnectionName { get; set; }
}
