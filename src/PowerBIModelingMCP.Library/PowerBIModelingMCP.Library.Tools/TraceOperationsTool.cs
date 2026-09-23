using System;
using System.Collections.Generic;
using System.Linq;
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
public class TraceOperationsTool
{
	public const string ToolName = "trace_operations";

	private readonly ILogger<TraceOperationsTool> _logger;

	private readonly MCPServerConfiguration _config;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Start"] = new OperationMetadata
			{
				Description = "Start a trace on the specified connection.\nOptional properties: ConnectionName, Events, FilterCurrentSessionOnly.\nFilterCurrentSessionOnly (default: true):\n- true: Filter events to only the current session\n- false: Capture all events from all sessions\nIf Events is not specified, default events will be captured: CommandBegin, CommandEnd, QueryBegin, QueryEnd, VertiPaqSEQueryBegin, VertiPaqSEQueryEnd, VertiPaqSEQueryCacheMatch, DirectQueryBegin, DirectQueryEnd, ExecutionMetrics, Error.\nOnly one trace can be active per connection.\nCannot start trace on offline connections - requires active server connection.\nOptional: ConnectionName, Events, FilterCurrentSessionOnly.",
				OptionalParams = new string[3] { "ConnectionName", "Events", "FilterCurrentSessionOnly" },
				Tips = new string[8] { "If Events is not specified, default events will be captured", "Default events: CommandBegin, CommandEnd, QueryBegin, QueryEnd, VertiPaqSEQueryBegin, VertiPaqSEQueryEnd, VertiPaqSEQueryCacheMatch, DirectQueryBegin, DirectQueryEnd, ExecutionMetrics, Error", "Only one trace can be active per connection", "Cannot start trace on offline connections - requires active server connection", "FilterCurrentSessionOnly=true is recommended for most scenarios to reduce noise and improve performance", "FilterCurrentSessionOnly=false should be used when you need to monitor all server activity", "Session filtering uses SessionID, ApplicationName, SPID, and always captures ExecutionMetrics events", "No need to call this operation explicitly to collect DAX query execution metrics for performance analysis. Call Execute operation on the dax_query_operations tool and set GetExecutionMetrics parameter to true. That operation will implicitly start trace if necessary." },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Start\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Start\",\n        \"ConnectionName\": \"MyConnection\",\n        \"Events\": [\"QueryBegin\", \"QueryEnd\", \"VertiPaqSEQueryEnd\"]\n    }\n}" }
			},
			["Stop"] = new OperationMetadata
			{
				Description = "Stop active trace on a connection.\nReturns summary of captured events.\nAll captured events are discarded.\nOptional: ConnectionName.",
				OptionalParams = new string[1] { "ConnectionName" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Stop\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["Pause"] = new OperationMetadata
			{
				Description = "Pause event capture on active trace (events are discarded while paused).\nTrace continues running but events are not captured.\nOptional: ConnectionName.",
				OptionalParams = new string[1] { "ConnectionName" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Pause\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["Resume"] = new OperationMetadata
			{
				Description = "Resume event capture on paused trace.\nEvents will be captured again after resuming.\nOptional: ConnectionName.",
				OptionalParams = new string[1] { "ConnectionName" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Resume\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["Clear"] = new OperationMetadata
			{
				Description = "Clear captured events without stopping trace.\nTrace continues running and capturing new events.\nOptional: ConnectionName.",
				OptionalParams = new string[1] { "ConnectionName" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Clear\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				Description = "Get trace details for a connection.\nReturns trace name, status, duration, and event counts.\nDoes not include captured events in the list for performance.\nUse Fetch operation to retrieve captured events.\nOptional: ConnectionName.",
				OptionalParams = new string[1] { "ConnectionName" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				Description = "List all traces across all connections.\nReturns trace information for all connections with traces.\nDoes not include captured events in the list for performance.\nUse Fetch operation to retrieve captured events.",
				Tips = new string[3] { "Returns trace information for all connections with traces", "Does not include captured events in the list for performance", "Use Get operation to retrieve full trace details including events" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["Fetch"] = new OperationMetadata
			{
				Description = "Fetch captured trace events with specified columns.\nBy default, includes only EventClassName, EventSubclassName, and StartTime.\nUse Columns parameter to specify which columns to include.\nBy default, retains events after fetching.\nSet ClearAfterFetch=true to clear events after fetching.\nOptional: ConnectionName, ClearAfterFetch, Columns.",
				OptionalParams = new string[3] { "ConnectionName", "ClearAfterFetch", "Columns" },
				Tips = new string[3] { "Default columns: EventClassName, EventSubclassName, StartTime", "Available columns: EventClassName, EventSubclassName, TextData, DatabaseName, ActivityId, RequestId, SessionId, ApplicationName, CurrentTime, StartTime, Duration, CpuTime, EndTime, NTUserName, RequestProperties, RequestParameters, ObjectName, ObjectPath, ObjectReference, Spid, IntegerData, ProgressTotal, ObjectId, Error", "Specify Columns as an array to customize the report output" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Fetch\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Fetch\",\n        \"ConnectionName\": \"MyConnection\",\n        \"ClearAfterFetch\": true\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Fetch\",\n        \"ConnectionName\": \"MyConnection\",\n        \"Columns\": [\"EventClassName\", \"Duration\", \"CpuTime\", \"TextData\"]\n    }\n}" }
			},
			["ExportJSON"] = new OperationMetadata
			{
				Description = "Export captured trace events to a JSON file.\nBy default, retains events after export (ClearAfterFetch defaults to false).\nSet ClearAfterFetch=true to clear events after export.\nRequired: FilePath.\nOptional: ConnectionName, ClearAfterFetch.",
				RequiredParams = new string[1] { "FilePath" },
				OptionalParams = new string[2] { "ConnectionName", "ClearAfterFetch" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportJSON\",\n        \"ConnectionName\": \"MyConnection\",\n        \"FilePath\": \"C:\\\\traces\\\\trace_events.json\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ExportJSON\",\n        \"ConnectionName\": \"MyConnection\",\n        \"FilePath\": \"./trace_output.json\",\n        \"ClearAfterFetch\": true\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the trace operations tool and its operations.\nProvides detailed information about all available operations.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	private static readonly JsonSerializerOptions DefaultSerializerOptions = new JsonSerializerOptions
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public TraceOperationsTool(ILogger<TraceOperationsTool> logger, MCPServerConfiguration config)
	{
		_logger = logger;
		_config = config;
	}

	[McpServerTool(Name = "trace_operations", Title = "Trace Operations", ReadOnly = true)]
	[YamlToolDescription("trace_operations")]
	public async Task<CallToolResult> ExecuteTraceOperation(McpServer mcpServer, TraceOperationRequest request)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "TraceOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("trace_operations", request.Operation, readOnly: true);
		CallToolResult result = null;
		try
		{
			string[] array = new string[10] { "START", "STOP", "PAUSE", "RESUME", "CLEAR", "GET", "LIST", "FETCH", "EXPORTJSON", "HELP" };
			if (!Enumerable.Contains(array, request.Operation.ToUpperInvariant()))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "TraceOperationsTool", string.Join(", ", array));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(new TraceOperationResponse
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
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.Error(request.Operation, text, annotations, ErrorSource.User));
				return result2;
			}
			CallToolResult result3;
			result = (result3 = request.Operation.ToUpperInvariant() switch
			{
				"START" => CallToolResultHelper.FromResponse(await HandleStartOperation(request), annotations), 
				"STOP" => CallToolResultHelper.FromResponse(await HandleStopOperation(request), annotations), 
				"PAUSE" => CallToolResultHelper.FromResponse(await HandlePauseOperation(request), annotations), 
				"RESUME" => CallToolResultHelper.FromResponse(await HandleResumeOperation(request), annotations), 
				"CLEAR" => CallToolResultHelper.FromResponse(await HandleClearOperation(request), annotations), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperationAsync(request), annotations), 
				"FETCH" => await HandleFetchOperation(request, annotations), 
				"EXPORTJSON" => CallToolResultHelper.FromResponse(await HandleExportJSONOperation(request), annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request), annotations), 
				_ => CallToolResultHelper.FromResponse(new TraceOperationResponse
				{
					Success = false,
					Message = "Operation " + request.Operation + " is not implemented",
					Operation = request.Operation
				}, annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("TraceOperationsTool", request.Operation, ex);
			string message = request.Operation.ToUpperInvariant() switch
			{
				"START" => "Error starting trace: " + ex.GetErrorMessage(), 
				"STOP" => "Error stopping trace: " + ex.GetErrorMessage(), 
				"PAUSE" => "Error pausing trace: " + ex.GetErrorMessage(), 
				"RESUME" => "Error resuming trace: " + ex.GetErrorMessage(), 
				"CLEAR" => "Error clearing trace events: " + ex.GetErrorMessage(), 
				"GET" => "Error getting trace details: " + ex.GetErrorMessage(), 
				"LIST" => "Error listing traces: " + ex.GetErrorMessage(), 
				"FETCH" => "Error fetching trace events: " + ex.GetErrorMessage(), 
				"EXPORTJSON" => "Error exporting trace events to JSON: " + ex.GetErrorMessage(), 
				_ => "Error executing trace operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new TraceOperationResponse
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

	private async Task<TraceOperationResponse> HandleStartOperation(TraceOperationRequest request)
	{
		TraceStartRequest request2 = new TraceStartRequest
		{
			Events = request.Events,
			FilterCurrentSessionOnly = (request.FilterCurrentSessionOnly ?? true)
		};
		TraceStartResult traceStartResult = await TraceOperations.StartTrace(request.ConnectionName, request2, _config.ApplicationName);
		if (traceStartResult.Warnings != null && traceStartResult.Warnings.Any())
		{
			foreach (string warning in traceStartResult.Warnings)
			{
				_logger.LogOperationWarning("TraceOperationsTool", request.Operation, warning);
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, EventTypes={EventTypeCount}", "TraceOperationsTool", request.Operation, request.ConnectionName, traceStartResult.SubscribedEvents.Count);
		return new TraceOperationResponse
		{
			Success = true,
			Message = ((traceStartResult.Warnings?.Any() ?? false) ? ("Trace already active: " + traceStartResult.TraceName) : $"Trace started: {traceStartResult.TraceName}, capturing {traceStartResult.SubscribedEvents.Count} event types"),
			Operation = request.Operation,
			Data = traceStartResult,
			Warnings = traceStartResult.Warnings
		};
	}

	private async Task<TraceOperationResponse> HandleStopOperation(TraceOperationRequest request)
	{
		TraceStopResult traceStopResult = await TraceOperations.StopTrace(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Duration={Duration}s, Captured={EventsCaptured}, Discarded={EventsDiscarded}", "TraceOperationsTool", request.Operation, request.ConnectionName, traceStopResult.Duration, traceStopResult.TotalEventsCaptured, traceStopResult.TotalEventsDiscarded);
		return new TraceOperationResponse
		{
			Success = true,
			Message = $"Trace stopped: {traceStopResult.TraceName}, duration: {traceStopResult.Duration:F2}s, captured: {traceStopResult.TotalEventsCaptured} events, discarded: {traceStopResult.TotalEventsDiscarded} events",
			Operation = request.Operation,
			Data = traceStopResult
		};
	}

	private async Task<TraceOperationResponse> HandlePauseOperation(TraceOperationRequest request)
	{
		TracePauseResult tracePauseResult = await TraceOperations.PauseTrace(request.ConnectionName);
		if (tracePauseResult.Warnings != null && tracePauseResult.Warnings.Any())
		{
			foreach (string warning in tracePauseResult.Warnings)
			{
				_logger.LogOperationWarning("TraceOperationsTool", request.Operation, warning);
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, EventsCaptured={EventsCaptured}", "TraceOperationsTool", request.Operation, request.ConnectionName, tracePauseResult.EventsCaptured);
		return new TraceOperationResponse
		{
			Success = true,
			Message = ((tracePauseResult.Warnings?.Any() ?? false) ? ("Trace already paused: " + tracePauseResult.TraceName) : $"Trace paused: {tracePauseResult.TraceName}, events captured so far: {tracePauseResult.EventsCaptured}"),
			Operation = request.Operation,
			Data = tracePauseResult,
			Warnings = tracePauseResult.Warnings
		};
	}

	private async Task<TraceOperationResponse> HandleResumeOperation(TraceOperationRequest request)
	{
		TraceResumeResult traceResumeResult = await TraceOperations.ResumeTrace(request.ConnectionName);
		if (traceResumeResult.Warnings != null && traceResumeResult.Warnings.Any())
		{
			foreach (string warning in traceResumeResult.Warnings)
			{
				_logger.LogOperationWarning("TraceOperationsTool", request.Operation, warning);
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, EventsCaptured={EventsCaptured}", "TraceOperationsTool", request.Operation, request.ConnectionName, traceResumeResult.EventsCaptured);
		return new TraceOperationResponse
		{
			Success = true,
			Message = ((traceResumeResult.Warnings?.Any() ?? false) ? ("Trace already active: " + traceResumeResult.TraceName) : $"Trace resumed: {traceResumeResult.TraceName}, events captured so far: {traceResumeResult.EventsCaptured}"),
			Operation = request.Operation,
			Data = traceResumeResult,
			Warnings = traceResumeResult.Warnings
		};
	}

	private async Task<TraceOperationResponse> HandleClearOperation(TraceOperationRequest request)
	{
		TraceClearResult traceClearResult = await TraceOperations.ClearTraceEvents(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, EventsCleared={EventsCleared}", "TraceOperationsTool", request.Operation, request.ConnectionName, traceClearResult.EventsCleared);
		return new TraceOperationResponse
		{
			Success = true,
			Message = $"Cleared {traceClearResult.EventsCleared} events from trace: {traceClearResult.TraceName}",
			Operation = request.Operation,
			Data = traceClearResult
		};
	}

	private async Task<TraceOperationResponse> HandleGetOperation(TraceOperationRequest request)
	{
		TraceGet traceGet = await TraceOperations.GetTrace(request.ConnectionName);
		string message = traceGet.Status switch
		{
			"no active trace" => "No active trace on connection", 
			"active" => $"Trace active: {traceGet.TraceName}, captured: {traceGet.EventsCaptured} events, duration: {traceGet.Duration:F2}s", 
			"paused" => $"Trace paused: {traceGet.TraceName}, captured: {traceGet.EventsCaptured} events, duration: {traceGet.Duration:F2}s", 
			_ => "Trace status: " + traceGet.Status, 
		};
		if (traceGet.Warnings != null && traceGet.Warnings.Any())
		{
			foreach (string warning in traceGet.Warnings)
			{
				_logger.LogOperationWarning("TraceOperationsTool", request.Operation, warning);
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Status={Status}, EventsCaptured={EventsCaptured}", "TraceOperationsTool", request.Operation, request.ConnectionName, traceGet.Status, traceGet.EventsCaptured);
		return new TraceOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = traceGet,
			Warnings = traceGet.Warnings
		};
	}

	private async Task<TraceOperationResponse> HandleListOperationAsync(TraceOperationRequest request)
	{
		List<TraceGet> list = await TraceOperations.ListTracesAsync();
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "TraceOperationsTool", request.Operation, request.ConnectionName, list.Count);
		return new TraceOperationResponse
		{
			Success = true,
			Message = ((list.Count == 0) ? "No active traces found" : $"Found {list.Count} active trace(s)"),
			Operation = request.Operation,
			Data = new
			{
				Traces = list,
				Count = list.Count
			}
		};
	}

	private async Task<CallToolResult> HandleFetchOperation(TraceOperationRequest request, ToolCallAnnotations? annotations = null)
	{
		bool valueOrDefault = request.ClearAfterFetch == true;
		TraceEventFetch traceEventFetch = await TraceOperations.FetchTraceEvents(request.ConnectionName, valueOrDefault);
		List<string> columnsToInclude = request.Columns ?? new List<string> { "EventClassName", "EventSubclassName", "StartTime" };
		HashSet<string> validColumns = GetValidColumnNames();
		List<string> list = columnsToInclude.Where((string c) => !validColumns.Contains<string>(c, StringComparer.OrdinalIgnoreCase)).ToList();
		if (list.Any())
		{
			_logger.LogWarning("{ToolName}.{Operation} validation failed", "TraceOperationsTool", request.Operation);
			return CallToolResultHelper.FromResponse(new TraceOperationResponse
			{
				Success = false,
				Message = "Invalid column names: " + string.Join(", ", list) + ". Valid columns: " + string.Join(", ", validColumns),
				Operation = request.Operation
			}, annotations);
		}
		List<Dictionary<string, object>> list2 = traceEventFetch.Events.Select((CapturedTraceEvent e) => ExtractEventColumns(e, columnsToInclude)).ToList();
		if (traceEventFetch.Cleared)
		{
			_ = $"Fetched {traceEventFetch.EventCount} events from trace: {traceEventFetch.TraceName} (events cleared)";
		}
		else
		{
			_ = $"Fetched {traceEventFetch.EventCount} events from trace: {traceEventFetch.TraceName} (events retained)";
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, EventCount={EventCount}, Cleared={Cleared}", "TraceOperationsTool", request.Operation, request.ConnectionName, traceEventFetch.EventCount, traceEventFetch.Cleared);
		List<ContentBlock> list3 = new List<ContentBlock>();
		if (list2.Count > 0)
		{
			string input = JsonSerializer.Serialize(new
			{
				TraceName = traceEventFetch.TraceName,
				EventCount = traceEventFetch.EventCount,
				Cleared = traceEventFetch.Cleared,
				Columns = columnsToInclude,
				Events = list2
			}, DefaultSerializerOptions);
			EmbeddedResourceBlock item = new EmbeddedResourceBlock
			{
				Resource = new TextResourceContents
				{
					Uri = "trace://" + Uri.EscapeDataString(traceEventFetch.TraceName ?? "trace") + "/events.json",
					MimeType = "application/json",
					Text = CallToolResultHelper.StripPrivacyTags(input)
				}
			};
			list3.Add(item);
		}
		return new CallToolResult
		{
			IsError = false,
			Content = list3,
			Meta = CallToolResultHelper.BuildMeta(annotations)
		};
	}

	private async Task<TraceOperationResponse> HandleExportJSONOperation(TraceOperationRequest request)
	{
		bool valueOrDefault = request.ClearAfterFetch == true;
		TraceEventJSONExport traceEventJSONExport = await TraceOperations.ExportTraceEventsToJSON(request.ConnectionName, request.FilePath, valueOrDefault);
		string message = (traceEventJSONExport.Cleared ? $"Exported {traceEventJSONExport.EventCount} events from trace: {traceEventJSONExport.TraceName} to {traceEventJSONExport.FilePath} (events cleared)" : $"Exported {traceEventJSONExport.EventCount} events from trace: {traceEventJSONExport.TraceName} to {traceEventJSONExport.FilePath} (events retained)");
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, EventCount={EventCount}, Cleared={Cleared}", "TraceOperationsTool", request.Operation, request.ConnectionName, traceEventJSONExport.EventCount, traceEventJSONExport.Cleared);
		return new TraceOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = traceEventJSONExport
		};
	}

	private TraceOperationResponse HandleHelpOperation(TraceOperationRequest request)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "TraceOperationsTool", request.Operation, request.ConnectionName, toolMetadata.Operations.Keys.Count);
		TraceOperationResponse traceOperationResponse = new TraceOperationResponse();
		traceOperationResponse.Success = true;
		traceOperationResponse.Message = "Tool description retrieved successfully";
		traceOperationResponse.Operation = "Help";
		traceOperationResponse.Help = new
		{
			ToolName = "trace_operations",
			Description = "Capture and analyze Analysis Services trace events for query execution monitoring, debugging, and performance analysis.",
			SupportedOperations = toolMetadata.Operations.Keys.ToList(),
			Authentication = "Uses existing connection established via connection_operations tool.",
			Capabilities = new string[5] { "Start/stop/pause/resume trace on connections", "Capture Analysis Services events (query, storage engine, DirectQuery, etc.)", "Fetch captured event summaries (event names and start times)", "Export full event details to JSON file", "List opened traces" },
			SupportedEvents = new
			{
				Queries = new string[2] { "QueryBegin", "QueryEnd" },
				Command = new string[2] { "CommandBegin", "CommandEnd" },
				Discover = new string[2] { "DiscoverBegin", "DiscoverEnd" },
				ErrorsAndWarnings = new string[1] { "Error" },
				ProgressReports = new string[4] { "ProgressReportBegin", "ProgressReportEnd", "ProgressReportCurrent", "ProgressReportError" },
				QueryProcessing = new string[9] { "VertiPaqSEQueryBegin", "VertiPaqSEQueryEnd", "VertiPaqSEQueryCacheMatch", "DirectQueryBegin", "DirectQueryEnd", "DAXQueryPlan", "ResourceUsage", "DAXEvaluationLog", "AggregateTableRewriteQuery" },
				ExecutionMetrics = new string[1] { "ExecutionMetrics" },
				JobGraph = new string[1] { "JobGraph" }
			},
			DefaultEvents = new string[12]
			{
				"CommandBegin", "CommandEnd", "QueryBegin", "QueryEnd", "VertiPaqSEQueryBegin", "VertiPaqSEQueryEnd", "VertiPaqSEQueryCacheMatch", "DirectQueryBegin", "DirectQueryEnd", "ExecutionMetrics",
				"AggregateTableRewriteQuery", "Error"
			},
			ValidFetchColumns = new string[24]
			{
				"EventClassName", "EventSubclassName", "TextData", "DatabaseName", "ActivityId", "RequestId", "SessionId", "ApplicationName", "CurrentTime", "StartTime",
				"Duration", "CpuTime", "EndTime", "NTUserName", "RequestProperties", "RequestParameters", "ObjectName", "ObjectPath", "ObjectReference", "Spid",
				"IntegerData", "ProgressTotal", "ObjectId", "Error"
			},
			Examples = toolMetadata.Operations,
			Notes = new string[6] { "Only one trace can be created per connection at a time.", "Traces are automatically filtered on the server side to the current session.", "If Events parameter is not specified when starting a trace, default events will be captured: CommandBegin, CommandEnd, QueryBegin, QueryEnd, VertiPaqSEQueryBegin, VertiPaqSEQueryEnd, VertiPaqSEQueryCacheMatch, DirectQueryBegin, DirectQueryEnd, ExecutionMetrics, Error.", "Paused traces discard new events but can be resumed.", "Trace operations are not supported on offline connections.", "All operations are read-only and do not modify the model." }
		};
		return traceOperationResponse;
	}

	private static HashSet<string> GetValidColumnNames()
	{
		return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"EventClassName", "EventSubclassName", "TextData", "DatabaseName", "ActivityId", "RequestId", "SessionId", "ApplicationName", "CurrentTime", "StartTime",
			"Duration", "CpuTime", "EndTime", "NTUserName", "RequestProperties", "RequestParameters", "ObjectName", "ObjectPath", "ObjectReference", "Spid",
			"IntegerData", "ProgressTotal", "ObjectId", "Error"
		};
	}

	private static Dictionary<string, object?> ExtractEventColumns(CapturedTraceEvent evt, List<string> columns)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		foreach (string column in columns)
		{
			dictionary[column] = column.ToLowerInvariant() switch
			{
				"eventclassname" => evt.EventClassName, 
				"eventsubclassname" => evt.EventSubclassName, 
				"textdata" => evt.TextData, 
				"databasename" => evt.DatabaseName, 
				"activityid" => evt.ActivityId, 
				"requestid" => evt.RequestId, 
				"sessionid" => evt.SessionId, 
				"applicationname" => evt.ApplicationName, 
				"currenttime" => evt.CurrentTime, 
				"starttime" => evt.StartTime, 
				"duration" => evt.Duration, 
				"cputime" => evt.CpuTime, 
				"endtime" => evt.EndTime, 
				"ntusername" => evt.NTUserName, 
				"requestproperties" => evt.RequestProperties, 
				"requestparameters" => evt.RequestParameters, 
				"objectname" => evt.ObjectName, 
				"objectpath" => evt.ObjectPath, 
				"objectreference" => evt.ObjectReference, 
				"spid" => evt.Spid, 
				"integerdata" => evt.IntegerData, 
				"progresstotal" => evt.ProgressTotal, 
				"objectid" => evt.ObjectId, 
				"error" => evt.Error, 
				_ => null, 
			};
		}
		return dictionary;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, TraceOperationRequest request)
	{
		if (!toolMetadata.Operations.TryGetValue(operation, out OperationMetadata value))
		{
			return (isValid: true, errorMessage: null);
		}
		JsonObject requestDict = JsonSerializer.SerializeToNode(request) as JsonObject;
		List<string> list = value.RequiredParams.Where((string p) => requestDict != null && requestDict[p] == null).ToList();
		List<string> list2 = value.ForbiddenParams.Where((string p) => requestDict != null && requestDict[p] != null).ToList();
		if (list.Any())
		{
			string text = "Missing required parameters for " + operation + " operation: " + string.Join(", ", list);
			_logger.LogWarning(text);
			return (isValid: false, errorMessage: text);
		}
		if (list2.Any())
		{
			string text2 = "Forbidden parameters for " + operation + " operation: " + string.Join(", ", list2);
			_logger.LogWarning(text2);
			return (isValid: false, errorMessage: text2);
		}
		return (isValid: true, errorMessage: null);
	}
}
