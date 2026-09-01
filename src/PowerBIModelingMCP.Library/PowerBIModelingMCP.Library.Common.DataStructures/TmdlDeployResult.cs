using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmdlDeployResult : ResultBase
{
	public string SourceConnectionName { get; set; } = string.Empty;

	public string TargetConnectionName { get; set; } = string.Empty;

	public string SourceDatabaseName { get; set; } = string.Empty;

	public string TargetDatabaseName { get; set; } = string.Empty;

	public DateTime DeploymentTimestamp { get; set; }

	public string? Message { get; set; }
}
