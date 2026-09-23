using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class CultureOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static CultureOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<CultureOperationResponse>(op, msg);
	}
}
