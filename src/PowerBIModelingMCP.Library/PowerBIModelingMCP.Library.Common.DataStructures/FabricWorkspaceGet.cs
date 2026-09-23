using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class FabricWorkspaceGet
{
	public required Guid Id { get; set; }

	public string? Name { get; set; }

	public string? Description { get; set; }

	public string? Type { get; set; }

	public string? State { get; set; }

	public Guid? CapacityId { get; set; }

	public object? Raw { get; set; }
}
