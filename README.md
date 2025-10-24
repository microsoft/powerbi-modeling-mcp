# ✨ Power BI Modeling MCP Server

The **Power BI Modeling MCP Server** implements the [MCP specification](https://modelcontextprotocol.io/introduction) to create a seamless connection between AI agents and Power BI semantic models. This project is in Public Preview and implementation may significantly change prior to our General Availability.

It’s a local, AI-ready Model Context Protocol (MCP) server that encapsulates the full Power BI semantic modeling capabilities, enabling you to build and modify models using natural language. Its goal is to dramatically accelerate semantic model development in Power BI, empowering developers to work more efficiently and focus on what matters most: delivering insights.

![powerbi-modeling-mcp-diagram](docs/img/e2e-diagram.png)

## 💡 What can you do?

- **🔄 Build and Modify Semantic Models with Natural Language** - Tell your AI assistant what you need, and it uses this MCP server to create, update, and manage tables, columns, measures, relationships, and more... across Power BI Desktop and Fabric semantic models.

- **⚡ Bulk Operations at Scale** - AI applications can execute batch modeling operations on hundreds of objects simultaneously — bulk renaming, bulk refactoring, model translations, or model security rules - with transaction support and error handling, turning hours of repetitive work into seconds.

- **🤖 Agentic Development Workflows** - Supports working with [TMDL and Power BI Project files](https://learn.microsoft.com/power-bi/developer/projects/projects-dataset#tmdl-format), enabling AI agents to autonomously plan, create, and execute complex modeling tasks across your semantic model codebase.

- **🔍 Query and Validate DAX** - AI assistants can execute and validate DAX queries against your model, helping you test measures, troubleshoot calculations, and explore your data

📹 Watch the video for an end-to-end demo: https://www.youtube.com/watch?v=nViU15ANL9M

## 🚀 Getting started

### Installation

#### Visual Studio Code (Recommended)

1. Install [Visual Studio Code](https://code.visualstudio.com/download).
2. Install the [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot) and [GitHub Copilot Chat](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot-chat) extensions.
3. Install the [Power BI Modeling MCP](https://marketplace.visualstudio.com) extension.

#### Manual setup

This MCP Server can also be configured across other IDEs, CLIs, and MCP clients:

1. Download the latest version [here](../../releases/latest).
2. Unzip the contents to a folder of your choice, for example: `%USERPROFILE%\MCPServers\PowerBIModelingMCP`
3. Run `powerbi-modeling-mcp.exe`
4. Copy the MCP JSON registration from the console and register it in your preferred MCP client tool.

### Usage

> [!WARNING]  
> Use caution when connecting this MCP server to a semantic model. The underlying LLM may produce unexpected or inaccurate results, which could lead to unintended changes. Always create a backup of your model before performing any operations.

**First, connect to a Power BI semantic model**, which can reside in either Power BI Desktop, a local Power BI Project (PBIP) or a Fabric workspace.

- **For Power BI Desktop:** enter a prompt such as `Connect to '[File Name]' in Power BI Desktop`

- **For Semantic Model in PBIP:** enter a prompt such as `Open semantic model from PBIP folder '[Path to the TMDL files in the PBIP folder]'`

- **For Semantic Model in Fabric Workspace:** enter a prompt such as `Connect to semantic model '[Semantic Model Name]' in Fabric Workspace '[Workspace Name]'`

Once the connection is successfully established, you can try one of the following scenarios:

| Scenario                                                | Prompt examples                                                                                                                                                                   |
| ------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Analyze naming convention and bulk rename.              | `Analyze the naming convention of my model and suggest renames for consistency.`<br>`Analyze the naming convention of table 'Sales' and apply the same pattern across all model.` |
| Set descriptions across your model for documentation.   | `Add descriptions to all measures, columns, and tables to clearly communicate the purpose of your model and explain the logic behind your DAX calculations in simple terms.`      |
| Translate your semantic model.                          | `Generate a French translation for my model including tables, columns and measures.`                                                                                              |
| Refactor measures into Calculation Groups or UDF.       | `Refactor measures 'Sales Amount 12M Avg' and 'Sales Amount 6M Avg' into a calculation group and include new variants: 24M and 3M.`                                               |
| Refactor your queries to use semantic model parameters. | `Analyze the Power Query code for all tables, identify the data source configuration, and create semantic model parameters to enable easy switching of the data source location.` |

> [!TIP]
> The scenarios above are just examples. This MCP server equips your agents with modeling tools for any type of model change, and with the right prompt and context, you can automate virtually any modeling task.

## 🛠️ Available tools

| Tool Name                               | What It Does                                                                                                   |
| --------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| **connection_operations**               | Connect to Power BI Desktop or Fabric workspaces                                                               |
| **database_operations**                 | Manage semantic models - connect, create, update, list databases, import/export TMDL folders, deploy to Fabric |
| **transaction_operations**              | Control database transactions (begin, commit, rollback, get status)                                            |
| **model_operations**                    | Work with the overall model (get info, create, update, refresh, get stats, rename)                             |
| **table_operations**                    | Manage tables (create, update, delete, get, list, refresh, rename)                                             |
| **column_operations**                   | Manage individual table columns (create, update, delete, get, list, rename)                                    |
| **measure_operations**                  | Manage individual DAX measures (create, update, delete, get, list, rename, move between tables)                |
| **relationship_operations**             | Handle relationships between tables (create, update, delete, activate/deactivate, find)                        |
| **dax_query_operations**                | Execute, validate, and generate DAX queries against the model                                                  |
| **partition_operations**                | Manage table partitions (create, update, delete, refresh specific partitions)                                  |
| **user_hierarchy_operations**           | Work with user-defined hierarchies (create, update, delete levels, reorder)                                    |
| **calculation_group_operations**        | Manage calculation groups and calculation items for time intelligence and other calculations                   |
| **security_role_operations**            | Configure security roles and row-level security (RLS) table permissions                                        |
| **perspective_operations**              | Manage perspectives and their members (filtered views of the model for different audiences)                    |
| **named_expression_operations**         | Work with named expressions and Power Query parameters (create, update, delete, get, list, rename)             |
| **function_operations**                 | Manage individual DAX user-defined functions                                                                   |
| **culture_operations**                  | Manage cultures for multi-language support (create, update, delete, get valid culture names)                   |
| **object_translation_operations**       | Handle translations for model objects across different cultures/languages                                      |
| **calendar_operations**                 | Manage calendar objects and time intelligence column groups                                                    |
| **query_group_operations**              | Organize and manage query groups for Power Query expressions                                                   |
| **batch_table_operations**              | Perform bulk operations on tables (create, update, delete, get, rename multiple tables)                        |
| **batch_column_operations**             | Perform bulk operations on table columns (create, update, delete, get, rename multiple columns at once)        |
| **batch_measure_operations**            | Perform bulk operations on measures (create, update, delete, get, rename, move multiple measures)              |
| **batch_function_operations**           | Perform bulk operations on DAX functions (create, update, delete, get, rename multiple functions)              |
| **batch_perspective_operations**        | Bulk manage perspective members (tables, columns, measures, hierarchies)                                       |
| **batch_object_translation_operations** | Bulk create, update, delete, or get object translations across cultures                                        |

> [!NOTE]
> This project is in Public Preview and tools may significantly change prior to our General Availability.

## Configuration

The MCP server supports several command line options:

| Option               | Default | Description                                                                                                                                                                                                               |
| -------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `--start`            |         | Starts the MCP server; necessary for server registration with MCP client.                                                                                                                                                 |
| `--readwrite`        | Yes     | Enabled by default, enables write operations with confirmation prompt before applying an edit to your semantic model (once per database).                                                                                 |
| `--readonly`         |         | Safe mode, prevents any write operations to your semantic model                                                                                                                                                           |
| `--skipconfirmation` |         | Automatically approves all write operations without confirmation prompts. Only use skip confirmation mode when you're confident about the operations being performed and have appropriate backups of your semantic model. |
| `--compatibility`    | PowerBI | By default, it is optimized for Power BI semantic models. Change the setting to `Full` if you want to run this MCP server against Analysis Services databases.                                                            |

You can modify the `args` in the MCP registration JSON:

```json
{
"servers": {
		"powerbi-modeling-mcp": {
			"command": "[PATH TO YOUR MCP]\\powerbi-modeling-mcp.exe",
			"args": [
				"--start"
                , "--skipconfirmation"
			],
			"env": {},
			"type": "stdio"
		}
	}
}
```

## 💬 Feedback and Support

- Check the [Troubleshooting guide](TROUBLESHOOTING.md) to diagnose and resolve common issues.
- We're building this in the open. Your feedback is much appreciated, and will help us shape the future of the Power BI Modeling MCP server.
    - 👉 [Open an issue](../../issues) in the public GitHub repository - we’d love to hear from you!

## Security

Your credentials are always handled securely through the official [Azure Identity SDK](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/README.md) - **we never store or manage tokens directly**.

MCP as a phenomenon is very novel and cutting-edge. As with all new technology standards, consider doing a security review to ensure any systems that integrate with MCP servers follow all regulations and standards your system is expected to adhere to. This includes not only the Azure MCP Server, but any MCP client/agent that you choose to implement down to the model provider.

You should follow Microsoft security guidance for MCP servers, including enabling Entra ID authentication, secure token management, and network isolation. Refer to [Microsoft Security Documentation](https://learn.microsoft.com/en-us/azure/api-management/secure-mcp-servers) for details.

## Data Collection

The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's [privacy statement](https://www.microsoft.com/privacy/privacystatement). You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.

## Compliance Responsibility

This MCP server may interact with clients and services outside Microsoft compliance boundaries. You are responsible for ensuring that any integration complies with applicable organizational, regulatory, and contractual requirements.

## Third Party Components

This MCP server may use or depend on third party components.  You are responsible for reviewing and complying with the licenses and security posture of any third-party components.

## Export Control

Use of this software must comply with all applicable export laws and regulations, including U.S. Export Administration Regulations and local jurisdiction requirements.

## No Warranty / Limitation of Liability

This software is provided “as is” without warranties or conditions of any kind, either express or implied. Microsoft shall not be liable for any damages arising from use, misuse, or misconfiguration of this software.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information, see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [open@microsoft.com](mailto:open@microsoft.com) with any additional questions or comments.
