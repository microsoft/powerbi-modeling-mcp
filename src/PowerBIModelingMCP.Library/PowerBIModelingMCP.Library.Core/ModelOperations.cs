using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class ModelOperations
{
	public static bool AddProToolingAnnotation(IConnectionInfo info, string? proToolingValue = null)
	{
		if (info == null)
		{
			throw new ArgumentNullException("info");
		}
		Model model = info.Database.Model;
		string targetValue = proToolingValue ?? "MCP-PBIModeling";
		Annotation annotation = model.Annotations.FirstOrDefault((Annotation a) => a.Name == "PBI_ProTooling");
		if (annotation != null)
		{
			List<string> list;
			try
			{
				list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(annotation.Value) ?? new List<string>();
			}
			catch
			{
				list = new List<string>();
			}
			if (list.Any((string v) => string.Equals(v, targetValue, StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}
			list.Add(targetValue);
			annotation.Value = System.Text.Json.JsonSerializer.Serialize(list);
			return true;
		}
		List<string> value = new List<string> { targetValue };
		model.Annotations.Add(new Annotation
		{
			Name = "PBI_ProTooling",
			Value = System.Text.Json.JsonSerializer.Serialize(value)
		});
		return true;
	}

	private static void ValidateBase(ModelBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Model definition cannot be null", ErrorSource.User);
		}
		if (def.ExtendedProperties != null)
		{
			List<string> list = ExtendedPropertyHelpers.Validate(def.ExtendedProperties);
			if (list.Count > 0)
			{
				throw new McpExceptionWithSource("ExtendedProperties validation failed: " + string.Join(", ", list), ErrorSource.User, "ExtendedProperties validation failed.");
			}
		}
		AnnotationHelpers.ValidateAnnotations(def.Annotations);
		if (def.BindingInfos != null)
		{
			ValidateBindingInfos(def.BindingInfos, null);
		}
	}

	private static void ValidateBindingInfos(List<PowerBIModelingMCP.Library.Common.DataStructures.BindingInfo> bindingInfos, Model? model)
	{
		if (bindingInfos == null)
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>();
		foreach (PowerBIModelingMCP.Library.Common.DataStructures.BindingInfo bindingInfo in bindingInfos)
		{
			if (string.IsNullOrWhiteSpace(bindingInfo.Name))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("BindingInfo Name is required", ErrorSource.User);
			}
			if (!hashSet.Add(bindingInfo.Name))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Duplicate BindingInfo name: " + bindingInfo.Name, ErrorSource.User);
			}
			if (string.IsNullOrWhiteSpace(bindingInfo.Type))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("BindingInfo Type is required for '" + bindingInfo.Name + "'", ErrorSource.User);
			}
			if (bindingInfo.Type.ToLowerInvariant() == "databindinghint")
			{
				if (string.IsNullOrWhiteSpace(bindingInfo.ConnectionId))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionId is required for DataBindingHint '" + bindingInfo.Name + "'", ErrorSource.User);
				}
				if (model != null)
				{
					string name = bindingInfo.TargetDataSourceReferenceName ?? bindingInfo.Name;
					if (model.DataSources.Find(name) != null)
					{
					}
				}
				if (bindingInfo.ExtendedProperties != null)
				{
					List<string> list = ExtendedPropertyHelpers.Validate(bindingInfo.ExtendedProperties);
					if (list.Count > 0)
					{
						throw new McpExceptionWithSource("BindingInfo '" + bindingInfo.Name + "' ExtendedProperties validation failed: " + string.Join(", ", list), ErrorSource.User, "BindingInfo '" + bindingInfo.Name + "' ExtendedProperties validation failed.");
					}
				}
				AnnotationHelpers.ValidateAnnotations(bindingInfo.Annotations, "BindingInfo '" + bindingInfo.Name + "'");
				continue;
			}
			throw new McpExceptionWithSource("Unsupported BindingInfo type '" + bindingInfo.Type + "'. Currently supported types: DataBindingHint", ErrorSource.User, "Unsupported BindingInfo type supplied. Currently supported types: DataBindingHint.");
		}
	}

	public static async Task<ModelGet> GetModel(string? connectionName = null)
	{
		ModelGet result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				ModelGet modelInternal = GetModelInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("get model", success: true, OperationType.Read, connectionInfo);
				result = modelInternal;
			}
			catch
			{
				AuditEvent.Default.Emit("get model", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static ModelGet GetModelInternal(Database db)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		Model model = db.Model;
		ModelGet modelGet = new ModelGet
		{
			Name = model.Name,
			Description = model.Description,
			StorageLocation = model.StorageLocation,
			DefaultMode = model.DefaultMode.ToString(),
			DefaultDataView = model.DefaultDataView.ToString(),
			Culture = model.Culture,
			Collation = model.Collation,
			ModifiedTime = model.ModifiedTime,
			StructureModifiedTime = model.StructureModifiedTime,
			DataAccessOptions = ((model.DataAccessOptions != null) ? System.Text.Json.JsonSerializer.Serialize(model.DataAccessOptions) : null),
			DefaultPowerBIDataSourceVersion = model.DefaultPowerBIDataSourceVersion.ToString(),
			ForceUniqueNames = model.ForceUniqueNames,
			DiscourageImplicitMeasures = model.DiscourageImplicitMeasures,
			DiscourageReportMeasures = model.DiscourageReportMeasures,
			DataSourceVariablesOverrideBehavior = model.DataSourceVariablesOverrideBehavior.ToString(),
			DataSourceDefaultMaxConnections = model.DataSourceDefaultMaxConnections,
			SourceQueryCulture = model.SourceQueryCulture,
			MAttributes = model.MAttributes,
			DiscourageCompositeModels = model.DiscourageCompositeModels,
			DirectLakeBehavior = model.DirectLakeBehavior.ToString(),
			ValueFilterBehavior = model.ValueFilterBehavior.ToString(),
			SelectionExpressionBehavior = model.SelectionExpressionBehavior.ToString(),
			MetadataAccessPolicy = model.MetadataAccessPolicy.ToString()
		};
		if (model.DefaultMeasure != null)
		{
			modelGet.DefaultMeasureTable = model.DefaultMeasure.Table?.Name;
			modelGet.DefaultMeasureName = model.DefaultMeasure.Name;
		}
		if (model.AutomaticAggregationOptions != null)
		{
			PowerBIModelingMCP.Library.Common.DataStructures.AutomaticAggregationOptions value = new PowerBIModelingMCP.Library.Common.DataStructures.AutomaticAggregationOptions
			{
				AggregationTableMaxRows = model.AutomaticAggregationOptions.AggregationTableMaxRows,
				AggregationTableSizeLimit = model.AutomaticAggregationOptions.AggregationTableSizeLimit,
				DetailTableMinRows = model.AutomaticAggregationOptions.DetailTableMinRows,
				QueryCoverage = model.AutomaticAggregationOptions.QueryCoverage
			};
			modelGet.AutomaticAggregationOptions = System.Text.Json.JsonSerializer.Serialize(value);
		}
		modelGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromModel(model);
		modelGet.Annotations = new List<KeyValuePair<string, string>>();
		foreach (Annotation annotation in model.Annotations)
		{
			modelGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		modelGet.BindingInfos = new List<PowerBIModelingMCP.Library.Common.DataStructures.BindingInfo>();
		foreach (Microsoft.AnalysisServices.Tabular.BindingInfo item in model.BindingInfoCollection)
		{
			PowerBIModelingMCP.Library.Common.DataStructures.BindingInfo bindingInfo = new PowerBIModelingMCP.Library.Common.DataStructures.BindingInfo
			{
				Name = item.Name,
				Description = item.Description
			};
			if (item is DataBindingHint dataBindingHint)
			{
				bindingInfo.Type = "DataBindingHint";
				bindingInfo.ConnectionId = dataBindingHint.ConnectionId;
				bindingInfo.TargetDataSourceReferenceName = item.Name;
			}
			else
			{
				bindingInfo.Type = item.GetType().Name;
			}
			if (item.Annotations.Count > 0)
			{
				bindingInfo.Annotations = new List<KeyValuePair<string, string>>();
				foreach (Annotation annotation2 in item.Annotations)
				{
					bindingInfo.Annotations.Add(new KeyValuePair<string, string>(annotation2.Name, annotation2.Value));
				}
			}
			bindingInfo.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromBindingInfo(item);
			modelGet.BindingInfos.Add(bindingInfo);
		}
		return modelGet;
	}

	public static async Task<OperationResult> UpdateModel(string? connectionName, ModelDefinition update)
	{
		ValidateBase(update, isCreate: false);
		OperationResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = UpdateModelInternal(info, update);
		}
		return result;
	}

	private static OperationResult UpdateModelInternal(IConnectionInfo info, ModelDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateBase(update, isCreate: false);
		Model model = info.Database.Model;
		bool flag = false;
		if (!string.IsNullOrWhiteSpace(update.Name) && model.Name != update.Name)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Model name changes are not allowed in UpdateModel. Use the RenameModel operation instead.", ErrorSource.User);
		}
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (model.Description != text)
			{
				model.Description = text;
				flag = true;
			}
		}
		if (update.StorageLocation != null)
		{
			string text2 = (string.IsNullOrEmpty(update.StorageLocation) ? null : update.StorageLocation);
			if (model.StorageLocation != text2)
			{
				model.StorageLocation = text2;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.DefaultMode))
		{
			if (!Enum.TryParse<ModeType>(update.DefaultMode, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(ModeType));
				throw new McpExceptionWithSource("Invalid DefaultMode '" + update.DefaultMode + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid DefaultMode supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			if (model.DefaultMode != result)
			{
				model.DefaultMode = result;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.DefaultDataView))
		{
			if (!Enum.TryParse<DataViewType>(update.DefaultDataView, ignoreCase: true, out var result2))
			{
				string[] names2 = Enum.GetNames(typeof(DataViewType));
				throw new McpExceptionWithSource("Invalid DefaultDataView '" + update.DefaultDataView + "'. Valid values are: " + string.Join(", ", names2), ErrorSource.User, "Invalid DefaultDataView supplied. Valid values are: " + string.Join(", ", names2) + ".");
			}
			if (model.DefaultDataView != result2)
			{
				model.DefaultDataView = result2;
				flag = true;
			}
		}
		if (update.Culture != null)
		{
			string text3 = (string.IsNullOrEmpty(update.Culture) ? null : update.Culture);
			if (model.Culture != text3)
			{
				model.Culture = text3;
				flag = true;
			}
		}
		if (update.Collation != null)
		{
			string text4 = (string.IsNullOrEmpty(update.Collation) ? null : update.Collation);
			if (model.Collation != text4)
			{
				model.Collation = text4;
				flag = true;
			}
		}
		if (update.DataAccessOptions != null)
		{
			if (string.IsNullOrEmpty(update.DataAccessOptions))
			{
				if (model.DataAccessOptions != null)
				{
					model.DataAccessOptions = null;
					flag = true;
				}
			}
			else
			{
				try
				{
					DataAccessOptions dataAccessOptions = System.Text.Json.JsonSerializer.Deserialize<DataAccessOptions>(update.DataAccessOptions);
					model.DataAccessOptions = dataAccessOptions;
					flag = true;
				}
				catch (Exception ex)
				{
					throw new McpExceptionWithSource("Invalid DataAccessOptions: " + ex.Message, ex, ErrorSource.User, "Invalid DataAccessOptions; exception type: '" + ex.GetType().Name + "'.");
				}
			}
		}
		if (update.DefaultMeasureTable != null || update.DefaultMeasureName != null)
		{
			if (string.IsNullOrEmpty(update.DefaultMeasureTable) && string.IsNullOrEmpty(update.DefaultMeasureName))
			{
				if (model.DefaultMeasure != null)
				{
					model.DefaultMeasure = null;
					flag = true;
				}
			}
			else
			{
				if (string.IsNullOrWhiteSpace(update.DefaultMeasureTable) || string.IsNullOrWhiteSpace(update.DefaultMeasureName))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("For DefaultMeasure, both DefaultMeasureTable and DefaultMeasureName must be provided together (both non-empty to set, both empty to clear)", ErrorSource.User);
				}
				Measure measure = (model.Tables.Find(update.DefaultMeasureTable) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + update.DefaultMeasureTable + "' not found for DefaultMeasure", ErrorSource.User)).Measures.Find(update.DefaultMeasureName);
				if (measure == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure '{update.DefaultMeasureName}' not found in table '{update.DefaultMeasureTable}'", ErrorSource.User);
				}
				if (model.DefaultMeasure != measure)
				{
					model.DefaultMeasure = measure;
					flag = true;
				}
			}
		}
		if (!string.IsNullOrWhiteSpace(update.DefaultPowerBIDataSourceVersion))
		{
			if (!Enum.TryParse<PowerBIDataSourceVersion>(update.DefaultPowerBIDataSourceVersion, ignoreCase: true, out var result3))
			{
				string[] names3 = Enum.GetNames(typeof(PowerBIDataSourceVersion));
				throw new McpExceptionWithSource("Invalid DefaultPowerBIDataSourceVersion '" + update.DefaultPowerBIDataSourceVersion + "'. Valid values are: " + string.Join(", ", names3), ErrorSource.User, "Invalid DefaultPowerBIDataSourceVersion supplied. Valid values are: " + string.Join(", ", names3) + ".");
			}
			if (model.DefaultPowerBIDataSourceVersion != result3)
			{
				model.DefaultPowerBIDataSourceVersion = result3;
				flag = true;
			}
		}
		if (update.ForceUniqueNames.HasValue && model.ForceUniqueNames != update.ForceUniqueNames.Value)
		{
			model.ForceUniqueNames = update.ForceUniqueNames.Value;
			flag = true;
		}
		if (update.DiscourageImplicitMeasures.HasValue && model.DiscourageImplicitMeasures != update.DiscourageImplicitMeasures.Value)
		{
			model.DiscourageImplicitMeasures = update.DiscourageImplicitMeasures.Value;
			flag = true;
		}
		if (update.DiscourageCompositeModels.HasValue && model.DiscourageCompositeModels != update.DiscourageCompositeModels.Value)
		{
			model.DiscourageCompositeModels = update.DiscourageCompositeModels.Value;
			flag = true;
		}
		if (!string.IsNullOrWhiteSpace(update.DataSourceVariablesOverrideBehavior))
		{
			if (!Enum.TryParse<DataSourceVariablesOverrideBehaviorType>(update.DataSourceVariablesOverrideBehavior, ignoreCase: true, out var result4))
			{
				string[] names4 = Enum.GetNames(typeof(DataSourceVariablesOverrideBehaviorType));
				throw new McpExceptionWithSource("Invalid DataSourceVariablesOverrideBehavior '" + update.DataSourceVariablesOverrideBehavior + "'. Valid values are: " + string.Join(", ", names4), ErrorSource.User, "Invalid DataSourceVariablesOverrideBehavior supplied. Valid values are: " + string.Join(", ", names4) + ".");
			}
			if (model.DataSourceVariablesOverrideBehavior != result4)
			{
				model.DataSourceVariablesOverrideBehavior = result4;
				flag = true;
			}
		}
		if (update.DataSourceDefaultMaxConnections.HasValue && model.DataSourceDefaultMaxConnections != update.DataSourceDefaultMaxConnections.Value)
		{
			model.DataSourceDefaultMaxConnections = update.DataSourceDefaultMaxConnections.Value;
			flag = true;
		}
		if (update.SourceQueryCulture != null)
		{
			string text5 = (string.IsNullOrEmpty(update.SourceQueryCulture) ? null : update.SourceQueryCulture);
			if (model.SourceQueryCulture != text5)
			{
				model.SourceQueryCulture = text5;
				flag = true;
			}
		}
		if (update.MAttributes != null)
		{
			string text6 = (string.IsNullOrEmpty(update.MAttributes) ? null : update.MAttributes);
			if (model.MAttributes != text6)
			{
				model.MAttributes = text6;
				flag = true;
			}
		}
		if (update.DiscourageReportMeasures.HasValue && model.DiscourageReportMeasures != update.DiscourageReportMeasures.Value)
		{
			model.DiscourageReportMeasures = update.DiscourageReportMeasures.Value;
			flag = true;
		}
		if (!string.IsNullOrWhiteSpace(update.DirectLakeBehavior))
		{
			if (!Enum.TryParse<DirectLakeBehavior>(update.DirectLakeBehavior, ignoreCase: true, out var result5))
			{
				string[] names5 = Enum.GetNames(typeof(DirectLakeBehavior));
				throw new McpExceptionWithSource("Invalid DirectLakeBehavior '" + update.DirectLakeBehavior + "'. Valid values are: " + string.Join(", ", names5), ErrorSource.User, "Invalid DirectLakeBehavior supplied. Valid values are: " + string.Join(", ", names5) + ".");
			}
			if (model.DirectLakeBehavior != result5)
			{
				model.DirectLakeBehavior = result5;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.ValueFilterBehavior))
		{
			if (!Enum.TryParse<ValueFilterBehaviorType>(update.ValueFilterBehavior, ignoreCase: true, out var result6))
			{
				string[] names6 = Enum.GetNames(typeof(ValueFilterBehaviorType));
				throw new McpExceptionWithSource("Invalid ValueFilterBehavior '" + update.ValueFilterBehavior + "'. Valid values are: " + string.Join(", ", names6), ErrorSource.User, "Invalid ValueFilterBehavior supplied. Valid values are: " + string.Join(", ", names6) + ".");
			}
			if (model.ValueFilterBehavior != result6)
			{
				model.ValueFilterBehavior = result6;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.SelectionExpressionBehavior))
		{
			if (!Enum.TryParse<SelectionExpressionBehaviorType>(update.SelectionExpressionBehavior, ignoreCase: true, out var result7))
			{
				string[] names7 = Enum.GetNames(typeof(SelectionExpressionBehaviorType));
				throw new McpExceptionWithSource("Invalid SelectionExpressionBehavior '" + update.SelectionExpressionBehavior + "'. Valid values are: " + string.Join(", ", names7), ErrorSource.User, "Invalid SelectionExpressionBehavior supplied. Valid values are: " + string.Join(", ", names7) + ".");
			}
			if (model.SelectionExpressionBehavior != result7)
			{
				model.SelectionExpressionBehavior = result7;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.MetadataAccessPolicy))
		{
			if (!Enum.TryParse<MetadataCategory>(update.MetadataAccessPolicy, ignoreCase: true, out var result8))
			{
				string[] names8 = Enum.GetNames(typeof(MetadataCategory));
				throw new McpExceptionWithSource("Invalid MetadataAccessPolicy '" + update.MetadataAccessPolicy + "'. Valid values are: " + string.Join(", ", names8), ErrorSource.User, "Invalid MetadataAccessPolicy supplied. Valid values are: " + string.Join(", ", names8) + ".");
			}
			if (model.MetadataAccessPolicy != result8)
			{
				model.MetadataAccessPolicy = result8;
				flag = true;
			}
		}
		if (update.AutomaticAggregationOptions != null)
		{
			if (string.IsNullOrEmpty(update.AutomaticAggregationOptions))
			{
				if (model.AutomaticAggregationOptions != null)
				{
					model.AutomaticAggregationOptions = null;
					flag = true;
				}
			}
			else
			{
				try
				{
					PowerBIModelingMCP.Library.Common.DataStructures.AutomaticAggregationOptions automaticAggregationOptions = System.Text.Json.JsonSerializer.Deserialize<PowerBIModelingMCP.Library.Common.DataStructures.AutomaticAggregationOptions>(update.AutomaticAggregationOptions);
					if (automaticAggregationOptions != null)
					{
						if (model.AutomaticAggregationOptions == null)
						{
							model.AutomaticAggregationOptions = new Microsoft.AnalysisServices.Tabular.AutomaticAggregationOptions();
							flag = true;
						}
						if (automaticAggregationOptions.AggregationTableMaxRows.HasValue && model.AutomaticAggregationOptions.AggregationTableMaxRows != automaticAggregationOptions.AggregationTableMaxRows.Value)
						{
							model.AutomaticAggregationOptions.AggregationTableMaxRows = automaticAggregationOptions.AggregationTableMaxRows.Value;
							flag = true;
						}
						if (automaticAggregationOptions.AggregationTableSizeLimit.HasValue && model.AutomaticAggregationOptions.AggregationTableSizeLimit != automaticAggregationOptions.AggregationTableSizeLimit.Value)
						{
							model.AutomaticAggregationOptions.AggregationTableSizeLimit = automaticAggregationOptions.AggregationTableSizeLimit.Value;
							flag = true;
						}
						if (automaticAggregationOptions.DetailTableMinRows.HasValue && model.AutomaticAggregationOptions.DetailTableMinRows != automaticAggregationOptions.DetailTableMinRows.Value)
						{
							model.AutomaticAggregationOptions.DetailTableMinRows = automaticAggregationOptions.DetailTableMinRows.Value;
							flag = true;
						}
						if (automaticAggregationOptions.QueryCoverage.HasValue && model.AutomaticAggregationOptions.QueryCoverage != automaticAggregationOptions.QueryCoverage.Value)
						{
							model.AutomaticAggregationOptions.QueryCoverage = automaticAggregationOptions.QueryCoverage.Value;
							flag = true;
						}
					}
				}
				catch (Exception ex2)
				{
					throw new McpExceptionWithSource("Invalid AutomaticAggregationOptions: " + ex2.Message, ex2, ErrorSource.User, "Invalid AutomaticAggregationOptions; exception type: '" + ex2.GetType().Name + "'.");
				}
			}
		}
		if (update.ExtendedProperties != null)
		{
			bool num = model.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceExtendedProperties(model, update.ExtendedProperties, (Model obj) => obj.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(model, update.Annotations, (Model obj) => obj.Annotations))
		{
			flag = true;
		}
		if (update.BindingInfos != null)
		{
			ValidateBindingInfos(update.BindingInfos, model);
			model.BindingInfoCollection.Clear();
			if (update.BindingInfos.Count > 0)
			{
				foreach (PowerBIModelingMCP.Library.Common.DataStructures.BindingInfo bindingInfo2 in update.BindingInfos)
				{
					if (bindingInfo2.Type.ToLowerInvariant() == "databindinghint")
					{
						Microsoft.AnalysisServices.Tabular.BindingInfo bindingInfo = new DataBindingHint
						{
							Name = bindingInfo2.Name,
							ConnectionId = (bindingInfo2.ConnectionId ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionId is required for DataBindingHint '" + bindingInfo2.Name + "'"))
						};
						if (!string.IsNullOrWhiteSpace(bindingInfo2.Description))
						{
							bindingInfo.Description = bindingInfo2.Description;
						}
						if (bindingInfo2.Annotations != null)
						{
							foreach (KeyValuePair<string, string> annotation in bindingInfo2.Annotations)
							{
								bindingInfo.Annotations.Add(new Annotation
								{
									Name = annotation.Key,
									Value = annotation.Value
								});
							}
						}
						if (bindingInfo2.ExtendedProperties != null)
						{
							ExtendedPropertyHelpers.ApplyToBindingInfo(bindingInfo, bindingInfo2.ExtendedProperties);
						}
						model.BindingInfoCollection.Add(bindingInfo);
						continue;
					}
					throw new McpExceptionWithSource("Unsupported BindingInfo type '" + bindingInfo2.Type + "'. Currently supported types: DataBindingHint", ErrorSource.User, "Unsupported BindingInfo type supplied. Currently supported types: DataBindingHint.");
				}
			}
			flag = true;
		}
		if (!flag)
		{
			return OperationResult.CreateSuccess("Model is already in the requested state", model.Name, ObjectType.Model, Operation.Update, hasChanges: false);
		}
		TransactionOperations.RecordOperation(info, "Updated model properties");
		ConnectionOperations.SaveChangesWithRollback(info, "update model", OperationType.Update);
		return OperationResult.CreateSuccess("Model '" + model.Name + "' updated successfully", model.Name, ObjectType.Model, Operation.Update);
	}

	public static async Task RefreshModel(string? connectionName = null, string? refreshType = "Automatic")
	{
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		RefreshModelInternal(info, refreshType);
	}

	private static void RefreshModelInternal(IConnectionInfo info, string? refreshType)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		Model model = info.Database.Model;
		RefreshType result;
		if (string.IsNullOrWhiteSpace(refreshType))
		{
			result = RefreshType.Automatic;
		}
		else if (!Enum.TryParse<RefreshType>(refreshType, ignoreCase: true, out result))
		{
			string[] names = Enum.GetNames(typeof(RefreshType));
			throw new McpExceptionWithSource("Invalid refresh type '" + refreshType + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid refresh type supplied. Valid values are: " + string.Join(", ", names) + ".");
		}
		model.RequestRefresh(result);
		TransactionOperations.RecordOperation(info, $"Refreshed model with refresh type '{result}'");
		ConnectionOperations.SaveChangesWithRollback(info, "refresh model", OperationType.Update);
	}

	public static async Task<Dictionary<string, object>> GetModelStats(string? connectionName = null)
	{
		Dictionary<string, object> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				Database database = connectionInfo.Database;
				if (database?.Model == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Model not found in the specified database", ErrorSource.User);
				}
				Model model = database.Model;
				Dictionary<string, object> dictionary = new Dictionary<string, object>
				{
					["ModelName"] = model.Name,
					["DatabaseName"] = database.Name,
					["CompatibilityLevel"] = database.CompatibilityLevel,
					["TableCount"] = model.Tables.Count,
					["TotalMeasureCount"] = model.Tables.Sum((Table t) => t.Measures.Count),
					["TotalColumnCount"] = model.Tables.Sum((Table t) => t.Columns.Count),
					["TotalPartitionCount"] = model.Tables.Sum((Table t) => t.Partitions.Count),
					["RelationshipCount"] = model.Relationships.Count,
					["RoleCount"] = model.Roles.Count,
					["DataSourceCount"] = model.DataSources.Count,
					["CultureCount"] = model.Cultures.Count,
					["PerspectiveCount"] = model.Perspectives.Count,
					["Tables"] = model.Tables.Select((Table t) => new
					{
						Name = t.Name,
						ColumnCount = t.Columns.Count,
						MeasureCount = t.Measures.Count,
						PartitionCount = t.Partitions.Count,
						IsHidden = t.IsHidden
					}).ToList()
				};
				AuditEvent.Default.Emit("get model stats", success: true, OperationType.Read, connectionInfo);
				result = dictionary;
			}
			catch
			{
				AuditEvent.Default.Emit("get model stats", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	public static async Task RenameModel(string? connectionName, string newName)
	{
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("New model name cannot be null or empty", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		RenameModelInternal(info, newName);
	}

	private static void RenameModelInternal(IConnectionInfo info, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("New model name cannot be null or empty", ErrorSource.User);
		}
		string name = info.Database.Model.Name;
		if (name == newName)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Model is already named '" + newName + "'", ErrorSource.User);
		}
		TransactionOperations.RecordOperation(info, $"Renamed model from '{name}' to '{newName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename model", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	public static async Task<string> ExportTMDL(string? connectionName, ExportTmdl options)
	{
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string text = ExportTMDLInternal(connectionInfo.Database, options);
				AuditEvent.Default.Emit("export model to TMDL", success: true, OperationType.Read, connectionInfo);
				result = text;
			}
			catch
			{
				AuditEvent.Default.Emit("export model to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static string ExportTMDLInternal(Database db, ExportTmdl options)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		return ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(db.Model, options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
	}
}
