using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class FieldParameterFieldDefinition
{
	public string? TableName { get; set; }

	[Required]
	public required string Name { get; set; }

	[Required]
	public required string ObjectType { get; set; }

	public string? DisplayName { get; set; }
}
