using System;
using System.ComponentModel;
using Microsoft.AnalysisServices.Tabular.Serialization;
using ModelContextProtocol;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class TmdlSerializationOptions
{
	[Description("Default: false")]
	public bool IncludeChildren { get; set; }

	[Description("Default: false")]
	public bool IncludeInferredDataTypes { get; set; }

	[Description("Default: false")]
	public bool IncludeRestrictedInformation { get; set; }

	public MetadataSerializationOptions ToMetadataSerializationOptions()
	{
		try
		{
			MetadataSerializationOptionsBuilder metadataSerializationOptionsBuilder = new MetadataSerializationOptionsBuilder(MetadataSerializationStyle.Tmdl);
			if (!IncludeChildren)
			{
				metadataSerializationOptionsBuilder.WithoutChildrenMetadata();
			}
			if (IncludeInferredDataTypes)
			{
				metadataSerializationOptionsBuilder.WithInferredDataTypes();
			}
			if (IncludeRestrictedInformation)
			{
				metadataSerializationOptionsBuilder.WithRestrictedInformation();
			}
			return metadataSerializationOptionsBuilder.GetOptions();
		}
		catch (McpException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			throw new McpExceptionWithSource("Failed to convert TMDL serialization options: " + ex2.Message + ". Please check your SerializationOptions configuration.", ex2, ErrorSource.User, "Failed to convert TMDL serialization options. Please check your SerializationOptions configuration.");
		}
	}
}
