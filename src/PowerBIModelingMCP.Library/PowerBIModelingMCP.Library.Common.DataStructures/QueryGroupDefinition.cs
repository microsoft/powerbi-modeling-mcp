using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class QueryGroupDefinition : QueryGroupBase
{
	[Description("Required for Update, not used for Create")]
	public string? Name { get; set; }
}
