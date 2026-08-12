using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Adapters;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Console;

public class ConnectionOperationsService(ModelingClientConfig config, MCPServerConfiguration serverConfig) : IConnectionOperationsService
{
	private struct TcpRow
	{
		public uint State;

		public uint LocalAddr;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public byte[] LocalPortBytes;

		public uint RemoteAddr;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public byte[] RemotePort;

		public uint ProcessId;

		public int LocalPort => BitConverter.ToUInt16(new byte[2]
		{
			LocalPortBytes[1],
			LocalPortBytes[0]
		}, 0);
	}

	private struct ProcessBasicInformation
	{
		private nint Reserved1;

		private nint PebBaseAddress;

		private nint Reserved2_0;

		private nint Reserved2_1;

		private nint UniqueProcessId;

		private nint InheritedFromUniqueProcessId;

		public int ParentProcessId => ((IntPtr)InheritedFromUniqueProcessId).ToInt32();
	}

	private readonly ConcurrentDictionary<string, IConnectionInfo> _namedConnections = new ConcurrentDictionary<string, IConnectionInfo>();

	private string? _lastUsedConnectionName;

	public async Task Reconnect(IConnectionInfo info)
	{
		if (!info.IsCloudConnection)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Reconnect is only supported for cloud connections.", ErrorSource.User);
		}
		if (!(info is ConnectionInfo connInfo))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot reconnect: invalid connection info type.", ErrorSource.User);
		}
		string connectionString = connInfo.ServerConnectionString ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot reconnect: no connection string stored.", ErrorSource.User);
		connectionString = ConnectionStringHelper.RemoveSessionIdFromConnectionString(connectionString);
		string databaseName = connInfo.Database?.Name ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot reconnect: no database name stored.", ErrorSource.User);
		await connInfo.Lock.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			AccessToken value = await AuthService.GetAccessTokenAsync().ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				connInfo.AdomdConnection?.Dispose();
			}
			catch
			{
			}
			AdomdConnectionAdapter adomdConnectionAdapter = null;
			TabularServerAdapter tabularServerAdapter = null;
			try
			{
				adomdConnectionAdapter = new AdomdConnectionAdapter(connectionString, value);
				adomdConnectionAdapter.OnAccessTokenExpired = CreateTokenRefreshCallback("ADOMD");
				adomdConnectionAdapter.Open();
				string sessionID = adomdConnectionAdapter.SessionID;
				try
				{
					connInfo.TabularServer?.Disconnect();
				}
				catch
				{
				}
				string connectionString2 = (string.IsNullOrWhiteSpace(sessionID) ? connectionString : ConnectionStringHelper.AddParameterToConnectionString(connectionString, "SessionId", sessionID));
				tabularServerAdapter = new TabularServerAdapter(value);
				tabularServerAdapter.OnAccessTokenExpired = CreateTokenRefreshCallback("TOM Server");
				tabularServerAdapter.Connect(connectionString2);
				Microsoft.AnalysisServices.Tabular.Database database = tabularServerAdapter.FindDatabase(databaseName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Reconnect failed: database '" + databaseName + "' not found after reconnecting.");
				connInfo.AdomdConnection = adomdConnectionAdapter;
				connInfo.TabularServer = tabularServerAdapter;
				connInfo.Database = database;
				connInfo.ConnectedAt = DateTime.UtcNow;
			}
			catch
			{
				try
				{
					adomdConnectionAdapter?.Dispose();
				}
				catch
				{
				}
				try
				{
					tabularServerAdapter?.Disconnect();
				}
				catch
				{
				}
				throw;
			}
		}
		finally
		{
			connInfo.Lock.Release();
		}
	}

	public async Task<IAdomdConnection> OpenAdomdConnectionAsync(IConnectionInfo info, DaxQueryImpersonationOptions impersonationOptions)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (impersonationOptions == null || !impersonationOptions.HasAny())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Impersonation settings are required for an impersonated DAX query.", ErrorSource.User);
		}
		if (info.IsOffline)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("DAX impersonation is not supported for offline connections.", ErrorSource.User);
		}
		string baseConnectionString = info.ServerConnectionString ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot run an impersonated DAX query because no server connection string is stored.", ErrorSource.User);
		string databaseName = info.Database?.Name ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot run an impersonated DAX query because no database is selected.", ErrorSource.User);
		string connectionString = DaxImpersonationConnectionStringBuilder.Build(baseConnectionString, databaseName, impersonationOptions);
		AccessToken? accessToken = null;
		bool useAccessToken = info.IsCloudConnection;
		if (useAccessToken)
		{
			accessToken = await AuthService.GetAccessTokenAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		AdomdConnectionAdapter adomdConnectionAdapter = new AdomdConnectionAdapter(connectionString, accessToken);
		if (useAccessToken)
		{
			adomdConnectionAdapter.OnAccessTokenExpired = CreateTokenRefreshCallback("ADOMD impersonated");
		}
		try
		{
			adomdConnectionAdapter.Open();
			return adomdConnectionAdapter;
		}
		catch (Exception ex)
		{
			try
			{
				adomdConnectionAdapter.Dispose();
			}
			catch (Exception value)
			{
				System.Console.Error.WriteLine($"Error disposing ADOMD connection: {value}");
			}
			if (ex is AdomdErrorResponseException ex2 && AnalysisServicesErrorUtils.KnownASUserErrorCodes.Contains(ex2.ErrorCode.ToErrorCodeEnum()))
			{
				throw new McpExceptionWithSource("Failed to open impersonated ADOMD connection: " + ex.Message, ex, ErrorSource.User, "Failed to open impersonated ADOMD connection.");
			}
			throw new McpExceptionWithSource("Failed to open impersonated ADOMD connection: " + ex.Message, ex, ErrorSource.System, "Failed to open impersonated ADOMD connection.");
		}
	}

	private Func<AccessToken, AccessToken> CreateTokenRefreshCallback(string connectionType)
	{
		IAuthService authService = AuthService.Instance;
		return delegate(AccessToken expiredToken)
		{
			try
			{
				System.Console.Error.WriteLine($"[INFO] {connectionType} OnAccessTokenExpired callback fired — expired token expires at {expiredToken.ExpirationTime:O}");
				AccessToken result = authService.GetAccessTokenAsync().GetAwaiter().GetResult();
				System.Console.Error.WriteLine($"[INFO] {connectionType} token refreshed successfully — new token expires at {result.ExpirationTime:O}");
				return result;
			}
			catch (Exception ex)
			{
				System.Console.Error.WriteLine("[ERROR] " + connectionType + " token refresh failed: " + ex.Message);
				throw;
			}
		};
	}

	public async Task<string> Connect(string serverConnectionString, bool clearCredential)
	{
		if (string.IsNullOrWhiteSpace(serverConnectionString))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("serverConnectionString is required and cannot be empty", ErrorSource.User);
		}
		bool isPBIDesktopInstance;
		string connectionName;
		(isPBIDesktopInstance, connectionName) = GenerateConnectionNameFromConnectionString(serverConnectionString);
		ValidateConnectionName(connectionName);
		string enhancedConnectionString = ConnectionStringHelper.AddParameterToConnectionString(serverConnectionString, "Application Name", serverConfig.ApplicationName);
		bool isCloudConnection = ConnectionStringHelper.IsFabricConnectionString(enhancedConnectionString);
		bool useAccessToken = isCloudConnection;
		AccessToken? accessToken = null;
		if (useAccessToken)
		{
			accessToken = await AuthService.GetAccessTokenAsync(clearCredential);
		}
		AdomdConnectionAdapter adomdConnectionAdapter = new AdomdConnectionAdapter(enhancedConnectionString, accessToken);
		if (useAccessToken)
		{
			adomdConnectionAdapter.OnAccessTokenExpired = CreateTokenRefreshCallback("ADOMD");
		}
		try
		{
			adomdConnectionAdapter.Open();
		}
		catch (Exception ex)
		{
			System.Console.Error.WriteLine($"Exception caught AdomdClient.AdomdConnection.Open: {ex}");
			throw new McpExceptionWithSource("Failed to open ADOMD connection: " + ex.Message, ex, ErrorSource.System, "Failed to open ADOMD connection.");
		}
		string sessionID = adomdConnectionAdapter.SessionID;
		if (string.IsNullOrWhiteSpace(sessionID))
		{
			System.Console.Error.WriteLine("Warning: ADOMD connection opened but SessionID is null or empty. Proceeding without session sharing.");
		}
		string connectionString = (string.IsNullOrWhiteSpace(sessionID) ? enhancedConnectionString : ConnectionStringHelper.AddParameterToConnectionString(enhancedConnectionString, "SessionId", sessionID));
		TabularServerAdapter tabularServerAdapter = new TabularServerAdapter(accessToken);
		if (useAccessToken)
		{
			tabularServerAdapter.OnAccessTokenExpired = CreateTokenRefreshCallback("TOM Server");
		}
		try
		{
			tabularServerAdapter.Connect(connectionString);
		}
		catch (Exception ex2)
		{
			System.Console.Error.WriteLine($"Exception caught Tabular.Server.Connect: {ex2}");
			Exception ex3 = ex2;
			if (ConnectionStringHelper.IsLikelyDataSourceUrl(serverConnectionString))
			{
				try
				{
					string text = BuildConnectionString(serverConnectionString, null);
					if (!string.IsNullOrWhiteSpace(sessionID))
					{
						text = ConnectionStringHelper.AddParameterToConnectionString(text, "SessionId", sessionID);
					}
					tabularServerAdapter.Connect(text);
					enhancedConnectionString = text;
					ex3 = null;
				}
				catch (Exception value)
				{
					System.Console.Error.WriteLine($"Exception caught on retry with Data Source treatment: {value}");
				}
			}
			if (ex3 != null)
			{
				try
				{
					adomdConnectionAdapter.Dispose();
				}
				catch (Exception value2)
				{
					System.Console.Error.WriteLine($"Error disposing ADOMD connection: {value2}");
				}
				tabularServerAdapter.Disconnect();
				throw new McpExceptionWithSource("Failed to connect to TOM Server: " + ex3.Message, "Failed to connect to TOM server.");
			}
		}
		string text2 = ConnectionStringHelper.ExtractDatabaseName(enhancedConnectionString);
		string semanticModelId = null;
		Microsoft.AnalysisServices.Tabular.Database database;
		if (text2 != null)
		{
			database = tabularServerAdapter.FindDatabase(text2);
			if (database == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Database '" + text2 + "' not found", ErrorSource.User);
			}
			if (isCloudConnection)
			{
				semanticModelId = database.ID;
			}
			if (!isPBIDesktopInstance)
			{
				connectionName = connectionName + "-" + text2;
			}
			connectionName = ConnectionStringHelper.EnsureUniqueName(connectionName, _namedConnections.Keys);
		}
		else
		{
			if (tabularServerAdapter.Databases.Count <= 0)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("No databases found on the server", ErrorSource.User);
			}
			database = tabularServerAdapter.Databases[0];
			text2 = database.Name;
			if (isCloudConnection)
			{
				semanticModelId = database.ID;
			}
			if (!isPBIDesktopInstance)
			{
				connectionName = connectionName + "-" + text2;
			}
			connectionName = ConnectionStringHelper.EnsureUniqueName(connectionName, _namedConnections.Keys);
		}
		if (_namedConnections.ContainsKey(connectionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection '" + connectionName + "' already exists. Use a different name or disconnect the existing connection first.", ErrorSource.User);
		}
		ConnectionInfo connectionInfo = new ConnectionInfo
		{
			ConnectionName = connectionName,
			TabularServer = tabularServerAdapter,
			Database = database,
			AdomdConnection = adomdConnectionAdapter,
			ServerConnectionString = enhancedConnectionString,
			IsCloudConnection = isCloudConnection,
			ConnectedAt = DateTime.UtcNow,
			SemanticModelId = semanticModelId
		};
		if (isCloudConnection)
		{
			try
			{
				ConnectionInfo connectionInfo2 = connectionInfo;
				connectionInfo2.WorkspaceId = await ResolveWorkspaceIdAsync(enhancedConnectionString).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
				connectionInfo.Disconnect();
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Failed to resolve workspace ID for cloud connection.");
			}
		}
		_namedConnections[connectionName] = connectionInfo;
		SetLastUsedConnection(connectionName);
		return connectionName;
	}

	public ConnectionGet GetConnection(string connectionName)
	{
		if (string.IsNullOrWhiteSpace(connectionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("connectionName is required and cannot be empty", ErrorSource.User);
		}
		if (!_namedConnections.TryGetValue(connectionName, out IConnectionInfo value))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection '" + connectionName + "' not found. Available connections: " + string.Join(", ", _namedConnections.Keys), ErrorSource.User);
		}
		return new ConnectionGet
		{
			ConnectionName = value.ConnectionName,
			DatabaseName = value.Database?.Name,
			ServerName = ConnectionStringHelper.ExtractServerName(value.ServerConnectionString),
			IsCloudConnection = value.IsCloudConnection,
			IsOffline = value.IsOffline,
			SourcePath = value.SourcePath,
			ConnectedAt = value.ConnectedAt,
			LastUsedAt = value.LastUsedAt,
			HasTransaction = (value.Transaction != null),
			HasTrace = (value.Trace != null),
			SessionId = (string.IsNullOrWhiteSpace(value.SessionId) ? null : value.SessionId),
			SemanticModelId = value.SemanticModelId
		};
	}

	private IConnectionInfo Get(string? connectionName = null)
	{
		if (string.IsNullOrEmpty(connectionName))
		{
			connectionName = _lastUsedConnectionName;
			if (string.IsNullOrEmpty(connectionName))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("No connectionName provided and no last used connection available. Please connect to a server first or specify a connection name.", ErrorSource.User);
			}
		}
		if (_namedConnections.TryGetValue(connectionName, out IConnectionInfo value))
		{
			SetLastUsedConnection(connectionName);
			value.LastUsedAt = DateTime.UtcNow;
			return value;
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection '" + connectionName + "' not found. Available connections: " + string.Join(", ", _namedConnections.Keys), ErrorSource.User);
	}

	public async Task<IConnectionInfo> GetAsync(string? connectionName = null)
	{
		IConnectionInfo connectionInfo = Get(connectionName);
		if (!(connectionInfo is ConnectionInfo conn))
		{
			throw new InvalidOperationException("Connection '" + connectionName + "' is not a valid ConnectionInfo instance.");
		}
		await conn.Lock.WaitAsync();
		return conn;
	}

	public bool Exists(string connectionName)
	{
		return _namedConnections.ContainsKey(connectionName);
	}

	public void Disconnect(string? connectionName = null)
	{
		if (string.IsNullOrEmpty(connectionName))
		{
			DisconnectAll();
			return;
		}
		if (_namedConnections.TryRemove(connectionName, out IConnectionInfo value))
		{
			value.Disconnect();
			if (_lastUsedConnectionName == connectionName)
			{
				_lastUsedConnectionName = null;
			}
			return;
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection '" + connectionName + "' not found. Available connections: " + string.Join(", ", _namedConnections.Keys), ErrorSource.User);
	}

	public IReadOnlyList<string> ListConnectionNames()
	{
		return _namedConnections.Keys.ToList();
	}

	public IReadOnlyList<ConnectionGet> ListConnections()
	{
		List<ConnectionGet> list = new List<ConnectionGet>();
		foreach (KeyValuePair<string, IConnectionInfo> namedConnection in _namedConnections)
		{
			IConnectionInfo value = namedConnection.Value;
			ConnectionGet item = new ConnectionGet
			{
				ConnectionName = value.ConnectionName,
				DatabaseName = value.Database?.Name,
				ServerName = value.TabularServer?.Name,
				IsCloudConnection = value.IsCloudConnection,
				IsOffline = value.IsOffline,
				SourcePath = value.SourcePath,
				ConnectedAt = value.ConnectedAt,
				LastUsedAt = value.LastUsedAt,
				HasTransaction = (value.Transaction != null),
				HasTrace = (value.Trace != null),
				SessionId = (string.IsNullOrWhiteSpace(value.SessionId) ? null : value.SessionId),
				SemanticModelId = value.SemanticModelId
			};
			list.Add(item);
		}
		return list;
	}

	public TmdlDeserializeResult ConnectFolder(string folderPath, string? connectionName = null)
	{
		if (string.IsNullOrWhiteSpace(folderPath))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Folder path cannot be null or empty", ErrorSource.User);
		}
		if (!Directory.Exists(folderPath))
		{
			throw new McpExceptionWithSource("Folder does not exist: " + folderPath, ErrorSource.User, "Folder path is invalid or the folder does not exist.");
		}
		string text = ResolveDefinitionPath(folderPath);
		if (!File.Exists(Path.Combine(text, "database.tmdl")))
		{
			throw new McpExceptionWithSource("Required file 'database.tmdl' not found in: " + text, ErrorSource.User, "Required database.tmdl file was not found in the model folder.");
		}
		return DatabaseOperations.ImportFromTmdlFolder(text, connectionName);
	}

	public BimDeserializeResult ConnectBimFile(string filePath, string? connectionName = null)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("File path cannot be null or empty", ErrorSource.User);
		}
		if (!File.Exists(filePath))
		{
			throw new McpExceptionWithSource("BIM file does not exist: " + filePath, ErrorSource.User, "BIM file does not exist.");
		}
		if (!filePath.EndsWith(".bim", StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("File must have a .bim extension", ErrorSource.User);
		}
		return DatabaseOperations.ImportFromBimFile(filePath, connectionName);
	}

	private string ResolveDefinitionPath(string folderPath)
	{
		folderPath = Path.GetFullPath(folderPath);
		if (File.Exists(Path.Combine(folderPath, "database.tmdl")))
		{
			return folderPath;
		}
		string text = Path.Combine(folderPath, "definition");
		string path = Path.Combine(text, "database.tmdl");
		if (Directory.Exists(text) && File.Exists(path))
		{
			return text;
		}
		throw new McpExceptionWithSource($"'database.tmdl' not found in '{folderPath}' or '{text}'", ErrorSource.User, "Required database.tmdl file was not found in the expected folder locations.");
	}

	public virtual IReadOnlyList<LocalAnalysisServicesInstance> ListLocalAnalysisServicesInstances()
	{
		List<LocalAnalysisServicesInstance> list = new List<LocalAnalysisServicesInstance>();
		try
		{
			Dictionary<int, int> localListenerPorts = GetLocalListenerPorts();
			Process[] processesByName = Process.GetProcessesByName("msmdsrv");
			foreach (Process process in processesByName)
			{
				if (localListenerPorts.TryGetValue(process.Id, out var value))
				{
					try
					{
						Process process2 = (OperatingSystem.IsWindows() ? GetParentProcess(process.Id) : null);
						string connectionString = ConnectionStringHelper.AddParameterToConnectionString(ConnectionStringHelper.BuildConnectionString($"localhost:{value}"), "Application Name", serverConfig.ApplicationName);
						list.Add(new LocalAnalysisServicesInstance
						{
							ProcessId = process.Id,
							Port = value,
							ConnectionString = connectionString,
							ParentProcessName = process2?.ProcessName,
							ParentWindowTitle = process2?.MainWindowTitle,
							StartTime = process.StartTime
						});
					}
					catch (Win32Exception)
					{
					}
					catch (UnauthorizedAccessException)
					{
					}
					catch (InvalidOperationException)
					{
					}
				}
			}
		}
		catch (Exception)
		{
			return list;
		}
		return list.OrderBy((LocalAnalysisServicesInstance localAnalysisServicesInstance) => localAnalysisServicesInstance.Port).ToList();
	}

	public string CreateOfflineConnection(string connectionName, Microsoft.AnalysisServices.Tabular.Database database, string sourcePath)
	{
		if (string.IsNullOrWhiteSpace(connectionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("connectionName is required and cannot be empty", ErrorSource.User);
		}
		if (database == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("database cannot be null", ErrorSource.User);
		}
		connectionName = ConnectionStringHelper.EnsureUniqueName(connectionName, _namedConnections.Keys);
		ConnectionInfo value = new ConnectionInfo
		{
			ConnectionName = connectionName,
			TabularServer = null,
			Database = database,
			AdomdConnection = null,
			ServerConnectionString = null,
			IsCloudConnection = false,
			IsOffline = true,
			SourcePath = sourcePath,
			ConnectedAt = DateTime.UtcNow,
			IsLLMCreated = true
		};
		_namedConnections[connectionName] = value;
		SetLastUsedConnection(connectionName);
		return connectionName;
	}

	public void SaveChangesIfNeeded(IConnectionInfo info, CheckpointMode checkpointMode = CheckpointMode.Default)
	{
		if (info.Database == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (!info.IsOffline)
		{
			if (checkpointMode == CheckpointMode.AfterRequestRename)
			{
				info.Database.Model.SaveChanges();
			}
			ModelOperations.AddProToolingAnnotation(info, serverConfig.ProToolingValue);
			if (!TransactionOperations.IsInTransaction(info) || checkpointMode == CheckpointMode.ForceEvenInTransaction)
			{
				info.Database.Model.SaveChanges();
			}
		}
	}

	private void UndoLocalChangesIfNeeded(IConnectionInfo info)
	{
		if (info.Database == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (!info.IsOffline && !TransactionOperations.IsInTransaction(info) && info.Database.Model.HasLocalChanges)
		{
			info.Database.Model.UndoLocalChanges();
		}
	}

	public void SaveChangesWithRollback(IConnectionInfo info, string operationName, CheckpointMode checkpointMode = CheckpointMode.Default)
	{
		try
		{
			SaveChangesIfNeeded(info, checkpointMode);
		}
		catch (Exception ex)
		{
			if (info.IsCloudConnection && SessionEvictionDetection.IsSessionEvictionError(ex))
			{
				bool flag = false;
				try
				{
					Reconnect(info).GetAwaiter().GetResult();
					flag = true;
				}
				catch (Exception ex2)
				{
					throw new McpExceptionWithSource($"Failed to {operationName}: the server session was evicted and reconnect also failed. Reconnect error: {ex2.Message}. Please disconnect and reconnect manually.", ex2, ErrorSource.System, "Failed to " + operationName + ": the server session was evicted and reconnect also failed. Please disconnect and reconnect manually.");
				}
				if (flag)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"Failed to {operationName}: the server session was evicted. The connection has been re-established, but unsaved local changes were lost. Please re-apply your changes and try again.");
				}
			}
			UndoLocalChangesIfNeeded(info);
			throw new McpExceptionWithSource("Failed to " + operationName + ": " + ex.Message, "Failed to " + operationName + ".");
		}
	}

	public void Sync(IConnectionInfo info)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (info.Database == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (info.Database.Model == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Model cannot be null", ErrorSource.User);
		}
		if (info.IsOffline || info.Database.Model.HasLocalChanges)
		{
			return;
		}
		try
		{
			if (!info.Database.Model.Sync().Impact.IsEmpty)
			{
				info.LastSynced = DateTime.UtcNow;
			}
		}
		catch (Exception ex)
		{
			if (info.IsCloudConnection && SessionEvictionDetection.IsSessionEvictionError(ex))
			{
				Reconnect(info).GetAwaiter().GetResult();
				if (!info.Database.Model.Sync().Impact.IsEmpty)
				{
					info.LastSynced = DateTime.UtcNow;
				}
				return;
			}
			throw;
		}
	}

	public void GetConnectionDetails(string? connectionName, out string? serverName, out string databaseName, out bool isLLMCreated)
	{
		IConnectionInfo connectionInfo = Get(connectionName);
		serverName = connectionInfo.TabularServer?.Name;
		databaseName = connectionInfo.Database.Name;
		isLLMCreated = connectionInfo.IsLLMCreated;
	}

	private async Task<string?> ResolveWorkspaceIdAsync(string connectionString, CancellationToken cancellationToken = default(CancellationToken))
	{
		string workspaceName = ConnectionStringHelper.ExtractWorkspaceName(connectionString);
		if (string.Equals(workspaceName, "My Workspace", StringComparison.Ordinal))
		{
			return null;
		}
		if (string.IsNullOrEmpty(workspaceName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot resolve workspace ID: no workspace name found in connection string.", ErrorSource.User);
		}
		AccessToken accessToken = await AuthService.GetAccessTokenAsync();
		using HttpClient httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://" + config.XMLAEndpoint?.TrimEnd('/') + "/"),
			Timeout = TimeSpan.FromSeconds(30.0)
		};
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
		string text = Uri.EscapeDataString(workspaceName).Replace("'", "''");
		HttpResponseMessage response = await httpClient.GetAsync("v1.0/myorg/groups?$filter=name eq '" + text + "'", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!response.IsSuccessStatusCode)
		{
			string value = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			throw new McpExceptionWithSource($"Failed to resolve workspace ID: {(int)response.StatusCode} {response.ReasonPhrase}. {value}", $"Failed to resolve workspace ID: {(int)response.StatusCode}.");
		}
		string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		JsonNode jsonNode;
		try
		{
			jsonNode = JsonNode.Parse(json);
		}
		catch (JsonException ex)
		{
			throw new McpExceptionWithSource("Failed to parse workspace resolution response: " + ex.Message, ex, ErrorSource.System, "Failed to parse workspace resolution response.");
		}
		JsonArray jsonArray = jsonNode?["value"]?.AsArray();
		if (jsonArray == null || jsonArray.Count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot resolve workspace ID: workspace '" + workspaceName + "' not found.", ErrorSource.User);
		}
		return jsonArray[0]?["id"]?.GetValue<string>() ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot resolve workspace ID: no ID returned for workspace '" + workspaceName + "'.", ErrorSource.User);
	}

	private void ValidateConnectionName(string connectionName)
	{
		if (string.IsNullOrWhiteSpace(connectionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection name cannot be null or empty", ErrorSource.User);
		}
		if (connectionName.Length > 100)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection name cannot exceed 100 characters", ErrorSource.User);
		}
	}

	public string BuildPowerBiXmlaEndpoint(string workspaceName, string? tenantName = null)
	{
		string xMLAEndpoint = config.XMLAEndpoint;
		ConnectionStringHelper.ValidateConnectionStringSegment(workspaceName, "workspaceName");
		string text = (string.IsNullOrWhiteSpace(tenantName) ? "myorg" : Uri.EscapeDataString(tenantName));
		ConnectionStringHelper.ValidateConnectionStringSegment(text, "tenant");
		if (workspaceName.Equals("My Workspace", StringComparison.OrdinalIgnoreCase))
		{
			return "powerbi://" + xMLAEndpoint + "/v1.0/" + text;
		}
		string value = Uri.EscapeDataString(workspaceName);
		return $"powerbi://{xMLAEndpoint}/v1.0/{text}/{value}";
	}

	public string BuildConnectionString(string dataSource, string? initialCatalog)
	{
		if (string.IsNullOrWhiteSpace(dataSource))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("DataSource is required.", ErrorSource.User);
		}
		if (!dataSource.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase) && (dataSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || dataSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
		{
			Uri uri = new Uri(dataSource);
			dataSource = ("powerbi://" + uri.Host + uri.PathAndQuery).TrimEnd('/');
		}
		return ConnectionStringHelper.AddParameterToConnectionString(ConnectionStringHelper.BuildConnectionString(dataSource, initialCatalog), "Application Name", serverConfig.ApplicationName);
	}

	private void DisconnectAll()
	{
		List<string> list = new List<string>();
		List<KeyValuePair<string, IConnectionInfo>> list2 = _namedConnections.ToList();
		try
		{
			foreach (KeyValuePair<string, IConnectionInfo> item in list2)
			{
				if (_namedConnections.TryRemove(item.Key, out IConnectionInfo value))
				{
					try
					{
						value.Disconnect();
					}
					catch (Exception ex)
					{
						list.Add("'" + item.Key + "': " + ex.Message);
					}
				}
			}
		}
		finally
		{
			_lastUsedConnectionName = null;
		}
		if (list.Count > 0)
		{
			throw new McpExceptionWithSource("Failed to disconnect one or more connections: " + string.Join("; ", list), ErrorSource.System, "Failed to disconnect one or more connections.");
		}
	}

	private void SetLastUsedConnection(string connectionName)
	{
		if (string.IsNullOrWhiteSpace(connectionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("connectionName is required and cannot be empty", ErrorSource.User);
		}
		if (!_namedConnections.ContainsKey(connectionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection '" + connectionName + "' not found", ErrorSource.User);
		}
		_lastUsedConnectionName = connectionName;
	}

	internal (bool isPBIDesktopInstance, string connectionName) GenerateConnectionNameFromConnectionString(string cs)
	{
		if (string.IsNullOrEmpty(cs))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection string cannot be null or empty", ErrorSource.User);
		}
		if (ConnectionStringHelper.IsFabricConnectionString(cs))
		{
			string text = ConnectionStringHelper.ExtractWorkspaceName(cs);
			if (text == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Workspace must be present in Fabric connection string", ErrorSource.User);
			}
			return (isPBIDesktopInstance: false, connectionName: "Fabric-" + text);
		}
		string serverName = ConnectionStringHelper.ExtractServerName(cs);
		if (serverName == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Server name must be present in connection string", ErrorSource.User);
		}
		string[] array = serverName.Split(':');
		LocalAnalysisServicesInstance localAnalysisServicesInstance = null;
		try
		{
			localAnalysisServicesInstance = ListLocalAnalysisServicesInstances().FirstOrDefault((LocalAnalysisServicesInstance i) => serverName.EndsWith($":{i.Port}"));
		}
		catch (Exception)
		{
		}
		if (array.Length > 1)
		{
			string text2 = array[1];
			if (localAnalysisServicesInstance != null && localAnalysisServicesInstance.ParentProcessName == "PBIDesktop")
			{
				return (isPBIDesktopInstance: true, connectionName: "PBIDesktop-" + localAnalysisServicesInstance.ParentWindowTitle + "-" + text2);
			}
			return (isPBIDesktopInstance: false, connectionName: "Local-" + text2);
		}
		return (isPBIDesktopInstance: false, connectionName: "Local-" + serverName);
	}

	private Dictionary<int, int> GetLocalListenerPorts()
	{
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		int dwOutBufLen = 0;
		GetExtendedTcpTable(IntPtr.Zero, ref dwOutBufLen, sort: true, 2, 5);
		nint num = Marshal.AllocHGlobal(dwOutBufLen);
		try
		{
			if (GetExtendedTcpTable(num, ref dwOutBufLen, sort: true, 2, 5) != 0)
			{
				return dictionary;
			}
			uint num2 = (uint)Marshal.ReadInt32(num);
			nint num3 = IntPtr.Add(num, 4);
			int offset = Marshal.SizeOf<TcpRow>();
			for (int i = 0; i < num2; i++)
			{
				TcpRow tcpRow = Marshal.PtrToStructure<TcpRow>(num3);
				if (tcpRow.State == 2)
				{
					int processId = (int)tcpRow.ProcessId;
					if (!dictionary.ContainsKey(processId))
					{
						dictionary.Add(processId, tcpRow.LocalPort);
					}
				}
				num3 = IntPtr.Add(num3, offset);
			}
			return dictionary;
		}
		finally
		{
			Marshal.FreeHGlobal(num);
		}
	}

	private Process? GetParentProcess_Legacy(int processId)
	{
		try
		{
			using Process process = Process.GetProcessById(processId);
			return GetParentProcess_Legacy(process.Handle);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private Process? GetParentProcess_Legacy(nint handle)
	{
		ProcessBasicInformation processInformation = default(ProcessBasicInformation);
		if (NtQueryInformationProcess(handle, 0, ref processInformation, Marshal.SizeOf(processInformation), out var _) != 0)
		{
			return null;
		}
		try
		{
			return Process.GetProcessById(processInformation.ParentProcessId);
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	[SupportedOSPlatform("windows")]
	private Process? GetParentProcess(int processId)
	{
		try
		{
			using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId=" + processId);
			return (from p in managementObjectSearcher.Get().OfType<ManagementObject>()
				select Process.GetProcessById((int)(uint)p["ParentProcessId"])).FirstOrDefault();
		}
		catch (Exception)
		{
			return null;
		}
	}

	[DllImport("iphlpapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern uint GetExtendedTcpTable(nint pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved = 0u);

	[DllImport("ntdll.dll")]
	private static extern int NtQueryInformationProcess(nint processHandle, int processInformationClass, ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);
}
