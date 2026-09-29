using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PartitionBase
{
	public string? Name { get; set; }

	public string? TableName { get; set; }

	public string? Description { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }

	public string? QueryGroupName { get; set; }

	public string? SourceType { get; set; }

	public string? Mode { get; set; }

	public string? Expression { get; set; }

	public string? DataSourceName { get; set; }

	public string? Query { get; set; }

	public string? StartDateTime { get; set; }

	public string? EndDateTime { get; set; }

	public string? Granularity { get; set; }

	public string? RefreshBookmark { get; set; }

	public bool? RetainDataTillForceCalculate { get; set; }

	public string? Attributes { get; set; }

	public string? EntityName { get; set; }

	public string? SchemaName { get; set; }

	public string? ExpressionSourceName { get; set; }
}
