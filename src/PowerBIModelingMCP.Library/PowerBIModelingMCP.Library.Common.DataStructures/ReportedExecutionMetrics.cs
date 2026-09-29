using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ReportedExecutionMetrics : ResultBase
{
	public string? ErrorMessage { get; set; }

	public string? ActivityId { get; set; }

	public string? RequestId { get; set; }

	public DateTime? TimeStart { get; set; }

	public DateTime? TimeEnd { get; set; }

	public long? DurationMs { get; set; }

	public long? DatasourceConnectionThrottleTimeMs { get; set; }

	public long? DirectQueryConnectionTimeMs { get; set; }

	public long? DirectQueryIterationTimeMs { get; set; }

	public long? DirectQueryTotalTimeMs { get; set; }

	public long? ExternalQueryExecutionTimeMs { get; set; }

	public long? VertipaqJobCpuTimeMs { get; set; }

	public long? MEngineCpuTimeMs { get; set; }

	public long? QueryProcessingCpuTimeMs { get; set; }

	public long? TotalCpuTimeMs { get; set; }

	public long? ExecutionDelayMs { get; set; }

	public long? CapacityThrottlingMs { get; set; }

	public long? ApproximatePeakMemConsumptionKB { get; set; }

	public long? MEnginePeakMemoryKB { get; set; }

	public long? ExternalQueryTimeoutMs { get; set; }

	public long? DirectQueryTimeoutMs { get; set; }

	public long? TabularConnectionTimeoutMs { get; set; }

	public string? CommandType { get; set; }

	public string? DiscoverType { get; set; }

	public string? QueryDialect { get; set; }

	public int? ErrorCount { get; set; }

	public int? RefreshParallelism { get; set; }

	public long? VertipaqTotalRows { get; set; }

	public long? QueryResultRows { get; set; }

	public int? DirectQueryRequestCount { get; set; }

	public long? DirectQueryTotalRows { get; set; }

	public string? QsoReplicaVersion { get; set; }

	public int? IntendedUsage { get; set; }

	public bool? DirectLakeFallbackNotFramed { get; set; }

	public bool? DirectLakeFallbackView { get; set; }

	public bool? DirectLakeFallbackTooManyFiles { get; set; }

	public bool? DirectLakeFallbackTooManyRowgroups { get; set; }

	public bool? DirectLakeFallbackTooManyRows { get; set; }

	public bool? DirectLakeFallbackFramingRls { get; set; }

	public bool? DirectLakeFallbackQueryOls { get; set; }

	public bool? DirectLakeFallbackQueryRls { get; set; }
}
