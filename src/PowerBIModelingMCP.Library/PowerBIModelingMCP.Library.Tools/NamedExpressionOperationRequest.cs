using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class NamedExpressionOperationRequest
{
	[YamlFieldDescription("named_expression_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("named_expression_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("named_expression_operations", "Definitions")]
	public List<NamedExpressionDefinition>? Definitions { get; set; }

	[YamlFieldDescription("named_expression_operations", "References")]
	public List<NamedExpressionReference>? References { get; set; }

	[YamlFieldDescription("named_expression_operations", "RenameDefinitions")]
	public List<NamedExpressionRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("named_expression_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("named_expression_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
