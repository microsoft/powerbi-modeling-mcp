using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public static class CommonTypes
{
	public static class ObjectTypes
	{
		public const string Table = "Table";

		public const string Column = "Column";

		public const string Measure = "Measure";

		public const string CalculationGroup = "CalculationGroup";

		public const string CalculationItem = "CalculationItem";

		public const string Relationship = "Relationship";

		public const string DataSource = "DataSource";

		public const string Partition = "Partition";

		public const string ModelRole = "ModelRole";

		public const string TablePermission = "TablePermission";

		public const string UserHierarchy = "UserHierarchy";

		public const string HierarchyLevel = "HierarchyLevel";

		public const string Culture = "Culture";

		public const string Database = "Database";
	}

	public static class Annotations
	{
		public static KeyValuePair<string, string> Create(string key, string value)
		{
			return new KeyValuePair<string, string>(key, value);
		}
	}

	public static class Validation
	{
		public static void ValidateName(string? name, string objectType)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage(objectType + " name cannot be null or empty", ErrorSource.User);
			}
		}

		public static void ValidateRequired(string? value, string propertyName, string objectType)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage(propertyName + " is required for " + objectType, ErrorSource.User);
			}
		}

		public static void ValidateEnum<T>(string? value, string propertyName) where T : struct, Enum
		{
			if (!string.IsNullOrWhiteSpace(value) && !Enum.TryParse<T>(value, out var _))
			{
				string[] names = Enum.GetNames(typeof(T));
				throw new McpExceptionWithSource($"Invalid {propertyName} '{value}'. Valid values are: {string.Join(", ", names)}", ErrorSource.User, $"Invalid {propertyName} value provided. Valid values are: {string.Join(", ", names)}.");
			}
		}
	}
}
