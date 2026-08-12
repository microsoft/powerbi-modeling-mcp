using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class DataSourceOperationRequest
{
	[YamlFieldDescription("data_source_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("data_source_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("data_source_operations", "Definitions")]
	public List<DataSourceDefinition>? Definitions { get; set; }

	[YamlFieldDescription("data_source_operations", "References")]
	public List<DataSourceReference>? References { get; set; }

	[YamlFieldDescription("data_source_operations", "RenameDefinitions")]
	public List<DataSourceRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("data_source_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("data_source_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
