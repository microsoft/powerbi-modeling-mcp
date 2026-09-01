using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class ModelOperationRequest
{
	[YamlFieldDescription("model_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("model_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("model_operations", "ModelName")]
	public string? ModelName { get; set; }

	[YamlFieldDescription("model_operations", "NewName")]
	public string? NewName { get; set; }

	[YamlFieldDescription("model_operations", "RefreshType")]
	public string? RefreshType { get; set; }

	[YamlFieldDescription("model_operations", "TableName")]
	public string? TableName { get; set; }

	[YamlFieldDescription("model_operations", "RequestId")]
	public string? RequestId { get; set; }

	[YamlFieldDescription("model_operations", "Definition")]
	public ModelDefinition? Definition { get; set; }

	[YamlFieldDescription("model_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }
}
