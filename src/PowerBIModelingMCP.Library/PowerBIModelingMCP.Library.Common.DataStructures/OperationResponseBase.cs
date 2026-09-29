using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public abstract class OperationResponseBase : ResultBase, IOperationResponse, IResultBase
{
	[JsonPropertyOrder(1)]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyOrder(2)]
	public string Operation { get; set; } = string.Empty;

	[JsonPropertyOrder(5)]
	public object? Data { get; set; }

	[JsonPropertyOrder(6)]
	public object? Help { get; set; }

	[JsonPropertyOrder(7)]
	public IList<string>? Warnings { get; set; }

	[JsonIgnore]
	public IList<Exception> Exceptions { get; } = new List<Exception>();

	[JsonPropertyOrder(8)]
	public ErrorSource? ErrorSource { get; set; }

	protected static T CreateForbidden<T>(string op, string msg) where T : OperationResponseBase, new()
	{
		return new T
		{
			Success = false,
			Operation = op,
			Message = msg,
			ErrorSource = PowerBIModelingMCP.Library.Common.DataStructures.ErrorSource.User
		};
	}
}
