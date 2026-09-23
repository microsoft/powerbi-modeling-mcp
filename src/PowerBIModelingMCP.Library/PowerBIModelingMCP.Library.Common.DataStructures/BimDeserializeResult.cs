using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class BimDeserializeResult : ResultBase
{
	public string ConnectionName { get; set; } = string.Empty;

	public string DatabaseName { get; set; } = string.Empty;

	public string FilePath { get; set; } = string.Empty;

	public int TablesLoaded { get; set; }

	public int MeasuresLoaded { get; set; }

	public int RelationshipsLoaded { get; set; }

	public DateTime LoadedAt { get; set; }

	public string? Message { get; set; }
}
