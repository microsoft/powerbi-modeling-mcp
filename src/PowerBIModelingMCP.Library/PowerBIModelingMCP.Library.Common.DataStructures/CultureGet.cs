using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CultureGet : CultureBase
{
	public string? State { get; set; }

	public string? ErrorMessage { get; set; }

	public DateTime? ModifiedTime { get; set; }

	public DateTime? StructureModifiedTime { get; set; }

	public bool? IsRemoved { get; set; }

	public string? LinguisticMetadataReference { get; set; }

	public List<string> ObjectTranslationReferences { get; set; } = new List<string>();

	public CultureGet()
	{
	}

	public CultureGet(string name)
	{
		base.Name = name;
	}
}
