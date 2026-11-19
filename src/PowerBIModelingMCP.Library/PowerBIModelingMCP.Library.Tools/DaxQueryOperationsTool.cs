using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Common.Telemetry;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Tools;

[McpServerToolType]
public class DaxQueryOperationsTool
{
	public const string ToolName = "dax_query_operations";

	private readonly ILogger<DaxQueryOperationsTool> _logger;

	private readonly MCPServerConfiguration _config;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Execute"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Query" },
				Description = "Execute a DAX query against a semantic model and return the results. \nOptionally capture detailed execution metrics including storage engine and DirectQuery metrics.\nMandatory properties: Query. \nOptional: ConnectionName, TimeoutSeconds, MaxRows, GetExecutionMetrics, ExecutionMetricsOnly, Impersonation, ResultMode.",
				Tips = new string[5] { "When GetExecutionMetrics is true, a trace will be automatically started if not already active and paused after metrics collection", "If a trace is already active, it must have the required events subscribed for metrics collection", "ExecutionMetricsOnly only takes effect when GetExecutionMetrics is true. When true, row data is not returned but all rows are read for accurate execution time", "Impersonation is supported for Execute only. Roles uses the Roles connection string property; UserPrincipalName uses EffectiveUserName and requires caller impersonation permissions on the target model. GetExecutionMetrics cannot be combined with Impersonation in this version", "Provide UserPrincipalName alone to test as that user, Roles alone to test specific roles as the caller, or both to constrain the impersonated user to specific roles" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Execute\",\n        \"ConnectionName\": \"MyConnection\",\n        \"Query\": \"EVALUATE Sales\",\n        \"GetExecutionMetrics\": false\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Execute\",\n        \"ConnectionName\": \"MyConnection\",\n        \"Query\": \"EVALUATE Sales\",\n        \"GetExecutionMetrics\": true\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Execute\",\n        \"ConnectionName\": \"MyConnection\",\n        \"Query\": \"EVALUATE Sales\",\n        \"Impersonation\": {\n            \"Roles\": [ \"SalesRole\" ]\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Execute\",\n        \"ConnectionName\": \"MyConnection\",\n        \"Query\": \"EVALUATE Sales\",\n        \"Impersonation\": {\n            \"UserPrincipalName\": \"alice@contoso.com\",\n            \"Roles\": [ \"SalesRole\" ]\n        }\n    }\n}" }
			},
			["Validate"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Query" },
				Description = "Validate a DAX query for syntax and semantic correctness without executing it. \nMandatory properties: Query. \nOptional: ConnectionName, TimeoutSeconds.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Validate\",\n        \"ConnectionName\": \"MyConnection\",\n        \"Query\": \"EVALUATE Sales\"\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the DAX query operations tool and its available operations. \nMandatory properties: None. \nOptional: ConnectionName.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			},
			["ClearCache"] = new OperationMetadata
			{
				Description = "Clears the cache for the specified database connection. This forces the semantic model to reload data from source.\nMandatory properties: None.\nOptional: ConnectionName (uses last used connection if not provided).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ClearCache\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ClearCache\"\n    }\n}" }
			}
		}
	};

	public const int MaxTraceResourceSizeBytes = 4194304;

	private static readonly JsonSerializerOptions DefaultSerializerOptions = new JsonSerializerOptions
	{
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public DaxQueryOperationsTool(ILogger<DaxQueryOperationsTool> logger, MCPServerConfiguration config)
	{
		_logger = logger;
		_config = config;
	}

	[McpServerTool(Name = "dax_query_operations", Title = "DAX Query Operations", ReadOnly = true)]
	[YamlToolDescription("dax_query_operations")]
	public async Task<CallToolResult> ExecuteDaxQueryOperation(McpServer mcpServer, DaxQueryOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: QueryLength={QueryLength}, Connection={ConnectionName}", "DaxQueryOperationsTool", request.Operation, request.Query?.Length ?? 0, request.ConnectionName ?? "(last used)");
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("dax_query_operations", request.Operation, readOnly: true);
		CallToolResult result = null;
		try
		{
			string[] array = new string[4] { "EXECUTE", "VALIDATE", "HELP", "CLEARCACHE" };
			CallToolResult result2;
			if (!Enumerable.Contains(array, request.Operation.ToUpperInvariant()))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "DaxQueryOperationsTool", string.Join(", ", array));
				result = (result2 = CallToolResultHelper.FromResponse(new DaxQueryOperationResponse
				{
					Success = false,
					Message = "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", array),
					Operation = request.Operation,
					ErrorSource = ErrorSource.User
				}, annotations));
				return result2;
			}
			var (flag, text) = ValidateRequest(request.Operation, request);
			if (!flag)
			{
				_logger.LogWarning("Invalid request for {Operation} operation: {ValidationError}", request.Operation, text);
				result = (result2 = CallToolResultHelper.Error(request.Operation, text, annotations, ErrorSource.User));
				return result2;
			}
			if (request.Operation.ToUpperInvariant() == "EXECUTE" && !ConfirmationService.ConfirmRequest(mcpServer, request.ConnectionName, ConfirmationType.DaxOperation, writeGuard))
			{
				result = (result2 = CallToolResultHelper.FromResponse(new DaxQueryOperationResponse
				{
					Success = false,
					Message = "The user requested a dax operation but declined when asked to confirm. Do not retry or initiate any dax operations on your own. Wait until the user explicitly confirms or requests a dax operation again.",
					Operation = request.Operation
				}, annotations));
				return result2;
			}
			CallToolResult callToolResult;
			switch (request.Operation.ToUpperInvariant())
			{
			case "EXECUTE":
				callToolResult = ((request.ResultMode != DaxResultMode.Resource) ? CallToolResultHelper.FromResponse(await HandleExecuteOperation(request), annotations) : (await HandleExecuteOperationWithCsvExport(request, annotations)));
				result2 = callToolResult;
				break;
			case "VALIDATE":
				result2 = CallToolResultHelper.FromResponse(await HandleValidateOperation(request), annotations);
				break;
			case "HELP":
				result2 = CallToolResultHelper.FromResponse(HandleHelpOperation(request), annotations);
				break;
			case "CLEARCACHE":
				result2 = CallToolResultHelper.FromResponse(await HandleClearCacheOperation(request), annotations);
				break;
			default:
				result2 = CallToolResultHelper.FromResponse(new DaxQueryOperationResponse
				{
					Success = false,
					Message = "Operation " + request.Operation + " is not implemented",
					Operation = request.Operation
				}, annotations);
				break;
			}
			result = (callToolResult = result2);
			return callToolResult;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("DaxQueryOperationsTool", request.Operation, ex);
			string message = request.Operation.ToUpperInvariant() switch
			{
				"EXECUTE" => "Error executing DAX query: " + ex.GetErrorMessage(), 
				"VALIDATE" => "Error validating DAX query: " + ex.GetErrorMessage(), 
				"CLEARCACHE" => "Error clearing cache: " + ex.GetErrorMessage(), 
				_ => "Error executing DAX query operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new DaxQueryOperationResponse
			{
				Success = false,
				Message = message,
				Operation = request.Operation
			}, annotations, ex));
			return result2;
		}
		finally
		{
			_logger.LogToolCallCompleted(annotations.Title, !annotations.ReadOnlyHint, result?.IsError ?? true, ConnectionOperations.ResolveSemanticModelId());
		}
	}

	private async Task<DaxQueryOperationResponse> HandleExecuteOperation(DaxQueryOperationRequest request)
	{
		bool traceStartedByUs = false;
		bool traceResumedByUs = false;
		DaxQueryOperationResponse result;
		try
		{
			if (!request.GetExecutionMetrics)
			{
				goto IL_0370;
			}
			List<string> requiredEvents = new List<string> { "QueryBegin", "QueryEnd", "VertiPaqSEQueryBegin", "VertiPaqSEQueryEnd", "VertiPaqSEQueryCacheMatch", "DirectQueryBegin", "DirectQueryEnd", "ExecutionMetrics", "AggregateTableRewriteQuery", "Error" };
			TraceGet traceGet = await TraceOperations.GetTrace(request.ConnectionName);
			if (traceGet.Status == "no active trace")
			{
				TraceStartRequest request2 = new TraceStartRequest
				{
					Events = requiredEvents
				};
				await TraceOperations.StartTrace(request.ConnectionName, request2);
				traceStartedByUs = true;
				goto IL_0361;
			}
			if (traceGet.Status == "paused")
			{
				await TraceOperations.ResumeTrace(request.ConnectionName);
				traceResumedByUs = true;
				goto IL_0361;
			}
			if (!(traceGet.Status == "active"))
			{
				goto IL_0361;
			}
			List<string> list = requiredEvents.Except<string>(traceGet.SubscribedEvents ?? new List<string>(), StringComparer.OrdinalIgnoreCase).ToList();
			if (!list.Any())
			{
				goto IL_0361;
			}
			result = new DaxQueryOperationResponse
			{
				Success = false,
				Message = "Active trace is missing required events for query metrics: " + string.Join(", ", list) + ". Please stop the current trace and let the operation start a new one, or ensure all required events are subscribed.",
				Operation = request.Operation
			};
			goto end_IL_0058;
			IL_0370:
			int val = Math.Max(1, request.MaxRows ?? _config.DaxQuery.DefaultRowLimit);
			val = Math.Min(val, _config.DaxQuery.MaxRowLimit);
			DaxQueryExecute queryDef = new DaxQueryExecute
			{
				Query = request.Query,
				TimeoutSeconds = request.TimeoutSeconds,
				MaxRows = val,
				ReturnRows = (!request.GetExecutionMetrics || !request.ExecutionMetricsOnly),
				Impersonation = request.Impersonation
			};
			DaxQueryResult daxQueryResult = await DaxQueryOperations.ExecuteDaxQuery(request.ConnectionName, queryDef);
			DaxQueryOperationResponse response = new DaxQueryOperationResponse
			{
				Success = daxQueryResult.Success,
				Message = (daxQueryResult.Success ? $"DAX query executed successfully{GetImpersonationMessageSuffix(request.Impersonation)}, returned {daxQueryResult.RowCount} rows in {daxQueryResult.ExecutionTimeMs}ms" : ("DAX query execution failed: " + daxQueryResult.ErrorMessage)),
				Operation = request.Operation,
				ErrorSource = (daxQueryResult.Success ? ((ErrorSource?)null) : daxQueryResult.ErrorSource)
			};
			response.Data = daxQueryResult;
			if (daxQueryResult.Success)
			{
				_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, RowCount={RowCount}, Duration={Duration}ms", "DaxQueryOperationsTool", request.Operation, request.ConnectionName, daxQueryResult.RowCount, daxQueryResult.ExecutionTimeMs);
			}
			else
			{
				_logger.LogResultFailure("DaxQueryOperationsTool", request.Operation, "Failed", daxQueryResult.ErrorMessage);
			}
			if (request.GetExecutionMetrics && daxQueryResult.Success)
			{
				try
				{
					(bool, string) tuple = await ExecutionMetricsHelper.WaitForQueryMetricsEvents(request.ConnectionName);
					if (!tuple.Item1)
					{
						response.Warnings = new List<string> { "Query executed successfully but failed to collect complete metrics: " + tuple.Item2 };
					}
					else
					{
						List<CapturedTraceEvent> obj = await TraceOperations.GetCapturedEvents(request.ConnectionName);
						CalculatedExecutionMetrics calculatedExecutionMetrics = ExecutionMetricsHelper.ExtractQueryMetrics(obj);
						ReportedExecutionMetrics reportedExecutionMetrics = ExecutionMetricsHelper.ExtractServerReportedMetrics(obj);
						response.ExecutionMetrics = new QueryExecutionMetrics
						{
							CalculatedExecutionMetrics = calculatedExecutionMetrics,
							ReportedExecutionMetrics = reportedExecutionMetrics
						};
						List<string> list2 = new List<string>();
						if (!calculatedExecutionMetrics.Success)
						{
							list2.Add("Calculated metrics extraction had issues: " + calculatedExecutionMetrics.ErrorMessage);
						}
						if (!reportedExecutionMetrics.Success)
						{
							list2.Add("Server-reported metrics extraction had issues: " + reportedExecutionMetrics.ErrorMessage);
						}
						if (list2.Any())
						{
							response.Warnings = list2;
						}
					}
				}
				catch (Exception ex)
				{
					response.Warnings = new List<string> { "Query executed successfully but failed to collect metrics: " + ex.Message };
				}
			}
			result = response;
			goto end_IL_0058;
			IL_0361:
			await TraceOperations.ClearTraceEvents(request.ConnectionName);
			goto IL_0370;
			end_IL_0058:;
		}
		finally
		{
			if (traceStartedByUs || traceResumedByUs)
			{
				try
				{
					await TraceOperations.PauseTrace(request.ConnectionName);
				}
				catch
				{
				}
			}
		}
		return result;
	}

	private async Task<CallToolResult> HandleExecuteOperationWithCsvExport(DaxQueryOperationRequest request, ToolCallAnnotations? annotations = null)
	{
		DaxQueryOperationResponse response = await HandleExecuteOperation(request);
		List<CapturedTraceEvent> capturedEvents = null;
		if (request.GetExecutionMetrics && response.Success)
		{
			try
			{
				capturedEvents = await TraceOperations.GetCapturedEvents(request.ConnectionName);
			}
			catch (Exception exception)
			{
				_logger.LogOperationError("DaxQueryOperationsTool", request.Operation, exception);
			}
		}
		return CreateResultWithCsvResource(response, request, capturedEvents, annotations);
	}

	private CallToolResult CreateResultWithCsvResource(DaxQueryOperationResponse response, DaxQueryOperationRequest request, List<CapturedTraceEvent>? capturedTraceEvents = null, ToolCallAnnotations? annotations = null)
	{
		if (!response.Success)
		{
			return CallToolResultHelper.FromResponse(response, annotations);
		}
		DaxQueryExecuteMetadata daxQueryExecuteMetadata = new DaxQueryExecuteMetadata
		{
			Warnings = response.Warnings?.ToList(),
			ExecutionMetrics = response.ExecutionMetrics
		};
		List<ContentBlock> list = new List<ContentBlock>();
		if (_config.DaxQuery.EnableCsvExport && (!request.GetExecutionMetrics || !request.ExecutionMetricsOnly) && response.Data is DaxQueryResult daxQueryResult && daxQueryResult.Rows.Count > 0)
		{
			try
			{
				CsvExportHelper.CleanupOldFiles(_config.DaxQuery.CsvExportFolder);
				CsvExportResult csvExportResult = CsvExportHelper.ExportToCsv(daxQueryResult.Columns, daxQueryResult.Rows, _config.DaxQuery.CsvExportFolder, request.MaxRows);
				EmbeddedResourceBlock item = new EmbeddedResourceBlock
				{
					Resource = new TextResourceContents
					{
						Uri = "file:///" + csvExportResult.FilePath.Replace("\\", "/"),
						MimeType = "text/csv",
						Text = CallToolResultHelper.StripPrivacyTags(File.ReadAllText(csvExportResult.FilePath))
					}
				};
				list.Add(item);
				if (csvExportResult.WasTruncated)
				{
					DaxQueryExecuteMetadata daxQueryExecuteMetadata2 = daxQueryExecuteMetadata;
					if (daxQueryExecuteMetadata2.Warnings == null)
					{
						IList<string> list2 = (daxQueryExecuteMetadata2.Warnings = new List<string>());
					}
					daxQueryExecuteMetadata.Warnings.Add($"CSV output was truncated: {csvExportResult.TruncationReason}. Only {csvExportResult.RowsWritten} of {daxQueryResult.RowCount} rows were exported.");
				}
			}
			catch (Exception exception)
			{
				_logger.LogOperationError("DaxQueryOperationsTool", request.Operation, exception);
			}
		}
		if ((!request.GetExecutionMetrics || !request.ExecutionMetricsOnly) && capturedTraceEvents != null && capturedTraceEvents.Count > 0)
		{
			try
			{
				string csvExportFolder = _config.DaxQuery.CsvExportFolder;
				if (!string.IsNullOrEmpty(csvExportFolder))
				{
					if (!Directory.Exists(csvExportFolder))
					{
						Directory.CreateDirectory(csvExportFolder);
					}
					string text = Path.Combine(csvExportFolder, $"trace-events-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
					string text2 = SerializeTraceEvents(capturedTraceEvents);
					File.WriteAllText(text, text2);
					string text3 = text2;
					bool flag = false;
					if (Encoding.UTF8.GetByteCount(text3) > 4194304)
					{
						text3 = SerializeTraceEventsWithLimit(capturedTraceEvents, 4194304);
						flag = true;
					}
					text3 = CallToolResultHelper.StripPrivacyTags(text3);
					if (flag)
					{
						DaxQueryExecuteMetadata daxQueryExecuteMetadata2 = daxQueryExecuteMetadata;
						if (daxQueryExecuteMetadata2.Warnings == null)
						{
							IList<string> list2 = (daxQueryExecuteMetadata2.Warnings = new List<string>());
						}
						daxQueryExecuteMetadata.Warnings.Add($"Trace events were truncated in the response to stay within the 4 MB resource limit. Full trace ({capturedTraceEvents.Count} events) saved to: {text}");
					}
					EmbeddedResourceBlock item2 = new EmbeddedResourceBlock
					{
						Resource = new TextResourceContents
						{
							Uri = "file:///" + text.Replace("\\", "/"),
							MimeType = "application/json",
							Text = text3
						}
					};
					list.Add(item2);
				}
			}
			catch (Exception exception2)
			{
				_logger.LogOperationError("DaxQueryOperationsTool", request.Operation, exception2);
			}
		}
		IList<string>? warnings = daxQueryExecuteMetadata.Warnings;
		if ((warnings != null && warnings.Count > 0) || daxQueryExecuteMetadata.ExecutionMetrics != null)
		{
			list.Add(new TextContentBlock
			{
				Text = CallToolResultHelper.StripPrivacyTags(JsonSerializer.Serialize(daxQueryExecuteMetadata, DefaultSerializerOptions))
			});
		}
		return new CallToolResult
		{
			IsError = false,
			Content = list,
			Meta = CallToolResultHelper.BuildMeta(annotations)
		};
	}

	public static string SerializeTraceEvents(List<CapturedTraceEvent> events)
	{
		return JsonSerializer.Serialize(events.Select((CapturedTraceEvent e) => new { e.EventClassName, e.EventSubclassName, e.TextData, e.StartTime, e.EndTime, e.Duration, e.CpuTime, e.RequestId, e.Error }), DefaultSerializerOptions);
	}

	public static string SerializeTraceEventsWithLimit(List<CapturedTraceEvent> events, int maxBytes)
	{
		int num = 0;
		int num2 = events.Count;
		while (num < num2)
		{
			int num3 = (num + num2 + 1) / 2;
			string s = SerializeTraceEvents(events.Take(num3).ToList());
			if (Encoding.UTF8.GetByteCount(s) <= maxBytes)
			{
				num = num3;
			}
			else
			{
				num2 = num3 - 1;
			}
		}
		return SerializeTraceEvents(events.Take(Math.Max(num, 1)).ToList());
	}

	private async Task<DaxQueryOperationResponse> HandleValidateOperation(DaxQueryOperationRequest request)
	{
		DaxQueryValidate queryDef = new DaxQueryValidate
		{
			Query = request.Query,
			TimeoutSeconds = request.TimeoutSeconds
		};
		DaxValidationResult daxValidationResult = await DaxQueryOperations.ValidateDaxQuery(request.ConnectionName, queryDef);
		if (daxValidationResult.IsValid)
		{
			_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, ColumnCount={ColumnCount}, Duration={Duration}ms", "DaxQueryOperationsTool", request.Operation, request.ConnectionName, daxValidationResult.ExpectedColumns.Count, daxValidationResult.ValidationTimeMs);
		}
		else
		{
			_logger.LogResultFailure("DaxQueryOperationsTool", request.Operation, "Invalid", daxValidationResult.ErrorMessage);
		}
		return new DaxQueryOperationResponse
		{
			Success = daxValidationResult.IsValid,
			Message = (daxValidationResult.IsValid ? $"DAX query validated successfully with {daxValidationResult.ExpectedColumns.Count} columns in {daxValidationResult.ValidationTimeMs}ms" : ("DAX query validation failed: " + daxValidationResult.ErrorMessage)),
			Operation = request.Operation,
			Data = daxValidationResult
		};
	}

	private DaxQueryOperationResponse HandleHelpOperation(DaxQueryOperationRequest request)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "DaxQueryOperationsTool", request.Operation, request.ConnectionName, toolMetadata.Operations.Keys.Count);
		DaxQueryOperationResponse daxQueryOperationResponse = new DaxQueryOperationResponse();
		daxQueryOperationResponse.Success = true;
		daxQueryOperationResponse.Message = "Tool description retrieved successfully";
		daxQueryOperationResponse.Operation = "Help";
		daxQueryOperationResponse.Help = new
		{
			ToolName = "dax_query_operations",
			Description = "Execute and validate DAX queries against semantic models.",
			SupportedOperations = toolMetadata.Operations.Keys.ToList(),
			Authentication = "Uses existing connection established via connection_operations tool.",
			Capabilities = new string[5] { "Execute DAX queries with configurable timeout and row limits", "Execute DAX queries while impersonating security roles or a user principal", "Validate DAX query syntax without execution", "Capture detailed query execution metrics (storage engine, DirectQuery, cache metrics)", "Clear database cache to force data refresh from source" },
			Examples = toolMetadata.Operations,
			Notes = new string[6] { "Query parameter is required for Execute and Validate operations.", "TimeoutSeconds is optional (defaults: 200s for execute, 10s for validate).", "MaxRows is optional for Execute operation to limit result size.", "GetExecutionMetrics is optional for Execute operation to capture execution metrics (requires trace support).", "Impersonation is supported only for Execute and cannot be combined with GetExecutionMetrics.", "ClearCache operation clears the cache for the entire database." }
		};
		return daxQueryOperationResponse;
	}

	private async Task<DaxQueryOperationResponse> HandleClearCacheOperation(DaxQueryOperationRequest request)
	{
		ClearCacheResult clearCacheResult = await DaxQueryOperations.ClearCache(request.ConnectionName);
		if (clearCacheResult.Success)
		{
			_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "DaxQueryOperationsTool", request.Operation, request.ConnectionName);
		}
		else
		{
			_logger.LogResultFailure("DaxQueryOperationsTool", request.Operation, "Failed", clearCacheResult.ErrorMessage);
		}
		return new DaxQueryOperationResponse
		{
			Success = clearCacheResult.Success,
			Message = (clearCacheResult.Success ? ("Cache cleared successfully for database '" + clearCacheResult.DatabaseName + "'") : ("Failed to clear cache: " + clearCacheResult.ErrorMessage)),
			Operation = request.Operation,
			Data = clearCacheResult
		};
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, DaxQueryOperationRequest request)
	{
		if (!toolMetadata.Operations.TryGetValue(operation, out OperationMetadata value))
		{
			return (isValid: true, errorMessage: null);
		}
		JsonObject requestDict = JsonSerializer.SerializeToNode(request) as JsonObject;
		List<string> list = value.RequiredParams.Where((string p) => requestDict != null && requestDict[p] == null).ToList();
		List<string> list2 = value.ForbiddenParams.Where((string p) => requestDict != null && requestDict[p] != null).ToList();
		bool flag = request.Impersonation?.HasAny() ?? false;
		if (list.Any())
		{
			string text = "Missing required parameters for " + operation + " operation: " + string.Join(", ", list);
			_logger.LogWarning(text);
			return (isValid: false, errorMessage: text);
		}
		if (list2.Any())
		{
			string text2 = "Forbidden parameters provided for " + operation + " operation: " + string.Join(", ", list2);
			_logger.LogWarning(text2);
			return (isValid: false, errorMessage: text2);
		}
		if (operation.Equals("Execute", StringComparison.OrdinalIgnoreCase) && !Enum.IsDefined(typeof(DaxResultMode), request.ResultMode))
		{
			string text3 = $"Invalid ResultMode value '{request.ResultMode}'. Supported values: {DaxResultMode.Resource}, {DaxResultMode.Inline}.";
			_logger.LogWarning(text3);
			return (isValid: false, errorMessage: text3);
		}
		if (!operation.Equals("Execute", StringComparison.OrdinalIgnoreCase) && flag)
		{
			string text4 = "Impersonation is supported only for Execute operation, not " + operation + ".";
			_logger.LogWarning(text4);
			return (isValid: false, errorMessage: text4);
		}
		if (operation.Equals("Execute", StringComparison.OrdinalIgnoreCase) && flag)
		{
			if (request.GetExecutionMetrics)
			{
				string text5 = "GetExecutionMetrics cannot be combined with Impersonation because metrics tracing targets the shared connection session.";
				_logger.LogWarning(text5);
				return (isValid: false, errorMessage: text5);
			}
			try
			{
				DaxImpersonationConnectionStringBuilder.Validate(request.Impersonation);
			}
			catch (McpExceptionWithSource mcpExceptionWithSource)
			{
				_logger.LogWarning("Invalid DAX impersonation request: {ValidationError}", mcpExceptionWithSource.Message);
				return (isValid: false, errorMessage: mcpExceptionWithSource.Message);
			}
		}
		return (isValid: true, errorMessage: null);
	}

	private static string GetImpersonationMessageSuffix(DaxQueryImpersonationOptions? impersonation)
	{
		if (impersonation == null || !impersonation.HasAny())
		{
			return string.Empty;
		}
		return " using impersonation (" + impersonation.ToDisplayString() + ")";
	}
}
