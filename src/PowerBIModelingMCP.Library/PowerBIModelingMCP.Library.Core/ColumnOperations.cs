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

public static class ColumnOperations
{
	public class ColumnOperationResult
	{
		public string State { get; set; } = string.Empty;

		public string? ErrorMessage { get; set; }

		public string ColumnName { get; set; } = string.Empty;

		public string TableName { get; set; } = string.Empty;

		public string ColumnType { get; set; } = string.Empty;

		public bool HasChanges { get; set; }

		public List<string>? Warnings { get; set; } = new List<string>();
	}

	public class CalculatedColumnValidationResult
	{
		public bool IsValid { get; set; }

		public string? ObjectState { get; set; }

		public string? ErrorMessage { get; set; }

		public string Expression { get; set; } = string.Empty;

		public string? Message { get; set; }

		public long ValidationTimeMs { get; set; }
	}

	internal static PostCommitDaxValidator.Target? ResolveColumnForValidation(IConnectionInfo conn, ColumnDefinition def)
	{
		if (def == null || string.IsNullOrEmpty(def.Name) || string.IsNullOrEmpty(def.TableName))
		{
			return null;
		}
		Database database = conn?.Database;
		if (database == null)
		{
			return null;
		}
		Column column = database.Model.Tables.Find(def.TableName)?.Columns.Find(def.Name);
		if (column == null)
		{
			return null;
		}
		List<PostCommitDaxValidator.Check> checks = new List<PostCommitDaxValidator.Check>
		{
			new PostCommitDaxValidator.Check(string.Empty, column.State.ToString(), column.ErrorMessage)
		};
		return new PostCommitDaxValidator.Target("Column", $"'{column.Name}' on table '{column.Table?.Name}'", checks);
	}

	private static void ValidateBase(ColumnBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (isCreate && string.IsNullOrWhiteSpace(def.Expression) && string.IsNullOrWhiteSpace(def.SourceColumn))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Either Expression or SourceColumn must be provided for creation", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(def.Expression) && !string.IsNullOrWhiteSpace(def.SourceColumn))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot specify both Expression and SourceColumn", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(def.DataType) && !Enum.TryParse<DataType>(def.DataType, ignoreCase: true, out var _))
		{
			string[] names = Enum.GetNames(typeof(DataType));
			throw new McpExceptionWithSource("Invalid DataType '" + def.DataType + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid DataType supplied. Valid values are: " + string.Join(", ", names) + ".");
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
		if (!string.IsNullOrWhiteSpace(def.ExpressionContext))
		{
			if (!Enum.TryParse<ExpressionContext>(def.ExpressionContext, ignoreCase: true, out var _))
			{
				string[] names2 = Enum.GetNames(typeof(ExpressionContext));
				throw new McpExceptionWithSource("Invalid ExpressionContext '" + def.ExpressionContext + "'. Valid values are: " + string.Join(", ", names2), ErrorSource.User, "Invalid ExpressionContext supplied. Valid values are: " + string.Join(", ", names2) + ".");
			}
			if (isCreate && string.IsNullOrWhiteSpace(def.Expression))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("ExpressionContext can only be set on calculated columns (columns with an Expression)", ErrorSource.User);
			}
		}
		if (def.AlternateOf != null)
		{
			ValidateAlternateOfStructure(def.AlternateOf);
		}
		if (def.GroupByColumns != null && def.GroupByColumns.Count > 0)
		{
			ValidateGroupByColumnsStructure(def.GroupByColumns, def.Name);
		}
	}

	private static void ValidateAlternateOfStructure(AlternateOfDefinition alternateOf)
	{
		if (string.IsNullOrWhiteSpace(alternateOf.BaseTable))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("AlternateOf BaseTable is required. Specify the name of the table that contains the source data for this aggregation.", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(alternateOf.BaseColumn))
		{
			if (!string.IsNullOrWhiteSpace(alternateOf.Summarization) && !alternateOf.Summarization.Equals("Count", StringComparison.OrdinalIgnoreCase))
			{
				throw new McpExceptionWithSource("When creating a table reference (BaseColumn not specified), Summarization must be 'Count'. Current value: '" + alternateOf.Summarization + "'. For other summarization types (Sum, Min, Max, GroupBy), specify a BaseColumn to reference a specific column.", ErrorSource.User, "When creating an AlternateOf table reference (BaseColumn not specified), Summarization must be 'Count'. For other supported summarizations, specify BaseColumn.");
			}
		}
		else if (!string.IsNullOrWhiteSpace(alternateOf.Summarization))
		{
			string[] array = new string[5] { "GroupBy", "Sum", "Count", "Min", "Max" };
			if (!array.Contains<string>(alternateOf.Summarization, StringComparer.OrdinalIgnoreCase))
			{
				throw new McpExceptionWithSource($"Invalid Summarization '{alternateOf.Summarization}' for column reference. Valid values: {string.Join(", ", array)}. " + "Use 'GroupBy' for dimensional data, 'Sum'/'Min'/'Max' for numeric aggregations, or 'Count' for counting rows.", ErrorSource.User, "Invalid Summarization supplied for an AlternateOf column reference. Valid values: " + string.Join(", ", array) + ".");
			}
		}
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, string> annotation in alternateOf.Annotations)
		{
			if (string.IsNullOrWhiteSpace(annotation.Key))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("AlternateOf annotation keys cannot be null or empty. Each annotation must have a valid key-value pair.", ErrorSource.User);
			}
			if (list.Contains(annotation.Key))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Duplicate AlternateOf annotation key: '" + annotation.Key + "'. Each annotation key must be unique within the AlternateOf definition.", ErrorSource.User);
			}
			list.Add(annotation.Key);
		}
	}

	private static void ValidateAlternateOf(AlternateOfDefinition alternateOf, string currentTableName, Database database)
	{
		ValidateAlternateOfStructure(alternateOf);
		if (alternateOf.BaseTable.Equals(currentTableName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("AlternateOf BaseTable cannot reference the same table ('" + currentTableName + "'). AlternateOf relationships must establish cross-table relationships. Specify a different table name for BaseTable.", ErrorSource.User);
		}
		Table table = database.Model.Tables.Find(alternateOf.BaseTable);
		if (table == null)
		{
			List<string> list = (from t in database.Model.Tables
				where !t.Name.Equals(currentTableName, StringComparison.OrdinalIgnoreCase)
				select t.Name).Take(5).ToList();
			string text = (list.Any() ? (" Available tables (excluding current table): " + string.Join(", ", list) + ((database.Model.Tables.Count > list.Count + 1) ? "..." : "")) : " No other tables are available in the model.");
			throw McpExceptionWithSource.FromTelemetrySafeMessage("AlternateOf BaseTable references non-existent table '" + alternateOf.BaseTable + "'." + text, ErrorSource.User);
		}
		int count = table.Partitions.Count;
		if (count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("AlternateOf BaseTable '" + alternateOf.BaseTable + "' has no partitions. AlternateOf relationships require the base table to have exactly one partition in DirectQuery mode.", ErrorSource.User);
		}
		if (count > 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"AlternateOf BaseTable '{alternateOf.BaseTable}' has {count} partitions. " + "AlternateOf relationships require the base table to have exactly one partition in DirectQuery mode.", ErrorSource.User);
		}
		Partition partition = table.Partitions[0];
		if (partition.Mode != ModeType.DirectQuery)
		{
			string value = partition.Mode.ToString();
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"AlternateOf BaseTable '{alternateOf.BaseTable}' partition is in '{value}' mode. " + "AlternateOf relationships require the base table to have exactly one partition in DirectQuery mode. This is a platform limitation for AlternateOf functionality.", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(alternateOf.BaseColumn))
		{
			return;
		}
		Column column = table.Columns.Find(alternateOf.BaseColumn);
		if (column == null)
		{
			List<string> list2 = (from c in table.Columns
				where !(c is RowNumberColumn)
				select c.Name).Take(5).ToList();
			string value2 = (list2.Any() ? $" Available columns in table '{alternateOf.BaseTable}': {string.Join(", ", list2)}{((table.Columns.Count > list2.Count) ? "..." : "")}" : (" Table '" + alternateOf.BaseTable + "' has no accessible columns."));
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"AlternateOf BaseColumn references non-existent column '{alternateOf.BaseColumn}' in table '{alternateOf.BaseTable}'.{value2}", ErrorSource.User);
		}
		string value3 = column.DataType.ToString();
		if (!string.IsNullOrWhiteSpace(alternateOf.Summarization))
		{
			bool flag = new string[3] { "Sum", "Min", "Max" }.Contains<string>(alternateOf.Summarization, StringComparer.OrdinalIgnoreCase);
			bool flag2 = Enumerable.Contains(new string[4] { "Int64", "Double", "Decimal", "Currency" }, value3);
			if (flag && !flag2)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Summarization '{alternateOf.Summarization}' is typically used with numeric columns, but column '{alternateOf.BaseColumn}' has data type '{value3}'. " + "Consider using 'GroupBy' for non-numeric columns or 'Count' for counting operations.", ErrorSource.User);
			}
		}
	}

	private static void ValidateGroupByColumnsStructure(List<string> groupByColumns, string columnName)
	{
		if (groupByColumns.Where((string c) => string.IsNullOrWhiteSpace(c)).ToList().Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("GroupByColumns cannot contain null or empty column names. All referenced columns must have valid names.", ErrorSource.User);
		}
		List<string> list = (from g in groupByColumns.GroupBy<string, string>((string c) => c, StringComparer.OrdinalIgnoreCase)
			where g.Count() > 1
			select g.Key).ToList();
		if (list.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("GroupByColumns contains duplicate column references: " + string.Join(", ", list) + ". Each column can only be referenced once.", ErrorSource.User);
		}
		if (groupByColumns.Any((string c) => c.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column '" + columnName + "' cannot reference itself in GroupByColumns. GroupByColumns must reference other columns in the same table.", ErrorSource.User);
		}
	}

	private static void ValidateGroupByColumns(List<string> groupByColumns, string tableName, string columnName, Database database)
	{
		ValidateGroupByColumnsStructure(groupByColumns, columnName);
		Table table = database.Model.Tables.Find(tableName);
		if (table == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found for GroupByColumns validation.", ErrorSource.User);
		}
		foreach (string groupByColumn in groupByColumns)
		{
			Column column = table.Columns.Find(groupByColumn);
			if (column == null)
			{
				List<string> list = (from c in table.Columns
					where !(c is RowNumberColumn)
					select c.Name).ToList();
				string value = (list.Any() ? $" Available columns in table '{tableName}': {string.Join(", ", list)}{((table.Columns.Count > list.Count) ? "..." : "")}" : (" Table '" + tableName + "' has no accessible columns."));
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"GroupByColumns references non-existent column '{groupByColumn}' in table '{tableName}'.{value}", ErrorSource.User);
			}
			if (column is RowNumberColumn)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("GroupByColumns cannot reference RowNumber column '" + groupByColumn + "'. RowNumber columns are system-generated and not suitable for grouping operations.", ErrorSource.User);
			}
		}
	}

	internal static void ValidateAndApplyGroupByColumns(IConnectionInfo info, string tableName, string columnName, List<string> groupByColumns)
	{
		ValidateGroupByColumns(groupByColumns, tableName, columnName, info.Database);
		ApplyGroupByColumns((info.Database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found during GroupByColumns assignment", ErrorSource.User)).Columns.Find(columnName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{columnName}' not found in table '{tableName}' during GroupByColumns assignment", ErrorSource.User), groupByColumns);
	}

	private static void ApplyGroupByColumns(Column column, List<string> groupByColumns)
	{
		try
		{
			column.RelatedColumnDetails = null;
			if (groupByColumns == null || groupByColumns.Count <= 0)
			{
				return;
			}
			RelatedColumnDetails relatedColumnDetails = new RelatedColumnDetails();
			foreach (string groupByColumn2 in groupByColumns)
			{
				Column column2 = column.Table.Columns.Find(groupByColumn2);
				if (column2 == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"GroupByColumns references non-existent column '{groupByColumn2}' in table '{column.Table.Name}'.", ErrorSource.User);
				}
				GroupByColumn groupByColumn = new GroupByColumn();
				groupByColumn.GroupingColumn = column2;
				relatedColumnDetails.GroupByColumns.Add(groupByColumn);
			}
			column.RelatedColumnDetails = relatedColumnDetails;
		}
		catch (Exception ex)
		{
			throw new McpExceptionWithSource("Failed to apply GroupByColumns to column '" + column.Name + "': " + ex.Message, ex, null, "Failed to apply GroupByColumns to column '" + column.Name + "'; see inner error details.");
		}
	}

	private static List<string>? ExtractGroupByColumns(Column column)
	{
		if (column.RelatedColumnDetails?.GroupByColumns == null || !column.RelatedColumnDetails.GroupByColumns.Any())
		{
			return null;
		}
		return column.RelatedColumnDetails.GroupByColumns.Select((GroupByColumn gbc) => gbc.GroupingColumn.Name).ToList();
	}

	private static bool CompareGroupByColumns(List<string>? current, List<string>? updated)
	{
		if (current == null && updated == null)
		{
			return true;
		}
		if (current == null || updated == null)
		{
			return false;
		}
		if (current.Count != updated.Count)
		{
			return false;
		}
		return current.SequenceEqual<string>(updated, StringComparer.OrdinalIgnoreCase);
	}

	private static bool ApplyIsKeyUpdate(Table table, Column col, bool isKey)
	{
		if (!isKey)
		{
			if (!col.IsKey)
			{
				return false;
			}
			col.IsKey = false;
			return true;
		}
		List<Column> list = (from c in table.Columns.Where((Column c) => c.IsKey).ToList()
			where !c.Name.Equals(col.Name, StringComparison.OrdinalIgnoreCase)
			select c).ToList();
		if (list.Count == 0 && col.IsKey)
		{
			return false;
		}
		foreach (Column item in list.Where((Column c) => !(c is RowNumberColumn)))
		{
			item.IsKey = false;
		}
		if (!col.IsKey)
		{
			col.IsKey = true;
		}
		return true;
	}

	internal static void ValidateSingleKeyColumnDefinitionPerTable(IEnumerable<ColumnBase>? columns, string context, string? defaultTableName = null)
	{
		if (columns == null)
		{
			return;
		}
		List<IGrouping<string, ColumnBase>> source = (			from g in columns.Where((ColumnBase c) => c?.IsKey == true).GroupBy<ColumnBase, string>((ColumnBase c) => c.TableName ?? defaultTableName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
			where g.Count() > 1
			select g).ToList();
		if (!source.Any())
		{
			return;
		}
		IEnumerable<string> values = source.Select(delegate(IGrouping<string, ColumnBase> g)
		{
			string text = (string.IsNullOrWhiteSpace(g.Key) ? "(missing table)" : g.Key);
			string text2 = string.Join(", ", g.Select((ColumnBase c) => c.Name ?? "(missing name)"));
			return "table '" + text + "': " + text2;
		});
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Only one column per table can be designated as a key column in " + context + ". Found multiple key columns for " + string.Join("; ", values), ErrorSource.User);
	}

	private static VariationDefinition? ExtractVariation(Column column, Database database)
	{
		if (column.Variations == null || !column.Variations.Any())
		{
			return null;
		}
		if (column.Variations.Count > 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{column.Name}' in table '{column.Table.Name}' has {column.Variations.Count} variations. Only single variations are supported.", ErrorSource.User);
		}
		Variation variation = column.Variations[0];
		ValidateVariation(variation, database);
		VariationDefinition variationDefinition = new VariationDefinition();
		if (variation.Relationship != null)
		{
			variationDefinition.RelationshipName = variation.Relationship.Name;
			Table toTable = variation.Relationship.ToTable;
			variationDefinition.HiddenTableName = toTable.Name;
			if (toTable.Hierarchies.Any())
			{
				Hierarchy hierarchy = toTable.Hierarchies[0];
				variationDefinition.HierarchyName = hierarchy.Name;
				variationDefinition.HierarchyLevelNames = ExtractHierarchyLevelNames(toTable, hierarchy.Name);
			}
		}
		return variationDefinition;
	}

	private static void ValidateVariation(Variation variation, Database database)
	{
		if (variation.Relationship == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Variation must have a Relationship property. Other variation types are not supported in the simplified scenario.", ErrorSource.User);
		}
		Relationship relationship = variation.Relationship;
		if (database.Model.Relationships.Find(relationship.Name) == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + relationship.Name + "' referenced by variation does not exist in the model.", ErrorSource.User);
		}
		Table toTable = relationship.ToTable;
		if (toTable == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + relationship.Name + "' does not have a valid ToTable.", ErrorSource.User);
		}
		if (!toTable.IsHidden)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + toTable.Name + "' referenced by variation relationship must be hidden.", ErrorSource.User);
		}
		if (toTable.Hierarchies == null || !toTable.Hierarchies.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Hidden table '" + toTable.Name + "' must contain at least one hierarchy.", ErrorSource.User);
		}
	}

	private static List<string> ExtractHierarchyLevelNames(Table table, string hierarchyName)
	{
		Hierarchy obj = table.Hierarchies.Find(hierarchyName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in table '{table.Name}'.", ErrorSource.User);
		List<string> list = new List<string>();
		foreach (Level level in obj.Levels)
		{
			list.Add(level.Name);
		}
		return list;
	}

	private static void ApplyAlternateOf(Column column, AlternateOfDefinition alternateOf)
	{
		try
		{
			AlternateOf alternateOf2 = new AlternateOf();
			if (!string.IsNullOrWhiteSpace(alternateOf.BaseColumn))
			{
				Table table = column.Table.Model.Tables.Find(alternateOf.BaseTable);
				if (table == null)
				{
					string text = string.Join(", ", column.Table.Model.Tables.Select((Table t) => t.Name));
					throw McpExceptionWithSource.FromTelemetrySafeMessage("AlternateOf BaseTable '" + alternateOf.BaseTable + "' not found. Available tables: " + text, ErrorSource.User);
				}
				Column column2 = table.Columns.Find(alternateOf.BaseColumn);
				if (column2 == null)
				{
					string value = string.Join(", ", table.Columns.Select((Column c) => c.Name));
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"AlternateOf BaseColumn '{alternateOf.BaseColumn}' not found in table '{alternateOf.BaseTable}'. Available columns: {value}", ErrorSource.User);
				}
				alternateOf2.BaseColumn = column2;
			}
			else
			{
				Table table2 = column.Table.Model.Tables.Find(alternateOf.BaseTable);
				if (table2 == null)
				{
					string text2 = string.Join(", ", column.Table.Model.Tables.Select((Table t) => t.Name));
					throw McpExceptionWithSource.FromTelemetrySafeMessage("AlternateOf BaseTable '" + alternateOf.BaseTable + "' not found. Available tables: " + text2, ErrorSource.User);
				}
				alternateOf2.BaseTable = table2;
			}
			string text3 = ((!string.IsNullOrWhiteSpace(alternateOf.BaseColumn)) ? (string.IsNullOrWhiteSpace(alternateOf.Summarization) ? "GroupBy" : alternateOf.Summarization) : (string.IsNullOrWhiteSpace(alternateOf.Summarization) ? "Count" : alternateOf.Summarization));
			if (!Enum.TryParse<SummarizationType>(text3, ignoreCase: true, out var result))
			{
				throw new McpExceptionWithSource("Invalid AlternateOf Summarization '" + text3 + "'. Valid values: GroupBy, Sum, Count, Min, Max", ErrorSource.User, "Invalid AlternateOf Summarization supplied. Valid values: GroupBy, Sum, Count, Min, Max.");
			}
			alternateOf2.Summarization = result;
			foreach (KeyValuePair<string, string> annotation in alternateOf.Annotations)
			{
				alternateOf2.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
			column.AlternateOf = alternateOf2;
		}
		catch (Exception ex)
		{
			throw new McpExceptionWithSource("Failed to apply AlternateOf to column '" + column.Name + "': " + ex.Message, ex, null, "Failed to apply AlternateOf to column '" + column.Name + "'; see inner error details.");
		}
	}

	private static AlternateOfDefinition? ExtractAlternateOf(Column column)
	{
		if (column.AlternateOf == null)
		{
			return null;
		}
		AlternateOfDefinition alternateOfDefinition = new AlternateOfDefinition();
		if (column.AlternateOf.BaseTable != null)
		{
			alternateOfDefinition.BaseTable = column.AlternateOf.BaseTable.Name;
		}
		if (column.AlternateOf.BaseColumn != null)
		{
			alternateOfDefinition.BaseColumn = column.AlternateOf.BaseColumn.Name;
		}
		alternateOfDefinition.Summarization = column.AlternateOf.Summarization.ToString();
		foreach (Annotation annotation in column.AlternateOf.Annotations)
		{
			alternateOfDefinition.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		return alternateOfDefinition;
	}

	private static bool CompareAlternateOf(AlternateOfDefinition? current, AlternateOfDefinition? updated)
	{
		if (current == null && updated == null)
		{
			return true;
		}
		if (current == null || updated == null)
		{
			return false;
		}
		if (current.BaseTable == updated.BaseTable && current.BaseColumn == updated.BaseColumn && current.Summarization == updated.Summarization)
		{
			return current.Annotations.SequenceEqual(updated.Annotations);
		}
		return false;
	}

	public static async Task<(List<TableColumnList> Columns, int TotalCount)> ListColumns(string? connectionName, List<string>? tableNames, int? maxResults)
	{
		(List<TableColumnList> Columns, int TotalCount) result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				int totalCount;
				List<TableColumnList> item = ListColumnsInternal(connectionInfo.Database, tableNames, maxResults, out totalCount);
				AuditEvent.Default.Emit("list columns", success: true, OperationType.Read, connectionInfo);
				result = (Columns: item, TotalCount: totalCount);
			}
			catch
			{
				AuditEvent.Default.Emit("list columns", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<TableColumnList> ListColumnsInternal(Database db, List<string>? tableNames, int? maxResults, out int totalCount)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		IEnumerable<Table> source;
		if (tableNames != null && tableNames.Any())
		{
			List<Table> list = new List<Table>();
			foreach (string tableName in tableNames)
			{
				Table table = db.Model.Tables.Find(tableName);
				if (table == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
				}
				list.Add(table);
			}
			source = list;
		}
		else
		{
			source = db.Model.Tables;
		}
		List<TableColumnList> list2 = (from t in source
			select new TableColumnList
			{
				TableName = t.Name,
				Columns = (from c in t.Columns
					where !(c is RowNumberColumn)
					select new ColumnList
					{
						Name = c.Name,
						Description = ((!string.IsNullOrEmpty(c.Description)) ? c.Description : null),
						DataType = c.DataType.ToString(),
						IsCalculated = ((c is CalculatedColumn calculatedColumn && !string.IsNullOrEmpty(calculatedColumn.Expression)) ? new bool?(true) : ((bool?)null)),
						DisplayFolder = ((!string.IsNullOrEmpty(c.DisplayFolder)) ? c.DisplayFolder : null),
						IsHidden = (c.IsHidden ? new bool?(true) : ((bool?)null)),
						SummarizeBy = ((c.SummarizeBy != AggregateFunction.Default) ? c.SummarizeBy.ToString() : null),
						FormatString = ((!string.IsNullOrWhiteSpace(c.FormatString)) ? c.FormatString : null)
					}).ToList()
			} into g
			where g.Columns.Any()
			select g).ToList();
		totalCount = list2.Sum((TableColumnList g) => g.Columns.Count);
		if (maxResults.HasValue && maxResults.Value > 0)
		{
			int num = maxResults.Value;
			List<TableColumnList> list3 = new List<TableColumnList>();
			foreach (TableColumnList item in list2)
			{
				if (num <= 0)
				{
					break;
				}
				if (item.Columns.Count <= num)
				{
					list3.Add(item);
					num -= item.Columns.Count;
					continue;
				}
				list3.Add(new TableColumnList
				{
					TableName = item.TableName,
					Columns = item.Columns.Take(num).ToList()
				});
				num = 0;
			}
			return list3;
		}
		return list2;
	}

	internal static ColumnGet GetColumnInternal(Database db, string tableName, string columnName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(columnName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("columnName is required", ErrorSource.User);
		}
		Column column = (db.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User)).Columns.Find(columnName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{columnName}' not found in table '{tableName}'", ErrorSource.User);
		if (column is RowNumberColumn)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column '" + columnName + "' is a RowNumber column and is not accessible", ErrorSource.User);
		}
		string text = ((column is DataColumn) ? "Data" : ((column is CalculatedColumn) ? "Calculated" : ((column is CalculatedTableColumn) ? "CalculatedTableColumn" : ((!(column is RowNumberColumn)) ? "Unknown" : "RowNumber"))));
		string columnType = text;
		ColumnGet columnGet = new ColumnGet
		{
			TableName = tableName,
			Name = column.Name,
			SourceColumn = (column as DataColumn)?.SourceColumn,
			Expression = (column as CalculatedColumn)?.Expression,
			ExpressionContext = (column as CalculatedColumn)?.ExpressionContext.ToString(),
			DataType = column.DataType.ToString(),
			DataCategory = column.DataCategory,
			FormatString = column.FormatString,
			SummarizeBy = column.SummarizeBy.ToString(),
			DefaultLabel = column.IsDefaultLabel,
			DefaultImage = column.IsDefaultImage,
			IsHidden = column.IsHidden,
			IsUnique = column.IsUnique,
			IsKey = column.IsKey,
			IsNullable = column.IsNullable,
			DisplayFolder = column.DisplayFolder,
			SortByColumn = column.SortByColumn?.Name,
			SourceProviderType = column.SourceProviderType,
			Description = column.Description,
			IsAvailableInMDX = column.IsAvailableInMDX,
			Alignment = column.Alignment.ToString(),
			TableDetailPosition = column.TableDetailPosition,
			LineageTag = column.LineageTag,
			SourceLineageTag = column.SourceLineageTag,
			ColumnType = columnType,
			State = column.State.ToString(),
			ErrorMessage = column.ErrorMessage,
			Annotations = new List<KeyValuePair<string, string>>()
		};
		if (column.Annotations != null)
		{
			foreach (Annotation annotation in column.Annotations)
			{
				columnGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name ?? string.Empty, annotation.Value ?? string.Empty));
			}
		}
		columnGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromColumn(column);
		columnGet.AlternateOf = ExtractAlternateOf(column);
		columnGet.GroupByColumns = ExtractGroupByColumns(column);
		columnGet.Variation = ExtractVariation(column, db);
		return columnGet;
	}

	public static async Task<string> ExportTMDL(string? connectionName, string tableName, string columnName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(columnName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("columnName is required", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				Column obj = (connectionInfo.Database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User)).Columns.Find(columnName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{columnName}' not found in table '{tableName}'", ErrorSource.User);
				if (obj is RowNumberColumn)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Column '" + columnName + "' is a RowNumber column and is not accessible", ErrorSource.User);
				}
				string item = ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(obj, options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
				AuditEvent.Default.Emit("export column to TMDL", success: true, OperationType.Read, connectionInfo);
				result = item;
			}
			catch
			{
				AuditEvent.Default.Emit("export column to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static ColumnOperationResult CreateColumnInternal(IConnectionInfo info, ColumnDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateBase(def, isCreate: true);
		Table table = info.Database.Model.Tables.Find(def.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + def.TableName + "' not found", ErrorSource.User);
		if (table.Columns.Contains(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column [{def.Name}] already exists in table '{def.TableName}'", ErrorSource.User);
		}
		Column column = (string.IsNullOrWhiteSpace(def.Expression) ? ((Column)new DataColumn
		{
			Name = def.Name,
			SourceColumn = def.SourceColumn
		}) : ((Column)new CalculatedColumn
		{
			Name = def.Name,
			Expression = def.Expression
		}));
		if (!string.IsNullOrWhiteSpace(def.ExpressionContext) && column is CalculatedColumn calculatedColumn && Enum.TryParse<ExpressionContext>(def.ExpressionContext, ignoreCase: true, out var result))
		{
			calculatedColumn.ExpressionContext = result;
		}
		if (!string.IsNullOrWhiteSpace(def.DataType))
		{
			if (!Enum.TryParse<DataType>(def.DataType, ignoreCase: true, out var result2))
			{
				throw new McpExceptionWithSource("Invalid DataType '" + def.DataType + "'. Valid values are: " + string.Join(", ", Enum.GetNames(typeof(DataType))), ErrorSource.User, "Invalid DataType supplied. Valid values are: " + string.Join(", ", Enum.GetNames(typeof(DataType))) + ".");
			}
			column.DataType = result2;
		}
		column.DataCategory = def.DataCategory;
		column.FormatString = def.FormatString;
		if (!string.IsNullOrWhiteSpace(def.SummarizeBy))
		{
			if (!Enum.TryParse<AggregateFunction>(def.SummarizeBy, ignoreCase: true, out var result3))
			{
				string[] names = Enum.GetNames(typeof(AggregateFunction));
				throw new McpExceptionWithSource("Invalid SummarizeBy '" + def.SummarizeBy + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid SummarizeBy supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			column.SummarizeBy = result3;
		}
		if (def.DefaultLabel.HasValue)
		{
			column.IsDefaultLabel = def.DefaultLabel.Value;
		}
		if (def.DefaultImage.HasValue)
		{
			column.IsDefaultImage = def.DefaultImage.Value;
		}
		if (def.IsHidden.HasValue)
		{
			column.IsHidden = def.IsHidden.Value;
		}
		if (def.IsUnique.HasValue)
		{
			column.IsUnique = def.IsUnique.Value;
		}
		if (def.IsKey.HasValue)
		{
			ApplyIsKeyUpdate(table, column, def.IsKey.Value);
		}
		if (def.IsNullable.HasValue)
		{
			column.IsNullable = def.IsNullable.Value;
		}
		column.DisplayFolder = def.DisplayFolder;
		if (!string.IsNullOrWhiteSpace(def.SortByColumn))
		{
			Column column2 = table.Columns.Find(def.SortByColumn);
			if (column2 == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"SortByColumn '{def.SortByColumn}' not found in table '{def.TableName}'", ErrorSource.User);
			}
			column.SortByColumn = column2;
		}
		column.SourceProviderType = def.SourceProviderType;
		column.Description = def.Description;
		if (def.IsAvailableInMDX.HasValue)
		{
			column.IsAvailableInMDX = def.IsAvailableInMDX.Value;
		}
		if (!string.IsNullOrWhiteSpace(def.Alignment))
		{
			if (!Enum.TryParse<Alignment>(def.Alignment, ignoreCase: true, out var result4))
			{
				throw new McpExceptionWithSource("Invalid Alignment '" + def.Alignment + "'. Valid values are: Default, Left, Right, Center", ErrorSource.User, "Invalid Alignment supplied. Valid values are: Default, Left, Right, Center.");
			}
			column.Alignment = result4;
		}
		if (def.TableDetailPosition.HasValue)
		{
			column.TableDetailPosition = def.TableDetailPosition.Value;
		}
		if (def.Annotations != null)
		{
			AnnotationHelpers.ApplyAnnotations(column, def.Annotations, (Column c) => c.Annotations);
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToColumn(column, def.ExtendedProperties);
		}
		column.LineageTag = (string.IsNullOrWhiteSpace(def.LineageTag) ? Guid.NewGuid().ToString() : def.LineageTag);
		if (!string.IsNullOrWhiteSpace(def.SourceLineageTag))
		{
			column.SourceLineageTag = def.SourceLineageTag;
		}
		table.Columns.Add(column);
		if (def.AlternateOf != null)
		{
			ValidateAlternateOf(def.AlternateOf, def.TableName, info.Database);
			ApplyAlternateOf(column, def.AlternateOf);
		}
		if (def.GroupByColumns != null && def.GroupByColumns.Count > 0)
		{
			ValidateGroupByColumns(def.GroupByColumns, def.TableName, def.Name, info.Database);
			ApplyGroupByColumns(column, def.GroupByColumns);
		}
		TransactionOperations.RecordOperation(info, $"Created column [{def.Name}] in table '{def.TableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "create column", OperationType.Create);
		string text = ((column is DataColumn) ? "Data" : ((column is CalculatedColumn) ? "Calculated" : ((column is CalculatedTableColumn) ? "CalculatedTableColumn" : ((!(column is RowNumberColumn)) ? "Unknown" : "RowNumber"))));
		string columnType = text;
		return new ColumnOperationResult
		{
			State = column.State.ToString(),
			ErrorMessage = column.ErrorMessage,
			ColumnName = column.Name,
			TableName = table.Name,
			ColumnType = columnType
		};
	}

	internal static ColumnOperationResult UpdateColumnInternal(IConnectionInfo info, ColumnDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateBase(update, isCreate: false);
		Database database = info.Database;
		Table table = database.Model.Tables.Find(update.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + update.TableName + "' not found", ErrorSource.User);
		Column column = table.Columns.Find(update.Name) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{update.Name}' not found in table '{update.TableName}'", ErrorSource.User);
		if (column is RowNumberColumn)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column '" + update.Name + "' is a RowNumber column and cannot be updated", ErrorSource.User);
		}
		bool flag = false;
		if (!string.IsNullOrWhiteSpace(update.Expression))
		{
			if (!(column is CalculatedColumn calculatedColumn))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot set Expression on a non-calculated column", ErrorSource.User);
			}
			if (calculatedColumn.Expression != update.Expression)
			{
				calculatedColumn.Expression = update.Expression;
				flag = true;
			}
		}
		if (update.ExpressionContext != null)
		{
			if (!(column is CalculatedColumn calculatedColumn2))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("ExpressionContext can only be set on calculated columns (columns with an Expression)", ErrorSource.User);
			}
			int num;
			if (!string.IsNullOrWhiteSpace(update.ExpressionContext))
			{
				if (!Enum.TryParse<ExpressionContext>(update.ExpressionContext, ignoreCase: true, out var result))
				{
					throw new McpExceptionWithSource("Invalid ExpressionContext '" + update.ExpressionContext + "'. Valid values are: " + string.Join(", ", Enum.GetNames(typeof(ExpressionContext))), ErrorSource.User, "Invalid ExpressionContext supplied. Valid values are: " + string.Join(", ", Enum.GetNames(typeof(ExpressionContext))) + ".");
				}
				num = (int)result;
			}
			else
			{
				num = 1;
			}
			ExpressionContext expressionContext = (ExpressionContext)num;
			if (calculatedColumn2.ExpressionContext != expressionContext)
			{
				calculatedColumn2.ExpressionContext = expressionContext;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.SourceColumn))
		{
			if (!(column is DataColumn dataColumn))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot set SourceColumn on a calculated column", ErrorSource.User);
			}
			if (dataColumn.SourceColumn != update.SourceColumn)
			{
				dataColumn.SourceColumn = update.SourceColumn;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.DataType))
		{
			if (!Enum.TryParse<DataType>(update.DataType, ignoreCase: true, out var result2))
			{
				string[] names = Enum.GetNames(typeof(DataType));
				throw new McpExceptionWithSource("Invalid DataType '" + update.DataType + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid DataType supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			if (column.DataType != result2)
			{
				column.DataType = result2;
				flag = true;
			}
		}
		if (update.DataCategory != null)
		{
			string text = (string.IsNullOrEmpty(update.DataCategory) ? null : update.DataCategory);
			if (text != column.DataCategory)
			{
				column.DataCategory = text;
				flag = true;
			}
		}
		if (update.FormatString != null)
		{
			string text2 = (string.IsNullOrEmpty(update.FormatString) ? null : update.FormatString);
			if (text2 != column.FormatString)
			{
				column.FormatString = text2;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.SummarizeBy))
		{
			if (!Enum.TryParse<AggregateFunction>(update.SummarizeBy, ignoreCase: true, out var result3))
			{
				string[] names2 = Enum.GetNames(typeof(AggregateFunction));
				throw new McpExceptionWithSource("Invalid SummarizeBy '" + update.SummarizeBy + "'. Valid values are: " + string.Join(", ", names2), ErrorSource.User, "Invalid SummarizeBy supplied. Valid values are: " + string.Join(", ", names2) + ".");
			}
			if (column.SummarizeBy != result3)
			{
				column.SummarizeBy = result3;
				flag = true;
			}
		}
		if (update.DefaultLabel.HasValue && column.IsDefaultLabel != update.DefaultLabel.Value)
		{
			column.IsDefaultLabel = update.DefaultLabel.Value;
			flag = true;
		}
		if (update.DefaultImage.HasValue && column.IsDefaultImage != update.DefaultImage.Value)
		{
			column.IsDefaultImage = update.DefaultImage.Value;
			flag = true;
		}
		if (update.IsHidden.HasValue && column.IsHidden != update.IsHidden.Value)
		{
			column.IsHidden = update.IsHidden.Value;
			flag = true;
		}
		if (update.IsUnique.HasValue && column.IsUnique != update.IsUnique.Value)
		{
			column.IsUnique = update.IsUnique.Value;
			flag = true;
		}
		if (update.IsKey.HasValue && ApplyIsKeyUpdate(table, column, update.IsKey.Value))
		{
			flag = true;
		}
		if (update.IsNullable.HasValue && column.IsNullable != update.IsNullable.Value)
		{
			column.IsNullable = update.IsNullable.Value;
			flag = true;
		}
		if (update.DisplayFolder != null)
		{
			string text3 = (string.IsNullOrEmpty(update.DisplayFolder) ? null : update.DisplayFolder);
			if (text3 != column.DisplayFolder)
			{
				column.DisplayFolder = text3;
				flag = true;
			}
		}
		if (update.SortByColumn != null)
		{
			if (string.IsNullOrEmpty(update.SortByColumn))
			{
				if (column.SortByColumn != null)
				{
					column.SortByColumn = null;
					flag = true;
				}
			}
			else
			{
				Column column2 = table.Columns.Find(update.SortByColumn);
				if (column2 == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"SortByColumn '{update.SortByColumn}' not found in table '{update.TableName}'", ErrorSource.User);
				}
				if (column.SortByColumn != column2)
				{
					column.SortByColumn = column2;
					flag = true;
				}
			}
		}
		if (update.SourceProviderType != null)
		{
			string text4 = (string.IsNullOrEmpty(update.SourceProviderType) ? null : update.SourceProviderType);
			if (text4 != column.SourceProviderType)
			{
				column.SourceProviderType = text4;
				flag = true;
			}
		}
		if (update.Description != null)
		{
			string text5 = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (text5 != column.Description)
			{
				column.Description = text5;
				flag = true;
			}
		}
		if (update.LineageTag != null)
		{
			string text6 = (string.IsNullOrEmpty(update.LineageTag) ? null : update.LineageTag);
			if (column.LineageTag != text6)
			{
				column.LineageTag = text6;
				flag = true;
			}
		}
		if (update.SourceLineageTag != null)
		{
			string text7 = (string.IsNullOrEmpty(update.SourceLineageTag) ? null : update.SourceLineageTag);
			if (column.SourceLineageTag != text7)
			{
				column.SourceLineageTag = text7;
				flag = true;
			}
		}
		if (update.IsAvailableInMDX.HasValue && column.IsAvailableInMDX != update.IsAvailableInMDX.Value)
		{
			column.IsAvailableInMDX = update.IsAvailableInMDX.Value;
			flag = true;
		}
		if (!string.IsNullOrWhiteSpace(update.Alignment))
		{
			if (!Enum.TryParse<Alignment>(update.Alignment, ignoreCase: true, out var result4))
			{
				throw new McpExceptionWithSource("Invalid Alignment '" + update.Alignment + "'. Valid values are: Default, Left, Right, Center", ErrorSource.User, "Invalid Alignment supplied. Valid values are: Default, Left, Right, Center.");
			}
			if (column.Alignment != result4)
			{
				column.Alignment = result4;
				flag = true;
			}
		}
		if (update.TableDetailPosition.HasValue && column.TableDetailPosition != update.TableDetailPosition.Value)
		{
			column.TableDetailPosition = update.TableDetailPosition.Value;
			flag = true;
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(column, update.Annotations, (Column c) => c.Annotations))
		{
			flag = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num2 = column.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceExtendedProperties(column, update.ExtendedProperties, (Column c) => c.ExtendedProperties);
			if (num2 || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		if (update.AlternateOf != null)
		{
			if (!CompareAlternateOf(ExtractAlternateOf(column), update.AlternateOf))
			{
				ValidateAlternateOf(update.AlternateOf, update.TableName, database);
				ApplyAlternateOf(column, update.AlternateOf);
				flag = true;
			}
		}
		else if (update.AlternateOf == null && column.AlternateOf != null)
		{
			column.AlternateOf = null;
			flag = true;
		}
		if (update.GroupByColumns != null)
		{
			if (!CompareGroupByColumns(ExtractGroupByColumns(column), update.GroupByColumns))
			{
				if (update.GroupByColumns.Count > 0)
				{
					ValidateGroupByColumns(update.GroupByColumns, update.TableName, update.Name, database);
				}
				ApplyGroupByColumns(column, update.GroupByColumns);
				flag = true;
			}
		}
		else if (update.GroupByColumns == null && ExtractGroupByColumns(column) != null)
		{
			ApplyGroupByColumns(column, new List<string>());
			flag = true;
		}
		string text8;
		if (!flag)
		{
			text8 = ((column is DataColumn) ? "Data" : ((column is CalculatedColumn) ? "Calculated" : ((column is CalculatedTableColumn) ? "CalculatedTableColumn" : ((!(column is RowNumberColumn)) ? "Unknown" : "RowNumber"))));
			string columnType = text8;
			return new ColumnOperationResult
			{
				State = column.State.ToString(),
				ErrorMessage = column.ErrorMessage,
				ColumnName = column.Name,
				TableName = table.Name,
				ColumnType = columnType,
				HasChanges = false
			};
		}
		TransactionOperations.RecordOperation(info, $"Updated column '{update.Name}' in table '{update.TableName}'");
		CheckpointMode checkpointMode = (IsBrokenCalculatedColumnState(column, update) ? CheckpointMode.ForceEvenInTransaction : CheckpointMode.Default);
		ConnectionOperations.SaveChangesWithRollback(info, "update column", OperationType.Update, checkpointMode);
		text8 = ((column is DataColumn) ? "Data" : ((column is CalculatedColumn) ? "Calculated" : ((column is CalculatedTableColumn) ? "CalculatedTableColumn" : ((!(column is RowNumberColumn)) ? "Unknown" : "RowNumber"))));
		string columnType2 = text8;
		return new ColumnOperationResult
		{
			State = column.State.ToString(),
			ErrorMessage = column.ErrorMessage,
			ColumnName = column.Name,
			TableName = table.Name,
			ColumnType = columnType2,
			HasChanges = true
		};
	}

	private static bool IsBrokenCalculatedColumnState(Column column, ColumnDefinition update)
	{
		if (column is CalculatedColumn calculatedColumn && !string.IsNullOrWhiteSpace(update.Expression))
		{
			ObjectState state = calculatedColumn.State;
			if (state != ObjectState.Ready && state != ObjectState.NoData)
			{
				return state != ObjectState.CalculationNeeded;
			}
			return false;
		}
		return false;
	}

	internal static void RenameColumnInternal(IConnectionInfo info, string tableName, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		Table obj = info.Database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		Column column = obj.Columns.Find(oldName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{oldName}' not found in table '{tableName}'", ErrorSource.User);
		if (column is RowNumberColumn)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column '" + oldName + "' is a RowNumber column and cannot be renamed", ErrorSource.User);
		}
		if (obj.Columns.Contains(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{newName}' already exists in table '{tableName}'", ErrorSource.User);
		}
		column.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed column '{oldName}' to '{newName}' in table '{tableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename column", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	internal static List<string> DeleteColumnInternal(IConnectionInfo info, string tableName, string columnName, bool shouldCascadeDelete)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(columnName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("columnName is required", ErrorSource.User);
		}
		List<string> list = new List<string>();
		Database database = info.Database;
		Table table = database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		Column col = table.Columns.Find(columnName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{columnName}' not found in table '{tableName}'", ErrorSource.User);
		if (col is RowNumberColumn)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column '" + columnName + "' is a RowNumber column and cannot be deleted", ErrorSource.User);
		}
		if (table.Partitions.Count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Cannot delete column '{columnName}' from table '{tableName}' because the table has no partitions. \nDon't know how to handle column deletion from a table with no partitions. \nDeleting columns requires modifying the partition source to remove the columns, but don't know how to make this happen automatically.", ErrorSource.User);
		}
		Partition partition = table.Partitions[0];
		if (!(partition.Source is CalculatedPartitionSource) && table.Columns.Count((Column c) => !(c is RowNumberColumn)) <= 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete the last column from table '" + tableName + "'. Non-calculated tables must have at least one column defined.", ErrorSource.User);
		}
		List<string> list2 = StructuralDependencyHelper.CheckAndDeleteDependenciesIfRequired(database, col, shouldCascadeDelete);
		if (!shouldCascadeDelete)
		{
			if (list2.Any())
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete column '" + columnName + "' because it is used by: " + string.Join(", ", list2), ErrorSource.User);
			}
			List<string> list3 = (from c in table.Columns
				where c.SortByColumn == col
				select c.Name).ToList();
			if (list3.Any())
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete column '" + columnName + "' because it is used as SortByColumn for: " + string.Join(", ", list3), ErrorSource.User);
			}
		}
		string value = (list2.Any() ? ("Dependencies have been removed: " + string.Join(", ", list2) + ".\n") : "");
		if (partition.Source is CalculatedPartitionSource)
		{
			if (col.Type != ColumnType.Calculated)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"{value}Cannot delete column '{columnName}' from calculated table '{tableName}'. \nCalculated table columns are derived from the DAX expression. \nModify the DAX expression directly to eliminate unwanted columns.", ErrorSource.User);
			}
			table.Columns.Remove(col);
		}
		else if (partition.Source is MPartitionSource mPartitionSource)
		{
			string value2 = (col as DataColumn)?.SourceColumn ?? columnName;
			string value3 = mPartitionSource.Expression ?? "";
			table.Columns.Remove(col);
			if (col.Type != ColumnType.Calculated)
			{
				string item = $"{value}Column '{columnName}' has been deleted from table '{tableName}' with M partition. \nWARNING: The column may be re-added automatically by other authoring tools unless you also modify the M expression of the partition to remove the column and then refresh the table. \nAdd a Table.RemoveColumns step to exclude the source column '{value2}'.\n\nCurrent M Expression:\n{value3}\n\nExample RemoveColumns syntax:\nlet\n    Source = Sql.Databases(\"ServerName\"),\n    SampleDB = Source{{[Name=\"SampleDatabase\"]}}[Data],\n    SampleTable = SampleDB{{[Schema=\"dbo\",Item=\"SampleTable\"]}}[Data],\n    #\"Removed Columns\" = Table.RemoveColumns(SampleTable, {{\"ColumnA\", \"ColumnB\", \"{value2}\"}}, MissingField.Ignore)\nin\n    #\"Removed Columns\"\n\nUse the partition_operations tool to modify the partition expression and then refresh the table.";
				list.Add(item);
			}
		}
		else
		{
			table.Columns.Remove(col);
			string item2 = $"{value}Column '{columnName}' has been deleted from table '{tableName}'. \nWARNING: The column may be re-added automatically by other authoring tools unless you also modify the source query of the partition to exclude the column and then refresh the table.\nUse the partition_operations tool to modify the partition to exclude the column, then refresh the table.";
			list.Add(item2);
		}
		TransactionOperations.RecordOperation(info, $"Deleted column '{columnName}' from table '{tableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete column", OperationType.Delete);
		return list;
	}

	public static async Task<CalculatedColumnValidationResult> ValidateCalculatedColumnExpression(string? connectionName, string tableName, string expression)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("expression is required", ErrorSource.User);
		}
		CalculatedColumnValidationResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			Table table = connectionInfo.Database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Column column in table.Columns)
			{
				hashSet.Add(column.Name);
			}
			int num = 1;
			string text;
			do
			{
				text = $"__TempColumn{num}";
				num++;
			}
			while (hashSet.Contains(text));
			CalculatedColumnValidationResult calculatedColumnValidationResult = new CalculatedColumnValidationResult
			{
				Expression = expression
			};
			Stopwatch stopwatch = Stopwatch.StartNew();
			CalculatedColumn calculatedColumn = null;
			bool flag = false;
			try
			{
				calculatedColumn = new CalculatedColumn
				{
					Name = text,
					Expression = expression
				};
				table.Columns.Add(calculatedColumn);
				flag = true;
				ConnectionOperations.SaveChangesIfNeeded(connectionInfo);
				calculatedColumnValidationResult.ObjectState = calculatedColumn.State.ToString();
				if (calculatedColumn.State != ObjectState.Ready)
				{
					calculatedColumnValidationResult.IsValid = false;
					calculatedColumnValidationResult.ErrorMessage = calculatedColumn.ErrorMessage ?? "Unknown error during validation.";
				}
				else
				{
					calculatedColumnValidationResult.IsValid = true;
					calculatedColumnValidationResult.Message = "Expression is valid.";
				}
			}
			catch (Exception ex)
			{
				calculatedColumnValidationResult.IsValid = false;
				calculatedColumnValidationResult.ErrorMessage = ex.Message;
			}
			finally
			{
				stopwatch.Stop();
				calculatedColumnValidationResult.ValidationTimeMs = stopwatch.ElapsedMilliseconds;
				if (flag && calculatedColumn != null)
				{
					try
					{
						table.Columns.Remove(calculatedColumn);
						ConnectionOperations.SaveChangesIfNeeded(connectionInfo, CheckpointMode.ForceEvenInTransaction);
					}
					catch (Exception)
					{
					}
				}
			}
			result = calculatedColumnValidationResult;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> CreateColumns(string? connectionName, List<ColumnDefinition> columns, BatchOptions options)
	{
		ValidateSingleKeyColumnDefinitionPerTable(columns, "a single create request");
		return await BatchExecutor.ExecuteAsync(connectionName, columns, options, "Create", "Created", "columns", (ColumnDefinition item) => item.TableName + "." + item.Name, delegate(BatchItemContext<ColumnDefinition> ctx)
		{
			ColumnOperationResult columnOperationResult = CreateColumnInternal(ctx.Connection, ctx.Item);
			List<string>? warnings = columnOperationResult.Warnings;
			if (warnings != null && warnings.Count > 0)
			{
				ctx.Warnings.AddRange(columnOperationResult.Warnings);
			}
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully created column '{ctx.Item.Name}' in table '{ctx.Item.TableName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Created column '{ctx.Item.TableName}.{ctx.Item.Name}'");
			}
		}, delegate(IConnectionInfo conn, List<ColumnDefinition> items, BatchOperationResponse response, List<string> warnings, string? txId, bool owns, bool txFailed, int fc)
		{
			PostCommitDaxValidator.Append(conn, warnings, response.Results, items, txId, owns, txFailed, fc, "created", (ColumnDefinition def) => ResolveColumnForValidation(conn, def));
		});
	}

	public static async Task<BatchOperationResponse> UpdateColumns(string? connectionName, List<ColumnDefinition> columns, BatchOptions options)
	{
		ValidateSingleKeyColumnDefinitionPerTable(columns, "a single update request");
		return await BatchExecutor.ExecuteAsync(connectionName, columns, options, "Update", "Updated", "columns", (ColumnDefinition item) => item.TableName + "." + item.Name, delegate(BatchItemContext<ColumnDefinition> ctx)
		{
			ColumnOperationResult columnOperationResult = UpdateColumnInternal(ctx.Connection, ctx.Item);
			List<string>? warnings = columnOperationResult.Warnings;
			if (warnings != null && warnings.Count > 0)
			{
				ctx.Warnings.AddRange(columnOperationResult.Warnings);
			}
			ctx.Result.Success = true;
			ctx.Result.Data = columnOperationResult;
			ctx.Result.Message = (columnOperationResult.HasChanges ? $"Successfully updated column '{ctx.Item.Name}' in table '{ctx.Item.TableName}'" : $"Column '{ctx.Item.Name}' in table '{ctx.Item.TableName}' updated (no changes detected)");
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Updated column '{ctx.Item.TableName}.{ctx.Item.Name}'");
			}
		}, delegate(IConnectionInfo conn, List<ColumnDefinition> items, BatchOperationResponse response, List<string> warnings, string? txId, bool owns, bool txFailed, int fc)
		{
			PostCommitDaxValidator.Append(conn, warnings, response.Results, items, txId, owns, txFailed, fc, "updated", (ColumnDefinition def) => ResolveColumnForValidation(conn, def));
		});
	}

	public static async Task<BatchOperationResponse> DeleteColumns(string? connectionName, List<ColumnReference> columns, bool shouldCascadeDelete, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, columns, options, "Delete", "Deleted", "columns", (ColumnReference item) => item.TableName + "." + item.Name, delegate(BatchItemContext<ColumnReference> ctx)
		{
			List<string> list = DeleteColumnInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.Name, shouldCascadeDelete);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully deleted column '{ctx.Item.Name}' from table '{ctx.Item.TableName}'";
			if (list.Any())
			{
				ctx.Result.Warnings.AddRange(list);
			}
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Deleted column '{ctx.Item.TableName}.{ctx.Item.Name}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetColumns(string? connectionName, List<ColumnReference> columns, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>()
		};
		if (columns == null || !columns.Any())
		{
			response.Success = false;
			response.Message = "No columns provided for retrieval";
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
				for (int i = 0; i < columns.Count; i++)
				{
					ColumnReference columnReference = columns[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = columnReference.TableName + "." + columnReference.Name
					};
					try
					{
						ColumnGet columnInternal = GetColumnInternal(connectionInfo.Database, columnReference.TableName, columnReference.Name);
						itemResult.Success = true;
						itemResult.Message = $"Successfully retrieved column '{columnReference.Name}' from table '{columnReference.TableName}'";
						itemResult.Data = columnInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = $"Error retrieving column '{columnReference.TableName}.{columnReference.Name}': {ex.Message}";
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
				}
				response.Message = $"Retrieved {successCount} of {columns.Count} columns.";
				response.Success = failureCount == 0;
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = columns.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get columns", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = columns.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameColumns(string? connectionName, List<ColumnRename> columns, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, columns, options, "Rename", "Renamed", "columns", (ColumnRename item) => item.TableName + "." + item.CurrentName, delegate(BatchItemContext<ColumnRename> ctx)
		{
			RenameColumnInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.CurrentName, ctx.Item.NewName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed column '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}' in table '{ctx.Item.TableName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed column '{ctx.Item.TableName}.{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'");
			}
		});
	}
}
