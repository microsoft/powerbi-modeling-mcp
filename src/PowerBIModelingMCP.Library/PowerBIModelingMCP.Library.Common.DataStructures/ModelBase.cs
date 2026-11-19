using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ModelBase
{
	public string? Name { get; set; }

	public string? Description { get; set; }

	public string? StorageLocation { get; set; }

	public string? DefaultMode { get; set; }

	public string? DefaultDataView { get; set; }

	public string? Culture { get; set; }

	public string? Collation { get; set; }

	public string? DataAccessOptions { get; set; }

	public string? DefaultMeasureTable { get; set; }

	public string? DefaultMeasureName { get; set; }

	public string? DefaultPowerBIDataSourceVersion { get; set; }

	public bool? ForceUniqueNames { get; set; }

	public bool? DiscourageImplicitMeasures { get; set; }

	public bool? DiscourageReportMeasures { get; set; }

	public string? DataSourceVariablesOverrideBehavior { get; set; }

	public int? DataSourceDefaultMaxConnections { get; set; }

	public string? SourceQueryCulture { get; set; }

	public string? MAttributes { get; set; }

	public bool? DiscourageCompositeModels { get; set; }

	public string? AutomaticAggregationOptions { get; set; }

	public string? DirectLakeBehavior { get; set; }

	public string? ValueFilterBehavior { get; set; }

	public string? SelectionExpressionBehavior { get; set; }

	public string? MetadataAccessPolicy { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }

	public List<BindingInfo>? BindingInfos { get; set; }
}
