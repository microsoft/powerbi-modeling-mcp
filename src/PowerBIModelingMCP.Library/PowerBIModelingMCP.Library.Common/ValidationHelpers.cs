using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Common;

public static class ValidationHelpers
{
	public static void ValidateObjectName(string? objectName, string parameterName)
	{
		if (string.IsNullOrWhiteSpace(objectName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage(parameterName + " is required and cannot be empty", ErrorSource.User);
		}
	}

	public static void ValidateConnectionName(string? connectionName)
	{
		if (!string.IsNullOrEmpty(connectionName) && string.IsNullOrWhiteSpace(connectionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("connectionName cannot be empty if provided", ErrorSource.User);
		}
	}
}
