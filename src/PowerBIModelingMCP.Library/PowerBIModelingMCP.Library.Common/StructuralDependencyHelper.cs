using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AnalysisServices.Tabular;

namespace PowerBIModelingMCP.Library.Common;

public static class StructuralDependencyHelper
{
	public static List<string> CheckAndDeleteDependenciesIfRequired(Database db, NamedMetadataObject obj, bool cascadeDelete)
	{
		if (!(obj is Table table))
		{
			if (!(obj is Column column))
			{
				if (!(obj is Measure measure))
				{
					if (!(obj is Hierarchy hierarchy))
					{
						if (obj is Level level)
						{
							return GetLevelStructuralDependencies(db, level, cascadeDelete);
						}
						throw new ArgumentException("Unsupported object type: " + obj.GetType().Name);
					}
					return GetHierarchyStructuralDependencies(db, hierarchy, cascadeDelete);
				}
				return GetMeasureStructuralDependencies(db, measure, cascadeDelete);
			}
			return GetColumnStructuralDependencies(db, column.Table, column, cascadeDelete);
		}
		return GetTableStructuralDependencies(db, table, cascadeDelete);
	}

	private static List<string> GetTableStructuralDependencies(Database db, Table table, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		list.AddRange(GetDependentVariations(db, table, cascadeDelete));
		list.AddRange(GetDependentRelationships(db, table, cascadeDelete));
		list.AddRange(GetDependentTablePermissions(db, table, cascadeDelete));
		list.AddRange(GetDependentRelatedAggregations(db, table, null, cascadeDelete));
		list.AddRange(GetDependentPerspectives(db, table, cascadeDelete));
		list.AddRange(GetDependentObjectTranslations(db, table, cascadeDelete));
		list.AddRange(GetDependentDataSources(db, table, cascadeDelete));
		return list;
	}

	private static List<string> GetColumnStructuralDependencies(Database db, Table table, Column col, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (Column item in GetAllInferredColumns(db.Model).GetInferredColumns(col).Prepend(col))
		{
			list.AddRange(GetDependentPerspectives(db, item, cascadeDelete));
			list.AddRange(GetDependentObjectTranslations(db, item, cascadeDelete));
			list.AddRange(GetDependentTablePermissions(db, table, item, cascadeDelete));
			list.AddRange(GetDependentHierarchies(db, table, item, cascadeDelete));
			list.AddRange(GetDependentVariations(db, table, item, cascadeDelete));
			list.AddRange(GetDependentRelatedAggregations(db, item, null, cascadeDelete));
			list.AddRange(GetDependentRelationships(db, item, cascadeDelete));
			list.AddRange(GetDependentGroupByColumns(db, table, item, cascadeDelete));
		}
		return list;
	}

	private static List<string> GetMeasureStructuralDependencies(Database db, Measure measure, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		list.AddRange(GetDependentPerspectives(db, measure, cascadeDelete));
		list.AddRange(GetDependentObjectTranslations(db, measure, cascadeDelete));
		return list;
	}

	private static List<string> GetHierarchyStructuralDependencies(Database db, Hierarchy hierarchy, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		list.AddRange(GetDependentPerspectives(db, hierarchy, cascadeDelete));
		list.AddRange(GetDependentObjectTranslations(db, hierarchy, cascadeDelete));
		return list;
	}

	private static List<string> GetLevelStructuralDependencies(Database db, Level level, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		list.AddRange(GetDependentObjectTranslations(db, level, cascadeDelete));
		return list;
	}

	private static IReadOnlyDictionary<Column, IReadOnlyList<CalculatedTableColumn>> GetAllInferredColumns(Model model)
	{
		Dictionary<Column, IReadOnlyList<CalculatedTableColumn>> dictionary = new Dictionary<Column, IReadOnlyList<CalculatedTableColumn>>();
		Dictionary<Column, List<CalculatedTableColumn>> dictionary2 = (from c in model.Tables.SelectMany((Table t) => t.Columns).OfType<CalculatedTableColumn>()
			where c.ColumnOrigin != null
			group c by c.ColumnOrigin).ToDictionary((IGrouping<Column, CalculatedTableColumn> g) => g.Key, (IGrouping<Column, CalculatedTableColumn> g) => g.ToList());
		IEnumerable<Column> enumerable = dictionary2.Keys.Where((Column c) => (c as CalculatedTableColumn)?.ColumnOrigin == null);
		Func<CalculatedTableColumn, bool> includeSubtreePredicate = (CalculatedTableColumn c) => true;
		foreach (Column item in enumerable)
		{
			dictionary.Add(item, item.GetInferredColumns(dictionary2, includeSubtreePredicate).ToList());
		}
		return dictionary;
	}

	private static IEnumerable<Column> GetInferredColumns(this IReadOnlyDictionary<Column, IReadOnlyList<CalculatedTableColumn>> allInferredColumns, Column sourceColumn, bool includeSourceColumn = false)
	{
		IEnumerable<Column> enumerable = Enumerable.Empty<Column>();
		if (allInferredColumns.ContainsKey(sourceColumn))
		{
			enumerable = enumerable.Concat(allInferredColumns[sourceColumn]);
		}
		return enumerable;
	}

	private static IEnumerable<CalculatedTableColumn> GetInferredColumns(this Column sourceColumn, IReadOnlyDictionary<Column, List<CalculatedTableColumn>> inferredColumnsLookup, Func<CalculatedTableColumn, bool> includeSubtreePredicate)
	{
		if (!inferredColumnsLookup.TryGetValue(sourceColumn, out List<CalculatedTableColumn> value))
		{
			value = new List<CalculatedTableColumn>();
		}
		value = value.Where(includeSubtreePredicate).ToList();
		List<CalculatedTableColumn> list = new List<CalculatedTableColumn>();
		foreach (CalculatedTableColumn item in value)
		{
			list.Add(item);
			list.AddRange(item.GetInferredColumns(inferredColumnsLookup, includeSubtreePredicate));
		}
		return list;
	}

	private static List<string> GetDependentGroupByColumns(Database db, Table table, Column col, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (Column column in table.Columns)
		{
			RelatedColumnDetails relatedColumnDetails = column.RelatedColumnDetails;
			if (relatedColumnDetails == null)
			{
				continue;
			}
			GroupByColumn groupByColumn = relatedColumnDetails.GroupByColumns.SingleOrDefault((GroupByColumn g) => g.GroupingColumn == col);
			if (groupByColumn != null)
			{
				if (cascadeDelete)
				{
					relatedColumnDetails.GroupByColumns.Remove(groupByColumn);
				}
				list.Add("GroupByColumn: " + groupByColumn.GroupingColumn.Name);
			}
		}
		return list;
	}

	private static List<string> GetDependentPerspectives(Database db, Table table, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (Perspective perspective in db.Model.Perspectives)
		{
			if (perspective.PerspectiveTables.Contains(table.Name))
			{
				PerspectiveTable perspectiveTable = perspective.PerspectiveTables[table.Name];
				if (cascadeDelete)
				{
					perspective.PerspectiveTables.Remove(table.Name);
				}
				list.Add("Perspective: " + perspective.Name + ", PerspectiveTable: " + perspectiveTable.Name);
			}
		}
		return list;
	}

	private static List<string> GetDependentPerspectives(Database db, Column col, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (Perspective perspective in db.Model.Perspectives)
		{
			if (!perspective.PerspectiveTables.Contains(col.Table.Name))
			{
				continue;
			}
			PerspectiveTable perspectiveTable = perspective.PerspectiveTables[col.Table.Name];
			if (perspectiveTable.PerspectiveColumns.Contains(col.Name))
			{
				PerspectiveColumn perspectiveColumn = perspectiveTable.PerspectiveColumns[col.Name];
				if (cascadeDelete)
				{
					perspectiveTable.PerspectiveColumns.Remove(col.Name);
				}
				list.Add($"Perspective: {perspective.Name}, PerspectiveTable: {perspectiveTable.Name}, PerspectiveColumn: {perspectiveColumn.Name}");
			}
		}
		return list;
	}

	private static List<string> GetDependentPerspectives(Database db, Measure measure, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (Perspective perspective in db.Model.Perspectives)
		{
			if (!perspective.PerspectiveTables.Contains(measure.Table.Name))
			{
				continue;
			}
			PerspectiveTable perspectiveTable = perspective.PerspectiveTables[measure.Table.Name];
			if (perspectiveTable.PerspectiveMeasures.Contains(measure.Name))
			{
				PerspectiveMeasure perspectiveMeasure = perspectiveTable.PerspectiveMeasures[measure.Name];
				if (cascadeDelete)
				{
					perspectiveTable.PerspectiveMeasures.Remove(measure.Name);
				}
				list.Add($"Perspective: {perspective.Name}, PerspectiveTable: {perspectiveTable.Name}, PerspectiveMeasure: {perspectiveMeasure.Name}");
			}
		}
		return list;
	}

	private static List<string> GetDependentPerspectives(Database db, Hierarchy hierarchy, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (Perspective perspective in db.Model.Perspectives)
		{
			if (!perspective.PerspectiveTables.Contains(hierarchy.Table.Name))
			{
				continue;
			}
			PerspectiveTable perspectiveTable = perspective.PerspectiveTables[hierarchy.Table.Name];
			if (perspectiveTable.PerspectiveHierarchies.Contains(hierarchy.Name))
			{
				PerspectiveHierarchy perspectiveHierarchy = perspectiveTable.PerspectiveHierarchies[hierarchy.Name];
				if (cascadeDelete)
				{
					perspectiveTable.PerspectiveHierarchies.Remove(hierarchy.Name);
				}
				list.Add($"Perspective: {perspective.Name}, PerspectiveTable: {perspectiveTable.Name}, PerspectiveHierarchy: {perspectiveHierarchy.Name}");
			}
		}
		return list;
	}

	private static List<string> GetDependentRelationships(Database db, Table table, bool cascadeDelete)
	{
		List<Relationship> list = db.Model.Relationships.Where((Relationship r) => r.FromTable == table || r.ToTable == table).ToList();
		if (cascadeDelete)
		{
			foreach (SingleColumnRelationship item in list)
			{
				db.Model.Relationships.Remove(item);
			}
		}
		return list.Select((Relationship r) => "Relationship: " + r.Name).ToList();
	}

	private static List<string> GetDependentRelationships(Database db, Column col, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (SingleColumnRelationship item in db.Model.Relationships.Where((Relationship r) => ((SingleColumnRelationship)r).FromColumn == col || ((SingleColumnRelationship)r).ToColumn == col).ToList())
		{
			if (cascadeDelete)
			{
				db.Model.Relationships.Remove(item);
			}
			if (item.FromColumn == col)
			{
				list.Add("Relationship: " + item.Name + ", ToColumn: " + item.ToColumn.Name);
			}
			else
			{
				list.Add("Relationship: " + item.Name + ", FromColumn: " + item.FromColumn.Name);
			}
		}
		return list;
	}

	private static List<string> GetDependentObjectTranslations(Database db, Table table, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (TranslatedProperty value in Enum.GetValues(typeof(TranslatedProperty)))
		{
			foreach (Culture culture in db.Model.Cultures)
			{
				ObjectTranslation objectTranslation = culture.ObjectTranslations[table, value];
				if (objectTranslation != null)
				{
					if (cascadeDelete)
					{
						culture.ObjectTranslations.Remove(objectTranslation);
					}
					list.Add("Culture: " + culture.Name + ", Translation: " + objectTranslation.Value);
				}
			}
		}
		foreach (Column column in table.Columns)
		{
			list.AddRange(GetDependentObjectTranslations(db, column, cascadeDelete));
		}
		foreach (Measure measure in table.Measures)
		{
			list.AddRange(GetDependentObjectTranslations(db, measure, cascadeDelete));
		}
		foreach (Hierarchy hierarchy in table.Hierarchies)
		{
			list.AddRange(GetDependentObjectTranslations(db, hierarchy, cascadeDelete));
		}
		return list;
	}

	private static List<string> GetDependentObjectTranslations(Database db, Column col, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (TranslatedProperty value in Enum.GetValues(typeof(TranslatedProperty)))
		{
			foreach (Culture culture in db.Model.Cultures)
			{
				ObjectTranslation objectTranslation = culture.ObjectTranslations[col, value];
				if (objectTranslation != null)
				{
					if (cascadeDelete)
					{
						culture.ObjectTranslations.Remove(objectTranslation);
					}
					list.Add($"Culture: {culture.Name}, Column Translation: {objectTranslation.Value}, TranslatedProperty: {value.ToString()}");
				}
			}
		}
		return list;
	}

	private static List<string> GetDependentObjectTranslations(Database db, Measure measure, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (TranslatedProperty value in Enum.GetValues(typeof(TranslatedProperty)))
		{
			foreach (Culture culture in db.Model.Cultures)
			{
				ObjectTranslation objectTranslation = culture.ObjectTranslations[measure, value];
				if (objectTranslation != null)
				{
					if (cascadeDelete)
					{
						culture.ObjectTranslations.Remove(objectTranslation);
					}
					list.Add($"Culture: {culture.Name}, Measure Translation: {objectTranslation.Value}, TranslatedProperty: {value.ToString()}");
				}
			}
		}
		return list;
	}

	private static List<string> GetDependentObjectTranslations(Database db, Hierarchy hierarchy, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (TranslatedProperty value in Enum.GetValues(typeof(TranslatedProperty)))
		{
			foreach (Culture culture in db.Model.Cultures)
			{
				ObjectTranslation objectTranslation = culture.ObjectTranslations[hierarchy, value];
				if (objectTranslation != null)
				{
					if (cascadeDelete)
					{
						culture.ObjectTranslations.Remove(objectTranslation);
					}
					list.Add($"Culture: {culture.Name}, Hierarchy Translation: {objectTranslation.Value}, TranslatedProperty: {value.ToString()}");
				}
			}
		}
		foreach (Level level in hierarchy.Levels)
		{
			list.AddRange(GetDependentObjectTranslations(db, level, cascadeDelete));
		}
		return list;
	}

	private static List<string> GetDependentObjectTranslations(Database db, Level level, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (TranslatedProperty value in Enum.GetValues(typeof(TranslatedProperty)))
		{
			foreach (Culture culture in db.Model.Cultures)
			{
				ObjectTranslation objectTranslation = culture.ObjectTranslations[level, value];
				if (objectTranslation != null)
				{
					if (cascadeDelete)
					{
						culture.ObjectTranslations.Remove(objectTranslation);
					}
					list.Add($"Culture: {culture.Name}, Level Translation: {objectTranslation.Value}, TranslatedProperty: {value.ToString()}");
				}
			}
		}
		return list;
	}

	private static List<string> GetDependentTablePermissions(Database db, Table table, bool cascadeDelete)
	{
		TablePermission[] array = (from tp in db.Model.Roles.SelectMany((ModelRole r) => r.TablePermissions)
			where tp.Table == table
			select tp).ToArray();
		List<string> result = array.Select((TablePermission tp) => "TablePermission Role: " + tp.Role.Name).ToList();
		if (cascadeDelete)
		{
			TablePermission[] array2 = array;
			foreach (TablePermission tablePermission in array2)
			{
				tablePermission.Role.TablePermissions.Remove(tablePermission);
			}
		}
		return result;
	}

	private static List<string> GetDependentTablePermissions(Database db, Table table, Column col, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		TablePermission[] array = (from tp in db.Model.Roles.SelectMany((ModelRole r) => r.TablePermissions)
			where tp.Table == table
			select tp).ToArray();
		foreach (TablePermission tablePermission in array)
		{
			ColumnPermission columnPermission = tablePermission.ColumnPermissions.FirstOrDefault((ColumnPermission cp) => cp.Column == col);
			if (columnPermission != null)
			{
				if (cascadeDelete)
				{
					tablePermission.ColumnPermissions.Remove(columnPermission);
				}
				list.Add("TablePermission Role: " + tablePermission.Role.Name + ", ColumnPermission: " + columnPermission.Name);
			}
		}
		return list;
	}

	private static List<string> GetDependentHierarchies(Database db, Table table, Column col, bool cascadeDelete)
	{
		Level[] array = (from l in db.Model.Tables.SelectMany((Table t) => t.Hierarchies.SelectMany((Hierarchy h) => h.Levels))
			where l.Column == col
			select l).ToArray();
		List<string> result = array.Select((Level l) => "Hierarchy: " + l.Hierarchy.Name + ", Level: " + l.Name).ToList();
		if (cascadeDelete)
		{
			Level[] array2 = array;
			foreach (Level level in array2)
			{
				level.Hierarchy.Levels.Remove(level);
			}
		}
		return result;
	}

	private static List<string> GetDependentVariations(Database db, Table table, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		if (table.ShowAsVariationsOnly)
		{
			foreach (SingleColumnRelationship item in from r in db.Model.Relationships.OfType<SingleColumnRelationship>()
				where r.FromColumn.Variations.Any((Variation v) => v.Relationship?.ToTable == table)
				select r)
			{
				Column fromColumn = item.FromColumn;
				foreach (Variation variation in fromColumn.Variations)
				{
					if (cascadeDelete)
					{
						fromColumn.Variations.Remove(variation);
					}
					list.Add("Variation: " + variation.Name);
				}
			}
		}
		foreach (Variation item2 in (from v in table.Columns.SelectMany((Column c) => c.Variations)
			where v.Relationship != null && v.Relationship.ToTable.ShowAsVariationsOnly
			select v).ToList())
		{
			SingleColumnRelationship singleColumnRelationship = (SingleColumnRelationship)item2.Relationship;
			Table toTable = singleColumnRelationship.ToTable;
			list.Add($"Variation: {item2.Name}, Relationship: {singleColumnRelationship.Name}, RelationshipToTable: {toTable.Name}");
			if (cascadeDelete)
			{
				db.Model.Relationships.Remove(singleColumnRelationship);
				db.Model.Tables.Remove(toTable);
			}
		}
		return list;
	}

	private static List<string> GetDependentVariations(Database db, Table table, Column col, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (Variation item in col.Variations.ToList())
		{
			if (item.Relationship != null)
			{
				SingleColumnRelationship singleColumnRelationship = (SingleColumnRelationship)item.Relationship;
				Table toTable = singleColumnRelationship.ToTable;
				list.Add($"Variation: {item.Name}, Relationship: {singleColumnRelationship.Name}, RelationshipToTable: {toTable.Name}");
				if (cascadeDelete)
				{
					db.Model.Relationships.Remove(singleColumnRelationship);
					if (toTable.ShowAsVariationsOnly)
					{
						db.Model.Tables.Remove(toTable);
					}
				}
			}
			else
			{
				list.Add("Variation: " + item.Name);
			}
			if (cascadeDelete)
			{
				col.Variations.Remove(item);
			}
		}
		return list;
	}

	private static List<string> GetDependentRelatedAggregations(Database db, Table table, IReadOnlyDictionary<NamedMetadataObject, IList<AlternateOf>>? relatedAggregationsMap, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		if (relatedAggregationsMap == null)
		{
			relatedAggregationsMap = GetRelatedAggregationsMap(db.Model);
		}
		foreach (Column column in table.Columns)
		{
			list.AddRange(GetDependentRelatedAggregations(db, column, relatedAggregationsMap, cascadeDelete));
		}
		if (relatedAggregationsMap.TryGetValue(table, out IList<AlternateOf> value))
		{
			foreach (AlternateOf item in value)
			{
				if (cascadeDelete)
				{
					item.Column.AlternateOf = null;
				}
				list.Add("RelatedAggregation: " + item.Column.Name);
			}
		}
		return list;
	}

	private static List<string> GetDependentRelatedAggregations(Database db, Column col, IReadOnlyDictionary<NamedMetadataObject, IList<AlternateOf>>? relatedAggregationsMap, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		if (relatedAggregationsMap == null)
		{
			relatedAggregationsMap = GetRelatedAggregationsMap(db.Model);
		}
		if (relatedAggregationsMap.TryGetValue(col, out IList<AlternateOf> value))
		{
			foreach (AlternateOf item in value)
			{
				if (cascadeDelete)
				{
					item.Column.AlternateOf = null;
				}
				list.Add("RelatedAggregation: " + item.Column.Name);
			}
		}
		return list;
	}

	private static IReadOnlyDictionary<NamedMetadataObject, IList<AlternateOf>> GetRelatedAggregationsMap(Model model)
	{
		Dictionary<NamedMetadataObject, IList<AlternateOf>> dictionary = new Dictionary<NamedMetadataObject, IList<AlternateOf>>();
		foreach (Table table in model.Tables)
		{
			foreach (Column column in table.Columns)
			{
				if (column.AlternateOf?.BaseColumn != null)
				{
					if (dictionary.TryGetValue(column.AlternateOf.BaseColumn, out var value))
					{
						value.Add(column.AlternateOf);
					}
					else
					{
						dictionary.Add(column.AlternateOf.BaseColumn, new List<AlternateOf> { column.AlternateOf });
					}
				}
				if (column.AlternateOf?.BaseTable != null)
				{
					if (dictionary.TryGetValue(column.AlternateOf.BaseTable, out var value2))
					{
						value2.Add(column.AlternateOf);
						continue;
					}
					dictionary.Add(column.AlternateOf.BaseTable, new List<AlternateOf> { column.AlternateOf });
				}
			}
		}
		return dictionary;
	}

	private static List<string> GetDependentDataSources(Database db, Table table, bool cascadeDelete)
	{
		List<string> list = new List<string>();
		foreach (Partition partition in table.Partitions)
		{
			DataSource dataSource = ((partition.Source is QueryPartitionSource queryPartitionSource) ? queryPartitionSource.DataSource : (partition.Source as EntityPartitionSource)?.DataSource);
			if (dataSource != null && db.Model.Tables.Where((Table t) => t.Partitions.Any((Partition p) => (p.Source as QueryPartitionSource)?.DataSource == dataSource || (p.Source as EntityPartitionSource)?.DataSource == dataSource)).Count() == 1)
			{
				if (cascadeDelete)
				{
					partition.Source = null;
				}
				list.Add("DataSource: " + dataSource.Name);
			}
		}
		return list;
	}
}
