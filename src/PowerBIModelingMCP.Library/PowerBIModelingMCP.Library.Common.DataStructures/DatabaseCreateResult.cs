using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DatabaseCreateResult : ResultBase
{
	public string ConnectionName { get; set; } = string.Empty;

	public string DatabaseName { get; set; } = string.Empty;

	public string ModelName { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }

	public string? Message { get; set; }
}
