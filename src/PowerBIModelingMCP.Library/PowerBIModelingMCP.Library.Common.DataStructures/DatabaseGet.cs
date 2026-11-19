using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DatabaseGet : DatabaseBase
{
	public string? Id { get; set; }

	public string? State { get; set; }

	public DateTime CreatedTimestamp { get; set; }

	public DateTime LastProcessed { get; set; }

	public DateTime LastUpdate { get; set; }

	public DateTime LastSchemaUpdate { get; set; }

	public long EstimatedSize { get; set; }

	public string? Model { get; set; }

	public string? ModelType { get; set; }
}
