using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class MarkAsDateTableDefinition
{
	[Required]
	public required string TableName { get; set; }

	[Description("Auto-detected if omitted")]
	public string? DateColumnName { get; set; }
}
