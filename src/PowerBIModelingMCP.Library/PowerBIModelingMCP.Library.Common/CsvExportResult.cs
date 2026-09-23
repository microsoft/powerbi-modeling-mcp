namespace PowerBIModelingMCP.Library.Common;

public class CsvExportResult
{
	public string FilePath { get; set; } = string.Empty;

	public int RowsWritten { get; set; }

	public bool WasTruncated { get; set; }

	public string? TruncationReason { get; set; }
}
