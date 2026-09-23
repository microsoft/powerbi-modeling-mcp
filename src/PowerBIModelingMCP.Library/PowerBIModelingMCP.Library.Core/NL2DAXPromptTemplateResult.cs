using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Core;

public class NL2DAXPromptTemplateResult : ResultBase
{
	public string? ErrorMessage { get; set; }

	public string? TemplateContent { get; set; }
}
