using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Common.Telemetry;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Tools;

[McpServerToolType]
public class PerspectiveOperationsTool
{
	public const string ToolName = "perspective_operations";

	private readonly ILogger<PerspectiveOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["List"] = new OperationMetadata
			{
				Description = "List all perspectives in the model with summary information. \nMandatory properties: None. \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get detailed information about one or more perspectives including all tables, columns, measures, and hierarchies. \nMandatory properties: References (list of PerspectiveReference objects with Name). \nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"SalesPerspective\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"SalesPerspective\" },\n            { \"Name\": \"FinancePerspective\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true }\n    }\n}" }
			},
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more perspectives in the model. \nMandatory properties: Definitions (list of PerspectiveDefinition objects with Name). \nOptional: Description, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \"Name\": \"SalesPerspective\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \"Name\": \"SalesPerspective\", \"Description\": \"Sales view\" },\n            { \"Name\": \"FinancePerspective\", \"Description\": \"Finance view\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing perspectives. Names cannot be changed - use Rename operation instead. \nMandatory properties: Definitions (list of PerspectiveDefinition objects with Name). \nOptional: Description, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"SalesPerspective\",\n                \"Description\": \"Updated sales perspective for the sales team\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \"Name\": \"SalesPerspective\", \"Description\": \"Updated sales view\" },\n            { \"Name\": \"FinancePerspective\", \"Description\": \"Updated finance view\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more perspectives from the model. \nMandatory properties: References (list of PerspectiveReference objects with Name). \nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"ObsoletePerspective\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"OldPerspective1\" },\n            { \"Name\": \"OldPerspective2\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more perspectives to new names. \nMandatory properties: RenameDefinitions (list of PerspectiveRename objects with CurrentName and NewName). \nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \"CurrentName\": \"Sales\", \"NewName\": \"SalesNew\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \"CurrentName\": \"Perspective1\", \"NewName\": \"RenamedPerspective1\" },\n            { \"CurrentName\": \"Perspective2\", \"NewName\": \"RenamedPerspective2\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["ListTables"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Filter" },
				Description = "List all tables included in a specific perspective with summary information. \nMandatory properties: Filter (with PerspectiveName). \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListTables\",\n        \"Filter\": {\n            \"PerspectiveName\": \"Sales\"\n        }\n    }\n}" }
			},
			["AddTables"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Tables" },
				Description = "Add multiple model tables to a perspective in a batch operation. \nMandatory properties: PerspectiveName, Tables (list of table definitions). \nOptional: Options (with ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"AddTables\",\n        \"PerspectiveName\": \"Sales\",\n        \"Tables\": [\n            { \"TableName\": \"Orders\", \"IncludeAll\": true },\n            { \"TableName\": \"Customers\", \"IncludeAll\": false },\n            { \"TableName\": \"Products\", \"IncludeAll\": true }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["UpdateTables"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Tables" },
				Description = "Update multiple perspective table settings in a batch operation. \nMandatory properties: PerspectiveName, Tables (list of table update definitions). \nOptional: Options (with ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"UpdateTables\",\n        \"PerspectiveName\": \"Sales\",\n        \"Tables\": [\n            { \"TableName\": \"Orders\", \"IncludeAll\": false },\n            { \"TableName\": \"Products\", \"IncludeAll\": true }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["RemoveTables"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Tables" },
				Description = "Remove multiple tables from a perspective in a batch operation. \nMandatory properties: PerspectiveName, Tables (list with TableName). \nOptional: Options (with ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RemoveTables\",\n        \"PerspectiveName\": \"Sales\",\n        \"Tables\": [\n            { \"TableName\": \"OldOrders\" },\n            { \"TableName\": \"TempData\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["GetTables"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Tables" },
				Description = "Get detailed information about multiple tables in a perspective. \nMandatory properties: PerspectiveName, Tables (list with TableName). \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetTables\",\n        \"PerspectiveName\": \"Sales\",\n        \"Tables\": [\n            { \"TableName\": \"Orders\" },\n            { \"TableName\": \"Products\" }\n        ]\n    }\n}" }
			},
			["ListColumns"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Filter" },
				Description = "List all columns included in a specific perspective table. \nMandatory properties: Filter (with PerspectiveName and TableName). \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListColumns\",\n        \"Filter\": {\n            \"PerspectiveName\": \"Sales\",\n            \"TableName\": \"FactSales\"\n        }\n    }\n}" }
			},
			["AddColumns"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Columns" },
				Description = "Add multiple columns to perspective tables in a batch operation. \nMandatory properties: PerspectiveName, Columns (list with TableName and ColumnName). \nOptional: Options (with ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"AddColumns\",\n        \"PerspectiveName\": \"Sales\",\n        \"Columns\": [\n            { \"TableName\": \"Sales\", \"ColumnName\": \"Amount\" },\n            { \"TableName\": \"Sales\", \"ColumnName\": \"Quantity\" },\n            { \"TableName\": \"Products\", \"ColumnName\": \"ProductName\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["RemoveColumns"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Columns" },
				Description = "Remove multiple columns from perspective tables in a batch operation. \nMandatory properties: PerspectiveName, Columns (list with TableName and ColumnName). \nOptional: Options (with ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RemoveColumns\",\n        \"PerspectiveName\": \"Sales\",\n        \"Columns\": [\n            { \"TableName\": \"Sales\", \"ColumnName\": \"OldColumn\" },\n            { \"TableName\": \"Products\", \"ColumnName\": \"TempColumn\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["GetColumns"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Columns" },
				Description = "Get detailed information about multiple columns in perspective tables. \nMandatory properties: PerspectiveName, Columns (list with TableName and ColumnName). \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetColumns\",\n        \"PerspectiveName\": \"Sales\",\n        \"Columns\": [\n            { \"TableName\": \"Sales\", \"ColumnName\": \"Amount\" },\n            { \"TableName\": \"Products\", \"ColumnName\": \"ProductName\" }\n        ]\n    }\n}" }
			},
			["ListMeasures"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Filter" },
				Description = "List all measures included in a specific perspective table. \nMandatory properties: Filter (with PerspectiveName and TableName). \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListMeasures\",\n        \"Filter\": {\n            \"PerspectiveName\": \"Sales\",\n            \"TableName\": \"FactSales\"\n        }\n    }\n}" }
			},
			["AddMeasures"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Measures" },
				Description = "Add multiple measures to perspective tables in a batch operation. \nMandatory properties: PerspectiveName, Measures (list with TableName and MeasureName). \nOptional: Options (with ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"AddMeasures\",\n        \"PerspectiveName\": \"Sales\",\n        \"Measures\": [\n            { \"TableName\": \"Sales\", \"MeasureName\": \"Total Revenue\" },\n            { \"TableName\": \"Sales\", \"MeasureName\": \"Average Sale\" },\n            { \"TableName\": \"Products\", \"MeasureName\": \"Product Count\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["RemoveMeasures"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Measures" },
				Description = "Remove multiple measures from perspective tables in a batch operation. \nMandatory properties: PerspectiveName, Measures (list with TableName and MeasureName). \nOptional: Options (with ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RemoveMeasures\",\n        \"PerspectiveName\": \"Sales\",\n        \"Measures\": [\n            { \"TableName\": \"Sales\", \"MeasureName\": \"Total Revenue\" },\n            { \"TableName\": \"Sales\", \"MeasureName\": \"Average Sale\" },\n            { \"TableName\": \"Products\", \"MeasureName\": \"Product Count\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["GetMeasures"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Measures" },
				Description = "Get detailed information about multiple measures in perspective tables. \nMandatory properties: PerspectiveName, Measures (list with TableName and MeasureName). \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetMeasures\",\n        \"PerspectiveName\": \"Sales\",\n        \"Measures\": [\n            { \"TableName\": \"Sales\", \"MeasureName\": \"Total Revenue\" },\n            { \"TableName\": \"Products\", \"MeasureName\": \"Product Count\" }\n        ]\n    }\n}" }
			},
			["ListHierarchies"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Filter" },
				Description = "List all hierarchies included in a specific perspective table. \nMandatory properties: Filter (with PerspectiveName and TableName). \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListHierarchies\",\n        \"Filter\": {\n            \"PerspectiveName\": \"Sales\",\n            \"TableName\": \"FactSales\"\n        }\n    }\n}" }
			},
			["AddHierarchies"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Hierarchies" },
				Description = "Add multiple hierarchies to perspective tables in a batch operation. \nMandatory properties: PerspectiveName, Hierarchies (list with TableName and HierarchyName). \nOptional: Options (with ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"AddHierarchies\",\n        \"PerspectiveName\": \"Sales\",\n        \"Hierarchies\": [\n            { \"TableName\": \"Geography\", \"HierarchyName\": \"Location\" },\n            { \"TableName\": \"Date\", \"HierarchyName\": \"Calendar\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["RemoveHierarchies"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Hierarchies" },
				Description = "Remove multiple hierarchies from perspective tables in a batch operation. \nMandatory properties: PerspectiveName, Hierarchies (list with TableName and HierarchyName). \nOptional: Options (with ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RemoveHierarchies\",\n        \"PerspectiveName\": \"Sales\",\n        \"Hierarchies\": [\n            { \"TableName\": \"Geography\", \"HierarchyName\": \"OldLocation\" },\n            { \"TableName\": \"Date\", \"HierarchyName\": \"FiscalCalendar\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["GetHierarchies"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "PerspectiveName", "Hierarchies" },
				Description = "Get detailed information about multiple hierarchies in perspective tables. \nMandatory properties: PerspectiveName, Hierarchies (list with TableName and HierarchyName). \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetHierarchies\",\n        \"PerspectiveName\": \"Sales\",\n        \"Hierarchies\": [\n            { \"TableName\": \"Geography\", \"HierarchyName\": \"Location\" },\n            { \"TableName\": \"Date\", \"HierarchyName\": \"Calendar\" }\n        ]\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export perspective definition to TMDL (Tabular Model Definition Language) format. \nMandatory properties: References (list with at least one PerspectiveReference containing Name). \nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).\nNote: Only the first reference is used for export.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"Name\": \"Sales\" }\n        ]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the perspective_operations tool and its available operations. \nMandatory properties: None. \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"help\"\n    }\n}" }
			}
		}
	};

	public PerspectiveOperationsTool(ILogger<PerspectiveOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "perspective_operations", Title = "Perspective Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("perspective_operations")]
	public async Task<CallToolResult> ExecutePerspectiveOperation(McpServer mcpServer, PerspectiveOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Perspective={PerspectiveName}, Table={TableName}, Connection={ConnectionName}", "PerspectiveOperationsTool", request.Operation, request.PerspectiveName ?? request.Filter?.PerspectiveName, request.Filter?.TableName, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[25]
		{
			"LIST", "GET", "CREATE", "UPDATE", "DELETE", "RENAME", "LISTTABLES", "GETTABLES", "ADDTABLES", "UPDATETABLES",
			"REMOVETABLES", "LISTCOLUMNS", "GETCOLUMNS", "ADDCOLUMNS", "REMOVECOLUMNS", "LISTMEASURES", "GETMEASURES", "ADDMEASURES", "REMOVEMEASURES", "LISTHIERARCHIES",
			"GETHIERARCHIES", "ADDHIERARCHIES", "REMOVEHIERARCHIES", "EXPORTTMDL", "HELP"
		};
		string[] writeOperations = new string[13]
		{
			"CREATE", "UPDATE", "DELETE", "RENAME", "ADDTABLES", "UPDATETABLES", "REMOVETABLES", "ADDCOLUMNS", "REMOVECOLUMNS", "ADDMEASURES",
			"REMOVEMEASURES", "ADDHIERARCHIES", "REMOVEHIERARCHIES"
		};
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("perspective_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "PerspectiveOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(PerspectiveOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
				return result2;
			}
			var (flag, text) = ValidateRequest(request.Operation, request);
			if (!flag)
			{
				_logger.LogWarning("Invalid request for {Operation} operation: {ValidationError}", request.Operation, text);
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.Error(request.Operation, text, annotations, ErrorSource.User));
				return result2;
			}
			if (Enumerable.Contains(writeOperations, request.Operation.ToUpperInvariant()))
			{
				WriteOperationResult writeOperationResult = await writeGuard.ExecuteWriteOperationWithGuards(mcpServer, request.ConnectionName, request.Operation);
				if (!writeOperationResult.Success)
				{
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "PerspectiveOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(PerspectiveOperationResponse.Forbidden(request.Operation, writeOperationResult.Message), annotations));
					return result2;
				}
			}
			bool isWriteEnabled = writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = request.Operation.ToUpperInvariant() switch
			{
				"LIST" => CallToolResultHelper.FromResponse(await HandleListPerspectivesOperation(request), annotations), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetPerspectiveOperation(request), annotations), 
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreatePerspectiveOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdatePerspectiveOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeletePerspectiveOperation(request), annotations, null, minimalSuccessPayload: true), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenamePerspectiveOperation(request), annotations, null, minimalSuccessPayload: true), 
				"LISTTABLES" => CallToolResultHelper.FromResponse(await HandleListPerspectiveTablesOperation(request), annotations), 
				"GETTABLES" => CallToolResultHelper.FromResponse(await HandleGetPerspectiveTablesOperation(request), annotations), 
				"ADDTABLES" => CallToolResultHelper.FromResponse(await HandleAddPerspectiveTablesOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATETABLES" => CallToolResultHelper.FromResponse(await HandleUpdatePerspectiveTablesOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REMOVETABLES" => CallToolResultHelper.FromResponse(await HandleRemovePerspectiveTablesOperation(request), annotations, null, minimalSuccessPayload: true), 
				"LISTCOLUMNS" => CallToolResultHelper.FromResponse(await HandleListPerspectiveColumnsOperation(request), annotations), 
				"GETCOLUMNS" => CallToolResultHelper.FromResponse(await HandleGetPerspectiveColumnsOperation(request), annotations), 
				"ADDCOLUMNS" => CallToolResultHelper.FromResponse(await HandleAddPerspectiveColumnsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REMOVECOLUMNS" => CallToolResultHelper.FromResponse(await HandleRemovePerspectiveColumnsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"LISTMEASURES" => CallToolResultHelper.FromResponse(await HandleListPerspectiveMeasuresOperation(request), annotations), 
				"GETMEASURES" => CallToolResultHelper.FromResponse(await HandleGetPerspectiveMeasuresOperation(request), annotations), 
				"ADDMEASURES" => CallToolResultHelper.FromResponse(await HandleAddPerspectiveMeasuresOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REMOVEMEASURES" => CallToolResultHelper.FromResponse(await HandleRemovePerspectiveMeasuresOperation(request), annotations, null, minimalSuccessPayload: true), 
				"LISTHIERARCHIES" => CallToolResultHelper.FromResponse(await HandleListPerspectiveHierarchiesOperation(request), annotations), 
				"GETHIERARCHIES" => CallToolResultHelper.FromResponse(await HandleGetPerspectiveHierarchiesOperation(request), annotations), 
				"ADDHIERARCHIES" => CallToolResultHelper.FromResponse(await HandleAddPerspectiveHierarchiesOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REMOVEHIERARCHIES" => CallToolResultHelper.FromResponse(await HandleRemovePerspectiveHierarchiesOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "perspective") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(await HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(PerspectiveOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("PerspectiveOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"LIST" => "Error occurred while validating request: " + ex.GetErrorMessage(), 
				"GET" => "Error executing Get operation: " + ex.GetErrorMessage(), 
				"CREATE" => "Error executing Create operation: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error executing Update operation: " + ex.GetErrorMessage(), 
				"DELETE" => "Error executing Delete operation: " + ex.GetErrorMessage(), 
				"RENAME" => "Error executing Rename operation: " + ex.GetErrorMessage(), 
				"LISTTABLES" => "Error occurred while validating request: " + ex.GetErrorMessage(), 
				"GETTABLES" => "Error retrieving tables: " + ex.GetErrorMessage(), 
				"ADDTABLES" => "Error adding tables: " + ex.GetErrorMessage(), 
				"UPDATETABLES" => "Error updating tables: " + ex.GetErrorMessage(), 
				"REMOVETABLES" => "Error removing tables: " + ex.GetErrorMessage(), 
				"LISTCOLUMNS" => "Error occurred while retrieving perspective columns: " + ex.GetErrorMessage(), 
				"GETCOLUMNS" => "Error retrieving columns: " + ex.GetErrorMessage(), 
				"ADDCOLUMNS" => "Error adding columns: " + ex.GetErrorMessage(), 
				"REMOVECOLUMNS" => "Error removing columns: " + ex.GetErrorMessage(), 
				"LISTMEASURES" => "Error occurred while retrieving perspective measure: " + ex.GetErrorMessage(), 
				"GETMEASURES" => "Error retrieving measures: " + ex.GetErrorMessage(), 
				"ADDMEASURES" => "Error adding measures: " + ex.GetErrorMessage(), 
				"REMOVEMEASURES" => "Error removing measures: " + ex.GetErrorMessage(), 
				"LISTHIERARCHIES" => "Error occurred while retrieving perspective hierarchy: " + ex.GetErrorMessage(), 
				"GETHIERARCHIES" => "Error retrieving hierarchies: " + ex.GetErrorMessage(), 
				"ADDHIERARCHIES" => "Error adding hierarchies: " + ex.GetErrorMessage(), 
				"REMOVEHIERARCHIES" => "Error removing hierarchies: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Failed to export TMDL for perspective: " + ex.GetErrorMessage(), 
				_ => "Error executing perspective operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new PerspectiveOperationResponse
			{
				Success = false,
				Message = message,
				Operation = request.Operation,
				PerspectiveName = request.PerspectiveName
			}, annotations, ex));
			return result2;
		}
		finally
		{
			_logger.LogToolCallCompleted(annotations.Title, !annotations.ReadOnlyHint, result?.IsError ?? true, ConnectionOperations.ResolveSemanticModelId());
		}
	}

	private async Task<PerspectiveOperationResponse> HandleListPerspectivesOperation(PerspectiveOperationRequest request)
	{
		List<PerspectiveList> list = await PerspectiveOperations.ListPerspectives(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "PerspectiveOperationsTool", "List", request.ConnectionName, list.Count);
		return new PerspectiveOperationResponse
		{
			Success = true,
			Message = $"Retrieved {list.Count} perspective(s)",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<PerspectiveOperationResponse> HandleGetPerspectiveOperation(PerspectiveOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new PerspectiveOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one perspective reference"
			};
		}
		return MapBatchResponse(await PerspectiveOperations.GetPerspectives(request.ConnectionName, request.References, request.Options));
	}

	private async Task<PerspectiveOperationResponse> HandleCreatePerspectiveOperation(PerspectiveOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new PerspectiveOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one perspective definition"
			};
		}
		return MapBatchResponse(await PerspectiveOperations.CreatePerspectives(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<PerspectiveOperationResponse> HandleUpdatePerspectiveOperation(PerspectiveOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new PerspectiveOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one perspective definition"
			};
		}
		return MapBatchResponse(await PerspectiveOperations.UpdatePerspectives(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<PerspectiveOperationResponse> HandleDeletePerspectiveOperation(PerspectiveOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new PerspectiveOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one perspective reference"
			};
		}
		return MapBatchResponse(await PerspectiveOperations.DeletePerspectives(request.ConnectionName, request.References, request.Options));
	}

	private async Task<PerspectiveOperationResponse> HandleRenamePerspectiveOperation(PerspectiveOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			return new PerspectiveOperationResponse
			{
				Success = false,
				Operation = "Rename",
				Message = "RenameDefinitions is required and must contain at least one perspective rename definition"
			};
		}
		return MapBatchResponse(await PerspectiveOperations.RenamePerspectives(request.ConnectionName, request.RenameDefinitions, request.Options));
	}

	private async Task<PerspectiveOperationResponse> HandleListPerspectiveTablesOperation(PerspectiveOperationRequest request)
	{
		string perspectiveName = request.Filter?.PerspectiveName;
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Filter.PerspectiveName is required for ListTables operation", ErrorSource.User);
		}
		List<Dictionary<string, string>> list = await PerspectiveOperations.ListPerspectiveTables(request.ConnectionName, perspectiveName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "PerspectiveOperationsTool", "ListTables", request.ConnectionName, list.Count);
		return new PerspectiveOperationResponse
		{
			Success = true,
			Message = $"Retrieved {list.Count} perspective table(s) for perspective '{perspectiveName}'",
			Operation = request.Operation,
			PerspectiveName = perspectiveName,
			Data = list
		};
	}

	private async Task<PerspectiveOperationResponse> HandleListPerspectiveColumnsOperation(PerspectiveOperationRequest request)
	{
		string perspectiveName = request.Filter?.PerspectiveName;
		string tableName = request.Filter?.TableName;
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Filter.PerspectiveName is required for ListColumns operation", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Filter.TableName is required for ListColumns operation", ErrorSource.User);
		}
		List<Dictionary<string, string>> list = await PerspectiveOperations.ListPerspectiveColumns(request.ConnectionName, perspectiveName, tableName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "PerspectiveOperationsTool", "ListColumns", request.ConnectionName, list.Count);
		return new PerspectiveOperationResponse
		{
			Success = true,
			Message = $"Retrieved {list.Count} perspective column(s) for table '{tableName}' in perspective '{perspectiveName}'",
			Operation = request.Operation,
			PerspectiveName = perspectiveName,
			Data = list
		};
	}

	private async Task<PerspectiveOperationResponse> HandleListPerspectiveMeasuresOperation(PerspectiveOperationRequest request)
	{
		string perspectiveName = request.Filter?.PerspectiveName;
		string tableName = request.Filter?.TableName;
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Filter.PerspectiveName is required for ListMeasures operation", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Filter.TableName is required for ListMeasures operation", ErrorSource.User);
		}
		List<Dictionary<string, string>> list = await PerspectiveOperations.ListPerspectiveMeasures(request.ConnectionName, perspectiveName, tableName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "PerspectiveOperationsTool", "ListMeasures", request.ConnectionName, list.Count);
		return new PerspectiveOperationResponse
		{
			Success = true,
			Message = $"Retrieved {list.Count} perspective measure(s) for table '{tableName}' in perspective '{perspectiveName}'",
			Operation = request.Operation,
			PerspectiveName = perspectiveName,
			Data = list
		};
	}

	private async Task<PerspectiveOperationResponse> HandleListPerspectiveHierarchiesOperation(PerspectiveOperationRequest request)
	{
		string perspectiveName = request.Filter?.PerspectiveName;
		string tableName = request.Filter?.TableName;
		if (string.IsNullOrWhiteSpace(perspectiveName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Filter.PerspectiveName is required for ListHierarchies operation", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Filter.TableName is required for ListHierarchies operation", ErrorSource.User);
		}
		List<Dictionary<string, string>> list = await PerspectiveOperations.ListPerspectiveHierarchies(request.ConnectionName, perspectiveName, tableName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "PerspectiveOperationsTool", "ListHierarchies", request.ConnectionName, list.Count);
		return new PerspectiveOperationResponse
		{
			Success = true,
			Message = $"Retrieved {list.Count} perspective hierarchy(ies) for table '{tableName}' in perspective '{perspectiveName}'",
			Operation = request.Operation,
			PerspectiveName = perspectiveName,
			Data = list
		};
	}

	private async Task<PerspectiveOperationResponse> HandleExportTMDLOperation(PerspectiveOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Perspective");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new PerspectiveOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		string perspectiveName = request.References.First().Name;
		string data = await PerspectiveOperations.ExportTMDL(request.ConnectionName, perspectiveName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "PerspectiveOperationsTool", "ExportTMDL", request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Perspective", perspectiveName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new PerspectiveOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			PerspectiveName = perspectiveName,
			Data = data,
			Warnings = warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleAddPerspectiveTablesOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.AddPerspectiveTables(request.ConnectionName, request.PerspectiveName, request.Tables, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "AddTables", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleUpdatePerspectiveTablesOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.UpdatePerspectiveTables(request.ConnectionName, request.PerspectiveName, request.Tables, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "UpdateTables", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleRemovePerspectiveTablesOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.RemovePerspectiveTables(request.ConnectionName, request.PerspectiveName, request.Tables, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "RemoveTables", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleGetPerspectiveTablesOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.GetPerspectiveTables(request.ConnectionName, request.PerspectiveName, request.Tables);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "GetTables", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results
		};
	}

	private async Task<PerspectiveOperationResponse> HandleAddPerspectiveColumnsOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.AddPerspectiveColumns(request.ConnectionName, request.PerspectiveName, request.Columns, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "AddColumns", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleRemovePerspectiveColumnsOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.RemovePerspectiveColumns(request.ConnectionName, request.PerspectiveName, request.Columns, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "RemoveColumns", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleGetPerspectiveColumnsOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.GetPerspectiveColumns(request.ConnectionName, request.PerspectiveName, request.Columns);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "GetColumns", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results
		};
	}

	private async Task<PerspectiveOperationResponse> HandleAddPerspectiveMeasuresOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.AddPerspectiveMeasures(request.ConnectionName, request.PerspectiveName, request.Measures, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "AddMeasures", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleRemovePerspectiveMeasuresOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.RemovePerspectiveMeasures(request.ConnectionName, request.PerspectiveName, request.Measures, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "RemoveMeasures", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleGetPerspectiveMeasuresOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.GetPerspectiveMeasures(request.ConnectionName, request.PerspectiveName, request.Measures);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "GetMeasures", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results
		};
	}

	private async Task<PerspectiveOperationResponse> HandleAddPerspectiveHierarchiesOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.AddPerspectiveHierarchies(request.ConnectionName, request.PerspectiveName, request.Hierarchies, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "AddHierarchies", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleRemovePerspectiveHierarchiesOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.RemovePerspectiveHierarchies(request.ConnectionName, request.PerspectiveName, request.Hierarchies, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "RemoveHierarchies", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<PerspectiveOperationResponse> HandleGetPerspectiveHierarchiesOperation(PerspectiveOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await PerspectiveOperations.GetPerspectiveHierarchies(request.ConnectionName, request.PerspectiveName, request.Hierarchies);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {Summary}", "PerspectiveOperationsTool", "GetHierarchies", request.ConnectionName, batchOperationResponse.Message);
		return new PerspectiveOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			PerspectiveName = request.PerspectiveName,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results
		};
	}

	private Task<PerspectiveOperationResponse> HandleHelpOperation(PerspectiveOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "PerspectiveOperationsTool", "Help", request.ConnectionName, operations.Length);
		PerspectiveOperationResponse perspectiveOperationResponse = new PerspectiveOperationResponse();
		perspectiveOperationResponse.Success = true;
		perspectiveOperationResponse.Message = "Help information for perspective operations";
		perspectiveOperationResponse.Operation = request.Operation;
		perspectiveOperationResponse.Help = new
		{
			ToolName = "perspective_operations",
			Description = "Perform operations on semantic model perspectives, perspective tables, perspective columns, perspective measures, and perspective hierarchies.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[10] { "For CREATE, UPDATE, DELETE, and RENAME operations, the 'perspective_name' parameter is required.", "For operations that require a table name, the 'table_name' parameter is required.", "For operations that require a column name, the 'column_name' parameter is required.", "For operations that require a measure name, the 'measure_name' parameter is required.", "For operations that require a hierarchy name, the 'hierarchy_name' parameter is required.", "For operations that require a new perspective name, the 'new_perspective_name' parameter is required.", "For operations that require a new table name, the 'new_table_name' parameter is required.", "For operations that require a new column name, the 'new_column_name' parameter is required.", "For operations that require a new measure name, the 'new_measure_name' parameter is required.", "For operations that require a new hierarchy name, the 'new_hierarchy_name' parameter is required." }
		};
		return Task.FromResult(perspectiveOperationResponse);
	}

	private PerspectiveOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		PerspectiveOperationResponse perspectiveOperationResponse = new PerspectiveOperationResponse
		{
			Success = batchResponse.Success,
			Message = batchResponse.Message,
			Operation = batchResponse.Operation,
			Summary = batchResponse.Summary,
			Results = batchResponse.Results,
			Warnings = batchResponse.Warnings
		};
		if (batchResponse.Exceptions.Count > 0)
		{
			perspectiveOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return perspectiveOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, PerspectiveOperationRequest request)
	{
		if (!toolMetadata.Operations.TryGetValue(operation, out OperationMetadata value))
		{
			return (isValid: true, errorMessage: null);
		}
		JsonObject requestDict = JsonSerializer.SerializeToNode(request) as JsonObject;
		List<string> list = value.RequiredParams.Where((string p) => requestDict != null && requestDict[p] == null).ToList();
		List<string> list2 = value.ForbiddenParams.Where((string p) => requestDict != null && requestDict[p] != null).ToList();
		if (list.Any())
		{
			string text = "Missing required parameters for " + operation + " operation: " + string.Join(", ", list);
			_logger.LogWarning(text);
			return (isValid: false, errorMessage: text);
		}
		if (list2.Any())
		{
			string text2 = "Forbidden parameters for " + operation + " operation: " + string.Join(", ", list2);
			_logger.LogWarning(text2);
			return (isValid: false, errorMessage: text2);
		}
		return (isValid: true, errorMessage: null);
	}
}
