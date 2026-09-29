using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class UserHierarchyOperationRequest
{
	[YamlFieldDescription("user_hierarchy_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("user_hierarchy_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "Definitions")]
	public List<HierarchyDefinition>? Definitions { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "References")]
	public List<UserHierarchyReference>? References { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "RenameDefinitions")]
	public List<UserHierarchyRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "LevelDefinitions")]
	public List<HierarchyLevelDefinition>? LevelDefinitions { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "LevelReferences")]
	public List<HierarchyLevelReference>? LevelReferences { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "LevelRenameDefinitions")]
	public List<HierarchyLevelRenameDefinition>? LevelRenameDefinitions { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "ReorderLevelsDefinitions")]
	public List<UserHierarchyReorderLevels>? ReorderLevelsDefinitions { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "Filter")]
	public TableScopedListFilter? Filter { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "ShouldCascadeDelete")]
	public bool ShouldCascadeDelete { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("user_hierarchy_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
