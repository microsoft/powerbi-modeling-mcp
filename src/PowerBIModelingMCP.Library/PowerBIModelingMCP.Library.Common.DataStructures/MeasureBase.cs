using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class MeasureBase
{
	public string? TableName { get; set; }

	public required string Name { get; set; }

	public string? Expression { get; set; }

	public string? Description { get; set; }

	public string? FormatString { get; set; }

	public bool? IsHidden { get; set; }

	public bool? IsSimpleMeasure { get; set; }

	public string? DisplayFolder { get; set; }

	public string? DataType { get; set; }

	public string? DataCategory { get; set; }

	public string? LineageTag { get; set; }

	public string? SourceLineageTag { get; set; }

	public string? KPI { get; set; }

	public string? DetailRowsExpression { get; set; }

	public string? FormatStringExpression { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }

	public string? State { get; set; }

	public string? ErrorMessage { get; set; }

	public DateTime? ModifiedTime { get; set; }

	public DateTime? StructureModifiedTime { get; set; }
}
