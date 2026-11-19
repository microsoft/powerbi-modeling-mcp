using System.Collections.Generic;
using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ModelRoleList : ObjectListBase
{
	[Description("None, Read, ReadRefresh, Refresh, Administrator")]
	public string? ModelPermission { get; set; }

	public List<string>? TableNames { get; set; }
}
