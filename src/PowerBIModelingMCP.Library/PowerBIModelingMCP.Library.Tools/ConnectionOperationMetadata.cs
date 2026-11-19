using System;
using System.Collections.Generic;
using PowerBIModelingMCP.Library.Common;

namespace PowerBIModelingMCP.Library.Tools;

public static class ConnectionOperationMetadata
{
	public static ToolMetadata<ConnectionOperation> CreateToolMeatadata()
	{
		return new ToolMetadata<ConnectionOperation>
		{
			Operations = new Dictionary<ConnectionOperation, OperationMetadata>
			{
				[ConnectionOperation.Connect] = CreateConnectMetadata(),
				[ConnectionOperation.ConnectFabric] = CreateConnectFabricMetadata(),
				[ConnectionOperation.ConnectFolder] = CreateConnectFolderMetadata(),
				[ConnectionOperation.ConnectBimFile] = CreateConnectBimFileMetadata(),
				[ConnectionOperation.Disconnect] = CreateDisconnectMetadata(),
				[ConnectionOperation.GetConnection] = CreateGetConnectionMetadata(),
				[ConnectionOperation.ListConnections] = CreateListConnectionsMetadata(),
				[ConnectionOperation.ListLocalInstances] = CreateListLocalInstancesMetadata(),
				[ConnectionOperation.Help] = CreateHelpMetadata()
			}
		};
	}

	private static OperationMetadata CreateConnectMetadata()
	{
		OperationMetadata operationMetadata = new OperationMetadata();
		operationMetadata.RequiredParams = Array.Empty<string>();
		operationMetadata.ForbiddenParams = new string[1] { "ConnectionName" };
		operationMetadata.Description = "Establishes a connection to a Microsoft tabular semantic model data source (PowerBI Desktop, Fabric XML/A endpoint, Analysis Services).\nMandatory properties: Either ConnectionString OR DataSource.\nOptional: InitialCatalog (when using DataSource).";
		operationMetadata.Tips = new string[1] { "If local connection fails, use ListLocalInstances to discover Power BI Desktop instances running on your machine." };
		operationMetadata.ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Connect\",\n        \"ConnectionString\": \"Provider=MSOLAP;Data Source=localhost:<port>\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Connect\",\n        \"ConnectionString\": \"Data Source=powerbi://api.fabric.microsoft.com/v1.0/myorg/MyWorkspace;Initial Catalog=MyDataset;\"\n    }\n}" };
		return operationMetadata;
	}

	private static OperationMetadata CreateConnectFabricMetadata()
	{
		OperationMetadata operationMetadata = new OperationMetadata();
		operationMetadata.RequiredParams = new string[2] { "WorkspaceName", "SemanticModelName" };
		operationMetadata.ForbiddenParams = new string[1] { "ConnectionName" };
		operationMetadata.Description = "Connects to a Microsoft Fabric workspace and semantic model using natural workspace names.\nMandatory properties: WorkspaceName, SemanticModelName.\nOptional: TenantName (defaults to 'myorg'), ClearCredential (defaults to false).";
		operationMetadata.ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ConnectFabric\",\n        \"WorkspaceName\": \"My Premium Space\",\n        \"SemanticModelName\": \"SalesModel\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ConnectFabric\",\n        \"WorkspaceName\": \"My Premium Space\",\n        \"SemanticModelName\": \"Experiment2\",\n        \"TenantName\": \"contoso.com\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ConnectFabric\",\n        \"WorkspaceName\": \"My Premium Space\",\n        \"SemanticModelName\": \"SalesModel\",\n        \"ClearCredential\": false\n    }\n}" };
		return operationMetadata;
	}

	private static OperationMetadata CreateConnectFolderMetadata()
	{
		OperationMetadata operationMetadata = new OperationMetadata();
		operationMetadata.RequiredParams = new string[1] { "FolderPath" };
		operationMetadata.ForbiddenParams = new string[1] { "ConnectionName" };
		operationMetadata.Description = "Connects to a folder that contains the database.tmdl file, usually inside the Power BI Project (PBIP) *.SemanticModel/definition folder.\nFirst checks if database.tmdl exists in the provided folder. If not found, checks in the 'definition' subfolder.\nMandatory properties: FolderPath.\nOptional: None (ConnectionName is auto-generated).";
		operationMetadata.ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ConnectFolder\",\n        \"FolderPath\": \"C:\\\\pbip\\\\Contoso.SemanticModel\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ConnectFolder\",\n        \"FolderPath\": \"C:\\\\pbip\\\\Contoso.SemanticModel\\\\definition\"\n    }\n}" };
		return operationMetadata;
	}

	private static OperationMetadata CreateConnectBimFileMetadata()
	{
		OperationMetadata operationMetadata = new OperationMetadata();
		operationMetadata.RequiredParams = new string[1] { "BimFilePath" };
		operationMetadata.ForbiddenParams = new string[1] { "ConnectionName" };
		operationMetadata.Description = "Connects to a .bim (JSON-serialized TOM) file by loading it as an offline database.\nThe .bim file is the standard JSON format for tabular model definitions.\nMandatory properties: BimFilePath.\nOptional: None (ConnectionName is auto-generated).";
		operationMetadata.ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ConnectBimFile\",\n        \"BimFilePath\": \"C:\\\\Models\\\\Sales.bim\"\n    }\n}" };
		return operationMetadata;
	}

	private static OperationMetadata CreateDisconnectMetadata()
	{
		return new OperationMetadata
		{
			RequiredParams = Array.Empty<string>(),
			Description = "Disconnects from a specific connection or all connections if no ConnectionName is specified.\nMandatory properties: None.\nOptional: ConnectionName (if omitted, disconnects all connections).",
			ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Disconnect\",\n        \"ConnectionName\": \"LocalPbi\"\n    }\n}" }
		};
	}

	private static OperationMetadata CreateGetConnectionMetadata()
	{
		OperationMetadata operationMetadata = new OperationMetadata();
		operationMetadata.RequiredParams = new string[1] { "ConnectionName" };
		operationMetadata.Description = "Retrieves detailed information about a specific connection including server details, database name, and connection status.\nMandatory properties: ConnectionName.\nOptional: None.";
		operationMetadata.ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetConnection\",\n        \"ConnectionName\": \"LocalPbi\"\n    }\n}" };
		return operationMetadata;
	}

	private static OperationMetadata CreateListConnectionsMetadata()
	{
		OperationMetadata operationMetadata = new OperationMetadata();
		operationMetadata.Description = "Lists all active connections with detailed information including connection name, server details, database name, and folder path for offline connections.\nMandatory properties: None.\nOptional: None.";
		operationMetadata.Tips = new string[2] { "Offline connections are created as a byproduct of creating empty database/model or deserializing database from TMDL files", "Offline connections show a folder path instead of server details" };
		operationMetadata.ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListConnections\"\n    }\n}" };
		return operationMetadata;
	}

	private static OperationMetadata CreateListLocalInstancesMetadata()
	{
		return new OperationMetadata
		{
			Description = "Discovers and lists all local PowerBI Desktop and Analysis Services instances running on the local machine with connection details.\nMandatory properties: None.\nOptional: None.",
			ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListLocalInstances\"\n    }\n}" }
		};
	}

	private static OperationMetadata CreateHelpMetadata()
	{
		return new OperationMetadata
		{
			Description = "Describe the tool and its operations.",
			ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
		};
	}
}
