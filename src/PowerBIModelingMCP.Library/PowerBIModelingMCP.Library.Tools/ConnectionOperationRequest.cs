using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;

namespace PowerBIModelingMCP.Library.Tools;

public class ConnectionOperationRequest
{
	[Required]
	[YamlFieldDescription("connection_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("connection_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[YamlFieldDescription("connection_operations", "ConnectionString")]
	public string? ConnectionString { get; set; }

	[YamlFieldDescription("connection_operations", "DataSource")]
	public string? DataSource { get; set; }

	[YamlFieldDescription("connection_operations", "InitialCatalog")]
	public string? InitialCatalog { get; set; }

	[YamlFieldDescription("connection_operations", "WorkspaceName")]
	public string? WorkspaceName { get; set; }

	[YamlFieldDescription("connection_operations", "SemanticModelName")]
	public string? SemanticModelName { get; set; }

	[YamlFieldDescription("connection_operations", "TenantName")]
	public string? TenantName { get; set; }

	[YamlFieldDescription("connection_operations", "ClearCredential")]
	public bool ClearCredential { get; set; }

	[YamlFieldDescription("connection_operations", "FolderPath")]
	public string? FolderPath { get; set; }

	[YamlFieldDescription("connection_operations", "BimFilePath")]
	public string? BimFilePath { get; set; }
}
