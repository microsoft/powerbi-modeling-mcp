using System;
using System.Collections.Generic;
using System.Linq;
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
public class CalendarOperationsTool
{
	public const string ToolName = "calendar_operations";

	private readonly ILogger<CalendarOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more calendars with optional initial column groups.\nMandatory properties: Definitions (list of CalendarDefinition objects with Name, TableName).\nOptional: Description, LineageTag, SourceLineageTag, InitialColumnGroups, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"FiscalCalendar\",\n                \"TableName\": \"DimDate\",\n                \"Description\": \"Fiscal calendar for financial reporting\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"FiscalCalendar\",\n                \"TableName\": \"DimDate\",\n                \"Description\": \"Fiscal calendar for financial reporting\"\n            },\n            {\n                \"Name\": \"StandardCalendar\",\n                \"TableName\": \"DateTable\",\n                \"Description\": \"Standard Gregorian calendar\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update properties of one or more existing calendars. Calendar names cannot be changed using this operation (use Rename instead).\nMandatory properties: Definitions (list of CalendarDefinition objects with Name, TableName).\nOptional: Description, LineageTag, SourceLineageTag, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"FiscalCalendar\",\n                \"TableName\": \"DimDate\",\n                \"Description\": \"Updated fiscal calendar for financial reporting\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"FiscalCalendar\",\n                \"TableName\": \"DimDate\",\n                \"Description\": \"Updated fiscal calendar\"\n            },\n            {\n                \"Name\": \"StandardCalendar\",\n                \"TableName\": \"DateTable\",\n                \"Description\": \"Updated standard calendar\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more calendars and all their column groups.\nMandatory properties: References (list of CalendarReference objects with Name, TableName).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"Name\": \"OldFiscalCalendar\",\n                \"TableName\": \"DimDate\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"Name\": \"OldFiscalCalendar\",\n                \"TableName\": \"DimDate\"\n            },\n            {\n                \"Name\": \"OldStandardCalendar\",\n                \"TableName\": \"DateTable\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Retrieve detailed information about one or more calendars including all their column groups.\nMandatory properties: References (list of CalendarReference objects with Name, TableName).\nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"Name\": \"FiscalCalendar\",\n                \"TableName\": \"DimDate\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"Name\": \"FiscalCalendar\",\n                \"TableName\": \"DimDate\"\n            },\n            {\n                \"Name\": \"StandardCalendar\",\n                \"TableName\": \"DateTable\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true\n        }\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				RequiredParams = Array.Empty<string>(),
				Description = "List all calendars in the specified table, or all calendars across all tables if no filter is provided.\nMandatory properties: None.\nOptional: Filter (with TableName to filter by table), MaxResults.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"Filter\": {\n            \"TableName\": \"DimDate\"\n        }\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more calendars.\nMandatory properties: RenameDefinitions (list of CalendarRename objects with CurrentName, NewName).\nOptional: TableName (as search hint), Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            {\n                \"CurrentName\": \"OldCalendarName\",\n                \"NewName\": \"NewCalendarName\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            {\n                \"CurrentName\": \"OldCalendar1\",\n                \"NewName\": \"NewCalendar1\",\n                \"TableName\": \"DimDate\"\n            },\n            {\n                \"CurrentName\": \"OldCalendar2\",\n                \"NewName\": \"NewCalendar2\",\n                \"TableName\": \"DateTable\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export a calendar to TMDL format.\nMandatory properties: References (list with at least one CalendarReference containing Name and TableName).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).\nNote: Only the first reference is used; additional references are ignored with a warning.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"Name\": \"FiscalCalendar\", \"TableName\": \"DimDate\" }\n        ]\n    }\n}" }
			},
			["CreateColumnGroups"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ColumnGroupDefinitions" },
				Description = "Create one or more column groups within calendars.\nMandatory properties: ColumnGroupDefinitions (list of CalendarColumnGroupDefinition objects with CalendarName, GroupType).\nEach definition requires GroupType and either TimeRelatedGroup with Columns OR TimeUnitAssociation with TimeUnit.\nOptional: TableName (as search hint), Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CreateColumnGroups\",\n        \"ColumnGroupDefinitions\": [\n            {\n                \"CalendarName\": \"FiscalCalendar\",\n                \"TableName\": \"DimDate\",\n                \"GroupType\": \"TimeUnitAssociation\",\n                \"TimeUnit\": \"Months\",\n                \"TimeUnitAssociation\": {\n                    \"TimeUnit\": \"Months\",\n                    \"PrimaryColumnName\": \"FiscalMonth\",\n                    \"AssociatedColumns\": [\"FiscalMonth\", \"FiscalMonthName\"]\n                }\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"CreateColumnGroups\",\n        \"ColumnGroupDefinitions\": [\n            {\n                \"CalendarName\": \"FiscalCalendar\",\n                \"GroupType\": \"TimeRelated\",\n                \"TimeRelatedGroup\": {\n                    \"Columns\": [\"DateKey\", \"FullDateAlternateKey\", \"Date\"]\n                }\n            },\n            {\n                \"CalendarName\": \"FiscalCalendar\",\n                \"GroupType\": \"TimeUnitAssociation\",\n                \"TimeUnit\": \"Years\",\n                \"TimeUnitAssociation\": {\n                    \"TimeUnit\": \"Years\",\n                    \"PrimaryColumnName\": \"FiscalYear\"\n                }\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["UpdateColumnGroups"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ColumnGroupDefinitions" },
				Description = "Update properties of one or more existing column groups.\nMandatory properties: ColumnGroupDefinitions (list with CalendarName, GroupType, TimeUnit (for TimeUnitAssociation), plus update values).\nColumn groups are identified by GroupType + TimeUnit (for TimeUnitAssociation groups).\nOptional: TableName (as search hint), Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"UpdateColumnGroups\",\n        \"ColumnGroupDefinitions\": [\n            {\n                \"CalendarName\": \"FiscalCalendar\",\n                \"GroupType\": \"TimeUnitAssociation\",\n                \"TimeUnit\": \"Years\",\n                \"TimeUnitAssociation\": {\n                    \"TimeUnit\": \"Years\",\n                    \"PrimaryColumnName\": \"FiscalYear\",\n                    \"AssociatedColumns\": [\"FiscalYear\", \"FiscalYearName\", \"FiscalYearShort\"]\n                }\n            }\n        ]\n    }\n}" }
			},
			["DeleteColumnGroups"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ColumnGroupDefinitions" },
				Description = "Delete one or more column groups from calendars.\nMandatory properties: ColumnGroupDefinitions (list with CalendarName, GroupType, TimeUnit (for TimeUnitAssociation)).\nColumn groups are identified by GroupType + TimeUnit (for TimeUnitAssociation groups).\nOptional: TableName (as search hint), Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"DeleteColumnGroups\",\n        \"ColumnGroupDefinitions\": [\n            {\n                \"CalendarName\": \"FiscalCalendar\",\n                \"GroupType\": \"TimeUnitAssociation\",\n                \"TimeUnit\": \"Months\"\n            }\n        ]\n    }\n}" }
			},
			["GetColumnGroups"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ColumnGroupDefinitions" },
				Description = "Get detailed information about one or more column groups. Use this (not ListColumnGroups) to retrieve the details or definition of a column group.\nMandatory properties: ColumnGroupDefinitions (list with CalendarName, GroupType, TimeUnit (for TimeUnitAssociation)).\nColumn groups are identified by GroupType + TimeUnit (for TimeUnitAssociation groups).\nOptional: TableName (as search hint), Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetColumnGroups\",\n        \"ColumnGroupDefinitions\": [\n            {\n                \"CalendarName\": \"FiscalCalendar\",\n                \"GroupType\": \"TimeUnitAssociation\",\n                \"TimeUnit\": \"Years\"\n            },\n            {\n                \"CalendarName\": \"FiscalCalendar\",\n                \"GroupType\": \"TimeRelated\"\n            }\n        ]\n    }\n}" }
			},
			["ListColumnGroups"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ColumnGroupFilter" },
				Description = "List all column groups within a calendar.\nMandatory properties: ColumnGroupFilter.CalendarName (the name of the calendar whose column groups to list). Note: CalendarName is a property of ColumnGroupFilter, not a top-level request field.\nOptional: ColumnGroupFilter.TableName (as search hint).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListColumnGroups\",\n        \"ColumnGroupFilter\": {\n            \"CalendarName\": \"FiscalCalendar\"\n        }\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				RequiredParams = Array.Empty<string>(),
				Description = "Get help information about calendar operations.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public CalendarOperationsTool(ILogger<CalendarOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "calendar_operations", Title = "Calendar Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("calendar_operations")]
	public async Task<CallToolResult> ExecuteCalendarOperation(McpServer mcpServer, CalendarOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "CalendarOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[13]
		{
			"CREATE", "UPDATE", "DELETE", "GET", "LIST", "RENAME", "EXPORTTMDL", "CREATECOLUMNGROUPS", "UPDATECOLUMNGROUPS", "DELETECOLUMNGROUPS",
			"GETCOLUMNGROUPS", "LISTCOLUMNGROUPS", "HELP"
		};
		string[] writeOperations = new string[7] { "CREATE", "UPDATE", "DELETE", "RENAME", "CREATECOLUMNGROUPS", "UPDATECOLUMNGROUPS", "DELETECOLUMNGROUPS" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("calendar_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "CalendarOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(CalendarOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
			if (Enumerable.Contains(writeOperations, op))
			{
				WriteOperationResult writeOperationResult = await writeGuard.ExecuteWriteOperationWithGuards(mcpServer, request.ConnectionName, request.Operation);
				if (!writeOperationResult.Success)
				{
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "CalendarOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(new CalendarOperationResponse
					{
						Success = false,
						Warnings = writeOperationResult.Warnings,
						Message = writeOperationResult.Message,
						Operation = request.Operation
					}, annotations));
					return result2;
				}
			}
			bool isWriteEnabled = writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = op switch
			{
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeleteOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTmdlOperation(request), (request.References?.FirstOrDefault()?.Name ?? "calendar") + ".tmdl", "text/plain", annotations), 
				"CREATECOLUMNGROUPS" => CallToolResultHelper.FromResponse(await HandleCreateColumnGroupsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATECOLUMNGROUPS" => CallToolResultHelper.FromResponse(await HandleUpdateColumnGroupsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETECOLUMNGROUPS" => CallToolResultHelper.FromResponse(await HandleDeleteColumnGroupsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GETCOLUMNGROUPS" => CallToolResultHelper.FromResponse(await HandleGetColumnGroupsOperation(request), annotations), 
				"LISTCOLUMNGROUPS" => CallToolResultHelper.FromResponse(await HandleListColumnGroupsOperation(request), annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(CalendarOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("CalendarOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"CREATE" => "Error executing Create operation: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error executing Update operation: " + ex.GetErrorMessage(), 
				"DELETE" => "Error executing Delete operation: " + ex.GetErrorMessage(), 
				"GET" => "Error executing Get operation: " + ex.GetErrorMessage(), 
				"LIST" => "Error listing calendars: " + ex.GetErrorMessage(), 
				"RENAME" => "Error executing Rename operation: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Error exporting calendar to TMDL: " + ex.GetErrorMessage(), 
				"CREATECOLUMNGROUPS" => "Error creating column group: " + ex.GetErrorMessage(), 
				"UPDATECOLUMNGROUPS" => "Error updating column group: " + ex.GetErrorMessage(), 
				"DELETECOLUMNGROUPS" => "Error deleting column group: " + ex.GetErrorMessage(), 
				"GETCOLUMNGROUPS" => "Error retrieving column group: " + ex.GetErrorMessage(), 
				"LISTCOLUMNGROUPS" => "Error listing column groups: " + ex.GetErrorMessage(), 
				_ => "Error executing calendar operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new CalendarOperationResponse
			{
				Success = false,
				Message = message,
				Operation = request.Operation
			}, annotations, ex));
			return result2;
		}
		finally
		{
			_logger.LogToolCallCompleted(annotations.Title, !annotations.ReadOnlyHint, result?.IsError ?? true, ConnectionOperations.ResolveSemanticModelId());
		}
	}

	private async Task<CalendarOperationResponse> HandleCreateOperation(CalendarOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one calendar definition"
			};
		}
		return MapBatchResponse(await CalendarOperations.CreateCalendars(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<CalendarOperationResponse> HandleUpdateOperation(CalendarOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one calendar definition"
			};
		}
		return MapBatchResponse(await CalendarOperations.UpdateCalendars(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<CalendarOperationResponse> HandleDeleteOperation(CalendarOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one calendar reference"
			};
		}
		return MapBatchResponse(await CalendarOperations.DeleteCalendars(request.ConnectionName, request.References, request.Options));
	}

	private async Task<CalendarOperationResponse> HandleGetOperation(CalendarOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one calendar reference"
			};
		}
		return MapBatchResponse(await CalendarOperations.GetCalendars(request.ConnectionName, request.References, request.Options));
	}

	private async Task<CalendarOperationResponse> HandleListOperation(CalendarOperationRequest request)
	{
		string tableName = request.Filter?.TableName;
		int maxResults = request.Filter?.MaxResults ?? 200;
		List<CalendarList> list = await CalendarOperations.ListCalendars(request.ConnectionName, tableName);
		int count = list.Count;
		bool num = count > maxResults;
		if (num)
		{
			list = list.Take(maxResults).ToList();
		}
		string text = (string.IsNullOrWhiteSpace(tableName) ? $"Listed {list.Count} calendar(s) across all tables" : $"Listed {list.Count} calendar(s) in table '{tableName}'");
		if (num)
		{
			text += $" (showing {maxResults} of {count}, use Filter.MaxResults to see more)";
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "CalendarOperationsTool", "List", request.ConnectionName, list.Count);
		return new CalendarOperationResponse
		{
			Success = true,
			Message = text,
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<CalendarOperationResponse> HandleRenameOperation(CalendarOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "Rename",
				Message = "RenameDefinitions is required and must contain at least one calendar rename definition"
			};
		}
		return MapBatchResponse(await CalendarOperations.RenameCalendars(request.ConnectionName, request.RenameDefinitions, request.Options));
	}

	private async Task<CalendarOperationResponse> HandleExportTmdlOperation(CalendarOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Calendar");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		CalendarReference calendarReference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateName(calendarReference.Name, "Calendar");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		string calendarName = calendarReference.Name;
		string tableName = calendarReference.TableName;
		string data = await CalendarOperations.ExportTMDL(request.ConnectionName, calendarName, tableName, request.TmdlExportOptions ?? new ExportTmdl());
		string objectIdentifier = ((!string.IsNullOrEmpty(tableName)) ? (tableName + "." + calendarName) : calendarName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "CalendarOperationsTool", "ExportTMDL", request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Calendar", objectIdentifier, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new CalendarOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = data,
			Warnings = warnings
		};
	}

	private static (string? calendarName, string? tableName, string? warningMessage) GetCalendarReferenceForExport(CalendarOperationRequest request)
	{
		if (request.References != null && request.References.Any())
		{
			ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateReferences(request.References, "Calendar");
			CalendarReference calendarReference = request.References.First();
			return (calendarName: calendarReference.Name, tableName: calendarReference.TableName, warningMessage: exportValidationResult.WarningMessage);
		}
		return (calendarName: null, tableName: null, warningMessage: null);
	}

	private async Task<CalendarOperationResponse> HandleCreateColumnGroupsOperation(CalendarOperationRequest request)
	{
		if (request.ColumnGroupDefinitions == null || !request.ColumnGroupDefinitions.Any())
		{
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "CreateColumnGroups",
				Message = "ColumnGroupDefinitions is required and must contain at least one column group definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await CalendarOperations.CreateColumnGroups(request.ConnectionName, request.ColumnGroupDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "CalendarOperationsTool", "CreateColumnGroups", request.ConnectionName, request.ColumnGroupDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<CalendarOperationResponse> HandleUpdateColumnGroupsOperation(CalendarOperationRequest request)
	{
		if (request.ColumnGroupDefinitions == null || !request.ColumnGroupDefinitions.Any())
		{
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "UpdateColumnGroups",
				Message = "ColumnGroupDefinitions is required and must contain at least one column group update definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await CalendarOperations.UpdateColumnGroupsByReference(request.ConnectionName, request.ColumnGroupDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "CalendarOperationsTool", "UpdateColumnGroups", request.ConnectionName, request.ColumnGroupDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<CalendarOperationResponse> HandleDeleteColumnGroupsOperation(CalendarOperationRequest request)
	{
		if (request.ColumnGroupDefinitions == null || !request.ColumnGroupDefinitions.Any())
		{
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "DeleteColumnGroups",
				Message = "ColumnGroupDefinitions is required and must contain at least one column group deletion definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await CalendarOperations.DeleteColumnGroupsByReference(request.ConnectionName, request.ColumnGroupDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "CalendarOperationsTool", "DeleteColumnGroups", request.ConnectionName, request.ColumnGroupDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<CalendarOperationResponse> HandleGetColumnGroupsOperation(CalendarOperationRequest request)
	{
		if (request.ColumnGroupDefinitions == null || !request.ColumnGroupDefinitions.Any())
		{
			return new CalendarOperationResponse
			{
				Success = false,
				Operation = "GetColumnGroups",
				Message = "ColumnGroupDefinitions is required and must contain at least one column group reference"
			};
		}
		BatchOperationResponse batchOperationResponse = await CalendarOperations.GetColumnGroupsByReference(request.ConnectionName, request.ColumnGroupDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "CalendarOperationsTool", "GetColumnGroups", request.ConnectionName, request.ColumnGroupDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<CalendarOperationResponse> HandleListColumnGroupsOperation(CalendarOperationRequest request)
	{
		string calendarName = request.ColumnGroupFilter?.CalendarName;
		string tableName = request.ColumnGroupFilter?.TableName;
		if (string.IsNullOrWhiteSpace(calendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ColumnGroupFilter.CalendarName is required for ListColumnGroups operation", ErrorSource.User);
		}
		List<CalendarColumnGroupDefinition> list = await CalendarOperations.ListColumnGroups(request.ConnectionName, calendarName, tableName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "CalendarOperationsTool", "ListColumnGroups", request.ConnectionName, list.Count);
		return new CalendarOperationResponse
		{
			Success = true,
			Message = $"Listed {list.Count} column group(s) in calendar '{calendarName}'",
			Operation = request.Operation,
			Data = list
		};
	}

	private CalendarOperationResponse HandleHelpOperation(CalendarOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "CalendarOperationsTool", "Help", request.ConnectionName, operations.Length);
		var help = new
		{
			SupportedOperations = operations,
			OperationDetails = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> op) => operations.Contains<string>(op.Key, StringComparer.OrdinalIgnoreCase)).ToDictionary<KeyValuePair<string, OperationMetadata>, string, OperationMetadata>((KeyValuePair<string, OperationMetadata> op) => op.Key, (KeyValuePair<string, OperationMetadata> op) => op.Value, StringComparer.OrdinalIgnoreCase),
			CalendarOverview = new
			{
				Description = "Calendar objects define logical calendars as part of DAX Time-Intelligence support. They are only supported when the compatibility level of the database is at 1701 or above.",
				SupportedColumnGroupTypes = new[]
				{
					new
					{
						Type = "TimeRelated",
						Description = "Simple collection of related time columns"
					},
					new
					{
						Type = "TimeUnitAssociation",
						Description = "Association with specific time unit and optional primary column"
					}
				},
				TimeUnits = new string[22]
				{
					"Unknown", "Year", "Semester", "SemesterOfYear", "Quarter", "QuarterOfYear", "QuarterOfSemester", "Month", "MonthOfYear", "MonthOfSemester",
					"MonthOfQuarter", "Week", "WeekOfYear", "WeekOfSemester", "WeekOfQuarter", "WeekOfMonth", "Date", "DayOfYear", "DayOfSemester", "DayOfQuarter",
					"DayOfMonth", "DayOfWeek"
				}
			},
			ExampleWorkflow = new string[4] { "1. Create a calendar: Use 'Create' operation with table and calendar name", "2. Add column groups: Use 'CreateColumnGroups' to add TimeRelated or TimeUnitAssociation groups", "3. Manage calendar: Use 'Update', 'Get', 'List', or 'Delete' operations as needed", "4. Export: Use 'ExportTMDL' to get TMDL representation" }
		};
		return new CalendarOperationResponse
		{
			Success = true,
			Message = "Calendar operations help information",
			Operation = request.Operation,
			Help = help
		};
	}

	private CalendarOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		CalendarOperationResponse calendarOperationResponse = new CalendarOperationResponse
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
			calendarOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return calendarOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, CalendarOperationRequest request)
	{
		switch (operation.ToUpperInvariant())
		{
		case "CREATE":
		{
			List<CalendarDefinition>? definitions2 = request.Definitions;
			return (definitions2 != null && definitions2.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "Definitions is required for Create operation");
		}
		case "UPDATE":
		{
			List<CalendarDefinition>? definitions = request.Definitions;
			return (definitions != null && definitions.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "Definitions is required for Update operation");
		}
		case "DELETE":
		{
			List<CalendarReference>? references = request.References;
			return (references != null && references.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "References is required for Delete operation");
		}
		case "GET":
		{
			List<CalendarReference>? references3 = request.References;
			return (references3 != null && references3.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "References is required for Get operation");
		}
		case "LIST":
			return (isValid: true, errorMessage: null);
		case "RENAME":
		{
			List<CalendarRename>? renameDefinitions = request.RenameDefinitions;
			return (renameDefinitions != null && renameDefinitions.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "RenameDefinitions is required for Rename operation");
		}
		case "EXPORTTMDL":
		{
			List<CalendarReference>? references2 = request.References;
			return (references2 != null && references2.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "References is required for ExportTMDL operation");
		}
		case "CREATECOLUMNGROUPS":
		{
			List<CalendarColumnGroupDefinition>? columnGroupDefinitions2 = request.ColumnGroupDefinitions;
			return (columnGroupDefinitions2 != null && columnGroupDefinitions2.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "ColumnGroupDefinitions is required for CreateColumnGroups operation");
		}
		case "UPDATECOLUMNGROUPS":
		{
			List<CalendarColumnGroupDefinition>? columnGroupDefinitions3 = request.ColumnGroupDefinitions;
			return (columnGroupDefinitions3 != null && columnGroupDefinitions3.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "ColumnGroupDefinitions is required for UpdateColumnGroups operation");
		}
		case "DELETECOLUMNGROUPS":
		{
			List<CalendarColumnGroupDefinition>? columnGroupDefinitions = request.ColumnGroupDefinitions;
			return (columnGroupDefinitions != null && columnGroupDefinitions.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "ColumnGroupDefinitions is required for DeleteColumnGroups operation");
		}
		case "GETCOLUMNGROUPS":
		{
			List<CalendarColumnGroupDefinition>? columnGroupDefinitions4 = request.ColumnGroupDefinitions;
			return (columnGroupDefinitions4 != null && columnGroupDefinitions4.Count > 0) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "ColumnGroupDefinitions is required for GetColumnGroups operation");
		}
		case "LISTCOLUMNGROUPS":
			return (!string.IsNullOrWhiteSpace(request.ColumnGroupFilter?.CalendarName)) ? (isValid: true, errorMessage: null) : (isValid: false, errorMessage: "ColumnGroupFilter.CalendarName is required for ListColumnGroups operation");
		case "HELP":
			return (isValid: true, errorMessage: null);
		default:
			return (isValid: false, errorMessage: "Unknown operation: " + operation);
		}
	}
}
