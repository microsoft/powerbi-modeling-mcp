using System;
using System.Linq;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.AdomdClient;

namespace PowerBIModelingMCP.Library.Core;

public static class SessionEvictionDetection
{
	private static readonly ConnectionExceptionCause[] SessionEvictionCauses = new ConnectionExceptionCause[5]
	{
		ConnectionExceptionCause.InvalidSessionId,
		ConnectionExceptionCause.Timeout,
		ConnectionExceptionCause.ConnectionNotOpen,
		ConnectionExceptionCause.DataStreamingInterrupted,
		ConnectionExceptionCause.TransportProtocolError
	};

	private const int SessionNotFoundXmlaErrorCode = -1056178166;

	public static bool IsSessionEvictionError(Exception ex)
	{
		for (Exception ex2 = ex; ex2 != null; ex2 = ex2.InnerException)
		{
			if (ex2 is AdomdConnectionException ex3)
			{
				if (Enumerable.Contains(SessionEvictionCauses, ex3.ExceptionCause))
				{
					return true;
				}
			}
			else if (ex2 is ConnectionException ex4)
			{
				if (Enumerable.Contains(SessionEvictionCauses, ex4.ExceptionCause))
				{
					return true;
				}
			}
			else if (ex2 is OperationException opEx && HasSessionEvictionXmlaError(opEx))
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasSessionEvictionXmlaError(OperationException opEx)
	{
		if (opEx.Results == null || !opEx.Results.ContainsErrors)
		{
			return false;
		}
		for (int i = 0; i < opEx.Results.Count; i++)
		{
			XmlaMessageCollection messages = opEx.Results[i].Messages;
			if (messages == null)
			{
				continue;
			}
			for (int j = 0; j < messages.Count; j++)
			{
				if (messages[j] is XmlaError { ErrorCode: -1056178166 })
				{
					return true;
				}
			}
		}
		return false;
	}
}
