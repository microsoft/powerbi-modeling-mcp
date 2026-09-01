using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class BatchOperationResponse : ResultBase
{
	[JsonPropertyOrder(1)]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyOrder(2)]
	public string Operation { get; set; } = string.Empty;

	[JsonPropertyOrder(3)]
	public List<ItemResult> Results { get; set; } = new List<ItemResult>();

	[JsonPropertyOrder(4)]
	public BatchSummary Summary { get; set; } = new BatchSummary();

	[JsonPropertyOrder(5)]
	public List<string>? Warnings { get; set; }

	[JsonIgnore]
	public List<Exception> Exceptions { get; } = new List<Exception>();
}
