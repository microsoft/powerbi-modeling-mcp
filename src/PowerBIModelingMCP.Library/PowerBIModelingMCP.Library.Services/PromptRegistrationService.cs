using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Prompts;

namespace PowerBIModelingMCP.Library.Services;

public class PromptRegistrationService
{
	private readonly MCPServerConfiguration _config;

	private readonly ILogger<PromptRegistrationService> _logger;

	public PromptRegistrationService(MCPServerConfiguration config, ILogger<PromptRegistrationService> logger)
	{
		_config = config;
		_logger = logger;
	}

	public void RegisterPrompts(IMcpServerBuilder mcpBuilder)
	{
		_logger.LogInformation("=== Prompt Registration Started ===");
		PromptsConfiguration prompts = _config.Prompts;
		_logger.LogInformation("Registering prompts...");
		if (prompts.EnableConnectionPrompts)
		{
			_logger.LogInformation("Loading common user scenario prompts...");
			mcpBuilder.WithPrompts<ConnectionPrompts>();
		}
		if (prompts.EnableDaxQueryPrompts)
		{
			_logger.LogInformation("Loading DAX query prompts...");
			mcpBuilder.WithPrompts<DaxQueryPrompts>();
		}
		_logger.LogInformation("=== Prompt Registration Completed ===");
	}
}
