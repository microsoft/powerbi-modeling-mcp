using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Tools;

namespace PowerBIModelingMCP.Library.Services;

public class ToolRegistrationService
{
	private readonly MCPServerConfiguration _config;

	private readonly ILogger<ToolRegistrationService> _logger;

	public ToolRegistrationService(MCPServerConfiguration config, ILogger<ToolRegistrationService> logger)
	{
		_config = config;
		_logger = logger;
	}

	public void RegisterTools(IMcpServerBuilder mcpBuilder)
	{
		_logger.LogInformation("=== Tool Registration Started ===");
		_logger.LogInformation("Tool Configuration at registration time:");
		_logger.LogInformation("  Tool Mode: {Mode}", _config.Mode);
		_logger.LogInformation("  Current Tool Mode: {ToolMode}", _config.GetEnabledToolMode());
		if (!_config.IsValid())
		{
			_logger.LogError("Invalid tool configuration: Tool mode must be valid.");
			_logger.LogError("Current settings - Mode: {Mode}", _config.Mode);
			throw new InvalidOperationException("Invalid tool configuration: Tool mode must be valid.");
		}
		ToolsConfiguration tools = _config.Tools;
		_logger.LogInformation("Registering tools (all tools registered, write operations controlled by WriteGuard)...");
		if (tools.EnableDatabaseOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "DatabaseOperationsTool");
			mcpBuilder.WithTools<DatabaseOperationsTool>();
		}
		if (tools.EnableTableOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "TableOperationsTool");
			mcpBuilder.WithTools<TableOperationsTool>();
		}
		if (tools.EnableColumnOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "ColumnOperationsTool");
			mcpBuilder.WithTools<ColumnOperationsTool>();
		}
		if (tools.EnableMeasureOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "MeasureOperationsTool");
			mcpBuilder.WithTools<MeasureOperationsTool>();
		}
		if (tools.EnableNamedExpressionOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "NamedExpressionOperationsTool");
			mcpBuilder.WithTools<NamedExpressionOperationsTool>();
		}
		if (tools.EnableFunctionOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "FunctionOperationsTool");
			mcpBuilder.WithTools<FunctionOperationsTool>();
		}
		if (tools.EnableObjectTranslationOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "ObjectTranslationOperationsTool");
			mcpBuilder.WithTools<ObjectTranslationOperationsTool>();
		}
		if (tools.EnableCalculationGroupOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "CalculationGroupOperationsTool");
			mcpBuilder.WithTools<CalculationGroupOperationsTool>();
		}
		if (tools.EnableCalendarOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "CalendarOperationsTool");
			mcpBuilder.WithTools<CalendarOperationsTool>();
		}
		if (tools.EnableQueryGroupOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "QueryGroupOperationsTool");
			mcpBuilder.WithTools<QueryGroupOperationsTool>();
		}
		if (tools.EnableRelationshipOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "RelationshipOperationsTool");
			mcpBuilder.WithTools<RelationshipOperationsTool>();
		}
		if (tools.EnableDataSourceOperationsTool && _config.Compatibility == CompatibilityMode.Full)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "DataSourceOperationsTool");
			mcpBuilder.WithTools<DataSourceOperationsTool>();
		}
		else if (tools.EnableDataSourceOperationsTool && _config.Compatibility == CompatibilityMode.PowerBI)
		{
			_logger.LogInformation("Skipping tool: {ToolName} (not supported in PowerBI compatibility mode)", "DataSourceOperationsTool");
		}
		if (tools.EnablePartitionOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "PartitionOperationsTool");
			mcpBuilder.WithTools<PartitionOperationsTool>();
		}
		if (tools.EnableSecurityRoleOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "SecurityRoleOperationsTool");
			mcpBuilder.WithTools<SecurityRoleOperationsTool>();
		}
		if (tools.EnableUserHierarchyOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "UserHierarchyOperationsTool");
			mcpBuilder.WithTools<UserHierarchyOperationsTool>();
		}
		if (tools.EnableCultureOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "CultureOperationsTool");
			mcpBuilder.WithTools<CultureOperationsTool>();
		}
		if (tools.EnableModelOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "ModelOperationsTool");
			mcpBuilder.WithTools<ModelOperationsTool>();
		}
		if (tools.EnablePerspectiveOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "PerspectiveOperationsTool");
			mcpBuilder.WithTools<PerspectiveOperationsTool>();
		}
		if (tools.EnableConnectionOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "ConnectionOperationsTool");
			mcpBuilder.WithTools<ConnectionOperationsTool>();
		}
		if (tools.EnableDaxQueryOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "DaxQueryOperationsTool");
			mcpBuilder.WithTools<DaxQueryOperationsTool>();
		}
		if (tools.EnableTransactionOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "TransactionOperationsTool");
			mcpBuilder.WithTools<TransactionOperationsTool>();
		}
		if (tools.EnableTraceOperationsTool)
		{
			_logger.LogInformation("Loading tool: {ToolName}", "TraceOperationsTool");
			mcpBuilder.WithTools<TraceOperationsTool>();
		}
		_logger.LogInformation("=== Tool Registration Completed ===");
	}
}
