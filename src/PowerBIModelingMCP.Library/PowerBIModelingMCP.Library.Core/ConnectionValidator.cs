using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class ConnectionValidator
{
	public static void ValidateForTransactions(IConnectionInfo connection)
	{
		if (connection == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection cannot be null");
		}
		if (connection.IsOffline)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Transaction operations are not supported on offline connections", ErrorSource.User);
		}
		if (connection.TabularServer == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Active server connection is required for transaction operations", ErrorSource.User);
		}
	}

	public static void ValidateForDaxQueries(IConnectionInfo connection)
	{
		if (connection == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection cannot be null");
		}
		if (connection.IsOffline)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("DAX query operations are not supported on offline connections", ErrorSource.User);
		}
		if (connection.AdomdConnection == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ADOMD connection is required for DAX queries", ErrorSource.User);
		}
	}

	public static void ValidateForTrace(IConnectionInfo connection)
	{
		if (connection == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection cannot be null");
		}
		if (connection.IsOffline)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Trace operations are not supported on offline (TMDL) connections", ErrorSource.User);
		}
		if (connection.TabularServer == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection does not have an active server connection for trace operations", ErrorSource.User);
		}
	}

	public static void ValidateForModelOperations(IConnectionInfo connection)
	{
		if (connection == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection cannot be null");
		}
		if (connection.Database == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database reference is required for model operations", ErrorSource.User);
		}
	}

	public static void ValidateOnlineConnection(IConnectionInfo connection)
	{
		if (connection == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection cannot be null", ErrorSource.User);
		}
		if (connection.IsOffline)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("This operation requires an online connection", ErrorSource.User);
		}
		if (connection.TabularServer == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Active server connection is required for this operation", ErrorSource.User);
		}
	}

	public static void ValidateOfflineConnection(IConnectionInfo connection)
	{
		if (connection == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection cannot be null");
		}
		if (!connection.IsOffline)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("This operation requires an offline connection", ErrorSource.User);
		}
		if (connection.Database == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database reference is required for offline operations", ErrorSource.User);
		}
	}
}
