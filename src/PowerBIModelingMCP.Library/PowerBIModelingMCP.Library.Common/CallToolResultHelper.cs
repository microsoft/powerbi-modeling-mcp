using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Common;

public static class CallToolResultHelper
{
	private sealed class AdHocResponse : OperationResponseBase
	{
	}

	private static readonly JsonSerializerOptions DefaultSerializerOptions = new JsonSerializerOptions
	{
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { (JsonConverter)new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};

	private static readonly Regex PrivacyTagRegex = new Regex("</?(?:ccon|pii)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex PrivacyTagJsonEscapedRegex = new Regex("\\\\u003[Cc]/?(?:ccon|pii)\\\\u003[Ee]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static CallToolResult FromResponse<T>(T response, ToolCallAnnotations? annotations = null, Exception? exception = null, bool minimalSuccessPayload = false) where T : class, IOperationResponse
	{
		return BuildResult(response, annotations, exception, null, minimalSuccessPayload);
	}

	public static CallToolResult FromExportResponse<T>(T response, string resourceName = "export.tmdl", string mimeType = "text/plain", ToolCallAnnotations? annotations = null) where T : class, IOperationResponse, IExportDataResponse
	{
		if (!response.Success)
		{
			return FromResponse(response, annotations);
		}
		ContentBlock[] extraContent = null;
		if (response.Data is string text && !string.IsNullOrEmpty(text))
		{
			extraContent = new ContentBlock[1]
			{
				new EmbeddedResourceBlock
				{
					Resource = new TextResourceContents
					{
						Uri = "tmdl://" + Uri.EscapeDataString(resourceName),
						MimeType = mimeType,
						Text = StripPrivacyTags(text)
					}
				}
			};
		}
		return BuildResult(response, annotations, null, extraContent, minimalSuccessPayload: true);
	}

	public static CallToolResult Success(string operation, string message, object? data = null, ToolCallAnnotations? annotations = null)
	{
		return BuildResult(new AdHocResponse
		{
			Success = true,
			Operation = operation,
			Message = message,
			Data = data
		}, annotations);
	}

	public static CallToolResult Error(string operation, string message, ToolCallAnnotations? annotations = null, ErrorSource errorSource = ErrorSource.System, bool appendHelpHint = true)
	{
		return BuildResult(new AdHocResponse
		{
			Success = false,
			Operation = operation,
			Message = (appendHelpHint ? (message + " Please call the Help operation to understand correct usage of the tool.") : message),
			ErrorSource = errorSource
		}, annotations);
	}

	public static JsonObject? BuildMeta(ToolCallAnnotations? annotations, ErrorResponse? errorResponse = null)
	{
		if (annotations == null && errorResponse == null)
		{
			return null;
		}
		JsonObject jsonObject = new JsonObject();
		if (annotations != null)
		{
			jsonObject["annotations"] = JsonSerializer.SerializeToNode(annotations, DefaultSerializerOptions);
		}
		if (errorResponse != null)
		{
			jsonObject["error"] = JsonSerializer.SerializeToNode(errorResponse, DefaultSerializerOptions);
		}
		return jsonObject;
	}

	internal static string StripPrivacyTags(string? input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return string.Empty;
		}
		bool flag = input.IndexOf('<') >= 0;
		bool flag2 = input.IndexOf("\\u003", StringComparison.Ordinal) >= 0;
		if (!flag && !flag2)
		{
			return input;
		}
		string text = (flag ? PrivacyTagRegex.Replace(input, string.Empty) : input);
		if (!flag2)
		{
			return text;
		}
		return PrivacyTagJsonEscapedRegex.Replace(text, string.Empty);
	}

	private static CallToolResult BuildResult<T>(T response, ToolCallAnnotations? annotations, Exception? exception = null, IReadOnlyList<ContentBlock>? extraContent = null, bool minimalSuccessPayload = false) where T : class, IOperationResponse
	{
		bool flag = minimalSuccessPayload && response.Success;
		if (exception != null && !response.Exceptions.Contains(exception))
		{
			response.Exceptions.Add(exception);
		}
		ErrorResponse errorResponse = BuildErrorResponse(response);
		if (!response.Success && response.Exceptions.Count > 0)
		{
			string guidanceText = response.Exceptions.GetGuidanceText();
			if (guidanceText != null)
			{
				response.Message = response.Message + " " + guidanceText;
			}
		}
		SanitizeErrorResponse(errorResponse);
		TextContentBlock textContentBlock = (flag ? TryCreateMinimalSuccessBlock(response) : new TextContentBlock
		{
			Text = StripPrivacyTags(JsonSerializer.Serialize(response, DefaultSerializerOptions))
		});
		List<ContentBlock> list = new List<ContentBlock>(((textContentBlock != null) ? 1 : 0) + (extraContent?.Count ?? 0));
		if (textContentBlock != null && !flag)
		{
			list.Add(textContentBlock);
		}
		if (extraContent != null)
		{
			list.AddRange(extraContent);
		}
		if (textContentBlock != null && flag)
		{
			list.Add(textContentBlock);
		}
		return new CallToolResult
		{
			IsError = !response.Success,
			Content = list,
			Meta = BuildMeta(annotations, errorResponse)
		};
	}

	private static ErrorResponse? BuildErrorResponse(IOperationResponse response)
	{
		if (response.Success)
		{
			return null;
		}
		ErrorResponse errorResponse = null;
		if (response.Exceptions.Count > 0)
		{
			errorResponse = response.Exceptions.ToTelemetrySafeErrorResponse();
		}
		if (response.ErrorSource.HasValue && (errorResponse == null || errorResponse.ErrorSource == ErrorSource.System))
		{
			if (errorResponse == null)
			{
				errorResponse = new ErrorResponse
				{
					Message = response.Message,
					ErrorSource = response.ErrorSource.Value
				};
			}
			else
			{
				errorResponse.ErrorSource = response.ErrorSource.Value;
			}
		}
		return errorResponse;
	}

	private static void SanitizeErrorResponse(ErrorResponse? error)
	{
		if (error == null)
		{
			return;
		}
		if (error.Message != null)
		{
			error.Message = StripPrivacyTags(error.Message);
		}
		if (error.MoreDetails == null)
		{
			return;
		}
		foreach (ErrorDetails moreDetail in error.MoreDetails)
		{
			if (moreDetail.Message != null)
			{
				moreDetail.Message = StripPrivacyTags(moreDetail.Message);
			}
		}
	}

	private static TextContentBlock? TryCreateMinimalSuccessBlock(IOperationResponse response)
	{
		IList<string> list = CollectMinimalWarnings(response);
		if (list == null || list.Count == 0)
		{
			return null;
		}
		string text = StripPrivacyTags(JsonSerializer.Serialize(new
		{
			warnings = list
		}, DefaultSerializerOptions));
		return new TextContentBlock
		{
			Text = text
		};
	}

	private static IList<string>? CollectMinimalWarnings(IOperationResponse response)
	{
		List<string> list = ((response is IBatchOperationResponse response2) ? CollectBatchWarnings(response2) : response.Warnings)?.Where((string w) => !w.StartsWith("Transaction committed.")).ToList();
		if (list == null || list.Count <= 0)
		{
			return null;
		}
		return list;
	}

	private static IList<string>? CollectBatchWarnings(IBatchOperationResponse response)
	{
		List<string> list = new List<string>();
		if (response.Warnings != null)
		{
			list.AddRange(response.Warnings);
		}
		if (response.Results != null)
		{
			foreach (ItemResult result in response.Results)
			{
				List<string> warnings = result.Warnings;
				if (warnings != null && warnings.Count > 0)
				{
					list.AddRange(result.Warnings);
				}
			}
		}
		if (list.Count <= 0)
		{
			return null;
		}
		return list;
	}
}
