using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class PerspectiveOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public string? PerspectiveName { get; set; }

	public static PerspectiveOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<PerspectiveOperationResponse>(op, msg);
	}
}
