using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common;

public static class ExportValidationHelper
{
	public static ExportValidationResult ValidateReferences<TReference>(IList<TReference>? references, string objectTypeName, string operationName = "ExportTMDL") where TReference : class
	{
		if (references == null || references.Count == 0)
		{
			return ExportValidationResult.Failure($"References is required with at least one {objectTypeName}Reference for {operationName} operation");
		}
		string warningMessage = null;
		if (references.Count > 1)
		{
			warningMessage = $"Multiple {objectTypeName} references provided ({references.Count} objects). Only the first object will be exported; the remaining {references.Count - 1} object(s) will be ignored.";
		}
		return ExportValidationResult.Success(warningMessage);
	}

	public static ExportValidationResult ValidateName(string? name, string objectTypeName, string operationName = "ExportTMDL")
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return ExportValidationResult.Failure($"References is required with at least one {objectTypeName}Reference containing a valid Name for {operationName} operation");
		}
		return ExportValidationResult.Success();
	}

	public static ExportValidationResult ValidateTableScopedReference(string? tableName, string? name, string objectTypeName, string operationName = "ExportTMDL")
	{
		if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(name))
		{
			return ExportValidationResult.Failure($"References is required with at least one {objectTypeName}Reference containing TableName and Name for {operationName} operation");
		}
		return ExportValidationResult.Success();
	}

	public static ExportValidationResult ValidatePartitionReference(string? tableName, string operationName = "ExportTMDL")
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			return ExportValidationResult.Failure("References is required with at least one PartitionReference containing TableName for " + operationName + " operation");
		}
		return ExportValidationResult.Success();
	}

	public static string FormatSuccessMessage(string objectTypeName, string objectIdentifier, string? warningMessage = null)
	{
		string text = $"TMDL exported for {objectTypeName.ToLowerInvariant()} '{objectIdentifier}'";
		if (!string.IsNullOrEmpty(warningMessage))
		{
			text = "Warning: " + warningMessage + "\n" + text;
		}
		return text;
	}

	public static string FormatTmslSuccessMessage(string objectTypeName, string objectIdentifier, string operationType, string? warningMessage = null)
	{
		string text = $"TMSL {operationType} script for {objectTypeName.ToLowerInvariant()} '{objectIdentifier}' generated successfully";
		if (!string.IsNullOrEmpty(warningMessage))
		{
			text = "Warning: " + warningMessage + "\n" + text;
		}
		return text;
	}
}
