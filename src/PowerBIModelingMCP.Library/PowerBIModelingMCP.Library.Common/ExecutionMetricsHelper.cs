using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Common;

public static class ExecutionMetricsHelper
{
	public static List<CapturedTraceEvent> FilterEventsByRequestId(List<CapturedTraceEvent> allEvents, string requestId)
	{
		if (string.IsNullOrWhiteSpace(requestId))
		{
			return new List<CapturedTraceEvent>();
		}
		return allEvents.Where((CapturedTraceEvent e) => string.Equals(e.RequestId, requestId, StringComparison.OrdinalIgnoreCase)).ToList();
	}

	public static string? ExtractRequestIdFromQueryBegin(List<CapturedTraceEvent> events)
	{
		return (from e in events
			where string.Equals(e.EventClassName, "QueryBegin", StringComparison.OrdinalIgnoreCase)
			orderby e.CurrentTime ?? e.StartTime descending
			select e).FirstOrDefault()?.RequestId;
	}

	public static CalculatedExecutionMetrics CalculateMetrics(List<CapturedTraceEvent> queryEvents)
	{
		CalculatedExecutionMetrics calculatedExecutionMetrics = new CalculatedExecutionMetrics
		{
			Success = true
		};
		if (queryEvents.Count == 0)
		{
			calculatedExecutionMetrics.Success = false;
			calculatedExecutionMetrics.ErrorMessage = "No trace events found for query";
			return calculatedExecutionMetrics;
		}
		calculatedExecutionMetrics.ActivityId = queryEvents.FirstOrDefault()?.ActivityId;
		FixEventTimings(queryEvents);
		CapturedTraceEvent capturedTraceEvent = queryEvents.FirstOrDefault((CapturedTraceEvent e) => string.Equals(e.EventClassName, "QueryBegin", StringComparison.OrdinalIgnoreCase));
		CapturedTraceEvent capturedTraceEvent2 = queryEvents.FirstOrDefault((CapturedTraceEvent e) => string.Equals(e.EventClassName, "QueryEnd", StringComparison.OrdinalIgnoreCase));
		if (capturedTraceEvent2 != null)
		{
			calculatedExecutionMetrics.TotalDuration = capturedTraceEvent2.Duration.GetValueOrDefault();
			calculatedExecutionMetrics.TotalCpuTime = capturedTraceEvent2.CpuTime.GetValueOrDefault();
			calculatedExecutionMetrics.QueryEndDateTime = capturedTraceEvent2.EndTime ?? capturedTraceEvent2.CurrentTime;
			calculatedExecutionMetrics.QueryText = capturedTraceEvent2.TextData;
		}
		if (capturedTraceEvent != null)
		{
			calculatedExecutionMetrics.QueryStartDateTime = capturedTraceEvent.StartTime ?? capturedTraceEvent.CurrentTime;
			if (calculatedExecutionMetrics.QueryText == null)
			{
				calculatedExecutionMetrics.QueryText = capturedTraceEvent.TextData;
			}
		}
		CapturedTraceEvent maxEvent = null;
		CapturedTraceEvent maxEvent2 = null;
		int num = 0;
		long num2 = 0L;
		long num3 = 0L;
		foreach (CapturedTraceEvent queryEvent in queryEvents)
		{
			if (string.Equals(queryEvent.EventClassName, "VertiPaqSEQueryBegin", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(queryEvent.EventSubclassName, "BatchVertiPaqScan", StringComparison.OrdinalIgnoreCase))
				{
					num++;
					num2 = 0L;
					num3 = 0L;
				}
			}
			else if (string.Equals(queryEvent.EventClassName, "VertiPaqSEQueryEnd", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(queryEvent.EventSubclassName, "BatchVertiPaqScan", StringComparison.OrdinalIgnoreCase))
				{
					num--;
					queryEvent.Duration = Math.Max(queryEvent.Duration.GetValueOrDefault() - num2, 0L);
					queryEvent.NetParallelDuration = queryEvent.Duration.GetValueOrDefault();
					queryEvent.CpuTime = Math.Max(queryEvent.CpuTime.GetValueOrDefault() - num3, 0L);
					calculatedExecutionMetrics.StorageEngineDuration += queryEvent.Duration.GetValueOrDefault();
					calculatedExecutionMetrics.StorageEngineNetParallelDuration += queryEvent.Duration.GetValueOrDefault();
					calculatedExecutionMetrics.StorageEngineCpuTime += queryEvent.CpuTime.GetValueOrDefault();
					calculatedExecutionMetrics.StorageEngineQueryCount++;
				}
				else if (string.Equals(queryEvent.EventSubclassName, "VertiPaqScan", StringComparison.OrdinalIgnoreCase))
				{
					if (num > 0)
					{
						queryEvent.InternalBatchEvent = true;
						num2 += queryEvent.NetParallelDuration;
						num3 += queryEvent.CpuTime.GetValueOrDefault();
					}
					else
					{
						UpdateForParallelOperations(ref maxEvent, queryEvent);
						calculatedExecutionMetrics.StorageEngineDuration += queryEvent.Duration.GetValueOrDefault();
					}
					calculatedExecutionMetrics.StorageEngineNetParallelDuration += queryEvent.NetParallelDuration;
					calculatedExecutionMetrics.StorageEngineCpuTime += queryEvent.CpuTime.GetValueOrDefault();
					calculatedExecutionMetrics.StorageEngineQueryCount++;
				}
			}
			else if (string.Equals(queryEvent.EventClassName, "DirectQueryEnd", StringComparison.OrdinalIgnoreCase))
			{
				UpdateForParallelOperations(ref maxEvent2, queryEvent);
				calculatedExecutionMetrics.TotalDirectQueryDuration += queryEvent.Duration.GetValueOrDefault();
				calculatedExecutionMetrics.StorageEngineDuration += queryEvent.Duration.GetValueOrDefault();
				calculatedExecutionMetrics.StorageEngineNetParallelDuration += queryEvent.NetParallelDuration;
				calculatedExecutionMetrics.StorageEngineCpuTime += queryEvent.CpuTime.GetValueOrDefault();
				calculatedExecutionMetrics.StorageEngineQueryCount++;
				calculatedExecutionMetrics.TotalDirectQueryCount++;
			}
			else if (string.Equals(queryEvent.EventClassName, "VertiPaqSEQueryCacheMatch", StringComparison.OrdinalIgnoreCase))
			{
				calculatedExecutionMetrics.VertipaqCacheMatches++;
			}
		}
		calculatedExecutionMetrics.FormulaEngineDuration = CalculateFormulaEngineDuration(queryEvents);
		calculatedExecutionMetrics.TotalDuration = Math.Max(calculatedExecutionMetrics.FormulaEngineDuration, calculatedExecutionMetrics.TotalDuration);
		if ((double)(calculatedExecutionMetrics.StorageEngineNetParallelDuration + calculatedExecutionMetrics.FormulaEngineDuration) < (double)calculatedExecutionMetrics.TotalDuration)
		{
			calculatedExecutionMetrics.StorageEngineDuration = calculatedExecutionMetrics.StorageEngineNetParallelDuration;
			calculatedExecutionMetrics.FormulaEngineDuration = calculatedExecutionMetrics.TotalDuration - calculatedExecutionMetrics.StorageEngineDuration;
		}
		else
		{
			calculatedExecutionMetrics.StorageEngineDuration = calculatedExecutionMetrics.TotalDuration - calculatedExecutionMetrics.FormulaEngineDuration;
		}
		calculatedExecutionMetrics.TotalCpuFactor = ((calculatedExecutionMetrics.TotalDuration == 0L) ? 0.0 : ((double)calculatedExecutionMetrics.TotalCpuTime / (double)calculatedExecutionMetrics.TotalDuration));
		calculatedExecutionMetrics.StorageEngineCpuFactor = ((calculatedExecutionMetrics.StorageEngineDuration == 0L) ? 0.0 : ((double)calculatedExecutionMetrics.StorageEngineCpuTime / (double)calculatedExecutionMetrics.StorageEngineDuration));
		calculatedExecutionMetrics.StorageEngineDurationPercentage = ((calculatedExecutionMetrics.TotalDuration == 0L) ? 0.0 : ((double)calculatedExecutionMetrics.StorageEngineNetParallelDuration / (double)calculatedExecutionMetrics.TotalDuration * 100.0));
		calculatedExecutionMetrics.FormulaEngineDurationPercentage = ((calculatedExecutionMetrics.TotalDuration == 0L) ? 0.0 : ((double)calculatedExecutionMetrics.FormulaEngineDuration / (double)calculatedExecutionMetrics.TotalDuration * 100.0));
		calculatedExecutionMetrics.VertipaqCacheMatchesPercentage = ((calculatedExecutionMetrics.StorageEngineQueryCount == 0) ? 0.0 : ((double)calculatedExecutionMetrics.VertipaqCacheMatches / (double)calculatedExecutionMetrics.StorageEngineQueryCount * 100.0));
		return calculatedExecutionMetrics;
	}

	private static void FixEventTimings(List<CapturedTraceEvent> events)
	{
		foreach (CapturedTraceEvent @event in events)
		{
			if (!string.Equals(@event.EventClassName, "VertiPaqSEQueryEnd", StringComparison.OrdinalIgnoreCase) && !string.Equals(@event.EventClassName, "DirectQueryEnd", StringComparison.OrdinalIgnoreCase) && !string.Equals(@event.EventClassName, "QueryEnd", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			DateTime? dateTime = @event.StartTime ?? @event.CurrentTime;
			DateTime? dateTime2 = @event.EndTime ?? @event.CurrentTime;
			long valueOrDefault = @event.Duration.GetValueOrDefault();
			DateTime? dateTime3 = dateTime2;
			DateTime? dateTime4 = dateTime;
			if (dateTime3.HasValue == dateTime4.HasValue && (!dateTime3.HasValue || dateTime3.GetValueOrDefault() == dateTime4.GetValueOrDefault()) && valueOrDefault > 0)
			{
				@event.EndTime = dateTime?.AddMilliseconds(valueOrDefault);
			}
			else
			{
				dateTime4 = dateTime2;
				dateTime3 = dateTime;
				if ((dateTime4.HasValue & dateTime3.HasValue) && dateTime4.GetValueOrDefault() >= dateTime3.GetValueOrDefault() && dateTime2.HasValue && dateTime.HasValue)
				{
					long num = (long)(dateTime2.Value - dateTime.Value).TotalMilliseconds;
					if (num > valueOrDefault)
					{
						@event.Duration = num;
					}
				}
			}
			@event.NetParallelDuration = @event.Duration.GetValueOrDefault();
		}
	}

	private static void UpdateForParallelOperations(ref CapturedTraceEvent? maxEvent, CapturedTraceEvent traceEvent)
	{
		if (maxEvent == null)
		{
			maxEvent = traceEvent;
			return;
		}
		DateTime? dateTime = maxEvent.EndTime ?? maxEvent.CurrentTime;
		DateTime? dateTime2 = traceEvent.StartTime ?? traceEvent.CurrentTime;
		DateTime? dateTime3 = traceEvent.EndTime ?? traceEvent.CurrentTime;
		if (!dateTime.HasValue || !dateTime2.HasValue || !dateTime3.HasValue)
		{
			return;
		}
		if ((dateTime.Value - dateTime2.Value).TotalMilliseconds > 0.0)
		{
			if (dateTime.Value > dateTime3.Value)
			{
				traceEvent.NetParallelDuration = 0L;
				return;
			}
			traceEvent.NetParallelDuration = (long)(dateTime3.Value - dateTime.Value).TotalMilliseconds;
			maxEvent = traceEvent;
		}
		else
		{
			maxEvent = traceEvent;
		}
	}

	private static long CalculateFormulaEngineDuration(List<CapturedTraceEvent> events)
	{
		int num = 0;
		double num2 = 0.0;
		DateTime? dateTime = null;
		foreach (CapturedTraceEvent @event in events)
		{
			DateTime? dateTime2 = @event.StartTime ?? @event.CurrentTime;
			DateTime? dateTime3 = @event.EndTime ?? @event.CurrentTime;
			if (string.Equals(@event.EventClassName, "QueryBegin", StringComparison.OrdinalIgnoreCase))
			{
				dateTime = dateTime2;
			}
			else if (string.Equals(@event.EventClassName, "QueryEnd", StringComparison.OrdinalIgnoreCase))
			{
				if (num == 0 && dateTime.HasValue && dateTime3.HasValue)
				{
					double totalMilliseconds = (dateTime3.Value - dateTime.Value).TotalMilliseconds;
					num2 += totalMilliseconds;
				}
			}
			else if (string.Equals(@event.EventClassName, "VertiPaqSEQueryBegin", StringComparison.OrdinalIgnoreCase) || string.Equals(@event.EventClassName, "DirectQueryBegin", StringComparison.OrdinalIgnoreCase))
			{
				if (num == 0 && dateTime.HasValue && dateTime2.HasValue)
				{
					double totalMilliseconds2 = (dateTime2.Value - dateTime.Value).TotalMilliseconds;
					num2 += totalMilliseconds2;
				}
				num++;
			}
			else if (string.Equals(@event.EventClassName, "VertiPaqSEQueryEnd", StringComparison.OrdinalIgnoreCase) || string.Equals(@event.EventClassName, "DirectQueryEnd", StringComparison.OrdinalIgnoreCase))
			{
				num--;
				if (num == 0)
				{
					dateTime = dateTime3;
				}
			}
		}
		return (long)num2;
	}

	public static async Task<(bool Success, string ErrorMessage)> WaitForQueryMetricsEvents(string? connectionName, int timeoutSeconds = 20)
	{
		int maxIterations = timeoutSeconds * 1000 / 100;
		int iteration = 0;
		while (iteration < maxIterations)
		{
			try
			{
				List<CapturedTraceEvent> list = await TraceOperations.GetCapturedEvents(connectionName);
				for (int num = list.Count - 1; num >= 0; num--)
				{
					string eventClassName = list[num].EventClassName;
					if (string.Equals(eventClassName, "QueryEnd", StringComparison.OrdinalIgnoreCase) || string.Equals(eventClassName, "Error", StringComparison.OrdinalIgnoreCase))
					{
						return (Success: true, ErrorMessage: string.Empty);
					}
				}
				Thread.Sleep(100);
				iteration++;
			}
			catch (Exception ex)
			{
				return (Success: false, ErrorMessage: "Error while waiting for query metrics events: " + ex.Message);
			}
		}
		return (Success: false, ErrorMessage: $"Timeout after {timeoutSeconds} seconds waiting for QueryEnd or Error event");
	}

	public static CalculatedExecutionMetrics ExtractQueryMetrics(List<CapturedTraceEvent> allEvents)
	{
		string text = ExtractRequestIdFromQueryBegin(allEvents);
		if (string.IsNullOrWhiteSpace(text))
		{
			return new CalculatedExecutionMetrics
			{
				Success = false,
				ErrorMessage = "No QueryBegin event found in trace events"
			};
		}
		List<CapturedTraceEvent> list = FilterEventsByRequestId(allEvents, text);
		if (list.Count == 0)
		{
			return new CalculatedExecutionMetrics
			{
				Success = false,
				ErrorMessage = "No trace events found with RequestId: " + text
			};
		}
		return CalculateMetrics(list);
	}

	public static ReportedExecutionMetrics ExtractServerReportedMetrics(List<CapturedTraceEvent> capturedEvents)
	{
		if (capturedEvents == null || capturedEvents.Count == 0)
		{
			return new ReportedExecutionMetrics
			{
				Success = false,
				ErrorMessage = "No captured events provided"
			};
		}
		List<CapturedTraceEvent> list = capturedEvents.Where((CapturedTraceEvent e) => string.Equals(e.EventClassName, "ExecutionMetrics", StringComparison.OrdinalIgnoreCase)).ToList();
		if (list.Count == 0)
		{
			return new ReportedExecutionMetrics
			{
				Success = false,
				ErrorMessage = "No ExecutionMetrics events found in captured events"
			};
		}
		List<(CapturedTraceEvent, ReportedExecutionMetrics, JsonElement)> list2 = new List<(CapturedTraceEvent, ReportedExecutionMetrics, JsonElement)>();
		foreach (CapturedTraceEvent item2 in list)
		{
			string textData = item2.TextData;
			if (string.IsNullOrWhiteSpace(textData))
			{
				continue;
			}
			try
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(textData);
				JsonElement rootElement = jsonDocument.RootElement;
				string? a = GetString(rootElement, "commandType");
				string text = GetString(rootElement, "queryDialect");
				if (string.Equals(a, "Statement", StringComparison.OrdinalIgnoreCase) && text == "3")
				{
					ReportedExecutionMetrics item = new ReportedExecutionMetrics
					{
						Success = true,
						ActivityId = item2.ActivityId,
						RequestId = item2.RequestId,
						TimeStart = GetDateTime(rootElement, "timeStart"),
						TimeEnd = GetDateTime(rootElement, "timeEnd"),
						DurationMs = GetInt64(rootElement, "durationMs"),
						DatasourceConnectionThrottleTimeMs = GetInt64(rootElement, "datasourceConnectionThrottleTimeMs"),
						DirectQueryConnectionTimeMs = GetInt64(rootElement, "directQueryConnectionTimeMs"),
						DirectQueryIterationTimeMs = GetInt64(rootElement, "directQueryIterationTimeMs"),
						DirectQueryTotalTimeMs = GetInt64(rootElement, "directQueryTotalTimeMs"),
						ExternalQueryExecutionTimeMs = GetInt64(rootElement, "externalQueryExecutionTimeMs"),
						VertipaqJobCpuTimeMs = GetInt64(rootElement, "vertipaqJobCpuTimeMs"),
						MEngineCpuTimeMs = GetInt64(rootElement, "mEngineCpuTimeMs"),
						QueryProcessingCpuTimeMs = GetInt64(rootElement, "queryProcessingCpuTimeMs"),
						TotalCpuTimeMs = GetInt64(rootElement, "totalCpuTimeMs"),
						ExecutionDelayMs = GetInt64(rootElement, "executionDelayMs"),
						CapacityThrottlingMs = GetInt64(rootElement, "capacityThrottlingMs"),
						ApproximatePeakMemConsumptionKB = GetInt64(rootElement, "approximatePeakMemConsumptionKB"),
						MEnginePeakMemoryKB = GetInt64(rootElement, "mEnginePeakMemoryKB"),
						ExternalQueryTimeoutMs = GetInt64(rootElement, "externalQueryTimeoutMs"),
						DirectQueryTimeoutMs = GetInt64(rootElement, "directQueryTimeoutMs"),
						TabularConnectionTimeoutMs = GetInt64(rootElement, "tabularConnectionTimeoutMs"),
						CommandType = GetString(rootElement, "commandType"),
						DiscoverType = GetString(rootElement, "discoverType"),
						QueryDialect = GetString(rootElement, "queryDialect"),
						ErrorCount = GetInt32(rootElement, "errorCount"),
						RefreshParallelism = GetInt32(rootElement, "refreshParallelism"),
						VertipaqTotalRows = GetInt64(rootElement, "vertipaqTotalRows"),
						QueryResultRows = GetInt64(rootElement, "queryResultRows"),
						DirectQueryRequestCount = GetInt32(rootElement, "directQueryRequestCount"),
						DirectQueryTotalRows = GetInt64(rootElement, "directQueryTotalRows"),
						QsoReplicaVersion = GetString(rootElement, "qsoReplicaVersion"),
						IntendedUsage = GetInt32(rootElement, "intendedUsage"),
						DirectLakeFallbackNotFramed = GetBool(rootElement, "directLakeFallbackNotFramed"),
						DirectLakeFallbackView = GetBool(rootElement, "directLakeFallbackView"),
						DirectLakeFallbackTooManyFiles = GetBool(rootElement, "directLakeFallbackTooManyFiles"),
						DirectLakeFallbackTooManyRowgroups = GetBool(rootElement, "directLakeFallbackTooManyRowgroups"),
						DirectLakeFallbackTooManyRows = GetBool(rootElement, "directLakeFallbackTooManyRows"),
						DirectLakeFallbackFramingRls = GetBool(rootElement, "directLakeFallbackFramingRls"),
						DirectLakeFallbackQueryOls = GetBool(rootElement, "directLakeFallbackQueryOls"),
						DirectLakeFallbackQueryRls = GetBool(rootElement, "directLakeFallbackQueryRls")
					};
					list2.Add((item2, item, rootElement));
				}
			}
			catch (JsonException)
			{
			}
		}
		if (list2.Count == 0)
		{
			return new ReportedExecutionMetrics
			{
				Success = false,
				ErrorMessage = $"No ExecutionMetrics events found matching criteria (commandType='Statement', queryDialect=3). Found {list.Count} ExecutionMetrics events total."
			};
		}
		if (list2.Count > 1)
		{
			return new ReportedExecutionMetrics
			{
				Success = false,
				ErrorMessage = $"Found {list2.Count} ExecutionMetrics events matching criteria. Expected exactly one."
			};
		}
		return list2[0].Item2;
	}

	private static long? GetInt64(JsonElement root, string propertyName)
	{
		if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
		{
			return value.GetInt64();
		}
		return null;
	}

	private static int? GetInt32(JsonElement root, string propertyName)
	{
		if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
		{
			return value.GetInt32();
		}
		return null;
	}

	private static string? GetString(JsonElement root, string propertyName)
	{
		if (root.TryGetProperty(propertyName, out var value))
		{
			if (value.ValueKind == JsonValueKind.String)
			{
				return value.GetString();
			}
			if (value.ValueKind == JsonValueKind.Number)
			{
				return value.GetInt64().ToString();
			}
		}
		return null;
	}

	private static DateTime? GetDateTime(JsonElement root, string propertyName)
	{
		if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
		{
			string text = value.GetString();
			if (!string.IsNullOrEmpty(text) && DateTime.TryParse(text, out var result))
			{
				return result;
			}
		}
		return null;
	}

	private static bool? GetBool(JsonElement root, string propertyName)
	{
		if (root.TryGetProperty(propertyName, out var value))
		{
			if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
			{
				return value.GetBoolean();
			}
			if (value.ValueKind == JsonValueKind.Number)
			{
				return value.GetInt64() != 0;
			}
		}
		return null;
	}
}
