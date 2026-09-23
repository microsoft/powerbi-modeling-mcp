using System;
using System.Collections.Generic;
using System.Linq;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Console;

public static class AuthServiceFactory
{
	public enum AuthenticationMode
	{
		ServicePrincipal,
		InteractiveBrowser,
		ManagedIdentity,
		DefaultAzureCredential,
		AzureCli
	}

	public static AuthenticationMode DetectAuthMode(string? cliAuthMode = null)
	{
		string text = (cliAuthMode ?? GetEnv("PBI_MODELING_MCP_AUTH_MODE"))?.ToLowerInvariant();
		switch (text)
		{
		case "serviceprincipal":
			return AuthenticationMode.ServicePrincipal;
		case "interactive":
			return AuthenticationMode.InteractiveBrowser;
		case "managedidentity":
			return AuthenticationMode.ManagedIdentity;
		case "defaultazurecredential":
			return AuthenticationMode.DefaultAzureCredential;
		case "azurecli":
			return AuthenticationMode.AzureCli;
		case null:
		case "":
			return AuthenticationMode.InteractiveBrowser;
		default:
			throw new McpExceptionWithSource("Invalid authentication mode value: '" + text + "'. Valid values for --authmode CLI argument or PBI_MODELING_MCP_AUTH_MODE environment variable are: 'serviceprincipal', 'interactive', 'managedidentity', 'defaultazurecredential', 'azurecli'", "Invalid authentication mode value provided. Valid values for --authmode CLI argument or PBI_MODELING_MCP_AUTH_MODE environment variable are: 'serviceprincipal', 'interactive', 'managedidentity', 'defaultazurecredential', 'azurecli'.");
		}
	}

	public static IAuthService CreateAuthService(ModelingClientConfig config, string? cliAuthMode = null)
	{
		AuthenticationMode authenticationMode = DetectAuthMode(cliAuthMode);
		System.Console.Error.WriteLine($"[INFO] Authentication mode: {authenticationMode}");
		return authenticationMode switch
		{
			AuthenticationMode.ServicePrincipal => CreateServicePrincipalAuthService(), 
			AuthenticationMode.InteractiveBrowser => new InteractiveAuthService(config), 
			AuthenticationMode.ManagedIdentity => new ManagedIdentityAuthService(), 
			AuthenticationMode.DefaultAzureCredential => new DefaultAzureCredentialAuthService(), 
			AuthenticationMode.AzureCli => new AzureCliAuthService(), 
			_ => throw new McpExceptionWithSource($"Unsupported authentication mode: {authenticationMode}"), 
		};
	}

	private static IAuthService CreateServicePrincipalAuthService()
	{
		string? env = GetEnv("AZURE_CLIENT_ID");
		string env2 = GetEnv("AZURE_TENANT_ID");
		string env3 = GetEnv("AZURE_CLIENT_SECRET");
		string env4 = GetEnv("AZURE_CLIENT_CERTIFICATE_PATH");
		List<string> list = new List<string>();
		if (string.IsNullOrEmpty(env))
		{
			list.Add("AZURE_CLIENT_ID");
		}
		if (string.IsNullOrEmpty(env2))
		{
			list.Add("AZURE_TENANT_ID");
		}
		if (string.IsNullOrEmpty(env3) && string.IsNullOrEmpty(env4))
		{
			list.Add("AZURE_CLIENT_SECRET or AZURE_CLIENT_CERTIFICATE_PATH");
		}
		if (list.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Service principal authentication mode is enabled but required environment variables are missing:\n" + string.Join("\n", list.Select((string v) => "  - " + v)) + "\n\nUse 'interactive' mode to use interactive browser authentication instead.", ErrorSource.User);
		}
		return new ServicePrincipalAuthService();
	}

	private static string? GetEnv(string name)
	{
		return Environment.GetEnvironmentVariable(name);
	}
}
