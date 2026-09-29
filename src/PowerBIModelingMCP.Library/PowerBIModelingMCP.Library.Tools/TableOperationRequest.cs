using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class TableOperationRequest
{
	[YamlFieldDescription("table_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("table_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("table_operations", "Definitions")]
	public List<TableDefinition>? Definitions { get; set; }

	[YamlFieldDescription("table_operations", "References")]
	public List<TableReference>? References { get; set; }

	[YamlFieldDescription("table_operations", "RenameDefinitions")]
	public List<TableRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("table_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("table_operations", "TmslExportOptions")]
	public ExportTmsl? TmslExportOptions { get; set; }

	[YamlFieldDescription("table_operations", "MarkAsDateTableDefinitions")]
	public List<MarkAsDateTableDefinition>? MarkAsDateTableDefinitions { get; set; }

	[YamlFieldDescription("table_operations", "FieldParameterDefinitions")]
	public List<FieldParameterDefinition>? FieldParameterDefinitions { get; set; }

	[YamlFieldDescription("table_operations", "ShouldCascadeDelete")]
	public bool ShouldCascadeDelete { get; set; } = true;

	[YamlFieldDescription("table_operations", "RefreshType")]
	public string? RefreshType { get; set; }

	[YamlFieldDescription("table_operations", "RequestId")]
	public string? RequestId { get; set; }

	[YamlFieldDescription("table_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
