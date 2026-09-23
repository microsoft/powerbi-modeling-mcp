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

public static class PerspectiveOperations
{
	public static async Task<List<PerspectiveList>> ListPerspectives(string? connectionName)
	{
		List<PerspectiveList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<PerspectiveList> list = ListPerspectivesInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list perspectives", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list perspectives", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<PerspectiveList> ListPerspectivesInternal(Database db)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		List<PerspectiveList> list = new List<PerspectiveList>();
		foreach (Perspective perspective in db.Model.Perspectives)
		{
			list.Add(new PerspectiveList
			{
				Name = perspective.Name,
				Description = ((!string.IsNullOrEmpty(perspective.Description)) ? perspective.Description : null),
				TableCount = ((perspective.PerspectiveTables.Count > 0) ? new int?(perspective.PerspectiveTables.Count) : ((int?)null)),
				MeasureCount = ((perspective.PerspectiveTables.Sum((PerspectiveTable pt) => pt.PerspectiveMeasures.Count) > 0) ? new int?(perspective.PerspectiveTables.Sum((PerspectiveTable pt) => pt.PerspectiveMeasures.Count)) : ((int?)null)),
				ColumnCount = ((perspective.PerspectiveTables.Sum((PerspectiveTable pt) => pt.PerspectiveColumns.Count) > 0) ? new int?(perspective.PerspectiveTables.Sum((PerspectiveTable pt) => pt.PerspectiveColumns.Count)) : ((int?)null)),
				HierarchyCount = ((perspective.PerspectiveTables.Sum((PerspectiveTable pt) => pt.PerspectiveHierarchies.Count) > 0) ? new int?(perspective.PerspectiveTables.Sum((PerspectiveTable pt) => pt.PerspectiveHierarchies.Count)) : ((int?)null))
			});
		}
		return list;
	}

	private static PerspectiveGet GetPerspectiveInternal(Database db, string perspectiveName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		Perspective perspective = db.Model.Perspectives.Find(perspectiveName);
		if (perspective == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User);
		}
		PerspectiveGet perspectiveGet = new PerspectiveGet
		{
			Name = perspective.Name,
			Description = perspective.Description,
			ModifiedTime = perspective.ModifiedTime,
			Annotations = new List<KeyValuePair<string, string>>()
		};
		foreach (Annotation annotation in perspective.Annotations)
		{
			perspectiveGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		foreach (PerspectiveTable perspectiveTable in perspective.PerspectiveTables)
		{
			PerspectiveTableGet perspectiveTableGet = new PerspectiveTableGet
			{
				Name = perspectiveTable.Name,
				TableName = perspectiveTable.Table?.Name,
				Annotations = new List<KeyValuePair<string, string>>()
			};
			foreach (Annotation annotation2 in perspectiveTable.Annotations)
			{
				perspectiveTableGet.Annotations.Add(new KeyValuePair<string, string>(annotation2.Name, annotation2.Value));
			}
			foreach (PerspectiveColumn perspectiveColumn in perspectiveTable.PerspectiveColumns)
			{
				PerspectiveColumnGet perspectiveColumnGet = new PerspectiveColumnGet
				{
					Name = perspectiveColumn.Name,
					ColumnName = perspectiveColumn.Column?.Name,
					TableName = perspectiveTable.Table?.Name,
					Annotations = new List<KeyValuePair<string, string>>()
				};
				foreach (Annotation annotation3 in perspectiveColumn.Annotations)
				{
					perspectiveColumnGet.Annotations.Add(new KeyValuePair<string, string>(annotation3.Name, annotation3.Value));
				}
				perspectiveTableGet.PerspectiveColumns.Add(perspectiveColumnGet);
				perspectiveGet.Columns.Add(perspectiveColumn.Name);
			}
			foreach (PerspectiveMeasure perspectiveMeasure in perspectiveTable.PerspectiveMeasures)
			{
				PerspectiveMeasureGet perspectiveMeasureGet = new PerspectiveMeasureGet
				{
					Name = perspectiveMeasure.Name,
					MeasureName = perspectiveMeasure.Measure?.Name,
					TableName = perspectiveTable.Table?.Name,
					Annotations = new List<KeyValuePair<string, string>>()
				};
				foreach (Annotation annotation4 in perspectiveMeasure.Annotations)
				{
					perspectiveMeasureGet.Annotations.Add(new KeyValuePair<string, string>(annotation4.Name, annotation4.Value));
				}
				perspectiveTableGet.PerspectiveMeasures.Add(perspectiveMeasureGet);
				perspectiveGet.Measures.Add(perspectiveMeasure.Name);
			}
			foreach (PerspectiveHierarchy perspectiveHierarchy in perspectiveTable.PerspectiveHierarchies)
			{
				PerspectiveHierarchyGet perspectiveHierarchyGet = new PerspectiveHierarchyGet
				{
					Name = perspectiveHierarchy.Name,
					HierarchyName = perspectiveHierarchy.Hierarchy?.Name,
					TableName = perspectiveTable.Table?.Name,
					Annotations = new List<KeyValuePair<string, string>>()
				};
				foreach (Annotation annotation5 in perspectiveHierarchy.Annotations)
				{
					perspectiveHierarchyGet.Annotations.Add(new KeyValuePair<string, string>(annotation5.Name, annotation5.Value));
				}
				perspectiveTableGet.PerspectiveHierarchies.Add(perspectiveHierarchyGet);
				perspectiveGet.Hierarchies.Add(perspectiveHierarchy.Name);
			}
			perspectiveGet.PerspectiveTables.Add(perspectiveTableGet);
			perspectiveGet.Tables.Add(perspectiveTable.Name);
		}
		return perspectiveGet;
	}

	private static PerspectiveOperationResult CreatePerspectiveInternal(IConnectionInfo info, PerspectiveDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		Database database = info.Database;
		if (database.Model.Perspectives.Find(def.Name) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + def.Name + "' already exists", ErrorSource.User);
		}
		Perspective perspective = new Perspective
		{
			Name = def.Name,
			Description = def.Description
		};
		if (def.Annotations != null)
		{
			AnnotationHelpers.ApplyAnnotations(perspective, def.Annotations, (Perspective p) => p.Annotations);
		}
		database.Model.Perspectives.Add(perspective);
		TransactionOperations.RecordOperation(info, "Created perspective '" + def.Name + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "create perspective", OperationType.Create);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = def.Name,
			Message = "Perspective '" + def.Name + "' created successfully"
		};
	}

	private static PerspectiveOperationResult UpdatePerspectiveInternal(IConnectionInfo info, string perspectiveName, PerspectiveDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective definition cannot be null", ErrorSource.User);
		}
		Perspective perspective = info.Database.Model.Perspectives.Find(perspectiveName);
		if (perspective == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User);
		}
		bool flag = false;
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (perspective.Description != text)
			{
				perspective.Description = text;
				flag = true;
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(perspective, update.Annotations, (Perspective p) => p.Annotations))
		{
			flag = true;
		}
		if (!flag)
		{
			return new PerspectiveOperationResult
			{
				Success = true,
				PerspectiveName = perspectiveName,
				Message = "Perspective '" + perspectiveName + "' is already in the requested state",
				HasChanges = false
			};
		}
		TransactionOperations.RecordOperation(info, "Updated perspective '" + perspectiveName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "update perspective", OperationType.Update);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = "Perspective '" + perspectiveName + "' updated successfully",
			HasChanges = true
		};
	}

	private static PerspectiveOperationResult DeletePerspectiveInternal(IConnectionInfo info, string perspectiveName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		Database database = info.Database;
		Perspective perspective = database.Model.Perspectives.Find(perspectiveName);
		if (perspective == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User);
		}
		database.Model.Perspectives.Remove(perspective);
		TransactionOperations.RecordOperation(info, "Deleted perspective '" + perspectiveName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete perspective", OperationType.Delete);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = "Perspective '" + perspectiveName + "' deleted successfully"
		};
	}

	private static PerspectiveOperationResult RenamePerspectiveInternal(IConnectionInfo info, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Old perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("New perspective name is required", ErrorSource.User);
		}
		if (oldName == newName)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("New name must be different from current name", ErrorSource.User);
		}
		Database database = info.Database;
		Perspective perspective = database.Model.Perspectives.Find(oldName);
		if (perspective == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + oldName + "' not found", ErrorSource.User);
		}
		if (database.Model.Perspectives.Find(newName) != null && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + newName + "' already exists", ErrorSource.User);
		}
		perspective.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed perspective '{oldName}' to '{newName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename perspective", OperationType.Update, CheckpointMode.AfterRequestRename);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = newName,
			Message = $"Perspective renamed from '{oldName}' to '{newName}' successfully"
		};
	}

	public static async Task<BatchOperationResponse> CreatePerspectives(string? connectionName, List<PerspectiveDefinition> perspectives, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, perspectives, options, "Create", "Created", "perspectives", (PerspectiveDefinition item) => item.Name, delegate(BatchItemContext<PerspectiveDefinition> ctx)
		{
			CreatePerspectiveInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully created perspective '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Created perspective '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> UpdatePerspectives(string? connectionName, List<PerspectiveDefinition> perspectives, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, perspectives, options, "Update", "Updated", "perspectives", (PerspectiveDefinition item) => item.Name, delegate(BatchItemContext<PerspectiveDefinition> ctx)
		{
			UpdatePerspectiveInternal(ctx.Connection, ctx.Item.Name, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully updated perspective '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Updated perspective '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> DeletePerspectives(string? connectionName, List<PerspectiveReference> references, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, references, options, "Delete", "Deleted", "perspectives", (PerspectiveReference item) => item.Name, delegate(BatchItemContext<PerspectiveReference> ctx)
		{
			DeletePerspectiveInternal(ctx.Connection, ctx.Item.Name);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully deleted perspective '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Deleted perspective '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetPerspectives(string? connectionName, List<PerspectiveReference> references, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (references == null || !references.Any())
		{
			response.Success = false;
			response.Message = "No perspective references provided";
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
			Database database = connectionInfo.Database;
			try
			{
				for (int i = 0; i < references.Count; i++)
				{
					PerspectiveReference perspectiveReference = references[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveReference.Name
					};
					try
					{
						PerspectiveGet perspectiveInternal = GetPerspectiveInternal(database, perspectiveReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved perspective '" + perspectiveReference.Name + "'";
						itemResult.Data = perspectiveInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving perspective '" + perspectiveReference.Name + "': " + ex.Message;
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
				response.Message = $"Processed {references.Count} perspective(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = references.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get perspectives", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = references.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenamePerspectives(string? connectionName, List<PerspectiveRename> renames, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, renames, options, "Rename", "Renamed", "perspectives", (PerspectiveRename item) => item.CurrentName + " -> " + item.NewName, delegate(BatchItemContext<PerspectiveRename> ctx)
		{
			RenamePerspectiveInternal(ctx.Connection, ctx.Item.CurrentName, ctx.Item.NewName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed perspective from '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed perspective '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'");
			}
		});
	}

	public static async Task<List<Dictionary<string, string>>> ListPerspectiveTables(string? connectionName, string perspectiveName)
	{
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		List<Dictionary<string, string>> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				Perspective obj = connectionInfo.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User);
				List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
				foreach (PerspectiveTable perspectiveTable in obj.PerspectiveTables)
				{
					list.Add(new Dictionary<string, string>
					{
						["Name"] = perspectiveTable.Name,
						["TableName"] = perspectiveTable.Table?.Name ?? "",
						["ModifiedTime"] = perspectiveTable.ModifiedTime.ToString("yyyy-MM-dd HH:mm:ss"),
						["IncludeAll"] = perspectiveTable.IncludeAll.ToString(),
						["ColumnCount"] = perspectiveTable.PerspectiveColumns.Count.ToString(),
						["MeasureCount"] = perspectiveTable.PerspectiveMeasures.Count.ToString(),
						["HierarchyCount"] = perspectiveTable.PerspectiveHierarchies.Count.ToString()
					});
				}
				AuditEvent.Default.Emit("list perspective tables", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list perspective tables", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static PerspectiveTableGet GetPerspectiveTableInternal(IConnectionInfo info, string perspectiveName, string tableName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		PerspectiveTable perspectiveTable = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName);
		if (perspectiveTable == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		}
		PerspectiveTableGet perspectiveTableGet = new PerspectiveTableGet
		{
			Name = perspectiveTable.Name,
			TableName = perspectiveTable.Table?.Name,
			ModifiedTime = perspectiveTable.ModifiedTime,
			IncludeAll = perspectiveTable.IncludeAll,
			Annotations = new List<KeyValuePair<string, string>>()
		};
		foreach (Annotation annotation in perspectiveTable.Annotations)
		{
			perspectiveTableGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		foreach (PerspectiveColumn perspectiveColumn in perspectiveTable.PerspectiveColumns)
		{
			PerspectiveColumnGet perspectiveColumnGet = new PerspectiveColumnGet
			{
				Name = perspectiveColumn.Name,
				ColumnName = perspectiveColumn.Column?.Name,
				TableName = perspectiveTable.Table?.Name,
				Annotations = new List<KeyValuePair<string, string>>()
			};
			foreach (Annotation annotation2 in perspectiveColumn.Annotations)
			{
				perspectiveColumnGet.Annotations.Add(new KeyValuePair<string, string>(annotation2.Name, annotation2.Value));
			}
			perspectiveTableGet.PerspectiveColumns.Add(perspectiveColumnGet);
		}
		foreach (PerspectiveMeasure perspectiveMeasure in perspectiveTable.PerspectiveMeasures)
		{
			PerspectiveMeasureGet perspectiveMeasureGet = new PerspectiveMeasureGet
			{
				Name = perspectiveMeasure.Name,
				MeasureName = perspectiveMeasure.Measure?.Name,
				TableName = perspectiveTable.Table?.Name,
				Annotations = new List<KeyValuePair<string, string>>()
			};
			foreach (Annotation annotation3 in perspectiveMeasure.Annotations)
			{
				perspectiveMeasureGet.Annotations.Add(new KeyValuePair<string, string>(annotation3.Name, annotation3.Value));
			}
			perspectiveTableGet.PerspectiveMeasures.Add(perspectiveMeasureGet);
		}
		foreach (PerspectiveHierarchy perspectiveHierarchy in perspectiveTable.PerspectiveHierarchies)
		{
			PerspectiveHierarchyGet perspectiveHierarchyGet = new PerspectiveHierarchyGet
			{
				Name = perspectiveHierarchy.Name,
				HierarchyName = perspectiveHierarchy.Hierarchy?.Name,
				TableName = perspectiveTable.Table?.Name,
				Annotations = new List<KeyValuePair<string, string>>()
			};
			foreach (Annotation annotation4 in perspectiveHierarchy.Annotations)
			{
				perspectiveHierarchyGet.Annotations.Add(new KeyValuePair<string, string>(annotation4.Name, annotation4.Value));
			}
			perspectiveTableGet.PerspectiveHierarchies.Add(perspectiveHierarchyGet);
		}
		return perspectiveTableGet;
	}

	internal static PerspectiveOperationResult AddTableToPerspectiveInternal(IConnectionInfo info, string perspectiveName, PerspectiveTableDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective table definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		Database database = info.Database;
		Perspective perspective = database.Model.Perspectives.Find(perspectiveName);
		if (perspective == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User);
		}
		Table table = database.Model.Tables.Find(def.TableName);
		if (table == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + def.TableName + "' not found in model", ErrorSource.User);
		}
		if (perspective.PerspectiveTables.Find(def.TableName) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{def.TableName}' already exists in perspective '{perspectiveName}'", ErrorSource.User);
		}
		PerspectiveTable perspectiveTable = new PerspectiveTable
		{
			Table = table,
			IncludeAll = (def.IncludeAll == true)
		};
		if (def.Annotations != null)
		{
			AnnotationHelpers.ApplyAnnotations(perspectiveTable, def.Annotations, (PerspectiveTable pt) => pt.Annotations);
		}
		perspective.PerspectiveTables.Add(perspectiveTable);
		TransactionOperations.RecordOperation(info, $"Added table '{def.TableName}' to perspective '{perspectiveName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "add table to perspective", OperationType.Create);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = $"Table '{def.TableName}' added to perspective '{perspectiveName}' successfully"
		};
	}

	internal static PerspectiveOperationResult RemoveTableFromPerspectiveInternal(IConnectionInfo info, string perspectiveName, string tableName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		Perspective obj = info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User);
		PerspectiveTable perspectiveTable = obj.PerspectiveTables.Find(tableName);
		if (perspectiveTable == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		}
		obj.PerspectiveTables.Remove(perspectiveTable);
		TransactionOperations.RecordOperation(info, $"Removed table '{tableName}' from perspective '{perspectiveName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "remove table from perspective", OperationType.Delete);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = $"Table '{tableName}' removed from perspective '{perspectiveName}' successfully"
		};
	}

	internal static PerspectiveOperationResult UpdatePerspectiveTableInternal(IConnectionInfo info, string perspectiveName, string tableName, PerspectiveTableDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Update definition cannot be null", ErrorSource.User);
		}
		PerspectiveTable perspectiveTable = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName);
		if (perspectiveTable == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		}
		bool flag = false;
		if (update.IncludeAll.HasValue && perspectiveTable.IncludeAll != update.IncludeAll.Value)
		{
			perspectiveTable.IncludeAll = update.IncludeAll.Value;
			flag = true;
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(perspectiveTable, update.Annotations, (PerspectiveTable pt) => pt.Annotations))
		{
			flag = true;
		}
		if (!flag)
		{
			return new PerspectiveOperationResult
			{
				Success = true,
				PerspectiveName = perspectiveName,
				Message = $"Table '{tableName}' in perspective '{perspectiveName}' is already in the requested state",
				HasChanges = false
			};
		}
		TransactionOperations.RecordOperation(info, $"Updated table '{tableName}' in perspective '{perspectiveName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "update perspective table", OperationType.Update);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = $"Table '{tableName}' in perspective '{perspectiveName}' updated successfully",
			HasChanges = true
		};
	}

	public static async Task<List<Dictionary<string, string>>> ListPerspectiveColumns(string? connectionName, string perspectiveName, string tableName)
	{
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		List<Dictionary<string, string>> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				PerspectiveTable perspectiveTable = (connectionInfo.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName);
				if (perspectiveTable == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
				}
				List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
				foreach (PerspectiveColumn perspectiveColumn in perspectiveTable.PerspectiveColumns)
				{
					list.Add(new Dictionary<string, string>
					{
						["Name"] = perspectiveColumn.Name,
						["ColumnName"] = perspectiveColumn.Column?.Name ?? "",
						["TableName"] = perspectiveTable.Table?.Name ?? "",
						["ModifiedTime"] = perspectiveColumn.ModifiedTime.ToString("yyyy-MM-dd HH:mm:ss"),
						["DataType"] = perspectiveColumn.Column?.DataType.ToString() ?? "",
						["IsHidden"] = perspectiveColumn.Column?.IsHidden.ToString() ?? "false"
					});
				}
				AuditEvent.Default.Emit("list perspective columns", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list perspective columns", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static PerspectiveColumnGet GetPerspectiveColumnInternal(IConnectionInfo info, string perspectiveName, string tableName, string columnName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(columnName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column name is required", ErrorSource.User);
		}
		PerspectiveTable perspectiveTable = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName);
		if (perspectiveTable == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		}
		PerspectiveColumn perspectiveColumn = perspectiveTable.PerspectiveColumns.Find(columnName);
		if (perspectiveColumn == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{columnName}' not found in perspective table '{tableName}'", ErrorSource.User);
		}
		PerspectiveColumnGet perspectiveColumnGet = new PerspectiveColumnGet
		{
			Name = perspectiveColumn.Name,
			ColumnName = perspectiveColumn.Column?.Name,
			TableName = perspectiveTable.Table?.Name,
			ModifiedTime = perspectiveColumn.ModifiedTime,
			Annotations = new List<KeyValuePair<string, string>>()
		};
		foreach (Annotation annotation in perspectiveColumn.Annotations)
		{
			perspectiveColumnGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		return perspectiveColumnGet;
	}

	internal static PerspectiveOperationResult AddColumnToPerspectiveTableInternal(IConnectionInfo info, string perspectiveName, PerspectiveColumnDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective column definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.ColumnName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column name is required", ErrorSource.User);
		}
		PerspectiveTable obj = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(def.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{def.TableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		Column column = obj.Table?.Columns.Find(def.ColumnName);
		if (column == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{def.ColumnName}' not found in table '{def.TableName}'", ErrorSource.User);
		}
		if (obj.PerspectiveColumns.Find(def.ColumnName) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{def.ColumnName}' already exists in perspective table '{def.TableName}'", ErrorSource.User);
		}
		PerspectiveColumn perspectiveColumn = new PerspectiveColumn
		{
			Column = column
		};
		if (def.Annotations != null)
		{
			AnnotationHelpers.ApplyAnnotations(perspectiveColumn, def.Annotations, (PerspectiveColumn pc) => pc.Annotations);
		}
		obj.PerspectiveColumns.Add(perspectiveColumn);
		TransactionOperations.RecordOperation(info, $"Added column '{def.ColumnName}' to perspective table '{def.TableName}' in perspective '{perspectiveName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "add column to perspective table", OperationType.Create);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = $"Column '{def.ColumnName}' added to perspective table '{def.TableName}' successfully"
		};
	}

	internal static PerspectiveOperationResult RemoveColumnFromPerspectiveTableInternal(IConnectionInfo info, string perspectiveName, string tableName, string columnName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(columnName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column name is required", ErrorSource.User);
		}
		PerspectiveTable obj = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		PerspectiveColumn perspectiveColumn = obj.PerspectiveColumns.Find(columnName);
		if (perspectiveColumn == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{columnName}' not found in perspective table '{tableName}'", ErrorSource.User);
		}
		obj.PerspectiveColumns.Remove(perspectiveColumn);
		TransactionOperations.RecordOperation(info, $"Removed column '{columnName}' from perspective table '{tableName}' in perspective '{perspectiveName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "remove column from perspective table", OperationType.Delete);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = $"Column '{columnName}' removed from perspective table '{tableName}' successfully"
		};
	}

	public static async Task<List<Dictionary<string, string>>> ListPerspectiveMeasures(string? connectionName, string perspectiveName, string tableName)
	{
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		List<Dictionary<string, string>> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				PerspectiveTable perspectiveTable = (connectionInfo.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName);
				if (perspectiveTable == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
				}
				List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
				foreach (PerspectiveMeasure perspectiveMeasure in perspectiveTable.PerspectiveMeasures)
				{
					list.Add(new Dictionary<string, string>
					{
						["Name"] = perspectiveMeasure.Name,
						["MeasureName"] = perspectiveMeasure.Measure?.Name ?? "",
						["TableName"] = perspectiveTable.Table?.Name ?? "",
						["ModifiedTime"] = perspectiveMeasure.ModifiedTime.ToString("yyyy-MM-dd HH:mm:ss"),
						["DataType"] = perspectiveMeasure.Measure?.DataType.ToString() ?? "",
						["FormatString"] = perspectiveMeasure.Measure?.FormatString ?? "",
						["IsHidden"] = perspectiveMeasure.Measure?.IsHidden.ToString() ?? "false",
						["Expression"] = perspectiveMeasure.Measure?.Expression ?? ""
					});
				}
				AuditEvent.Default.Emit("list perspective measures", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list perspective measures", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static PerspectiveMeasureGet GetPerspectiveMeasureInternal(IConnectionInfo info, string perspectiveName, string tableName, string measureName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(measureName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Measure name is required", ErrorSource.User);
		}
		PerspectiveTable perspectiveTable = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName);
		if (perspectiveTable == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		}
		PerspectiveMeasure perspectiveMeasure = perspectiveTable.PerspectiveMeasures.Find(measureName);
		if (perspectiveMeasure == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure '{measureName}' not found in perspective table '{tableName}'", ErrorSource.User);
		}
		PerspectiveMeasureGet perspectiveMeasureGet = new PerspectiveMeasureGet
		{
			Name = perspectiveMeasure.Name,
			MeasureName = perspectiveMeasure.Measure?.Name,
			TableName = perspectiveTable.Table?.Name,
			ModifiedTime = perspectiveMeasure.ModifiedTime,
			Annotations = new List<KeyValuePair<string, string>>()
		};
		foreach (Annotation annotation in perspectiveMeasure.Annotations)
		{
			perspectiveMeasureGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		return perspectiveMeasureGet;
	}

	internal static PerspectiveOperationResult AddMeasureToPerspectiveTableInternal(IConnectionInfo info, string perspectiveName, PerspectiveMeasureDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective measure definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.MeasureName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Measure name is required", ErrorSource.User);
		}
		PerspectiveTable obj = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(def.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{def.TableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		Measure measure = obj.Table?.Measures.Find(def.MeasureName);
		if (measure == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure '{def.MeasureName}' not found in table '{def.TableName}'", ErrorSource.User);
		}
		if (obj.PerspectiveMeasures.Find(def.MeasureName) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure '{def.MeasureName}' already exists in perspective table '{def.TableName}'", ErrorSource.User);
		}
		PerspectiveMeasure perspectiveMeasure = new PerspectiveMeasure
		{
			Measure = measure
		};
		if (def.Annotations != null)
		{
			AnnotationHelpers.ApplyAnnotations(perspectiveMeasure, def.Annotations, (PerspectiveMeasure pm) => pm.Annotations);
		}
		obj.PerspectiveMeasures.Add(perspectiveMeasure);
		TransactionOperations.RecordOperation(info, $"Added measure '{def.MeasureName}' to perspective table '{def.TableName}' in perspective '{perspectiveName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "add measure to perspective table", OperationType.Create);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = $"Measure '{def.MeasureName}' added to perspective table '{def.TableName}' successfully"
		};
	}

	internal static PerspectiveOperationResult RemoveMeasureFromPerspectiveTableInternal(IConnectionInfo info, string perspectiveName, string tableName, string measureName)
	{
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(measureName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Measure name is required", ErrorSource.User);
		}
		PerspectiveTable obj = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		PerspectiveMeasure perspectiveMeasure = obj.PerspectiveMeasures.Find(measureName);
		if (perspectiveMeasure == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure '{measureName}' not found in perspective table '{tableName}'", ErrorSource.User);
		}
		obj.PerspectiveMeasures.Remove(perspectiveMeasure);
		TransactionOperations.RecordOperation(info, $"Removed measure '{measureName}' from perspective table '{tableName}' in perspective '{perspectiveName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "remove measure from perspective table", OperationType.Delete);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = $"Measure '{measureName}' removed from perspective table '{tableName}' successfully"
		};
	}

	public static async Task<List<Dictionary<string, string>>> ListPerspectiveHierarchies(string? connectionName, string perspectiveName, string tableName)
	{
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		List<Dictionary<string, string>> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				PerspectiveTable perspectiveTable = (connectionInfo.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName);
				if (perspectiveTable == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
				}
				List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
				foreach (PerspectiveHierarchy perspectiveHierarchy in perspectiveTable.PerspectiveHierarchies)
				{
					list.Add(new Dictionary<string, string>
					{
						["Name"] = perspectiveHierarchy.Name,
						["HierarchyName"] = perspectiveHierarchy.Hierarchy?.Name ?? "",
						["TableName"] = perspectiveTable.Table?.Name ?? "",
						["ModifiedTime"] = perspectiveHierarchy.ModifiedTime.ToString("yyyy-MM-dd HH:mm:ss"),
						["IsHidden"] = perspectiveHierarchy.Hierarchy?.IsHidden.ToString() ?? "false",
						["LevelCount"] = perspectiveHierarchy.Hierarchy?.Levels.Count.ToString() ?? "0",
						["DisplayFolder"] = perspectiveHierarchy.Hierarchy?.DisplayFolder ?? "",
						["Description"] = perspectiveHierarchy.Hierarchy?.Description ?? ""
					});
				}
				AuditEvent.Default.Emit("list perspective hierarchies", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list perspective hierarchies", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static PerspectiveHierarchyGet GetPerspectiveHierarchyInternal(IConnectionInfo info, string perspectiveName, string tableName, string hierarchyName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Hierarchy name is required", ErrorSource.User);
		}
		PerspectiveTable perspectiveTable = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName);
		if (perspectiveTable == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		}
		PerspectiveHierarchy perspectiveHierarchy = perspectiveTable.PerspectiveHierarchies.Find(hierarchyName);
		if (perspectiveHierarchy == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in perspective table '{tableName}'", ErrorSource.User);
		}
		PerspectiveHierarchyGet perspectiveHierarchyGet = new PerspectiveHierarchyGet
		{
			Name = perspectiveHierarchy.Name,
			HierarchyName = perspectiveHierarchy.Hierarchy?.Name,
			TableName = perspectiveTable.Table?.Name,
			ModifiedTime = perspectiveHierarchy.ModifiedTime,
			Annotations = new List<KeyValuePair<string, string>>()
		};
		foreach (Annotation annotation in perspectiveHierarchy.Annotations)
		{
			perspectiveHierarchyGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		return perspectiveHierarchyGet;
	}

	internal static PerspectiveOperationResult AddHierarchyToPerspectiveTableInternal(IConnectionInfo info, string perspectiveName, PerspectiveHierarchyDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective hierarchy definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.HierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Hierarchy name is required", ErrorSource.User);
		}
		PerspectiveTable obj = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(def.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{def.TableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		Hierarchy hierarchy = obj.Table?.Hierarchies.Find(def.HierarchyName);
		if (hierarchy == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{def.HierarchyName}' not found in table '{def.TableName}'", ErrorSource.User);
		}
		if (obj.PerspectiveHierarchies.Find(def.HierarchyName) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{def.HierarchyName}' already exists in perspective table '{def.TableName}'", ErrorSource.User);
		}
		PerspectiveHierarchy perspectiveHierarchy = new PerspectiveHierarchy
		{
			Hierarchy = hierarchy
		};
		if (def.Annotations != null)
		{
			AnnotationHelpers.ApplyAnnotations(perspectiveHierarchy, def.Annotations, (PerspectiveHierarchy ph) => ph.Annotations);
		}
		obj.PerspectiveHierarchies.Add(perspectiveHierarchy);
		TransactionOperations.RecordOperation(info, $"Added hierarchy '{def.HierarchyName}' to perspective table '{def.TableName}' in perspective '{perspectiveName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "add hierarchy to perspective table", OperationType.Create);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = $"Hierarchy '{def.HierarchyName}' added to perspective table '{def.TableName}' successfully"
		};
	}

	internal static PerspectiveOperationResult RemoveHierarchyFromPerspectiveTableInternal(IConnectionInfo info, string perspectiveName, string tableName, string hierarchyName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Hierarchy name is required", ErrorSource.User);
		}
		PerspectiveTable obj = (info.Database.Model.Perspectives.Find(perspectiveName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Perspective '" + perspectiveName + "' not found", ErrorSource.User)).PerspectiveTables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{tableName}' not found in perspective '{perspectiveName}'", ErrorSource.User);
		PerspectiveHierarchy perspectiveHierarchy = obj.PerspectiveHierarchies.Find(hierarchyName);
		if (perspectiveHierarchy == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in perspective table '{tableName}'", ErrorSource.User);
		}
		obj.PerspectiveHierarchies.Remove(perspectiveHierarchy);
		TransactionOperations.RecordOperation(info, $"Removed hierarchy '{hierarchyName}' from perspective table '{tableName}' in perspective '{perspectiveName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "remove hierarchy from perspective table", OperationType.Delete);
		return new PerspectiveOperationResult
		{
			Success = true,
			PerspectiveName = perspectiveName,
			Message = $"Hierarchy '{hierarchyName}' removed from perspective table '{tableName}' successfully"
		};
	}

	public static async Task<BatchOperationResponse> AddPerspectiveTables(string? connectionName, string perspectiveName, List<PerspectiveTableDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "AddTables",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective tables provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			response.Success = false;
			response.Message = "Perspective name is required";
			response.Summary = new BatchSummary
			{
				TotalItems = items.Count,
				SuccessCount = 0,
				FailureCount = items.Count,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			int count = items.Count;
			int successCount = 0;
			int failureCount = 0;
			bool flag = false;
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveTableDefinition perspectiveTableDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = (perspectiveTableDefinition.TableName ?? $"Item_{i}")
					};
					try
					{
						PerspectiveOperationResult perspectiveOperationResult = AddTableToPerspectiveInternal(connectionInfo, perspectiveName, perspectiveTableDefinition);
						itemResult.Success = perspectiveOperationResult.Success;
						itemResult.Message = (itemResult.Success ? $"Successfully added table '{perspectiveTableDefinition.TableName}' to perspective '{perspectiveName}'" : ("Failed to add table '" + perspectiveTableDefinition.TableName + "': " + perspectiveOperationResult.Message));
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
						itemResult.Message = "Error adding table '" + perspectiveTableDefinition.TableName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				if (transactionId != null)
				{
					if (failureCount == 0)
					{
						if (ownsTransaction)
						{
							try
							{
								TransactionOperations.CommitTransactionInternal(connectionInfo);
							}
							catch (Exception ex2)
							{
								flag = !ExceptionHelper.HandleCommitFailure(ex2, warnings, response.Exceptions);
								BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
							}
						}
						else
						{
							response.Warnings.Add("Transaction remains open for explicit commit by caller.");
						}
					}
					else if (ownsTransaction)
					{
						try
						{
							TransactionOperations.RollbackTransactionInternal(connectionInfo);
							BatchTransactionHelper.ApplyPerItemFailureRollback(response.Results, ref successCount, ref failureCount, warnings);
						}
						catch (Exception ex3)
						{
							response.Warnings.Add("Failed to rollback transaction: " + ex3.Message);
						}
					}
					else
					{
						response.Warnings.Add("Transaction remains open. Caller must handle rollback due to operation errors.");
					}
				}
				response.Success = failureCount == 0 && !flag;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {count} perspective table(s): {successCount} succeeded, {failureCount} failed";
				}
			}
			catch (Exception ex4)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch (Exception ex5)
					{
						response.Warnings.Add("Failed to rollback transaction: " + ex5.Message);
					}
				}
				response.Success = false;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = "Batch operation failed: " + ex4.Message;
				}
			}
			stopwatch.Stop();
			response.Summary = new BatchSummary
			{
				TotalItems = count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> UpdatePerspectiveTables(string? connectionName, string perspectiveName, List<PerspectiveTableDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "UpdateTables",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective tables provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			response.Success = false;
			response.Message = "Perspective name is required";
			response.Summary = new BatchSummary
			{
				TotalItems = items.Count,
				SuccessCount = 0,
				FailureCount = items.Count,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			int count = items.Count;
			int successCount = 0;
			int failureCount = 0;
			bool flag = false;
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveTableDefinition perspectiveTableDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = (perspectiveTableDefinition.TableName ?? $"Item_{i}")
					};
					try
					{
						PerspectiveOperationResult perspectiveOperationResult = UpdatePerspectiveTableInternal(connectionInfo, perspectiveName, perspectiveTableDefinition.TableName, perspectiveTableDefinition);
						itemResult.Success = perspectiveOperationResult.Success;
						itemResult.Message = (itemResult.Success ? $"Successfully updated table '{perspectiveTableDefinition.TableName}' in perspective '{perspectiveName}'" : ("Failed to update table '" + perspectiveTableDefinition.TableName + "': " + perspectiveOperationResult.Message));
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
						itemResult.Message = "Error updating table '" + perspectiveTableDefinition.TableName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				if (transactionId != null)
				{
					if (failureCount == 0)
					{
						if (ownsTransaction)
						{
							try
							{
								TransactionOperations.CommitTransactionInternal(connectionInfo);
							}
							catch (Exception ex2)
							{
								flag = !ExceptionHelper.HandleCommitFailure(ex2, warnings, response.Exceptions);
								BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
							}
						}
						else
						{
							response.Warnings.Add("Transaction remains open for explicit commit by caller.");
						}
					}
					else if (ownsTransaction)
					{
						try
						{
							TransactionOperations.RollbackTransactionInternal(connectionInfo);
							BatchTransactionHelper.ApplyPerItemFailureRollback(response.Results, ref successCount, ref failureCount, warnings);
						}
						catch (Exception ex3)
						{
							response.Warnings.Add("Failed to rollback transaction: " + ex3.Message);
						}
					}
					else
					{
						response.Warnings.Add("Transaction remains open. Caller must handle rollback due to operation errors.");
					}
				}
				response.Success = failureCount == 0 && !flag;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {count} perspective table(s): {successCount} succeeded, {failureCount} failed";
				}
			}
			catch (Exception ex4)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch (Exception ex5)
					{
						response.Warnings.Add("Failed to rollback transaction: " + ex5.Message);
					}
				}
				response.Success = false;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = "Batch operation failed: " + ex4.Message;
				}
			}
			stopwatch.Stop();
			response.Summary = new BatchSummary
			{
				TotalItems = count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RemovePerspectiveTables(string? connectionName, string perspectiveName, List<PerspectiveTableDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "RemoveTables",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective tables provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			response.Success = false;
			response.Message = "Perspective name is required";
			response.Summary = new BatchSummary
			{
				TotalItems = items.Count,
				SuccessCount = 0,
				FailureCount = items.Count,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			int count = items.Count;
			int successCount = 0;
			int failureCount = 0;
			bool flag = false;
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveTableDefinition perspectiveTableDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveTableDefinition.TableName
					};
					try
					{
						PerspectiveOperationResult perspectiveOperationResult = RemoveTableFromPerspectiveInternal(connectionInfo, perspectiveName, perspectiveTableDefinition.TableName);
						itemResult.Success = perspectiveOperationResult.Success;
						itemResult.Message = (itemResult.Success ? $"Successfully removed table '{perspectiveTableDefinition.TableName}' from perspective '{perspectiveName}'" : ("Failed to remove table '" + perspectiveTableDefinition.TableName + "': " + perspectiveOperationResult.Message));
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
						itemResult.Message = "Error removing table '" + perspectiveTableDefinition.TableName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				if (transactionId != null)
				{
					if (failureCount == 0)
					{
						if (ownsTransaction)
						{
							try
							{
								TransactionOperations.CommitTransactionInternal(connectionInfo);
							}
							catch (Exception ex2)
							{
								flag = !ExceptionHelper.HandleCommitFailure(ex2, warnings, response.Exceptions);
								BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
							}
						}
						else
						{
							response.Warnings.Add("Transaction remains open for explicit commit by caller.");
						}
					}
					else if (ownsTransaction)
					{
						try
						{
							TransactionOperations.RollbackTransactionInternal(connectionInfo);
							BatchTransactionHelper.ApplyPerItemFailureRollback(response.Results, ref successCount, ref failureCount, warnings);
						}
						catch (Exception ex3)
						{
							response.Warnings.Add("Failed to rollback transaction: " + ex3.Message);
						}
					}
					else
					{
						response.Warnings.Add("Transaction remains open. Caller must handle rollback due to operation errors.");
					}
				}
				response.Success = failureCount == 0 && !flag;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {count} perspective table(s): {successCount} succeeded, {failureCount} failed";
				}
			}
			catch (Exception ex4)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch (Exception ex5)
					{
						response.Warnings.Add("Failed to rollback transaction: " + ex5.Message);
					}
				}
				response.Success = false;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = "Batch operation failed: " + ex4.Message;
				}
			}
			stopwatch.Stop();
			response.Summary = new BatchSummary
			{
				TotalItems = count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> GetPerspectiveTables(string? connectionName, string perspectiveName, List<PerspectiveTableDefinition> items)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "GetTables",
			Results = new List<ItemResult>()
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective tables provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		List<PerspectiveTableGet> dataList = new List<PerspectiveTableGet>();
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveTableDefinition perspectiveTableDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveTableDefinition.TableName
					};
					try
					{
						PerspectiveTableGet perspectiveTableInternal = GetPerspectiveTableInternal(connectionInfo, perspectiveName, perspectiveTableDefinition.TableName);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved table '" + perspectiveTableDefinition.TableName + "'";
						itemResult.Data = perspectiveTableInternal;
						dataList.Add(perspectiveTableInternal);
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving table '" + perspectiveTableDefinition.TableName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
				}
				response.Success = failureCount == 0;
				response.Message = $"Retrieved {successCount} of {totalItems} tables";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Batch operation failed: " + ex2.Message;
				failureCount = totalItems - successCount;
			}
			stopwatch.Stop();
			AuditEvent.Default.Emit("get perspective tables", response.Success, OperationType.Read, connectionInfo);
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

	public static async Task<BatchOperationResponse> AddPerspectiveColumns(string? connectionName, string perspectiveName, List<PerspectiveColumnDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "AddColumns",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective columns provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			int count = items.Count;
			int successCount = 0;
			int failureCount = 0;
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveColumnDefinition perspectiveColumnDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveColumnDefinition.TableName + "." + perspectiveColumnDefinition.ColumnName
					};
					try
					{
						PerspectiveOperationResult perspectiveOperationResult = AddColumnToPerspectiveTableInternal(connectionInfo, perspectiveName, perspectiveColumnDefinition);
						itemResult.Success = perspectiveOperationResult.Success;
						itemResult.Message = (itemResult.Success ? $"Successfully added column '{perspectiveColumnDefinition.ColumnName}' from table '{perspectiveColumnDefinition.TableName}'" : ("Failed to add column: " + perspectiveOperationResult.Message));
						if (itemResult.Success)
						{
							successCount++;
							if (transactionId != null)
							{
								TransactionOperations.RecordOperation(connectionInfo, $"Added column '{perspectiveColumnDefinition.TableName}.{perspectiveColumnDefinition.ColumnName}' to perspective '{perspectiveName}'");
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
						itemResult.Message = "Error adding column: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, count, ref successCount, ref failureCount, "Added", "column(s)");
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {count} perspective column(s): {successCount} succeeded, {failureCount} failed";
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
					catch (Exception ex3)
					{
						response.Warnings.Add("Failed to rollback transaction: " + ex3.Message);
					}
				}
				response.Success = false;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = "Batch operation failed: " + ex2.Message;
				}
			}
			stopwatch.Stop();
			response.Summary = new BatchSummary
			{
				TotalItems = count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RemovePerspectiveColumns(string? connectionName, string perspectiveName, List<PerspectiveColumnDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "RemoveColumns",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective columns provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		bool transactionFailed = false;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveColumnDefinition perspectiveColumnDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveColumnDefinition.TableName + "." + perspectiveColumnDefinition.ColumnName
					};
					try
					{
						PerspectiveOperationResult perspectiveOperationResult = RemoveColumnFromPerspectiveTableInternal(connectionInfo, perspectiveName, perspectiveColumnDefinition.TableName, perspectiveColumnDefinition.ColumnName);
						itemResult.Success = perspectiveOperationResult.Success;
						itemResult.Message = (itemResult.Success ? $"Successfully removed column '{perspectiveColumnDefinition.ColumnName}' from table '{perspectiveColumnDefinition.TableName}'" : ("Failed to remove column: " + perspectiveOperationResult.Message));
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
						itemResult.Message = "Error removing column: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				if (transactionId != null)
				{
					if (failureCount == 0)
					{
						if (ownsTransaction)
						{
							try
							{
								TransactionOperations.CommitTransactionInternal(connectionInfo);
							}
							catch (Exception ex2)
							{
								transactionFailed = !ExceptionHelper.HandleCommitFailure(ex2, warnings, response.Exceptions);
								BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
							}
						}
						else
						{
							response.Warnings.Add("Transaction remains open for explicit commit by caller.");
						}
					}
					else if (ownsTransaction)
					{
						try
						{
							TransactionOperations.RollbackTransactionInternal(connectionInfo);
							BatchTransactionHelper.ApplyPerItemFailureRollback(response.Results, ref successCount, ref failureCount, warnings);
						}
						catch (Exception ex3)
						{
							response.Warnings.Add("Failed to rollback transaction: " + ex3.Message);
						}
					}
					else
					{
						response.Warnings.Add("Transaction remains open. Caller must handle rollback due to operation errors.");
					}
				}
				response.Success = failureCount == 0 && !transactionFailed;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {totalItems} perspective column(s): {successCount} succeeded, {failureCount} failed";
				}
			}
			catch (Exception ex4)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch (Exception ex5)
					{
						response.Warnings.Add("Failed to rollback transaction: " + ex5.Message);
					}
				}
				response.Success = false;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = "Batch operation failed: " + ex4.Message;
				}
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

	public static async Task<BatchOperationResponse> GetPerspectiveColumns(string? connectionName, string perspectiveName, List<PerspectiveColumnDefinition> items)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "GetColumns",
			Results = new List<ItemResult>()
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective columns provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		List<PerspectiveColumnGet> dataList = new List<PerspectiveColumnGet>();
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveColumnDefinition perspectiveColumnDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveColumnDefinition.TableName + "." + perspectiveColumnDefinition.ColumnName
					};
					try
					{
						PerspectiveColumnGet perspectiveColumnInternal = GetPerspectiveColumnInternal(connectionInfo, perspectiveName, perspectiveColumnDefinition.TableName, perspectiveColumnDefinition.ColumnName);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved column '" + perspectiveColumnDefinition.ColumnName + "'";
						itemResult.Data = perspectiveColumnInternal;
						dataList.Add(perspectiveColumnInternal);
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving column: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
				}
				response.Success = failureCount == 0;
				response.Message = $"Retrieved {successCount} of {totalItems} columns";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Batch operation failed: " + ex2.Message;
				failureCount = totalItems - successCount;
			}
			stopwatch.Stop();
			AuditEvent.Default.Emit("get perspective columns", response.Success, OperationType.Read, connectionInfo);
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

	public static async Task<BatchOperationResponse> AddPerspectiveMeasures(string? connectionName, string perspectiveName, List<PerspectiveMeasureDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "AddMeasures",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective measures provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		bool transactionFailed = false;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveMeasureDefinition perspectiveMeasureDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveMeasureDefinition.TableName + "." + perspectiveMeasureDefinition.MeasureName
					};
					try
					{
						PerspectiveOperationResult perspectiveOperationResult = AddMeasureToPerspectiveTableInternal(connectionInfo, perspectiveName, perspectiveMeasureDefinition);
						itemResult.Success = perspectiveOperationResult.Success;
						itemResult.Message = (itemResult.Success ? $"Successfully added measure '{perspectiveMeasureDefinition.MeasureName}' from table '{perspectiveMeasureDefinition.TableName}'" : ("Failed to add measure: " + perspectiveOperationResult.Message));
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
						itemResult.Message = "Error adding measure: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				if (transactionId != null)
				{
					if (failureCount == 0)
					{
						if (ownsTransaction)
						{
							try
							{
								TransactionOperations.CommitTransactionInternal(connectionInfo);
							}
							catch (Exception ex2)
							{
								transactionFailed = !ExceptionHelper.HandleCommitFailure(ex2, warnings, response.Exceptions);
								BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
							}
						}
						else
						{
							response.Warnings.Add("Transaction remains open for explicit commit by caller.");
						}
					}
					else if (ownsTransaction)
					{
						try
						{
							TransactionOperations.RollbackTransactionInternal(connectionInfo);
							BatchTransactionHelper.ApplyPerItemFailureRollback(response.Results, ref successCount, ref failureCount, warnings);
						}
						catch (Exception ex3)
						{
							response.Warnings.Add("Failed to rollback transaction: " + ex3.Message);
						}
					}
					else
					{
						response.Warnings.Add("Transaction remains open. Caller must handle rollback due to operation errors.");
					}
				}
				response.Success = failureCount == 0 && !transactionFailed;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {totalItems} perspective measure(s): {successCount} succeeded, {failureCount} failed";
				}
			}
			catch (Exception ex4)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch (Exception ex5)
					{
						response.Warnings.Add("Failed to rollback transaction: " + ex5.Message);
					}
				}
				response.Success = false;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = "Batch operation failed: " + ex4.Message;
				}
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

	public static async Task<BatchOperationResponse> RemovePerspectiveMeasures(string? connectionName, string perspectiveName, List<PerspectiveMeasureDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "RemoveMeasures",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective measures provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		bool transactionFailed = false;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveMeasureDefinition perspectiveMeasureDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveMeasureDefinition.TableName + "." + perspectiveMeasureDefinition.MeasureName
					};
					try
					{
						PerspectiveOperationResult perspectiveOperationResult = RemoveMeasureFromPerspectiveTableInternal(connectionInfo, perspectiveName, perspectiveMeasureDefinition.TableName, perspectiveMeasureDefinition.MeasureName);
						itemResult.Success = perspectiveOperationResult.Success;
						itemResult.Message = (itemResult.Success ? $"Successfully removed measure '{perspectiveMeasureDefinition.MeasureName}' from table '{perspectiveMeasureDefinition.TableName}'" : ("Failed to remove measure: " + perspectiveOperationResult.Message));
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
						itemResult.Message = "Error removing measure: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				if (transactionId != null)
				{
					if (failureCount == 0)
					{
						if (ownsTransaction)
						{
							try
							{
								TransactionOperations.CommitTransactionInternal(connectionInfo);
							}
							catch (Exception ex2)
							{
								transactionFailed = !ExceptionHelper.HandleCommitFailure(ex2, warnings, response.Exceptions);
								BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
							}
						}
						else
						{
							response.Warnings.Add("Transaction remains open for explicit commit by caller.");
						}
					}
					else if (ownsTransaction)
					{
						try
						{
							TransactionOperations.RollbackTransactionInternal(connectionInfo);
							BatchTransactionHelper.ApplyPerItemFailureRollback(response.Results, ref successCount, ref failureCount, warnings);
						}
						catch (Exception ex3)
						{
							response.Warnings.Add("Failed to rollback transaction: " + ex3.Message);
						}
					}
					else
					{
						response.Warnings.Add("Transaction remains open. Caller must handle rollback due to operation errors.");
					}
				}
				response.Success = failureCount == 0 && !transactionFailed;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {totalItems} perspective measure(s): {successCount} succeeded, {failureCount} failed";
				}
			}
			catch (Exception ex4)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch (Exception ex5)
					{
						response.Warnings.Add("Failed to rollback transaction: " + ex5.Message);
					}
				}
				response.Success = false;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = "Batch operation failed: " + ex4.Message;
				}
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

	public static async Task<BatchOperationResponse> GetPerspectiveMeasures(string? connectionName, string perspectiveName, List<PerspectiveMeasureDefinition> items)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "GetMeasures",
			Results = new List<ItemResult>()
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective measures provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		List<PerspectiveMeasureGet> dataList = new List<PerspectiveMeasureGet>();
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveMeasureDefinition perspectiveMeasureDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveMeasureDefinition.TableName + "." + perspectiveMeasureDefinition.MeasureName
					};
					try
					{
						PerspectiveMeasureGet perspectiveMeasureInternal = GetPerspectiveMeasureInternal(connectionInfo, perspectiveName, perspectiveMeasureDefinition.TableName, perspectiveMeasureDefinition.MeasureName);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved measure '" + perspectiveMeasureDefinition.MeasureName + "'";
						itemResult.Data = perspectiveMeasureInternal;
						dataList.Add(perspectiveMeasureInternal);
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving measure: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
				}
				response.Success = failureCount == 0;
				response.Message = $"Retrieved {successCount} of {totalItems} measures";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Batch operation failed: " + ex2.Message;
				failureCount = totalItems - successCount;
			}
			stopwatch.Stop();
			AuditEvent.Default.Emit("get perspective measures", response.Success, OperationType.Read, connectionInfo);
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

	public static async Task<BatchOperationResponse> AddPerspectiveHierarchies(string? connectionName, string perspectiveName, List<PerspectiveHierarchyDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "AddHierarchies",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective hierarchies provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		bool transactionFailed = false;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveHierarchyDefinition perspectiveHierarchyDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveHierarchyDefinition.TableName + "." + perspectiveHierarchyDefinition.HierarchyName
					};
					try
					{
						PerspectiveOperationResult perspectiveOperationResult = AddHierarchyToPerspectiveTableInternal(connectionInfo, perspectiveName, perspectiveHierarchyDefinition);
						itemResult.Success = perspectiveOperationResult.Success;
						itemResult.Message = (itemResult.Success ? $"Successfully added hierarchy '{perspectiveHierarchyDefinition.HierarchyName}' from table '{perspectiveHierarchyDefinition.TableName}'" : ("Failed to add hierarchy: " + perspectiveOperationResult.Message));
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
						itemResult.Message = "Error adding hierarchy: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				if (transactionId != null)
				{
					if (failureCount == 0)
					{
						if (ownsTransaction)
						{
							try
							{
								TransactionOperations.CommitTransactionInternal(connectionInfo);
							}
							catch (Exception ex2)
							{
								transactionFailed = !ExceptionHelper.HandleCommitFailure(ex2, warnings, response.Exceptions);
								BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
							}
						}
						else
						{
							response.Warnings.Add("Transaction remains open for explicit commit by caller.");
						}
					}
					else if (ownsTransaction)
					{
						try
						{
							TransactionOperations.RollbackTransactionInternal(connectionInfo);
							BatchTransactionHelper.ApplyPerItemFailureRollback(response.Results, ref successCount, ref failureCount, warnings);
						}
						catch (Exception ex3)
						{
							response.Warnings.Add("Failed to rollback transaction: " + ex3.Message);
						}
					}
					else
					{
						response.Warnings.Add("Transaction remains open. Caller must handle rollback due to operation errors.");
					}
				}
				response.Success = failureCount == 0 && !transactionFailed;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {totalItems} perspective hierarchy(s): {successCount} succeeded, {failureCount} failed";
				}
			}
			catch (Exception ex4)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch (Exception ex5)
					{
						response.Warnings.Add("Failed to rollback transaction: " + ex5.Message);
					}
				}
				response.Success = false;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = "Batch operation failed: " + ex4.Message;
				}
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

	public static async Task<BatchOperationResponse> RemovePerspectiveHierarchies(string? connectionName, string perspectiveName, List<PerspectiveHierarchyDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "RemoveHierarchies",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective hierarchies provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		bool transactionFailed = false;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveHierarchyDefinition perspectiveHierarchyDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveHierarchyDefinition.TableName + "." + perspectiveHierarchyDefinition.HierarchyName
					};
					try
					{
						PerspectiveOperationResult perspectiveOperationResult = RemoveHierarchyFromPerspectiveTableInternal(connectionInfo, perspectiveName, perspectiveHierarchyDefinition.TableName, perspectiveHierarchyDefinition.HierarchyName);
						itemResult.Success = perspectiveOperationResult.Success;
						itemResult.Message = (itemResult.Success ? $"Successfully removed hierarchy '{perspectiveHierarchyDefinition.HierarchyName}' from table '{perspectiveHierarchyDefinition.TableName}'" : ("Failed to remove hierarchy: " + perspectiveOperationResult.Message));
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
						itemResult.Message = "Error removing hierarchy: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				if (transactionId != null)
				{
					if (failureCount == 0)
					{
						if (ownsTransaction)
						{
							try
							{
								TransactionOperations.CommitTransactionInternal(connectionInfo);
							}
							catch (Exception ex2)
							{
								transactionFailed = !ExceptionHelper.HandleCommitFailure(ex2, warnings, response.Exceptions);
								BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
							}
						}
						else
						{
							response.Warnings.Add("Transaction remains open for explicit commit by caller.");
						}
					}
					else if (ownsTransaction)
					{
						try
						{
							TransactionOperations.RollbackTransactionInternal(connectionInfo);
							BatchTransactionHelper.ApplyPerItemFailureRollback(response.Results, ref successCount, ref failureCount, warnings);
						}
						catch (Exception ex3)
						{
							response.Warnings.Add("Failed to rollback transaction: " + ex3.Message);
						}
					}
					else
					{
						response.Warnings.Add("Transaction remains open. Caller must handle rollback due to operation errors.");
					}
				}
				response.Success = failureCount == 0 && !transactionFailed;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {totalItems} perspective hierarchy(s): {successCount} succeeded, {failureCount} failed";
				}
			}
			catch (Exception ex4)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch (Exception ex5)
					{
						response.Warnings.Add("Failed to rollback transaction: " + ex5.Message);
					}
				}
				response.Success = false;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = "Batch operation failed: " + ex4.Message;
				}
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

	public static async Task<BatchOperationResponse> GetPerspectiveHierarchies(string? connectionName, string perspectiveName, List<PerspectiveHierarchyDefinition> items)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "GetHierarchies",
			Results = new List<ItemResult>()
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No perspective hierarchies provided";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		List<PerspectiveHierarchyGet> dataList = new List<PerspectiveHierarchyGet>();
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					PerspectiveHierarchyDefinition perspectiveHierarchyDefinition = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = perspectiveHierarchyDefinition.TableName + "." + perspectiveHierarchyDefinition.HierarchyName
					};
					try
					{
						PerspectiveHierarchyGet perspectiveHierarchyInternal = GetPerspectiveHierarchyInternal(connectionInfo, perspectiveName, perspectiveHierarchyDefinition.TableName, perspectiveHierarchyDefinition.HierarchyName);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved hierarchy '" + perspectiveHierarchyDefinition.HierarchyName + "'";
						itemResult.Data = perspectiveHierarchyInternal;
						dataList.Add(perspectiveHierarchyInternal);
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving hierarchy: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
				}
				response.Success = failureCount == 0;
				response.Message = $"Retrieved {successCount} of {totalItems} hierarchies";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Batch operation failed: " + ex2.Message;
				failureCount = totalItems - successCount;
			}
			stopwatch.Stop();
			AuditEvent.Default.Emit("get perspective hierarchies", response.Success, OperationType.Read, connectionInfo);
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

	public static async Task<string> ExportTMDL(string? connectionName, string perspectiveName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw new ArgumentException("Perspective name cannot be null or empty", "perspectiveName");
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string item = ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(connectionInfo.Database.Model.Perspectives.Find(perspectiveName) ?? throw new ArgumentException("Perspective '" + perspectiveName + "' not found"), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
				AuditEvent.Default.Emit("export perspective to TMDL", success: true, OperationType.Read, connectionInfo);
				result = item;
			}
			catch
			{
				AuditEvent.Default.Emit("export perspective to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}
}
