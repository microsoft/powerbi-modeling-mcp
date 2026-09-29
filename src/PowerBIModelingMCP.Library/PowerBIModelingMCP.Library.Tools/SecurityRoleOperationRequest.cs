using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class SecurityRoleOperationRequest
{
	[YamlFieldDescription("security_role_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("security_role_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("security_role_operations", "Definitions")]
	public List<ModelRoleDefinition>? Definitions { get; set; }

	[YamlFieldDescription("security_role_operations", "References")]
	public List<ModelRoleReference>? References { get; set; }

	[YamlFieldDescription("security_role_operations", "RenameDefinitions")]
	public List<ModelRoleRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("security_role_operations", "PermissionDefinitions")]
	public List<TablePermissionDefinition>? PermissionDefinitions { get; set; }

	[YamlFieldDescription("security_role_operations", "PermissionReferences")]
	public List<TablePermissionReference>? PermissionReferences { get; set; }

	[YamlFieldDescription("security_role_operations", "PermissionFilter")]
	public RolePermissionListFilter? PermissionFilter { get; set; }

	[YamlFieldDescription("security_role_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("security_role_operations", "TmslExportOptions")]
	public ExportTmsl? TmslExportOptions { get; set; }

	[YamlFieldDescription("security_role_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
