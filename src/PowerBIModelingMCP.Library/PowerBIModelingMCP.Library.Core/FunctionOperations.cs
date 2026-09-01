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

public static class FunctionOperations
{
	public class FunctionOperationResult
	{
		public string State { get; set; } = "Ready";

		public string? ErrorMessage { get; set; }

		public string FunctionName { get; set; } = string.Empty;

		public bool HasChanges { get; set; }

		public List<string> Warnings { get; set; } = new List<string>();
	}

	internal static PostCommitDaxValidator.Target? ResolveFunctionForValidation(IConnectionInfo conn, FunctionDefinition def)
	{
		if (def == null || string.IsNullOrEmpty(def.Name))
		{
			return null;
		}
		Database database = conn?.Database;
		if (database == null)
		{
			return null;
		}
		Function function = database.Model.Functions.Find(def.Name);
		if (function == null)
		{
			return null;
		}
		List<PostCommitDaxValidator.Check> checks = new List<PostCommitDaxValidator.Check>
		{
			new PostCommitDaxValidator.Check(string.Empty, function.State.ToString(), function.ErrorMessage)
		};
		return new PostCommitDaxValidator.Target("Function", "'" + function.Name + "'", checks);
	}

	public static void ValidateFunctionDefinition(FunctionBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Function definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (isCreate && string.IsNullOrWhiteSpace(def.Expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression is required for creating functions", ErrorSource.User);
		}
		if (def.ExtendedProperties != null)
		{
			List<string> list = ExtendedPropertyHelpers.Validate(def.ExtendedProperties);
			if (list.Count > 0)
			{
				throw new McpExceptionWithSource("ExtendedProperties validation failed: " + string.Join(", ", list), ErrorSource.User, "ExtendedProperties validation failed.");
			}
		}
		AnnotationHelpers.ValidateAnnotations(def.Annotations);
	}

	public static Function FindFunction(Model model, string functionName)
	{
		return model.Functions.Find(functionName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Function '" + functionName + "' not found in model", ErrorSource.User);
	}

	public static async Task<List<FunctionList>> ListFunctions(string? connectionName = null)
	{
		List<FunctionList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<FunctionList> list = ListFunctionsInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list functions", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list functions", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<FunctionList> ListFunctionsInternal(Database db)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null.", ErrorSource.User);
		}
		return db.Model.Functions.Select((Function f) => new FunctionList
		{
			Name = f.Name,
			Description = ((!string.IsNullOrEmpty(f.Description)) ? f.Description : null)
		}).ToList();
	}

	private static FunctionGet GetFunctionInternal(Database db, string functionName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null.", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(functionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("functionName is required", ErrorSource.User);
		}
		Function function = FindFunction(db.Model, functionName);
		FunctionGet functionGet = new FunctionGet
		{
			Name = function.Name,
			Expression = (function.Expression ?? string.Empty),
			Description = function.Description,
			IsHidden = function.IsHidden,
			LineageTag = function.LineageTag,
			SourceLineageTag = function.SourceLineageTag,
			ModifiedTime = function.ModifiedTime,
			StructureModifiedTime = function.StructureModifiedTime,
			State = function.State.ToString(),
			ErrorMessage = function.ErrorMessage,
			Annotations = new List<KeyValuePair<string, string>>(),
			ExtendedProperties = new List<PowerBIModelingMCP.Library.Common.DataStructures.ExtendedProperty>()
		};
		foreach (Annotation annotation in function.Annotations)
		{
			functionGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value ?? string.Empty));
		}
		functionGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromFunction(function);
		return functionGet;
	}

	private static FunctionOperationResult CreateFunctionInternal(IConnectionInfo info, FunctionDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection info cannot be null", ErrorSource.User);
		}
		ValidateFunctionDefinition(def, isCreate: true);
		Model model = info.Database.Model;
		if (model.Functions.Find(def.Name) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Function '" + def.Name + "' already exists in the model", ErrorSource.User);
		}
		Function function = new Function
		{
			Name = def.Name,
			Expression = def.Expression
		};
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			function.Description = def.Description;
		}
		if (def.IsHidden.HasValue)
		{
			function.IsHidden = def.IsHidden.Value;
		}
		function.LineageTag = (string.IsNullOrWhiteSpace(def.LineageTag) ? Guid.NewGuid().ToString() : def.LineageTag);
		if (!string.IsNullOrWhiteSpace(def.SourceLineageTag))
		{
			function.SourceLineageTag = def.SourceLineageTag;
		}
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				function.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToFunction(function, def.ExtendedProperties);
		}
		model.Functions.Add(function);
		TransactionOperations.RecordOperation(info, "Created function '" + def.Name + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "create function", OperationType.Create);
		return new FunctionOperationResult
		{
			State = function.State.ToString(),
			ErrorMessage = function.ErrorMessage,
			FunctionName = function.Name
		};
	}

	private static FunctionOperationResult UpdateFunctionInternal(IConnectionInfo info, FunctionDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection info cannot be null", ErrorSource.User);
		}
		ValidateFunctionDefinition(update, isCreate: false);
		Function function = FindFunction(info.Database.Model, update.Name);
		bool hasChanges = false;
		if (!string.IsNullOrWhiteSpace(update.Expression) && function.Expression != update.Expression)
		{
			function.Expression = update.Expression;
			hasChanges = true;
		}
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (function.Description != text)
			{
				function.Description = text;
				hasChanges = true;
			}
		}
		if (update.IsHidden.HasValue && function.IsHidden != update.IsHidden.Value)
		{
			function.IsHidden = update.IsHidden.Value;
			hasChanges = true;
		}
		if (update.LineageTag != null)
		{
			string text2 = (string.IsNullOrEmpty(update.LineageTag) ? null : update.LineageTag);
			if (function.LineageTag != text2)
			{
				function.LineageTag = text2;
				hasChanges = true;
			}
		}
		if (update.SourceLineageTag != null)
		{
			string text3 = (string.IsNullOrEmpty(update.SourceLineageTag) ? null : update.SourceLineageTag);
			if (function.SourceLineageTag != text3)
			{
				function.SourceLineageTag = text3;
				hasChanges = true;
			}
		}
		if (update.Annotations != null)
		{
			function.Annotations.Clear();
			foreach (KeyValuePair<string, string> annotation in update.Annotations)
			{
				function.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
			hasChanges = true;
		}
		if (update.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ReplaceFunctionProperties(function, update.ExtendedProperties);
			hasChanges = true;
		}
		TransactionOperations.RecordOperation(info, "Updated function '" + update.Name + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "update function", OperationType.Update);
		return new FunctionOperationResult
		{
			State = function.State.ToString(),
			ErrorMessage = function.ErrorMessage,
			FunctionName = function.Name,
			HasChanges = hasChanges
		};
	}

	private static void RenameFunctionInternal(IConnectionInfo info, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		Model model = info.Database.Model;
		Function function = FindFunction(model, oldName);
		if (model.Functions.Find(newName) != null && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Function '" + newName + "' already exists in the model", ErrorSource.User);
		}
		function.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed function from '{oldName}' to '{newName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename function", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	private static void DeleteFunctionInternal(IConnectionInfo info, string functionName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(functionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("functionName is required", ErrorSource.User);
		}
		Model model = info.Database.Model;
		Function metadataObject = FindFunction(model, functionName);
		model.Functions.Remove(metadataObject);
		TransactionOperations.RecordOperation(info, "Deleted function '" + functionName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete function", OperationType.Delete);
	}

	public static async Task<string> ExportTMDL(string? connectionName, string functionName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(functionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("functionName is required", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string text = ExportTMDLInternal(connectionInfo.Database, functionName, options);
				AuditEvent.Default.Emit("export function to TMDL", success: true, OperationType.Read, connectionInfo);
				result = text;
			}
			catch
			{
				AuditEvent.Default.Emit("export function to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static string ExportTMDLInternal(Database db, string functionName, ExportTmdl options)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(functionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("functionName is required", ErrorSource.User);
		}
		return ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(db.Model.Functions.Find(functionName) ?? throw new ArgumentException("Function '" + functionName + "' not found"), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
	}

	public static async Task<BatchOperationResponse> CreateFunctions(string? connectionName, List<FunctionDefinition> definitions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Create",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (definitions == null || !definitions.Any())
		{
			response.Success = false;
			response.Message = "No functions provided for creation";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = definitions.Count;
		int successCount = 0;
		int failureCount = 0;
		IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName);
		BatchOperationResponse result;
		try
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(conn, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < definitions.Count; i++)
				{
					FunctionDefinition functionDefinition = definitions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = (functionDefinition.Name ?? "Unknown")
					};
					try
					{
						FunctionOperationResult functionOperationResult = CreateFunctionInternal(conn, functionDefinition);
						List<string> warnings2 = functionOperationResult.Warnings;
						if (warnings2 != null && warnings2.Count > 0)
						{
							warnings.AddRange(functionOperationResult.Warnings);
						}
						string[] source = new string[3] { "Ready", "NoData", "CalculationNeeded" };
						itemResult.Success = Enumerable.Contains(source, functionOperationResult.State);
						itemResult.Message = (itemResult.Success ? ("Function '" + functionDefinition.Name + "' created successfully") : (functionOperationResult.ErrorMessage ?? ("Failed to create function '" + functionDefinition.Name + "'")));
						itemResult.Data = functionOperationResult;
						if (itemResult.Success)
						{
							successCount++;
						}
						else
						{
							failureCount++;
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error creating function '" + functionDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(conn, response, transactionId, ownsTransaction, totalItems, ref successCount, ref failureCount, "Created", "functions");
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = (response.Success ? $"Successfully created {successCount} functions" : $"Batch operation completed with {failureCount} failures out of {totalItems} items");
				}
				PostCommitDaxValidator.Append(conn, warnings, response.Results, definitions, transactionId, ownsTransaction, transactionFailed, failureCount, "created", (FunctionDefinition def) => ResolveFunctionForValidation(conn, def));
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(conn);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = "Batch create operation failed: " + ex2.Message;
				failureCount = totalItems;
				successCount = 0;
			}
			stopwatch.Stop();
			response.Summary = new BatchSummary
			{
				TotalItems = totalItems,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		finally
		{
			if (conn != null)
			{
				await conn.DisposeAsync();
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> UpdateFunctions(string? connectionName, List<FunctionDefinition> definitions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Update",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (definitions == null || !definitions.Any())
		{
			response.Success = false;
			response.Message = "No functions provided for update";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = definitions.Count;
		int successCount = 0;
		int failureCount = 0;
		IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName);
		BatchOperationResponse result;
		try
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(conn, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < definitions.Count; i++)
				{
					FunctionDefinition functionDefinition = definitions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = (functionDefinition.Name ?? "Unknown")
					};
					try
					{
						FunctionOperationResult functionOperationResult = UpdateFunctionInternal(conn, functionDefinition);
						List<string> warnings2 = functionOperationResult.Warnings;
						if (warnings2 != null && warnings2.Count > 0)
						{
							warnings.AddRange(functionOperationResult.Warnings);
						}
						string[] source = new string[3] { "Ready", "NoData", "CalculationNeeded" };
						itemResult.Success = Enumerable.Contains(source, functionOperationResult.State);
						itemResult.Data = functionOperationResult;
						if (itemResult.Success)
						{
							itemResult.Message = (functionOperationResult.HasChanges ? ("Function '" + functionDefinition.Name + "' updated successfully") : ("Function '" + functionDefinition.Name + "' updated (no changes detected)"));
							successCount++;
						}
						else
						{
							itemResult.Message = functionOperationResult.ErrorMessage ?? ("Failed to update function '" + functionDefinition.Name + "'");
							failureCount++;
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error updating function '" + functionDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(conn, response, transactionId, ownsTransaction, totalItems, ref successCount, ref failureCount, "Updated", "functions");
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = (response.Success ? $"Successfully updated {successCount} functions" : $"Batch operation completed with {failureCount} failures out of {totalItems} items");
				}
				PostCommitDaxValidator.Append(conn, warnings, response.Results, definitions, transactionId, ownsTransaction, transactionFailed, failureCount, "updated", (FunctionDefinition def) => ResolveFunctionForValidation(conn, def));
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(conn);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = "Batch update operation failed: " + ex2.Message;
				failureCount = totalItems - successCount;
			}
			stopwatch.Stop();
			response.Summary = new BatchSummary
			{
				TotalItems = totalItems,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		finally
		{
			if (conn != null)
			{
				await conn.DisposeAsync();
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> DeleteFunctions(string? connectionName, List<FunctionReference> definitions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Delete",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (definitions == null || !definitions.Any())
		{
			response.Success = false;
			response.Message = "No function names provided for deletion";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = definitions.Count;
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
				for (int i = 0; i < definitions.Count; i++)
				{
					FunctionReference functionReference = definitions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = functionReference.Name
					};
					try
					{
						DeleteFunctionInternal(connectionInfo, functionReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Function '" + functionReference.Name + "' deleted successfully";
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error deleting function '" + functionReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, totalItems, ref successCount, ref failureCount, "Deleted", "functions");
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = (response.Success ? $"Successfully deleted {successCount} functions" : $"Batch operation completed with {failureCount} failures out of {totalItems} items");
				}
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
				response.Message = "Batch delete operation failed: " + ex2.Message;
				failureCount = totalItems - successCount;
			}
			stopwatch.Stop();
			response.Summary = new BatchSummary
			{
				TotalItems = totalItems,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> GetFunctions(string? connectionName, List<FunctionReference> identifiers, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>()
		};
		if (identifiers == null || !identifiers.Any())
		{
			response.Success = false;
			response.Message = "No function names provided for retrieval";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = identifiers.Count;
		int successCount = 0;
		int failureCount = 0;
		bool transactionFailed = false;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < identifiers.Count; i++)
				{
					FunctionReference functionReference = identifiers[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = functionReference.Name
					};
					try
					{
						FunctionGet functionInternal = GetFunctionInternal(connectionInfo.Database, functionReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Function '" + functionReference.Name + "' retrieved successfully";
						itemResult.Data = functionInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving function '" + functionReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				response.Success = failureCount == 0 && !transactionFailed;
				response.Message = (response.Success ? $"Successfully retrieved {successCount} functions" : $"Batch operation completed with {failureCount} failures out of {totalItems} items");
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Batch get operation failed: " + ex2.Message;
				failureCount = totalItems;
				successCount = 0;
			}
			stopwatch.Stop();
			AuditEvent.Default.Emit("get functions", response.Success, OperationType.Read, connectionInfo);
			response.Summary = new BatchSummary
			{
				TotalItems = totalItems,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameFunctions(string? connectionName, List<FunctionRename> definitions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Rename",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (definitions == null || !definitions.Any())
		{
			response.Success = false;
			response.Message = "No function rename definitions provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = definitions.Count;
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
				for (int i = 0; i < definitions.Count; i++)
				{
					FunctionRename functionRename = definitions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = functionRename.CurrentName + " -> " + functionRename.NewName
					};
					try
					{
						RenameFunctionInternal(connectionInfo, functionRename.CurrentName, functionRename.NewName);
						itemResult.Success = true;
						itemResult.Message = $"Function renamed from '{functionRename.CurrentName}' to '{functionRename.NewName}' successfully";
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = $"Error renaming function from '{functionRename.CurrentName}' to '{functionRename.NewName}': {ex.Message}";
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, totalItems, ref successCount, ref failureCount, "Renamed", "functions");
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = (response.Success ? $"Successfully renamed {successCount} functions" : $"Batch operation completed with {failureCount} failures out of {totalItems} items");
				}
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
				response.Message = "Batch rename operation failed: " + ex2.Message;
				failureCount = totalItems - successCount;
			}
			stopwatch.Stop();
			response.Summary = new BatchSummary
			{
				TotalItems = totalItems,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}
}
