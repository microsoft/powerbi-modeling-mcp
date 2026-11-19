using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class KPIDefinition
{
	public string? StatusExpression { get; set; }

	public string? StatusGraphic { get; set; }

	public string? TrendExpression { get; set; }

	public string? TrendGraphic { get; set; }

	public string? TargetExpression { get; set; }

	public string? TargetFormatString { get; set; }

	public string? TargetDescription { get; set; }

	public string? StatusDescription { get; set; }

	public string? TrendDescription { get; set; }

	public List<KeyValuePair<string, string>>? Annotations { get; set; }
}
