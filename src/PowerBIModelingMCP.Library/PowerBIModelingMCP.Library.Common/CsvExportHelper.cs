using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Common;

public static class CsvExportHelper
{
	private const long MaxFileSizeBytes = 4194304L;

	private const int CleanupAgeMinutes = 60;

	private const string Crlf = "\r\n";

	private static readonly HashSet<string> BinaryTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Binary", "Varbinary", "Image", "Geography", "Geometry" };

	public static CsvExportResult ExportToCsv(List<DaxColumnInfo> columns, List<Dictionary<string, object?>> rows, string exportFolder, int? maxRows = null)
	{
		string text = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
		string path = "dax_query_result_" + text + ".csv";
		string path2 = Path.Combine(exportFolder, path);
		int num = maxRows ?? int.MaxValue;
		Directory.CreateDirectory(exportFolder);
		CsvExportResult csvExportResult = new CsvExportResult
		{
			FilePath = Path.GetFullPath(path2)
		};
		using StreamWriter streamWriter = new StreamWriter(path2, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		streamWriter.NewLine = "\r\n";
		IEnumerable<string> source = columns.Select((DaxColumnInfo c) => (!IsBinaryType(c.DataType)) ? c.Name : (c.Name + "[base64]"));
		streamWriter.WriteLine(string.Join(",", source.Select(EscapeCsvField)));
		long num2 = 0L;
		int num3 = 0;
		foreach (Dictionary<string, object> row in rows)
		{
			if (num3 >= num)
			{
				csvExportResult.WasTruncated = true;
				csvExportResult.TruncationReason = $"Row limit reached ({num} rows)";
				break;
			}
			IEnumerable<string> values = columns.Select(delegate(DaxColumnInfo c)
			{
				row.TryGetValue(c.Name, out var value);
				return FormatValue(value, c.DataType);
			});
			string text2 = string.Join(",", values) + "\r\n";
			num2 += Encoding.UTF8.GetByteCount(text2);
			if (num2 > 4194304)
			{
				csvExportResult.WasTruncated = true;
				csvExportResult.TruncationReason = $"File size limit reached ({4L}MB)";
				break;
			}
			streamWriter.Write(text2);
			num3++;
		}
		csvExportResult.RowsWritten = num3;
		return csvExportResult;
	}

	public static void CleanupOldFiles(string exportFolder)
	{
		if (!Directory.Exists(exportFolder))
		{
			return;
		}
		DateTime dateTime = DateTime.Now.AddMinutes(-60.0);
		string[] files = Directory.GetFiles(exportFolder, "dax_query_result_*.csv");
		foreach (string path in files)
		{
			if (File.GetCreationTime(path) < dateTime)
			{
				File.Delete(path);
			}
		}
	}

	private static bool IsBinaryType(string dataType)
	{
		return BinaryTypes.Contains(dataType);
	}

	private static string FormatValue(object? value, string dataType)
	{
		if (value == null || value == DBNull.Value)
		{
			return "";
		}
		if (IsBinaryType(dataType) && value is byte[] inArray)
		{
			return EscapeCsvField(Convert.ToBase64String(inArray));
		}
		return EscapeCsvField(value.ToString() ?? "");
	}

	private static string EscapeCsvField(string value)
	{
		if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
		{
			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}
		return value;
	}
}
