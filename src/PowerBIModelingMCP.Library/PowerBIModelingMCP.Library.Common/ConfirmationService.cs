using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Common;

public static class ConfirmationService
{
	private static readonly ConcurrentDictionary<(string, ConfirmationType), Task<bool>> confirmedDatabases = new ConcurrentDictionary<(string, ConfirmationType), Task<bool>>();

	private static readonly ConcurrentDictionary<(string, ConfirmationType), SemaphoreSlim> elicitationLocks = new ConcurrentDictionary<(string, ConfirmationType), SemaphoreSlim>();

	public static void CacheConfirmedOperation(string databaseName, ConfirmationType confirmationType)
	{
		(string, ConfirmationType) key = (databaseName, confirmationType);
		confirmedDatabases.TryAdd(key, Task.FromResult(result: true));
	}

	public static bool CheckForConfirmedAction(string databaseName, ConfirmationType requestedConfirmationType)
	{
		(string, ConfirmationType) key = (databaseName, requestedConfirmationType);
		return confirmedDatabases.ContainsKey(key);
	}

	public static async Task<bool> RequestConfirmationAsync(McpServer server, string databaseName, string message, ConfirmationType confirmationType)
	{
		return await ElicitationRequestHandler.HandleConfirmRequest(server, databaseName, message, confirmationType);
	}

	public static async Task<bool> ValidateConfirmationAsync(McpServer server, string databaseName, string message, ConfirmationType confirmationType, IWriteGuard writeGuard)
	{
		if (writeGuard.IsSkipConfirmationEnabled)
		{
			return true;
		}
		if (confirmationType != ConfirmationType.GenericOperation)
		{
			(string databaseName, ConfirmationType confirmationType) key = (databaseName: databaseName, confirmationType: confirmationType);
			if (confirmedDatabases.TryGetValue(key, out Task<bool> value))
			{
				return await value;
			}
			SemaphoreSlim semaphore = elicitationLocks.GetOrAdd(key, ((string, ConfirmationType) _) => new SemaphoreSlim(1, 1));
			await semaphore.WaitAsync();
			try
			{
				if (confirmedDatabases.TryGetValue(key, out value))
				{
					return await value;
				}
				bool num = await RequestConfirmationAsync(server, databaseName, message, confirmationType);
				if (num)
				{
					confirmedDatabases.TryAdd(key, Task.FromResult(result: true));
				}
				return num;
			}
			finally
			{
				semaphore.Release();
			}
		}
		return await RequestConfirmationAsync(server, databaseName, message, confirmationType);
	}

	public static bool ConfirmRequest(McpServer server, string? connectionName, ConfirmationType confirmationType, IWriteGuard writeGuard)
	{
		if (server == null)
		{
			throw new ArgumentNullException("server");
		}
		if (writeGuard.IsSkipConfirmationEnabled)
		{
			return true;
		}
		ConnectionOperations.GetConnectionDetails(connectionName, out string _, out string databaseName, out bool isLLMCreated);
		if (isLLMCreated)
		{
			return true;
		}
		if (string.IsNullOrEmpty(databaseName))
		{
			throw new ArgumentException("Connection does not have a database specified.", "connectionName");
		}
		return ValidateConfirmationAsync(message: confirmationType switch
		{
			ConfirmationType.WriteOperation => "Are you sure you want to perform operations that will modify your database: '" + databaseName + "'?", 
			ConfirmationType.DaxOperation => "Are you sure you want to execute dax queries on your database: '" + databaseName + "'?", 
			_ => throw new ArgumentOutOfRangeException("confirmationType", "Unsupported confirmation type."), 
		}, server: server, databaseName: databaseName, confirmationType: confirmationType, writeGuard: writeGuard).Result;
	}

	public static bool ConfirmGenericRequest(McpServer server, string? connectionName, string request, IWriteGuard writeGuard)
	{
		if (server == null)
		{
			throw new ArgumentNullException("server");
		}
		if (writeGuard.IsSkipConfirmationEnabled)
		{
			return true;
		}
		ConnectionOperations.GetConnectionDetails(connectionName, out string _, out string databaseName, out bool _);
		if (string.IsNullOrEmpty(databaseName))
		{
			throw new ArgumentException("Connection does not have a database specified.", "connectionName");
		}
		return ElicitationRequestHandler.HandleConfirmRequest(server, databaseName, request, ConfirmationType.GenericOperation).Result;
	}
}
