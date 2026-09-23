using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class MeasureOperationRequest
{
	[YamlFieldDescription("measure_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("measure_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("measure_operations", "Definitions")]
	public List<MeasureDefinition>? Definitions { get; set; }

	[YamlFieldDescription("measure_operations", "References")]
	public List<MeasureReference>? References { get; set; }

	[YamlFieldDescription("measure_operations", "Filter")]
	public TableScopedListFilter? Filter { get; set; }

	[YamlFieldDescription("measure_operations", "ShouldCascadeDelete")]
	public bool ShouldCascadeDelete { get; set; } = true;

	[YamlFieldDescription("measure_operations", "RenameDefinitions")]
	public List<MeasureRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("measure_operations", "MoveDefinitions")]
	public List<MeasureMove>? MoveDefinitions { get; set; }

	[YamlFieldDescription("measure_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("measure_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
