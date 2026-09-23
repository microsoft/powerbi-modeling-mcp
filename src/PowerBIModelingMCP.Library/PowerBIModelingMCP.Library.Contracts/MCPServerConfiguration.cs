using System;

namespace PowerBIModelingMCP.Library.Contracts;

public class MCPServerConfiguration
{
	public ToolMode Mode { get; set; } = ToolMode.ReadWrite;

	public CompatibilityMode Compatibility { get; set; }

	public bool SkipConfirmation { get; set; } = true;

	public ToolsConfiguration Tools { get; set; } = new ToolsConfiguration();

	public PromptsConfiguration Prompts { get; set; } = new PromptsConfiguration();

	public ResourcesConfiguration Resources { get; set; } = new ResourcesConfiguration();

	public DaxQueryConfiguration DaxQuery { get; set; } = new DaxQueryConfiguration();

	public string ApplicationName { get; set; } = "MCP-PBIModeling";

	public string ProToolingValue { get; set; } = "MCP-PBIModeling";

	public void SetToolMode(ToolMode mode)
	{
		Mode = mode;
	}

	public void SetToolMode(string mode)
	{
		switch (mode?.ToLowerInvariant())
		{
		case "readonly":
		case "read-only":
			Mode = ToolMode.ReadOnly;
			break;
		case "readwrite":
		case "read-write":
			Mode = ToolMode.ReadWrite;
			break;
		default:
			throw new ArgumentException("Invalid tool mode '" + mode + "'. Supported modes: 'readonly', 'readwrite'.");
		}
	}

	public void SetCompatibilityMode(CompatibilityMode compatibility)
	{
		Compatibility = compatibility;
	}

	public void SetCompatibilityMode(string compatibility)
	{
		string text = compatibility?.ToLowerInvariant();
		if (!(text == "powerbi"))
		{
			if (!(text == "full"))
			{
				throw new ArgumentException("Invalid compatibility mode '" + compatibility + "'. Supported modes: 'powerbi', 'full'.");
			}
			Compatibility = CompatibilityMode.Full;
		}
		else
		{
			Compatibility = CompatibilityMode.PowerBI;
		}
	}

	public bool IsValid()
	{
		if (Enum.IsDefined(typeof(ToolMode), Mode))
		{
			return Enum.IsDefined(typeof(CompatibilityMode), Compatibility);
		}
		return false;
	}

	public string GetEnabledToolMode()
	{
		return Mode.ToString();
	}

	public string GetEnabledCompatibilityMode()
	{
		return Compatibility.ToString();
	}
}
