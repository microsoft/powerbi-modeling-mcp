using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class QueryGroupOperationRequest
{
	[YamlFieldDescription("query_group_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("query_group_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("query_group_operations", "Definitions")]
	public List<QueryGroupDefinition>? Definitions { get; set; }

	[YamlFieldDescription("query_group_operations", "References")]
	public List<QueryGroupReference>? References { get; set; }

	[YamlFieldDescription("query_group_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("query_group_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
