using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AnalysisServices.Tabular;

namespace PowerBIModelingMCP.Library.Common;

public static class ObjectImpactSerializer
{
	public static string? SerializeToString(ObjectImpact? impact)
	{
		if (impact == null)
		{
			return null;
		}
		List<string> list = new List<string>();
		if (impact.PropertyChanges != null && impact.PropertyChanges.Any())
		{
			IEnumerable<string> values = impact.PropertyChanges.Select((PropertyChangeEntry change) => "  - " + SerializePropertyChange(change));
			list.Add("Property Changes:\n" + string.Join("\n", values));
		}
		if (impact.RemovedObjects != null && impact.RemovedObjects.Any())
		{
			IEnumerable<string> values2 = impact.RemovedObjects.Select((MetadataObject obj) => "  - " + SerializeMetadataObject(obj));
			list.Add("Removed Objects:\n" + string.Join("\n", values2));
		}
		if (impact.RemovedSubtreeRoots != null && impact.RemovedSubtreeRoots.Any())
		{
			IEnumerable<string> values3 = impact.RemovedSubtreeRoots.Select((RemovedSubtreeEntry root) => "  - " + SerializeRemovedSubtreeEntry(root));
			list.Add("Removed Subtree Roots:\n" + string.Join("\n", values3));
		}
		if (!list.Any())
		{
			return null;
		}
		return string.Join("\n\n", list);
	}

	private static string SerializePropertyChange(PropertyChangeEntry change)
	{
		if (change == null)
		{
			return "null";
		}
		StringBuilder stringBuilder2;
		StringBuilder stringBuilder = (stringBuilder2 = new StringBuilder());
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(23, 2, stringBuilder2);
		handler.AppendLiteral("Property '");
		handler.AppendFormatted(change.PropertyName);
		handler.AppendLiteral("' changed on ");
		handler.AppendFormatted(SerializeMetadataObject(change.Object));
		stringBuilder2.Append(ref handler);
		return stringBuilder.ToString();
	}

	private static string SerializeRemovedSubtreeEntry(RemovedSubtreeEntry entry)
	{
		if (entry == null)
		{
			return "null";
		}
		StringBuilder stringBuilder = new StringBuilder();
		PropertyInfo property = entry.GetType().GetProperty("Object");
		if (property != null)
		{
			object value = property.GetValue(entry);
			if (value != null)
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder3 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
				handler.AppendLiteral("Removed subtree for ");
				handler.AppendFormatted(SerializeMetadataObject(value as MetadataObject));
				stringBuilder3.Append(ref handler);
				return stringBuilder.ToString();
			}
		}
		PropertyInfo property2 = entry.GetType().GetProperty("ID");
		if (property2 != null)
		{
			string value2 = property2.GetValue(entry)?.ToString();
			if (!string.IsNullOrEmpty(value2))
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(22, 1, stringBuilder2);
				handler.AppendLiteral("Removed subtree (ID: ");
				handler.AppendFormatted(value2);
				handler.AppendLiteral(")");
				stringBuilder4.Append(ref handler);
				return stringBuilder.ToString();
			}
		}
		return "Removed subtree entry";
	}

	private static string SerializeMetadataObject(MetadataObject? obj)
	{
		if (obj == null)
		{
			return "null";
		}
		StringBuilder stringBuilder = new StringBuilder(obj.GetType().Name ?? "");
		PropertyInfo property = obj.GetType().GetProperty("Name");
		if (property != null)
		{
			string value = property.GetValue(obj)?.ToString();
			if (!string.IsNullOrEmpty(value))
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder3 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder2);
				handler.AppendLiteral(" '");
				handler.AppendFormatted(value);
				handler.AppendLiteral("'");
				stringBuilder3.Append(ref handler);
			}
		}
		PropertyInfo property2 = obj.GetType().GetProperty("ID");
		if (property2 != null)
		{
			string value2 = property2.GetValue(obj)?.ToString();
			if (!string.IsNullOrEmpty(value2))
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder2);
				handler.AppendLiteral(" (ID: ");
				handler.AppendFormatted(value2);
				handler.AppendLiteral(")");
				stringBuilder4.Append(ref handler);
			}
		}
		if (!(obj is Table table))
		{
			if (!(obj is Column column))
			{
				if (!(obj is Measure measure))
				{
					if (!(obj is Relationship relationship))
					{
						if (!(obj is Partition partition))
						{
							if (obj is DataSource dataSource)
							{
								StringBuilder stringBuilder2 = stringBuilder;
								StringBuilder stringBuilder5 = stringBuilder2;
								StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
								handler.AppendLiteral(" [Type: ");
								handler.AppendFormatted(dataSource.Type);
								handler.AppendLiteral("]");
								stringBuilder5.Append(ref handler);
							}
						}
						else
						{
							StringBuilder stringBuilder2 = stringBuilder;
							StringBuilder stringBuilder6 = stringBuilder2;
							StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(18, 2, stringBuilder2);
							handler.AppendLiteral(" [Table: ");
							handler.AppendFormatted(partition.Table?.Name ?? "Unknown");
							handler.AppendLiteral(", Type: ");
							handler.AppendFormatted(partition.SourceType);
							handler.AppendLiteral("]");
							stringBuilder6.Append(ref handler);
						}
					}
					else
					{
						StringBuilder stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder7 = stringBuilder2;
						StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(14, 2, stringBuilder2);
						handler.AppendLiteral(" [From: ");
						handler.AppendFormatted(relationship.FromTable?.Name ?? "Unknown");
						handler.AppendLiteral(" To: ");
						handler.AppendFormatted(relationship.ToTable?.Name ?? "Unknown");
						handler.AppendLiteral("]");
						stringBuilder7.Append(ref handler);
					}
				}
				else
				{
					StringBuilder stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder8 = stringBuilder2;
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
					handler.AppendLiteral(" [Table: ");
					handler.AppendFormatted(measure.Table?.Name ?? "Unknown");
					handler.AppendLiteral("]");
					stringBuilder8.Append(ref handler);
				}
			}
			else
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder9 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(18, 2, stringBuilder2);
				handler.AppendLiteral(" [Type: ");
				handler.AppendFormatted(column.DataType);
				handler.AppendLiteral(", Table: ");
				handler.AppendFormatted(column.Table?.Name ?? "Unknown");
				handler.AppendLiteral("]");
				stringBuilder9.Append(ref handler);
			}
		}
		else
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder10 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder2);
			handler.AppendLiteral(" [Columns: ");
			handler.AppendFormatted(table.Columns?.Count ?? 0);
			handler.AppendLiteral(", Measures: ");
			handler.AppendFormatted(table.Measures?.Count ?? 0);
			handler.AppendLiteral("]");
			stringBuilder10.Append(ref handler);
		}
		return stringBuilder.ToString();
	}
}
