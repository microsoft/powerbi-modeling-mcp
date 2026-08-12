using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class DatabaseOperationResponse : OperationResponseBase, IExportDataResponse
{
	public string? DatabaseName { get; set; }

	public static DatabaseOperationResponse Forbidden(string op, string msg, string? dbName = null)
	{
		DatabaseOperationResponse databaseOperationResponse = OperationResponseBase.CreateForbidden<DatabaseOperationResponse>(op, msg);
		databaseOperationResponse.DatabaseName = dbName;
		return databaseOperationResponse;
	}
}
