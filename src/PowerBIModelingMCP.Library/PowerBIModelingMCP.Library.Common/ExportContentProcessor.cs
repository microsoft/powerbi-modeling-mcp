using System;
using System.Collections.Generic;
using System.IO;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Common;

public static class ExportContentProcessor
{
	public static (string Content, bool IsTruncated, string? SavedFilePath, List<string> Warnings) ProcessExportContent(string content, ExportOptionsBase options)
	{
		List<string> list = new List<string>();
		string text = null;
		bool item = false;
		string item2 = content;
		int maxReturnCharacters = options.MaxReturnCharacters;
		if (maxReturnCharacters <= 0)
		{
			if (maxReturnCharacters != -1 && maxReturnCharacters == 0)
			{
				item2 = string.Empty;
				if (!string.IsNullOrWhiteSpace(text))
				{
					list.Add("Content not returned in response - saved to file only");
				}
			}
		}
		else if (content.Length > options.MaxReturnCharacters)
		{
			item2 = content.Substring(0, options.MaxReturnCharacters);
			item = true;
			list.Add($"Content truncated to {options.MaxReturnCharacters} characters (original length: {content.Length})");
		}
		return (Content: item2, IsTruncated: item, SavedFilePath: text, Warnings: list);
	}

	public static List<string> ValidateFilePath(string? filePath)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrWhiteSpace(filePath))
		{
			return list;
		}
		try
		{
			if (filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
			{
				list.Add("File path contains invalid characters");
			}
			if (filePath.Length > 260)
			{
				list.Add("File path is too long (maximum 260 characters)");
			}
			try
			{
				Path.GetFullPath(filePath);
			}
			catch (Exception)
			{
				list.Add("Invalid file path format");
			}
		}
		catch (Exception)
		{
			list.Add("Invalid file path format");
		}
		return list;
	}
}
