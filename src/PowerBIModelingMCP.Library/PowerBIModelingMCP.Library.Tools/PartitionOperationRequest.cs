using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class PartitionOperationRequest
{
	[YamlFieldDescription("partition_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("partition_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("partition_operations", "Definitions")]
	public List<PartitionDefinition>? Definitions { get; set; }

	[YamlFieldDescription("partition_operations", "References")]
	public List<PartitionReference>? References { get; set; }

	[YamlFieldDescription("partition_operations", "RenameDefinitions")]
	public List<PartitionRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("partition_operations", "RefreshDefinitions")]
	public List<PartitionRefresh>? RefreshDefinitions { get; set; }

	[YamlFieldDescription("partition_operations", "Filter")]
	public PartitionListFilter? Filter { get; set; }

	[YamlFieldDescription("partition_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("partition_operations", "TmslExportOptions")]
	public ExportTmsl? TmslExportOptions { get; set; }

	[YamlFieldDescription("partition_operations", "RefreshType")]
	public string? RefreshType { get; set; }

	[YamlFieldDescription("partition_operations", "TableName")]
	public string? TableName { get; set; }

	[YamlFieldDescription("partition_operations", "PartitionName")]
	public string? PartitionName { get; set; }

	[YamlFieldDescription("partition_operations", "RequestId")]
	public string? RequestId { get; set; }

	[YamlFieldDescription("partition_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
