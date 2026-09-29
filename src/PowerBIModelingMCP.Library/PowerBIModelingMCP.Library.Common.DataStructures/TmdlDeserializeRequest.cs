using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmdlDeserializeRequest
{
	[Required]
	public required string FolderPath { get; set; }

	public string? ConnectionName { get; set; }
}
