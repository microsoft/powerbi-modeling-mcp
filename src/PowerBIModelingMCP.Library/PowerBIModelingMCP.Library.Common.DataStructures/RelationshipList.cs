using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class RelationshipList : ObjectListBase
{
	public string? FromTable { get; set; }

	public string? FromColumn { get; set; }

	public string? ToTable { get; set; }

	public string? ToColumn { get; set; }

	public bool? IsActive { get; set; }

	[Description("OneDirection, BothDirections, etc.")]
	public string? CrossFilteringBehavior { get; set; }

	[Description("One, Many")]
	public string? FromCardinality { get; set; }

	[Description("One, Many")]
	public string? ToCardinality { get; set; }
}
