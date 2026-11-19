using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class DataSourceOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static DataSourceOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<DataSourceOperationResponse>(op, msg);
	}
}
