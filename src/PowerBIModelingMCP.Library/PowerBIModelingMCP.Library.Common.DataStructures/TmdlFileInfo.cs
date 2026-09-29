using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmdlFileInfo
{
	public string FileName { get; set; } = string.Empty;

	public string RelativePath { get; set; } = string.Empty;

	public string ObjectType { get; set; } = string.Empty;

	public long FileSize { get; set; }

	public DateTime LastModified { get; set; }
}
