using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class ColumnOperationRequest
{
	[YamlFieldDescription("column_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("column_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("column_operations", "Definitions")]
	public List<ColumnDefinition>? Definitions { get; set; }

	[YamlFieldDescription("column_operations", "References")]
	public List<ColumnReference>? References { get; set; }

	[YamlFieldDescription("column_operations", "Filter")]
	public TableScopedListFilter? Filter { get; set; }

	[YamlFieldDescription("column_operations", "ShouldCascadeDelete")]
	public bool ShouldCascadeDelete { get; set; } = true;

	[YamlFieldDescription("column_operations", "RenameDefinitions")]
	public List<ColumnRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("column_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("column_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
