using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class ConnectionOperations
{
	private sealed class UninitializedConnectionOperations : IConnectionOperationsService
	{
		public static readonly UninitializedConnectionOperations Instance = new UninitializedConnectionOperations();

		private static InvalidOperationException NotInitialized()
		{
			return new InvalidOperationException("ConnectionOperations has not been initialized. Call ConnectionOperations.Initialize() during host startup.");
		}

		public Task<string> Connect(string serverConnectionString, bool clearCredential)
		{
			throw NotInitialized();
		}

		public ConnectionGet GetConnection(string connectionName)
		{
			throw NotInitialized();
		}

		public Task<IConnectionInfo> GetAsync(string? connectionName = null)
		{
			throw NotInitialized();
		}

		public bool Exists(string connectionName)
		{
			throw NotInitialized();
		}

		public void Disconnect(string? connectionName = null)
		{
			throw NotInitialized();
		}

		public IReadOnlyList<string> ListConnectionNames()
		{
			throw NotInitialized();
		}

		public IReadOnlyList<ConnectionGet> ListConnections()
		{
			throw NotInitialized();
		}

		public TmdlDeserializeResult ConnectFolder(string folderPath, string? connectionName)
		{
			throw NotInitialized();
		}

		public BimDeserializeResult ConnectBimFile(string filePath, string? connectionName)
		{
			throw NotInitialized();
		}

		public IReadOnlyList<LocalAnalysisServicesInstance> ListLocalAnalysisServicesInstances()
		{
			throw NotInitialized();
		}

		public string CreateOfflineConnection(string connectionName, Database database, string sourcePath)
		{
			throw NotInitialized();
		}

		public void SaveChangesIfNeeded(IConnectionInfo info, CheckpointMode checkpointMode)
		{
			throw NotInitialized();
		}

		public void SaveChangesWithRollback(IConnectionInfo info, string operationName, CheckpointMode checkpointMode)
		{
			throw NotInitialized();
		}

		public void Sync(IConnectionInfo info)
		{
			throw NotInitialized();
		}

		public Task Reconnect(IConnectionInfo info)
		{
			throw NotInitialized();
		}

		public Task<IAdomdConnection> OpenAdomdConnectionAsync(IConnectionInfo info, DaxQueryImpersonationOptions impersonationOptions)
		{
			throw NotInitialized();
		}

		public void GetConnectionDetails(string? connectionName, out string? serverName, out string databaseName, out bool isLLMCreated)
		{
			throw NotInitialized();
		}

		public string BuildPowerBiXmlaEndpoint(string workspaceName, string? tenantName = null)
		{
			throw NotInitialized();
		}

		public string BuildConnectionString(string dataSource, string? initialCatalog)
		{
			throw NotInitialized();
		}
	}

	private static volatile IConnectionOperationsService _connectionOperations = UninitializedConnectionOperations.Instance;

	public static void Initialize(IConnectionOperationsService connectionOperations)
	{
		ArgumentNullException.ThrowIfNull(connectionOperations, "connectionOperations");
		_connectionOperations = connectionOperations;
	}

	public static async Task<string> Connect(string serverConnectionString, bool clearCredential)
	{
		return await _connectionOperations.Connect(serverConnectionString, clearCredential);
	}

	public static ConnectionGet GetConnection(string connectionName)
	{
		return _connectionOperations.GetConnection(connectionName);
	}

	public static async Task<IConnectionInfo> GetAsync(string? connectionName = null)
	{
		return await _connectionOperations.GetAsync(connectionName);
	}

	public static bool Exists(string connectionName)
	{
		return _connectionOperations.Exists(connectionName);
	}

	public static void Disconnect(string? connectionName = null)
	{
		_connectionOperations.Disconnect(connectionName);
	}

	public static IReadOnlyList<string> ListConnectionNames()
	{
		return _connectionOperations.ListConnectionNames();
	}

	public static IReadOnlyList<ConnectionGet> ListConnections()
	{
		return _connectionOperations.ListConnections();
	}

	public static TmdlDeserializeResult ConnectFolder(string folderPath, string? connectionName = null)
	{
		return _connectionOperations.ConnectFolder(folderPath, connectionName);
	}

	public static BimDeserializeResult ConnectBimFile(string filePath, string? connectionName = null)
	{
		return _connectionOperations.ConnectBimFile(filePath, connectionName);
	}

	public static IReadOnlyList<LocalAnalysisServicesInstance> ListLocalAnalysisServicesInstances()
	{
		return _connectionOperations.ListLocalAnalysisServicesInstances();
	}

	public static string CreateOfflineConnection(string connectionName, Database database, string sourcePath)
	{
		return _connectionOperations.CreateOfflineConnection(connectionName, database, sourcePath);
	}

	public static void SaveChangesIfNeeded(IConnectionInfo info, CheckpointMode checkpointMode = CheckpointMode.Default)
	{
		_connectionOperations.SaveChangesIfNeeded(info, checkpointMode);
	}

	public static void SaveChangesWithRollback(IConnectionInfo info, string operationName, OperationType operationType, CheckpointMode checkpointMode = CheckpointMode.Default)
	{
		try
		{
			_connectionOperations.SaveChangesWithRollback(info, operationName, checkpointMode);
			if (info.Transaction != null)
			{
				info.Transaction.PendingAuditEntries.Add(new PendingAuditEntry(operationName, operationType, info));
			}
			else
			{
				AuditEvent.Default.Emit(operationName, success: true, operationType, info);
			}
		}
		catch
		{
			if (info.Transaction == null)
			{
				AuditEvent.Default.Emit(operationName, success: false, operationType, info);
			}
			throw;
		}
	}

	public static void Sync(IConnectionInfo info)
	{
		_connectionOperations.Sync(info);
	}

	public static async Task Reconnect(IConnectionInfo info)
	{
		await _connectionOperations.Reconnect(info).ConfigureAwait(continueOnCapturedContext: false);
	}

	public static async Task<IAdomdConnection> OpenAdomdConnectionAsync(IConnectionInfo info, DaxQueryImpersonationOptions impersonationOptions)
	{
		return await _connectionOperations.OpenAdomdConnectionAsync(info, impersonationOptions).ConfigureAwait(continueOnCapturedContext: false);
	}

	public static void GetConnectionDetails(string? connectionName, out string? serverName, out string databaseName, out bool isLLMCreated)
	{
		_connectionOperations.GetConnectionDetails(connectionName, out serverName, out databaseName, out isLLMCreated);
	}

	public static string BuildPowerBiXmlaEndpoint(string workspaceName, string? tenantName = null)
	{
		return _connectionOperations.BuildPowerBiXmlaEndpoint(workspaceName, tenantName);
	}

	public static string BuildConnectionString(string dataSource, string? initialCatalog)
	{
		return _connectionOperations.BuildConnectionString(dataSource, initialCatalog);
	}

	public static string? ResolveSemanticModelId()
	{
		IReadOnlyList<ConnectionGet> readOnlyList = ListConnections();
		if (readOnlyList.Count == 1)
		{
			return readOnlyList[0].SemanticModelId;
		}
		return readOnlyList.Where((ConnectionGet c) => c.LastUsedAt.HasValue)?.OrderByDescending((ConnectionGet c) => c.LastUsedAt).FirstOrDefault()?.SemanticModelId;
	}
}
