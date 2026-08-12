using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CultureBase
{
	[Required]
	[Description("e.g., 'en-US', 'fr-FR'")]
	public required string Name { get; set; }

	[Description("Null=skip, empty=clear, values=set")]
	public List<KeyValuePair<string, string>>? Annotations { get; set; }

	public List<ExtendedProperty>? ExtendedProperties { get; set; }
}
