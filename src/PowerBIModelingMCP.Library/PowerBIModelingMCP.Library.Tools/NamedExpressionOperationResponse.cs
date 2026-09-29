using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class NamedExpressionOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static NamedExpressionOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<NamedExpressionOperationResponse>(op, msg);
	}
}
