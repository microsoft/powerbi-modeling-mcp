using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Contracts;

public interface IConnectionOperationsService
{
	Task<string> Connect(string serverConnectionString, bool clearCredential);

	ConnectionGet GetConnection(string connectionName);

	Task<IConnectionInfo> GetAsync(string? connectionName = null);

	bool Exists(string connectionName);

	void Disconnect(string? connectionName = null);

	IReadOnlyList<string> ListConnectionNames();

	IReadOnlyList<ConnectionGet> ListConnections();

	TmdlDeserializeResult ConnectFolder(string folderPath, string? connectionName);

	BimDeserializeResult ConnectBimFile(string filePath, string? connectionName);

	IReadOnlyList<LocalAnalysisServicesInstance> ListLocalAnalysisServicesInstances();

	string CreateOfflineConnection(string connectionName, Database database, string sourcePath);

	void SaveChangesIfNeeded(IConnectionInfo info, CheckpointMode checkpointMode);

	void SaveChangesWithRollback(IConnectionInfo info, string operationName, CheckpointMode checkpointMode);

	void Sync(IConnectionInfo info);

	Task Reconnect(IConnectionInfo info);

	Task<IAdomdConnection> OpenAdomdConnectionAsync(IConnectionInfo info, DaxQueryImpersonationOptions impersonationOptions);

	void GetConnectionDetails(string? connectionName, out string? serverName, out string databaseName, out bool isLLMCreated);

	string BuildPowerBiXmlaEndpoint(string workspaceName, string? tenantName = null);

	string BuildConnectionString(string dataSource, string? initialCatalog);
}
