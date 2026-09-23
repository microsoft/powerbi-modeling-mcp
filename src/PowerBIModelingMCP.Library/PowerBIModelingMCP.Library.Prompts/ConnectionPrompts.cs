using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace PowerBIModelingMCP.Library.Prompts;

[McpServerPromptType]
public class ConnectionPrompts
{
	[McpServerPrompt(Name = "ConnectToFabric")]
	[Description("Connects to a semantic model in a Fabric Workspace.")]
	public IEnumerable<ChatMessage> ConnectToFabric([Description("Name of the workspace to connect to")] string workspaceName, [Description("Name of semantic model within the fabric workspace")] string semanticModelName)
	{
		return new ChatMessage[1]
		{
			new ChatMessage(ChatRole.User, $"Connect to semantic model '{semanticModelName}' in Fabric workspace '{workspaceName}'.")
		};
	}

	[McpServerPrompt(Name = "ConnectToPowerBIDesktop")]
	[Description("Searches for the Power BI Desktop Analysis Services instance that matches the file name and connects to it.")]
	public IEnumerable<ChatMessage> ConnectToPowerBIDesktop([Description("Name of the Power BI Desktop file")] string name)
	{
		return new ChatMessage[1]
		{
			new ChatMessage(ChatRole.User, "Connect to Power BI Desktop with name '" + name + "'.")
		};
	}

	[McpServerPrompt(Name = "ConnectToPBIP")]
	[Description("Loads the TMDL definition from the semantic model in the Power BI Project (pbip) files.")]
	public IEnumerable<PromptMessage> ConnectToPowerBIProject([Description("Path to PowerBI Project semantic model definition")] string? pbipPath = null)
	{
		return new PromptMessage[2]
		{
			new PromptMessage
			{
				Role = Role.User,
				Content = new EmbeddedResourceBlock
				{
					Resource = new TextResourceContents
					{
						Text = string.Empty,
						Uri = "resource://powerbi_project_instructions"
					}
				}
			},
			new PromptMessage
			{
				Role = Role.User,
				Content = new TextContentBlock
				{
					Text = (string.IsNullOrEmpty(pbipPath) ? "Look for my semantic model within the workspace and open semantic model from PBIP folder" : ("Open semantic model from PBIP folder '" + pbipPath + "'."))
				}
			}
		};
	}
}
