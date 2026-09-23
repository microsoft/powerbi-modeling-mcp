using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common;

public class OperationMetadata
{
	public string[] RequiredParams { get; set; } = Array.Empty<string>();

	public string[] OptionalParams { get; set; } = Array.Empty<string>();

	public string[] ForbiddenParams { get; set; } = Array.Empty<string>();

	public string Description { get; set; } = "";

	public string[] CommonMistakes { get; set; } = Array.Empty<string>();

	public string[] Tips { get; set; } = Array.Empty<string>();

	public List<string>? ExampleRequests { get; set; }
}
