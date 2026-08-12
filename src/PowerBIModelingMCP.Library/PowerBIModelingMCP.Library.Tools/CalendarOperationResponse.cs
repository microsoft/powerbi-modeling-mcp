using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class CalendarOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static CalendarOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<CalendarOperationResponse>(op, msg);
	}
}
