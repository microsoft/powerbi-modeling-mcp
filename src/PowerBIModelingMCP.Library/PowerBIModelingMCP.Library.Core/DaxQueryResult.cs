using System.Collections.Generic;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Core;

public class DaxQueryResult : ResultBase
{
	public string? ErrorMessage { get; set; }

	public ErrorSource? ErrorSource { get; set; }

	public int RowCount { get; set; }

	public List<DaxColumnInfo> Columns { get; set; } = new List<DaxColumnInfo>();

	public List<Dictionary<string, object?>> Rows { get; set; } = new List<Dictionary<string, object>>();

	public long ExecutionTimeMs { get; set; }
}
