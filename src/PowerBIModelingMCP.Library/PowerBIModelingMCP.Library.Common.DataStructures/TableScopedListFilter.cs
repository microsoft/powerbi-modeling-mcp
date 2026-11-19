using System.Collections.Generic;
using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TableScopedListFilter : ListFilterBase
{
	[Description("Matches any specified table")]
	public List<string>? TableNames { get; set; }

	[Description("Case-insensitive, matches any specified folder")]
	public List<string>? DisplayFolders { get; set; }
}
