using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class FunctionOperationRequest
{
	[YamlFieldDescription("function_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("function_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("function_operations", "Definitions")]
	public List<FunctionDefinition>? Definitions { get; set; }

	[YamlFieldDescription("function_operations", "References")]
	public List<FunctionReference>? References { get; set; }

	[YamlFieldDescription("function_operations", "RenameDefinitions")]
	public List<FunctionRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("function_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("function_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
