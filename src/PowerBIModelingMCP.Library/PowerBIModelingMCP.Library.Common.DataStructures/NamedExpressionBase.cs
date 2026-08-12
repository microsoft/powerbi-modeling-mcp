using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class NamedExpressionBase
{
	[Required]
	public required string Name { get; set; }

	public string? Expression { get; set; }

	[Description("M for PowerQuery, DAX for calculations")]
	public string? Kind { get; set; }

	public string? Description { get; set; }

	public string? LineageTag { get; set; }

	public string? SourceLineageTag { get; set; }

	public string? QueryGroupName { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }

	public string? State { get; set; }

	public string? ErrorMessage { get; set; }

	public DateTime? ModifiedTime { get; set; }
}
