using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Common;

public static class ExecutionStatisticsHelper
{
	public static List<CapturedTraceEvent> FilterEventsByRequestId(List<CapturedTraceEvent> allEvents, string requestId)
	{
		if (string.IsNullOrWhiteSpace(requestId))
		{
			return new List<CapturedTraceEvent>();
		}
		return allEvents.Where((CapturedTraceEvent e) => string.Equals(e.RequestId, requestId, StringComparison.OrdinalIgnoreCase)).ToList();
	}

	private static List<CapturedTraceEvent> GetPrimaryVertipaqScans(List<CapturedTraceEvent> queryEvents)
	{
		List<CapturedTraceEvent> list = new List<CapturedTraceEvent>();
		bool flag = false;
		foreach (CapturedTraceEvent queryEvent in queryEvents)
		{
			bool num = string.Equals(queryEvent.EventClassName, "VertiPaqSEQueryBegin", StringComparison.OrdinalIgnoreCase);
			bool flag2 = string.Equals(queryEvent.EventClassName, "VertiPaqSEQueryEnd", StringComparison.OrdinalIgnoreCase);
			bool flag3 = string.Equals(queryEvent.EventSubclassName, "BatchVertiPaqScan", StringComparison.OrdinalIgnoreCase);
			bool flag4 = string.Equals(queryEvent.EventSubclassName, "VertiPaqScan", StringComparison.OrdinalIgnoreCase);
			if (num && flag3)
			{
				flag = true;
			}
			else if (flag2 && flag3)
			{
				flag = false;
				list.Add(queryEvent);
			}
			else if (flag2 && flag4 && !flag)
			{
				list.Add(queryEvent);
			}
		}
		return list;
	}

	public static string? ExtractRequestIdFromQueryBegin(List<CapturedTraceEvent> events)
	{
		return (from e in events
			where string.Equals(e.EventClassName, "QueryBegin", StringComparison.OrdinalIgnoreCase)
			orderby e.CurrentTime ?? e.StartTime descending
			select e).FirstOrDefault()?.RequestId;
	}

	public static QueryExecutionStatistics CalculateStatistics(List<CapturedTraceEvent> queryEvents, bool includeDetailedEvents = false)
	{
		QueryExecutionStatistics queryExecutionStatistics = new QueryExecutionStatistics
		{
			Success = true,
			DetailedEvents = (includeDetailedEvents ? queryEvents : null)
		};
		if (queryEvents.Count == 0)
		{
			queryExecutionStatistics.Success = false;
			queryExecutionStatistics.ErrorMessage = "No trace events found for query";
			return queryExecutionStatistics;
		}
		queryExecutionStatistics.ActivityId = queryEvents.FirstOrDefault()?.ActivityId;
		CapturedTraceEvent capturedTraceEvent = queryEvents.FirstOrDefault((CapturedTraceEvent e) => string.Equals(e.EventClassName, "QueryBegin", StringComparison.OrdinalIgnoreCase));
		CapturedTraceEvent capturedTraceEvent2 = queryEvents.FirstOrDefault((CapturedTraceEvent e) => string.Equals(e.EventClassName, "QueryEnd", StringComparison.OrdinalIgnoreCase));
		if (capturedTraceEvent2 != null)
		{
			queryExecutionStatistics.TotalDuration = capturedTraceEvent2.Duration.GetValueOrDefault();
			queryExecutionStatistics.TotalCpuTime = capturedTraceEvent2.CpuTime.GetValueOrDefault();
			queryExecutionStatistics.QueryEndDateTime = capturedTraceEvent2.EndTime ?? capturedTraceEvent2.CurrentTime;
			queryExecutionStatistics.QueryText = capturedTraceEvent2.TextData;
		}
		if (capturedTraceEvent != null)
		{
			queryExecutionStatistics.QueryStartDateTime = capturedTraceEvent.StartTime ?? capturedTraceEvent.CurrentTime;
			if (queryExecutionStatistics.QueryText == null)
			{
				queryExecutionStatistics.QueryText = capturedTraceEvent.TextData;
			}
		}
		List<CapturedTraceEvent> primaryVertipaqScans = GetPrimaryVertipaqScans(queryEvents);
		queryExecutionStatistics.TotalVertipaqQueryCount = primaryVertipaqScans.Count;
		queryExecutionStatistics.TotalVertipaqDuration = primaryVertipaqScans.Sum((CapturedTraceEvent e) => e.Duration.GetValueOrDefault());
		queryExecutionStatistics.TotalVertipaqCpuTime = primaryVertipaqScans.Sum((CapturedTraceEvent e) => e.CpuTime.GetValueOrDefault());
		List<CapturedTraceEvent> list = queryEvents.Where((CapturedTraceEvent e) => string.Equals(e.EventClassName, "VertiPaqSEQueryCacheMatch", StringComparison.OrdinalIgnoreCase)).ToList();
		queryExecutionStatistics.TotalVertipaqCacheMatches = list.Count;
		List<CapturedTraceEvent> list2 = queryEvents.Where((CapturedTraceEvent e) => string.Equals(e.EventClassName, "DirectQueryEnd", StringComparison.OrdinalIgnoreCase)).ToList();
		queryExecutionStatistics.TotalDirectQueryCount = list2.Count;
		queryExecutionStatistics.TotalDirectQueryDuration = list2.Sum((CapturedTraceEvent e) => e.Duration.GetValueOrDefault());
		return queryExecutionStatistics;
	}

	public static async Task<(bool Success, string ErrorMessage)> WaitForQueryStatisticsEvents(string? connectionName, int timeoutSeconds = 20)
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
				return (Success: false, ErrorMessage: "Error while waiting for query statistics events: " + ex.Message);
			}
		}
		return (Success: false, ErrorMessage: $"Timeout after {timeoutSeconds} seconds waiting for QueryEnd or Error event");
	}

	public static QueryExecutionStatistics ExtractQueryStatistics(List<CapturedTraceEvent> allEvents, bool includeDetailedEvents = false)
	{
		string text = ExtractRequestIdFromQueryBegin(allEvents);
		if (string.IsNullOrWhiteSpace(text))
		{
			return new QueryExecutionStatistics
			{
				Success = false,
				ErrorMessage = "No QueryBegin event found in trace events"
			};
		}
		List<CapturedTraceEvent> list = FilterEventsByRequestId(allEvents, text);
		if (list.Count == 0)
		{
			return new QueryExecutionStatistics
			{
				Success = false,
				ErrorMessage = "No trace events found with RequestId: " + text
			};
		}
		return CalculateStatistics(list, includeDetailedEvents);
	}
}
