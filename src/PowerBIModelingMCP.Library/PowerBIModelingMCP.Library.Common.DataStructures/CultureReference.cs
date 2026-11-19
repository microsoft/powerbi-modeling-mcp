using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CultureReference
{
	[Required]
	[Description("e.g., 'en-US', 'fr-FR'")]
	public required string Name { get; set; }
}
