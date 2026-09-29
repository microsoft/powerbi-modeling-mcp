using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PowerBIModelingMCP.Library.Services;

public class MarkdownResourceParser
{
	private static readonly Regex YamlFrontMatterRegex = new Regex("^---\\s*\\n(.*?)\\n---\\s*$", RegexOptions.Multiline | RegexOptions.Singleline);

	private readonly IDeserializer _yamlDeserializer;

	public MarkdownResourceParser()
	{
		_yamlDeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
	}

	public async Task<ParsedResourceDefinition> ParseFileAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException("Resource file not found:" + filePath);
		}
		return ParseContent(await File.ReadAllTextAsync(filePath), Path.GetFileNameWithoutExtension(filePath));
	}

	public ParsedResourceDefinition ParseContent(string content, string fileName)
	{
		Match match = YamlFrontMatterRegex.Match(content);
		if (!match.Success)
		{
			throw new InvalidOperationException("No YAML frontmatter found in markdown content");
		}
		string value = match.Groups[1].Value;
		ResourceMetadata resourceMetadata = _yamlDeserializer.Deserialize<ResourceMetadata>(value);
		int startIndex = match.Index + match.Length;
		string text = content.Substring(startIndex).Trim();
		string name = ((!string.IsNullOrWhiteSpace(resourceMetadata.Name)) ? resourceMetadata.Name : fileName);
		return new ParsedResourceDefinition
		{
			Name = name,
			Description = (resourceMetadata.Description ?? string.Empty),
			UriTemplate = (resourceMetadata.UriTemplate ?? string.Empty),
			Text = (text ?? string.Empty)
		};
	}

	public async Task<IEnumerable<ParsedResourceDefinition>> ParseDirectoryAsync(string directoryPath)
	{
		if (!Directory.Exists(directoryPath))
		{
			throw new DirectoryNotFoundException("Directory not found: " + directoryPath);
		}
		List<ParsedResourceDefinition> resourceDefinitions = new List<ParsedResourceDefinition>();
		string[] files = Directory.GetFiles(directoryPath, "*.md", SearchOption.TopDirectoryOnly);
		string[] array = files;
		foreach (string filePath in array)
		{
			try
			{
				resourceDefinitions.Add(await ParseFileAsync(filePath));
			}
			catch (Exception)
			{
			}
		}
		return resourceDefinitions;
	}
}
