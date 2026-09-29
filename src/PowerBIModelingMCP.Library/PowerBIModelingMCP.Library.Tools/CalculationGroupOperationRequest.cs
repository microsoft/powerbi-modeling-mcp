using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class CalculationGroupOperationRequest
{
	[YamlFieldDescription("calculation_group_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("calculation_group_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("calculation_group_operations", "GroupDefinitions")]
	public List<CalculationGroupDefinition>? GroupDefinitions { get; set; }

	[YamlFieldDescription("calculation_group_operations", "GroupReferences")]
	public List<CalculationGroupReference>? GroupReferences { get; set; }

	[YamlFieldDescription("calculation_group_operations", "RenameGroupDefinitions")]
	public List<CalculationGroupRename>? RenameGroupDefinitions { get; set; }

	[YamlFieldDescription("calculation_group_operations", "ItemDefinitions")]
	public List<CalculationItemDefinition>? ItemDefinitions { get; set; }

	[YamlFieldDescription("calculation_group_operations", "ItemReferences")]
	public List<CalculationItemReference>? ItemReferences { get; set; }

	[YamlFieldDescription("calculation_group_operations", "RenameItemsDefinitions")]
	public List<CalculationItemRename>? RenameItemsDefinitions { get; set; }

	[YamlFieldDescription("calculation_group_operations", "ReorderDefinitions")]
	public List<CalculationItemReorder>? ReorderDefinitions { get; set; }

	[YamlFieldDescription("calculation_group_operations", "ItemFilter")]
	public CalculationItemListFilter? ItemFilter { get; set; }

	[YamlFieldDescription("calculation_group_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("calculation_group_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
