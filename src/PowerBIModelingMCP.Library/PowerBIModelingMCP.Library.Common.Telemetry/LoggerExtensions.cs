using System;
using Microsoft.Extensions.Logging;

namespace PowerBIModelingMCP.Library.Common.Telemetry;

public static class LoggerExtensions
{
	public static void LogOperationError(this ILogger logger, string toolName, string operation, Exception exception)
	{
		logger.LogError("Error executing {ToolName}.{Operation}: ExceptionType={ExceptionType}", toolName, operation, exception.GetType().Name);
		logger.LogDebug(exception, "Error executing {ToolName}.{Operation}: {ErrorMessage}", toolName, operation, exception.Message);
	}

	public static void LogOperationWarning(this ILogger logger, string toolName, string operation, string warning)
	{
		logger.LogWarning("{ToolName}.{Operation} completed with warning", toolName, operation);
		logger.LogDebug("{ToolName}.{Operation} warning: {Warning}", toolName, operation, warning);
	}

	public static void LogResultFailure(this ILogger logger, string toolName, string operation, string status, string? errorMessage)
	{
		logger.LogWarning("{ToolName}.{Operation} completed: Status={Status}", toolName, operation, status);
		if (errorMessage != null)
		{
			logger.LogDebug("{ToolName}.{Operation} completed: Status={Status}, Error={ErrorMessage}", toolName, operation, status, errorMessage);
		}
	}

	public static void LogToolCallCompleted(this ILogger logger, string title, bool isWrite, bool isError, string? semanticModelId = null)
	{
		logger.LogInformation("ToolCallCompleted: {Title} IsWrite={IsWrite} IsError={IsError} SemanticModelId={SemanticModelId}", title, isWrite, isError, semanticModelId);
	}
}
