using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ObjectTranslationBase
{
	[Required]
	public required string CultureName { get; set; }

	[Required]
	public required string Property { get; set; }

	[Required]
	public required string ObjectType { get; set; }

	public string? ModelName { get; set; }

	public string? TableName { get; set; }

	public string? MeasureName { get; set; }

	public string? ColumnName { get; set; }

	public string? HierarchyName { get; set; }

	public string? LevelName { get; set; }
}
