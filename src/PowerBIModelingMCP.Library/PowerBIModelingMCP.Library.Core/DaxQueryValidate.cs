namespace PowerBIModelingMCP.Library.Core;

public class DaxQueryValidate
{
	public required string Query { get; set; }

	public int? TimeoutSeconds { get; set; }
}
