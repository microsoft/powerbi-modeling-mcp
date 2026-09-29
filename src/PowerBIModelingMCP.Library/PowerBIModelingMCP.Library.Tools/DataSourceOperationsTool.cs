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
public class DataSourceOperationsTool
{
	public const string ToolName = "data_source_operations";

	private readonly ILogger<DataSourceOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["List"] = new OperationMetadata
			{
				Description = "List all SSAS data sources in the model.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get details of one or more SSAS data sources.\nMandatory properties: References (list of DataSourceReference objects with Name).\nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"SalesDataSource\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"SalesDataSource\" },\n            { \"Name\": \"InventoryDataSource\" }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true\n        }\n    }\n}" }
			},
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more new SSAS data sources.\nNote: Power BI uses named expressions instead of data sources as the centralized place to store connectivity information.\nMandatory properties: Definitions (list of DataSourceDefinition objects with Name, ConnectionString).\nOptional: Description, Provider, ImpersonationMode, Account, Password, MaxConnections, Isolation, Timeout, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Name\": \"SalesDataSource\", \n                \"ConnectionString\": \"Server=localhost;Database=SalesNew;Trusted_Connection=True\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Name\": \"SalesDataSource\", \n                \"ConnectionString\": \"Server=localhost;Database=Sales;Trusted_Connection=True\" \n            },\n            {\n                \"Name\": \"InventoryDataSource\",\n                \"ConnectionString\": \"Server=localhost;Database=Inventory;Trusted_Connection=True\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing SSAS data sources' properties.\nNames cannot be changed - use Rename operation for name changes.\nMandatory properties: Definitions (list of DataSourceDefinition objects with Name).\nOptional: Description, ConnectionString, Provider, ImpersonationMode, Account, Password, MaxConnections, Isolation, Timeout, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"SalesDataSource\", \n                \"ConnectionString\": \"Server=localhost;Database=SalesNew;Trusted_Connection=True\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"SalesDataSource\", \n                \"Description\": \"Sales database connection\"\n            },\n            {\n                \"Name\": \"InventoryDataSource\",\n                \"Description\": \"Inventory database connection\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more SSAS data sources.\nCannot delete SSAS data sources that are referenced by table partitions.\nMandatory properties: References (list of DataSourceReference objects with Name).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"ObsoleteDataSource\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"ObsoleteDataSource1\" },\n            { \"Name\": \"ObsoleteDataSource2\" }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more SSAS data sources.\nMandatory properties: RenameDefinitions (list of DataSourceRename objects with CurrentName, NewName).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"CurrentName\": \"OldDataSource\", \n                \"NewName\": \"NewDataSource\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"CurrentName\": \"OldDataSource1\", \n                \"NewName\": \"NewDataSource1\"\n            },\n            {\n                \"CurrentName\": \"OldDataSource2\",\n                \"NewName\": \"NewDataSource2\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Test"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Test an SSAS data source connection by validating its configuration.\nMandatory properties: References (list with one DataSourceReference containing Name).\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Test\",\n        \"References\": [\n            { \"Name\": \"SalesSource\" }\n        ]\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export SSAS data source to TMDL format.\nMandatory properties: References (list with one DataSourceReference containing Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"Name\": \"SalesDataSource\" }\n        ]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations.\nProvides comprehensive information about all available SSAS data source operations.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public DataSourceOperationsTool(ILogger<DataSourceOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "data_source_operations", Title = "Data Source Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("data_source_operations")]
	public async Task<CallToolResult> ExecuteDataSourceOperation(McpServer mcpServer, DataSourceOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "DataSourceOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[9] { "CREATE", "UPDATE", "DELETE", "GET", "LIST", "RENAME", "TEST", "EXPORTTMDL", "HELP" };
		string[] writeOperations = new string[4] { "CREATE", "UPDATE", "DELETE", "RENAME" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("data_source_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "DataSourceOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(DataSourceOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "DataSourceOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(new DataSourceOperationResponse
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
				"TEST" => CallToolResultHelper.FromResponse(await HandleTestOperation(request), annotations), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "datasource") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(DataSourceOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("DataSourceOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"CREATE" => "Error creating data source: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error updating data source: " + ex.GetErrorMessage(), 
				"DELETE" => "Error deleting data source: " + ex.GetErrorMessage(), 
				"GET" => "Error getting data source: " + ex.GetErrorMessage(), 
				"LIST" => "Failed to list data sources: " + ex.GetErrorMessage(), 
				"RENAME" => "Failed to rename data source: " + ex.GetErrorMessage(), 
				"TEST" => "Error testing data source: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Error getting data source TMDL: " + ex.GetErrorMessage(), 
				_ => "Error executing data source operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new DataSourceOperationResponse
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

	private async Task<DataSourceOperationResponse> HandleCreateOperation(DataSourceOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new DataSourceOperationResponse
			{
				Success = false,
				Message = "Definitions is required for Create operation",
				Operation = "Create",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await DataSourceOperations.CreateDataSources(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "DataSourceOperationsTool", "Create", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<DataSourceOperationResponse> HandleUpdateOperation(DataSourceOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new DataSourceOperationResponse
			{
				Success = false,
				Message = "Definitions is required for Update operation",
				Operation = "Update",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await DataSourceOperations.UpdateDataSources(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "DataSourceOperationsTool", "Update", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<DataSourceOperationResponse> HandleDeleteOperation(DataSourceOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new DataSourceOperationResponse
			{
				Success = false,
				Message = "References is required for Delete operation",
				Operation = "Delete",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await DataSourceOperations.DeleteDataSources(request.ConnectionName, request.References, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "DataSourceOperationsTool", "Delete", request.ConnectionName, request.References.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<DataSourceOperationResponse> HandleGetOperation(DataSourceOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new DataSourceOperationResponse
			{
				Success = false,
				Message = "References is required for Get operation",
				Operation = "Get",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await DataSourceOperations.GetDataSources(request.ConnectionName, request.References, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "DataSourceOperationsTool", "Get", request.ConnectionName, request.References.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<DataSourceOperationResponse> HandleListOperation(DataSourceOperationRequest request)
	{
		List<DataSourceList> list = await DataSourceOperations.ListDataSources(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "DataSourceOperationsTool", request.Operation, request.ConnectionName, list.Count);
		return new DataSourceOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} data sources",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<DataSourceOperationResponse> HandleRenameOperation(DataSourceOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new DataSourceOperationResponse
			{
				Success = false,
				Message = "RenameDefinitions is required for Rename operation",
				Operation = "Rename",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await DataSourceOperations.RenameDataSources(request.ConnectionName, request.RenameDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "DataSourceOperationsTool", "Rename", request.ConnectionName, request.RenameDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<DataSourceOperationResponse> HandleTestOperation(DataSourceOperationRequest request)
	{
		DataSourceReference dataSourceReference = request.References?.FirstOrDefault();
		if (dataSourceReference == null || string.IsNullOrEmpty(dataSourceReference.Name))
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new DataSourceOperationResponse
			{
				Success = false,
				Message = "References with Name is required for Test operation",
				Operation = "Test",
				Help = value
			};
		}
		string dataSourceName = dataSourceReference.Name;
		OperationResult operationResult = await DataSourceOperations.TestDataSource(request.ConnectionName, dataSourceName);
		if (operationResult.Success)
		{
			_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Status=Success", "DataSourceOperationsTool", request.Operation, request.ConnectionName);
		}
		else
		{
			_logger.LogWarning("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Status=Failed", "DataSourceOperationsTool", request.Operation, request.ConnectionName);
		}
		return new DataSourceOperationResponse
		{
			Success = operationResult.Success,
			Message = (operationResult.Success ? ("Data source '" + dataSourceName + "' tested successfully") : ("Data source '" + dataSourceName + "' test failed")),
			Operation = request.Operation,
			Data = operationResult
		};
	}

	private async Task<DataSourceOperationResponse> HandleExportTMDLOperation(DataSourceOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "DataSource");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new DataSourceOperationResponse
			{
				Success = false,
				Message = validation.ErrorMessage,
				Operation = "ExportTMDL",
				Help = value
			};
		}
		DataSourceReference dataSourceReference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateName(dataSourceReference.Name, "DataSource");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new DataSourceOperationResponse
			{
				Success = false,
				Message = exportValidationResult.ErrorMessage,
				Operation = "ExportTMDL",
				Help = value2
			};
		}
		string dataSourceName = dataSourceReference.Name;
		string data = await DataSourceOperations.ExportTMDL(request.ConnectionName, dataSourceName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "DataSourceOperationsTool", request.Operation, request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Data Source", dataSourceName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new DataSourceOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = data,
			Warnings = warnings
		};
	}

	private DataSourceOperationResponse HandleHelpOperation(DataSourceOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "DataSourceOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		DataSourceOperationResponse dataSourceOperationResponse = new DataSourceOperationResponse();
		dataSourceOperationResponse.Success = true;
		dataSourceOperationResponse.Message = "Help information retrieved successfully";
		dataSourceOperationResponse.Operation = request.Operation;
		dataSourceOperationResponse.Help = new
		{
			ToolName = "data_source_operations",
			Description = "Perform operations on SSAS data sources in semantic models.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[6] { "Use the Operation parameter to specify which operation to perform.", "Operations accept lists of items for bulk processing.", "Single-item operations are represented as lists of one.", "Use Options.ContinueOnError to control error handling in batch operations.", "Use Options.UseTransaction to control transactional behavior.", "Note: Power BI uses named expressions instead of data sources for centralized connectivity information." }
		};
		return dataSourceOperationResponse;
	}

	private DataSourceOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		DataSourceOperationResponse dataSourceOperationResponse = new DataSourceOperationResponse
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
			dataSourceOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return dataSourceOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, DataSourceOperationRequest request)
	{
		switch (operation.ToUpperInvariant())
		{
		case "CREATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				_logger.LogWarning("Definitions is required for Create operation");
				return (isValid: false, errorMessage: "Definitions is required for Create operation");
			}
			break;
		case "UPDATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				_logger.LogWarning("Definitions is required for Update operation");
				return (isValid: false, errorMessage: "Definitions is required for Update operation");
			}
			break;
		case "DELETE":
			if (request.References == null || !request.References.Any())
			{
				_logger.LogWarning("References is required for Delete operation");
				return (isValid: false, errorMessage: "References is required for Delete operation");
			}
			break;
		case "GET":
			if (request.References == null || !request.References.Any())
			{
				_logger.LogWarning("References is required for Get operation");
				return (isValid: false, errorMessage: "References is required for Get operation");
			}
			break;
		case "RENAME":
			if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
			{
				_logger.LogWarning("RenameDefinitions is required for Rename operation");
				return (isValid: false, errorMessage: "RenameDefinitions is required for Rename operation");
			}
			break;
		case "TEST":
			if (request.References == null || !request.References.Any() || string.IsNullOrEmpty(request.References.First().Name))
			{
				_logger.LogWarning("References with Name is required for Test operation");
				return (isValid: false, errorMessage: "References with Name is required for Test operation");
			}
			break;
		case "EXPORTTMDL":
			if (request.References == null || !request.References.Any() || string.IsNullOrEmpty(request.References.First().Name))
			{
				_logger.LogWarning("References with Name is required for ExportTMDL operation");
				return (isValid: false, errorMessage: "References with Name is required for ExportTMDL operation");
			}
			break;
		}
		return (isValid: true, errorMessage: null);
	}
}
