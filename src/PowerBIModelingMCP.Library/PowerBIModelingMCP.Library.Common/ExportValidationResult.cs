namespace PowerBIModelingMCP.Library.Common;

public class ExportValidationResult
{
	public bool IsValid { get; set; }

	public string? ErrorMessage { get; set; }

	public string? WarningMessage { get; set; }

	public static ExportValidationResult Success(string? warningMessage = null)
	{
		return new ExportValidationResult
		{
			IsValid = true,
			ErrorMessage = null,
			WarningMessage = warningMessage
		};
	}

	public static ExportValidationResult Failure(string errorMessage)
	{
		return new ExportValidationResult
		{
			IsValid = false,
			ErrorMessage = errorMessage,
			WarningMessage = null
		};
	}
}
