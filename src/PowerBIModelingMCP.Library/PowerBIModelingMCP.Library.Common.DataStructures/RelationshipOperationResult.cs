using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class RelationshipOperationResult
{
	public string State { get; set; } = string.Empty;

	public string? ErrorMessage { get; set; }

	public string RelationshipName { get; set; } = string.Empty;

	public string FromTable { get; set; } = string.Empty;

	public string FromColumn { get; set; } = string.Empty;

	public string ToTable { get; set; } = string.Empty;

	public string ToColumn { get; set; } = string.Empty;

	public List<string>? Warnings { get; set; }

	public bool HasChanges { get; set; }
}
