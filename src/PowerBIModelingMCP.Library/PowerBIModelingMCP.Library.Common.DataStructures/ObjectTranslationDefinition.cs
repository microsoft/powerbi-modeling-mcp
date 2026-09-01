namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ObjectTranslationDefinition : ObjectTranslationBase
{
	public string? Value { get; set; }

	public bool CreateCultureIfNotExists { get; set; } = true;
}
