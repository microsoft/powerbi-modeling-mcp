namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ObjectTranslationListFilter : ListFilterBase
{
	public string? CultureName { get; set; }

	public string? ObjectType { get; set; }

	public string? ObjectName { get; set; }
}
