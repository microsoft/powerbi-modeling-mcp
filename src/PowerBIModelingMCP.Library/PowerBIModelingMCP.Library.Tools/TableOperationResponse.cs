using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class TableOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static TableOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<TableOperationResponse>(op, msg);
	}
}
