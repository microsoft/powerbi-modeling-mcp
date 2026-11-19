using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveSubObjectListFilter : ListFilterBase
{
	[Required]
	public required string PerspectiveName { get; set; }

	[Description("Required for ListColumns, ListMeasures, ListHierarchies")]
	public string? TableName { get; set; }
}
