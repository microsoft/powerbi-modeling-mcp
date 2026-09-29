using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmdlFolderSummary
{
	public string FolderPath { get; set; } = string.Empty;

	public List<TmdlFileInfo> Files { get; set; } = new List<TmdlFileInfo>();

	public int TotalFiles { get; set; }

	public long TotalSize { get; set; }

	public DateTime LastModified { get; set; }

	public bool IsValidTmdlFolder { get; set; }

	public List<string> ValidationErrors { get; set; } = new List<string>();
}
