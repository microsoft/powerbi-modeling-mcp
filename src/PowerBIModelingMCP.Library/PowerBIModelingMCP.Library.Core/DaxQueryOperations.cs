using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.AdomdClient;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class DaxQueryOperations
{
	private const int DEFAULT_TIMEOUT_SECONDS = 200;

	private const int DEFAULT_VALIDATION_TIMEOUT_SECONDS = 10;

	private const int DEFAULT_MAX_ROWS = int.MaxValue;

	private const int ABSOLUTE_MAX_ROWS = int.MaxValue;

	private static void ValidateCommonParameters(string query, int? timeoutSeconds, int? maxRows = null)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Query cannot be null or empty", ErrorSource.User);
		}
		if (timeoutSeconds.HasValue && timeoutSeconds.Value <= 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TimeoutSeconds must be greater than 0", ErrorSource.User);
		}
		if (maxRows.HasValue && maxRows.Value <= 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("MaxRows must be greater than 0", ErrorSource.User);
		}
	}

	private static string GetDataTypeString(Type type)
	{
		if (type == typeof(string))
		{
			return "String";
		}
		if (type == typeof(int) || type == typeof(int?))
		{
			return "Int32";
		}
		if (type == typeof(long) || type == typeof(long?))
		{
			return "Int64";
		}
		if (type == typeof(double) || type == typeof(double?))
		{
			return "Double";
		}
		if (type == typeof(decimal) || type == typeof(decimal?))
		{
			return "Decimal";
		}
		if (type == typeof(DateTime) || type == typeof(DateTime?))
		{
			return "DateTime";
		}
		if (type == typeof(bool) || type == typeof(bool?))
		{
			return "Boolean";
		}
		if (type == typeof(byte[]))
		{
			return "Binary";
		}
		return type.Name;
	}

	public static async Task<DaxQueryResult> ExecuteDaxQuery(string? connectionName, DaxQueryExecute queryDef)
	{
		if (queryDef == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Query definition cannot be null", ErrorSource.User);
		}
		DaxQueryResult result;
		await using (IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName))
		{
			result = await RetryAdomdOnEviction(conn, (IConnectionInfo c) => ExecuteDaxQueryInternal(c, queryDef));
		}
		return result;
	}

	private static async Task<DaxQueryResult> ExecuteDaxQueryInternal(IConnectionInfo info, DaxQueryExecute queryDef)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (queryDef == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Query definition cannot be null", ErrorSource.User);
		}
		int timeoutSeconds = queryDef.TimeoutSeconds ?? 200;
		int maxRows = queryDef.MaxRows ?? int.MaxValue;
		ValidateCommonParameters(queryDef.Query, timeoutSeconds, maxRows);
		if ((queryDef.Impersonation?.HasAny() ?? false) && info.IsOffline)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("DAX impersonation is not supported for offline connections.", ErrorSource.User);
		}
		ConnectionValidator.ValidateForDaxQueries(info);
		DaxQueryResult result = new DaxQueryResult();
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			IAdomdConnection impersonatedConnection = null;
			IAdomdConnection adomdConnection = info.AdomdConnection;
			try
			{
				if (queryDef.Impersonation?.HasAny() ?? false)
				{
					impersonatedConnection = await ConnectionOperations.OpenAdomdConnectionAsync(info, queryDef.Impersonation).ConfigureAwait(continueOnCapturedContext: false);
					adomdConnection = impersonatedConnection;
				}
				if (adomdConnection.State != ConnectionState.Open)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection is not open. Please reconnect.", ErrorSource.User);
				}
				using AdomdCommand adomdCommand = adomdConnection.CreateCommand(queryDef.Query);
				adomdCommand.CommandTimeout = timeoutSeconds;
				using AdomdDataReader adomdDataReader = adomdCommand.ExecuteReader();
				int fieldCount = adomdDataReader.FieldCount;
				for (int i = 0; i < fieldCount; i++)
				{
					result.Columns.Add(new DaxColumnInfo
					{
						Name = adomdDataReader.GetName(i),
						DataType = GetDataTypeString(adomdDataReader.GetFieldType(i)),
						IsNullable = true,
						Ordinal = i
					});
				}
				int num = 0;
				while (adomdDataReader.Read() && (maxRows == int.MaxValue || num < maxRows))
				{
					if (queryDef.ReturnRows)
					{
						Dictionary<string, object> dictionary = new Dictionary<string, object>();
						for (int j = 0; j < fieldCount; j++)
						{
							object value = (adomdDataReader.IsDBNull(j) ? null : adomdDataReader.GetValue(j));
							dictionary[adomdDataReader.GetName(j)] = value;
						}
						result.Rows.Add(dictionary);
					}
					num++;
				}
				result.RowCount = num;
				result.Success = true;
				TransactionOperations.RecordOperation(info, $"Executed DAX query{GetImpersonationAuditSuffix(queryDef.Impersonation)} returning {num} rows");
			}
			finally
			{
				impersonatedConnection?.Dispose();
			}
		}
		catch (Exception ex) when (SessionEvictionDetection.IsSessionEvictionError(ex))
		{
			throw;
		}
		catch (Exception ex2)
		{
			result.Success = false;
			result.ErrorMessage = ex2.Message;
			result.ErrorSource = ((ex2 is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : ErrorSource.System);
		}
		finally
		{
			stopwatch.Stop();
			result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
			AuditEvent.Default.Emit("execute DAX query" + GetImpersonationAuditSuffix(queryDef.Impersonation), result.Success, OperationType.Read, info);
		}
		return result;
	}

	public static async Task<DaxValidationResult> ValidateDaxQuery(string? connectionName, DaxQueryValidate queryDef)
	{
		if (queryDef == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Query definition cannot be null", ErrorSource.User);
		}
		DaxValidationResult result;
		await using (IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName))
		{
			result = await RetryAdomdOnEviction(conn, (IConnectionInfo c) => ValidateDaxQueryInternal(c, queryDef));
		}
		return result;
	}

	private static DaxValidationResult ValidateDaxQueryInternal(IConnectionInfo info, DaxQueryValidate queryDef)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (queryDef == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Query definition cannot be null", ErrorSource.User);
		}
		int num = queryDef.TimeoutSeconds ?? 10;
		ValidateCommonParameters(queryDef.Query, num);
		ConnectionValidator.ValidateForDaxQueries(info);
		DaxValidationResult daxValidationResult = new DaxValidationResult();
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			IAdomdConnection? adomdConnection = info.AdomdConnection;
			if (adomdConnection.State != ConnectionState.Open)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection is not open. Please reconnect.", ErrorSource.User);
			}
			using AdomdCommand adomdCommand = adomdConnection.CreateCommand(queryDef.Query);
			adomdCommand.CommandTimeout = num;
			adomdCommand.Properties.Add(new AdomdProperty("ExecutionMode", "Prepare"));
			using AdomdDataReader adomdDataReader = adomdCommand.ExecuteReader(CommandBehavior.SchemaOnly);
			DataTable schemaTable = adomdDataReader.GetSchemaTable();
			if (schemaTable != null)
			{
				int value = 0;
				foreach (DataRow row in schemaTable.Rows)
				{
					daxValidationResult.ExpectedColumns.Add(new DaxColumnInfo
					{
						Name = (row["ColumnName"]?.ToString() ?? $"Column{value}"),
						DataType = GetDataTypeString((Type)(row["DataType"] ?? typeof(object))),
						IsNullable = (bool)(row["AllowDBNull"] ?? ((object)true)),
						Ordinal = value++
					});
				}
			}
			daxValidationResult.IsValid = true;
			TransactionOperations.RecordOperation(info, "Validated DAX query successfully");
			AuditEvent.Default.Emit("validate DAX query", success: true, OperationType.Read, info);
		}
		catch (Exception ex) when (SessionEvictionDetection.IsSessionEvictionError(ex))
		{
			throw;
		}
		catch (Exception ex2)
		{
			daxValidationResult.IsValid = false;
			daxValidationResult.ErrorMessage = ex2.Message;
			AuditEvent.Default.Emit("validate DAX query", success: false, OperationType.Read, info);
			if (ex2 is AdomdException ex3)
			{
				daxValidationResult.DetailedError = "ADOMD Error: " + ex3.Message;
				if (ex3.InnerException != null)
				{
					daxValidationResult.DetailedError = daxValidationResult.DetailedError + " Inner: " + ex3.InnerException.Message;
				}
			}
		}
		finally
		{
			stopwatch.Stop();
			daxValidationResult.ValidationTimeMs = stopwatch.ElapsedMilliseconds;
		}
		return daxValidationResult;
	}

	public static NL2DAXPromptTemplateResult GetNL2DAXPromptTemplate()
	{
		NL2DAXPromptTemplateResult nL2DAXPromptTemplateResult = new NL2DAXPromptTemplateResult();
		try
		{
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "NL2DAXPromptTemplate.txt");
			if (!File.Exists(text))
			{
				nL2DAXPromptTemplateResult.Success = false;
				nL2DAXPromptTemplateResult.ErrorMessage = "NL2DAX prompt template file not found at: " + text;
				return nL2DAXPromptTemplateResult;
			}
			nL2DAXPromptTemplateResult.TemplateContent = File.ReadAllText(text);
			nL2DAXPromptTemplateResult.Success = true;
		}
		catch (Exception ex)
		{
			nL2DAXPromptTemplateResult.Success = false;
			nL2DAXPromptTemplateResult.ErrorMessage = "Error reading NL2DAX prompt template: " + ex.Message;
		}
		return nL2DAXPromptTemplateResult;
	}

	public static async Task<ClearCacheResult> ClearCache(string? connectionName)
	{
		ClearCacheResult result;
		await using (IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName))
		{
			result = await RetryAdomdOnEviction(conn, (IConnectionInfo c) => ClearCacheInternal(c));
		}
		return result;
	}

	private static ClearCacheResult ClearCacheInternal(IConnectionInfo info)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ConnectionValidator.ValidateForDaxQueries(info);
		ClearCacheResult clearCacheResult = new ClearCacheResult();
		try
		{
			string iD = info.Database.ID;
			string name = info.Database.Name;
			string commandText = "<Batch xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\">\n\t<ClearCache>\n\t\t<Object>\n\t\t\t<DatabaseID>" + iD + "</DatabaseID>\n\t\t</Object>\n\t</ClearCache>\n</Batch>";
			IAdomdConnection? adomdConnection = info.AdomdConnection;
			if (adomdConnection.State != ConnectionState.Open)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection is not open. Please reconnect.", ErrorSource.User);
			}
			AdomdCommand adomdCommand = adomdConnection.CreateCommand();
			try
			{
				adomdCommand.CommandType = CommandType.Text;
				adomdCommand.CommandText = commandText;
				int rowsAffected = adomdCommand.ExecuteNonQuery();
				clearCacheResult.Success = true;
				clearCacheResult.DatabaseName = name;
				clearCacheResult.ConnectionName = info.ConnectionName;
				clearCacheResult.RowsAffected = rowsAffected;
				TransactionOperations.RecordOperation(info, "Cleared cache for database '" + name + "'");
				AuditEvent.Default.Emit("clear cache", success: true, OperationType.Update, info);
			}
			finally
			{
				adomdCommand.Dispose();
			}
		}
		catch (Exception ex) when (SessionEvictionDetection.IsSessionEvictionError(ex))
		{
			throw;
		}
		catch (Exception ex2)
		{
			clearCacheResult.Success = false;
			clearCacheResult.ErrorMessage = ex2.Message;
			AuditEvent.Default.Emit("clear cache", success: false, OperationType.Update, info);
		}
		return clearCacheResult;
	}

	private static async Task<T> RetryAdomdOnEviction<T>(IConnectionInfo conn, Func<IConnectionInfo, T> operation)
	{
		T result = default(T);
		int num;
		try
		{
			result = operation(conn);
			return result;
		}
		catch (Exception ex) when (conn.IsCloudConnection && SessionEvictionDetection.IsSessionEvictionError(ex))
		{
			num = 1;
		}
		if (num != 1)
		{
			return result;
		}
		await ConnectionOperations.Reconnect(conn);
		return operation(conn);
	}

	private static async Task<T> RetryAdomdOnEviction<T>(IConnectionInfo conn, Func<IConnectionInfo, Task<T>> operation)
	{
		T result = default(T);
		int num;
		try
		{
			result = await operation(conn).ConfigureAwait(continueOnCapturedContext: false);
			return result;
		}
		catch (Exception ex) when (conn.IsCloudConnection && SessionEvictionDetection.IsSessionEvictionError(ex))
		{
			num = 1;
		}
		if (num != 1)
		{
			return result;
		}
		await ConnectionOperations.Reconnect(conn).ConfigureAwait(continueOnCapturedContext: false);
		return await operation(conn).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static string GetImpersonationAuditSuffix(DaxQueryImpersonationOptions? impersonation)
	{
		if (impersonation == null || !impersonation.HasAny())
		{
			return string.Empty;
		}
		return " with impersonation (" + impersonation.ToDisplayString() + ")";
	}
}
