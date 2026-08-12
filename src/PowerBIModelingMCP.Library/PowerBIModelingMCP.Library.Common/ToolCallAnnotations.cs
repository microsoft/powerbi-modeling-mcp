using System.Text.Json.Serialization;

namespace PowerBIModelingMCP.Library.Common;

public class ToolCallAnnotations
{
	[JsonPropertyName("title")]
	public string Title { get; set; } = string.Empty;

	[JsonPropertyName("readOnlyHint")]
	public bool ReadOnlyHint { get; set; }

	public static ToolCallAnnotations Create(string toolName, string operation, bool readOnly)
	{
		return new ToolCallAnnotations
		{
			Title = toolName + "." + operation.ToLowerInvariant(),
			ReadOnlyHint = readOnly
		};
	}
}
