using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class PartitionOperations
{
	private readonly record struct EntityPartitionSourceUpdatePlan(string? EntityName, string? SchemaName, DataSource? DataSource, NamedExpression? ExpressionSource, bool UpdatesSourceReference);

	private readonly record struct PartitionSourceIntent(string Kind, string Fields);

	private const string PowerBIQuerySourceDataSourceNotSupportedReason = "PowerBI does not support partitions with QueryPartitionSource that references a data source";

	private const string PowerBIEntitySourceDataSourceNotSupportedReason = "PowerBI does not support partitions with EntityPartitionSource referencing DataSourceName";

	private const string OpUpdateQuerySourceDataSourceName = "UPDATE Partition QueryPartitionSource DataSourceName";

	private const string OpUpdateEntitySourceDataSourceName = "UPDATE Partition EntityPartitionSource DataSourceName";

	private const string DataSourceNameRequiredForQueryOnNonQuerySource = "DataSourceName is required when Query is provided for a non-query partition source";

	internal static PostCommitDaxValidator.Target? ResolvePartitionForValidation(IConnectionInfo conn, PartitionDefinition def)
	{
		if (def == null || string.IsNullOrEmpty(def.TableName))
		{
			return null;
		}
		Database database = conn?.Database;
		if (database == null)
		{
			return null;
		}
		Table table = database.Model.Tables.Find(def.TableName);
		if (table == null)
		{
			return null;
		}
		Partition partition = null;
		if (!string.IsNullOrWhiteSpace(def.Name))
		{
			partition = table.Partitions.Find(def.Name);
		}
		else if (table.Partitions.Count == 1)
		{
			partition = table.Partitions[0];
		}
		if (partition == null)
		{
			return null;
		}
		if (!(partition.Source is CalculatedPartitionSource))
		{
			return null;
		}
		List<PostCommitDaxValidator.Check> checks = new List<PostCommitDaxValidator.Check>
		{
			new PostCommitDaxValidator.Check(string.Empty, partition.State.ToString(), partition.ErrorMessage)
		};
		return new PostCommitDaxValidator.Target("Calculated partition", $"'{partition.Name}' on table '{table.Name}'", checks);
	}

	private static string ResolvePartitionName(Table table, string? partitionName)
	{
		if (!string.IsNullOrWhiteSpace(partitionName))
		{
			return partitionName;
		}
		if (table.Partitions.Count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + table.Name + "' has no partitions", ErrorSource.User);
		}
		if (table.Partitions.Count == 1)
		{
			return table.Partitions[0].Name;
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage($"Partition name is required because table '{table.Name}' contains {table.Partitions.Count} partitions. Available partitions: {string.Join(", ", table.Partitions.Select((Partition p) => p.Name))}", ErrorSource.User);
	}

	public static async Task<List<PartitionGet>> ListPartitions(string? connectionName, string? tableName = null)
	{
		List<PartitionGet> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<PartitionGet> list = ListPartitionsInternal(connectionInfo.Database, tableName);
				AuditEvent.Default.Emit("list partitions", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list partitions", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<PartitionGet> ListPartitionsInternal(Database db, string? tableName = null)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		List<PartitionGet> list = new List<PartitionGet>();
		Table[] array = (string.IsNullOrWhiteSpace(tableName) ? db.Model.Tables.ToArray() : new Table[1] { db.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found") });
		foreach (Table table in array)
		{
			foreach (Partition partition in table.Partitions)
			{
				PartitionGet partitionGet = new PartitionGet
				{
					Name = partition.Name,
					TableName = table.Name,
					Description = partition.Description,
					ModifiedTime = partition.ModifiedTime,
					State = partition.State.ToString(),
					DataView = partition.DataView.ToString(),
					Mode = partition.Mode.ToString(),
					ErrorMessage = partition.ErrorMessage,
					SourceType = partition.SourceType.ToString(),
					QueryGroupName = partition.QueryGroup?.Name
				};
				ExtractSourceInformation(partition, partitionGet);
				foreach (Annotation annotation in partition.Annotations)
				{
					partitionGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
				}
				partitionGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromPartition(partition);
				list.Add(partitionGet);
			}
		}
		return list;
	}

	internal static PartitionGet GetPartitionInternal(Database db, string tableName, string? partitionName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		Table table = db.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		string text = ResolvePartitionName(table, partitionName);
		Partition partition = table.Partitions.Find(text) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Partition '{text}' not found in table '{tableName}'", ErrorSource.User);
		PartitionGet partitionGet = new PartitionGet
		{
			Name = partition.Name,
			TableName = table.Name,
			Description = partition.Description,
			ModifiedTime = partition.ModifiedTime,
			State = partition.State.ToString(),
			DataView = partition.DataView.ToString(),
			Mode = partition.Mode.ToString(),
			ErrorMessage = partition.ErrorMessage,
			SourceType = partition.SourceType.ToString(),
			QueryGroupName = partition.QueryGroup?.Name
		};
		ExtractSourceInformation(partition, partitionGet);
		foreach (Annotation annotation in partition.Annotations)
		{
			partitionGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		partitionGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromPartition(partition);
		return partitionGet;
	}

	internal static PartitionOperationResult CreatePartitionInternal(IConnectionInfo info, PartitionDefinition def, IWriteGuard writeGuard)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Partition definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Partition name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.SourceType))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("SourceType is required", ErrorSource.User);
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
		Database database = info.Database;
		Table table = database.Model.Tables.Find(def.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + def.TableName + "' not found", ErrorSource.User);
		if (table.Partitions.Contains(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Partition '{def.Name}' already exists in table '{def.TableName}'", ErrorSource.User);
		}
		PartitionSource source = CreatePartitionSource(def, database, writeGuard);
		Partition partition = new Partition
		{
			Name = def.Name,
			Source = source
		};
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			partition.Description = def.Description;
		}
		if (!string.IsNullOrWhiteSpace(def.Mode))
		{
			if (!Enum.TryParse<ModeType>(def.Mode, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(ModeType));
				throw new McpExceptionWithSource("Invalid mode '" + def.Mode + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid mode supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			partition.Mode = result;
		}
		else
		{
			partition.Mode = ModeType.Import;
		}
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				partition.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToPartition(partition, def.ExtendedProperties);
		}
		List<string> warnings = null;
		if (!string.IsNullOrWhiteSpace(def.QueryGroupName))
		{
			bool wasCreated;
			QueryGroup queryGroup = QueryGroupOperations.FindOrCreateQueryGroup(database, def.QueryGroupName, out wasCreated);
			if (wasCreated)
			{
				warnings = new List<string> { "Query group '" + def.QueryGroupName + "' was automatically created" };
			}
			partition.QueryGroup = queryGroup;
		}
		table.Partitions.Add(partition);
		TransactionOperations.RecordOperation(info, $"Created partition '{def.Name}' in table '{def.TableName}' in model {database.Model.Name}");
		ConnectionOperations.SaveChangesWithRollback(info, "create partition", OperationType.Create);
		return new PartitionOperationResult
		{
			State = partition.State.ToString(),
			ErrorMessage = partition.ErrorMessage,
			PartitionName = partition.Name,
			TableName = table.Name,
			Warnings = warnings
		};
	}

	internal static void DeletePartitionInternal(IConnectionInfo info, string tableName, string? partitionName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Table obj = database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		if (obj.Partitions.Count <= 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete the last partition in a table", ErrorSource.User);
		}
		string text = ResolvePartitionName(obj, partitionName);
		Partition metadataObject = obj.Partitions.Find(text) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Partition '{text}' not found in table '{tableName}'", ErrorSource.User);
		obj.Partitions.Remove(metadataObject);
		TransactionOperations.RecordOperation(info, $"Deleted partition '{text}' from table '{tableName}' in model {database.Model.Name}");
		ConnectionOperations.SaveChangesWithRollback(info, "delete partition", OperationType.Delete);
	}

	internal static void RefreshPartitionInternal(IConnectionInfo info, string tableName, string? partitionName, string? refreshType = "Automatic")
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Table obj = database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		string text = ResolvePartitionName(obj, partitionName);
		Partition obj2 = obj.Partitions.Find(text) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Partition '{text}' not found in table '{tableName}'", ErrorSource.User);
		if (!Enum.TryParse<RefreshType>(refreshType, ignoreCase: true, out var result))
		{
			string[] names = Enum.GetNames(typeof(RefreshType));
			throw new McpExceptionWithSource("Invalid refresh type '" + refreshType + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid refresh type supplied. Valid values are: " + string.Join(", ", names) + ".");
		}
		obj2.RequestRefresh(result);
		TransactionOperations.RecordOperation(info, $"Refreshed partition '{text}' in table '{tableName}' in model {database.Model.Name} with refresh type '{result}'");
		ConnectionOperations.SaveChangesWithRollback(info, "refresh partition", OperationType.Update);
	}

	internal static void RenamePartitionInternal(IConnectionInfo info, string tableName, string? partitionName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Table obj = database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		string text = ResolvePartitionName(obj, partitionName);
		Partition partition = obj.Partitions.Find(text) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Partition '{text}' not found in table '{tableName}'", ErrorSource.User);
		if (obj.Partitions.Contains(newName) && !string.Equals(text, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Partition '{newName}' already exists in table '{tableName}'", ErrorSource.User);
		}
		partition.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed partition '{text}' to '{newName}' in table '{tableName}' in model {database.Model.Name}");
		ConnectionOperations.SaveChangesWithRollback(info, "rename partition", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	internal static PartitionOperationResult UpdatePartitionInternal(IConnectionInfo info, PartitionDefinition update, IWriteGuard writeGuard)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Partition update definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(update.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TableName is required to identify the table containing the partition", ErrorSource.User);
		}
		Database database = info.Database;
		Table table = database.Model.Tables.Find(update.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + update.TableName + "' not found", ErrorSource.User);
		string partitionName = (string.IsNullOrWhiteSpace(update.Name) ? null : update.Name);
		string text = ResolvePartitionName(table, partitionName);
		Partition partition = table.Partitions.Find(text) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Partition '{text}' not found in table '{update.TableName}'", ErrorSource.User);
		ValidatePartitionUpdateBeforeMutation(partition, update, database, writeGuard);
		bool flag = false;
		List<string> list = null;
		if (update.Description != null)
		{
			string text2 = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (partition.Description != text2)
			{
				partition.Description = text2;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.Mode))
		{
			if (!Enum.TryParse<ModeType>(update.Mode, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(ModeType));
				throw new McpExceptionWithSource("Invalid mode '" + update.Mode + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid mode supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			if (partition.Mode != result)
			{
				partition.Mode = result;
				flag = true;
			}
		}
		if (UpdatePartitionSource(partition, update, database, writeGuard, out var expressionChanged))
		{
			flag = true;
			if (PartitionWarnings.IsMExpressionUpdate(partition.Source, expressionChanged))
			{
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add("The partition source was updated, but table column mappings were not. If source column names or data types changed, use column_operations in a follow-up call to update DataColumn.SourceColumn and, for DirectQuery tables, Column.SourceProviderType for each affected column.");
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(partition, update.Annotations, (Partition obj) => obj.Annotations))
		{
			flag = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num = partition.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplacePartitionProperties(partition, update.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		if (update.QueryGroupName != null)
		{
			QueryGroup queryGroup = null;
			if (!string.IsNullOrEmpty(update.QueryGroupName))
			{
				queryGroup = QueryGroupOperations.FindOrCreateQueryGroup(database, update.QueryGroupName, out var wasCreated);
				if (wasCreated)
				{
					if (list == null)
					{
						list = new List<string>();
					}
					list.Add("Query group '" + update.QueryGroupName + "' was automatically created");
				}
			}
			if (partition.QueryGroup != queryGroup)
			{
				partition.QueryGroup = queryGroup;
				flag = true;
			}
		}
		if (!flag)
		{
			return new PartitionOperationResult
			{
				State = partition.State.ToString(),
				ErrorMessage = partition.ErrorMessage,
				PartitionName = partition.Name,
				TableName = table.Name,
				HasChanges = false,
				Warnings = list
			};
		}
		TransactionOperations.RecordOperation(info, $"Updated partition '{text}' in table '{update.TableName}' in model {database.Model.Name}");
		CheckpointMode checkpointMode = (IsBrokenCalculatedPartitionState(partition, update) ? CheckpointMode.ForceEvenInTransaction : CheckpointMode.Default);
		ConnectionOperations.SaveChangesWithRollback(info, "update partition", OperationType.Update, checkpointMode);
		return new PartitionOperationResult
		{
			State = partition.State.ToString(),
			ErrorMessage = partition.ErrorMessage,
			PartitionName = partition.Name,
			TableName = table.Name,
			HasChanges = true,
			Warnings = list
		};
	}

	private static bool IsBrokenCalculatedPartitionState(Partition partition, PartitionDefinition update)
	{
		if (partition.Source is CalculatedPartitionSource && !string.IsNullOrWhiteSpace(update.Expression) && (string.IsNullOrWhiteSpace(update.SourceType) || string.Equals(update.SourceType, "calculated", StringComparison.OrdinalIgnoreCase)))
		{
			ObjectState state = partition.State;
			if (state != ObjectState.Ready && state != ObjectState.NoData)
			{
				return state != ObjectState.CalculationNeeded;
			}
			return false;
		}
		return false;
	}

	private static void ExtractSourceInformation(Partition partition, PartitionGet partitionGet)
	{
		PartitionSource source = partition.Source;
		if (!(source is CalculatedPartitionSource calculatedPartitionSource))
		{
			if (!(source is MPartitionSource mPartitionSource))
			{
				if (!(source is QueryPartitionSource queryPartitionSource))
				{
					if (!(source is PolicyRangePartitionSource policyRangePartitionSource))
					{
						if (!(source is EntityPartitionSource entityPartitionSource))
						{
							if (!(source is CalculationGroupSource))
							{
								_ = source is InferredPartitionSource;
							}
						}
						else
						{
							partitionGet.DataSourceName = entityPartitionSource.DataSource?.Name;
							partitionGet.EntityName = entityPartitionSource.EntityName;
							partitionGet.SchemaName = entityPartitionSource.SchemaName;
							partitionGet.ExpressionSourceName = entityPartitionSource.ExpressionSource?.Name;
						}
					}
					else
					{
						partitionGet.StartDateTime = policyRangePartitionSource.Start.ToString("yyyy-MM-dd HH:mm:ss");
						partitionGet.EndDateTime = policyRangePartitionSource.End.ToString("yyyy-MM-dd HH:mm:ss");
						partitionGet.Granularity = policyRangePartitionSource.Granularity.ToString();
						partitionGet.RefreshBookmark = policyRangePartitionSource.RefreshBookmark;
					}
				}
				else
				{
					partitionGet.Query = queryPartitionSource.Query;
					partitionGet.DataSourceName = queryPartitionSource.DataSource?.Name;
				}
			}
			else
			{
				partitionGet.Expression = mPartitionSource.Expression;
				partitionGet.Attributes = mPartitionSource.Attributes;
			}
		}
		else
		{
			partitionGet.Expression = calculatedPartitionSource.Expression;
			partitionGet.RetainDataTillForceCalculate = calculatedPartitionSource.RetainDataTillForceCalculate;
		}
	}

	private static PartitionSource CreatePartitionSource(PartitionDefinition def, Database db, IWriteGuard writeGuard)
	{
		switch (def.SourceType?.ToLower() ?? string.Empty)
		{
		case "calculated":
		{
			if (string.IsNullOrWhiteSpace(def.Expression))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression is required for CalculatedPartitionSource", ErrorSource.User);
			}
			CalculatedPartitionSource calculatedPartitionSource = new CalculatedPartitionSource
			{
				Expression = def.Expression
			};
			if (def.RetainDataTillForceCalculate.HasValue)
			{
				calculatedPartitionSource.RetainDataTillForceCalculate = def.RetainDataTillForceCalculate.Value;
			}
			return calculatedPartitionSource;
		}
		case "m":
		{
			if (string.IsNullOrWhiteSpace(def.Expression))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression is required for MPartitionSource", ErrorSource.User);
			}
			MPartitionSource mPartitionSource = new MPartitionSource
			{
				Expression = def.Expression
			};
			if (!string.IsNullOrWhiteSpace(def.Attributes))
			{
				mPartitionSource.Attributes = def.Attributes;
			}
			return mPartitionSource;
		}
		case "query":
		{
			if (string.IsNullOrWhiteSpace(def.Query))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Query is required for QueryPartitionSource", ErrorSource.User);
			}
			if (string.IsNullOrWhiteSpace(def.DataSourceName))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("DataSourceName is required for QueryPartitionSource", ErrorSource.User);
			}
			writeGuard.AssertFullModeRequired("CREATE Partition with QueryPartitionSource", "PowerBI does not support partitions with QueryPartitionSource that references a data source");
			DataSource dataSource2 = db.Model.DataSources.Find(def.DataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + def.DataSourceName + "' not found", ErrorSource.User);
			return new QueryPartitionSource
			{
				Query = def.Query,
				DataSource = dataSource2
			};
		}
		case "policyrange":
		{
			if (string.IsNullOrWhiteSpace(def.StartDateTime) || string.IsNullOrWhiteSpace(def.EndDateTime))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("StartDateTime and EndDateTime are required for PolicyRangePartitionSource", ErrorSource.User);
			}
			if (!DateTime.TryParseExact(def.StartDateTime, "yyyy-MM-dd HH:mm:ss", null, DateTimeStyles.None, out var result))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid StartDateTime format. Expected 'yyyy-MM-dd HH:mm:ss'", ErrorSource.User);
			}
			if (!DateTime.TryParseExact(def.EndDateTime, "yyyy-MM-dd HH:mm:ss", null, DateTimeStyles.None, out var result2))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid EndDateTime format. Expected 'yyyy-MM-dd HH:mm:ss'", ErrorSource.User);
			}
			PolicyRangePartitionSource policyRangePartitionSource = new PolicyRangePartitionSource
			{
				Start = result,
				End = result2
			};
			if (!string.IsNullOrWhiteSpace(def.Granularity))
			{
				if (!Enum.TryParse<RefreshGranularityType>(def.Granularity, ignoreCase: true, out var result3))
				{
					string[] names = Enum.GetNames(typeof(RefreshGranularityType));
					throw new McpExceptionWithSource("Invalid granularity '" + def.Granularity + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid granularity supplied. Valid values are: " + string.Join(", ", names) + ".");
				}
				policyRangePartitionSource.Granularity = result3;
			}
			return policyRangePartitionSource;
		}
		case "entity":
		{
			if (string.IsNullOrWhiteSpace(def.EntityName))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("EntityName is required for EntityPartitionSource", ErrorSource.User);
			}
			if (string.IsNullOrWhiteSpace(def.DataSourceName) && string.IsNullOrWhiteSpace(def.ExpressionSourceName))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Either ExpressionSourceName or DataSourceName is required for EntityPartitionSource", ErrorSource.User);
			}
			if (!string.IsNullOrWhiteSpace(def.DataSourceName) && !string.IsNullOrWhiteSpace(def.ExpressionSourceName))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one of ExpressionSourceName or DataSourceName can be provided for EntityPartitionSource", ErrorSource.User);
			}
			EntityPartitionSource entityPartitionSource = new EntityPartitionSource
			{
				EntityName = def.EntityName
			};
			if (!string.IsNullOrWhiteSpace(def.SchemaName))
			{
				entityPartitionSource.SchemaName = def.SchemaName;
			}
			if (!string.IsNullOrWhiteSpace(def.ExpressionSourceName))
			{
				NamedExpression expressionSource = db.Model.Expressions.Find(def.ExpressionSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression source '" + def.ExpressionSourceName + "' not found", ErrorSource.User);
				entityPartitionSource.ExpressionSource = expressionSource;
			}
			else if (!string.IsNullOrWhiteSpace(def.DataSourceName))
			{
				writeGuard.AssertFullModeRequired("CREATE Partition with EntityPartitionSource", "PowerBI does not support partitions with EntityPartitionSource referencing DataSourceName");
				DataSource dataSource = db.Model.DataSources.Find(def.DataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + def.DataSourceName + "' not found", ErrorSource.User);
				entityPartitionSource.DataSource = dataSource;
			}
			return entityPartitionSource;
		}
		default:
			throw new McpExceptionWithSource("Unsupported SourceType '" + def.SourceType + "'. Valid values are: Calculated, M, Query, PolicyRange, Entity", ErrorSource.User, "Unsupported SourceType supplied. Valid values are: Calculated, M, Query, PolicyRange, Entity.");
		}
	}

	internal static bool UpdatePartitionSource(Partition partition, PartitionDefinition update, Database db, IWriteGuard writeGuard)
	{
		bool expressionChanged;
		return UpdatePartitionSource(partition, update, db, writeGuard, out expressionChanged);
	}

	internal static bool UpdatePartitionSource(Partition partition, PartitionDefinition update, Database db, IWriteGuard writeGuard, out bool expressionChanged)
	{
		expressionChanged = false;
		ValidateRequestedSourceType(partition, update);
		string text = NormalizePartitionSourceKind(update.SourceType);
		string text2 = InferRequestedPartitionSourceKind(partition, update);
		int num = new bool[5]
		{
			!string.IsNullOrWhiteSpace(update.Expression) && text == "calculated",
			!string.IsNullOrWhiteSpace(update.Expression) && text == "m",
			!string.IsNullOrWhiteSpace(update.Query) && !string.IsNullOrWhiteSpace(update.DataSourceName) && text2 == "query",
			!string.IsNullOrWhiteSpace(update.StartDateTime) && !string.IsNullOrWhiteSpace(update.EndDateTime) && text2 == "policyrange",
			!string.IsNullOrWhiteSpace(update.EntityName) && (text == "entity" || !string.IsNullOrWhiteSpace(update.DataSourceName) || !string.IsNullOrWhiteSpace(update.ExpressionSourceName)) && text2 == "entity"
		}.Count((bool x) => x);
		if (num > 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one complete source replacement can be provided at a time", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(update.Query) && string.IsNullOrWhiteSpace(update.DataSourceName) && !(partition.Source is QueryPartitionSource))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("DataSourceName is required when Query is provided for a non-query partition source", ErrorSource.User);
		}
		bool result = false;
		if (num == 1)
		{
			PartitionSource partitionSource = null;
			if (!string.IsNullOrWhiteSpace(update.Expression) && text == "calculated")
			{
				CalculatedPartitionSource calculatedPartitionSource = partition.Source as CalculatedPartitionSource;
				CalculatedPartitionSource calculatedPartitionSource2 = new CalculatedPartitionSource
				{
					Expression = update.Expression
				};
				if (update.RetainDataTillForceCalculate.HasValue)
				{
					calculatedPartitionSource2.RetainDataTillForceCalculate = update.RetainDataTillForceCalculate.Value;
				}
				else if (calculatedPartitionSource != null)
				{
					calculatedPartitionSource2.RetainDataTillForceCalculate = calculatedPartitionSource.RetainDataTillForceCalculate;
				}
				partitionSource = calculatedPartitionSource2;
				expressionChanged = calculatedPartitionSource == null || calculatedPartitionSource.Expression != update.Expression;
			}
			else if (!string.IsNullOrWhiteSpace(update.Expression) && text == "m")
			{
				MPartitionSource mPartitionSource = partition.Source as MPartitionSource;
				MPartitionSource mPartitionSource2 = new MPartitionSource
				{
					Expression = update.Expression
				};
				if (!string.IsNullOrWhiteSpace(update.Attributes))
				{
					mPartitionSource2.Attributes = update.Attributes;
				}
				else if (mPartitionSource != null)
				{
					mPartitionSource2.Attributes = mPartitionSource.Attributes;
				}
				partitionSource = mPartitionSource2;
				expressionChanged = mPartitionSource == null || mPartitionSource.Expression != update.Expression;
			}
			else if (!string.IsNullOrWhiteSpace(update.Query) && text2 == "query")
			{
				writeGuard.AssertFullModeRequired("UPDATE Partition to QueryPartitionSource", "PowerBI does not support partitions with QueryPartitionSource that references a data source");
				DataSource dataSource = db.Model.DataSources.Find(update.DataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + update.DataSourceName + "' not found", ErrorSource.User);
				partitionSource = new QueryPartitionSource
				{
					Query = update.Query,
					DataSource = dataSource
				};
			}
			else if (!string.IsNullOrWhiteSpace(update.StartDateTime) && !string.IsNullOrWhiteSpace(update.EndDateTime) && text2 == "policyrange")
			{
				if (!DateTime.TryParseExact(update.StartDateTime, "yyyy-MM-dd HH:mm:ss", null, DateTimeStyles.None, out var result2))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid StartDateTime format. Expected 'yyyy-MM-dd HH:mm:ss'", ErrorSource.User);
				}
				if (!DateTime.TryParseExact(update.EndDateTime, "yyyy-MM-dd HH:mm:ss", null, DateTimeStyles.None, out var result3))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid EndDateTime format. Expected 'yyyy-MM-dd HH:mm:ss'", ErrorSource.User);
				}
				PolicyRangePartitionSource policyRangePartitionSource = new PolicyRangePartitionSource
				{
					Start = result2,
					End = result3
				};
				if (!string.IsNullOrWhiteSpace(update.Granularity))
				{
					if (!Enum.TryParse<RefreshGranularityType>(update.Granularity, ignoreCase: true, out var result4))
					{
						throw new McpExceptionWithSource("Invalid granularity '" + update.Granularity + "'", ErrorSource.User, "Invalid granularity supplied.");
					}
					policyRangePartitionSource.Granularity = result4;
				}
				partitionSource = policyRangePartitionSource;
			}
			else if (!string.IsNullOrWhiteSpace(update.EntityName) && text2 == "entity")
			{
				if (partition.Source is EntityPartitionSource entity)
				{
					return ApplyEntityPartitionSourceUpdate(entity, update, db, writeGuard);
				}
				if (string.IsNullOrWhiteSpace(update.DataSourceName) && string.IsNullOrWhiteSpace(update.ExpressionSourceName))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Either ExpressionSourceName or DataSourceName is required when EntityName is provided", ErrorSource.User);
				}
				if (!string.IsNullOrWhiteSpace(update.DataSourceName) && !string.IsNullOrWhiteSpace(update.ExpressionSourceName))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one of ExpressionSourceName or DataSourceName can be provided when EntityName is specified", ErrorSource.User);
				}
				EntityPartitionSource entityPartitionSource = new EntityPartitionSource
				{
					EntityName = update.EntityName
				};
				EntityPartitionSource entityPartitionSource2 = partition.Source as EntityPartitionSource;
				entityPartitionSource.SchemaName = ((update.SchemaName != null) ? (string.IsNullOrWhiteSpace(update.SchemaName) ? null : update.SchemaName) : entityPartitionSource2?.SchemaName);
				if (!string.IsNullOrWhiteSpace(update.ExpressionSourceName))
				{
					NamedExpression expressionSource = db.Model.Expressions.Find(update.ExpressionSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression source '" + update.ExpressionSourceName + "' not found", ErrorSource.User);
					entityPartitionSource.DataSource = null;
					entityPartitionSource.ExpressionSource = expressionSource;
				}
				else if (!string.IsNullOrWhiteSpace(update.DataSourceName))
				{
					writeGuard.AssertFullModeRequired("UPDATE Partition to EntityPartitionSource", "PowerBI does not support partitions with EntityPartitionSource referencing DataSourceName");
					DataSource dataSource2 = db.Model.DataSources.Find(update.DataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + update.DataSourceName + "' not found", ErrorSource.User);
					entityPartitionSource.DataSource = dataSource2;
					entityPartitionSource.ExpressionSource = null;
				}
				partitionSource = entityPartitionSource;
			}
			if (ComparePartitionSources(partition.Source, partitionSource))
			{
				if (partitionSource is EntityPartitionSource { DataSource: var dataSource3, ExpressionSource: var expressionSource2 })
				{
					partition.Source = partitionSource;
					SetEntityPartitionSourceReference((EntityPartitionSource)partition.Source, dataSource3, expressionSource2);
					return true;
				}
				partition.Source = partitionSource;
				result = true;
			}
			else
			{
				expressionChanged = false;
			}
		}
		else
		{
			result = UpdateExistingPartitionSource(partition, update, db, writeGuard, out expressionChanged);
		}
		return result;
	}

	private static void ValidateRequestedSourceType(Partition partition, PartitionDefinition update)
	{
		string text = InferRequestedPartitionSourceKind(partition, update);
		if (text != null)
		{
			string partitionSourceKind = GetPartitionSourceKind(partition.Source);
			if (!string.Equals(partitionSourceKind, text, StringComparison.OrdinalIgnoreCase))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Cannot change partition source kind: current source kind '{partitionSourceKind}', requested source kind '{text}'. Delete and recreate the partition to change source kinds.", ErrorSource.User);
			}
		}
	}

	private static void ValidateDataSourceUpdateCompatibility(Partition partition, PartitionDefinition update, IWriteGuard writeGuard)
	{
		if (!string.IsNullOrWhiteSpace(update.DataSourceName))
		{
			string a = InferRequestedPartitionSourceKind(partition, update) ?? GetPartitionSourceKind(partition.Source);
			if (string.Equals(a, "query", StringComparison.OrdinalIgnoreCase) && (partition.Source is QueryPartitionSource || !string.IsNullOrWhiteSpace(update.Query)))
			{
				writeGuard.AssertFullModeRequired("UPDATE Partition QueryPartitionSource DataSourceName", "PowerBI does not support partitions with QueryPartitionSource that references a data source");
			}
			else if (string.Equals(a, "entity", StringComparison.OrdinalIgnoreCase) && (partition.Source is EntityPartitionSource || !string.IsNullOrWhiteSpace(update.EntityName)))
			{
				writeGuard.AssertFullModeRequired("UPDATE Partition EntityPartitionSource DataSourceName", "PowerBI does not support partitions with EntityPartitionSource referencing DataSourceName");
			}
		}
	}

	private static void ValidatePartitionUpdateBeforeMutation(Partition partition, PartitionDefinition update, Database db, IWriteGuard writeGuard)
	{
		ValidateRequestedSourceType(partition, update);
		ValidateDataSourceUpdateCompatibility(partition, update, writeGuard);
		ValidateQueryPartitionSourceUpdatePlan(partition, update, db);
		ValidatePolicyRangePartitionSourceUpdatePlan(partition, update);
		ValidateEntityPartitionSourceUpdateReferences(partition, update, db, writeGuard);
	}

	private static void ValidateQueryPartitionSourceUpdatePlan(Partition partition, PartitionDefinition update, Database db)
	{
		if (string.Equals(InferRequestedPartitionSourceKind(partition, update) ?? GetPartitionSourceKind(partition.Source), "query", StringComparison.OrdinalIgnoreCase))
		{
			if (!string.IsNullOrWhiteSpace(update.Query) && string.IsNullOrWhiteSpace(update.DataSourceName) && !(partition.Source is QueryPartitionSource))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("DataSourceName is required when Query is provided for a non-query partition source", ErrorSource.User);
			}
			if (!string.IsNullOrWhiteSpace(update.DataSourceName) && (partition.Source is QueryPartitionSource || !string.IsNullOrWhiteSpace(update.Query)) && db.Model.DataSources.Find(update.DataSourceName) == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + update.DataSourceName + "' not found", ErrorSource.User);
			}
		}
	}

	private static void ValidatePolicyRangePartitionSourceUpdatePlan(Partition partition, PartitionDefinition update)
	{
		if ((string.IsNullOrWhiteSpace(update.StartDateTime) && string.IsNullOrWhiteSpace(update.EndDateTime) && string.IsNullOrWhiteSpace(update.Granularity)) || !string.Equals(InferRequestedPartitionSourceKind(partition, update) ?? GetPartitionSourceKind(partition.Source), "policyrange", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		RefreshGranularityType result2;
		if (!string.IsNullOrWhiteSpace(update.StartDateTime) && !string.IsNullOrWhiteSpace(update.EndDateTime))
		{
			if (!DateTime.TryParseExact(update.StartDateTime, "yyyy-MM-dd HH:mm:ss", null, DateTimeStyles.None, out var result))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid StartDateTime format. Expected 'yyyy-MM-dd HH:mm:ss'", ErrorSource.User);
			}
			if (!DateTime.TryParseExact(update.EndDateTime, "yyyy-MM-dd HH:mm:ss", null, DateTimeStyles.None, out result))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid EndDateTime format. Expected 'yyyy-MM-dd HH:mm:ss'", ErrorSource.User);
			}
			if (!string.IsNullOrWhiteSpace(update.Granularity) && !Enum.TryParse<RefreshGranularityType>(update.Granularity, ignoreCase: true, out result2))
			{
				throw new McpExceptionWithSource("Invalid granularity '" + update.Granularity + "'", ErrorSource.User, "Invalid granularity supplied.");
			}
		}
		else if (!string.IsNullOrWhiteSpace(update.Granularity) && !Enum.TryParse<RefreshGranularityType>(update.Granularity, ignoreCase: true, out result2))
		{
			string[] names = Enum.GetNames(typeof(RefreshGranularityType));
			throw new McpExceptionWithSource("Invalid granularity '" + update.Granularity + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid granularity supplied. Valid values are: " + string.Join(", ", names) + ".");
		}
	}

	private static void ValidateEntityPartitionSourceUpdateReferences(Partition partition, PartitionDefinition update, Database db, IWriteGuard writeGuard)
	{
		if (partition.Source is EntityPartitionSource entity)
		{
			BuildEntityPartitionSourceUpdatePlan(entity, update, db, writeGuard);
		}
	}

	private static bool ApplyEntityPartitionSourceUpdate(EntityPartitionSource entity, PartitionDefinition update, Database db, IWriteGuard writeGuard)
	{
		EntityPartitionSourceUpdatePlan? entityPartitionSourceUpdatePlan = BuildEntityPartitionSourceUpdatePlan(entity, update, db, writeGuard);
		if (!entityPartitionSourceUpdatePlan.HasValue || !EntityPartitionSourceDiffers(entity, entityPartitionSourceUpdatePlan.Value))
		{
			return false;
		}
		if (entity.EntityName != entityPartitionSourceUpdatePlan.Value.EntityName)
		{
			entity.EntityName = entityPartitionSourceUpdatePlan.Value.EntityName;
		}
		if (entity.SchemaName != entityPartitionSourceUpdatePlan.Value.SchemaName)
		{
			entity.SchemaName = entityPartitionSourceUpdatePlan.Value.SchemaName;
		}
		if (entityPartitionSourceUpdatePlan.Value.UpdatesSourceReference)
		{
			SetEntityPartitionSourceReference(entity, entityPartitionSourceUpdatePlan.Value.DataSource, entityPartitionSourceUpdatePlan.Value.ExpressionSource);
		}
		return true;
	}

	private static EntityPartitionSourceUpdatePlan? BuildEntityPartitionSourceUpdatePlan(EntityPartitionSource entity, PartitionDefinition update, Database db, IWriteGuard writeGuard)
	{
		if (!HasEntityPartitionSourceUpdate(update))
		{
			return null;
		}
		if (!string.IsNullOrWhiteSpace(update.DataSourceName) && !string.IsNullOrWhiteSpace(update.ExpressionSourceName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one of ExpressionSourceName or DataSourceName can be provided when EntityName is specified", ErrorSource.User);
		}
		string entityName = ((!string.IsNullOrWhiteSpace(update.EntityName)) ? update.EntityName : entity.EntityName);
		string schemaName = ((update.SchemaName == null) ? entity.SchemaName : (string.IsNullOrWhiteSpace(update.SchemaName) ? null : update.SchemaName));
		DataSource dataSource = entity.DataSource;
		NamedExpression expressionSource = entity.ExpressionSource;
		bool updatesSourceReference = false;
		if (!string.IsNullOrWhiteSpace(update.DataSourceName))
		{
			writeGuard.AssertFullModeRequired("UPDATE Partition EntityPartitionSource DataSourceName", "PowerBI does not support partitions with EntityPartitionSource referencing DataSourceName");
			dataSource = db.Model.DataSources.Find(update.DataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + update.DataSourceName + "' not found", ErrorSource.User);
			expressionSource = null;
			updatesSourceReference = true;
		}
		else if (!string.IsNullOrWhiteSpace(update.ExpressionSourceName))
		{
			expressionSource = db.Model.Expressions.Find(update.ExpressionSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression source '" + update.ExpressionSourceName + "' not found", ErrorSource.User);
			dataSource = null;
			updatesSourceReference = true;
		}
		return new EntityPartitionSourceUpdatePlan(entityName, schemaName, dataSource, expressionSource, updatesSourceReference);
	}

	private static bool HasEntityPartitionSourceUpdate(PartitionDefinition update)
	{
		if (string.IsNullOrWhiteSpace(update.EntityName) && update.SchemaName == null && string.IsNullOrWhiteSpace(update.DataSourceName))
		{
			return !string.IsNullOrWhiteSpace(update.ExpressionSourceName);
		}
		return true;
	}

	private static bool EntityPartitionSourceDiffers(EntityPartitionSource entity, EntityPartitionSourceUpdatePlan plan)
	{
		if (!(entity.EntityName != plan.EntityName) && !(entity.SchemaName != plan.SchemaName) && entity.DataSource == plan.DataSource)
		{
			return entity.ExpressionSource != plan.ExpressionSource;
		}
		return true;
	}

	private static void SetEntityPartitionSourceReference(EntityPartitionSource entity, DataSource? dataSource, NamedExpression? expressionSource)
	{
		if (dataSource != null && expressionSource != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one of ExpressionSourceName or DataSourceName can be provided when EntityName is specified", ErrorSource.User);
		}
		if (dataSource != null)
		{
			if (entity.DataSource != dataSource)
			{
				entity.DataSource = dataSource;
			}
			if (entity.ExpressionSource != null)
			{
				entity.ExpressionSource = null;
			}
		}
		else if (expressionSource != null)
		{
			if (entity.ExpressionSource != expressionSource)
			{
				entity.ExpressionSource = expressionSource;
			}
			if (entity.DataSource != null)
			{
				entity.DataSource = null;
			}
		}
	}

	private static string? InferRequestedPartitionSourceKind(Partition partition, PartitionDefinition update)
	{
		string text = NormalizePartitionSourceKind(update.SourceType);
		if (!string.IsNullOrWhiteSpace(update.SourceType) && text == null)
		{
			throw new McpExceptionWithSource("Unsupported SourceType '" + update.SourceType + "'. Valid values are: Calculated, M, Query, PolicyRange, Entity", ErrorSource.User, "Unsupported SourceType supplied. Valid values are: Calculated, M, Query, PolicyRange, Entity.");
		}
		List<PartitionSourceIntent> populatedPartitionSourceIntents = GetPopulatedPartitionSourceIntents(update, text);
		List<string> list = populatedPartitionSourceIntents.Select((PartitionSourceIntent intent) => intent.Kind).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
		if (list.Count > 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one partition source kind can be provided at a time. Provided source fields request: " + string.Join(", ", list) + ".", ErrorSource.User);
		}
		PartitionSourceIntent? partitionSourceIntent = ((populatedPartitionSourceIntents.Count == 0) ? ((PartitionSourceIntent?)null) : new PartitionSourceIntent?(populatedPartitionSourceIntents[0]));
		if (text != null && partitionSourceIntent.HasValue && !string.Equals(text, partitionSourceIntent.Value.Kind, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"SourceType '{text}' conflicts with provided {partitionSourceIntent.Value.Fields} fields requesting source kind '{partitionSourceIntent.Value.Kind}'. " + "SourceType must match the populated source-specific fields.", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(update.Expression))
		{
			switch (text)
			{
			default:
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression can only be provided when SourceType is Calculated or M.", ErrorSource.User);
			case null:
			case "calculated":
			case "m":
				break;
			}
		}
		if (partitionSourceIntent.HasValue)
		{
			return partitionSourceIntent.Value.Kind;
		}
		bool flag = text == null && !string.IsNullOrWhiteSpace(update.Expression);
		if (flag)
		{
			PartitionSource source = partition.Source;
			bool flag2 = ((source is CalculatedPartitionSource || source is MPartitionSource) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return GetPartitionSourceKind(partition.Source);
		}
		return text;
	}

	private static List<PartitionSourceIntent> GetPopulatedPartitionSourceIntents(PartitionDefinition update, string? explicitSourceKind)
	{
		List<PartitionSourceIntent> list = new List<PartitionSourceIntent>();
		if (!string.IsNullOrWhiteSpace(update.EntityName))
		{
			list.Add(new PartitionSourceIntent("entity", "EntityName"));
		}
		if (!string.IsNullOrWhiteSpace(update.Query))
		{
			list.Add(new PartitionSourceIntent("query", "Query"));
		}
		if (!string.IsNullOrWhiteSpace(update.StartDateTime) || !string.IsNullOrWhiteSpace(update.EndDateTime))
		{
			list.Add(new PartitionSourceIntent("policyrange", "StartDateTime/EndDateTime"));
		}
		bool flag = !string.IsNullOrWhiteSpace(update.Expression);
		if (flag)
		{
			bool flag2 = ((explicitSourceKind == "calculated" || explicitSourceKind == "m") ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			list.Add(new PartitionSourceIntent(explicitSourceKind, "Expression"));
		}
		return list;
	}

	private static string? NormalizePartitionSourceKind(string? sourceType)
	{
		if (string.IsNullOrWhiteSpace(sourceType))
		{
			return null;
		}
		return sourceType.Trim().ToLowerInvariant() switch
		{
			"calculated" => "calculated", 
			"m" => "m", 
			"query" => "query", 
			"policyrange" => "policyrange", 
			"policy-range" => "policyrange", 
			"entity" => "entity", 
			_ => null, 
		};
	}

	private static string GetPartitionSourceKind(PartitionSource source)
	{
		if (!(source is CalculatedPartitionSource))
		{
			if (!(source is MPartitionSource))
			{
				if (!(source is QueryPartitionSource))
				{
					if (!(source is PolicyRangePartitionSource))
					{
						if (!(source is EntityPartitionSource))
						{
							if (!(source is CalculationGroupSource))
							{
								if (source is InferredPartitionSource)
								{
									return "inferred";
								}
								return source.GetType().Name;
							}
							return "calculationgroup";
						}
						return "entity";
					}
					return "policyrange";
				}
				return "query";
			}
			return "m";
		}
		return "calculated";
	}

	private static bool ComparePartitionSources(PartitionSource currentSource, PartitionSource? newSource)
	{
		if (newSource == null)
		{
			return false;
		}
		if (!(currentSource is CalculatedPartitionSource calculatedPartitionSource))
		{
			if (!(currentSource is MPartitionSource mPartitionSource))
			{
				if (!(currentSource is QueryPartitionSource queryPartitionSource))
				{
					if (!(currentSource is PolicyRangePartitionSource policyRangePartitionSource))
					{
						if (currentSource is EntityPartitionSource entityPartitionSource && newSource is EntityPartitionSource entityPartitionSource2)
						{
							if (!(entityPartitionSource.EntityName != entityPartitionSource2.EntityName) && !(entityPartitionSource.SchemaName != entityPartitionSource2.SchemaName) && entityPartitionSource.DataSource == entityPartitionSource2.DataSource)
							{
								return entityPartitionSource.ExpressionSource != entityPartitionSource2.ExpressionSource;
							}
							return true;
						}
					}
					else if (newSource is PolicyRangePartitionSource policyRangePartitionSource2)
					{
						if (!(policyRangePartitionSource.Start != policyRangePartitionSource2.Start) && !(policyRangePartitionSource.End != policyRangePartitionSource2.End) && policyRangePartitionSource.Granularity == policyRangePartitionSource2.Granularity)
						{
							return policyRangePartitionSource.RefreshBookmark != policyRangePartitionSource2.RefreshBookmark;
						}
						return true;
					}
				}
				else if (newSource is QueryPartitionSource queryPartitionSource2)
				{
					if (!(queryPartitionSource.Query != queryPartitionSource2.Query))
					{
						return queryPartitionSource.DataSource != queryPartitionSource2.DataSource;
					}
					return true;
				}
			}
			else if (newSource is MPartitionSource mPartitionSource2)
			{
				if (!(mPartitionSource.Expression != mPartitionSource2.Expression))
				{
					return mPartitionSource.Attributes != mPartitionSource2.Attributes;
				}
				return true;
			}
		}
		else if (newSource is CalculatedPartitionSource calculatedPartitionSource2)
		{
			if (!(calculatedPartitionSource.Expression != calculatedPartitionSource2.Expression))
			{
				return calculatedPartitionSource.RetainDataTillForceCalculate != calculatedPartitionSource2.RetainDataTillForceCalculate;
			}
			return true;
		}
		return true;
	}

	private static bool UpdateExistingPartitionSource(Partition partition, PartitionDefinition update, Database db, IWriteGuard writeGuard, out bool expressionChanged)
	{
		bool result = false;
		expressionChanged = false;
		PartitionSource source = partition.Source;
		if (!(source is CalculatedPartitionSource calculatedPartitionSource))
		{
			if (!(source is MPartitionSource mPartitionSource))
			{
				if (!(source is QueryPartitionSource queryPartitionSource))
				{
					if (!(source is PolicyRangePartitionSource policyRangePartitionSource))
					{
						if (source is EntityPartitionSource entity)
						{
							result = ApplyEntityPartitionSourceUpdate(entity, update, db, writeGuard);
						}
					}
					else if (!string.IsNullOrWhiteSpace(update.Granularity))
					{
						if (!Enum.TryParse<RefreshGranularityType>(update.Granularity, ignoreCase: true, out var result2))
						{
							string[] names = Enum.GetNames(typeof(RefreshGranularityType));
							throw new McpExceptionWithSource("Invalid granularity '" + update.Granularity + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid granularity supplied. Valid values are: " + string.Join(", ", names) + ".");
						}
						if (policyRangePartitionSource.Granularity != result2)
						{
							policyRangePartitionSource.Granularity = result2;
							result = true;
						}
					}
				}
				else
				{
					if (!string.IsNullOrWhiteSpace(update.DataSourceName))
					{
						writeGuard.AssertFullModeRequired("UPDATE Partition QueryPartitionSource DataSourceName", "PowerBI does not support partitions with QueryPartitionSource that references a data source");
					}
					if (!string.IsNullOrWhiteSpace(update.Query) && queryPartitionSource.Query != update.Query)
					{
						queryPartitionSource.Query = update.Query;
						result = true;
					}
					if (!string.IsNullOrWhiteSpace(update.DataSourceName))
					{
						DataSource dataSource = db.Model.DataSources.Find(update.DataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + update.DataSourceName + "' not found", ErrorSource.User);
						if (queryPartitionSource.DataSource != dataSource)
						{
							queryPartitionSource.DataSource = dataSource;
							result = true;
						}
					}
				}
			}
			else
			{
				if (!string.IsNullOrWhiteSpace(update.Expression) && (string.IsNullOrWhiteSpace(update.SourceType) || update.SourceType.ToLower() == "m") && mPartitionSource.Expression != update.Expression)
				{
					mPartitionSource.Expression = update.Expression;
					result = true;
					expressionChanged = true;
				}
				if (!string.IsNullOrWhiteSpace(update.Attributes) && mPartitionSource.Attributes != update.Attributes)
				{
					mPartitionSource.Attributes = update.Attributes;
					result = true;
				}
			}
		}
		else
		{
			if (!string.IsNullOrWhiteSpace(update.Expression) && (string.IsNullOrWhiteSpace(update.SourceType) || update.SourceType.ToLower() == "calculated") && calculatedPartitionSource.Expression != update.Expression)
			{
				calculatedPartitionSource.Expression = update.Expression;
				result = true;
				expressionChanged = true;
			}
			if (update.RetainDataTillForceCalculate.HasValue && calculatedPartitionSource.RetainDataTillForceCalculate != update.RetainDataTillForceCalculate.Value)
			{
				calculatedPartitionSource.RetainDataTillForceCalculate = update.RetainDataTillForceCalculate.Value;
				result = true;
			}
		}
		return result;
	}

	public static async Task<TmdlExportResult> ExportTMDL(string? connectionName, string tableName, string? partitionName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentException("Table name cannot be null or empty", "tableName");
		}
		TmdlExportResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				Table obj = connectionInfo.Database.Model.Tables.Find(tableName) ?? throw new ArgumentException("Table '" + tableName + "' not found");
				string text = ResolvePartitionName(obj, partitionName);
				string content = TmdlSerializer.SerializeObject(obj.Partitions.Find(text) ?? throw new ArgumentException($"Partition '{text}' not found in table '{tableName}'"), options.SerializationOptions.ToMetadataSerializationOptions());
				(string Content, bool IsTruncated, string? SavedFilePath, List<string> Warnings) tuple = ExportContentProcessor.ProcessExportContent(content, options);
				string item = tuple.Content;
				bool item2 = tuple.IsTruncated;
				string item3 = tuple.SavedFilePath;
				List<string> item4 = tuple.Warnings;
				TmdlExportResult tmdlExportResult = TmdlExportResult.CreateSuccess(tableName + "." + text, "Partition", content, item, item2, item3, item4, options);
				AuditEvent.Default.Emit("export partition to TMDL", success: true, OperationType.Read, connectionInfo);
				result = tmdlExportResult;
			}
			catch (Exception ex)
			{
				AuditEvent.Default.Emit("export partition to TMDL", success: false, OperationType.Read, connectionInfo);
				result = TmdlExportResult.CreateFailure(tableName + "." + (partitionName ?? "(unknown)"), "Partition", ex.Message, (ex is ArgumentException) ? ErrorSource.User : ErrorSource.System);
			}
		}
		return result;
	}

	public static async Task<TmslExportResult> ExportTMSL(string? connectionName, string tableName, string? partitionName, ExportTmsl tmslOptions)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentException("Table name cannot be null or empty", "tableName");
		}
		if (tmslOptions == null)
		{
			throw new ArgumentNullException("tmslOptions");
		}
		if (string.IsNullOrWhiteSpace(tmslOptions.TmslOperationType))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TmslOperationType is required in tmslOptions", ErrorSource.User);
		}
		TmslExportResult result3;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				Table obj = connectionInfo.Database.Model.Tables.Find(tableName) ?? throw new ArgumentException("Table '" + tableName + "' not found");
				string text = ResolvePartitionName(obj, partitionName);
				Partition metadataObject = obj.Partitions.Find(text) ?? throw new ArgumentException($"Partition '{text}' not found in table '{tableName}'");
				if (!Enum.TryParse<TmslOperationType>(tmslOptions.TmslOperationType, ignoreCase: true, out var result))
				{
					string[] names = Enum.GetNames<TmslOperationType>();
					throw new McpExceptionWithSource("Invalid TmslOperationType '" + tmslOptions.TmslOperationType + "'. Valid values: " + string.Join(", ", names), ErrorSource.User, "Invalid TmslOperationType supplied. Valid values: " + string.Join(", ", names) + ".");
				}
				TmslOperationRequest tmslOperationRequest = new TmslOperationRequest
				{
					OperationType = result,
					IncludeRestricted = (tmslOptions.IncludeRestricted == true)
				};
				if (!string.IsNullOrWhiteSpace(tmslOptions.RefreshType))
				{
					if (!Enum.TryParse<RefreshType>(tmslOptions.RefreshType, ignoreCase: true, out var result2))
					{
						string[] names2 = Enum.GetNames<RefreshType>();
						throw new McpExceptionWithSource("Invalid RefreshType '" + tmslOptions.RefreshType + "'. Valid values: " + string.Join(", ", names2), ErrorSource.User, "Invalid RefreshType supplied. Valid values: " + string.Join(", ", names2) + ".");
					}
					tmslOperationRequest.RefreshType = result2;
				}
				TmslExportResult tmslExportResult = TmslExportResult.FromLegacyResult(TmslScriptingService.GenerateScript(metadataObject, result, tmslOperationRequest));
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
				AuditEvent.Default.Emit("export partition to TMSL", success: true, OperationType.Read, connectionInfo);
				result3 = tmslExportResult;
			}
			catch (Exception ex)
			{
				AuditEvent.Default.Emit("export partition to TMSL", success: false, OperationType.Read, connectionInfo);
				result3 = TmslExportResult.CreateFailure(tableName + "." + (partitionName ?? "(unknown)"), "Partition", tmslOptions.TmslOperationType, ex.Message, (ex is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : ((ex is ArgumentException) ? ErrorSource.User : ErrorSource.System));
			}
		}
		return result3;
	}

	public static async Task<(List<TablePartitionList> Tables, int TotalCount)> ListPartitionsGrouped(string? connectionName, string? tableName, int? maxResults)
	{
		(List<TablePartitionList> Tables, int TotalCount) result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			Database database = connectionInfo.Database;
			IEnumerable<Table> source;
			if (!string.IsNullOrWhiteSpace(tableName))
			{
				Table table = database.Model.Tables.Find(tableName);
				if (table == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
				}
				source = new Table[1] { table };
			}
			else
			{
				source = database.Model.Tables;
			}
			List<TablePartitionList> list = (from t in source
				select new TablePartitionList
				{
					TableName = t.Name,
					Partitions = t.Partitions.Select((Partition p) => new PartitionList
					{
						Name = p.Name,
						TableName = t.Name,
						Description = ((!string.IsNullOrEmpty(p.Description)) ? p.Description : null),
						SourceType = p.SourceType.ToString(),
						Mode = p.Mode.ToString(),
						State = p.State.ToString()
					}).ToList()
				} into g
				where g.Partitions.Any()
				select g).ToList();
			int item = list.Sum((TablePartitionList g) => g.Partitions.Count);
			if (maxResults.HasValue && maxResults.Value > 0)
			{
				int num = maxResults.Value;
				List<TablePartitionList> list2 = new List<TablePartitionList>();
				foreach (TablePartitionList item2 in list)
				{
					if (num > 0)
					{
						if (item2.Partitions.Count <= num)
						{
							list2.Add(item2);
							num -= item2.Partitions.Count;
							continue;
						}
						list2.Add(new TablePartitionList
						{
							TableName = item2.TableName,
							Partitions = item2.Partitions.Take(num).ToList()
						});
						num = 0;
						continue;
					}
					break;
				}
				result = (Tables: list2, TotalCount: item);
			}
			else
			{
				result = (Tables: list, TotalCount: item);
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> CreatePartitions(string? connectionName, List<PartitionDefinition> partitions, BatchOptions options, IWriteGuard writeGuard)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, partitions, options, "Create", "Created", "partitions", (PartitionDefinition item) => item.TableName + "." + item.Name, delegate(BatchItemContext<PartitionDefinition> ctx)
		{
			PartitionOperationResult partitionOperationResult = CreatePartitionInternal(ctx.Connection, ctx.Item, writeGuard);
			string[] source = new string[3] { "Ready", "NoData", "CalculationNeeded" };
			ctx.Result.Success = Enumerable.Contains(source, partitionOperationResult.State);
			ctx.Result.Data = partitionOperationResult;
			ctx.Result.Message = (ctx.Result.Success ? $"Successfully created partition '{ctx.Item.Name}' in table '{ctx.Item.TableName}'" : $"Failed to create partition '{ctx.Item.Name}' in table '{ctx.Item.TableName}': {partitionOperationResult.ErrorMessage}");
			if (partitionOperationResult.Warnings != null)
			{
				ctx.Result.Warnings.AddRange(partitionOperationResult.Warnings);
			}
			if (ctx.Result.Success && ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Created partition '{ctx.Item.TableName}.{ctx.Item.Name}'");
			}
		}, delegate(IConnectionInfo conn, List<PartitionDefinition> items, BatchOperationResponse response, List<string> warnings, string? txId, bool owns, bool txFailed, int fc)
		{
			PostCommitDaxValidator.Append(conn, warnings, response.Results, items, txId, owns, txFailed, fc, "created", (PartitionDefinition def) => ResolvePartitionForValidation(conn, def));
		});
	}

	public static async Task<BatchOperationResponse> UpdatePartitions(string? connectionName, List<PartitionDefinition> partitions, BatchOptions options, IWriteGuard writeGuard)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Update",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (partitions == null || !partitions.Any())
		{
			response.Success = false;
			response.Message = "No partitions provided for update";
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
		IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName);
		BatchOperationResponse result;
		try
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(conn, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < partitions.Count; i++)
				{
					PartitionDefinition partitionDefinition = partitions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = (string.IsNullOrWhiteSpace(partitionDefinition.Name) ? partitionDefinition.TableName : (partitionDefinition.TableName + "." + partitionDefinition.Name))
					};
					try
					{
						PartitionOperationResult partitionOperationResult = UpdatePartitionInternal(conn, partitionDefinition, writeGuard);
						string text = partitionOperationResult.PartitionName ?? partitionDefinition.Name;
						itemResult.ItemIdentifier = (string.IsNullOrWhiteSpace(text) ? partitionDefinition.TableName : (partitionDefinition.TableName + "." + text));
						string text2 = (string.IsNullOrWhiteSpace(text) ? ("the only partition in table '" + partitionDefinition.TableName + "'") : $"partition '{text}' in table '{partitionDefinition.TableName}'");
						string[] source = new string[3] { "Ready", "NoData", "CalculationNeeded" };
						itemResult.Success = Enumerable.Contains(source, partitionOperationResult.State);
						itemResult.Data = partitionOperationResult;
						if (itemResult.Success)
						{
							itemResult.Message = (partitionOperationResult.HasChanges ? ("Successfully updated " + text2) : (text2 + " updated (no changes detected)"));
							successCount++;
							if (transactionId != null)
							{
								TransactionOperations.RecordOperation(conn, string.IsNullOrWhiteSpace(text) ? ("Updated partition in table '" + partitionDefinition.TableName + "'") : $"Updated partition '{partitionDefinition.TableName}.{text}'");
							}
						}
						else
						{
							itemResult.Message = "Failed to update " + text2 + ": " + partitionOperationResult.ErrorMessage;
							failureCount++;
						}
						if (partitionOperationResult.Warnings != null)
						{
							itemResult.Warnings.AddRange(partitionOperationResult.Warnings);
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						string text3 = (string.IsNullOrWhiteSpace(partitionDefinition.Name) ? ("partition in table '" + partitionDefinition.TableName + "'") : $"partition '{partitionDefinition.Name}' in table '{partitionDefinition.TableName}'");
						itemResult.Message = "Error updating " + text3 + ": " + ex.Message;
						failureCount++;
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(conn, response, transactionId, ownsTransaction, partitions.Count, ref successCount, ref failureCount, "Updated", "partitions");
				PostCommitDaxValidator.Append(conn, warnings, response.Results, partitions, transactionId, ownsTransaction, transactionFailed, failureCount, "updated", (PartitionDefinition def) => ResolvePartitionForValidation(conn, def));
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
				response.Message = "Update operation failed: " + ex2.Message;
				failureCount = partitions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = partitions.Count,
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

	public static async Task<BatchOperationResponse> DeletePartitions(string? connectionName, List<PartitionReference> partitions, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, partitions, options, "Delete", "Deleted", "partitions", (PartitionReference item) => (!string.IsNullOrWhiteSpace(item.Name)) ? (item.TableName + "." + item.Name) : item.TableName, delegate(BatchItemContext<PartitionReference> ctx)
		{
			DeletePartitionInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.Name);
			ctx.Result.Success = true;
			ctx.Result.Message = (string.IsNullOrWhiteSpace(ctx.Item.Name) ? ("Successfully deleted partition from table '" + ctx.Item.TableName + "'") : $"Successfully deleted partition '{ctx.Item.Name}' from table '{ctx.Item.TableName}'");
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, string.IsNullOrWhiteSpace(ctx.Item.Name) ? ("Deleted partition from table '" + ctx.Item.TableName + "'") : $"Deleted partition '{ctx.Item.TableName}.{ctx.Item.Name}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetPartitions(string? connectionName, List<PartitionReference> partitions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (partitions == null || !partitions.Any())
		{
			response.Success = false;
			response.Message = "No partitions provided for retrieval";
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
				for (int i = 0; i < partitions.Count; i++)
				{
					PartitionReference partitionReference = partitions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = (string.IsNullOrWhiteSpace(partitionReference.Name) ? partitionReference.TableName : (partitionReference.TableName + "." + partitionReference.Name))
					};
					try
					{
						PartitionGet partitionInternal = GetPartitionInternal(connectionInfo.Database, partitionReference.TableName, partitionReference.Name);
						itemResult.Success = true;
						itemResult.Message = $"Successfully retrieved partition '{partitionInternal.Name}' from table '{partitionReference.TableName}'";
						itemResult.Data = partitionInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving partition from table '" + partitionReference.TableName + "': " + ex.Message;
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
				response.Message = $"Processed {partitions.Count} partition(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = partitions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get partitions", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = partitions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenamePartitions(string? connectionName, List<PartitionRename> partitions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Rename",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (partitions == null || !partitions.Any())
		{
			response.Success = false;
			response.Message = "No partitions provided for renaming";
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
				for (int i = 0; i < partitions.Count; i++)
				{
					PartitionRename partitionRename = partitions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = (string.IsNullOrWhiteSpace(partitionRename.CurrentName) ? (partitionRename.TableName + " -> " + partitionRename.NewName) : $"{partitionRename.TableName}.{partitionRename.CurrentName} -> {partitionRename.NewName}")
					};
					try
					{
						RenamePartitionInternal(connectionInfo, partitionRename.TableName, partitionRename.CurrentName, partitionRename.NewName);
						itemResult.Success = true;
						itemResult.Message = (string.IsNullOrWhiteSpace(partitionRename.CurrentName) ? $"Successfully renamed partition to '{partitionRename.NewName}' in table '{partitionRename.TableName}'" : $"Successfully renamed partition '{partitionRename.CurrentName}' to '{partitionRename.NewName}' in table '{partitionRename.TableName}'");
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, string.IsNullOrWhiteSpace(partitionRename.CurrentName) ? $"Renamed partition in table '{partitionRename.TableName}' to '{partitionRename.NewName}'" : $"Renamed partition '{partitionRename.TableName}.{partitionRename.CurrentName}' to '{partitionRename.NewName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = (string.IsNullOrWhiteSpace(partitionRename.CurrentName) ? ("Error renaming partition in table '" + partitionRename.TableName + "': " + ex.Message) : $"Error renaming partition '{partitionRename.CurrentName}' in table '{partitionRename.TableName}': {ex.Message}");
						failureCount++;
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, partitions.Count, ref successCount, ref failureCount, "Renamed", "partitions");
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
				failureCount = partitions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = partitions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RefreshPartitions(string? connectionName, List<PartitionRefresh> partitions, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, partitions, options, "Refresh", "Refreshed", "partitions", (PartitionRefresh item) => (!string.IsNullOrWhiteSpace(item.Name)) ? (item.TableName + "." + item.Name) : item.TableName, delegate(BatchItemContext<PartitionRefresh> ctx)
		{
			string text = ctx.Item.RefreshType ?? "Automatic";
			RefreshPartitionInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.Name, text);
			ctx.Result.Success = true;
			ctx.Result.Message = (string.IsNullOrWhiteSpace(ctx.Item.Name) ? $"Successfully refreshed partition in table '{ctx.Item.TableName}' with refresh type '{text}'" : $"Successfully refreshed partition '{ctx.Item.Name}' in table '{ctx.Item.TableName}' with refresh type '{text}'");
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, string.IsNullOrWhiteSpace(ctx.Item.Name) ? $"Refreshed partition in table '{ctx.Item.TableName}' with type '{text}'" : $"Refreshed partition '{ctx.Item.TableName}.{ctx.Item.Name}' with type '{text}'");
			}
		});
	}
}
