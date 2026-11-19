using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ItemResult : ResultBase
{
	[JsonPropertyOrder(1)]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyOrder(2)]
	public int Index { get; set; }

	[JsonPropertyOrder(3)]
	[Description("e.g., measure name")]
	public string? ItemIdentifier { get; set; }

	[JsonPropertyOrder(4)]
	[Description("e.g., retrieved measure data for BatchGet")]
	public object? Data { get; set; }

	[JsonPropertyOrder(5)]
	public List<string> Warnings { get; set; } = new List<string>();
}
