namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ExportTmdl : ExportOptionsBase
{
	private TmdlSerializationOptions _serializationOptions = new TmdlSerializationOptions();

	public TmdlSerializationOptions? SerializationOptions
	{
		get
		{
			return _serializationOptions;
		}
		set
		{
			_serializationOptions = value ?? new TmdlSerializationOptions();
		}
	}
}
