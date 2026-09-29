using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class MeasureMove
{
	[Required]
	public required string Name { get; set; }

	[Description("Optional if measure name is unique")]
	public string? CurrentTableName { get; set; }

	[Required]
	public required string DestinationTableName { get; set; }
}
