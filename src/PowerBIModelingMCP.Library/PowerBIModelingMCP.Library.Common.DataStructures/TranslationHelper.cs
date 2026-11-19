using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public static class TranslationHelper
{
	public static class TranslatableProperties
	{
		public const string Caption = "Caption";

		public const string Description = "Description";

		public const string DisplayFolder = "DisplayFolder";
	}

	private static readonly HashSet<string> CaptionSupportedObjects = new HashSet<string> { "Model", "Table", "Column", "Measure", "Hierarchy", "Level", "KPI" };

	private static readonly HashSet<string> DescriptionSupportedObjects = new HashSet<string> { "Model", "Table", "Column", "Measure", "Hierarchy", "Level", "KPI" };

	private static readonly HashSet<string> DisplayFolderSupportedObjects = new HashSet<string> { "Measure", "Hierarchy", "Column" };

	public static readonly HashSet<string> ValidObjectTypes = new HashSet<string> { "Model", "Table", "Measure", "Column", "Hierarchy", "Level", "KPI" };

	public static List<string> GetValidProperties(string objectType)
	{
		List<string> list = new List<string>();
		if (CaptionSupportedObjects.Contains(objectType))
		{
			list.Add("Caption");
		}
		if (DescriptionSupportedObjects.Contains(objectType))
		{
			list.Add("Description");
		}
		if (DisplayFolderSupportedObjects.Contains(objectType))
		{
			list.Add("DisplayFolder");
		}
		return list;
	}

	public static void ValidateTranslatableProperty(string objectType, string property)
	{
		List<string> validProperties = GetValidProperties(objectType);
		if (!validProperties.Contains(property))
		{
			throw new ArgumentException($"Property '{property}' is not translatable for object type '{objectType}'. Valid properties for {objectType}: {string.Join(", ", validProperties)}");
		}
	}

	public static void ValidateObjectType(string objectType)
	{
		if (!ValidObjectTypes.Contains(objectType))
		{
			throw new ArgumentException($"Object type '{objectType}' is not supported for translations. Valid object types: {string.Join(", ", ValidObjectTypes)}");
		}
	}

	public static void ValidateObjectIdentification(ObjectTranslationBase translation)
	{
		List<string> list = new List<string>();
		switch (translation.ObjectType)
		{
		case "Model":
			if (string.IsNullOrWhiteSpace(translation.ModelName))
			{
				list.Add("ModelName");
			}
			break;
		case "Table":
			if (string.IsNullOrWhiteSpace(translation.TableName))
			{
				list.Add("TableName");
			}
			break;
		case "Measure":
		case "KPI":
			if (string.IsNullOrWhiteSpace(translation.MeasureName))
			{
				list.Add("MeasureName");
			}
			break;
		case "Column":
			if (string.IsNullOrWhiteSpace(translation.TableName))
			{
				list.Add("TableName");
			}
			if (string.IsNullOrWhiteSpace(translation.ColumnName))
			{
				list.Add("ColumnName");
			}
			break;
		case "Hierarchy":
			if (string.IsNullOrWhiteSpace(translation.TableName))
			{
				list.Add("TableName");
			}
			if (string.IsNullOrWhiteSpace(translation.HierarchyName))
			{
				list.Add("HierarchyName");
			}
			break;
		case "Level":
			if (string.IsNullOrWhiteSpace(translation.TableName))
			{
				list.Add("TableName");
			}
			if (string.IsNullOrWhiteSpace(translation.HierarchyName))
			{
				list.Add("HierarchyName");
			}
			if (string.IsNullOrWhiteSpace(translation.LevelName))
			{
				list.Add("LevelName");
			}
			break;
		default:
			throw new ArgumentException("Unknown object type: " + translation.ObjectType);
		}
		if (list.Any())
		{
			throw new ArgumentException("Missing required identification properties for " + translation.ObjectType + ": " + string.Join(", ", list));
		}
	}

	public static string GetObjectDisplayName(ObjectTranslationBase translation)
	{
		return translation.ObjectType switch
		{
			"Model" => "Model: " + translation.ModelName, 
			"Table" => "Table: " + translation.TableName, 
			"Measure" => string.IsNullOrWhiteSpace(translation.TableName) ? ("Measure: " + translation.MeasureName) : ("Measure: " + translation.TableName + "." + translation.MeasureName), 
			"Column" => "Column: " + translation.TableName + "." + translation.ColumnName, 
			"Hierarchy" => "Hierarchy: " + translation.TableName + "." + translation.HierarchyName, 
			"Level" => $"Level: {translation.TableName}.{translation.HierarchyName}.{translation.LevelName}", 
			"KPI" => string.IsNullOrWhiteSpace(translation.TableName) ? ("KPI: " + translation.MeasureName) : ("KPI: " + translation.TableName + "." + translation.MeasureName), 
			_ => "Unknown: " + translation.ObjectType, 
		};
	}
}
