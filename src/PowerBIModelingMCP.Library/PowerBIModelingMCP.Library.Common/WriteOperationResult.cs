using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common;

public class WriteOperationResult
{
	public bool Success { get; set; }

	public string? Message { get; set; }

	public List<string>? Warnings { get; set; }

	public bool UserDeclinedConfirmation { get; set; }

	public bool UserDeclinedDiscardLocalChanges { get; set; }
}
