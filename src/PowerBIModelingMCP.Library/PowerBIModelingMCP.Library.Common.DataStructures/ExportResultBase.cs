using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public abstract class ExportResultBase : ResultBase
{
	public string? ErrorMessage { get; set; }

	public ErrorSource? ErrorSource { get; set; }

	public string ObjectName { get; set; } = string.Empty;

	public string ObjectType { get; set; } = string.Empty;

	[Description("May be truncated")]
	public string Content { get; set; } = string.Empty;

	public int ContentLength { get; set; }

	public bool IsTruncated { get; set; }

	public string? SavedFilePath { get; set; }

	public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

	public List<string> Warnings { get; set; } = new List<string>();

	public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

	protected static T CreateSuccess<T>(string objectName, string objectType, string content, string processedContent, bool isTruncated, string? savedFilePath, List<string> warnings) where T : ExportResultBase, new()
	{
		return new T
		{
			Success = true,
			ObjectName = objectName,
			ObjectType = objectType,
			Content = processedContent,
			ContentLength = content.Length,
			IsTruncated = isTruncated,
			SavedFilePath = savedFilePath,
			Warnings = warnings,
			GeneratedAt = DateTime.UtcNow
		};
	}

	protected static T CreateFailure<T>(string objectName, string objectType, string errorMessage, ErrorSource errorSource) where T : ExportResultBase, new()
	{
		return new T
		{
			Success = false,
			ObjectName = objectName,
			ObjectType = objectType,
			ErrorMessage = errorMessage,
			GeneratedAt = DateTime.UtcNow,
			ErrorSource = errorSource
		};
	}
}
