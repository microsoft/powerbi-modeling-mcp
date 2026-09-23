using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmdlExportResult : ExportResultBase
{
	public Dictionary<string, object> TmdlMetadata { get; set; } = new Dictionary<string, object>();

	public ExportTmdl? AppliedOptions { get; set; }

	public static TmdlExportResult CreateSuccess(string objectName, string objectType, string content, string processedContent, bool isTruncated, string? savedFilePath, List<string> warnings, ExportTmdl? appliedOptions = null)
	{
		TmdlExportResult tmdlExportResult = ExportResultBase.CreateSuccess<TmdlExportResult>(objectName, objectType, content, processedContent, isTruncated, savedFilePath, warnings);
		tmdlExportResult.AppliedOptions = appliedOptions;
		tmdlExportResult.TmdlMetadata["ExportType"] = "TMDL";
		tmdlExportResult.TmdlMetadata["ContentType"] = "YAML-like";
		if (appliedOptions?.SerializationOptions != null)
		{
			tmdlExportResult.TmdlMetadata["IncludeChildren"] = appliedOptions.SerializationOptions.IncludeChildren;
			tmdlExportResult.TmdlMetadata["IncludeInferredDataTypes"] = appliedOptions.SerializationOptions.IncludeInferredDataTypes;
			tmdlExportResult.TmdlMetadata["IncludeRestrictedInformation"] = appliedOptions.SerializationOptions.IncludeRestrictedInformation;
		}
		return tmdlExportResult;
	}

	public static TmdlExportResult CreateFailure(string objectName, string objectType, string errorMessage, ErrorSource errorSource)
	{
		TmdlExportResult tmdlExportResult = ExportResultBase.CreateFailure<TmdlExportResult>(objectName, objectType, errorMessage, errorSource);
		tmdlExportResult.TmdlMetadata["ExportType"] = "TMDL";
		tmdlExportResult.TmdlMetadata["ContentType"] = "YAML-like";
		return tmdlExportResult;
	}
}
