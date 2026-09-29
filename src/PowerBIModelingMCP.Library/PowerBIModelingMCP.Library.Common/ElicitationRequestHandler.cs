using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace PowerBIModelingMCP.Library.Common;

public static class ElicitationRequestHandler
{
	private const string ConfirmPropertyName = "Confirm the operation";

	private const string SuccessResponseAction = "accept";

	private const string RejectResponseAction = "decline";

	private const string CancelResponseAction = "cancel";

	public static async Task<bool> HandleConfirmRequest(McpServer server, string databaseName, string message, ConfirmationType confirmationType)
	{
		if (server == null)
		{
			throw new ArgumentNullException("server");
		}
		if (server.ClientCapabilities == null || server.ClientCapabilities.Elicitation == null)
		{
			return true;
		}
		try
		{
			ElicitResult elicitResult = await server.ElicitAsync(new ElicitRequestParams
			{
				Message = message,
				RequestedSchema = new ElicitRequestParams.RequestSchema
				{
					Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition> { ["Confirm the operation"] = new ElicitRequestParams.LegacyTitledEnumSchema
					{
						Type = "string",
						Enum = new string[2] { "Yes", "No" },
						EnumNames = new string[2] { "Continue the operation", "Decline the operation" },
						Description = "Confirm the operation"
					} },
					Required = new string[1] { "Confirm the operation" }
				}
			});
			if (elicitResult.Action == "accept" && elicitResult.Content.TryGetValue("Confirm the operation", out var value))
			{
				string text = value.GetString();
				if (!string.IsNullOrEmpty(text) && (text.Equals("Yes", StringComparison.OrdinalIgnoreCase) || text.Equals("Y", StringComparison.OrdinalIgnoreCase)))
				{
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			throw new McpExceptionWithSource("Failed to handle confirm request. " + ex.Message, ex, null, "Failed to handle confirm request.");
		}
		return false;
	}
}
