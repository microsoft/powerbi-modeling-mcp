using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AnalysisServices;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Common;

public static class ExceptionHelper
{
	public const string AggregateError = "AggregateError";

	public static ErrorResponse ToErrorResponse(this Exception ex)
	{
		return CreateErrorResponse(ex, ex.Message);
	}

	public static ErrorResponse ToTelemetrySafeErrorResponse(this Exception ex)
	{
		ArgumentNullException.ThrowIfNull(ex, "ex");
		string text = ((!(ex is OperationException) && !(ex is ConnectionException)) ? ((!(ex is McpExceptionWithSource { TelemetrySafeMessage: not null } mcpExceptionWithSource)) ? GetTelemetrySafeTraceString(ex) : mcpExceptionWithSource.TelemetrySafeMessage) : ex.Message);
		string message = text;
		return CreateErrorResponse(ex, message);
	}

	private static string GetTelemetrySafeTraceString(Exception ex)
	{
		return ex.GetType().FullName + ":" + Environment.NewLine + ex.StackTrace;
	}

	private static ErrorResponse CreateErrorResponse(Exception ex, string? message)
	{
		ErrorResponse obj = new ErrorResponse
		{
			ErrorCode = ex.GetType().Name,
			Message = message,
			ErrorSource = ((ex is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : ErrorSource.System)
		};
		EnrichWithASDetails(obj, ex);
		return obj;
	}

	public static ErrorResponse ToErrorResponse(this IList<Exception> exceptions)
	{
		return CreateAggregateErrorResponse(exceptions, (Exception exception) => exception.ToErrorResponse());
	}

	public static ErrorResponse ToTelemetrySafeErrorResponse(this IList<Exception> exceptions)
	{
		return CreateAggregateErrorResponse(exceptions, (Exception exception) => exception.ToTelemetrySafeErrorResponse());
	}

	private static ErrorResponse CreateAggregateErrorResponse(IList<Exception> exceptions, Func<Exception, ErrorResponse> converter)
	{
		if (exceptions.Count == 1)
		{
			return converter(exceptions[0]);
		}
		List<ErrorResponse> list = exceptions.Select(converter).ToList();
		return new ErrorResponse
		{
			ErrorCode = "AggregateError",
			Message = $"Multiple errors occurred ({exceptions.Count} errors)",
			MoreDetails = ((IEnumerable<ErrorResponse>)list).Select((Func<ErrorResponse, ErrorDetails>)((ErrorResponse e) => e)).ToList(),
			ErrorSource = GetMostSevereErrorSource(list)
		};
	}

	private static ErrorSource GetMostSevereErrorSource(IEnumerable<ErrorResponse> responses)
	{
		ErrorSource? errorSource = null;
		foreach (ErrorResponse response in responses)
		{
			if (response.ErrorSource == ErrorSource.System)
			{
				return ErrorSource.System;
			}
			if (response.ErrorSource == ErrorSource.External)
			{
				errorSource = ErrorSource.External;
			}
			if (response.ErrorSource == ErrorSource.User && errorSource != ErrorSource.External)
			{
				errorSource = ErrorSource.User;
			}
		}
		return errorSource.GetValueOrDefault();
	}

	private static void EnrichWithASDetails(ErrorResponse response, Exception ex)
	{
		if (!(ex is OperationException opEx))
		{
			if (ex is ConnectionException ex2)
			{
				response.ErrorCode = $"ConnectionException:{ex2.ExceptionCause}";
			}
		}
		else
		{
			EnrichFromOperationException(response, opEx);
		}
	}

	private static void EnrichFromOperationException(ErrorResponse response, OperationException opEx)
	{
		if (opEx.Results == null || !opEx.Results.ContainsErrors)
		{
			return;
		}
		_ = AsErrorGuidanceLoader.Current;
		List<ErrorDetails> list = new List<ErrorDetails>();
		foreach (XmlaResult item2 in (IEnumerable)opEx.Results)
		{
			foreach (XmlaMessage item3 in (IEnumerable)item2.Messages)
			{
				if (item3 is XmlaError xmlaError)
				{
					ErrorDetails item = new ErrorDetails
					{
						ErrorCode = xmlaError.ErrorCode.ToErrorCodeEnum().ToErrorString(),
						Message = item3.Description
					};
					list.Add(item);
				}
			}
		}
		if (list.Count == 1)
		{
			response.ErrorCode = "OperationException:" + list[0].ErrorCode;
			response.Message = list[0].Message;
		}
		else if (list.Count > 1)
		{
			response.ErrorCode = "OperationException:AggregateError";
			response.Message = $"Multiple XMLA errors occurred ({list.Count} errors)";
			response.MoreDetails = list;
		}
		if (list.Count > 0)
		{
			response.ErrorSource = GetOperationExceptionErrorSource(opEx);
		}
	}

	public static string? GetGuidanceText(this IList<Exception> exceptions)
	{
		AsErrorGuidanceLoader current = AsErrorGuidanceLoader.Current;
		List<string> list = new List<string>();
		foreach (Exception exception in exceptions)
		{
			if (!(exception is OperationException { Results: not null } ex) || !ex.Results.ContainsErrors)
			{
				continue;
			}
			foreach (XmlaResult item in (IEnumerable)ex.Results)
			{
				foreach (XmlaMessage item2 in (IEnumerable)item.Messages)
				{
					if (!(item2 is XmlaError xmlaError))
					{
						continue;
					}
					ErrorGuidanceEntry errorGuidanceEntry = current.FindByErrorCode(xmlaError.ErrorCode);
					if (errorGuidanceEntry != null)
					{
						List<string> list2 = new List<string>();
						if (!string.IsNullOrEmpty(item2.Description))
						{
							list2.Add("Error: " + item2.Description);
						}
						if (!string.IsNullOrEmpty(errorGuidanceEntry.Guidance))
						{
							list2.Add("Guidance: " + errorGuidanceEntry.Guidance);
						}
						if (!string.IsNullOrEmpty(errorGuidanceEntry.DoNotDo))
						{
							list2.Add("Do NOT: " + errorGuidanceEntry.DoNotDo);
						}
						if (list2.Count > 0)
						{
							list.Add(string.Join(" ", list2));
						}
					}
				}
			}
		}
		if (list.Count <= 0)
		{
			return null;
		}
		return string.Join(" | ", list.Distinct());
	}

	public static bool HandleCommitFailure(Exception ex, List<string> warnings, List<Exception> exceptions)
	{
		string guidanceText = new List<Exception> { ex }.GetGuidanceText();
		if (guidanceText != null)
		{
			warnings.Add(guidanceText);
			return true;
		}
		warnings.Add("Failed to commit transaction: " + ex.Message);
		exceptions.Add(ex);
		return false;
	}

	public static string GetErrorMessage(this Exception ex)
	{
		string result = ex.Message;
		if (ex is OperationException { Results: not null } ex2 && ex2.Results.ContainsErrors)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (XmlaResult item in (IEnumerable)ex2.Results)
			{
				foreach (XmlaMessage item2 in (IEnumerable)item.Messages)
				{
					if (item2 is XmlaError)
					{
						stringBuilder.AppendLine(item2.Description);
					}
				}
			}
			if (stringBuilder.Length > 0)
			{
				result = stringBuilder.ToString();
			}
		}
		return result;
	}

	public static List<int> GetErrorCodes(this OperationException exception)
	{
		List<int> list = new List<int>();
		if (exception.Results == null || !exception.Results.ContainsErrors)
		{
			return list;
		}
		foreach (XmlaResult item in (IEnumerable)exception.Results)
		{
			foreach (XmlaMessage item2 in (IEnumerable)item.Messages)
			{
				if (item2 is XmlaError xmlaError)
				{
					list.Add(xmlaError.ErrorCode);
				}
			}
		}
		return list;
	}

	private static ErrorSource GetOperationExceptionErrorSource(OperationException exception)
	{
		ErrorSource result = ErrorSource.System;
		if (exception.Results == null || !exception.Results.ContainsErrors)
		{
			return result;
		}
		foreach (XmlaResult item in (IEnumerable)exception.Results)
		{
			foreach (XmlaMessage item2 in (IEnumerable)item.Messages)
			{
				if (item2 is XmlaError xmlaError)
				{
					switch (xmlaError.ErrorType)
					{
					case 1:
						return ErrorSource.User;
					case 3:
						result = ErrorSource.External;
						break;
					case 2:
						result = ErrorSource.System;
						break;
					}
				}
			}
		}
		return result;
	}
}
