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

public static class QueryGroupOperations
{
	public static QueryGroup FindOrCreateQueryGroup(Database db, string queryGroupName, out bool wasCreated)
	{
		QueryGroup queryGroup = db.Model.QueryGroups.Find(queryGroupName);
		if (queryGroup != null)
		{
			wasCreated = false;
			return queryGroup;
		}
		QueryGroup queryGroup2 = new QueryGroup
		{
			Folder = queryGroupName
		};
		db.Model.QueryGroups.Add(queryGroup2);
		wasCreated = true;
		return queryGroup2;
	}

	public static void ValidateQueryGroupDefinition(QueryGroupBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("QueryGroup definition cannot be null", ErrorSource.User);
		}
		if (isCreate && string.IsNullOrWhiteSpace(def.Folder))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Folder is required for create operations", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(def.Folder))
		{
			char[] invalidChars = new char[7] { '<', '>', ':', '"', '|', '?', '*' };
			if (def.Folder.Any((char c) => Enumerable.Contains(invalidChars, c)))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Folder contains invalid characters", ErrorSource.User);
			}
		}
		AnnotationHelpers.ValidateAnnotations(def.Annotations);
	}

	public static async Task<List<QueryGroupList>> ListQueryGroups(string? connectionName)
	{
		List<QueryGroupList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<QueryGroupList> list = ListQueryGroupsInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list query groups", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list query groups", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<QueryGroupList> ListQueryGroupsInternal(Database db)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database reference cannot be null", ErrorSource.User);
		}
		return db.Model.QueryGroups.Select((QueryGroup qg) => new QueryGroupList
		{
			Name = qg.Name,
			Description = ((!string.IsNullOrEmpty(qg.Description)) ? qg.Description : null),
			Folder = ((!string.IsNullOrEmpty(qg.Folder)) ? qg.Folder : null)
		}).ToList();
	}

	private static QueryGroupGet GetQueryGroupInternal(Database db, string queryGroupName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(queryGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("queryGroupName is required", ErrorSource.User);
		}
		QueryGroup queryGroup = db.Model.QueryGroups.Find(queryGroupName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("QueryGroup '" + queryGroupName + "' not found", ErrorSource.User);
		QueryGroupGet queryGroupGet = new QueryGroupGet
		{
			Name = queryGroup.Name,
			Description = queryGroup.Description,
			Folder = queryGroup.Folder,
			Annotations = new List<KeyValuePair<string, string>>()
		};
		if (queryGroup.Annotations != null)
		{
			foreach (Annotation annotation in queryGroup.Annotations)
			{
				queryGroupGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
			}
		}
		return queryGroupGet;
	}

	public static async Task<string> ExportTMDL(string? connectionName, string queryGroupName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(queryGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("queryGroupName is required", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string text = ExportTMDLInternal(connectionInfo.Database, queryGroupName, options);
				AuditEvent.Default.Emit("export query group to TMDL", success: true, OperationType.Read, connectionInfo);
				result = text;
			}
			catch
			{
				AuditEvent.Default.Emit("export query group to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static string ExportTMDLInternal(Database db, string queryGroupName, ExportTmdl options)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(queryGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("queryGroupName is required", ErrorSource.User);
		}
		return ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(db.Model.QueryGroups.Find(queryGroupName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("QueryGroup '" + queryGroupName + "' not found", ErrorSource.User), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
	}

	public static async Task<QueryGroupOperationResult> CreateQueryGroup(string? connectionName, QueryGroupDefinition def)
	{
		ValidateQueryGroupDefinition(def, isCreate: true);
		QueryGroupOperationResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = CreateQueryGroupInternal(info, def);
		}
		return result;
	}

	private static QueryGroupOperationResult CreateQueryGroupInternal(IConnectionInfo info, QueryGroupDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection info cannot be null", ErrorSource.User);
		}
		ValidateQueryGroupDefinition(def, isCreate: true);
		Database database = info.Database;
		QueryGroup queryGroup = new QueryGroup
		{
			Folder = def.Folder
		};
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			queryGroup.Description = def.Description;
		}
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				queryGroup.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
		}
		database.Model.QueryGroups.Add(queryGroup);
		TransactionOperations.RecordOperation(info, "Created query group in model " + database.Model.Name);
		ConnectionOperations.SaveChangesWithRollback(info, "create query group", OperationType.Create);
		return CreateQueryGroupOperationResult(queryGroup);
	}

	public static async Task<QueryGroupOperationResult> UpdateQueryGroup(string? connectionName, QueryGroupDefinition update)
	{
		ValidateQueryGroupDefinition(update, isCreate: false);
		QueryGroupOperationResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = UpdateQueryGroupInternal(info, update);
		}
		return result;
	}

	private static QueryGroupOperationResult UpdateQueryGroupInternal(IConnectionInfo info, QueryGroupDefinition update)
	{
		Database database = info.Database;
		QueryGroup queryGroup = database.Model.QueryGroups.Find(update.Name) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("QueryGroup '" + update.Name + "' not found", ErrorSource.User);
		bool flag = false;
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (queryGroup.Description != text)
			{
				queryGroup.Description = text;
				flag = true;
			}
		}
		if (update.Folder != null)
		{
			string text2 = (string.IsNullOrEmpty(update.Folder) ? null : update.Folder);
			if (queryGroup.Folder != text2)
			{
				queryGroup.Folder = text2;
				flag = true;
			}
		}
		if (update.Annotations != null)
		{
			queryGroup.Annotations.Clear();
			foreach (KeyValuePair<string, string> annotation in update.Annotations)
			{
				queryGroup.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
			flag = true;
		}
		if (!flag)
		{
			return CreateQueryGroupOperationResult(queryGroup, hasChanges: false);
		}
		TransactionOperations.RecordOperation(info, "Updated query group '" + update.Name + "' in model " + database.Model.Name);
		ConnectionOperations.SaveChangesWithRollback(info, "update query group", OperationType.Update);
		return CreateQueryGroupOperationResult(queryGroup);
	}

	public static async Task DeleteQueryGroup(string? connectionName, string queryGroupName)
	{
		if (string.IsNullOrWhiteSpace(queryGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("queryGroupName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		DeleteQueryGroupInternal(info, queryGroupName);
	}

	private static void DeleteQueryGroupInternal(IConnectionInfo info, string queryGroupName)
	{
		Database database = info.Database;
		QueryGroup queryGroup = database.Model.QueryGroups.Find(queryGroupName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("QueryGroup '" + queryGroupName + "' not found", ErrorSource.User);
		List<string> list = CheckQueryGroupDependencies(database, queryGroup);
		if (list.Count > 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete query group '" + queryGroupName + "' because it has dependencies: " + string.Join(", ", list), ErrorSource.User);
		}
		database.Model.QueryGroups.Remove(queryGroup);
		TransactionOperations.RecordOperation(info, "Deleted query group '" + queryGroupName + "' from model " + database.Model.Name);
		ConnectionOperations.SaveChangesWithRollback(info, "delete query group", OperationType.Delete);
	}

	private static List<string> CheckQueryGroupDependencies(Database db, QueryGroup queryGroup)
	{
		List<string> list = new List<string>();
		foreach (Table table in db.Model.Tables)
		{
			foreach (Partition partition in table.Partitions)
			{
				if (partition.QueryGroup == queryGroup)
				{
					list.Add("Partition: " + table.Name + "." + partition.Name);
				}
			}
		}
		foreach (NamedExpression expression in db.Model.Expressions)
		{
			if (expression.QueryGroup == queryGroup)
			{
				list.Add("NamedExpression: " + expression.Name);
			}
		}
		return list;
	}

	private static QueryGroupOperationResult CreateQueryGroupOperationResult(QueryGroup queryGroup, bool hasChanges = true)
	{
		return new QueryGroupOperationResult
		{
			QueryGroupName = queryGroup.Name,
			HasChanges = hasChanges
		};
	}

	public static async Task<BatchOperationResponse> CreateQueryGroups(string? connectionName, List<QueryGroupDefinition> queryGroups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Create",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (queryGroups == null || !queryGroups.Any())
		{
			response.Success = false;
			response.Message = "No query groups provided for creation";
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
				for (int i = 0; i < queryGroups.Count; i++)
				{
					QueryGroupDefinition queryGroupDefinition = queryGroups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = queryGroupDefinition.Folder
					};
					try
					{
						QueryGroupOperationResult queryGroupOperationResult = CreateQueryGroupInternal(connectionInfo, queryGroupDefinition);
						itemResult.Success = true;
						itemResult.Message = "Successfully created query group '" + queryGroupOperationResult.QueryGroupName + "'";
						itemResult.Data = queryGroupOperationResult;
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Created query group '" + queryGroupOperationResult.QueryGroupName + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error creating query group with folder '" + queryGroupDefinition.Folder + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, queryGroups.Count, ref successCount, ref failureCount, "Created", "query groups");
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
						warnings.Add("Transaction rolled back due to exception.");
					}
					catch (Exception ex3)
					{
						warnings.Add("Failed to rollback transaction: " + ex3.Message);
					}
				}
				response.Success = false;
				response.Message = "Create operation failed: " + ex2.Message;
				failureCount = queryGroups.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = queryGroups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> UpdateQueryGroups(string? connectionName, List<QueryGroupDefinition> queryGroups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Update",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (queryGroups == null || !queryGroups.Any())
		{
			response.Success = false;
			response.Message = "No query groups provided for update";
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
				for (int i = 0; i < queryGroups.Count; i++)
				{
					QueryGroupDefinition queryGroupDefinition = queryGroups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = queryGroupDefinition.Name
					};
					try
					{
						QueryGroupOperationResult queryGroupOperationResult = UpdateQueryGroupInternal(connectionInfo, queryGroupDefinition);
						itemResult.Success = true;
						itemResult.Message = (queryGroupOperationResult.HasChanges ? ("Successfully updated query group '" + queryGroupDefinition.Name + "'") : ("Query group '" + queryGroupDefinition.Name + "' updated (no changes detected)"));
						itemResult.Data = queryGroupOperationResult;
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Updated query group '" + queryGroupDefinition.Name + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error updating query group '" + queryGroupDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, queryGroups.Count, ref successCount, ref failureCount, "Updated", "query groups");
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
						warnings.Add("Transaction rolled back due to exception.");
					}
					catch (Exception ex3)
					{
						warnings.Add("Failed to rollback transaction: " + ex3.Message);
					}
				}
				response.Success = false;
				response.Message = "Update operation failed: " + ex2.Message;
				failureCount = queryGroups.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = queryGroups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> DeleteQueryGroups(string? connectionName, List<QueryGroupReference> queryGroups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Delete",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (queryGroups == null || !queryGroups.Any())
		{
			response.Success = false;
			response.Message = "No query groups provided for deletion";
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
				for (int i = 0; i < queryGroups.Count; i++)
				{
					QueryGroupReference queryGroupReference = queryGroups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = queryGroupReference.Name
					};
					try
					{
						DeleteQueryGroupInternal(connectionInfo, queryGroupReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully deleted query group '" + queryGroupReference.Name + "'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Deleted query group '" + queryGroupReference.Name + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error deleting query group '" + queryGroupReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, queryGroups.Count, ref successCount, ref failureCount, "Deleted", "query groups");
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
						warnings.Add("Transaction rolled back due to exception.");
					}
					catch (Exception ex3)
					{
						warnings.Add("Failed to rollback transaction: " + ex3.Message);
					}
				}
				response.Success = false;
				response.Message = "Delete operation failed: " + ex2.Message;
				failureCount = queryGroups.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = queryGroups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> GetQueryGroups(string? connectionName, List<QueryGroupReference> queryGroups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (queryGroups == null || !queryGroups.Any())
		{
			response.Success = false;
			response.Message = "No query groups provided for retrieval";
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
				for (int i = 0; i < queryGroups.Count; i++)
				{
					QueryGroupReference queryGroupReference = queryGroups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = queryGroupReference.Name
					};
					try
					{
						QueryGroupGet queryGroupInternal = GetQueryGroupInternal(connectionInfo.Database, queryGroupReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved query group '" + queryGroupReference.Name + "'";
						itemResult.Data = queryGroupInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving query group '" + queryGroupReference.Name + "': " + ex.Message;
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
				response.Message = $"Processed {queryGroups.Count} query group(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = queryGroups.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get query groups", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = queryGroups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}
}
