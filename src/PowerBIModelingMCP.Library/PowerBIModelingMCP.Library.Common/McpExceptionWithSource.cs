using System;
using ModelContextProtocol;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Common;

public class McpExceptionWithSource : McpException
{
	public ErrorSource ErrorSource { get; }

	public string? TelemetrySafeMessage { get; }

	public McpExceptionWithSource(string message, ErrorSource errorSource = ErrorSource.System)
		: base(message)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message, "message");
		ErrorSource = errorSource;
	}

	public McpExceptionWithSource(string message, string telemetrySafeMessage)
		: this(message, ErrorSource.System, telemetrySafeMessage)
	{
	}

	public McpExceptionWithSource(string message, ErrorSource errorSource, string telemetrySafeMessage)
		: this(message, errorSource)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(telemetrySafeMessage, "telemetrySafeMessage");
		TelemetrySafeMessage = telemetrySafeMessage;
	}

	public McpExceptionWithSource(string message, Exception innerException, ErrorSource? errorSource = null)
		: base(message, innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message, "message");
		ArgumentNullException.ThrowIfNull(innerException, "innerException");
		ErrorSource = ((!errorSource.HasValue && innerException is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : errorSource.GetValueOrDefault());
	}

	public McpExceptionWithSource(string message, Exception innerException, ErrorSource? errorSource, string telemetrySafeMessage)
		: this(message, innerException, errorSource)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(telemetrySafeMessage, "telemetrySafeMessage");
		TelemetrySafeMessage = telemetrySafeMessage;
	}

	public static McpExceptionWithSource FromTelemetrySafeMessage(string message, ErrorSource errorSource = ErrorSource.System)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message, "message");
		return new McpExceptionWithSource(message, errorSource, message);
	}

	public static McpExceptionWithSource FromTelemetrySafeMessage(string message, Exception innerException, ErrorSource? errorSource = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message, "message");
		ArgumentNullException.ThrowIfNull(innerException, "innerException");
		return new McpExceptionWithSource(message, innerException, errorSource, message);
	}
}
