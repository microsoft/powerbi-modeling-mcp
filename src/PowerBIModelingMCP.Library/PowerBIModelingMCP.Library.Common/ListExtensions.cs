using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common;

public static class ListExtensions
{
	public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
	{
		if (items == null)
		{
			return;
		}
		if (list is List<T> list2)
		{
			list2.AddRange(items);
			return;
		}
		foreach (T item in items)
		{
			list.Add(item);
		}
	}
}
