using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AnalysisServices.Tabular;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public static class AnnotationHelpers
{
	public static void ValidateAnnotations(List<KeyValuePair<string, string>>? annotations, string? errorPrefix = null)
	{
		if (annotations == null)
		{
			return;
		}
		string text = (string.IsNullOrEmpty(errorPrefix) ? "" : (errorPrefix + " "));
		HashSet<string> hashSet = new HashSet<string>();
		foreach (KeyValuePair<string, string> annotation in annotations)
		{
			if (string.IsNullOrWhiteSpace(annotation.Key))
			{
				throw new McpExceptionWithSource(text + "annotation key cannot be null or empty", ErrorSource.User, "Annotation key is required.");
			}
			if (!hashSet.Add(annotation.Key))
			{
				throw new McpExceptionWithSource(text + "has duplicate annotation key: " + annotation.Key, ErrorSource.User, "Duplicate annotation key detected.");
			}
		}
	}

	public static void ApplyAnnotations<T>(T target, List<KeyValuePair<string, string>> annotations, Func<T, ICollection<Annotation>> annotationsAccessor)
	{
		if (annotations.Count == 0)
		{
			return;
		}
		ICollection<Annotation> collection = annotationsAccessor(target);
		foreach (KeyValuePair<string, string> annotation in annotations)
		{
			collection.Add(new Annotation
			{
				Name = annotation.Key,
				Value = annotation.Value
			});
		}
	}

	public static bool ReplaceAnnotations<T>(T target, List<KeyValuePair<string, string>> updates, Func<T, ICollection<Annotation>> annotationsAccessor)
	{
		ICollection<Annotation> collection = annotationsAccessor(target);
		if (updates.Count == 0)
		{
			bool num = collection.Count > 0;
			if (num)
			{
				collection.Clear();
			}
			return num;
		}
		bool result = false;
		Dictionary<string, string> updatesDict = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, string> update in updates)
		{
			updatesDict[update.Key] = update.Value;
		}
		List<Annotation> list = collection.Where((Annotation a) => !updatesDict.ContainsKey(a.Name)).ToList();
		if (list.Count > 0)
		{
			foreach (Annotation item in list)
			{
				collection.Remove(item);
			}
			result = true;
		}
		foreach (KeyValuePair<string, string> kv in updatesDict)
		{
			Annotation annotation = collection.FirstOrDefault((Annotation a) => a.Name == kv.Key);
			if (annotation == null)
			{
				collection.Add(new Annotation
				{
					Name = kv.Key,
					Value = kv.Value
				});
				result = true;
			}
			else if (annotation.Value != kv.Value)
			{
				annotation.Value = kv.Value;
				result = true;
			}
		}
		return result;
	}
}
