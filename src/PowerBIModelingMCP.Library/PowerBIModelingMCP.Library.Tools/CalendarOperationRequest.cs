using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class CalendarOperationRequest
{
	[YamlFieldDescription("calendar_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("calendar_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("calendar_operations", "Definitions")]
	public List<CalendarDefinition>? Definitions { get; set; }

	[YamlFieldDescription("calendar_operations", "References")]
	public List<CalendarReference>? References { get; set; }

	[YamlFieldDescription("calendar_operations", "RenameDefinitions")]
	public List<CalendarRename>? RenameDefinitions { get; set; }

	[YamlFieldDescription("calendar_operations", "Filter")]
	public CalendarListFilter? Filter { get; set; }

	[YamlFieldDescription("calendar_operations", "ColumnGroupFilter")]
	public CalendarColumnGroupListFilter? ColumnGroupFilter { get; set; }

	[YamlFieldDescription("calendar_operations", "ColumnGroupDefinitions")]
	public List<CalendarColumnGroupDefinition>? ColumnGroupDefinitions { get; set; }

	[YamlFieldDescription("calendar_operations", "TmdlExportOptions")]
	public ExportTmdl? TmdlExportOptions { get; set; }

	[YamlFieldDescription("calendar_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
