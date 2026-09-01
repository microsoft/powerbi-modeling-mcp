using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ConnectionGet
{
	public string ConnectionName { get; set; } = string.Empty;

	public string? DatabaseName { get; set; }

	public string? ServerName { get; set; }

	public bool IsCloudConnection { get; set; }

	public bool IsOffline { get; set; }

	public string? SourcePath { get; set; }

	public DateTime? ConnectedAt { get; set; }

	public DateTime? LastUsedAt { get; set; }

	public bool HasTransaction { get; set; }

	public bool HasTrace { get; set; }

	public string? SessionId { get; set; }

	public string? SemanticModelId { get; set; }
}
