namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class CultureDetails
{
	public required string Name { get; set; }

	public required int LCID { get; set; }

	public string? DisplayName { get; set; }

	public string? EnglishName { get; set; }

	public bool IsNeutralCulture { get; set; }

	public bool IsUserCustomCulture { get; set; }

	public CultureDetails()
	{
	}

	public CultureDetails(string name, int lcid)
	{
		Name = name;
		LCID = lcid;
	}
}
