using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class FunctionBase
{
	[Required]
	public required string Name { get; set; }

	public string? Expression { get; set; }

	public string? Description { get; set; }

	public bool? IsHidden { get; set; }

	public string? LineageTag { get; set; }

	public string? SourceLineageTag { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }

	[Description("Ready, SemanticError, SyntaxError")]
	public string? State { get; set; }

	public string? ErrorMessage { get; set; }

	public DateTime? ModifiedTime { get; set; }

	public DateTime? StructureModifiedTime { get; set; }
}
