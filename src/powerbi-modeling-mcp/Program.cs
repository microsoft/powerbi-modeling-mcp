using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using PowerBIModelingMCP.Console;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;
using PowerBIModelingMCP.Library.Services;

[CompilerGenerated]
internal class Program
{
	private static async Task Main(string[] args)
	{
		Assembly asm = Assembly.GetEntryAssembly();
		string version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "Unknown";
		string value = args.FirstOrDefault((string arg) => arg.StartsWith("--start", StringComparison.OrdinalIgnoreCase));
		string value2 = args.FirstOrDefault((string arg) => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase));
		string? value3 = args.FirstOrDefault((string arg) => arg.Equals("--version", StringComparison.OrdinalIgnoreCase));
		string value4 = args.FirstOrDefault((string arg) => arg.Equals("--read-only", StringComparison.OrdinalIgnoreCase) || arg.Equals("--readonly", StringComparison.OrdinalIgnoreCase));
		string value5 = args.FirstOrDefault((string arg) => arg.Equals("--read-write", StringComparison.OrdinalIgnoreCase) || arg.Equals("--readwrite", StringComparison.OrdinalIgnoreCase));
		string text = args.FirstOrDefault((string arg) => arg.StartsWith("--compatibility=", StringComparison.OrdinalIgnoreCase));
		string value6 = args.FirstOrDefault((string arg) => arg.Equals("--require-confirmation", StringComparison.OrdinalIgnoreCase) || arg.Equals("--requireconfirmation", StringComparison.OrdinalIgnoreCase));
		string text2 = args.FirstOrDefault((string arg) => arg.StartsWith("--authmode=", StringComparison.OrdinalIgnoreCase));
		bool flag = !string.IsNullOrEmpty(value) || !string.IsNullOrEmpty(value4) || !string.IsNullOrEmpty(value5);
		bool flag2 = !string.IsNullOrEmpty(value2);
		bool num = !string.IsNullOrEmpty(value3);
		string modeOverride = null;
		if (!string.IsNullOrEmpty(value4))
		{
			modeOverride = "readonly";
		}
		else if (!string.IsNullOrEmpty(value5))
		{
			modeOverride = "readwrite";
		}
		string compatibilityOverride = null;
		if (!string.IsNullOrEmpty(text))
		{
			compatibilityOverride = text.Substring("--compatibility=".Length);
		}
		bool skipConfirmationEnabled = string.IsNullOrEmpty(value6);
		string authModeOverride = null;
		if (!string.IsNullOrEmpty(text2))
		{
			authModeOverride = text2.Substring("--authmode=".Length);
		}
		if (num)
		{
			Console.WriteLine(version);
			return;
		}
		if (flag2)
		{
			PrintUsage();
			return;
		}
		if (!flag)
		{
			PrintWelcomeInfo();
			return;
		}
		MCPServerConfiguration config = CreateConfigurationFromArgs(modeOverride, compatibilityOverride, skipConfirmationEnabled);
		await StartStdioServer(args, config, authModeOverride);
		void ConfigureBuilder(IHostApplicationBuilder builder, MCPServerConfiguration mCPServerConfiguration, string? cliAuthMode)
		{
			using (Stream stream = asm.GetManifestResourceStream("appsettings.json"))
			{
				if (stream == null)
				{
					WriteToConsole("Unable to load configuration settings from executable.", ConsoleColor.Red);
					Environment.Exit(1);
				}
				builder.Configuration.AddJsonStream(stream);
			}
			builder.Configuration.AddEnvironmentVariables();
			IConfigurationSection section = builder.Configuration.GetSection("ModelingClient");
			builder.Services.Configure<ModelingClientConfig>(section);
			ModelingClientConfig modelingClientConfig = new ModelingClientConfig();
			section.Bind(modelingClientConfig);
			AuthService.Initialize(AuthServiceFactory.CreateAuthService(modelingClientConfig, cliAuthMode));
			ConnectionOperations.Initialize(new ConnectionOperationsService(modelingClientConfig, mCPServerConfiguration));
			builder.Services.AddSingleton(mCPServerConfiguration);
			builder.Services.AddSingleton<MarkdownResourceParser>();
			builder.Services.AddSingleton<ToolRegistrationService>();
			builder.Services.AddSingleton<PromptRegistrationService>();
			builder.Services.AddSingleton<ResourceRegistrationService>();
			builder.Services.AddSingleton((IWriteGuard)new WriteGuard(mCPServerConfiguration));
			builder.Services.AddSingleton((IEnhancedRefreshService)new EnhancedRefreshService(modelingClientConfig));
			builder.Services.AddSingleton((IServiceProvider sp) => new RetryHelper(sp.GetRequiredService<ILoggerFactory>().CreateLogger<RetryHelper>()));
			builder.Logging.AddConsole(delegate(ConsoleLoggerOptions consoleLogOptions)
			{
				consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
			});
			builder.Logging.AddEventSourceLogger();
			builder.Logging.SetMinimumLevel(LogLevel.Information);
		}
		static MCPServerConfiguration CreateConfigurationFromArgs(string? text3, string? text4, bool skipConfirmation = true)
		{
			MCPServerConfiguration mCPServerConfiguration = new MCPServerConfiguration
			{
				Mode = ToolMode.ReadWrite,
				Compatibility = CompatibilityMode.PowerBI,
				SkipConfirmation = skipConfirmation,
				Tools = new ToolsConfiguration
				{
					EnableDatabaseOperationsTool = true,
					EnableTableOperationsTool = true,
					EnableColumnOperationsTool = true,
					EnableMeasureOperationsTool = true,
					EnableBatchMeasureOperationsTool = true,
					EnableBatchColumnOperationsTool = true,
					EnableBatchTableOperationsTool = true,
					EnableCalculationGroupOperationsTool = true,
					EnableCalendarOperationsTool = true,
					EnableQueryGroupOperationsTool = true,
					EnableRelationshipOperationsTool = true,
					EnableDataSourceOperationsTool = true,
					EnablePartitionOperationsTool = true,
					EnableSecurityRoleOperationsTool = true,
					EnableUserHierarchyOperationsTool = true,
					EnableCultureOperationsTool = true,
					EnableModelOperationsTool = true,
					EnableNamedExpressionOperationsTool = true,
					EnableFunctionOperationsTool = true,
					EnableObjectTranslationOperationsTool = true,
					EnablePerspectiveOperationsTool = true,
					EnableConnectionOperationsTool = true,
					EnableDaxQueryOperationsTool = true,
					EnableTransactionOperationsTool = true,
					EnableFabricOperationsTool = false
				}
			};
			if (!string.IsNullOrEmpty(text3))
			{
				try
				{
					mCPServerConfiguration.SetToolMode(text3);
				}
				catch (ArgumentException ex)
				{
					Console.Error.WriteLine("Invalid mode argument '" + text3 + "': " + ex.Message);
					Environment.Exit(1);
				}
			}
			if (!string.IsNullOrEmpty(text4))
			{
				try
				{
					mCPServerConfiguration.SetCompatibilityMode(text4);
				}
				catch (ArgumentException ex2)
				{
					Console.Error.WriteLine("Invalid compatibility argument '" + text4 + "': " + ex2.Message);
					Environment.Exit(1);
				}
			}
			return mCPServerConfiguration;
		}
		static dynamic GetMCPRegistrationAsJson(bool readOnly = false)
		{
			string? processPath = Environment.ProcessPath;
			if (string.IsNullOrEmpty(processPath))
			{
				throw new Exception("Unable to determine the application path. This may occur when running from certain environments. Try running the executable directly or check your environment configuration.");
			}
			List<string> args2 = new List<string> { "--start" };
			return new
			{
				command = processPath,
				args = args2,
				env = new { }
			};
		}
		static void LogConfigurationSettings(ILogger logger, MCPServerConfiguration mCPServerConfiguration, string text3)
		{
			logger.LogInformation("=== Final Configuration Settings ===");
			logger.LogInformation("Version: {Version}", text3);
			logger.LogInformation("Tool Configuration:");
			logger.LogInformation("  Mode: {Mode} (Source: Command Line)", mCPServerConfiguration.Mode);
			logger.LogInformation("  Compatibility: {Compatibility} (Source: Command Line)", mCPServerConfiguration.Compatibility);
			logger.LogInformation("  Skip Confirmation: {SkipConfirmation}", mCPServerConfiguration.SkipConfirmation ? "Enabled" : "Disabled");
			logger.LogInformation("========================================");
		}
		static void PrintUsage()
		{
			WriteToConsole("Semantic Model MCP Server", ConsoleColor.Yellow);
			WriteToConsole("", ConsoleColor.White);
			WriteToConsole("Usage:", ConsoleColor.Cyan);
			WriteToConsole("  --start                      Start the MCP server (uses default ReadWrite mode)", ConsoleColor.White);
			WriteToConsole("  --read-only                  Start in read-only mode", ConsoleColor.White);
			WriteToConsole("  --readonly                   Start in read-only mode (alias)", ConsoleColor.White);
			WriteToConsole("  --read-write                 Start in read-write mode", ConsoleColor.White);
			WriteToConsole("  --readwrite                  Start in read-write mode (alias)", ConsoleColor.White);
			WriteToConsole("  --require-confirmation       Require confirmation prompts for write operations (default: skipped)", ConsoleColor.White);
			WriteToConsole("  --authmode=<mode>            Set authentication mode (serviceprincipal, interactive, managedidentity, defaultazurecredential, azurecli)", ConsoleColor.White);
			WriteToConsole("  --compatibility=powerbi      PowerBI only compatibility (default)", ConsoleColor.White);
			WriteToConsole("  --compatibility=full         Full compatibility (PowerBI + Analysis Services)", ConsoleColor.White);
			WriteToConsole("  --version                    Show version information", ConsoleColor.White);
			WriteToConsole("  --help, -h                   Show this help message", ConsoleColor.White);
			WriteToConsole("", ConsoleColor.White);
			WriteToConsole("Examples:", ConsoleColor.Cyan);
			WriteToConsole("  powerbi-modeling-mcp --start", ConsoleColor.White);
			WriteToConsole("  powerbi-modeling-mcp --readonly", ConsoleColor.White);
			WriteToConsole("  powerbi-modeling-mcp --readwrite", ConsoleColor.White);
			WriteToConsole("  powerbi-modeling-mcp --readwrite --require-confirmation", ConsoleColor.White);
			WriteToConsole("  powerbi-modeling-mcp --start --compatibility=powerbi", ConsoleColor.White);
			WriteToConsole("  powerbi-modeling-mcp --readwrite --compatibility=full", ConsoleColor.White);
			WriteToConsole("  powerbi-modeling-mcp --start --authmode=serviceprincipal", ConsoleColor.White);
			WriteToConsole("  powerbi-modeling-mcp --readwrite --authmode=interactive", ConsoleColor.White);
			WriteToConsole("  powerbi-modeling-mcp --start --authmode=managedidentity", ConsoleColor.White);
		}
		void PrintWelcomeInfo()
		{
			WriteToConsole("\n ____                          ____ ___   __  __  ____ ____  \n|  _ \\ _____      _____ _ __  | __ )_ _| |  \\/  |/ ___|  _ \\ \n| |_) / _ \\ \\ /\\ / / _ \\ '__| |  _ \\| |  | |\\/| | |   | |_) |\n|  __/ (_) \\ V  V /  __/ |    | |_) | |  | |  | | |___|  __/ \n|_|   \\___/ \\_/\\_/ \\___|_|    |____/___| |_|  |_|\\____|_|    \n    ", ConsoleColor.Yellow);
			WriteToConsole("Version: " + version, ConsoleColor.Gray);
			string text3 = "powerbi-modeling-mcp";
			string text4 = JsonSerializer.Serialize(new
			{
				servers = new Dictionary<string, object> { [text3] = (object)GetMCPRegistrationAsJson() }
			}, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			WriteToConsole("MCP configuration (for manual registration):", ConsoleColor.Cyan, newLine: true, 1);
			WriteToConsole(text4, ConsoleColor.Yellow);
			WriteToConsole("Visual Studio Code installation (CTRL + Click to open):", ConsoleColor.Cyan, newLine: true, 1);
			dynamic val = JsonSerializer.Serialize(GetMCPRegistrationAsJson());
			WriteToConsole($"https://vscode.dev/redirect/mcp/install?name={text3}&config={(object?)Uri.EscapeDataString(val)}", ConsoleColor.Blue);
			WriteToConsole("Warning: ", ConsoleColor.Yellow, newLine: false, 1);
			WriteToConsole("Please use caution when working with this MCP server. It’s recommended to back up your semantic model, as AI interactions with this MCP may produce unexpected results.", ConsoleColor.Gray, newLine: false);
			WriteToConsole("More information: ", ConsoleColor.Cyan, newLine: false, 2);
			WriteToConsole("https://github.com/microsoft/powerbi-modeling-mcp", ConsoleColor.Blue);
			WriteToConsole("Press any key to close...", ConsoleColor.Gray, newLine: true, 1);
			Console.ReadKey(intercept: true);
		}
		static void RegisterMcpComponents(IServiceCollection services, IMcpServerBuilder mcpBuilder)
		{
			ServiceProvider provider = services.BuildServiceProvider();
			ToolDescriptionProvider.ConfigureLogger(provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ToolDescriptionProvider).FullName ?? "ToolDescriptionProvider"));
			provider.GetRequiredService<ToolRegistrationService>().RegisterTools(mcpBuilder);
			provider.GetRequiredService<PromptRegistrationService>().RegisterPrompts(mcpBuilder);
			provider.GetRequiredService<ResourceRegistrationService>().RegisterResources(mcpBuilder);
			RetryHelper.ConfigureDefault(provider.GetRequiredService<RetryHelper>());
		}
		async Task StartStdioServer(string[] args2, MCPServerConfiguration mCPServerConfiguration, string? authModeOverride2)
		{
			HostApplicationBuilder hostApplicationBuilder = Host.CreateApplicationBuilder(args2);
			ConfigureBuilder(hostApplicationBuilder, mCPServerConfiguration, authModeOverride2);
			IMcpServerBuilder mcpBuilder = hostApplicationBuilder.Services.AddMcpServer().WithStdioServerTransport();
			RegisterMcpComponents(hostApplicationBuilder.Services, mcpBuilder);
			ServiceProvider provider = hostApplicationBuilder.Services.BuildServiceProvider();
			ILogger<Program> logger = provider.GetRequiredService<ILogger<Program>>();
			logger.LogInformation("Server starting: Version={Version}, Mode={Mode}, Compatibility={Compatibility}, Transport=stdio", version, mCPServerConfiguration.Mode, mCPServerConfiguration.Compatibility);
			LogConfigurationSettings(logger, mCPServerConfiguration, version);
			await hostApplicationBuilder.Build().RunAsync();
			logger.LogInformation("Server stopped");
		}
		static void WriteToConsole(string value7, ConsoleColor color, bool newLine = true, int emptyLines = 0)
		{
			if (emptyLines > 0)
			{
				for (int i = 0; i < emptyLines; i++)
				{
					Console.WriteLine();
				}
			}
			Console.ForegroundColor = color;
			if (newLine)
			{
				Console.WriteLine(value7);
			}
			else
			{
				Console.Write(value7);
			}
			Console.ResetColor();
		}
	}
}
