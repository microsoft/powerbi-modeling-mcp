using System;
using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common;

[AttributeUsage(AttributeTargets.Method)]
public sealed class YamlToolDescriptionAttribute : DescriptionAttribute
{
	public YamlToolDescriptionAttribute(string toolName)
		: base(ToolDescriptionProvider.GetToolDescription(toolName))
	{
	}
}
