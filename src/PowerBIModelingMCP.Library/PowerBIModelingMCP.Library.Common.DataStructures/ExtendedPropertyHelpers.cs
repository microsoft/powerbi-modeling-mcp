using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AnalysisServices.Tabular;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public static class ExtendedPropertyHelpers
{
	public static List<string> Validate(List<ExtendedProperty> properties)
	{
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>();
		foreach (ExtendedProperty property in properties)
		{
			if (string.IsNullOrWhiteSpace(property.Name))
			{
				list.Add("Extended property name cannot be null or empty");
				continue;
			}
			if (!hashSet.Add(property.Name))
			{
				list.Add("Duplicate extended property name: " + property.Name);
			}
			if (string.IsNullOrWhiteSpace(property.Type))
			{
				list.Add("Extended property type cannot be null or empty for property: " + property.Name);
			}
			else if (property.Type != "String" && property.Type != "Json" && property.Type != "System.String" && property.Type != "System.Json")
			{
				list.Add("Extended property type must be 'String' or 'Json' for property: " + property.Name + ". Current type: " + property.Type);
			}
			if (property.Value == null)
			{
				list.Add("Extended property value cannot be null for property: " + property.Name);
			}
			if ((property.Type == "Json" || property.Type == "System.Json") && !string.IsNullOrEmpty(property.Value))
			{
				try
				{
					JsonDocument.Parse(property.Value);
				}
				catch (JsonException)
				{
					list.Add("Extended property with type 'Json' must contain well-formed JSON for property: " + property.Name);
				}
			}
		}
		return list;
	}

	public static ExtendedProperty? FindByName(List<ExtendedProperty> properties, string name)
	{
		return properties.FirstOrDefault((ExtendedProperty p) => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
	}

	public static void ApplyExtendedProperties<T>(T tabularObject, List<ExtendedProperty> properties, Func<T, ICollection<Microsoft.AnalysisServices.Tabular.ExtendedProperty>> extendedPropertiesCollection)
	{
		ICollection<Microsoft.AnalysisServices.Tabular.ExtendedProperty> collection = extendedPropertiesCollection(tabularObject);
		foreach (ExtendedProperty property in properties)
		{
			if ((property.Type == "Json" || property.Type == "System.Json") && !string.IsNullOrEmpty(property.Value))
			{
				try
				{
					JsonDocument.Parse(property.Value);
				}
				catch (JsonException ex)
				{
					throw new McpExceptionWithSource("Extended property '" + property.Name + "' with type 'Json' contains invalid JSON: " + ex.Message, ex, ErrorSource.User, "Extended property '" + property.Name + "' with type 'Json' contains invalid JSON.");
				}
			}
			Microsoft.AnalysisServices.Tabular.ExtendedProperty item = ((!(property.Type == "Json") && !(property.Type == "System.Json")) ? ((Microsoft.AnalysisServices.Tabular.ExtendedProperty)new StringExtendedProperty
			{
				Name = property.Name,
				Value = property.Value
			}) : ((Microsoft.AnalysisServices.Tabular.ExtendedProperty)new JsonExtendedProperty
			{
				Name = property.Name,
				Value = property.Value
			}));
			collection.Add(item);
		}
	}

	public static List<ExtendedProperty> ExtractExtendedProperties<T>(T tabularObject, Func<T, IEnumerable<Microsoft.AnalysisServices.Tabular.ExtendedProperty>> extendedPropertiesCollection)
	{
		List<ExtendedProperty> list = new List<ExtendedProperty>();
		foreach (Microsoft.AnalysisServices.Tabular.ExtendedProperty item in extendedPropertiesCollection(tabularObject))
		{
			if (!item.IsRemoved)
			{
				string type;
				string value;
				if (item is JsonExtendedProperty jsonExtendedProperty)
				{
					type = "Json";
					value = jsonExtendedProperty.Value ?? string.Empty;
				}
				else if (item is StringExtendedProperty stringExtendedProperty)
				{
					type = "String";
					value = stringExtendedProperty.Value ?? string.Empty;
				}
				else
				{
					type = "String";
					value = item.ToString() ?? string.Empty;
				}
				list.Add(new ExtendedProperty
				{
					Name = item.Name,
					Value = value,
					Type = type
				});
			}
		}
		return list;
	}

	public static void ReplaceExtendedProperties<T>(T tabularObject, List<ExtendedProperty> properties, Func<T, ICollection<Microsoft.AnalysisServices.Tabular.ExtendedProperty>> extendedPropertiesCollection)
	{
		ICollection<Microsoft.AnalysisServices.Tabular.ExtendedProperty> collection = extendedPropertiesCollection(tabularObject);
		collection.Clear();
		foreach (ExtendedProperty property in properties)
		{
			if ((property.Type == "Json" || property.Type == "System.Json") && !string.IsNullOrEmpty(property.Value))
			{
				try
				{
					JsonDocument.Parse(property.Value);
				}
				catch (JsonException ex)
				{
					throw new McpExceptionWithSource("Extended property '" + property.Name + "' with type 'Json' contains invalid JSON: " + ex.Message, ex, ErrorSource.User, "Extended property with type 'Json' contains invalid JSON.");
				}
			}
			Microsoft.AnalysisServices.Tabular.ExtendedProperty item = ((!(property.Type == "Json") && !(property.Type == "System.Json")) ? ((Microsoft.AnalysisServices.Tabular.ExtendedProperty)new StringExtendedProperty
			{
				Name = property.Name,
				Value = property.Value
			}) : ((Microsoft.AnalysisServices.Tabular.ExtendedProperty)new JsonExtendedProperty
			{
				Name = property.Name,
				Value = property.Value
			}));
			collection.Add(item);
		}
	}

	public static void ApplyToCulture(Culture culture, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(culture, properties, (Culture c) => c.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromCulture(Culture culture)
	{
		return ExtractExtendedProperties(culture, (Culture c) => c.ExtendedProperties);
	}

	public static void ReplaceCultureProperties(Culture culture, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(culture, properties, (Culture c) => c.ExtendedProperties);
	}

	public static void ApplyToTable(Table table, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(table, properties, (Table t) => t.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromTable(Table table)
	{
		return ExtractExtendedProperties(table, (Table t) => t.ExtendedProperties);
	}

	public static void ReplaceTableProperties(Table table, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(table, properties, (Table t) => t.ExtendedProperties);
	}

	public static void ApplyToMeasure(Measure measure, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(measure, properties, (Measure m) => m.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromMeasure(Measure measure)
	{
		return ExtractExtendedProperties(measure, (Measure m) => m.ExtendedProperties);
	}

	public static void ApplyToColumn(Column column, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(column, properties, (Column c) => c.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromColumn(Column column)
	{
		return ExtractExtendedProperties(column, (Column c) => c.ExtendedProperties);
	}

	public static void ApplyToHierarchy(Hierarchy hierarchy, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(hierarchy, properties, (Hierarchy h) => h.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromHierarchy(Hierarchy hierarchy)
	{
		return ExtractExtendedProperties(hierarchy, (Hierarchy h) => h.ExtendedProperties);
	}

	public static void ReplaceHierarchyProperties(Hierarchy hierarchy, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(hierarchy, properties, (Hierarchy h) => h.ExtendedProperties);
	}

	public static void ApplyToLevel(Level level, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(level, properties, (Level l) => l.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromLevel(Level level)
	{
		return ExtractExtendedProperties(level, (Level l) => l.ExtendedProperties);
	}

	public static void ReplaceLevelProperties(Level level, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(level, properties, (Level l) => l.ExtendedProperties);
	}

	public static void ApplyToModel(Model model, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(model, properties, (Model m) => m.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromModel(Model model)
	{
		return ExtractExtendedProperties(model, (Model m) => m.ExtendedProperties);
	}

	public static void ReplaceModelProperties(Model model, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(model, properties, (Model m) => m.ExtendedProperties);
	}

	public static void ApplyToNamedExpression(NamedExpression namedExpression, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(namedExpression, properties, (NamedExpression x) => x.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromNamedExpression(NamedExpression namedExpression)
	{
		return ExtractExtendedProperties(namedExpression, (NamedExpression x) => x.ExtendedProperties);
	}

	public static void ReplaceNamedExpressionProperties(NamedExpression namedExpression, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(namedExpression, properties, (NamedExpression x) => x.ExtendedProperties);
	}

	public static void ApplyToPartition(Partition partition, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(partition, properties, (Partition x) => x.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromPartition(Partition partition)
	{
		return ExtractExtendedProperties(partition, (Partition x) => x.ExtendedProperties);
	}

	public static void ReplacePartitionProperties(Partition partition, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(partition, properties, (Partition x) => x.ExtendedProperties);
	}

	public static void ApplyToRelationship(Relationship relationship, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(relationship, properties, (Relationship r) => r.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromRelationship(Relationship relationship)
	{
		return ExtractExtendedProperties(relationship, (Relationship r) => r.ExtendedProperties);
	}

	public static void ReplaceRelationshipProperties(Relationship relationship, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(relationship, properties, (Relationship r) => r.ExtendedProperties);
	}

	public static void ApplyToModelRole(ModelRole modelRole, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(modelRole, properties, (ModelRole r) => r.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromModelRole(ModelRole modelRole)
	{
		return ExtractExtendedProperties(modelRole, (ModelRole r) => r.ExtendedProperties);
	}

	public static void ReplaceModelRoleProperties(ModelRole modelRole, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(modelRole, properties, (ModelRole r) => r.ExtendedProperties);
	}

	public static void ApplyToTablePermission(TablePermission tablePermission, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(tablePermission, properties, (TablePermission tp) => tp.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromTablePermission(TablePermission tablePermission)
	{
		return ExtractExtendedProperties(tablePermission, (TablePermission tp) => tp.ExtendedProperties);
	}

	public static void ReplaceTablePermissionProperties(TablePermission tablePermission, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(tablePermission, properties, (TablePermission tp) => tp.ExtendedProperties);
	}

	public static void ApplyToFunction(Function function, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(function, properties, (Function f) => f.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromFunction(Function function)
	{
		return ExtractExtendedProperties(function, (Function f) => f.ExtendedProperties);
	}

	public static void ReplaceFunctionProperties(Function function, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(function, properties, (Function f) => f.ExtendedProperties);
	}

	public static void ApplyToBindingInfo(Microsoft.AnalysisServices.Tabular.BindingInfo bindingInfo, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(bindingInfo, properties, (Microsoft.AnalysisServices.Tabular.BindingInfo b) => b.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromBindingInfo(Microsoft.AnalysisServices.Tabular.BindingInfo bindingInfo)
	{
		return ExtractExtendedProperties(bindingInfo, (Microsoft.AnalysisServices.Tabular.BindingInfo b) => b.ExtendedProperties);
	}

	public static void ReplaceBindingInfoProperties(Microsoft.AnalysisServices.Tabular.BindingInfo bindingInfo, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(bindingInfo, properties, (Microsoft.AnalysisServices.Tabular.BindingInfo b) => b.ExtendedProperties);
	}

	public static void ApplyToDataSource(DataSource dataSource, List<ExtendedProperty> properties)
	{
		ApplyExtendedProperties(dataSource, properties, (DataSource ds) => ds.ExtendedProperties);
	}

	public static void ReplaceDataSourceProperties(DataSource dataSource, List<ExtendedProperty> properties)
	{
		ReplaceExtendedProperties(dataSource, properties, (DataSource ds) => ds.ExtendedProperties);
	}

	public static List<ExtendedProperty> ExtractFromDataSource(DataSource dataSource)
	{
		return ExtractExtendedProperties(dataSource, (DataSource ds) => ds.ExtendedProperties);
	}
}
