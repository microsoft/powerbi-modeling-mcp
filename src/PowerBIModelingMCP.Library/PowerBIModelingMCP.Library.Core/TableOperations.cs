using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class TableOperations
{
	public class TableOperationResult
	{
		public string TableName { get; set; } = string.Empty;

		public List<PartitionOperationResult> Partitions { get; set; } = new List<PartitionOperationResult>();

		public bool HasChanges { get; set; }

		public List<string> Warnings { get; set; } = new List<string>();
	}

	private readonly record struct TableSourceUpdatePlan(Partition Partition, PartitionDefinition PartitionUpdate);

	public record ResolvedFieldEntry(string TableName, string Name, string ObjectType, string? DisplayName);

	private sealed class FieldReferenceComparer : IEqualityComparer<(string TableName, string Name, string ObjectType)>
	{
		public static FieldReferenceComparer Instance { get; } = new FieldReferenceComparer();

		private FieldReferenceComparer()
		{
		}

		public bool Equals((string TableName, string Name, string ObjectType) x, (string TableName, string Name, string ObjectType) y)
		{
			if (StringComparer.OrdinalIgnoreCase.Equals(x.TableName, y.TableName) && StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name))
			{
				return StringComparer.OrdinalIgnoreCase.Equals(x.ObjectType, y.ObjectType);
			}
			return false;
		}

		public int GetHashCode((string TableName, string Name, string ObjectType) obj)
		{
			return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TableName), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ObjectType));
		}
	}

	internal static PostCommitDaxValidator.Target? ResolveTableForValidation(IConnectionInfo conn, TableDefinition def)
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
		Table table = database.Model.Tables.Find(def.Name);
		if (table == null)
		{
			return null;
		}
		List<PostCommitDaxValidator.Check> list = new List<PostCommitDaxValidator.Check>();
		foreach (Partition partition in table.Partitions)
		{
			string propertyLabel = ((table.Partitions.Count > 1) ? ("partition '" + partition.Name + "'") : "partition");
			list.Add(new PostCommitDaxValidator.Check(propertyLabel, partition.State.ToString(), partition.ErrorMessage));
		}
		if (list.Count == 0)
		{
			return null;
		}
		return new PostCommitDaxValidator.Target("Table", "'" + table.Name + "'", list);
	}

	public static void ValidateTableDefinition(TableBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (isCreate)
		{
			if (def is TableDefinition tableDefinition)
			{
				int num = new bool[4]
				{
					!string.IsNullOrWhiteSpace(tableDefinition.DaxExpression),
					!string.IsNullOrWhiteSpace(tableDefinition.MExpression),
					!string.IsNullOrWhiteSpace(tableDefinition.SqlQuery),
					!string.IsNullOrWhiteSpace(tableDefinition.EntityName)
				}.Count((bool x) => x);
				if (num == 0)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("One of DaxExpression, MExpression, EntityName, or SqlQuery must be provided", ErrorSource.User);
				}
				if (num > 1)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one of DaxExpression, MExpression, EntityName, or SqlQuery can be provided", ErrorSource.User);
				}
				if (!string.IsNullOrWhiteSpace(tableDefinition.SqlQuery) && string.IsNullOrWhiteSpace(tableDefinition.DataSourceName))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("DataSourceName is required when SqlQuery is provided", ErrorSource.User);
				}
				if (!string.IsNullOrWhiteSpace(tableDefinition.EntityName))
				{
					if (string.IsNullOrWhiteSpace(tableDefinition.DataSourceName) && string.IsNullOrWhiteSpace(tableDefinition.ExpressionSourceName))
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage("Either ExpressionSourceName or DataSourceName is required when EntityName is provided", ErrorSource.User);
					}
					if (!string.IsNullOrWhiteSpace(tableDefinition.DataSourceName) && !string.IsNullOrWhiteSpace(tableDefinition.ExpressionSourceName))
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one of ExpressionSourceName or DataSourceName can be provided when EntityName is specified", ErrorSource.User);
					}
				}
				bool num2 = !string.IsNullOrWhiteSpace(tableDefinition.DaxExpression);
				if (num2 && tableDefinition.Columns != null && tableDefinition.Columns.Count > 0)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Columns cannot be specified for calculated tables. The columns are derived from the DAX expression.", ErrorSource.User);
				}
				if (!num2 && (tableDefinition.Columns == null || tableDefinition.Columns.Count == 0))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Columns are required. The schema cannot be automatically inferred from the partition source expression - you must explicitly define the columns.", ErrorSource.User);
				}
				if (!num2 && tableDefinition.Columns != null && tableDefinition.Columns.Count > 0)
				{
					HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					List<string> list = new List<string>();
					foreach (ColumnDefinition column in tableDefinition.Columns)
					{
						if (string.IsNullOrWhiteSpace(column.Name))
						{
							throw McpExceptionWithSource.FromTelemetrySafeMessage("All columns must have a valid name", ErrorSource.User);
						}
						if (string.IsNullOrWhiteSpace(column.DataType) && string.IsNullOrWhiteSpace(column.Expression))
						{
							throw McpExceptionWithSource.FromTelemetrySafeMessage("DataType is required for column '" + column.Name + "'. Specify the data type (e.g., String, Int64, Double, DateTime, Decimal, Boolean).", ErrorSource.User);
						}
						if (!hashSet.Add(column.Name))
						{
							list.Add(column.Name);
						}
					}
					if (list.Count > 0)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage("Duplicate column names found: " + string.Join(", ", list.Distinct()) + ". Each column name must be unique within the table.", ErrorSource.User);
					}
					ColumnOperations.ValidateSingleKeyColumnDefinitionPerTable(tableDefinition.Columns, "a single table definition", tableDefinition.Name);
				}
			}
		}
		else if (def is TableDefinition update)
		{
			ValidateNoCreateOnlyPropertiesForUpdate(update);
		}
		if (def.ExtendedProperties != null)
		{
			List<string> list2 = ExtendedPropertyHelpers.Validate(def.ExtendedProperties);
			if (list2.Count > 0)
			{
				throw new McpExceptionWithSource("ExtendedProperties validation failed: " + string.Join(", ", list2), ErrorSource.User, "ExtendedProperties validation failed.");
			}
		}
		if (!string.IsNullOrWhiteSpace(def.LineageTag) && !Guid.TryParse(def.LineageTag, out var result))
		{
			throw new McpExceptionWithSource("LineageTag must be a valid GUID format. Current value: " + def.LineageTag, ErrorSource.User, "LineageTag must be a valid GUID format.");
		}
		if (!string.IsNullOrWhiteSpace(def.SourceLineageTag) && !Guid.TryParse(def.SourceLineageTag, out result))
		{
			throw new McpExceptionWithSource("SourceLineageTag must be a valid GUID format. Current value: " + def.SourceLineageTag, ErrorSource.User, "SourceLineageTag must be a valid GUID format.");
		}
		AnnotationHelpers.ValidateAnnotations(def.Annotations);
	}

	public static async Task<List<TableList>> ListTables(string? connectionName)
	{
		List<TableList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<TableList> list = ListTablesInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list tables", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list tables", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<TableList> ListTablesInternal(Database db)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		return db.Model.Tables.Select(delegate(Table t)
		{
			List<ModeType> list = (from m in t.Partitions.Select((Partition p) => (p.Mode != ModeType.Default) ? p.Mode : ModeType.Import).Distinct()
				orderby (int)m
				select m).ToList();
			string storageMode = list.Count switch
			{
				0 => null, 
				1 => list[0].ToString(), 
				_ => string.Join(",", list.Select((ModeType m) => m.ToString())), 
			};
			return new TableList
			{
				Name = t.Name,
				Description = ((!string.IsNullOrEmpty(t.Description)) ? t.Description : null),
				ColumnCount = t.Columns.Count((Column c) => !(c is RowNumberColumn)),
				MeasureCount = ((t.Measures.Count > 0) ? new int?(t.Measures.Count) : ((int?)null)),
				HierarchyCount = ((t.Hierarchies.Count > 0) ? new int?(t.Hierarchies.Count) : ((int?)null)),
				PartitionCount = ((t.Partitions.Count > 0) ? new int?(t.Partitions.Count) : ((int?)null)),
				CalendarCount = ((t.Calendars.Count > 0) ? new int?(t.Calendars.Count) : ((int?)null)),
				IsHidden = (t.IsHidden ? new bool?(true) : ((bool?)null)),
				IsPrivate = (t.IsPrivate ? new bool?(true) : ((bool?)null)),
				ShowAsVariationsOnly = (t.ShowAsVariationsOnly ? new bool?(true) : ((bool?)null)),
				IsCalculationGroup = ((t.CalculationGroup != null) ? new bool?(true) : ((bool?)null)),
				StorageMode = storageMode
			};
		}).ToList();
	}

	public static async Task<TableGet> GetTable(string? connectionName, string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		TableGet tableInternal;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			tableInternal = GetTableInternal(connectionInfo.Database, tableName);
		}
		return tableInternal;
	}

	internal static TableGet GetTableInternal(Database db, string tableName)
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
		TableGet tableGet = new TableGet
		{
			Name = table.Name,
			DataCategory = table.DataCategory,
			Description = table.Description,
			IsHidden = table.IsHidden,
			ShowAsVariationsOnly = table.ShowAsVariationsOnly,
			IsPrivate = table.IsPrivate,
			AlternateSourcePrecedence = table.AlternateSourcePrecedence,
			ExcludeFromModelRefresh = table.ExcludeFromModelRefresh,
			LineageTag = table.LineageTag,
			SourceLineageTag = table.SourceLineageTag,
			SystemManaged = table.SystemManaged,
			Mode = table.Partitions.FirstOrDefault()?.Mode,
			Columns = table.Columns.Select((Column c) => c.Name).ToList(),
			Measures = table.Measures.Select((Measure m) => m.Name).ToList(),
			Hierarchies = table.Hierarchies.Select((Hierarchy h) => h.Name).ToList(),
			Annotations = new List<KeyValuePair<string, string>>()
		};
		List<PartitionGet> partitionDetails = PartitionOperations.ListPartitionsInternal(db, tableName);
		tableGet.PartitionDetails = partitionDetails;
		if (table.Annotations != null)
		{
			foreach (Annotation annotation in table.Annotations)
			{
				tableGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name ?? string.Empty, annotation.Value ?? string.Empty));
			}
		}
		tableGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromTable(table);
		return tableGet;
	}

	internal static TableOperationResult CreateTableInternal(IConnectionInfo info, TableDefinition def, IWriteGuard writeGuard)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TableDefinition cannot be null", ErrorSource.User);
		}
		ValidateTableDefinition(def, isCreate: true);
		bool flag = !string.IsNullOrWhiteSpace(def.DaxExpression);
		Database database = info.Database;
		if (database.Model.Tables.Contains(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + def.Name + "' already exists", ErrorSource.User);
		}
		Table table = new Table
		{
			Name = def.Name
		};
		ApplyTableProperties(table, def);
		Partition metadataObject = CreatePartition(def, database, writeGuard);
		table.Partitions.Add(metadataObject);
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				table.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToTable(table, def.ExtendedProperties);
		}
		database.Model.Tables.Add(table);
		try
		{
			if (!flag && def.Columns != null)
			{
				List<(ColumnDefinition, string)> list = new List<(ColumnDefinition, string)>();
				List<(ColumnDefinition, List<string>)> list2 = new List<(ColumnDefinition, List<string>)>();
				foreach (ColumnDefinition column in def.Columns)
				{
					column.TableName = def.Name;
					if (string.IsNullOrWhiteSpace(column.SourceColumn) && string.IsNullOrWhiteSpace(column.Expression))
					{
						column.SourceColumn = column.Name;
					}
					string sortByColumn = column.SortByColumn;
					if (!string.IsNullOrWhiteSpace(sortByColumn))
					{
						list.Add((column, sortByColumn));
						column.SortByColumn = null;
					}
					List<string> groupByColumns = column.GroupByColumns;
					if (groupByColumns != null && groupByColumns.Count > 0)
					{
						list2.Add((column, groupByColumns));
						column.GroupByColumns = null;
					}
					ColumnOperations.CreateColumnInternal(info, column);
				}
				if (list.Count > 0 || list2.Count > 0)
				{
					Table table2 = database.Model.Tables.Find(def.Name);
					foreach (var item3 in list)
					{
						ColumnDefinition item = item3.Item1;
						string item2 = item3.Item2;
						Column obj = table2.Columns.Find(item.Name) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{item.Name}' not found in table '{def.Name}' during SortByColumn assignment", ErrorSource.User);
						Column sortByColumn2 = table2.Columns.Find(item2) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"SortByColumn '{item2}' not found in table '{def.Name}'", ErrorSource.User);
						obj.SortByColumn = sortByColumn2;
					}
					foreach (var (columnDefinition, groupByColumns2) in list2)
					{
						ColumnOperations.ValidateAndApplyGroupByColumns(info, columnDefinition.TableName, columnDefinition.Name, groupByColumns2);
					}
				}
			}
			TransactionOperations.RecordOperation(info, "Created table '" + def.Name + "' in model " + database.Model.Name);
			ConnectionOperations.SaveChangesWithRollback(info, "create table", OperationType.Create);
		}
		catch
		{
			database.Model.Tables.Remove(table);
			throw;
		}
		return CreateTableOperationResult(table);
	}

	public static async Task<TableOperationResult> UpdateTable(string? connectionName, TableDefinition update)
	{
		ValidateTableDefinition(update, isCreate: false);
		TableOperationResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TableOperationResult tableOperationResult = UpdateTableInternal(connectionInfo, update, null);
			AppendSingleUpdatePostCommitDaxWarnings(connectionInfo, update, tableOperationResult);
			result = tableOperationResult;
		}
		return result;
	}

	internal static TableOperationResult UpdateTableInternal(IConnectionInfo info, TableDefinition update)
	{
		return UpdateTableInternal(info, update, null);
	}

	internal static TableOperationResult UpdateTableInternal(IConnectionInfo info, TableDefinition update, IWriteGuard? writeGuard)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TableDefinition cannot be null", ErrorSource.User);
		}
		ValidateTableDefinition(update, isCreate: false);
		Database database = info.Database;
		Table table = database.Model.Tables.Find(update.Name) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + update.Name + "' not found", ErrorSource.User);
		ValidateTableSourceUpdate(table, update, database, writeGuard);
		bool flag = ApplyTableUpdates(table, update);
		(bool sourceChanged, IReadOnlyList<string> warnings) tuple = ApplyTableSourceUpdate(table, update, database, writeGuard);
		bool item = tuple.sourceChanged;
		IReadOnlyList<string> item2 = tuple.warnings;
		TableOperationResult tableOperationResult = CreateTableOperationResult(table);
		tableOperationResult.HasChanges = flag || item;
		tableOperationResult.Warnings.AddRange(item2);
		if (tableOperationResult.HasChanges)
		{
			TransactionOperations.RecordOperation(info, "Updated table '" + update.Name + "' in model " + database.Model.Name);
			CheckpointMode checkpointMode = (IsBrokenCalculatedPartitionState(table) ? CheckpointMode.ForceEvenInTransaction : CheckpointMode.Default);
			ConnectionOperations.SaveChangesWithRollback(info, "update table", OperationType.Update, checkpointMode);
		}
		return tableOperationResult;
	}

	public static async Task RenameTable(string? connectionName, string oldName, string newName)
	{
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		RenameTableInternal(info, oldName, newName);
	}

	internal static void RenameTableInternal(IConnectionInfo info, string oldName, string newName)
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
		Database database = info.Database;
		Table obj = database.Model.Tables.Find(oldName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + oldName + "' not found", ErrorSource.User);
		if (database.Model.Tables.Contains(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + newName + "' already exists", ErrorSource.User);
		}
		obj.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed table '{oldName}' to '{newName}' in model {database.Model.Name}");
		ConnectionOperations.SaveChangesWithRollback(info, "rename table", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	public static async Task DeleteTable(string? connectionName, string tableName, bool shouldCascadeDelete)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		DeleteTableInternal(info, tableName, shouldCascadeDelete);
	}

	internal static void DeleteTableInternal(IConnectionInfo info, string tableName, bool shouldCascadeDelete)
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
		Table table = database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		List<string> list = CheckTableDependencies(database, table);
		list.AddRange(StructuralDependencyHelper.CheckAndDeleteDependenciesIfRequired(database, table, shouldCascadeDelete));
		if (!shouldCascadeDelete && list.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete table '" + tableName + "' because it has dependencies: " + string.Join(", ", list), ErrorSource.User);
		}
		database.Model.Tables.Remove(table);
		TransactionOperations.RecordOperation(info, "Deleted table '" + tableName + "' from model " + database.Model.Name);
		ConnectionOperations.SaveChangesWithRollback(info, "delete table", OperationType.Delete);
	}

	public static async Task RefreshTable(string? connectionName, string tableName, string? refreshType = "Automatic")
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		RefreshTableInternal(info, tableName, refreshType);
	}

	internal static void RefreshTableInternal(IConnectionInfo info, string tableName, string? refreshType = "Automatic")
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
		RefreshType result;
		if (string.IsNullOrWhiteSpace(refreshType))
		{
			result = RefreshType.Automatic;
		}
		else if (!Enum.TryParse<RefreshType>(refreshType, ignoreCase: true, out result))
		{
			string[] names = Enum.GetNames(typeof(RefreshType));
			throw new McpExceptionWithSource("Invalid refresh type '" + refreshType + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid refresh type supplied. Valid values are: " + string.Join(", ", names) + ".");
		}
		obj.RequestRefresh(result);
		TransactionOperations.RecordOperation(info, $"Refreshed table '{tableName}' in model {database.Model.Name} with refresh type '{result}'");
		ConnectionOperations.SaveChangesWithRollback(info, "refresh table", OperationType.Update);
	}

	public static async Task MarkAsDateTable(string? connectionName, MarkAsDateTableDefinition definition)
	{
		if (definition == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("definition is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(definition.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TableName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		MarkAsDateTableInternal(info, definition);
	}

	internal static void MarkAsDateTableInternal(IConnectionInfo info, MarkAsDateTableDefinition definition)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (definition == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("definition is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(definition.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TableName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Table table = database.Model.Tables.Find(definition.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + definition.TableName + "' not found", ErrorSource.User);
		Column column;
		if (!string.IsNullOrWhiteSpace(definition.DateColumnName))
		{
			column = table.Columns.Find(definition.DateColumnName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{definition.DateColumnName}' not found in table '{definition.TableName}'", ErrorSource.User);
			if (column.DataType != DataType.DateTime)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{definition.DateColumnName}' has data type '{column.DataType}' but must be DateTime to be used as a date column", ErrorSource.User);
			}
		}
		else
		{
			List<Column> list = table.Columns.Where((Column c) => c.IsKey).ToList();
			Column singleKeyColumn = ((list.Count == 1) ? list[0] : null);
			HashSet<string> oneEndColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (SingleColumnRelationship item in database.Model.Relationships.OfType<SingleColumnRelationship>())
			{
				if (string.Equals(item.ToTable.Name, table.Name, StringComparison.OrdinalIgnoreCase))
				{
					oneEndColumnNames.Add(item.ToColumn.Name);
				}
			}
			List<Column> list2 = table.Columns.Where((Column c) => c.DataType == DataType.DateTime && (c.IsUnique || (singleKeyColumn != null && c.Name == singleKeyColumn.Name) || oneEndColumnNames.Contains(c.Name))).ToList();
			if (list2.Count == 0)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("No suitable date column found in table '" + definition.TableName + "'. No DateTime column with IsUnique=true, key status, or on the one-end of a relationship was found. Please specify DateColumnName explicitly.", ErrorSource.User);
			}
			if (list2.Count > 1)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Multiple candidate date columns found in table '{definition.TableName}': {string.Join(", ", list2.Select((Column c) => c.Name))}. Please specify DateColumnName explicitly.", ErrorSource.User);
			}
			column = list2[0];
		}
		table.DataCategory = "Time";
		column.IsUnique = true;
		TransactionOperations.RecordOperation(info, $"Marked table '{definition.TableName}' as date table with date column '{column.Name}' in model {database.Model.Name}");
		ConnectionOperations.SaveChangesWithRollback(info, "mark table as date table", OperationType.Update);
	}

	public static async Task<Dictionary<string, object>> GetTableSchema(string? connectionName, string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		Dictionary<string, object> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				Dictionary<string, object> tableSchemaInternal = GetTableSchemaInternal(connectionInfo, tableName);
				AuditEvent.Default.Emit("get table schema", success: true, OperationType.Read, connectionInfo);
				result = tableSchemaInternal;
			}
			catch
			{
				AuditEvent.Default.Emit("get table schema", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static Dictionary<string, object> GetTableSchemaInternal(IConnectionInfo info, string tableName)
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
		Table table = database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		return new Dictionary<string, object>
		{
			["TableName"] = table.Name,
			["Columns"] = table.Columns.Select((Column c) => new
			{
				Name = c.Name,
				DataType = c.DataType.ToString(),
				IsHidden = c.IsHidden,
				IsKey = c.IsKey,
				IsUnique = c.IsUnique,
				IsNullable = c.IsNullable,
				Description = c.Description,
				FormatString = c.FormatString,
				DataCategory = c.DataCategory,
				SummarizeBy = c.SummarizeBy.ToString(),
				DisplayFolder = c.DisplayFolder,
				SortByColumn = c.SortByColumn?.Name,
				Expression = (c as CalculatedColumn)?.Expression,
				SourceColumn = (c as DataColumn)?.SourceColumn
			}).ToList(),
			["Measures"] = table.Measures.Select((Measure m) => new
			{
				Name = m.Name,
				Expression = m.Expression,
				DataType = m.DataType.ToString(),
				FormatString = m.FormatString,
				Description = m.Description,
				IsHidden = m.IsHidden,
				DisplayFolder = m.DisplayFolder
			}).ToList(),
			["Hierarchies"] = table.Hierarchies.Select((Hierarchy h) => new
			{
				Name = h.Name,
				Description = h.Description,
				IsHidden = h.IsHidden,
				DisplayFolder = h.DisplayFolder,
				Levels = h.Levels.Select((Level l) => new
				{
					Name = l.Name,
					Ordinal = l.Ordinal,
					Column = l.Column?.Name
				}).ToList()
			}).ToList(),
			["Relationships"] = (from r in database.Model.Relationships.OfType<SingleColumnRelationship>()
				where r.FromTable.Name == table.Name || r.ToTable.Name == table.Name
				select new
				{
					Name = r.Name,
					FromTable = r.FromTable.Name,
					FromColumn = r.FromColumn?.Name,
					ToTable = r.ToTable.Name,
					ToColumn = r.ToColumn?.Name,
					IsActive = r.IsActive,
					CrossFilteringBehavior = r.CrossFilteringBehavior.ToString(),
					JoinOnDateBehavior = r.JoinOnDateBehavior.ToString(),
					RelyOnReferentialIntegrity = r.RelyOnReferentialIntegrity
				}).ToList()
		};
	}

	public static async Task<TmdlExportResult> ExportTMDL(string? connectionName, string tableName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		TmdlExportResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				TmdlExportResult tmdlExportResult = ExportTMDLInternal(connectionInfo, tableName, options);
				AuditEvent.Default.Emit("export table to TMDL", success: true, OperationType.Read, connectionInfo);
				result = tmdlExportResult;
			}
			catch
			{
				AuditEvent.Default.Emit("export table to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static TmdlExportResult ExportTMDLInternal(IConnectionInfo info, string tableName, ExportTmdl options)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		Table table = info.Database.Model.Tables.Find(tableName);
		if (table == null)
		{
			throw new ArgumentException("Table '" + tableName + "' not found");
		}
		try
		{
			string content = TmdlSerializer.SerializeObject(table, options.SerializationOptions.ToMetadataSerializationOptions());
			var (processedContent, isTruncated, savedFilePath, warnings) = ExportContentProcessor.ProcessExportContent(content, options);
			return TmdlExportResult.CreateSuccess(tableName, "Table", content, processedContent, isTruncated, savedFilePath, warnings, options);
		}
		catch (Exception ex)
		{
			return TmdlExportResult.CreateFailure(tableName, "Table", ex.Message, ErrorSource.System);
		}
	}

	public static async Task<TmslExportResult> ExportTMSL(string? connectionName, string tableName, ExportTmsl tmslOptions)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
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
				TmslExportResult tmslExportResult = ExportTMSLInternal(connectionInfo, tableName, tmslOptions);
				AuditEvent.Default.Emit("export table to TMSL", success: true, OperationType.Read, connectionInfo);
				result = tmslExportResult;
			}
			catch (Exception ex)
			{
				AuditEvent.Default.Emit("export table to TMSL", success: false, OperationType.Read, connectionInfo);
				result = TmslExportResult.CreateFailure(tableName, "Table", tmslOptions.TmslOperationType, ex.Message, (ex is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : ErrorSource.System);
			}
		}
		return result;
	}

	internal static TmslExportResult ExportTMSLInternal(IConnectionInfo info, string tableName, ExportTmsl tmslOptions)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (tmslOptions == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ExportTmsl options cannot be null", ErrorSource.User);
		}
		Table metadataObject = info.Database.Model.Tables.Find(tableName) ?? throw new ArgumentException("Table '" + tableName + "' not found");
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
			if (!Enum.TryParse<RefreshType>(tmslOptions.RefreshType, ignoreCase: true, out var result2))
			{
				string[] names2 = Enum.GetNames(typeof(RefreshType));
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
		return tmslExportResult;
	}

	private static void ApplyTableProperties(Table table, TableBase def)
	{
		if (!string.IsNullOrWhiteSpace(def.DataCategory))
		{
			table.DataCategory = def.DataCategory;
		}
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			table.Description = def.Description;
		}
		if (def.IsHidden.HasValue)
		{
			table.IsHidden = def.IsHidden.Value;
		}
		if (def.ShowAsVariationsOnly.HasValue)
		{
			table.ShowAsVariationsOnly = def.ShowAsVariationsOnly.Value;
		}
		if (def.IsPrivate.HasValue)
		{
			table.IsPrivate = def.IsPrivate.Value;
		}
		if (def.AlternateSourcePrecedence.HasValue)
		{
			table.AlternateSourcePrecedence = def.AlternateSourcePrecedence.Value;
		}
		if (def.ExcludeFromModelRefresh.HasValue)
		{
			table.ExcludeFromModelRefresh = def.ExcludeFromModelRefresh.Value;
		}
		table.LineageTag = (string.IsNullOrWhiteSpace(def.LineageTag) ? Guid.NewGuid().ToString() : def.LineageTag);
		if (!string.IsNullOrWhiteSpace(def.SourceLineageTag))
		{
			table.SourceLineageTag = def.SourceLineageTag;
		}
		if (def.SystemManaged.HasValue)
		{
			table.SystemManaged = def.SystemManaged.Value;
		}
	}

	private static Partition CreatePartition(TableDefinition def, Database db, IWriteGuard writeGuard)
	{
		Partition partition;
		if (!string.IsNullOrWhiteSpace(def.DaxExpression))
		{
			partition = new Partition
			{
				Name = (def.PartitionName ?? def.Name),
				Source = new CalculatedPartitionSource
				{
					Expression = def.DaxExpression
				}
			};
		}
		else if (!string.IsNullOrWhiteSpace(def.MExpression))
		{
			partition = new Partition
			{
				Name = (def.PartitionName ?? def.Name),
				Source = new MPartitionSource
				{
					Expression = def.MExpression
				}
			};
		}
		else if (!string.IsNullOrWhiteSpace(def.SqlQuery))
		{
			writeGuard.AssertFullModeRequired("CREATE Table with SqlQuery", "PowerBI does not support tables with SQL queries that reference data sources");
			DataSource dataSource = db.Model.DataSources.Find(def.DataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + def.DataSourceName + "' not found", ErrorSource.User);
			partition = new Partition
			{
				Name = (def.PartitionName ?? def.Name),
				Source = new QueryPartitionSource
				{
					Query = def.SqlQuery,
					DataSource = dataSource
				}
			};
		}
		else
		{
			if (string.IsNullOrWhiteSpace(def.EntityName))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("No valid expression provided", ErrorSource.User);
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
				writeGuard.AssertFullModeRequired("CREATE Table with EntityName", "PowerBI does not support tables with EntityPartitionSource referencing DataSourceName");
				DataSource dataSource2 = db.Model.DataSources.Find(def.DataSourceName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + def.DataSourceName + "' not found", ErrorSource.User);
				entityPartitionSource.DataSource = dataSource2;
			}
			partition = new Partition
			{
				Name = (def.PartitionName ?? def.Name),
				Source = entityPartitionSource
			};
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
		return partition;
	}

	private static void ValidateNoCreateOnlyPropertiesForUpdate(TableDefinition update)
	{
		List<string> list = new List<string>();
		if (update.SqlQuery != null)
		{
			list.Add("SqlQuery");
		}
		if (update.PartitionName != null)
		{
			list.Add("PartitionName");
		}
		if (update.Mode != null)
		{
			list.Add("Mode");
		}
		if (update.Columns != null)
		{
			list.Add("Columns");
		}
		if (list.Count > 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table Update does not support create-only partition source or schema fields: " + string.Join(", ", list) + ". SqlQuery, PartitionName, Mode, and Columns can only be set when creating a table. Use partition_operations.Update for explicit partition edits and column_operations for column schema changes.", ErrorSource.User);
		}
		ValidateUpdateSourceIntent(update);
	}

	private static void ValidateUpdateSourceIntent(TableDefinition update)
	{
		ValidateNonEmptyWhenProvided(update.DaxExpression, "DaxExpression");
		ValidateNonEmptyWhenProvided(update.MExpression, "MExpression");
		ValidateNonEmptyWhenProvided(update.EntityName, "EntityName");
		ValidateNonEmptyWhenProvided(update.ExpressionSourceName, "ExpressionSourceName");
		ValidateNonEmptyWhenProvided(update.DataSourceName, "DataSourceName");
		List<string> list = new List<string>();
		if (update.DaxExpression != null)
		{
			list.Add("DaxExpression");
		}
		if (update.MExpression != null)
		{
			list.Add("MExpression");
		}
		if (update.EntityName != null)
		{
			list.Add("EntityName");
		}
		if (list.Count > 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one source kind can be provided for Table Update: " + string.Join(", ", list) + ".", ErrorSource.User);
		}
		if ((update.SchemaName != null || update.ExpressionSourceName != null || update.DataSourceName != null) && update.EntityName == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("EntityName is required when SchemaName, ExpressionSourceName, or DataSourceName is provided for a table source update.", ErrorSource.User);
		}
		if (update.EntityName != null)
		{
			bool num = !string.IsNullOrWhiteSpace(update.ExpressionSourceName);
			bool flag = !string.IsNullOrWhiteSpace(update.DataSourceName);
			if (num == flag)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("EntityName table source updates require exactly one of ExpressionSourceName or DataSourceName.", ErrorSource.User);
			}
		}
	}

	private static void ValidateNonEmptyWhenProvided(string? value, string fieldName)
	{
		if (value != null && string.IsNullOrWhiteSpace(value))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage(fieldName + " cannot be empty when provided for Table Update.", ErrorSource.User);
		}
	}

	private static void ValidateTableSourceUpdate(Table table, TableDefinition update, Database db, IWriteGuard? writeGuard)
	{
		BuildTableSourceUpdatePlan(table, update, db, writeGuard);
	}

	internal static (bool sourceChanged, IReadOnlyList<string> warnings) ApplyTableSourceUpdate(Table table, TableDefinition update, Database db, IWriteGuard? writeGuard)
	{
		TableSourceUpdatePlan? tableSourceUpdatePlan = BuildTableSourceUpdatePlan(table, update, db, writeGuard);
		if (!tableSourceUpdatePlan.HasValue)
		{
			return (sourceChanged: false, warnings: Array.Empty<string>());
		}
		bool expressionChanged;
		bool item = PartitionOperations.UpdatePartitionSource(tableSourceUpdatePlan.Value.Partition, tableSourceUpdatePlan.Value.PartitionUpdate, db, writeGuard, out expressionChanged);
		List<string> list = new List<string>();
		if (PartitionWarnings.IsMExpressionUpdate(tableSourceUpdatePlan.Value.Partition.Source, expressionChanged))
		{
			list.Add("The partition source was updated, but table column mappings were not. If source column names or data types changed, use column_operations in a follow-up call to update DataColumn.SourceColumn and, for DirectQuery tables, Column.SourceProviderType for each affected column.");
		}
		return (sourceChanged: item, warnings: list);
	}

	private static TableSourceUpdatePlan? BuildTableSourceUpdatePlan(Table table, TableDefinition update, Database db, IWriteGuard? writeGuard)
	{
		if (!HasTableSourceUpdate(update))
		{
			return null;
		}
		if (table.Partitions.Count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + table.Name + "' has no partitions; table-level source updates require exactly one partition. Create a partition first with partition_operations.Create, or provide an explicit partition update.", ErrorSource.User);
		}
		if (table.Partitions.Count > 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table '{table.Name}' has {table.Partitions.Count} partitions; table-level source updates require exactly one partition. " + "Use partition_operations.Update with TableName, Name, SourceType, and the source-specific field to update a specific partition.", ErrorSource.User);
		}
		Partition partition = table.Partitions[0];
		(string requestedSourceKind, string requestedFieldName) requestedTableSourceKind = GetRequestedTableSourceKind(update);
		string item = requestedTableSourceKind.requestedSourceKind;
		string item2 = requestedTableSourceKind.requestedFieldName;
		string tablePartitionSourceKind = GetTablePartitionSourceKind(partition.Source);
		if (!string.Equals(tablePartitionSourceKind, item, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Cannot update table source type using {item2}: current source type '{tablePartitionSourceKind}', requested source type '{item}'. " + "Use a matching source field or recreate the partition to change source types.", ErrorSource.User);
		}
		PartitionDefinition partitionDefinition = new PartitionDefinition();
		partitionDefinition.TableName = update.Name;
		partitionDefinition.Name = partition.Name;
		PartitionDefinition partitionDefinition2 = partitionDefinition;
		partitionDefinition2.SourceType = item switch
		{
			"calculated" => "Calculated", 
			"m" => "M", 
			"entity" => "Entity", 
			_ => item, 
		};
		PartitionDefinition partitionDefinition3 = partitionDefinition;
		switch (item)
		{
		case "calculated":
			partitionDefinition3.Expression = update.DaxExpression;
			break;
		case "m":
			partitionDefinition3.Expression = update.MExpression;
			break;
		case "entity":
			partitionDefinition3.EntityName = update.EntityName;
			partitionDefinition3.SchemaName = update.SchemaName;
			partitionDefinition3.ExpressionSourceName = update.ExpressionSourceName;
			partitionDefinition3.DataSourceName = update.DataSourceName;
			ValidateEntitySourceReferences(update, db, writeGuard);
			break;
		}
		return new TableSourceUpdatePlan(partition, partitionDefinition3);
	}

	private static void ValidateEntitySourceReferences(TableDefinition update, Database db, IWriteGuard? writeGuard)
	{
		if (!string.IsNullOrWhiteSpace(update.ExpressionSourceName) && db.Model.Expressions.Find(update.ExpressionSourceName) == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression source '" + update.ExpressionSourceName + "' not found for entity table source update.", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(update.DataSourceName))
		{
			if (writeGuard == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Entity table source updates that reference DataSourceName require a guarded caller so compatibility mode can be enforced. Use the MCP table_operations.Update tool, or use ExpressionSourceName with the public library UpdateTable overload; the public library UpdateTable overload intentionally rejects DataSourceName to preserve API compatibility and write-guard safety.", ErrorSource.User);
			}
			writeGuard.AssertFullModeRequired("UPDATE Table with EntityName and DataSourceName", "PowerBI does not support tables with EntityPartitionSource referencing DataSourceName");
			if (db.Model.DataSources.Find(update.DataSourceName) == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Data source '" + update.DataSourceName + "' not found for entity table source update.", ErrorSource.User);
			}
		}
	}

	private static bool HasTableSourceUpdate(TableDefinition update)
	{
		if (update.DaxExpression == null && update.MExpression == null && update.EntityName == null && update.SchemaName == null && update.ExpressionSourceName == null)
		{
			return update.DataSourceName != null;
		}
		return true;
	}

	private static (string requestedSourceKind, string requestedFieldName) GetRequestedTableSourceKind(TableDefinition update)
	{
		if (update.DaxExpression != null)
		{
			return (requestedSourceKind: "calculated", requestedFieldName: "DaxExpression");
		}
		if (update.MExpression != null)
		{
			return (requestedSourceKind: "m", requestedFieldName: "MExpression");
		}
		if (update.EntityName != null)
		{
			return (requestedSourceKind: "entity", requestedFieldName: "EntityName");
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("No valid table source update was provided.", ErrorSource.User);
	}

	private static string GetTablePartitionSourceKind(PartitionSource source)
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

	private static void AppendSingleUpdatePostCommitDaxWarnings(IConnectionInfo conn, TableDefinition update, TableOperationResult result)
	{
		List<string> list = new List<string>();
		List<ItemResult> results = new List<ItemResult>
		{
			new ItemResult
			{
				Index = 0,
				ItemIdentifier = update.Name,
				Success = true
			}
		};
		PostCommitDaxValidator.Append(conn, list, results, new List<TableDefinition> { update }, conn.Transaction?.TransactionId, ownsTransaction: false, transactionFailed: false, 0, "updated", (TableDefinition def) => ResolveTableForValidation(conn, def));
		foreach (string item in list)
		{
			if (!result.Warnings.Contains<string>(item, StringComparer.Ordinal))
			{
				result.Warnings.Add(item);
			}
		}
	}

	private static bool IsBrokenCalculatedPartitionState(Table table)
	{
		return table.Partitions.Any(delegate(Partition p)
		{
			if (p.Source is CalculatedPartitionSource)
			{
				ObjectState state = p.State;
				if (state != ObjectState.Ready && state != ObjectState.NoData)
				{
					return state != ObjectState.CalculationNeeded;
				}
				return false;
			}
			return false;
		});
	}

	private static bool ApplyTableUpdates(Table table, TableDefinition update)
	{
		bool result = false;
		if (update.DataCategory != null)
		{
			string text = (string.IsNullOrEmpty(update.DataCategory) ? null : update.DataCategory);
			if (text != table.DataCategory)
			{
				table.DataCategory = text;
				result = true;
			}
		}
		if (update.Description != null)
		{
			string text2 = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (text2 != table.Description)
			{
				table.Description = text2;
				result = true;
			}
		}
		if (update.LineageTag != null)
		{
			string text3 = (string.IsNullOrEmpty(update.LineageTag) ? null : update.LineageTag);
			if (text3 != table.LineageTag)
			{
				table.LineageTag = text3;
				result = true;
			}
		}
		if (update.SourceLineageTag != null)
		{
			string text4 = (string.IsNullOrEmpty(update.SourceLineageTag) ? null : update.SourceLineageTag);
			if (text4 != table.SourceLineageTag)
			{
				table.SourceLineageTag = text4;
				result = true;
			}
		}
		if (update.IsHidden.HasValue && table.IsHidden != update.IsHidden.Value)
		{
			table.IsHidden = update.IsHidden.Value;
			result = true;
		}
		if (update.ShowAsVariationsOnly.HasValue && table.ShowAsVariationsOnly != update.ShowAsVariationsOnly.Value)
		{
			table.ShowAsVariationsOnly = update.ShowAsVariationsOnly.Value;
			result = true;
		}
		if (update.IsPrivate.HasValue && table.IsPrivate != update.IsPrivate.Value)
		{
			table.IsPrivate = update.IsPrivate.Value;
			result = true;
		}
		if (update.ExcludeFromModelRefresh.HasValue && table.ExcludeFromModelRefresh != update.ExcludeFromModelRefresh.Value)
		{
			table.ExcludeFromModelRefresh = update.ExcludeFromModelRefresh.Value;
			result = true;
		}
		if (update.SystemManaged.HasValue && table.SystemManaged != update.SystemManaged.Value)
		{
			table.SystemManaged = update.SystemManaged.Value;
			result = true;
		}
		if (update.AlternateSourcePrecedence.HasValue && table.AlternateSourcePrecedence != update.AlternateSourcePrecedence.Value)
		{
			table.AlternateSourcePrecedence = update.AlternateSourcePrecedence.Value;
			result = true;
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(table, update.Annotations, (Table obj) => obj.Annotations))
		{
			result = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num = table.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceExtendedProperties(table, update.ExtendedProperties, (Table obj) => obj.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				result = true;
			}
		}
		return result;
	}

	private static List<string> CheckTableDependencies(Database db, Table table)
	{
		List<string> list = new List<string>();
		foreach (Table table2 in db.Model.Tables)
		{
			if (table2 == table)
			{
				continue;
			}
			foreach (Measure measure in table2.Measures)
			{
				if ((!string.IsNullOrWhiteSpace(measure.Expression) && measure.Expression.Contains("'" + table.Name + "'")) || measure.Expression.Contains("[" + table.Name + "]"))
				{
					list.Add($"Measure: {table2.Name}[{measure.Name}]");
				}
			}
		}
		return list;
	}

	private static TableOperationResult CreateTableOperationResult(Table table)
	{
		TableOperationResult tableOperationResult = new TableOperationResult
		{
			TableName = (table.Name ?? string.Empty)
		};
		foreach (Partition partition in table.Partitions)
		{
			tableOperationResult.Partitions.Add(new PartitionOperationResult
			{
				State = partition.State.ToString(),
				ErrorMessage = partition.ErrorMessage,
				PartitionName = partition.Name,
				TableName = (table.Name ?? string.Empty)
			});
		}
		return tableOperationResult;
	}

	public static async Task<BatchOperationResponse> CreateTables(string? connectionName, List<TableDefinition> tables, BatchOptions options, IWriteGuard writeGuard)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, tables, options, "Create", "Created", "tables", (TableDefinition item) => item.Name, delegate(BatchItemContext<TableDefinition> ctx)
		{
			TableOperationResult tableOperationResult = CreateTableInternal(ctx.Connection, ctx.Item, writeGuard);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully created table '" + ctx.Item.Name + "'";
			foreach (PartitionOperationResult partition in tableOperationResult.Partitions)
			{
				if (partition.Warnings != null)
				{
					ctx.Result.Warnings.AddRange(partition.Warnings);
				}
			}
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Created table '" + ctx.Item.Name + "'");
			}
		}, delegate(IConnectionInfo conn, List<TableDefinition> items, BatchOperationResponse response, List<string> warnings, string? txId, bool owns, bool txFailed, int fc)
		{
			PostCommitDaxValidator.Append(conn, warnings, response.Results, items, txId, owns, txFailed, fc, "created", (TableDefinition def) => ResolveTableForValidation(conn, def));
		});
	}

	public static async Task<BatchOperationResponse> CreateFieldParameterTables(string? connectionName, List<FieldParameterDefinition> definitions, BatchOptions options, IWriteGuard writeGuard)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, definitions, options, "CreateFieldParameter", "Created", "field parameter tables", (FieldParameterDefinition item) => item.Name, delegate(BatchItemContext<FieldParameterDefinition> ctx)
		{
			TableOperationResult tableOperationResult = CreateFieldParameterTableInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully created field parameter table '" + ctx.Item.Name + "'";
			foreach (PartitionOperationResult partition in tableOperationResult.Partitions)
			{
				if (partition.Warnings != null)
				{
					ctx.Result.Warnings.AddRange(partition.Warnings);
				}
			}
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Created field parameter table '" + ctx.Item.Name + "'");
			}
		}, delegate(IConnectionInfo conn, List<FieldParameterDefinition> items, BatchOperationResponse response, List<string> warnings, string? txId, bool owns, bool txFailed, int fc)
		{
			PostCommitDaxValidator.Append(conn, warnings, response.Results, items, txId, owns, txFailed, fc, "created", (FieldParameterDefinition def) => ResolveTableForValidation(conn, new TableDefinition
			{
				Name = def.Name
			}));
		});
	}

	public static async Task<BatchOperationResponse> UpdateTables(string? connectionName, List<TableDefinition> tables, BatchOptions options)
	{
		return await UpdateTablesInternal(connectionName, tables, options, null);
	}

	internal static async Task<BatchOperationResponse> UpdateTablesInternal(string? connectionName, List<TableDefinition> tables, BatchOptions options, IWriteGuard? writeGuard)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, tables, options, "Update", "Updated", "tables", (TableDefinition item) => item.Name, delegate(BatchItemContext<TableDefinition> ctx)
		{
			TableOperationResult tableOperationResult = UpdateTableInternal(ctx.Connection, ctx.Item, writeGuard);
			ctx.Result.Success = true;
			ctx.Result.Message = (tableOperationResult.HasChanges ? ("Successfully updated table '" + ctx.Item.Name + "'") : ("Table '" + ctx.Item.Name + "' updated (no changes detected)"));
			ctx.Result.Warnings.AddRange(tableOperationResult.Warnings);
			foreach (PartitionOperationResult partition in tableOperationResult.Partitions)
			{
				if (partition.Warnings != null)
				{
					ctx.Result.Warnings.AddRange(partition.Warnings);
				}
			}
		}, delegate(IConnectionInfo conn, List<TableDefinition> items, BatchOperationResponse response, List<string> warnings, string? txId, bool owns, bool txFailed, int fc)
		{
			PostCommitDaxValidator.Append(conn, warnings, response.Results, items, txId, owns, txFailed, fc, "updated", (TableDefinition def) => ResolveTableForValidation(conn, def));
		});
	}

	public static async Task<BatchOperationResponse> DeleteTables(string? connectionName, List<TableReference> tables, bool shouldCascadeDelete, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, tables, options, "Delete", "Deleted", "tables", (TableReference item) => item.Name, delegate(BatchItemContext<TableReference> ctx)
		{
			DeleteTableInternal(ctx.Connection, ctx.Item.Name, shouldCascadeDelete);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully deleted table '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Deleted table '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetTables(string? connectionName, List<TableReference> tables, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (tables == null || !tables.Any())
		{
			response.Success = false;
			response.Message = "No tables provided for retrieval";
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
				for (int i = 0; i < tables.Count; i++)
				{
					TableReference tableReference = tables[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = tableReference.Name
					};
					try
					{
						TableGet tableInternal = GetTableInternal(connectionInfo.Database, tableReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved table '" + tableReference.Name + "'";
						itemResult.Data = tableInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving table '" + tableReference.Name + "': " + ex.Message;
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
				response.Message = $"Processed {tables.Count} table(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = tables.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get tables", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = tables.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameTables(string? connectionName, List<TableRename> tables, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, tables, options, "Rename", "Renamed", "tables", (TableRename item) => item.CurrentName, delegate(BatchItemContext<TableRename> ctx)
		{
			RenameTableInternal(ctx.Connection, ctx.Item.CurrentName, ctx.Item.NewName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed table '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed table '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> MarkAsDateTables(string? connectionName, List<MarkAsDateTableDefinition> definitions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "MarkAsDateTable",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (definitions == null || !definitions.Any())
		{
			response.Success = false;
			response.Message = "No definitions provided for MarkAsDateTable";
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
				for (int i = 0; i < definitions.Count; i++)
				{
					MarkAsDateTableDefinition markAsDateTableDefinition = definitions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = markAsDateTableDefinition.TableName
					};
					try
					{
						MarkAsDateTableInternal(connectionInfo, markAsDateTableDefinition);
						itemResult.Success = true;
						itemResult.Message = "Successfully marked table '" + markAsDateTableDefinition.TableName + "' as date table";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Marked table '" + markAsDateTableDefinition.TableName + "' as date table");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error marking table '" + markAsDateTableDefinition.TableName + "' as date table: " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, definitions.Count, ref successCount, ref failureCount, "Marked", "table(s) as date tables");
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
				response.Message = "MarkAsDateTable operation failed: " + ex2.Message;
				failureCount = definitions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = definitions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static IReadOnlyList<ResolvedFieldEntry> ValidateFieldParameterDefinition(FieldParameterDefinition def, IConnectionInfo conn)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Field parameter definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Field parameter name is required", ErrorSource.User);
		}
		if (def.Fields == null || def.Fields.Count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("At least one field is required", ErrorSource.User);
		}
		Model model = conn.Database.Model;
		if (model.Tables.Contains(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + def.Name + "' already exists", ErrorSource.User);
		}
		HashSet<(string, string, string)> hashSet = new HashSet<(string, string, string)>(FieldReferenceComparer.Instance);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<ResolvedFieldEntry> list = new List<ResolvedFieldEntry>(def.Fields.Count);
		foreach (FieldParameterFieldDefinition field in def.Fields)
		{
			if (string.IsNullOrWhiteSpace(field.Name))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Field Name is required", ErrorSource.User);
			}
			bool num = string.Equals(field.ObjectType, "Column", StringComparison.OrdinalIgnoreCase);
			bool flag = string.Equals(field.ObjectType, "Measure", StringComparison.OrdinalIgnoreCase);
			if (!num && !flag)
			{
				throw new McpExceptionWithSource("Field ObjectType must be 'Column' or 'Measure', but was '" + field.ObjectType + "'", ErrorSource.User, "Field ObjectType must be 'Column' or 'Measure'.");
			}
			string text;
			if (num)
			{
				if (string.IsNullOrWhiteSpace(field.TableName))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Field TableName is required for Column references", ErrorSource.User);
				}
				if (((model.Tables.Find(field.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + field.TableName + "' not found", ErrorSource.User)).Columns.Find(field.Name) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{field.Name}' not found in table '{field.TableName}'", ErrorSource.User)) is RowNumberColumn)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"Row-number column '{field.Name}' in table '{field.TableName}' cannot be used as a field parameter reference", ErrorSource.User);
				}
				text = field.TableName;
			}
			else if (string.IsNullOrWhiteSpace(field.TableName))
			{
				text = MeasureOperations.FindMeasureInternal(model, field.Name).Table.Name;
			}
			else
			{
				if ((model.Tables.Find(field.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + field.TableName + "' not found", ErrorSource.User)).Measures.Find(field.Name) == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure '{field.Name}' not found in table '{field.TableName}'", ErrorSource.User);
				}
				text = field.TableName;
			}
			string text2 = (num ? "Column" : "Measure");
			(string, string, string) item = (text, field.Name, text2);
			if (!hashSet.Add(item))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Duplicate field reference: '{text}'['{field.Name}'] (ObjectType: {text2})", ErrorSource.User);
			}
			string text3 = (string.IsNullOrWhiteSpace(field.DisplayName) ? field.Name : field.DisplayName);
			if (!hashSet2.Add(text3))
			{
				throw new McpExceptionWithSource("Duplicate display name '" + text3 + "' — field parameter column values must be unique", ErrorSource.User, "Duplicate field parameter display name supplied.");
			}
			list.Add(new ResolvedFieldEntry(text, field.Name, text2, field.DisplayName));
		}
		return list;
	}

	internal static string GenerateFieldParameterDax(IReadOnlyList<ResolvedFieldEntry> resolvedFields)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("{\n");
		for (int i = 0; i < resolvedFields.Count; i++)
		{
			ResolvedFieldEntry resolvedFieldEntry = resolvedFields[i];
			string value = (string.IsNullOrWhiteSpace(resolvedFieldEntry.DisplayName) ? resolvedFieldEntry.Name : resolvedFieldEntry.DisplayName).Replace("\"", "\"\"");
			string value2 = resolvedFieldEntry.TableName.Replace("'", "''");
			string value3 = resolvedFieldEntry.Name.Replace("]", "]]");
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(24, 4, stringBuilder2);
			handler.AppendLiteral("    (\"");
			handler.AppendFormatted(value);
			handler.AppendLiteral("\", NAMEOF('");
			handler.AppendFormatted(value2);
			handler.AppendLiteral("'[");
			handler.AppendFormatted(value3);
			handler.AppendLiteral("]), ");
			handler.AppendFormatted(i);
			handler.AppendLiteral(")");
			stringBuilder2.Append(ref handler);
			if (i < resolvedFields.Count - 1)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append('\n');
		}
		stringBuilder.Append('}');
		return stringBuilder.ToString();
	}

	internal static string GenerateFieldParameterDax(FieldParameterDefinition def)
	{
		return GenerateFieldParameterDax(def.Fields.Select(delegate(FieldParameterFieldDefinition f)
		{
			if (f.TableName == null)
			{
				throw new ArgumentException("Field '" + f.Name + "' has a null TableName; call ValidateFieldParameterDefinition first or use the resolved-entries overload.", "def");
			}
			return new ResolvedFieldEntry(f.TableName, f.Name, f.ObjectType, f.DisplayName);
		}).ToList());
	}

	internal static TableOperationResult CreateFieldParameterTableInternal(IConnectionInfo info, FieldParameterDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Field parameter definition cannot be null", ErrorSource.User);
		}
		string expression = GenerateFieldParameterDax(ValidateFieldParameterDefinition(def, info));
		Database database = info.Database;
		Table table = new Table
		{
			Name = def.Name
		};
		table.LineageTag = Guid.NewGuid().ToString();
		Partition metadataObject = new Partition
		{
			Name = def.Name,
			Mode = ModeType.Import,
			Source = new CalculatedPartitionSource
			{
				Expression = expression
			}
		};
		table.Partitions.Add(metadataObject);
		string name = def.Name;
		string name2 = def.Name + " Fields";
		string name3 = def.Name + " Order";
		CalculatedTableColumn calculatedTableColumn = new CalculatedTableColumn
		{
			Name = name3,
			SourceColumn = "[Value3]",
			DataType = DataType.Int64,
			FormatString = "0",
			IsHidden = true,
			SummarizeBy = AggregateFunction.Sum,
			LineageTag = Guid.NewGuid().ToString()
		};
		table.Columns.Add(calculatedTableColumn);
		CalculatedTableColumn calculatedTableColumn2 = new CalculatedTableColumn
		{
			Name = name2,
			SourceColumn = "[Value2]",
			DataType = DataType.String,
			IsHidden = true,
			SummarizeBy = AggregateFunction.None,
			LineageTag = Guid.NewGuid().ToString(),
			SortByColumn = calculatedTableColumn
		};
		calculatedTableColumn2.ExtendedProperties.Add(new JsonExtendedProperty
		{
			Name = "ParameterMetadata",
			Value = "{\"version\":3,\"kind\":2}"
		});
		table.Columns.Add(calculatedTableColumn2);
		CalculatedTableColumn calculatedTableColumn3 = new CalculatedTableColumn
		{
			Name = name,
			SourceColumn = "[Value1]",
			DataType = DataType.String,
			IsHidden = false,
			SummarizeBy = AggregateFunction.None,
			LineageTag = Guid.NewGuid().ToString(),
			SortByColumn = calculatedTableColumn
		};
		table.Columns.Add(calculatedTableColumn3);
		database.Model.Tables.Add(table);
		try
		{
			RelatedColumnDetails relatedColumnDetails = new RelatedColumnDetails();
			GroupByColumn metadataObject2 = new GroupByColumn
			{
				GroupingColumn = calculatedTableColumn2
			};
			relatedColumnDetails.GroupByColumns.Add(metadataObject2);
			calculatedTableColumn3.RelatedColumnDetails = relatedColumnDetails;
			TransactionOperations.RecordOperation(info, "Created field parameter table '" + def.Name + "' in model " + database.Model.Name);
			ConnectionOperations.SaveChangesWithRollback(info, "create table", OperationType.Create);
		}
		catch
		{
			database.Model.Tables.Remove(table);
			throw;
		}
		return CreateTableOperationResult(table);
	}
}
