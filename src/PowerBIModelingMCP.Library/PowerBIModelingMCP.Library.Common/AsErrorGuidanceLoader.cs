using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Common;

public sealed class AsErrorGuidanceLoader
{
	private static readonly Lazy<AsErrorGuidanceLoader> Instance = new Lazy<AsErrorGuidanceLoader>(() => new AsErrorGuidanceLoader());

	private readonly Dictionary<int, ErrorGuidanceEntry> _errorCodeMap = new Dictionary<int, ErrorGuidanceEntry>();

	private bool _loaded;

	public static AsErrorGuidanceLoader Current => Instance.Value;

	public IReadOnlyDictionary<int, ErrorGuidanceEntry> ErrorCodes
	{
		get
		{
			EnsureLoaded();
			return _errorCodeMap;
		}
	}

	private AsErrorGuidanceLoader()
	{
	}

	public ErrorGuidanceEntry? FindByErrorCode(int errorCode)
	{
		EnsureLoaded();
		if (!_errorCodeMap.TryGetValue(errorCode, out ErrorGuidanceEntry value))
		{
			return null;
		}
		return value;
	}

	private void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}
		lock (_errorCodeMap)
		{
			if (_loaded)
			{
				return;
			}
			try
			{
				string text = ResolveXmlPath();
				if (text != null && File.Exists(text))
				{
					LoadFromFile(text);
				}
			}
			catch (Exception)
			{
			}
			_loaded = true;
		}
	}

	private static string? ResolveXmlPath()
	{
		string directoryName = Path.GetDirectoryName(typeof(AsErrorGuidanceLoader).Assembly.Location);
		if (directoryName != null)
		{
			string text = Path.Combine(directoryName, "AsErrorGuidance.xml");
			if (File.Exists(text))
			{
				return text;
			}
			text = Path.Combine(directoryName, "Common", "AsErrorGuidance.xml");
			if (File.Exists(text))
			{
				return text;
			}
		}
		string text2 = Path.Combine(Directory.GetCurrentDirectory(), "AsErrorGuidance.xml");
		if (File.Exists(text2))
		{
			return text2;
		}
		if (directoryName == null)
		{
			return null;
		}
		return Path.Combine(directoryName, "AsErrorGuidance.xml");
	}

	private void LoadFromFile(string path)
	{
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.Load(path);
		XmlElement documentElement = xmlDocument.DocumentElement;
		if (documentElement == null)
		{
			return;
		}
		foreach (XmlNode item in documentElement.SelectNodes("Error"))
		{
			string text = item.Attributes?["code"]?.Value;
			if (!string.IsNullOrWhiteSpace(text))
			{
				int result2;
				if (Enum.TryParse<AnalysisServicesErrorUtils.AnalysisServicesErrorCode>(text, out var result))
				{
					result2 = result.ToInt();
				}
				else if (!int.TryParse(text, out result2))
				{
					continue;
				}
				ErrorGuidanceEntry value = new ErrorGuidanceEntry
				{
					ErrorCode = result2,
					Name = text,
					Category = GetChildText(item, "Category"),
					Guidance = GetChildText(item, "Guidance"),
					DoNotDo = GetChildText(item, "DoNotDo")
				};
				_errorCodeMap[result2] = value;
			}
		}
	}

	private static string GetChildText(XmlNode parent, string childName)
	{
		return parent.SelectSingleNode(childName)?.InnerText?.Trim() ?? string.Empty;
	}

	internal void Reload()
	{
		lock (_errorCodeMap)
		{
			_errorCodeMap.Clear();
			_loaded = false;
		}
		EnsureLoaded();
	}
}
