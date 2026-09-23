using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DataSourceList : ObjectListBase
{
	[Description("Provider, Structured, etc.")]
	public string? Type { get; set; }
}
