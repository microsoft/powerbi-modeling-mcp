using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class DatabaseOperationRequest
{
	[YamlFieldDescription("database_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("database_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("database_operations", "UpdateDefinition")]
	public DatabaseUpdate? UpdateDefinition { get; set; }

	[YamlFieldDescription("database_operations", "CreateDefinition")]
	public DatabaseCreate? CreateDefinition { get; set; }

	[YamlFieldDescription("database_operations", "TmdlFolderPath")]
	public string? TmdlFolderPath { get; set; }

	[YamlFieldDescription("database_operations", "BimFilePath")]
	public string? BimFilePath { get; set; }

	[YamlFieldDescription("database_operations", "DeployToFabricRequest")]
	public DeployToFabricRequest? DeployToFabricRequest { get; set; }

	[YamlFieldDescription("database_operations", "TmdlExportOptions")]
	public DatabaseExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("database_operations", "TmslExportOptions")]
	public DatabaseExportTmsl? TmslExportOptions { get; set; }
}
