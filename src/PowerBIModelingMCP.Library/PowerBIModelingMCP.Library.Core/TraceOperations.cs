using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class TraceOperations
{
	private static readonly List<string> DefaultEvents;

	private static readonly List<string> QueriesEvents;

	private static readonly List<string> CommandEvents;

	private static readonly List<string> DiscoverEvents;

	private static readonly List<string> ErrorsAndWarnings;

	private static readonly List<string> ProgressReports;

	private static readonly List<string> QueryProcessing;

	private static readonly List<string> ExecutionMetricsEvents;

	private static readonly List<string> JobGraphEvents;

	private static readonly HashSet<string> AllowedEvents;

	private static readonly Dictionary<string, TraceEventDefinition> EventDefinitions;

	static TraceOperations()
	{
		DefaultEvents = new List<string>
		{
			"CommandBegin", "CommandEnd", "QueryBegin", "QueryEnd", "VertiPaqSEQueryBegin", "VertiPaqSEQueryEnd", "VertiPaqSEQueryCacheMatch", "DirectQueryBegin", "DirectQueryEnd", "ExecutionMetrics",
			"AggregateTableRewriteQuery", "Error"
		};
		QueriesEvents = new List<string> { "QueryBegin", "QueryEnd" };
		CommandEvents = new List<string> { "CommandBegin", "CommandEnd" };
		DiscoverEvents = new List<string> { "DiscoverBegin", "DiscoverEnd" };
		ErrorsAndWarnings = new List<string> { "Error" };
		ProgressReports = new List<string> { "ProgressReportBegin", "ProgressReportEnd", "ProgressReportCurrent", "ProgressReportError" };
		QueryProcessing = new List<string> { "VertiPaqSEQueryBegin", "VertiPaqSEQueryEnd", "VertiPaqSEQueryCacheMatch", "DirectQueryBegin", "DirectQueryEnd", "DAXQueryPlan", "ResourceUsage", "DAXEvaluationLog", "AggregateTableRewriteQuery" };
		ExecutionMetricsEvents = new List<string> { "ExecutionMetrics" };
		JobGraphEvents = new List<string> { "JobGraph" };
		AllowedEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		EventDefinitions = new Dictionary<string, TraceEventDefinition>(StringComparer.OrdinalIgnoreCase);
		AllowedEvents.UnionWith(QueriesEvents);
		AllowedEvents.UnionWith(CommandEvents);
		AllowedEvents.UnionWith(DiscoverEvents);
		AllowedEvents.UnionWith(ErrorsAndWarnings);
		AllowedEvents.UnionWith(ProgressReports);
		AllowedEvents.UnionWith(QueryProcessing);
		AllowedEvents.UnionWith(ExecutionMetricsEvents);
		AllowedEvents.UnionWith(JobGraphEvents);
		InitializeEventDefinitions();
	}

	public static async Task<TraceStartResult> StartTrace(string? connectionName, TraceStartRequest request, string? applicationName = null)
	{
		TraceStartResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			ConnectionValidator.ValidateForTrace(connectionInfo);
			if (connectionInfo.Trace != null)
			{
				TraceContext trace = connectionInfo.Trace;
				result = new TraceStartResult
				{
					TraceName = trace.TraceName,
					Status = (trace.IsPaused ? "paused" : "active"),
					StartTime = trace.StartTime,
					SubscribedEvents = trace.SubscribedEvents,
					Warnings = new List<string> { "Trace is already active on this connection. Stop the existing trace first to start a new one." }
				};
			}
			else
			{
				List<string> list = DetermineEventsToSubscribe(request.Events);
				string text = Guid.NewGuid().ToString();
				Microsoft.AnalysisServices.Tabular.Trace trace2 = connectionInfo.TabularServer.Traces.Add(text);
				TraceContext traceContext = new TraceContext
				{
					Trace = trace2,
					Server = connectionInfo.TabularServer,
					TraceName = text,
					SubscribedEvents = list,
					StartTime = DateTime.UtcNow,
					FilterCurrentSessionOnly = request.FilterCurrentSessionOnly
				};
				try
				{
					AddEventsToTrace(traceContext, list);
					if (request.FilterCurrentSessionOnly)
					{
						ApplySessionFilter(traceContext, connectionInfo, applicationName);
					}
					SetupTraceEventHandler(traceContext);
					traceContext.Trace.Update();
					traceContext.Trace.Start();
					connectionInfo.Trace = traceContext;
					result = new TraceStartResult
					{
						TraceName = traceContext.TraceName,
						Status = "started",
						StartTime = traceContext.StartTime,
						SubscribedEvents = list,
						FilterCurrentSessionOnly = traceContext.FilterCurrentSessionOnly
					};
				}
				catch (Exception ex)
				{
					try
					{
						if (trace2 != null)
						{
							trace2.Stop();
							trace2.Drop();
						}
					}
					catch
					{
					}
					throw new McpExceptionWithSource("Failed to start trace: " + ex.Message, "Failed to start trace.");
				}
			}
		}
		return result;
	}

	public static async Task<TraceStopResult> StopTrace(string? connectionName)
	{
		TraceStopResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			ConnectionValidator.ValidateForTrace(connectionInfo);
			if (connectionInfo.Trace == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("No active trace on this connection", ErrorSource.User);
			}
			TraceContext trace = connectionInfo.Trace;
			double totalSeconds = (DateTime.UtcNow - trace.StartTime).TotalSeconds;
			try
			{
				trace.Trace.Stop();
				trace.Trace.Drop();
				TraceStopResult traceStopResult = new TraceStopResult
				{
					TraceName = trace.TraceName,
					Status = "stopped",
					Duration = totalSeconds,
					TotalEventsCaptured = trace.TotalEventsCaptured,
					TotalEventsDiscarded = trace.TotalEventsDiscarded
				};
				connectionInfo.Trace = null;
				result = traceStopResult;
			}
			catch (Exception ex)
			{
				throw new McpExceptionWithSource("Failed to stop trace: " + ex.Message, ex, null, "Failed to stop trace.");
			}
		}
		return result;
	}

	public static async Task<TraceGet> GetTrace(string? connectionName)
	{
		TraceGet result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				ConnectionValidator.ValidateForTrace(connectionInfo);
				TraceGet traceGet;
				if (connectionInfo.Trace == null)
				{
					traceGet = new TraceGet
					{
						Status = "no active trace"
					};
				}
				else
				{
					TraceContext trace = connectionInfo.Trace;
					double totalSeconds = (DateTime.UtcNow - trace.StartTime).TotalSeconds;
					traceGet = new TraceGet
					{
						TraceName = trace.TraceName,
						Status = (trace.IsPaused ? "paused" : "active"),
						StartTime = trace.StartTime,
						Duration = totalSeconds,
						EventsCaptured = trace.TotalEventsCaptured,
						EventsDiscarded = trace.TotalEventsDiscarded,
						SubscribedEvents = trace.SubscribedEvents,
						FilterCurrentSessionOnly = trace.FilterCurrentSessionOnly
					};
				}
				AuditEvent.Default.Emit("get trace", success: true, OperationType.Read, connectionInfo);
				result = traceGet;
			}
			catch
			{
				AuditEvent.Default.Emit("get trace", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	public static async Task<List<TraceGet>> ListTracesAsync()
	{
		List<TraceGet> traces = new List<TraceGet>();
		IReadOnlyList<ConnectionGet> readOnlyList = ConnectionOperations.ListConnections();
		foreach (ConnectionGet item in readOnlyList)
		{
			try
			{
				await using IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(item.ConnectionName);
				if (connectionInfo.Trace != null)
				{
					TraceContext trace = connectionInfo.Trace;
					double totalSeconds = (DateTime.UtcNow - trace.StartTime).TotalSeconds;
					traces.Add(new TraceGet
					{
						TraceName = trace.TraceName,
						Status = (trace.IsPaused ? "paused" : "active"),
						StartTime = trace.StartTime,
						Duration = totalSeconds,
						EventsCaptured = trace.TotalEventsCaptured,
						EventsDiscarded = trace.TotalEventsDiscarded,
						SubscribedEvents = trace.SubscribedEvents,
						FilterCurrentSessionOnly = trace.FilterCurrentSessionOnly
					});
				}
			}
			catch
			{
			}
		}
		return traces;
	}

	public static List<AvailableTraceEvent> ListAvailableEvents()
	{
		List<AvailableTraceEvent> list = new List<AvailableTraceEvent>();
		foreach (string allowedEvent in AllowedEvents)
		{
			TraceEventDefinition traceEventDefinition = EventDefinitions[allowedEvent];
			list.Add(new AvailableTraceEvent
			{
				EventName = allowedEvent,
				Category = traceEventDefinition.Category,
				Description = traceEventDefinition.Description
			});
		}
		return (from e in list
			orderby e.Category, e.EventName
			select e).ToList();
	}

	public static async Task<TracePauseResult> PauseTrace(string? connectionName)
	{
		TracePauseResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			ConnectionValidator.ValidateForTrace(connectionInfo);
			if (connectionInfo.Trace == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("No active trace on this connection", ErrorSource.User);
			}
			TraceContext trace = connectionInfo.Trace;
			if (trace.IsPaused)
			{
				result = new TracePauseResult
				{
					TraceName = trace.TraceName,
					Status = "paused",
					StartTime = trace.StartTime,
					Duration = (DateTime.UtcNow - trace.StartTime).TotalSeconds,
					EventsCaptured = trace.TotalEventsCaptured,
					EventsDiscarded = trace.TotalEventsDiscarded,
					SubscribedEvents = trace.SubscribedEvents,
					Warnings = new List<string> { "Trace is already paused" },
					FilterCurrentSessionOnly = trace.FilterCurrentSessionOnly
				};
			}
			else
			{
				trace.IsPaused = true;
				double totalSeconds = (DateTime.UtcNow - trace.StartTime).TotalSeconds;
				result = new TracePauseResult
				{
					TraceName = trace.TraceName,
					Status = "paused",
					StartTime = trace.StartTime,
					Duration = totalSeconds,
					EventsCaptured = trace.TotalEventsCaptured,
					EventsDiscarded = trace.TotalEventsDiscarded,
					SubscribedEvents = trace.SubscribedEvents,
					FilterCurrentSessionOnly = trace.FilterCurrentSessionOnly
				};
			}
		}
		return result;
	}

	public static async Task<TraceResumeResult> ResumeTrace(string? connectionName)
	{
		TraceResumeResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			ConnectionValidator.ValidateForTrace(connectionInfo);
			if (connectionInfo.Trace == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("No active trace on this connection", ErrorSource.User);
			}
			TraceContext trace = connectionInfo.Trace;
			if (!trace.IsPaused)
			{
				result = new TraceResumeResult
				{
					TraceName = trace.TraceName,
					Status = "active",
					StartTime = trace.StartTime,
					Duration = (DateTime.UtcNow - trace.StartTime).TotalSeconds,
					EventsCaptured = trace.TotalEventsCaptured,
					EventsDiscarded = trace.TotalEventsDiscarded,
					SubscribedEvents = trace.SubscribedEvents,
					Warnings = new List<string> { "Trace is not paused" },
					FilterCurrentSessionOnly = trace.FilterCurrentSessionOnly
				};
			}
			else
			{
				trace.IsPaused = false;
				double totalSeconds = (DateTime.UtcNow - trace.StartTime).TotalSeconds;
				result = new TraceResumeResult
				{
					TraceName = trace.TraceName,
					Status = "active",
					StartTime = trace.StartTime,
					Duration = totalSeconds,
					EventsCaptured = trace.TotalEventsCaptured,
					EventsDiscarded = trace.TotalEventsDiscarded,
					SubscribedEvents = trace.SubscribedEvents,
					FilterCurrentSessionOnly = trace.FilterCurrentSessionOnly
				};
			}
		}
		return result;
	}

	public static async Task<TraceClearResult> ClearTraceEvents(string? connectionName)
	{
		TraceClearResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			ConnectionValidator.ValidateForTrace(connectionInfo);
			if (connectionInfo.Trace == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("No active trace on this connection", ErrorSource.User);
			}
			TraceContext trace = connectionInfo.Trace;
			int count = trace.CapturedEvents.Count;
			trace.CapturedEvents.Clear();
			trace.TotalEventsCaptured = 0;
			trace.TotalEventsDiscarded = 0;
			result = new TraceClearResult
			{
				TraceName = trace.TraceName,
				EventsCleared = count,
				Status = (trace.IsPaused ? "paused" : "active")
			};
		}
		return result;
	}

	internal static List<CapturedTraceEvent> GetCapturedEventsInternal(IConnectionInfo info)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ConnectionValidator.ValidateForTrace(info);
		if (info.Trace == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("No active trace on this connection", ErrorSource.User);
		}
		return info.Trace.CapturedEvents;
	}

	internal static async Task<List<CapturedTraceEvent>> GetCapturedEvents(string? connectionName)
	{
		List<CapturedTraceEvent> capturedEventsInternal;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			capturedEventsInternal = GetCapturedEventsInternal(info);
		}
		return capturedEventsInternal;
	}

	public static async Task<TraceEventFetch> FetchTraceEvents(string? connectionName, bool clearAfterFetch = true)
	{
		TraceEventFetch result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			ConnectionValidator.ValidateForTrace(connectionInfo);
			if (connectionInfo.Trace == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("No active trace on this connection", ErrorSource.User);
			}
			TraceContext trace = connectionInfo.Trace;
			List<CapturedTraceEvent> list = new List<CapturedTraceEvent>(trace.CapturedEvents);
			TraceEventFetch traceEventFetch = new TraceEventFetch
			{
				TraceName = trace.TraceName,
				EventCount = list.Count,
				Cleared = clearAfterFetch,
				Events = list
			};
			if (clearAfterFetch)
			{
				trace.CapturedEvents.Clear();
				trace.TotalEventsCaptured = 0;
				trace.TotalEventsDiscarded = 0;
			}
			result = traceEventFetch;
		}
		return result;
	}

	public static async Task<TraceEventJSONExport> ExportTraceEventsToJSON(string? connectionName, string filePath, bool clearAfterFetch = false)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("File path is required for ExportJSON operation", ErrorSource.User);
		}
		List<string> list = ExportContentProcessor.ValidateFilePath(filePath);
		if (list.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid file path: " + string.Join(", ", list), ErrorSource.User);
		}
		TraceEventJSONExport result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				ConnectionValidator.ValidateForTrace(connectionInfo);
				if (connectionInfo.Trace == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("No active trace on this connection", ErrorSource.User);
				}
				TraceContext trace = connectionInfo.Trace;
				List<CapturedTraceEvent> list2 = new List<CapturedTraceEvent>(trace.CapturedEvents);
				string contents = System.Text.Json.JsonSerializer.Serialize(new
				{
					TraceName = trace.TraceName,
					ExportTime = DateTime.UtcNow,
					TraceStartTime = trace.StartTime,
					EventCount = list2.Count,
					SubscribedEvents = trace.SubscribedEvents,
					Events = list2
				}, new JsonSerializerOptions
				{
					WriteIndented = true,
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				});
				string text;
				try
				{
					text = (Path.IsPathRooted(filePath) ? filePath : Path.GetFullPath(filePath));
					string directoryName = Path.GetDirectoryName(text);
					if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
					{
						Directory.CreateDirectory(directoryName);
					}
					File.WriteAllText(text, contents);
				}
				catch (Exception ex)
				{
					throw new McpExceptionWithSource("Failed to save trace events to file: " + ex.Message, ex, null, "Failed to save trace events to file.");
				}
				TraceEventJSONExport traceEventJSONExport = new TraceEventJSONExport
				{
					TraceName = trace.TraceName,
					EventCount = list2.Count,
					Cleared = clearAfterFetch,
					FilePath = text
				};
				if (clearAfterFetch)
				{
					trace.CapturedEvents.Clear();
					trace.TotalEventsCaptured = 0;
					trace.TotalEventsDiscarded = 0;
				}
				AuditEvent.Default.Emit("export trace events to JSON", success: true, OperationType.Read, connectionInfo);
				result = traceEventJSONExport;
			}
			catch
			{
				AuditEvent.Default.Emit("export trace events to JSON", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<string> DetermineEventsToSubscribe(List<string>? requestedEvents)
	{
		List<string> list = new List<string>();
		if (requestedEvents == null || requestedEvents.Count == 0)
		{
			list.AddRange(DefaultEvents);
		}
		else
		{
			List<string> list2 = requestedEvents.Except<string>(AllowedEvents, StringComparer.OrdinalIgnoreCase).ToList();
			if (list2.Any())
			{
				throw new McpExceptionWithSource("Invalid event names: " + string.Join(", ", list2), ErrorSource.User, "Invalid trace event names supplied.");
			}
			list.AddRange(requestedEvents);
		}
		return list.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
	}

	private static void ApplySessionFilter(TraceContext context, IConnectionInfo info, string? applicationName = null)
	{
		if (info.AdomdConnection == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot apply session filter: ADOMD connection not available", ErrorSource.User);
		}
		if (applicationName == null)
		{
			applicationName = "MCP-PBIModeling";
		}
		string? sessionId = info.SessionId;
		if (string.IsNullOrEmpty(sessionId))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot apply session filter: SessionID not available", ErrorSource.User);
		}
		XmlNode filter = CreateSessionIdFilter(sessionId, applicationName);
		context.Trace.Filter = filter;
	}

	private static XmlNode CreateSessionIdFilter(string sessionId, string applicationName)
	{
		string xml = $"<Or xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\">\n  <Equal><ColumnID>{39}</ColumnID><Value>{sessionId}</Value></Equal>\n  <Equal><ColumnID>{37}</ColumnID><Value>{applicationName}</Value></Equal>\n</Or>";
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.LoadXml(xml);
		return xmlDocument.DocumentElement;
	}

	private static void AddEventsToTrace(TraceContext context, List<string> events)
	{
		foreach (string @event in events)
		{
			if (Enum.TryParse<TraceEventClass>(@event, ignoreCase: true, out var result))
			{
				AddEventColumns(context.Trace.Events.Add(result), result);
			}
		}
	}

	private static void AddEventColumns(Microsoft.AnalysisServices.Tabular.TraceEvent traceEvent, TraceEventClass eventClass)
	{
		string key = eventClass.ToString();
		if (!EventDefinitions.TryGetValue(key, out TraceEventDefinition value))
		{
			AddCommonColumns(traceEvent);
			return;
		}
		foreach (string column in value.Columns)
		{
			if (TryMapColumnNameToEnum(column, out var traceColumn))
			{
				try
				{
					traceEvent.Columns.Add(traceColumn);
				}
				catch
				{
				}
			}
		}
	}

	private static void AddCommonColumns(Microsoft.AnalysisServices.Tabular.TraceEvent traceEvent)
	{
		TraceColumn[] array = new TraceColumn[16]
		{
			TraceColumn.EventClass,
			TraceColumn.EventSubclass,
			TraceColumn.CurrentTime,
			TraceColumn.StartTime,
			TraceColumn.DatabaseName,
			TraceColumn.DatabaseFriendlyName,
			TraceColumn.Spid,
			TraceColumn.ApplicationName,
			TraceColumn.ActivityID,
			TraceColumn.RequestID,
			TraceColumn.SessionID,
			TraceColumn.TextData,
			TraceColumn.Duration,
			TraceColumn.CpuTime,
			TraceColumn.EndTime,
			TraceColumn.Error
		};
		foreach (TraceColumn item in array)
		{
			try
			{
				traceEvent.Columns.Add(item);
			}
			catch
			{
			}
		}
	}

	private static bool TryMapColumnNameToEnum(string columnName, out TraceColumn traceColumn)
	{
		string value = ((columnName == "SPID") ? "Spid" : ((!(columnName == "CPUTime")) ? columnName : "CpuTime"));
		return Enum.TryParse<TraceColumn>(value, ignoreCase: true, out traceColumn);
	}

	private static void SetupTraceEventHandler(TraceContext context)
	{
		context.Trace.OnEvent += delegate(object sender, Microsoft.AnalysisServices.Tabular.TraceEventArgs args)
		{
			if (context.IsPaused)
			{
				context.TotalEventsDiscarded++;
			}
			else
			{
				CapturedTraceEvent item = MapTraceEventArgs(args);
				context.CapturedEvents.Add(item);
				context.TotalEventsCaptured++;
				if (!context.IsActive)
				{
					context.IsActive = true;
				}
			}
		};
	}

	private static CapturedTraceEvent MapTraceEventArgs(Microsoft.AnalysisServices.Tabular.TraceEventArgs args)
	{
		string key = args.EventClass.ToString();
		HashSet<string> availableColumns = null;
		if (EventDefinitions.TryGetValue(key, out TraceEventDefinition value))
		{
			availableColumns = value.Columns;
		}
		return new CapturedTraceEvent
		{
			EventClassName = args.EventClass.ToString(),
			EventSubclassName = (HasColumn("EventSubclass") ? args.EventSubclass.ToString() : null),
			TextData = (HasColumn("TextData") ? args.TextData : null),
			DatabaseName = ((HasColumn("DatabaseName") || HasColumn("DatabaseFriendlyName")) ? args.DatabaseName : null),
			ActivityId = (HasColumn("ActivityID") ? args.ActivityID : null),
			RequestId = (HasColumn("RequestID") ? args.RequestID : null),
			SessionId = (HasColumn("SessionID") ? args.SessionID : null),
			ApplicationName = (HasColumn("ApplicationName") ? args.ApplicationName : null),
			CurrentTime = (HasColumn("CurrentTime") ? new DateTime?(args.CurrentTime) : ((DateTime?)null)),
			StartTime = (HasColumn("StartTime") ? new DateTime?(args.StartTime) : ((DateTime?)null)),
			Duration = (HasColumn("Duration") ? new long?(args.Duration) : ((long?)null)),
			CpuTime = ((HasColumn("CPUTime") || HasColumn("CpuTime")) ? new long?(args.CpuTime) : ((long?)null)),
			EndTime = (HasColumn("EndTime") ? new DateTime?(args.EndTime) : ((DateTime?)null)),
			NTUserName = ((HasColumn("NTUserName") || HasColumn("NTCanonicalUserName") || HasColumn("NTDomainName")) ? args.NTUserName : null),
			RequestProperties = (HasColumn("RequestProperties") ? args.RequestProperties : null),
			RequestParameters = (HasColumn("RequestParameters") ? args.RequestParameters : null),
			ObjectName = (HasColumn("ObjectName") ? args.ObjectName : null),
			ObjectPath = (HasColumn("ObjectPath") ? args.ObjectPath : null),
			ObjectReference = (HasColumn("ObjectReference") ? args.ObjectReference : null),
			Spid = ((HasColumn("SPID") || HasColumn("Spid")) ? args.Spid : null),
			IntegerData = (HasColumn("IntegerData") ? new long?(args.IntegerData) : ((long?)null)),
			ProgressTotal = (HasColumn("ProgressTotal") ? new long?(args.ProgressTotal) : ((long?)null)),
			ObjectId = (HasColumn("ObjectID") ? args.ObjectID : null),
			Error = (HasColumn("Error") ? args.Error : null)
		};
		bool HasColumn(string columnName)
		{
			if (availableColumns != null)
			{
				return availableColumns.Contains(columnName);
			}
			return true;
		}
	}

	private static void InitializeEventDefinitions()
	{
		AddEventDef("QueryBegin", "Queries Events", "Query begin.", "ActivityID", "ApplicationContext", "ApplicationName", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "EventClass", "EventSubclass", "Identity", "NTCanonicalUserName", "NTDomainName", "NTUserName", "RequestID", "RequestParameters", "RequestProperties", "SPID", "ServerName", "SessionID", "StartTime", "TextData", "UserObjectID");
		AddEventDef("QueryEnd", "Queries Events", "Query end.", "ActivityID", "ApplicationContext", "ApplicationName", "CPUTime", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "Error", "ErrorType", "EventClass", "EventSubclass", "Identity", "NTCanonicalUserName", "NTDomainName", "NTUserName", "RequestID", "ServerName", "SessionID", "Severity", "SPID", "StartTime", "Success", "TextData", "UserObjectID");
		AddEventDef("CommandBegin", "Command Events", "Command begin.", "ActivityID", "ApplicationContext", "ApplicationName", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "EventClass", "EventSubclass", "Identity", "NTCanonicalUserName", "NTDomainName", "NTUserName", "RequestID", "RequestParameters", "RequestProperties", "SPID", "ServerName", "SessionID", "SessionType", "StartTime", "TextData", "UserObjectID");
		AddEventDef("CommandEnd", "Command Events", "Command end.", "ActivityID", "ApplicationContext", "ApplicationName", "CPUTime", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "Error", "ErrorType", "EventClass", "EventSubclass", "Identity", "IntegerData", "NTCanonicalUserName", "NTDomainName", "NTUserName", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "Severity", "StartTime", "Success", "TextData", "UserObjectID");
		AddEventDef("Error", "Errors and Warnings", "Server error.", "ActivityID", "ApplicationContext", "ApplicationName", "CalculationExpression", "ClientHostName", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Error", "ErrorType", "EventClass", "Identity", "NTDomainName", "NTUserName", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "Severity", "StartTime", "Success", "TextData", "UserObjectID");
		AddEventDef("DiscoverBegin", "Discover Events", "Start of Discover Request.", "ActivityID", "ApplicationContext", "ApplicationName", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "EventClass", "EventSubclass", "Identity", "NTCanonicalUserName", "NTDomainName", "NTUserName", "RequestID", "RequestProperties", "SPID", "ServerName", "SessionID", "StartTime", "TextData", "UserObjectID");
		AddEventDef("DiscoverEnd", "Discover Events", "End of Discover Request.", "ActivityID", "ApplicationContext", "ApplicationName", "CPUTime", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "Error", "ErrorType", "EventClass", "EventSubclass", "Identity", "IntegerData", "NTCanonicalUserName", "NTDomainName", "NTUserName", "RequestID", "RequestProperties", "SPID", "ServerName", "SessionID", "Severity", "StartTime", "Success", "TextData", "UserObjectID");
		AddEventDef("ProgressReportBegin", "Progress Reports", "Progress report begin.", "ActivityID", "ApplicationContext", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "EventClass", "EventSubclass", "Identity", "JobID", "NTCanonicalUserName", "NTDomainName", "NTUserName", "ObjectID", "ObjectName", "ObjectPath", "ObjectReference", "ObjectType", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "StartTime", "TextData", "UserObjectID");
		AddEventDef("ProgressReportEnd", "Progress Reports", "Progress report end.", "ActivityID", "ApplicationContext", "CPUTime", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "Error", "ErrorType", "EventClass", "EventSubclass", "Identity", "IntegerData", "JobID", "NTCanonicalUserName", "NTDomainName", "NTUserName", "ObjectID", "ObjectName", "ObjectPath", "ObjectReference", "ObjectType", "ProgressTotal", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "Severity", "StartTime", "Success", "TextData", "UserObjectID");
		AddEventDef("ProgressReportCurrent", "Progress Reports", "Progress report current.", "ActivityID", "ApplicationContext", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "EventClass", "EventSubclass", "Identity", "IntegerData", "JobID", "NTDomainName", "NTUserName", "ObjectID", "ObjectName", "ObjectPath", "ObjectReference", "ObjectType", "ProgressTotal", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "StartTime", "TextData", "UserObjectID");
		AddEventDef("ProgressReportError", "Progress Reports", "Progress report error.", "ActivityID", "ApplicationContext", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "Error", "ErrorType", "EventClass", "EventSubclass", "Identity", "IntegerData", "JobID", "NTDomainName", "NTUserName", "ObjectID", "ObjectName", "ObjectPath", "ObjectReference", "ObjectType", "ProgressTotal", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "Severity", "StartTime", "TextData", "UserObjectID");
		AddEventDef("VertiPaqSEQueryBegin", "Query Processing", "VertiPaq SE Query", "ActivityID", "ApplicationContext", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "EventClass", "EventSubclass", "Identity", "JobID", "NTCanonicalUserName", "NTDomainName", "NTUserName", "ObjectID", "ObjectName", "ObjectPath", "ObjectReference", "ObjectType", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "StartTime", "TextData", "UserObjectID");
		AddEventDef("VertiPaqSEQueryEnd", "Query Processing", "VertiPaq SE Query", "ActivityID", "ApplicationContext", "CPUTime", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "Error", "ErrorType", "EventClass", "EventSubclass", "Identity", "IntegerData", "JobID", "NTCanonicalUserName", "NTDomainName", "NTUserName", "ObjectID", "ObjectName", "ObjectPath", "ObjectReference", "ObjectType", "ProgressTotal", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "Severity", "StartTime", "Success", "TextData", "UserObjectID");
		AddEventDef("VertiPaqSEQueryCacheMatch", "Query Processing", "VertiPaq SE Query Cache Use", "ActivityID", "ApplicationContext", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "EventClass", "EventSubclass", "Identity", "JobID", "NTCanonicalUserName", "NTDomainName", "NTUserName", "ObjectID", "ObjectName", "ObjectPath", "ObjectReference", "ObjectType", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "TextData", "UserObjectID");
		AddEventDef("DirectQueryBegin", "Query Processing", "DirectQuery Begin.", "ActivityID", "ApplicationContext", "ApplicationName", "CPUTime", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Error", "ErrorType", "EventClass", "Identity", "JobID", "NTUserName", "ObjectID", "ObjectName", "ObjectPath", "ObjectType", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "Severity", "StartTime", "Success", "TextData");
		AddEventDef("DirectQueryEnd", "Query Processing", "DirectQuery End.", "ActivityID", "ApplicationContext", "ApplicationName", "CPUTime", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "Error", "ErrorType", "EventClass", "Identity", "JobID", "NTUserName", "ObjectID", "ObjectName", "ObjectPath", "ObjectType", "RequestID", "SPID", "ServerName", "SessionID", "SessionType", "Severity", "StartTime", "Success", "TextData");
		AddEventDef("DAXQueryPlan", "Query Processing", "DAX logical/physical plan tree for VertiPaq and DirectQuery modes.", "ActivityID", "ApplicationContext", "ApplicationName", "CPUTime", "ClientHostName", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "EventClass", "EventSubclass", "Identity", "IntegerData", "NTCanonicalUserName", "RequestID", "SPID", "ServerName", "SessionID", "StartTime", "TextData");
		AddEventDef("ResourceUsage", "Query Processing", "Reports reads, writes, cpu usage after end of commands and queries.", "ActivityID", "ApplicationContext", "ApplicationName", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "EventClass", "Identity", "NTCanonicalUserName", "NTDomainName", "NTUserName", "RequestID", "SPID", "ServerName", "SessionID", "TextData", "UserObjectID");
		AddEventDef("CalculationEvaluation", "Query Processing", "Information about the evaluation of calculations. This event will have a negative impact on performance when turned on.", "ActivityID", "ApplicationContext", "ApplicationName", "CPUTime", "ClientHostName", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "EventClass", "EventSubclass", "Identity", "IntegerData", "NTCanonicalUserName", "RequestID", "SPID", "ServerName", "SessionID", "StartTime", "TextData");
		AddEventDef("DAXEvaluationLog", "Query Processing", "Output of EvaluateAndLog function.", "ActivityID", "ApplicationContext", "ApplicationName", "CPUTime", "ClientHostName", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "EventClass", "Identity", "IntegerData", "Label", "NTCanonicalUserName", "RequestID", "SPID", "ServerName", "SessionID", "StartTime", "TextData");
		AddEventDef("AggregateTableRewriteQuery", "Query Processing", "A query was rewritten according to available aggregate tables.", "ActivityID", "ApplicationContext", "ApplicationName", "ClientHostName", "ClientProcessID", "ConnectionID", "DatabaseFriendlyName", "DatabaseName", "Duration", "EndTime", "EventClass", "EventSubclass", "Identity", "NTCanonicalUserName", "NTDomainName", "NTUserName", "RequestID", "SPID", "ServerName", "SessionID", "StartTime", "Success", "TextData", "UserObjectID");
		AddEventDef("ExecutionMetrics", "Execution Metrics Events", "Customer facing execution metrics.", "ActivityID", "ApplicationContext", "ApplicationName", "DatabaseFriendlyName", "DatabaseName", "EventClass", "Identity", "RequestID", "SPID", "ServerName", "TextData");
		AddEventDef("JobGraph", "Job Graph Events", "Job graph related events", "ActivityID", "ApplicationName", "ClientProcessID", "ConnectionID", "CurrentTime", "DatabaseFriendlyName", "DatabaseName", "EventClass", "EventSubclass", "Identity", "IntegerData", "NTDomainName", "NTUserName", "RequestID", "SPID", "ServerName", "SessionID", "Success", "TextData");
	}

	private static void AddEventDef(string name, string category, string description, params string[] columns)
	{
		EventDefinitions[name] = new TraceEventDefinition
		{
			Name = name,
			Category = category,
			Description = description,
			Columns = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase)
		};
	}
}
