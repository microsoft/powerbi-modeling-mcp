using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class PerspectiveOperationRequest
{
	[YamlFieldDescription("perspective_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("perspective_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("perspective_operations", "Definitions")]
	public List<PerspectiveDefinition>? Definitions { get; set; }

	[YamlFieldDescription("perspective_operations", "References")]
	public List<PerspectiveReference>? References { get; set; }

	[YamlFieldDescription("perspective_operations", "RenameDefinitions")]
	public List<PerspectiveRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("perspective_operations", "Filter")]
	public PerspectiveSubObjectListFilter? Filter { get; set; }

	[YamlFieldDescription("perspective_operations", "PerspectiveName")]
	public string? PerspectiveName { get; set; }

	[YamlFieldDescription("perspective_operations", "Tables")]
	public List<PerspectiveTableDefinition>? Tables { get; set; }

	[YamlFieldDescription("perspective_operations", "Columns")]
	public List<PerspectiveColumnDefinition>? Columns { get; set; }

	[YamlFieldDescription("perspective_operations", "Measures")]
	public List<PerspectiveMeasureDefinition>? Measures { get; set; }

	[YamlFieldDescription("perspective_operations", "Hierarchies")]
	public List<PerspectiveHierarchyDefinition>? Hierarchies { get; set; }

	[YamlFieldDescription("perspective_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("perspective_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
