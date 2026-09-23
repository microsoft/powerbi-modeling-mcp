using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Core;

public class DaxValidationResult
{
	public bool IsValid { get; set; }

	public string? ErrorMessage { get; set; }

	public string? DetailedError { get; set; }

	public List<DaxColumnInfo> ExpectedColumns { get; set; } = new List<DaxColumnInfo>();

	public long ValidationTimeMs { get; set; }
}
