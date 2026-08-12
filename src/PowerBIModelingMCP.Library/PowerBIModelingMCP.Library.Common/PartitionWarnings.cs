using Microsoft.AnalysisServices.Tabular;

namespace PowerBIModelingMCP.Library.Common;

public static class PartitionWarnings
{
	public const string MExpressionSchemaSync = "The partition source was updated, but table column mappings were not. If source column names or data types changed, use column_operations in a follow-up call to update DataColumn.SourceColumn and, for DirectQuery tables, Column.SourceProviderType for each affected column.";

	public static bool IsMExpressionUpdate(PartitionSource? existingSource, bool expressionChanged)
	{
		if (!expressionChanged)
		{
			return false;
		}
		return existingSource is MPartitionSource;
	}
}
