using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class RelationshipBase
{
	public string? Name { get; set; }

	public bool? IsActive { get; set; }

	public string? Type { get; set; }

	public string? CrossFilteringBehavior { get; set; }

	public string? JoinOnDateBehavior { get; set; }

	public bool? RelyOnReferentialIntegrity { get; set; }

	public string? FromTable { get; set; }

	public string? FromColumn { get; set; }

	public string? FromCardinality { get; set; }

	public string? ToTable { get; set; }

	public string? ToColumn { get; set; }

	public string? ToCardinality { get; set; }

	public string? SecurityFilteringBehavior { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
