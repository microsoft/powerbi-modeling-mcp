using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveTableIdentifier
{
	[Required]
	public required string TableName { get; set; }
}
