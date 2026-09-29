using System.Collections.Generic;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Common;

public sealed class BatchItemContext<TItem>
{
	public required IConnectionInfo Connection { get; init; }

	public required TItem Item { get; init; }

	public required int Index { get; init; }

	public required ItemResult Result { get; init; }

	public required List<string> Warnings { get; init; }

	public required string? TransactionId { get; init; }
}
