using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class MeasureReference
{
	[Required]
	public required string Name { get; set; }

	[Description("Optional if measure name is unique")]
	public string? TableName { get; set; }
}
