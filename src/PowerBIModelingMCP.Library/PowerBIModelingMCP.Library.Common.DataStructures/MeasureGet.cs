namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class MeasureGet : MeasureBase
{
	public string? FormatStringExpressionState { get; set; }

	public string? FormatStringExpressionErrorMessage { get; set; }

	public string? DetailRowsExpressionState { get; set; }

	public string? DetailRowsExpressionErrorMessage { get; set; }
}
