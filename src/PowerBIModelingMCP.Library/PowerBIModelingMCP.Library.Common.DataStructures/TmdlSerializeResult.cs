using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmdlSerializeResult : ResultBase
{
	public string FolderPath { get; set; } = string.Empty;

	public string DatabaseName { get; set; } = string.Empty;

	public List<string> FilesCreated { get; set; } = new List<string>();

	public int FileCount { get; set; }

	public DateTime SerializedAt { get; set; }

	public string? Message { get; set; }
}
