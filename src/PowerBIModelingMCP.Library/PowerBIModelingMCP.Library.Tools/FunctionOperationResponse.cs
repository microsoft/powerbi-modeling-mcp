using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class FunctionOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static FunctionOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<FunctionOperationResponse>(op, msg);
	}
}
