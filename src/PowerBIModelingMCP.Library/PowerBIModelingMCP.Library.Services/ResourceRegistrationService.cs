using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PowerBIModelingMCP.Library.Common.Telemetry;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Services;

public class ResourceRegistrationService
{
	private readonly MarkdownResourceParser _parser;

	private readonly MCPServerConfiguration _config;

	private readonly ILogger<ResourceRegistrationService> _logger;

	private readonly Dictionary<string, ParsedResourceDefinition> _loadedResources = new Dictionary<string, ParsedResourceDefinition>();

	public ResourceRegistrationService(MarkdownResourceParser parser, MCPServerConfiguration config, ILogger<ResourceRegistrationService> logger)
	{
		_parser = parser;
		_config = config;
		_logger = logger;
	}

	public void RegisterResources(IMcpServerBuilder mcpBuilder)
	{
		_logger.LogInformation("=== Resource Registration Started ===");
		_ = _config.Resources;
		if (_config.Resources.EnableDynamicResourceLoading)
		{
			_logger.LogInformation("Loading resources dynamically...");
			LoadAndRegisterResourcesFromFolder(mcpBuilder).GetAwaiter().GetResult();
		}
		else
		{
			_logger.LogInformation("Registering static resources...");
			mcpBuilder.WithResources<ResourceLoader>();
		}
		_logger.LogInformation("=== Resource Registration Completed ===");
	}

	private async Task LoadAndRegisterResourcesFromFolder(IMcpServerBuilder mcpBuilder)
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "Resources");
			_logger.LogInformation("Loading markdown resources from: {Directory}", text);
			if (Directory.Exists(text))
			{
				await LoadResourcesFromDirectoryAsync(text);
				if (_loadedResources.Count > 0)
				{
					RegisterLoadedResources(mcpBuilder, _loadedResources);
					_logger.LogInformation("Registered {Count} markdown resources", _loadedResources.Count);
				}
			}
			else
			{
				_logger.LogWarning("Resources directory not found: {Directory}", text);
			}
		}
		catch (Exception exception)
		{
			_logger.LogOperationError("ResourceRegistrationService", "LoadResources", exception);
		}
	}

	private async Task LoadResourcesFromDirectoryAsync(string resourcesDirectory)
	{
		try
		{
			if (!Directory.Exists(resourcesDirectory))
			{
				_logger.LogWarning("Resources directory not found: " + resourcesDirectory);
				return;
			}
			List<ParsedResourceDefinition> list = (await _parser.ParseDirectoryAsync(resourcesDirectory)).ToList();
			if (list.Count == 0)
			{
				_logger.LogInformation("No resource files found in directory: " + resourcesDirectory);
				return;
			}
			_logger.LogInformation("Found {Count} resource definitions", list.Count);
			_loadedResources.Clear();
			foreach (ParsedResourceDefinition item in list)
			{
				_loadedResources[item.Name] = item;
				_logger.LogInformation("Loaded resource: {Name} - {Description}", item.Name, item.Description);
			}
			_logger.LogInformation("Successfully loaded {Count} resources from markdown files", _loadedResources.Count);
		}
		catch (Exception exception)
		{
			_logger.LogOperationError("ResourceRegistrationService", "LoadResourcesFromDirectory", exception);
		}
	}

	private void RegisterLoadedResources(IMcpServerBuilder mcpBuilder, IReadOnlyDictionary<string, ParsedResourceDefinition> resources)
	{
		List<McpServerResource> list = new List<McpServerResource>();
		foreach (KeyValuePair<string, ParsedResourceDefinition> resource in resources)
		{
			list.Add(CreateResource(resource.Value));
		}
		mcpBuilder.WithResources(list);
	}

	private McpServerResource CreateResource(ParsedResourceDefinition resourceDefinition)
	{
		return McpServerResource.Create((Func<string>)(() => resourceDefinition.Text), new McpServerResourceCreateOptions
		{
			Name = resourceDefinition.Name,
			Description = resourceDefinition.Description,
			UriTemplate = resourceDefinition.UriTemplate,
			MimeType = resourceDefinition.MimeType
		});
	}
}
