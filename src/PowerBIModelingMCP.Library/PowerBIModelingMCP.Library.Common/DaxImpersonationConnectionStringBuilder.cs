using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Common;

public static class DaxImpersonationConnectionStringBuilder
{
	public static string Build(string baseConnectionString, string databaseName, DaxQueryImpersonationOptions impersonation)
	{
		if (string.IsNullOrWhiteSpace(baseConnectionString))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Base connection string is required for DAX impersonation.", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(databaseName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database name is required for DAX impersonation.", ErrorSource.User);
		}
		if (impersonation == null || !impersonation.HasAny())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Impersonation settings are required for an impersonated DAX query.", ErrorSource.User);
		}
		Validate(impersonation);
		DbConnectionStringBuilder dbConnectionStringBuilder = new DbConnectionStringBuilder
		{
			ConnectionString = baseConnectionString
		};
		dbConnectionStringBuilder.Remove("SessionId");
		dbConnectionStringBuilder["Initial Catalog"] = databaseName;
		List<string> normalizedRoles = impersonation.GetNormalizedRoles();
		if (normalizedRoles.Count > 0)
		{
			dbConnectionStringBuilder["Roles"] = string.Join(",", normalizedRoles);
		}
		if (impersonation.HasUserPrincipal())
		{
			dbConnectionStringBuilder["EffectiveUserName"] = impersonation.UserPrincipalName.Trim();
		}
		return dbConnectionStringBuilder.ConnectionString;
	}

	public static void Validate(DaxQueryImpersonationOptions? impersonation)
	{
		if (impersonation == null || !impersonation.HasAny())
		{
			return;
		}
		if (impersonation.Roles != null)
		{
			foreach (string role in impersonation.Roles)
			{
				ValidateRequiredValue(role, "Impersonation.Roles");
				if (role.Contains(','))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Role names cannot contain commas because Roles is serialized as a comma-separated connection string value.", ErrorSource.User);
				}
			}
		}
		ValidateOptionalValue(impersonation.UserPrincipalName, "UserPrincipalName");
	}

	private static void ValidateRequiredValue(string? value, string parameterName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage(parameterName + " is required for DAX impersonation.", ErrorSource.User);
		}
		ValidateNoControlCharacters(value, parameterName);
	}

	private static void ValidateOptionalValue(string? value, string parameterName)
	{
		if (!string.IsNullOrEmpty(value))
		{
			ValidateNoControlCharacters(value, parameterName);
		}
	}

	private static void ValidateNoControlCharacters(string value, string parameterName)
	{
		if (value.Any(char.IsControl))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage(parameterName + " must not contain control characters.", ErrorSource.User);
		}
	}
}
