using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveMeasureIdentifier
{
	[Required]
	public required string TableName { get; set; }

	[Required]
	public required string MeasureName { get; set; }
}
