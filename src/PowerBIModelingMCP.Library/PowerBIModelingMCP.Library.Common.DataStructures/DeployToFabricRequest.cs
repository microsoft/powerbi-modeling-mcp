using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DeployToFabricRequest
{
	public string? TargetConnectionString { get; set; }

	public string? TargetWorkspaceName { get; set; }

	[Description("Default: 'myorg'")]
	public string? TargetTenantName { get; set; }

	public string? NewDatabaseName { get; set; }

	public bool? IncludeRestricted { get; set; }

	public int? ConnectTimeoutSeconds { get; set; }

	[Description("Forces fresh authentication. Default: false")]
	public bool ClearCredential { get; set; }
}
