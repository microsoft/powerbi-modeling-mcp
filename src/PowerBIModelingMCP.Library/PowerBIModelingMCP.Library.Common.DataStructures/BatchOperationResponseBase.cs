using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public abstract class BatchOperationResponseBase : OperationResponseBase, IBatchOperationResponse, IOperationResponse, IResultBase
{
	[JsonPropertyOrder(3)]
	public BatchSummary? Summary { get; set; }

	[JsonPropertyOrder(4)]
	public IList<ItemResult>? Results { get; set; }
}
