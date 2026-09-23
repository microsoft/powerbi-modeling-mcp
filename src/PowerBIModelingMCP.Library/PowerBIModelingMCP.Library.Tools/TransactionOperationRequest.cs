using System.ComponentModel.DataAnnotations;
using PowerBIModelingMCP.Library.Common;

namespace PowerBIModelingMCP.Library.Tools;

public class TransactionOperationRequest
{
	[Required]
	[YamlFieldDescription("transaction_operations", "Operation")]
	public required string Operation { get; set; }

	[YamlFieldDescription("transaction_operations", "ConnectionName")]
	public string? ConnectionName { get; set; }

	[YamlFieldDescription("transaction_operations", "TransactionId")]
	public string? TransactionId { get; set; }
}
