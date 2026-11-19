using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Tools;

namespace PowerBIModelingMCP.Library.Core;

public static class DatabaseOperations
{
	public static async Task<List<DatabaseGet>> ListDatabases(string? connectionName = null)
	{
		List<DatabaseGet> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<DatabaseGet> list = ListDatabasesInternal(connectionInfo);
				AuditEvent.Default.Emit("list databases", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list databases", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<DatabaseGet> ListDatabasesInternal(IConnectionInfo info)
	{
		List<DatabaseGet> list = new List<DatabaseGet>();
		if (info.IsOffline)
		{
			Microsoft.AnalysisServices.Tabular.Database database = info.Database;
			list.Add(new DatabaseGet
			{
				Name = database.Name,
				Id = database.ID,
				Description = database.Description,
				State = database.State.ToString(),
				CreatedTimestamp = database.CreatedTimestamp,
				LastProcessed = database.LastProcessed,
				LastUpdate = database.LastUpdate,
				LastSchemaUpdate = database.LastSchemaUpdate,
				EstimatedSize = database.EstimatedSize,
				CompatibilityLevel = database.CompatibilityLevel,
				Collation = database.Collation,
				Language = database.Language,
				Model = database.Model?.Name,
				ModelType = database.ModelType.ToString()
			});
		}
		else
		{
			ITabularServer tabularServer = info.TabularServer;
			if (tabularServer?.Databases != null)
			{
				foreach (Microsoft.AnalysisServices.Tabular.Database database2 in tabularServer.Databases)
				{
					list.Add(new DatabaseGet
					{
						Name = database2.Name,
						Id = database2.ID,
						Description = database2.Description,
						State = database2.State.ToString(),
						CreatedTimestamp = database2.CreatedTimestamp,
						LastProcessed = database2.LastProcessed,
						LastUpdate = database2.LastUpdate,
						LastSchemaUpdate = database2.LastSchemaUpdate,
						EstimatedSize = database2.EstimatedSize,
						CompatibilityLevel = database2.CompatibilityLevel,
						Collation = database2.Collation,
						Language = database2.Language,
						Model = database2.Model?.Name,
						ModelType = database2.ModelType.ToString()
					});
				}
			}
		}
		return list.OrderBy((DatabaseGet d) => d.Name).ToList();
	}

	public static async Task<DatabaseOperationResult> UpdateDatabase(string? connectionName, DatabaseUpdate update)
	{
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database update definition cannot be null", ErrorSource.User);
		}
		DatabaseOperationResult result;
		await using (IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName))
		{
			string text = update.Name;
			if (string.IsNullOrWhiteSpace(text))
			{
				if (conn.Database == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Database name is required for Update operation", ErrorSource.User);
				}
				text = (update.Name = conn.Database.Name);
			}
			if (conn.IsOffline)
			{
				result = UpdateDatabaseOffline(conn, update);
			}
			else
			{
				ConnectionValidator.ValidateOnlineConnection(conn);
				result = await UpdateDatabaseInternal(conn, update, text);
			}
		}
		return result;
	}

	private static DatabaseOperationResult UpdateDatabaseOffline(IConnectionInfo info, DatabaseUpdate update)
	{
		Microsoft.AnalysisServices.Tabular.Database database = info.Database;
		if (database == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("No database loaded for offline connection", ErrorSource.User);
		}
		if (update.Name != database.Name)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Offline database name mismatch: expected '{database.Name}', but got '{update.Name}'", ErrorSource.User);
		}
		bool hasChanges = ApplyDatabaseUpdates(database, update);
		return new DatabaseOperationResult
		{
			State = database.State.ToString(),
			ErrorMessage = null,
			DatabaseName = database.Name,
			HasChanges = hasChanges
		};
	}

	private static bool ApplyDatabaseUpdates(Microsoft.AnalysisServices.Tabular.Database database, DatabaseUpdate update)
	{
		bool result = false;
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (text != database.Description)
			{
				database.Description = text;
				result = true;
			}
		}
		if (update.CompatibilityLevel.HasValue && update.CompatibilityLevel.Value != database.CompatibilityLevel)
		{
			database.CompatibilityLevel = update.CompatibilityLevel.Value;
			result = true;
		}
		if (update.Collation != null)
		{
			string text2 = (string.IsNullOrEmpty(update.Collation) ? null : update.Collation);
			if (text2 != database.Collation)
			{
				database.Collation = text2;
				result = true;
			}
		}
		if (update.Language.HasValue && update.Language.Value != database.Language)
		{
			database.Language = update.Language.Value;
			result = true;
		}
		if (update.Annotations != null && database.Model != null && AnnotationHelpers.ReplaceAnnotations(database.Model, update.Annotations, (Model m) => m.Annotations))
		{
			result = true;
		}
		return result;
	}

	private static async Task<DatabaseOperationResult> UpdateDatabaseInternal(IConnectionInfo info, DatabaseUpdate update, string targetDatabaseName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("DatabaseUpdate cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrEmpty(targetDatabaseName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Target database name cannot be null or empty", ErrorSource.User);
		}
		ITabularServer tabularServer = info.TabularServer;
		if (tabularServer?.Databases == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("No databases available on the server", ErrorSource.User);
		}
		Microsoft.AnalysisServices.Tabular.Database database = tabularServer.FindDatabase(targetDatabaseName);
		if (database == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database '" + targetDatabaseName + "' not found on server", ErrorSource.User);
		}
		if (!ApplyDatabaseUpdates(database, update))
		{
			return new DatabaseOperationResult
			{
				State = database.State.ToString(),
				ErrorMessage = null,
				DatabaseName = database.Name,
				HasChanges = false
			};
		}
		try
		{
			await RetryHelper.Default.ExecuteWithRetryAsync(delegate
			{
				database.Update();
				return Task.CompletedTask;
			}, new RetryOptions
			{
				ShouldRetry = (Exception ex2) => ex2 is ConnectionException
			});
			AuditEvent.Default.Emit("update database", success: true, OperationType.Update, info);
		}
		catch (Exception ex)
		{
			AuditEvent.Default.Emit("update database", success: false, OperationType.Update, info);
			throw new McpExceptionWithSource("Failed to update database properties: " + ex.Message, ex, null, "Failed to update database properties; see inner error details.");
		}
		return new DatabaseOperationResult
		{
			State = database.State.ToString(),
			ErrorMessage = null,
			DatabaseName = database.Name,
			HasChanges = true
		};
	}

	public static TmdlDeserializeResult ImportFromTmdlFolder(string folderPath, string? connectionName = null)
	{
		if (string.IsNullOrWhiteSpace(folderPath))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Folder path cannot be null or empty", ErrorSource.User);
		}
		if (!Directory.Exists(folderPath))
		{
			throw new McpExceptionWithSource("TMDL folder does not exist: " + folderPath, ErrorSource.User, "The specified TMDL folder does not exist.");
		}
		try
		{
			Microsoft.AnalysisServices.Tabular.Database database = Microsoft.AnalysisServices.Tabular.TmdlSerializer.DeserializeDatabaseFromFolder(folderPath);
			string connectionName2 = ConnectionOperations.CreateOfflineConnection(connectionName ?? ("TMDL-" + folderPath), database, folderPath);
			return new TmdlDeserializeResult
			{
				Success = true,
				ConnectionName = connectionName2,
				DatabaseName = database.Name,
				FolderPath = folderPath,
				TablesLoaded = (database.Model?.Tables?.Count).GetValueOrDefault(),
				MeasuresLoaded = (database.Model?.Tables?.Sum((Table t) => t.Measures.Count)).GetValueOrDefault(),
				RelationshipsLoaded = (database.Model?.Relationships?.Count).GetValueOrDefault(),
				LoadedAt = DateTime.UtcNow,
				Message = "Successfully loaded database '" + database.Name + "' from TMDL folder"
			};
		}
		catch (Exception ex)
		{
			throw new McpExceptionWithSource("Failed to import TMDL folder: " + ex.Message, ex, null, "Failed to import TMDL folder; see inner error details.");
		}
	}

	public static async Task<TmdlSerializeResult> ExportToTmdlFolder(string? connectionName, string? targetPath = null)
	{
		TmdlSerializeResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				ConnectionValidator.ValidateForModelOperations(connectionInfo);
				string finalTargetPath;
				if (!string.IsNullOrWhiteSpace(targetPath))
				{
					finalTargetPath = targetPath;
				}
				else
				{
					if (!connectionInfo.IsOffline || string.IsNullOrWhiteSpace(connectionInfo.SourcePath))
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage("Target path must be specified for online connections or offline connections without a stored source path", ErrorSource.User);
					}
					finalTargetPath = connectionInfo.SourcePath;
				}
				TmdlSerializeResult tmdlSerializeResult = ExportToTmdlFolderInternal(connectionInfo, finalTargetPath);
				AuditEvent.Default.Emit("export database to TMDL folder", success: true, OperationType.Read, connectionInfo);
				result = tmdlSerializeResult;
			}
			catch
			{
				AuditEvent.Default.Emit("export database to TMDL folder", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static TmdlSerializeResult ExportToTmdlFolderInternal(IConnectionInfo info, string finalTargetPath)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(finalTargetPath))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Final target path cannot be null or empty", ErrorSource.User);
		}
		try
		{
			Directory.CreateDirectory(finalTargetPath);
			Microsoft.AnalysisServices.Tabular.TmdlSerializer.SerializeDatabaseToFolder(info.Database, finalTargetPath);
			List<string> tmdlFiles = GetTmdlFiles(finalTargetPath);
			return new TmdlSerializeResult
			{
				Success = true,
				FolderPath = finalTargetPath,
				DatabaseName = info.Database.Name,
				FilesCreated = tmdlFiles,
				FileCount = tmdlFiles.Count,
				SerializedAt = DateTime.UtcNow,
				Message = "Successfully exported database '" + info.Database.Name + "' to TMDL folder"
			};
		}
		catch (Exception ex)
		{
			throw new McpExceptionWithSource("Failed to export database to TMDL folder: " + ex.Message, ex, null, "Failed to export database to TMDL folder; see inner error details.");
		}
	}

	public static async Task<DatabaseOperationResponse> DeployToFabric(string? sourceConnectionName, DeployToFabricRequest request)
	{
		if (request == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("DeployToFabricRequest cannot be null", ErrorSource.User);
		}
		IConnectionInfo sourceConnForAudit = null;
		try
		{
			DatabaseOperationResponse result;
			await using (IConnectionInfo sourceConn = await ConnectionOperations.GetAsync(sourceConnectionName))
			{
				sourceConnForAudit = sourceConn;
				if (sourceConn.Database == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Source connection is not bound to a database. Reconnect with an Initial Catalog selected or open an offline database.", ErrorSource.User);
				}
				DatabaseExportTmsl tmslOptions = new DatabaseExportTmsl
				{
					TmslOperationType = "CreateOrReplace",
					IncludeRestricted = (request.IncludeRestricted == true),
					MaxReturnCharacters = -1
				};
				TmslExportResult tmslExportResult = ExportTMSLInternal(sourceConn, tmslOptions);
				if (!tmslExportResult.Success)
				{
					throw new McpExceptionWithSource("Failed to generate TMSL: " + tmslExportResult.ErrorMessage, "Failed to generate TMSL; see inner error details.");
				}
				string tmslScript = tmslExportResult.Content;
				if (!string.IsNullOrWhiteSpace(request.NewDatabaseName))
				{
					tmslScript = RewriteDbNameInTmsl(tmslScript, request.NewDatabaseName);
				}
				string targetXmla = ResolveTargetXmla(request);
				AccessToken accessToken = await AuthService.GetAccessTokenAsync(request.ClearCredential);
				using Microsoft.AnalysisServices.Tabular.Server server = new Microsoft.AnalysisServices.Tabular.Server();
				server.AccessToken = accessToken;
				server.Connect(targetXmla);
				XmlaResultCollection xmlaResultCollection = server.Execute(tmslScript);
				if (xmlaResultCollection.ContainsErrors)
				{
					string text = string.Join("\n", xmlaResultCollection.Cast<XmlaResult>().SelectMany((XmlaResult r) => from XmlaMessage m in r.Messages
						select m.Description).ToArray());
					throw new McpExceptionWithSource((!string.IsNullOrEmpty(text)) ? text : "TMSL execution failed with unknown errors", ErrorSource.User, "TMSL execution failed; see inner error details.");
				}
				string text2 = request.NewDatabaseName ?? sourceConn.Database.Name;
				AuditEvent.Default.Emit("deploy to Fabric", success: true, OperationType.Create, sourceConn);
				result = new DatabaseOperationResponse
				{
					Success = true,
					Operation = "DeployToFabric",
					DatabaseName = text2,
					Message = "Successfully deployed database '" + text2 + "' to Fabric workspace"
				};
			}
			return result;
		}
		catch (Exception ex)
		{
			if (sourceConnForAudit != null)
			{
				AuditEvent.Default.Emit("deploy to Fabric", success: false, OperationType.Create, sourceConnForAudit);
			}
			return new DatabaseOperationResponse
			{
				Success = false,
				Operation = "DeployToFabric",
				Message = "Failed to deploy database to Fabric: " + ex.Message,
				Exceptions = { ex }
			};
		}
	}

	public static async Task<DatabaseCreateResult> CreateOfflineDb(DatabaseCreate definition, string? connectionName = null, string? proToolingValue = null)
	{
		if (definition == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database creation definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(definition.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database name is required", ErrorSource.User);
		}
		string connectionName2 = connectionName ?? definition.Name;
		try
		{
			Microsoft.AnalysisServices.Tabular.Database database = new Microsoft.AnalysisServices.Tabular.Database
			{
				Name = definition.Name,
				ID = definition.Name
			};
			if (definition.Description != null)
			{
				database.Description = definition.Description;
			}
			if (definition.CompatibilityLevel.HasValue)
			{
				database.CompatibilityLevel = definition.CompatibilityLevel.Value;
			}
			if (definition.Collation != null)
			{
				database.Collation = definition.Collation;
			}
			if (definition.Language.HasValue)
			{
				database.Language = definition.Language.Value;
			}
			Model model = (database.Model = new Model
			{
				Name = definition.Name,
				Culture = "en-US",
				DefaultPowerBIDataSourceVersion = PowerBIDataSourceVersion.PowerBI_V3,
				SourceQueryCulture = "en-US",
				DataAccessOptions = new DataAccessOptions
				{
					LegacyRedirects = true,
					ReturnErrorValuesAsNull = true
				}
			});
			if (definition.Annotations != null)
			{
				foreach (KeyValuePair<string, string> annotation in definition.Annotations)
				{
					if (!string.IsNullOrWhiteSpace(annotation.Key))
					{
						model.Annotations.Add(new Microsoft.AnalysisServices.Tabular.Annotation
						{
							Name = annotation.Key,
							Value = (annotation.Value ?? string.Empty)
						});
					}
				}
			}
			string createdConnectionName = ConnectionOperations.CreateOfflineConnection(connectionName2, database, string.Empty);
			DatabaseCreateResult result;
			await using (IConnectionInfo info = await ConnectionOperations.GetAsync(createdConnectionName))
			{
				ModelOperations.AddProToolingAnnotation(info, proToolingValue);
				result = new DatabaseCreateResult
				{
					Success = true,
					ConnectionName = createdConnectionName,
					DatabaseName = database.Name,
					ModelName = model.Name,
					CreatedAt = DateTime.UtcNow,
					Message = $"Successfully created empty database '{database.Name}' with connection '{createdConnectionName}'"
				};
			}
			return result;
		}
		catch (Exception ex)
		{
			throw new McpExceptionWithSource("Failed to create empty database: " + ex.Message, ex, null, "Failed to create empty database; see inner error details.");
		}
	}

	private static List<string> GetTmdlFiles(string folderPath)
	{
		return (from string f in from f in Directory.GetFiles(folderPath, "*.tmdl", SearchOption.AllDirectories).Select(Path.GetFileName)
				where f != null
				select f
			orderby f
			select f).ToList();
	}

	public static async Task<TmdlExportResult> ExportTMDL(string? connectionName, DatabaseExportTmdl options)
	{
		TmdlExportResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				TmdlExportResult tmdlExportResult = ExportTMDLInternal(connectionInfo, options);
				AuditEvent.Default.Emit("export database to TMDL", success: true, OperationType.Read, connectionInfo);
				result = tmdlExportResult;
			}
			catch
			{
				AuditEvent.Default.Emit("export database to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static TmdlExportResult ExportTMDLInternal(IConnectionInfo info, DatabaseExportTmdl options)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		try
		{
			Microsoft.AnalysisServices.Tabular.Database database = info.Database;
			string objectName = info.Database.Name ?? "Database";
			string content = Microsoft.AnalysisServices.Tabular.TmdlSerializer.SerializeDatabase(database, options.SerializationOptions.ToMetadataSerializationOptions());
			var (processedContent, isTruncated, savedFilePath, warnings) = ExportContentProcessor.ProcessExportContent(content, options);
			return TmdlExportResult.CreateSuccess(objectName, "Database", content, processedContent, isTruncated, savedFilePath, warnings, options);
		}
		catch (Exception ex)
		{
			return TmdlExportResult.CreateFailure(info.Database?.Name ?? "Database", "Database", ex.Message, (ex is ArgumentException || ex is InvalidOperationException) ? ErrorSource.User : ErrorSource.System);
		}
	}

	public static async Task<TmslExportResult> ExportTMSL(string? connectionName, DatabaseExportTmsl tmslOptions)
	{
		if (tmslOptions == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tmslOptions is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tmslOptions.TmslOperationType))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TmslOperationType is required in tmslOptions", ErrorSource.User);
		}
		TmslExportResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				TmslExportResult tmslExportResult = ExportTMSLInternal(connectionInfo, tmslOptions);
				AuditEvent.Default.Emit("export database to TMSL", success: true, OperationType.Read, connectionInfo);
				result = tmslExportResult;
			}
			catch
			{
				AuditEvent.Default.Emit("export database to TMSL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static TmslExportResult ExportTMSLInternal(IConnectionInfo info, DatabaseExportTmsl tmslOptions)
	{
		if (tmslOptions == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tmslOptions is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tmslOptions.TmslOperationType))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TmslOperationType is required in tmslOptions", ErrorSource.User);
		}
		try
		{
			Microsoft.AnalysisServices.Tabular.Database database = info.Database;
			if (!Enum.TryParse<TmslOperationType>(tmslOptions.TmslOperationType, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(TmslOperationType));
				throw new McpExceptionWithSource("Invalid TmslOperationType '" + tmslOptions.TmslOperationType + "'. Valid values: " + string.Join(", ", names), ErrorSource.User, "Invalid TmslOperationType supplied. Valid values: " + string.Join(", ", names) + ".");
			}
			TmslOperationRequest tmslOperationRequest = new TmslOperationRequest
			{
				OperationType = result,
				IncludeRestricted = (tmslOptions.IncludeRestricted == true)
			};
			if (!string.IsNullOrWhiteSpace(tmslOptions.RefreshType))
			{
				if (!Enum.TryParse<Microsoft.AnalysisServices.Tabular.RefreshType>(tmslOptions.RefreshType, ignoreCase: true, out var result2))
				{
					string[] names2 = Enum.GetNames(typeof(Microsoft.AnalysisServices.Tabular.RefreshType));
					throw new McpExceptionWithSource("Invalid RefreshType '" + tmslOptions.RefreshType + "'. Valid values: " + string.Join(", ", names2), ErrorSource.User, "Invalid RefreshType supplied. Valid values: " + string.Join(", ", names2) + ".");
				}
				tmslOperationRequest.RefreshType = result2;
			}
			TmslExportResult tmslExportResult = TmslExportResult.FromLegacyResult(TmslScriptingService.GenerateScript(database, result, tmslOperationRequest));
			(string Content, bool IsTruncated, string? SavedFilePath, List<string> Warnings) tuple = ExportContentProcessor.ProcessExportContent(tmslExportResult.Content, tmslOptions);
			string item = tuple.Content;
			bool item2 = tuple.IsTruncated;
			string item3 = tuple.SavedFilePath;
			List<string> item4 = tuple.Warnings;
			tmslExportResult.Content = item;
			tmslExportResult.IsTruncated = item2;
			tmslExportResult.SavedFilePath = item3;
			tmslExportResult.Warnings.AddRange(item4);
			tmslExportResult.AppliedOptions = tmslOptions;
			return tmslExportResult;
		}
		catch (Exception ex)
		{
			return TmslExportResult.CreateFailure(info.Database?.Name ?? "Database", "Database", tmslOptions.TmslOperationType, ex.Message, (ex is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : ((ex is ArgumentException || ex is InvalidOperationException) ? ErrorSource.User : ErrorSource.System));
		}
	}

	private static string RewriteDbNameInTmsl(string tmsl, string newName)
	{
		try
		{
			JsonObject jsonObject = JsonNode.Parse(tmsl).AsObject();
			JsonObject obj = jsonObject["createOrReplace"]?.AsObject() ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid TMSL format. 'createOrReplace' is missing.", ErrorSource.User);
			JsonObject jsonObject2 = obj["object"]?.AsObject();
			if (jsonObject2 != null)
			{
				jsonObject2["database"] = newName;
			}
			JsonObject jsonObject3 = obj["database"]?.AsObject();
			if (jsonObject3 == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid TMSL format. 'createOrReplace.database' is missing.", ErrorSource.User);
			}
			jsonObject3["name"] = newName;
			if (jsonObject3.ContainsKey("id"))
			{
				jsonObject3["id"] = newName;
			}
			return jsonObject.ToJsonString(new JsonSerializerOptions
			{
				WriteIndented = false
			});
		}
		catch (Exception ex) when (!(ex is McpException))
		{
			throw new McpExceptionWithSource("Failed to rewrite database name in TMSL: " + ex.Message, ex, null, "Failed to rewrite database name in TMSL; see inner error details.");
		}
	}

	private static string ResolveTargetXmla(DeployToFabricRequest request)
	{
		if (!string.IsNullOrWhiteSpace(request.TargetConnectionString))
		{
			string text = request.TargetConnectionString;
			if (request.ConnectTimeoutSeconds.HasValue && request.ConnectTimeoutSeconds > 0 && !text.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
			{
				text = ConnectionStringHelper.AddParameterToConnectionString(text, "Timeout", request.ConnectTimeoutSeconds.Value.ToString());
			}
			return text;
		}
		if (!string.IsNullOrWhiteSpace(request.TargetWorkspaceName))
		{
			string text2 = ConnectionStringHelper.BuildConnectionString(ConnectionOperations.BuildPowerBiXmlaEndpoint(request.TargetWorkspaceName, request.TargetTenantName));
			if (request.ConnectTimeoutSeconds.HasValue && request.ConnectTimeoutSeconds > 0)
			{
				text2 = ConnectionStringHelper.AddParameterToConnectionString(text2, "Timeout", request.ConnectTimeoutSeconds.Value.ToString());
			}
			return text2;
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Specify TargetConnectionString or TargetWorkspaceName.", ErrorSource.User);
	}

	public static BimDeserializeResult ImportFromBimFile(string filePath, string? connectionName = null, PowerBIModelingMCP.Library.Contracts.CompatibilityMode compatibilityMode = PowerBIModelingMCP.Library.Contracts.CompatibilityMode.PowerBI)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("File path cannot be null or empty", ErrorSource.User);
		}
		if (!File.Exists(filePath))
		{
			throw new McpExceptionWithSource("BIM file does not exist: " + filePath, ErrorSource.User, "The specified BIM file does not exist.");
		}
		try
		{
			Microsoft.AnalysisServices.Tabular.Database database = Microsoft.AnalysisServices.Tabular.JsonSerializer.DeserializeDatabase(File.ReadAllText(filePath), mode: MapCompatibilityMode(compatibilityMode), options: new DeserializeOptions());
			string connectionName2 = ConnectionOperations.CreateOfflineConnection(connectionName ?? ("BIM-" + database.Name), database, filePath);
			return new BimDeserializeResult
			{
				Success = true,
				ConnectionName = connectionName2,
				DatabaseName = database.Name,
				FilePath = filePath,
				TablesLoaded = (database.Model?.Tables?.Count).GetValueOrDefault(),
				MeasuresLoaded = (database.Model?.Tables?.Sum((Table t) => t.Measures.Count)).GetValueOrDefault(),
				RelationshipsLoaded = (database.Model?.Relationships?.Count).GetValueOrDefault(),
				LoadedAt = DateTime.UtcNow,
				Message = "Successfully loaded database '" + database.Name + "' from BIM file"
			};
		}
		catch (McpException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			throw new McpExceptionWithSource("Failed to import BIM file: " + ex2.Message, ex2, null, "Failed to import BIM file; see inner error details.");
		}
	}

	public static async Task<BimSerializeResult> ExportToBimFile(string filePath, string? connectionName = null)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("File path cannot be null or empty", ErrorSource.User);
		}
		BimSerializeResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				ConnectionValidator.ValidateForModelOperations(connectionInfo);
				try
				{
					string contents = Microsoft.AnalysisServices.Tabular.JsonSerializer.SerializeDatabase(connectionInfo.Database, new SerializeOptions());
					string directoryName = Path.GetDirectoryName(filePath);
					if (!string.IsNullOrEmpty(directoryName))
					{
						Directory.CreateDirectory(directoryName);
					}
					File.WriteAllText(filePath, contents);
					BimSerializeResult bimSerializeResult = new BimSerializeResult
					{
						Success = true,
						FilePath = filePath,
						DatabaseName = connectionInfo.Database.Name,
						Message = "Successfully exported database '" + connectionInfo.Database.Name + "' to BIM file"
					};
					AuditEvent.Default.Emit("export database to BIM file", success: true, OperationType.Read, connectionInfo);
					result = bimSerializeResult;
				}
				catch (McpException)
				{
					throw;
				}
				catch (Exception ex2)
				{
					throw new McpExceptionWithSource("Failed to export database to BIM file: " + ex2.Message, ex2, null, "Failed to export database to BIM file; see inner error details.");
				}
			}
			catch
			{
				AuditEvent.Default.Emit("export database to BIM file", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static Microsoft.AnalysisServices.CompatibilityMode MapCompatibilityMode(PowerBIModelingMCP.Library.Contracts.CompatibilityMode mode)
	{
		return mode switch
		{
			PowerBIModelingMCP.Library.Contracts.CompatibilityMode.PowerBI => Microsoft.AnalysisServices.CompatibilityMode.PowerBI, 
			PowerBIModelingMCP.Library.Contracts.CompatibilityMode.Full => Microsoft.AnalysisServices.CompatibilityMode.AnalysisServices, 
			_ => Microsoft.AnalysisServices.CompatibilityMode.PowerBI, 
		};
	}
}
