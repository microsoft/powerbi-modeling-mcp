using System;
using System.ComponentModel;
using System.IO;
using ModelContextProtocol.Server;

namespace PowerBIModelingMCP.Library.Services;

[McpServerResourceType]
public class ResourceLoader
{
	private static string LoadResource(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
		{
			throw new ArgumentNullException("fileName");
		}
		string text = Path.Combine(Path.Combine(AppContext.BaseDirectory, "Resources"), fileName);
		if (!File.Exists(text))
		{
			return "Resource file not found: " + text;
		}
		return File.ReadAllText(text);
	}

	[McpServerResource(Name = "DAX Query Instructions and Examples", UriTemplate = "resource://dax_query_instructions_and_examples", MimeType = "text/plain")]
	[Description("Guidelines for writing Power BI DAX queries")]
	public string dax_query_instructions_and_examples()
	{
		return LoadResource("dax_query_instructions_and_examples.md");
	}

	[McpServerResource(Name = "DAX UDF Instructions and Examples", UriTemplate = "resource://dax_udf_instructions_and_examples", MimeType = "text/plain")]
	[Description("Guidelines for creating Power BI DAX user-defined functions (UDFs)")]
	public string dax_udf_instructions_and_examples()
	{
		return LoadResource("dax_udf_instructions_and_examples.md");
	}

	[McpServerResource(Name = "Calendar Instructions and Examples", UriTemplate = "resource://calendar_instructions_and_examples", MimeType = "text/plain")]
	[Description("Guidelines for creating Power BI calendar objects")]
	public string calendar_instructions_and_examples()
	{
		return LoadResource("calendar_instructions_and_examples.md");
	}

	[McpServerResource(Name = "PowerBI Project Instructions", UriTemplate = "resource://powerbi_project_instructions", MimeType = "text/plain")]
	[Description("Instructions for structuring Power BI projects")]
	public string powerbi_project_instructions()
	{
		return LoadResource("powerbi_project_instructions.md");
	}
}
