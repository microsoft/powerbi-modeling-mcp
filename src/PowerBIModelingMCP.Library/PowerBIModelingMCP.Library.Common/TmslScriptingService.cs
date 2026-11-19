using System;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Common;

public static class TmslScriptingService
{
	public static TmslOperationResult GenerateScript<T>(T metadataObject, TmslOperationType operationType, TmslOperationRequest? options = null) where T : NamedMetadataObject
	{
		if (metadataObject == null)
		{
			throw new ArgumentNullException("metadataObject");
		}
		if (options == null)
		{
			options = new TmslOperationRequest();
		}
		TmslOperationResult tmslOperationResult = new TmslOperationResult
		{
			OperationType = operationType,
			ObjectName = (metadataObject.Name ?? string.Empty),
			ObjectType = typeof(T).Name
		};
		try
		{
			ValidateObjectSupport<T>();
			ValidateOperationSupport<T>(operationType);
			string tmslScript = operationType switch
			{
				TmslOperationType.Create => JsonScripter.ScriptCreate(metadataObject, options.IncludeRestricted), 
				TmslOperationType.CreateOrReplace => JsonScripter.ScriptCreateOrReplace(metadataObject, options.IncludeRestricted), 
				TmslOperationType.Alter => JsonScripter.ScriptAlter(metadataObject, options.IncludeRestricted), 
				TmslOperationType.Delete => JsonScripter.ScriptDelete(metadataObject), 
				TmslOperationType.Refresh => GenerateRefreshScript(metadataObject, options.RefreshType ?? RefreshType.Automatic), 
				_ => throw McpExceptionWithSource.FromTelemetrySafeMessage($"Unsupported TMSL operation type: {operationType}"), 
			};
			tmslOperationResult.Success = true;
			tmslOperationResult.TmslScript = tmslScript;
		}
		catch (Exception ex)
		{
			tmslOperationResult.Success = false;
			tmslOperationResult.ErrorMessage = ex.Message;
			tmslOperationResult.ErrorSource = ((ex is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : ErrorSource.System);
		}
		return tmslOperationResult;
	}

	public static TmslOperationResult GenerateScript(Database database, TmslOperationType operationType, TmslOperationRequest? options = null)
	{
		if (database == null)
		{
			throw new ArgumentNullException("database");
		}
		if (options == null)
		{
			options = new TmslOperationRequest();
		}
		TmslOperationResult tmslOperationResult = new TmslOperationResult
		{
			OperationType = operationType,
			ObjectName = (database.Name ?? string.Empty),
			ObjectType = "Database"
		};
		try
		{
			string text;
			switch (operationType)
			{
			case TmslOperationType.Create:
				text = JsonScripter.ScriptCreate(database, options.IncludeRestricted);
				break;
			case TmslOperationType.CreateOrReplace:
				text = JsonScripter.ScriptCreateOrReplace(database, options.IncludeRestricted);
				break;
			case TmslOperationType.Alter:
				text = JsonScripter.ScriptAlter(database, options.IncludeRestricted);
				break;
			case TmslOperationType.Delete:
				text = JsonScripter.ScriptDelete(database);
				break;
			case TmslOperationType.Refresh:
				if (!options.RefreshType.HasValue)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("RefreshType is required for Refresh operations", ErrorSource.User);
				}
				text = JsonScripter.ScriptRefresh(database, options.RefreshType.Value);
				break;
			default:
				throw new ArgumentOutOfRangeException("operationType", operationType, "Unsupported operation type");
			}
			string tmslScript = text;
			tmslOperationResult.TmslScript = tmslScript;
			tmslOperationResult.Success = true;
		}
		catch (Exception ex)
		{
			tmslOperationResult.Success = false;
			tmslOperationResult.ErrorMessage = ex.Message;
			tmslOperationResult.ErrorSource = ((ex is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : ErrorSource.System);
		}
		tmslOperationResult.GeneratedAt = DateTime.UtcNow;
		return tmslOperationResult;
	}

	private static string GenerateRefreshScript(NamedMetadataObject metadataObject, RefreshType refreshType)
	{
		return JsonScripter.ScriptRefresh(metadataObject, refreshType);
	}

	private static void ValidateObjectSupport<T>() where T : NamedMetadataObject
	{
		string name = typeof(T).Name;
		HashSet<string> hashSet = new HashSet<string> { "Database", "Table", "Partition", "CalculationGroup", "Role" };
		if (!hashSet.Contains(name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"TMSL operations are not supported for object type '{name}'. Supported types are: {string.Join(", ", hashSet)}. " + "Use ExportTMDL instead for this object type.", ErrorSource.User);
		}
	}

	private static void ValidateOperationSupport<T>(TmslOperationType operationType) where T : NamedMetadataObject
	{
		string name = typeof(T).Name;
		if (name == "Role" && operationType == TmslOperationType.Refresh)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Operation '{operationType}' is not supported for object type '{name}'. " + "Roles support Create, CreateOrReplace, Alter, and Delete operations only.", ErrorSource.User);
		}
	}
}
