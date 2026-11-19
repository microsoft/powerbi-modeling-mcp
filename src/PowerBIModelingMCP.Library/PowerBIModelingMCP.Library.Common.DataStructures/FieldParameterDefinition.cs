using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class FieldParameterDefinition
{
	[Required]
	public required string Name { get; set; }

	[Required]
	public required List<FieldParameterFieldDefinition> Fields { get; set; }
}
