using System.IO;

namespace PowerBIModelingMCP.Library.Contracts;

public class DaxQueryConfiguration
{
	public int MaxRowLimit { get; set; } = 100;

	public int DefaultRowLimit { get; set; } = 10;

	public string CsvExportFolder { get; set; } = Path.Combine(Path.GetTempPath(), "PowerBIModelingMCP", "QueryResults");

	public bool EnableCsvExport { get; set; } = true;
}
