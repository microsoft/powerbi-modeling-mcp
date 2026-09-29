using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class CultureOperationRequest
{
	[YamlFieldDescription("culture_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("culture_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("culture_operations", "Definitions")]
	public List<CultureDefinition>? Definitions { get; set; }

	[YamlFieldDescription("culture_operations", "References")]
	public List<CultureReference>? References { get; set; }

	[YamlFieldDescription("culture_operations", "RenameDefinitions")]
	public List<CultureRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("culture_operations", "IncludeNeutralCultures")]
	public bool IncludeNeutralCultures { get; set; } = true;

	[YamlFieldDescription("culture_operations", "IncludeUserCustomCultures")]
	public bool IncludeUserCustomCultures { get; set; }

	[YamlFieldDescription("culture_operations", "LCID")]
	public int? LCID { get; set; }

	[YamlFieldDescription("culture_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("culture_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
