using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Tools;

public class DaxQueryOperationRequest
{
	[Required]
	[YamlFieldDescription("dax_query_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("dax_query_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[YamlFieldDescription("dax_query_operations", "Query")]
	public string? Query { get; set; }

	[YamlFieldDescription("dax_query_operations", "TimeoutSeconds")]
	public int? TimeoutSeconds { get; set; }

	[YamlFieldDescription("dax_query_operations", "MaxRows")]
	public int? MaxRows { get; set; }

	[YamlFieldDescription("dax_query_operations", "GetExecutionMetrics")]
	public bool GetExecutionMetrics { get; set; }

	[YamlFieldDescription("dax_query_operations", "ExecutionMetricsOnly")]
	public bool ExecutionMetricsOnly { get; set; }

	[YamlFieldDescription("dax_query_operations", "Impersonation")]
	public DaxQueryImpersonationOptions? Impersonation { get; set; }

	[YamlFieldDescription("dax_query_operations", "ResultMode")]
	public DaxResultMode ResultMode { get; set; }
}
