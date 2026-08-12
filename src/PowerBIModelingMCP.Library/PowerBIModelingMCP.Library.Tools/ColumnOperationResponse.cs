using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class ColumnOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static ColumnOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<ColumnOperationResponse>(op, msg);
	}
}
