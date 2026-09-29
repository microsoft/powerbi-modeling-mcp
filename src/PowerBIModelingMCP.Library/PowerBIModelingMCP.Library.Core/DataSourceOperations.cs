using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class DataSourceOperations
{
	public static void ValidateDataSourceDefinition(DataSourceBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("DataSource definition cannot be null", ErrorSource.User);
		}
		if (isCreate)
		{
			if (string.IsNullOrWhiteSpace(def.Name))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
			}
			if (string.IsNullOrWhiteSpace(def.ConnectionString))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionString is required", ErrorSource.User);
			}
		}
		if (!string.IsNullOrWhiteSpace(def.ImpersonationMode) && !Enum.IsDefined(typeof(ImpersonationMode), def.ImpersonationMode))
		{
			string[] names = Enum.GetNames(typeof(ImpersonationMode));
			throw new McpExceptionWithSource("Invalid ImpersonationMode '" + def.ImpersonationMode + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid ImpersonationMode supplied. Valid values are: " + string.Join(", ", names) + ".");
		}
		if (!string.IsNullOrWhiteSpace(def.Isolation) && !Enum.IsDefined(typeof(DatasourceIsolation), def.Isolation))
		{
			string[] names2 = Enum.GetNames(typeof(DatasourceIsolation));
			throw new McpExceptionWithSource("Invalid Isolation '" + def.Isolation + "'. Valid values are: " + string.Join(", ", names2), ErrorSource.User, "Invalid Isolation supplied. Valid values are: " + string.Join(", ", names2) + ".");
		}
	}

	public static async Task<List<DataSourceList>> ListDataSources(string? connectionName)
	{
		List<DataSourceList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<DataSourceList> list = ListDataSourcesInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list data sources", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list data sources", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<DataSourceList> ListDataSourcesInternal(Database db)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		List<DataSourceList> list = new List<DataSourceList>();
		foreach (DataSource dataSource in db.Model.DataSources)
		{
			list.Add(new DataSourceList
			{
				Name = dataSource.Name,
				Description = ((!string.IsNullOrEmpty(dataSource.Description)) ? dataSource.Description : null),
				Type = dataSource.Type.ToString()
			});
		}
		return list;
	}

	private static DataSourceGet GetDataSourceInternal(Database db, string dataSourceName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(dataSourceName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("dataSourceName is required", ErrorSource.User);
		}
		DataSource dataSource = db.Model.DataSources.Find(dataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + dataSourceName + "' not found", ErrorSource.User);
		DataSourceGet dataSourceGet = new DataSourceGet
		{
			Name = dataSource.Name,
			Type = dataSource.Type.ToString(),
			Description = dataSource.Description,
			MaxConnections = dataSource.MaxConnections,
			Annotations = new List<KeyValuePair<string, string>>(),
			ExtendedProperties = new List<PowerBIModelingMCP.Library.Common.DataStructures.ExtendedProperty>()
		};
		if (dataSource is ProviderDataSource providerDataSource)
		{
			dataSourceGet.Provider = providerDataSource.Provider;
			dataSourceGet.ConnectionString = providerDataSource.ConnectionString;
			dataSourceGet.ImpersonationMode = providerDataSource.ImpersonationMode.ToString();
			dataSourceGet.Account = providerDataSource.Account;
			dataSourceGet.Isolation = providerDataSource.Isolation.ToString();
		}
		else if (dataSource is StructuredDataSource)
		{
			dataSourceGet.Provider = "Structured/M";
			dataSourceGet.ConnectionString = "[StructuredDataSource - use ConnectionDetails]";
		}
		foreach (Annotation annotation in dataSource.Annotations)
		{
			dataSourceGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		dataSourceGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromDataSource(dataSource);
		return dataSourceGet;
	}

	private static OperationResult CreateDataSourceInternal(IConnectionInfo info, DataSourceDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateDataSourceDefinition(def, isCreate: true);
		Database database = info.Database;
		if (database.Model.DataSources.Contains(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + def.Name + "' already exists", ErrorSource.User);
		}
		ProviderDataSource providerDataSource = new ProviderDataSource
		{
			Name = def.Name,
			ConnectionString = def.ConnectionString,
			Provider = (def.Provider ?? "System.Data.SqlClient")
		};
		ApplyDataSourceProperties(providerDataSource, def);
		database.Model.DataSources.Add(providerDataSource);
		TransactionOperations.RecordOperation(info, "Created data source '" + def.Name + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "create data source", OperationType.Create);
		return new OperationResult
		{
			Success = true,
			Message = "Data source '" + def.Name + "' created successfully",
			ObjectName = def.Name,
			ObjectType = ObjectType.DataSource,
			Operation = Operation.Create
		};
	}

	private static OperationResult UpdateDataSourceInternal(IConnectionInfo info, DataSourceDefinition update)
	{
		ValidateDataSourceDefinition(update, isCreate: false);
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(update.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required to identify the data source to update", ErrorSource.User);
		}
		if (!ApplyDataSourceUpdates(info.Database.Model.DataSources.Find(update.Name) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + update.Name + "' not found", ErrorSource.User), update))
		{
			return new OperationResult
			{
				Success = true,
				Message = "Data source '" + update.Name + "' is already in the requested state",
				ObjectName = update.Name,
				ObjectType = ObjectType.DataSource,
				Operation = Operation.Update,
				HasChanges = false
			};
		}
		TransactionOperations.RecordOperation(info, "Updated data source '" + update.Name + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "update data source", OperationType.Update);
		return new OperationResult
		{
			Success = true,
			Message = "Data source '" + update.Name + "' updated successfully",
			ObjectName = update.Name,
			ObjectType = ObjectType.DataSource,
			Operation = Operation.Update,
			HasChanges = true
		};
	}

	private static void DeleteDataSourceInternal(IConnectionInfo info, string dataSourceName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(dataSourceName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("dataSourceName is required", ErrorSource.User);
		}
		Database database = info.Database;
		DataSource ds = database.Model.DataSources.Find(dataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + dataSourceName + "' not found", ErrorSource.User);
		if (database.Model.Tables.Any((Table t) => t.Partitions.Any((Partition p) => p.Source is QueryPartitionSource queryPartitionSource && queryPartitionSource.DataSource == ds)))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete data source '" + dataSourceName + "' as it is referenced by one or more table partitions", ErrorSource.User);
		}
		database.Model.DataSources.Remove(ds);
		TransactionOperations.RecordOperation(info, "Deleted data source '" + dataSourceName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete data source", OperationType.Delete);
	}

	private static void RenameDataSourceInternal(IConnectionInfo info, string currentName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(currentName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("currentName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		Database database = info.Database;
		DataSource dataSource = database.Model.DataSources.Find(currentName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + currentName + "' not found", ErrorSource.User);
		if (database.Model.DataSources.Contains(newName) && !string.Equals(currentName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + newName + "' already exists", ErrorSource.User);
		}
		dataSource.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed data source '{currentName}' to '{newName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename data source", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	public static async Task<OperationResult> TestDataSource(string? connectionName, string dataSourceName)
	{
		if (string.IsNullOrWhiteSpace(dataSourceName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("dataSourceName is required", ErrorSource.User);
		}
		OperationResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			result = TestDataSourceInternal(connectionInfo.Database, dataSourceName);
		}
		return result;
	}

	private static OperationResult TestDataSourceInternal(Database db, string dataSourceName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(dataSourceName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("dataSourceName is required", ErrorSource.User);
		}
		DataSource dataSource = db.Model.DataSources.Find(dataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + dataSourceName + "' not found", ErrorSource.User);
		try
		{
			bool flag = false;
			string text = "";
			if (dataSource is ProviderDataSource providerDataSource)
			{
				flag = !string.IsNullOrEmpty(providerDataSource.ConnectionString);
				text = (flag ? ("Data source '" + dataSourceName + "' connection configuration is valid") : ("Data source '" + dataSourceName + "' connection test failed - no connection string"));
			}
			else if (dataSource is StructuredDataSource structuredDataSource)
			{
				flag = structuredDataSource.ConnectionDetails != null;
				text = (flag ? ("Data source '" + dataSourceName + "' connection configuration is valid") : ("Data source '" + dataSourceName + "' connection test failed - no connection details"));
			}
			else
			{
				flag = false;
				text = $"Data source '{dataSourceName}' type '{dataSource.Type}' is not supported for testing";
			}
			return new OperationResult
			{
				Success = flag,
				Message = text,
				ObjectName = dataSourceName,
				ObjectType = ObjectType.DataSource,
				Operation = Operation.Get
			};
		}
		catch (Exception ex)
		{
			return new OperationResult
			{
				Success = false,
				Message = "Data source '" + dataSourceName + "' connection test failed: " + ex.Message,
				ObjectName = dataSourceName,
				ObjectType = ObjectType.DataSource,
				Operation = Operation.Get,
				Exception = ex
			};
		}
	}

	private static string GetProviderInfo(DataSource ds)
	{
		if (ds is ProviderDataSource providerDataSource)
		{
			return providerDataSource.Provider ?? "Unknown";
		}
		if (ds is StructuredDataSource)
		{
			return "Structured/M";
		}
		return ds.Type.ToString();
	}

	private static void ApplyDataSourceProperties(ProviderDataSource dataSource, DataSourceBase def)
	{
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			dataSource.Description = def.Description;
		}
		if (def.MaxConnections.HasValue)
		{
			dataSource.MaxConnections = def.MaxConnections.Value;
		}
		if (def.Timeout.HasValue)
		{
			dataSource.Timeout = def.Timeout.Value;
		}
		if (!string.IsNullOrWhiteSpace(def.ImpersonationMode) && Enum.TryParse<ImpersonationMode>(def.ImpersonationMode, ignoreCase: true, out var result))
		{
			dataSource.ImpersonationMode = result;
		}
		if (!string.IsNullOrWhiteSpace(def.Account))
		{
			dataSource.Account = def.Account;
		}
		if (!string.IsNullOrWhiteSpace(def.Password))
		{
			dataSource.Password = def.Password;
		}
		if (!string.IsNullOrWhiteSpace(def.Isolation) && Enum.TryParse<DatasourceIsolation>(def.Isolation, ignoreCase: true, out var result2))
		{
			dataSource.Isolation = result2;
		}
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				dataSource.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToDataSource(dataSource, def.ExtendedProperties);
		}
	}

	private static bool ApplyDataSourceUpdates(DataSource dataSource, DataSourceDefinition update)
	{
		bool result = false;
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (text != dataSource.Description)
			{
				dataSource.Description = text;
				result = true;
			}
		}
		if (update.MaxConnections.HasValue && dataSource.MaxConnections != update.MaxConnections.Value)
		{
			dataSource.MaxConnections = update.MaxConnections.Value;
			result = true;
		}
		if (dataSource is ProviderDataSource providerDataSource)
		{
			if (update.ConnectionString != null)
			{
				string text2 = (string.IsNullOrEmpty(update.ConnectionString) ? null : update.ConnectionString);
				if (text2 != providerDataSource.ConnectionString)
				{
					providerDataSource.ConnectionString = text2;
					result = true;
				}
			}
			if (update.Timeout.HasValue && providerDataSource.Timeout != update.Timeout.Value)
			{
				providerDataSource.Timeout = update.Timeout.Value;
				result = true;
			}
			if (update.Provider != null)
			{
				string text3 = (string.IsNullOrEmpty(update.Provider) ? null : update.Provider);
				if (text3 != providerDataSource.Provider)
				{
					providerDataSource.Provider = text3;
					result = true;
				}
			}
			if (!string.IsNullOrWhiteSpace(update.ImpersonationMode))
			{
				if (!Enum.TryParse<ImpersonationMode>(update.ImpersonationMode, ignoreCase: true, out var result2))
				{
					string[] names = Enum.GetNames(typeof(ImpersonationMode));
					throw new McpExceptionWithSource("Invalid ImpersonationMode '" + update.ImpersonationMode + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid ImpersonationMode supplied. Valid values are: " + string.Join(", ", names) + ".");
				}
				if (providerDataSource.ImpersonationMode != result2)
				{
					providerDataSource.ImpersonationMode = result2;
					result = true;
				}
			}
			if (update.Account != null)
			{
				string text4 = (string.IsNullOrEmpty(update.Account) ? null : update.Account);
				if (text4 != providerDataSource.Account)
				{
					providerDataSource.Account = text4;
					result = true;
				}
			}
			if (update.Password != null)
			{
				string text5 = (string.IsNullOrEmpty(update.Password) ? null : update.Password);
				if (text5 != providerDataSource.Password)
				{
					providerDataSource.Password = text5;
					result = true;
				}
			}
			if (!string.IsNullOrWhiteSpace(update.Isolation))
			{
				if (!Enum.TryParse<DatasourceIsolation>(update.Isolation, ignoreCase: true, out var result3))
				{
					string[] names2 = Enum.GetNames(typeof(DatasourceIsolation));
					throw new McpExceptionWithSource("Invalid Isolation '" + update.Isolation + "'. Valid values are: " + string.Join(", ", names2), ErrorSource.User, "Invalid Isolation supplied. Valid values are: " + string.Join(", ", names2) + ".");
				}
				if (providerDataSource.Isolation != result3)
				{
					providerDataSource.Isolation = result3;
					result = true;
				}
			}
		}
		else
		{
			if (update.ConnectionString != null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionString can only be updated on ProviderDataSource, not on StructuredDataSource", ErrorSource.User);
			}
			if (update.Timeout.HasValue)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Timeout can only be updated on ProviderDataSource, not on StructuredDataSource", ErrorSource.User);
			}
			if (update.Provider != null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Provider can only be updated on ProviderDataSource, not on StructuredDataSource", ErrorSource.User);
			}
			if (update.ImpersonationMode != null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("ImpersonationMode can only be updated on ProviderDataSource, not on StructuredDataSource", ErrorSource.User);
			}
			if (update.Account != null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Account can only be updated on ProviderDataSource, not on StructuredDataSource", ErrorSource.User);
			}
			if (update.Password != null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Password can only be updated on ProviderDataSource, not on StructuredDataSource", ErrorSource.User);
			}
			if (update.Isolation != null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Isolation can only be updated on ProviderDataSource, not on StructuredDataSource", ErrorSource.User);
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(dataSource, update.Annotations, (DataSource ds) => ds.Annotations))
		{
			result = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num = dataSource.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceDataSourceProperties(dataSource, update.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				result = true;
			}
		}
		return result;
	}

	public static async Task<string> ExportTMDL(string? connectionName, string dataSourceName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(dataSourceName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("dataSourceName is required", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string text = ExportTMDLInternal(connectionInfo.Database, dataSourceName, options);
				AuditEvent.Default.Emit("export data source to TMDL", success: true, OperationType.Read, connectionInfo);
				result = text;
			}
			catch
			{
				AuditEvent.Default.Emit("export data source to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static string ExportTMDLInternal(Database db, string dataSourceName, ExportTmdl options)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(dataSourceName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("dataSourceName is required", ErrorSource.User);
		}
		return ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(db.Model.DataSources.Find(dataSourceName) ?? throw new ArgumentException("Data source '" + dataSourceName + "' not found"), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
	}

	public static async Task<BatchOperationResponse> CreateDataSources(string? connectionName, List<DataSourceDefinition> dataSources, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Create",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (dataSources == null || !dataSources.Any())
		{
			response.Success = false;
			response.Message = "No data sources provided for creation";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int successCount = 0;
		int failureCount = 0;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < dataSources.Count; i++)
				{
					DataSourceDefinition dataSourceDefinition = dataSources[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = dataSourceDefinition.Name
					};
					try
					{
						OperationResult operationResult = CreateDataSourceInternal(connectionInfo, dataSourceDefinition);
						itemResult.Success = operationResult.Success;
						itemResult.Message = (itemResult.Success ? ("Successfully created data source '" + dataSourceDefinition.Name + "'") : ("Failed to create data source '" + dataSourceDefinition.Name + "': " + operationResult.Message));
						if (itemResult.Success)
						{
							successCount++;
							if (transactionId != null)
							{
								TransactionOperations.RecordOperation(connectionInfo, "Created data source '" + dataSourceDefinition.Name + "'");
							}
						}
						else
						{
							failureCount++;
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error creating data source '" + dataSourceDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, dataSources.Count, ref successCount, ref failureCount, "Created", "data sources");
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = "Create operation failed: " + ex2.Message;
				failureCount = dataSources.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = dataSources.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> UpdateDataSources(string? connectionName, List<DataSourceDefinition> dataSources, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Update",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (dataSources == null || !dataSources.Any())
		{
			response.Success = false;
			response.Message = "No data sources provided for update";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int successCount = 0;
		int failureCount = 0;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < dataSources.Count; i++)
				{
					DataSourceDefinition dataSourceDefinition = dataSources[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = dataSourceDefinition.Name
					};
					try
					{
						OperationResult operationResult = UpdateDataSourceInternal(connectionInfo, dataSourceDefinition);
						itemResult.Success = true;
						itemResult.Message = (operationResult.HasChanges ? ("Successfully updated data source '" + dataSourceDefinition.Name + "'") : ("Data source '" + dataSourceDefinition.Name + "' updated (no changes detected)"));
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Updated data source '" + dataSourceDefinition.Name + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error updating data source '" + dataSourceDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, dataSources.Count, ref successCount, ref failureCount, "Updated", "data sources");
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = "Update operation failed: " + ex2.Message;
				failureCount = dataSources.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = dataSources.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> DeleteDataSources(string? connectionName, List<DataSourceReference> dataSources, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Delete",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (dataSources == null || !dataSources.Any())
		{
			response.Success = false;
			response.Message = "No data sources provided for deletion";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int successCount = 0;
		int failureCount = 0;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < dataSources.Count; i++)
				{
					DataSourceReference dataSourceReference = dataSources[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = dataSourceReference.Name
					};
					try
					{
						DeleteDataSourceInternal(connectionInfo, dataSourceReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully deleted data source '" + dataSourceReference.Name + "'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Deleted data source '" + dataSourceReference.Name + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error deleting data source '" + dataSourceReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, dataSources.Count, ref successCount, ref failureCount, "Deleted", "data sources");
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = "Delete operation failed: " + ex2.Message;
				failureCount = dataSources.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = dataSources.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> GetDataSources(string? connectionName, List<DataSourceReference> dataSources, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (dataSources == null || !dataSources.Any())
		{
			response.Success = false;
			response.Message = "No data sources provided for retrieval";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int successCount = 0;
		int failureCount = 0;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < dataSources.Count; i++)
				{
					DataSourceReference dataSourceReference = dataSources[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = dataSourceReference.Name
					};
					try
					{
						DataSourceGet dataSourceInternal = GetDataSourceInternal(connectionInfo.Database, dataSourceReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved data source '" + dataSourceReference.Name + "'";
						itemResult.Data = dataSourceInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving data source '" + dataSourceReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				response.Success = failureCount == 0;
				response.Message = $"Processed {dataSources.Count} data source(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = dataSources.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get data sources", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = dataSources.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameDataSources(string? connectionName, List<DataSourceRename> dataSources, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Rename",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (dataSources == null || !dataSources.Any())
		{
			response.Success = false;
			response.Message = "No data sources provided for renaming";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int successCount = 0;
		int failureCount = 0;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < dataSources.Count; i++)
				{
					DataSourceRename dataSourceRename = dataSources[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = dataSourceRename.CurrentName
					};
					try
					{
						RenameDataSourceInternal(connectionInfo, dataSourceRename.CurrentName, dataSourceRename.NewName);
						itemResult.Success = true;
						itemResult.Message = $"Successfully renamed data source '{dataSourceRename.CurrentName}' to '{dataSourceRename.NewName}'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, $"Renamed data source '{dataSourceRename.CurrentName}' to '{dataSourceRename.NewName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error renaming data source '" + dataSourceRename.CurrentName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, dataSources.Count, ref successCount, ref failureCount, "Renamed", "data sources");
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = "Rename operation failed: " + ex2.Message;
				failureCount = dataSources.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = dataSources.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}
}
