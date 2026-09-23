using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.RegularExpressions;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Common;

public static class ConnectionStringHelper
{
	public const string MyWorkspace = "My Workspace";

	public static string AddParameterToConnectionString(string connectionString, string key, string value)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection string cannot be null or empty when adding " + key + ".");
		}
		if (string.IsNullOrWhiteSpace(key))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Parameter key cannot be null or empty.");
		}
		try
		{
			DbConnectionStringBuilder dbConnectionStringBuilder = new DbConnectionStringBuilder
			{
				ConnectionString = connectionString
			};
			if (dbConnectionStringBuilder.ContainsKey(key))
			{
				return connectionString;
			}
			dbConnectionStringBuilder[key] = value;
			return dbConnectionStringBuilder.ConnectionString;
		}
		catch
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Error parsing connection string when attempting to add parameter " + key);
		}
	}

	public static string RemoveSessionIdFromConnectionString(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			return connectionString;
		}
		DbConnectionStringBuilder dbConnectionStringBuilder = new DbConnectionStringBuilder();
		dbConnectionStringBuilder.ConnectionString = connectionString;
		dbConnectionStringBuilder.Remove("SessionId");
		return dbConnectionStringBuilder.ConnectionString;
	}

	public static bool IsFabricConnectionString(string connectionString)
	{
		return connectionString.Contains("powerbi://", StringComparison.OrdinalIgnoreCase);
	}

	public static void ValidateConnectionStringSegment(string str, string nameofString)
	{
		if (string.IsNullOrWhiteSpace(str))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage(nameofString + " is required", ErrorSource.User);
		}
		if (Regex.IsMatch(str, "(\\\\\\.\\.|\\.\\.[/\\\\])"))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage(nameofString + " must not contain path traversal patterns", ErrorSource.User);
		}
		if (str.IndexOfAny(new char[3] { '\r', '\n', '\t' }) != -1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage(nameofString + " must not contain control characters", ErrorSource.User);
		}
	}

	public static bool IsLikelyDataSourceUrl(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return false;
		}
		if (input.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (input.Contains("://") && !input.Contains(';'))
		{
			return true;
		}
		return false;
	}

	public static string? ExtractDatabaseName(string connectionString)
	{
		try
		{
			DbConnectionStringBuilder dbConnectionStringBuilder = new DbConnectionStringBuilder
			{
				ConnectionString = connectionString
			};
			if (dbConnectionStringBuilder.ContainsKey("Initial Catalog"))
			{
				return dbConnectionStringBuilder["Initial Catalog"]?.ToString();
			}
			if (dbConnectionStringBuilder.ContainsKey("Database"))
			{
				return dbConnectionStringBuilder["Database"]?.ToString();
			}
			return null;
		}
		catch
		{
			return null;
		}
	}

	public static string? ExtractServerName(string? connectionString)
	{
		try
		{
			DbConnectionStringBuilder dbConnectionStringBuilder = new DbConnectionStringBuilder
			{
				ConnectionString = connectionString
			};
			if (dbConnectionStringBuilder.ContainsKey("Data Source"))
			{
				string text = dbConnectionStringBuilder["Data Source"]?.ToString();
				if (!string.IsNullOrEmpty(text))
				{
					return text;
				}
			}
			if (dbConnectionStringBuilder.ContainsKey("Server"))
			{
				string text2 = dbConnectionStringBuilder["Server"]?.ToString();
				if (!string.IsNullOrEmpty(text2))
				{
					return text2;
				}
			}
			return null;
		}
		catch
		{
			return null;
		}
	}

	public static string? ExtractWorkspaceName(string? connectionString)
	{
		if (string.IsNullOrEmpty(connectionString) || !IsFabricConnectionString(connectionString))
		{
			return null;
		}
		string text = ExtractServerName(connectionString);
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string[] array = text.Split('/');
		if (array.Length == 6)
		{
			return Uri.UnescapeDataString(array[5]);
		}
		if (array.Length == 5)
		{
			return "My Workspace";
		}
		return null;
	}

	public static string EnsureUniqueName(string baseName, ICollection<string> existingNames)
	{
		if (!existingNames.Contains(baseName))
		{
			return baseName;
		}
		int num = 2;
		string text;
		do
		{
			text = $"{baseName} {num}";
			num++;
		}
		while (existingNames.Contains(text));
		return text;
	}

	public static string BuildConnectionString(string dataSource, string? initialCatalog = null)
	{
		DbConnectionStringBuilder dbConnectionStringBuilder = new DbConnectionStringBuilder();
		dbConnectionStringBuilder["Data Source"] = dataSource;
		if (!string.IsNullOrWhiteSpace(initialCatalog))
		{
			dbConnectionStringBuilder["Initial Catalog"] = initialCatalog;
		}
		return dbConnectionStringBuilder.ConnectionString;
	}
}
