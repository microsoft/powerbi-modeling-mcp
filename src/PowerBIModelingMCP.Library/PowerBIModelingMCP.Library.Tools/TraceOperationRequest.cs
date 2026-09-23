using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;

namespace PowerBIModelingMCP.Library.Tools;

public class TraceOperationRequest
{
	[Required]
	[YamlFieldDescription("trace_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("trace_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[YamlFieldDescription("trace_operations", "Events")]
	public List<string>? Events { get; set; }

	[YamlFieldDescription("trace_operations", "FilterCurrentSessionOnly")]
	public bool? FilterCurrentSessionOnly { get; set; }

	[YamlFieldDescription("trace_operations", "ClearAfterFetch")]
	public bool? ClearAfterFetch { get; set; }

	[YamlFieldDescription("trace_operations", "FilePath")]
	public string? FilePath { get; set; }

	[YamlFieldDescription("trace_operations", "Columns")]
	public List<string>? Columns { get; set; }
}
