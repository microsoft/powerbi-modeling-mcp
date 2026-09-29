using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ObjectTranslationList
{
	public required string CultureName { get; set; }

	public required string ObjectType { get; set; }

	public required string Property { get; set; }

	public string? Value { get; set; }

	public Dictionary<string, string> ObjectIdentifiers { get; set; } = new Dictionary<string, string>();
}
