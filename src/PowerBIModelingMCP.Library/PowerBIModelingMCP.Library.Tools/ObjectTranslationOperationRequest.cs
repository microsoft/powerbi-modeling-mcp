using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class ObjectTranslationOperationRequest
{
	[YamlFieldDescription("object_translation_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[Required]
	[YamlFieldDescription("object_translation_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("object_translation_operations", "Definitions")]
	public List<ObjectTranslationDefinition>? Definitions { get; set; }

	[YamlFieldDescription("object_translation_operations", "References")]
	public List<ObjectTranslationReference>? References { get; set; }

	[YamlFieldDescription("object_translation_operations", "Filter")]
	public ObjectTranslationListFilter? Filter { get; set; }

	[YamlFieldDescription("object_translation_operations", "Options")]
	public BatchOptions Options { get; set; } = new BatchOptions();
}
