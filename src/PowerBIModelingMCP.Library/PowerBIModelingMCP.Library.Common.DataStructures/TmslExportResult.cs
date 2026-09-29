using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmslExportResult : ExportResultBase
{
	public TmslOperationType OperationType { get; set; }

	public Dictionary<string, object> TmslMetadata { get; set; } = new Dictionary<string, object>();

	public ExportTmsl? AppliedOptions { get; set; }

	[Description("Legacy compatibility wrapper for Content")]
	public string TmslScript
	{
		get
		{
			return base.Content;
		}
		set
		{
			base.Content = value;
		}
	}

	public static TmslExportResult CreateSuccess(string objectName, string objectType, string content, string processedContent, bool isTruncated, string? savedFilePath, List<string> warnings, TmslOperationType operationType, ExportTmsl? appliedOptions = null)
	{
		TmslExportResult tmslExportResult = ExportResultBase.CreateSuccess<TmslExportResult>(objectName, objectType, content, processedContent, isTruncated, savedFilePath, warnings);
		tmslExportResult.OperationType = operationType;
		tmslExportResult.AppliedOptions = appliedOptions;
		tmslExportResult.TmslMetadata["ExportType"] = "TMSL";
		tmslExportResult.TmslMetadata["ContentType"] = "JSON";
		tmslExportResult.TmslMetadata["OperationType"] = operationType.ToString();
		if (appliedOptions != null)
		{
			tmslExportResult.TmslMetadata["FormatJson"] = appliedOptions.FormatJson;
			if (!string.IsNullOrWhiteSpace(appliedOptions.TmslOperationType))
			{
				tmslExportResult.TmslMetadata["RequestedOperationType"] = appliedOptions.TmslOperationType;
			}
			if (!string.IsNullOrWhiteSpace(appliedOptions.RefreshType))
			{
				tmslExportResult.TmslMetadata["RefreshType"] = appliedOptions.RefreshType;
			}
			if (appliedOptions.IncludeRestricted.HasValue)
			{
				tmslExportResult.TmslMetadata["IncludeRestricted"] = appliedOptions.IncludeRestricted.Value;
			}
		}
		return tmslExportResult;
	}

	public static TmslExportResult CreateFailure(string objectName, string objectType, string operationType, string errorMessage, ErrorSource errorSource)
	{
		TmslExportResult tmslExportResult = ExportResultBase.CreateFailure<TmslExportResult>(objectName, objectType, errorMessage, errorSource);
		if (Enum.TryParse<TmslOperationType>(operationType, ignoreCase: true, out var result))
		{
			tmslExportResult.OperationType = result;
		}
		tmslExportResult.TmslMetadata["ExportType"] = "TMSL";
		tmslExportResult.TmslMetadata["ContentType"] = "JSON";
		tmslExportResult.TmslMetadata["OperationType"] = operationType;
		return tmslExportResult;
	}

	public static TmslExportResult FromLegacyResult(TmslOperationResult legacyResult)
	{
		return new TmslExportResult
		{
			Success = legacyResult.Success,
			ObjectName = legacyResult.ObjectName,
			ObjectType = legacyResult.ObjectType,
			Content = legacyResult.TmslScript,
			ContentLength = legacyResult.TmslScript.Length,
			ErrorMessage = legacyResult.ErrorMessage,
			ErrorSource = legacyResult.ErrorSource,
			GeneratedAt = legacyResult.GeneratedAt,
			OperationType = legacyResult.OperationType,
			TmslMetadata = 
			{
				["ExportType"] = "TMSL",
				["ContentType"] = "JSON",
				["OperationType"] = legacyResult.OperationType.ToString(),
				["MigratedFromLegacy"] = true
			}
		};
	}
}
