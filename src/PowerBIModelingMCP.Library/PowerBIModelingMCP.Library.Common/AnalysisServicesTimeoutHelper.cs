using System;

namespace PowerBIModelingMCP.Library.Common;

public static class AnalysisServicesTimeoutHelper
{
	public static bool IsTimeoutException(Exception exception)
	{
		for (Exception ex = exception; ex != null; ex = ex.InnerException)
		{
			if (ex is TimeoutException || ContainsTimeoutSignal(ex.Message))
			{
				return true;
			}
		}
		return false;
	}

	private static bool ContainsTimeoutSignal(string? value)
	{
		if (!string.IsNullOrEmpty(value))
		{
			if (!value.Contains("TimeoutException", StringComparison.OrdinalIgnoreCase) && !value.Contains("timed out", StringComparison.OrdinalIgnoreCase))
			{
				return value.Contains("timeout", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}
}
