using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YamlDotNet.RepresentationModel;

namespace PowerBIModelingMCP.Library.Common;

public static class ToolDescriptionProvider
{
	private sealed record ToolDescriptionStore(IReadOnlyDictionary<string, string> Descriptions, string Sources);

	private const string MetadataDirectoryRelativePath = "Resources/tools";

	private const string DefaultMetadataFileName = "tool-metadata.yaml";

	private const string OverrideMetadataFileName = "tool-metadata.override.yaml";

	private static readonly object _metadataLock = new object();

	private static volatile ILogger _logger = NullLogger.Instance;

	private static ToolDescriptionStore? _metadata;

	public static void ConfigureLogger(ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(logger, "logger");
		_logger = logger;
	}

	public static string GetToolDescription(string toolName)
	{
		return GetRequiredDescription("tools." + toolName + ".description");
	}

	public static string GetRequestFieldDescription(string toolName, string fieldName)
	{
		return GetRequiredDescription("tools." + toolName + ".request." + fieldName);
	}

	internal static string GetToolDescription(string toolName, string baseDirectory)
	{
		return GetRequiredDescription("tools." + toolName + ".description", LoadMetadata(baseDirectory));
	}

	internal static string GetRequestFieldDescription(string toolName, string fieldName, string baseDirectory)
	{
		return GetRequiredDescription("tools." + toolName + ".request." + fieldName, LoadMetadata(baseDirectory));
	}

	private static string GetRequiredDescription(string key)
	{
		return GetRequiredDescription(key, GetMetadata());
	}

	private static string GetRequiredDescription(string key, ToolDescriptionStore store)
	{
		if (store.Descriptions.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		throw new InvalidOperationException($"Tool description metadata key '{key}' was not found in '{store.Sources}'.");
	}

	private static ToolDescriptionStore LoadMetadata()
	{
		return LoadMetadata(AppContext.BaseDirectory);
	}

	private static ToolDescriptionStore GetMetadata()
	{
		if (_metadata != null)
		{
			return _metadata;
		}
		lock (_metadataLock)
		{
			return _metadata ?? (_metadata = LoadMetadata());
		}
	}

	private static ToolDescriptionStore LoadMetadata(string baseDirectory)
	{
		Dictionary<string, string> descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
		List<string> list = new List<string>();
		LoadEmbeddedDefaultMetadata(descriptions, list);
		string metadataPath = GetMetadataPath(baseDirectory, "tool-metadata.yaml");
		if (File.Exists(metadataPath))
		{
			MergeYamlFile(metadataPath, descriptions, list, skipEmptyValues: false);
		}
		string metadataPath2 = GetMetadataPath(baseDirectory, "tool-metadata.override.yaml");
		if (File.Exists(metadataPath2))
		{
			MergeYamlFile(metadataPath2, descriptions, list, skipEmptyValues: true);
		}
		return new ToolDescriptionStore(descriptions, string.Join("; ", list));
	}

	private static string GetMetadataPath(string baseDirectory, string fileName)
	{
		return Path.Combine(baseDirectory, "Resources/tools".Replace('/', Path.DirectorySeparatorChar), fileName);
	}

	private static void MergeYamlFile(string filePath, Dictionary<string, string> descriptions, List<string> sources, bool skipEmptyValues)
	{
		if (File.Exists(filePath))
		{
			using (StreamReader reader = File.OpenText(filePath))
			{
				MergeDescriptions(ParseYaml(reader), descriptions, skipEmptyValues);
				sources.Add(filePath);
			}
		}
	}

	private static void LoadEmbeddedDefaultMetadata(Dictionary<string, string> descriptions, List<string> sources)
	{
		Assembly assembly = typeof(ToolDescriptionProvider).Assembly;
		string text = assembly.GetManifestResourceNames().SingleOrDefault((string name) => name.EndsWith(".Resources.tools.tool-metadata.yaml", StringComparison.Ordinal));
		if (text == null)
		{
			throw new FileNotFoundException("Embedded tool description metadata resource ending with '.Resources.tools.tool-metadata.yaml' was not found.");
		}
		using Stream stream = assembly.GetManifestResourceStream(text) ?? throw new FileNotFoundException("Embedded tool description metadata resource '" + text + "' was not found.");
		using StreamReader reader = new StreamReader(stream);
		MergeDescriptions(ParseYaml(reader), descriptions, skipEmptyValues: false);
		sources.Add("embedded resource '" + text + "'");
	}

	private static void MergeDescriptions(IReadOnlyDictionary<string, string> source, Dictionary<string, string> destination, bool skipEmptyValues)
	{
		foreach (var (key, value) in source)
		{
			if (!skipEmptyValues || !string.IsNullOrWhiteSpace(value))
			{
				destination[key] = value;
			}
		}
	}

	private static Dictionary<string, string> ParseYaml(TextReader reader)
	{
		YamlStream yamlStream = new YamlStream();
		yamlStream.Load(reader);
		if (yamlStream.Documents.Count == 0 || !(yamlStream.Documents[0].RootNode is YamlMappingNode node))
		{
			throw new InvalidOperationException("Tool description metadata YAML must contain a mapping document.");
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		AddDescriptions(node, null, dictionary);
		return dictionary;
	}

	private static void AddDescriptions(YamlMappingNode node, string? prefix, Dictionary<string, string> descriptions)
	{
		foreach (var (yamlNode3, yamlNode4) in node.Children)
		{
			if (yamlNode3 is YamlScalarNode yamlScalarNode)
			{
				string value = yamlScalarNode.Value;
				if (value != null)
				{
					string text = ((prefix == null) ? value : (prefix + "." + value));
					if (!(yamlNode4 is YamlScalarNode yamlScalarNode2))
					{
						if (yamlNode4 is YamlMappingNode node2)
						{
							AddDescriptions(node2, text, descriptions);
							continue;
						}
						_logger.LogWarning("Tool description metadata YAML contains an unsupported node type '{NodeType}' at '{Path}'. Only scalar and mapping nodes are supported. Skipping this entry.", yamlNode4.GetType().Name, text);
					}
					else
					{
						descriptions[text] = yamlScalarNode2.Value ?? string.Empty;
						if (value == "description" && prefix != null)
						{
							descriptions[prefix] = yamlScalarNode2.Value ?? string.Empty;
						}
					}
					continue;
				}
			}
			_logger.LogWarning("Tool description metadata YAML contains a non-scalar key under '{Prefix}'. Only scalar keys are supported. Skipping this entry.", prefix ?? string.Empty);
		}
	}
}
