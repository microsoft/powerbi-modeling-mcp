using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class RelationshipOperationRequest
{
	[YamlFieldDescription("relationship_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("relationship_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("relationship_operations", "Definitions")]
	public List<RelationshipDefinition>? Definitions { get; set; }

	[YamlFieldDescription("relationship_operations", "References")]
	public List<RelationshipReference>? References { get; set; }

	[YamlFieldDescription("relationship_operations", "RenameDefinitions")]
	public List<RelationshipRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("relationship_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("relationship_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
