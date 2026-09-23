using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class QueryGroupOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static QueryGroupOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<QueryGroupOperationResponse>(op, msg);
	}
}
