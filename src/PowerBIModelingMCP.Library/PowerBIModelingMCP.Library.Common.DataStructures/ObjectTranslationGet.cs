using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ObjectTranslationGet : ObjectTranslationBase
{
	public string? Value { get; set; }

	public DateTime? ModifiedTime { get; set; }
}
