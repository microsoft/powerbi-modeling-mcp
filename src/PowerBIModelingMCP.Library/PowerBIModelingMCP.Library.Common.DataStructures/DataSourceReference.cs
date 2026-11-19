using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DataSourceReference
{
	[Required]
	public required string Name { get; set; }
}
