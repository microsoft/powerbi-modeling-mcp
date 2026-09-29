using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class ObjectTranslationOperations
{
	public class ObjectTranslationOperationResult
	{
		public string CultureName { get; set; } = string.Empty;

		public string ObjectType { get; set; } = string.Empty;

		public string ObjectDisplayName { get; set; } = string.Empty;

		public string Property { get; set; } = string.Empty;

		public string? Value { get; set; }

		public string? ErrorMessage { get; set; }

		public bool Success { get; set; }

		public string? Message { get; set; }

		public bool HasChanges { get; set; }
	}

	public static void ValidateObjectTranslationDefinition(ObjectTranslationBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Object translation definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.CultureName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.ObjectType))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Object type is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Property))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Property is required", ErrorSource.User);
		}
		if (!IsValidCultureName(def.CultureName))
		{
			throw new McpExceptionWithSource("Invalid culture name format: " + def.CultureName + ". Expected format like 'en-US', 'fr-FR', etc.", ErrorSource.User, "Invalid culture name format supplied. Expected format like 'en-US', 'fr-FR', etc.");
		}
		TranslationHelper.ValidateObjectType(def.ObjectType);
		TranslationHelper.ValidateTranslatableProperty(def.ObjectType, def.Property);
		TranslationHelper.ValidateObjectIdentification(def);
	}

	public static bool IsValidCultureName(string cultureName)
	{
		if (string.IsNullOrWhiteSpace(cultureName))
		{
			return false;
		}
		try
		{
			CultureInfo.GetCultureInfo(cultureName);
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static Culture EnsureCultureExists(IConnectionInfo connInfo, string cultureName, bool createIfNotExists)
	{
		if (connInfo == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		Model model = connInfo.Database.Model;
		Culture culture = model.Cultures.Find(cultureName);
		if (culture == null)
		{
			if (!createIfNotExists)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture '" + cultureName + "' does not exist in the model", ErrorSource.User);
			}
			CultureDefinition def = new CultureDefinition
			{
				Name = cultureName,
				ExtendedProperties = new List<PowerBIModelingMCP.Library.Common.DataStructures.ExtendedProperty>(),
				Annotations = new List<KeyValuePair<string, string>>()
			};
			OperationResult operationResult = CultureOperations.CreateCultureInternal(connInfo, def);
			if (!operationResult.Success)
			{
				throw new McpExceptionWithSource("Failed to create culture '" + cultureName + "': " + operationResult.Message, "Failed to create culture '" + cultureName + "'.");
			}
			culture = model.Cultures.Find(cultureName);
			if (culture == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture '" + cultureName + "' was created but cannot be found in the model", ErrorSource.User);
			}
		}
		return culture;
	}

	public static NamedMetadataObject? FindTranslatableObject(Model model, ObjectTranslationBase translation)
	{
		return translation.ObjectType switch
		{
			"Model" => model, 
			"Table" => model.Tables.Find(translation.TableName), 
			"Measure" => FindMeasure(model, translation), 
			"Column" => FindColumn(model, translation), 
			"Hierarchy" => FindHierarchy(model, translation), 
			"Level" => FindLevel(model, translation), 
			"KPI" => FindKpi(model, translation), 
			_ => null, 
		};
	}

	private static NamedMetadataObject? FindMeasure(Model model, ObjectTranslationBase translation)
	{
		if (string.IsNullOrWhiteSpace(translation.MeasureName))
		{
			return null;
		}
		if (string.IsNullOrWhiteSpace(translation.TableName))
		{
			foreach (Table table in model.Tables)
			{
				Measure measure = table.Measures.Find(translation.MeasureName);
				if (measure != null)
				{
					return measure;
				}
			}
			return null;
		}
		return model.Tables.Find(translation.TableName)?.Measures.Find(translation.MeasureName);
	}

	private static NamedMetadataObject? FindColumn(Model model, ObjectTranslationBase translation)
	{
		if (string.IsNullOrWhiteSpace(translation.TableName) || string.IsNullOrWhiteSpace(translation.ColumnName))
		{
			return null;
		}
		return model.Tables.Find(translation.TableName)?.Columns.Find(translation.ColumnName);
	}

	private static NamedMetadataObject? FindHierarchy(Model model, ObjectTranslationBase translation)
	{
		if (string.IsNullOrWhiteSpace(translation.TableName) || string.IsNullOrWhiteSpace(translation.HierarchyName))
		{
			return null;
		}
		return model.Tables.Find(translation.TableName)?.Hierarchies.Find(translation.HierarchyName);
	}

	private static NamedMetadataObject? FindLevel(Model model, ObjectTranslationBase translation)
	{
		if (string.IsNullOrWhiteSpace(translation.TableName) || string.IsNullOrWhiteSpace(translation.HierarchyName) || string.IsNullOrWhiteSpace(translation.LevelName))
		{
			return null;
		}
		return (model.Tables.Find(translation.TableName)?.Hierarchies.Find(translation.HierarchyName))?.Levels.Find(translation.LevelName);
	}

	private static NamedMetadataObject? FindKpi(Model model, ObjectTranslationBase translation)
	{
		if (string.IsNullOrWhiteSpace(translation.MeasureName))
		{
			return null;
		}
		if (string.IsNullOrWhiteSpace(translation.TableName))
		{
			foreach (Table table in model.Tables)
			{
				Measure measure = table.Measures.Find(translation.MeasureName);
				if (measure?.KPI != null)
				{
					return measure;
				}
			}
			return null;
		}
		Measure measure2 = model.Tables.Find(translation.TableName)?.Measures.Find(translation.MeasureName);
		if (measure2?.KPI == null)
		{
			return null;
		}
		return measure2;
	}

	private static ObjectTranslation GetOrCreateObjectTranslation(Culture culture, NamedMetadataObject targetObject, string property)
	{
		TranslatedProperty translatedProperty = property switch
		{
			"Caption" => TranslatedProperty.Caption, 
			"Description" => TranslatedProperty.Description, 
			"DisplayFolder" => TranslatedProperty.DisplayFolder, 
			_ => throw new McpExceptionWithSource("Invalid translated property: " + property, ErrorSource.System, "Invalid translated property supplied."), 
		};
		ObjectTranslation objectTranslation = culture.ObjectTranslations.FirstOrDefault((ObjectTranslation ot) => ot.Object == targetObject && ot.Property == translatedProperty);
		if (objectTranslation != null)
		{
			return objectTranslation;
		}
		ObjectTranslation objectTranslation2 = new ObjectTranslation
		{
			Object = targetObject,
			Property = translatedProperty
		};
		culture.ObjectTranslations.Add(objectTranslation2);
		return objectTranslation2;
	}

	public static async Task<List<ObjectTranslationList>> ListObjectTranslations(string? connectionName, string? cultureName = null, string? objectType = null, string? objectName = null)
	{
		List<ObjectTranslationList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<ObjectTranslationList> list = ListObjectTranslationsInternal(connectionInfo.Database, cultureName, objectType, objectName);
				AuditEvent.Default.Emit("list object translations", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list object translations", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<ObjectTranslationList> ListObjectTranslationsInternal(Database db, string? cultureName = null, string? objectType = null, string? objectName = null)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		Model model = db.Model;
		List<ObjectTranslationList> list = new List<ObjectTranslationList>();
		foreach (Culture item in string.IsNullOrEmpty(cultureName) ? model.Cultures.ToList() : new List<Culture> { model.Cultures.Find(cultureName) }.Where((Culture c) => c != null).ToList())
		{
			foreach (ObjectTranslation objectTranslation in item.ObjectTranslations)
			{
				if (objectTranslation.Object is NamedMetadataObject obj)
				{
					string objectTypeName = GetObjectTypeName(obj);
					string property = objectTranslation.Property.ToString();
					if (string.IsNullOrEmpty(objectType) || objectTypeName.Equals(objectType, StringComparison.OrdinalIgnoreCase))
					{
						Dictionary<string, string> objectIdentifiers = GetObjectIdentifiers(obj);
						list.Add(new ObjectTranslationList
						{
							CultureName = item.Name,
							ObjectType = objectTypeName,
							Property = property,
							Value = objectTranslation.Value,
							ObjectIdentifiers = objectIdentifiers
						});
					}
				}
			}
		}
		return (from r in list
			orderby r.CultureName, r.ObjectType, r.Property
			select r).ToList();
	}

	private static ObjectTranslationGet? GetObjectTranslationInternal(Database db, ObjectTranslationBase translation, DateTime lastUpdate)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		ValidateObjectTranslationDefinition(translation, isCreate: false);
		Model model = db.Model;
		Culture culture = model.Cultures.Find(translation.CultureName);
		if (culture == null)
		{
			return null;
		}
		NamedMetadataObject targetObject = FindTranslatableObject(model, translation);
		if (targetObject == null)
		{
			return null;
		}
		TranslatedProperty translatedProperty = translation.Property switch
		{
			"Caption" => TranslatedProperty.Caption, 
			"Description" => TranslatedProperty.Description, 
			"DisplayFolder" => TranslatedProperty.DisplayFolder, 
			_ => throw new McpExceptionWithSource("Invalid translated property: " + translation.Property, ErrorSource.System, "Invalid translated property supplied."), 
		};
		ObjectTranslation objectTranslation = culture.ObjectTranslations.FirstOrDefault((ObjectTranslation ot) => ot.Object == targetObject && ot.Property == translatedProperty);
		if (objectTranslation == null)
		{
			return null;
		}
		ObjectTranslationGet objectTranslationGet = new ObjectTranslationGet
		{
			CultureName = translation.CultureName,
			ObjectType = translation.ObjectType,
			Property = translation.Property,
			Value = objectTranslation.Value,
			ModifiedTime = lastUpdate
		};
		CopyIdentificationProperties(translation, objectTranslationGet);
		return objectTranslationGet;
	}

	internal static ObjectTranslationOperationResult CreateObjectTranslationInternal(IConnectionInfo connInfo, ObjectTranslationDefinition translationDef)
	{
		if (connInfo == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateObjectTranslationDefinition(translationDef, isCreate: true);
		try
		{
			Model model = connInfo.Database.Model;
			Culture culture = EnsureCultureExists(connInfo, translationDef.CultureName, translationDef.CreateCultureIfNotExists);
			NamedMetadataObject namedMetadataObject = FindTranslatableObject(model, translationDef);
			if (namedMetadataObject == null)
			{
				return new ObjectTranslationOperationResult
				{
					Success = false,
					ErrorMessage = "Object of type '" + translationDef.ObjectType + "' not found",
					CultureName = translationDef.CultureName,
					ObjectType = translationDef.ObjectType,
					ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
					Property = translationDef.Property
				};
			}
			GetOrCreateObjectTranslation(culture, namedMetadataObject, translationDef.Property).Value = translationDef.Value;
			TransactionOperations.RecordOperation(connInfo, $"Created translation for {translationDef.ObjectType} property '{translationDef.Property}' in culture '{translationDef.CultureName}'");
			ConnectionOperations.SaveChangesWithRollback(connInfo, "create object translation", OperationType.Create);
			return new ObjectTranslationOperationResult
			{
				Success = true,
				Message = $"Translation created successfully for {translationDef.ObjectType} property '{translationDef.Property}' in culture '{translationDef.CultureName}'",
				CultureName = translationDef.CultureName,
				ObjectType = translationDef.ObjectType,
				ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
				Property = translationDef.Property,
				Value = translationDef.Value
			};
		}
		catch (Exception ex)
		{
			return new ObjectTranslationOperationResult
			{
				Success = false,
				ErrorMessage = ex.Message,
				CultureName = translationDef.CultureName,
				ObjectType = translationDef.ObjectType,
				ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
				Property = translationDef.Property
			};
		}
	}

	private static ObjectTranslationOperationResult UpdateObjectTranslationInternal(IConnectionInfo connInfo, ObjectTranslationDefinition translationDef)
	{
		if (connInfo == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateObjectTranslationDefinition(translationDef, isCreate: false);
		try
		{
			Model model = connInfo.Database.Model;
			Culture culture = model.Cultures.Find(translationDef.CultureName);
			if (culture == null)
			{
				return new ObjectTranslationOperationResult
				{
					Success = false,
					ErrorMessage = "Culture '" + translationDef.CultureName + "' not found",
					CultureName = translationDef.CultureName,
					ObjectType = translationDef.ObjectType,
					ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
					Property = translationDef.Property
				};
			}
			NamedMetadataObject namedMetadataObject = FindTranslatableObject(model, translationDef);
			if (namedMetadataObject == null)
			{
				return new ObjectTranslationOperationResult
				{
					Success = false,
					ErrorMessage = "Object of type '" + translationDef.ObjectType + "' not found",
					CultureName = translationDef.CultureName,
					ObjectType = translationDef.ObjectType,
					ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
					Property = translationDef.Property
				};
			}
			GetOrCreateObjectTranslation(culture, namedMetadataObject, translationDef.Property).Value = translationDef.Value;
			TransactionOperations.RecordOperation(connInfo, $"Updated translation for {translationDef.ObjectType} property '{translationDef.Property}' in culture '{translationDef.CultureName}'");
			ConnectionOperations.SaveChangesWithRollback(connInfo, "update object translation", OperationType.Update);
			return new ObjectTranslationOperationResult
			{
				Success = true,
				Message = $"Translation updated successfully for {translationDef.ObjectType} property '{translationDef.Property}' in culture '{translationDef.CultureName}'",
				CultureName = translationDef.CultureName,
				ObjectType = translationDef.ObjectType,
				ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
				Property = translationDef.Property,
				Value = translationDef.Value
			};
		}
		catch (Exception ex)
		{
			return new ObjectTranslationOperationResult
			{
				Success = false,
				ErrorMessage = ex.Message,
				CultureName = translationDef.CultureName,
				ObjectType = translationDef.ObjectType,
				ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
				Property = translationDef.Property
			};
		}
	}

	private static ObjectTranslationOperationResult DeleteObjectTranslationInternal(IConnectionInfo connInfo, ObjectTranslationReference translationDef)
	{
		if (connInfo == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateObjectTranslationDefinition(translationDef, isCreate: false);
		try
		{
			Model model = connInfo.Database.Model;
			Culture culture = model.Cultures.Find(translationDef.CultureName);
			if (culture == null)
			{
				return new ObjectTranslationOperationResult
				{
					Success = false,
					ErrorMessage = "Culture '" + translationDef.CultureName + "' not found",
					CultureName = translationDef.CultureName,
					ObjectType = translationDef.ObjectType,
					ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
					Property = translationDef.Property
				};
			}
			NamedMetadataObject targetObject = FindTranslatableObject(model, translationDef);
			if (targetObject == null)
			{
				return new ObjectTranslationOperationResult
				{
					Success = false,
					ErrorMessage = "Object of type '" + translationDef.ObjectType + "' not found",
					CultureName = translationDef.CultureName,
					ObjectType = translationDef.ObjectType,
					ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
					Property = translationDef.Property
				};
			}
			TranslatedProperty translatedProperty = translationDef.Property switch
			{
				"Caption" => TranslatedProperty.Caption, 
				"Description" => TranslatedProperty.Description, 
				"DisplayFolder" => TranslatedProperty.DisplayFolder, 
				_ => throw new McpExceptionWithSource("Invalid translated property: " + translationDef.Property, ErrorSource.System, "Invalid translated property supplied."), 
			};
			ObjectTranslation objectTranslation = culture.ObjectTranslations.FirstOrDefault((ObjectTranslation ot) => ot.Object == targetObject && ot.Property == translatedProperty);
			if (objectTranslation == null)
			{
				return new ObjectTranslationOperationResult
				{
					Success = false,
					ErrorMessage = $"Translation for {translationDef.ObjectType} property '{translationDef.Property}' in culture '{translationDef.CultureName}' not found",
					CultureName = translationDef.CultureName,
					ObjectType = translationDef.ObjectType,
					ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
					Property = translationDef.Property
				};
			}
			culture.ObjectTranslations.Remove(objectTranslation);
			TransactionOperations.RecordOperation(connInfo, $"Deleted translation for {translationDef.ObjectType} property '{translationDef.Property}' in culture '{translationDef.CultureName}'");
			ConnectionOperations.SaveChangesWithRollback(connInfo, "delete object translation", OperationType.Delete);
			return new ObjectTranslationOperationResult
			{
				Success = true,
				Message = $"Translation deleted successfully for {translationDef.ObjectType} property '{translationDef.Property}' in culture '{translationDef.CultureName}'",
				CultureName = translationDef.CultureName,
				ObjectType = translationDef.ObjectType,
				ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
				Property = translationDef.Property
			};
		}
		catch (Exception ex)
		{
			return new ObjectTranslationOperationResult
			{
				Success = false,
				ErrorMessage = ex.Message,
				CultureName = translationDef.CultureName,
				ObjectType = translationDef.ObjectType,
				ObjectDisplayName = TranslationHelper.GetObjectDisplayName(translationDef),
				Property = translationDef.Property
			};
		}
	}

	public static async Task<BatchOperationResponse> CreateObjectTranslations(string? connectionName, List<ObjectTranslationDefinition> items, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, items, options, "Create", "Created", "object translations", (ObjectTranslationDefinition item) => $"{item.CultureName}.{item.ObjectType}.{item.Property}", delegate(BatchItemContext<ObjectTranslationDefinition> ctx)
		{
			ObjectTranslationOperationResult objectTranslationOperationResult = CreateObjectTranslationInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = objectTranslationOperationResult.Success;
			ctx.Result.Message = objectTranslationOperationResult.Message ?? objectTranslationOperationResult.ErrorMessage ?? "Unknown result";
			ctx.Result.Data = objectTranslationOperationResult;
		});
	}

	public static async Task<BatchOperationResponse> UpdateObjectTranslations(string? connectionName, List<ObjectTranslationDefinition> items, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, items, options, "Update", "Updated", "object translations", (ObjectTranslationDefinition item) => $"{item.CultureName}.{item.ObjectType}.{item.Property}", delegate(BatchItemContext<ObjectTranslationDefinition> ctx)
		{
			ObjectTranslationOperationResult objectTranslationOperationResult = UpdateObjectTranslationInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = objectTranslationOperationResult.Success;
			ctx.Result.Data = objectTranslationOperationResult;
			if (objectTranslationOperationResult.Success)
			{
				ctx.Result.Message = (objectTranslationOperationResult.HasChanges ? (objectTranslationOperationResult.Message ?? "Successfully updated object translation") : $"Object translation for {ctx.Item.CultureName}.{ctx.Item.ObjectType}.{ctx.Item.Property} updated (no changes detected)");
			}
			else
			{
				ctx.Result.Message = objectTranslationOperationResult.Message ?? objectTranslationOperationResult.ErrorMessage ?? "Failed to update object translation";
			}
		});
	}

	public static async Task<BatchOperationResponse> DeleteObjectTranslations(string? connectionName, List<ObjectTranslationReference> items, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, items, options, "Delete", "Deleted", "object translations", (ObjectTranslationReference item) => $"{item.CultureName}.{item.ObjectType}.{item.Property}", delegate(BatchItemContext<ObjectTranslationReference> ctx)
		{
			ObjectTranslationOperationResult objectTranslationOperationResult = DeleteObjectTranslationInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = objectTranslationOperationResult.Success;
			ctx.Result.Message = objectTranslationOperationResult.Message ?? objectTranslationOperationResult.ErrorMessage ?? "Unknown result";
			ctx.Result.Data = objectTranslationOperationResult;
		});
	}

	public static async Task<BatchOperationResponse> GetObjectTranslations(string? connectionName, List<ObjectTranslationReference> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>()
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No object translation identifiers provided for retrieval";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int totalItems = items.Count;
		int successCount = 0;
		int failureCount = 0;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					ObjectTranslationReference objectTranslationReference = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = $"{objectTranslationReference.CultureName}.{objectTranslationReference.ObjectType}.{objectTranslationReference.Property}"
					};
					try
					{
						ObjectTranslationGet objectTranslationInternal = GetObjectTranslationInternal(connectionInfo.Database, objectTranslationReference, connectionInfo.Database.LastUpdate);
						if (objectTranslationInternal != null)
						{
							itemResult.Success = true;
							itemResult.Message = "Object translation retrieved successfully";
							itemResult.Data = objectTranslationInternal;
							successCount++;
						}
						else
						{
							itemResult.Success = false;
							itemResult.Message = "Object translation not found";
							failureCount++;
							if (!options.ContinueOnError)
							{
								response.Success = false;
								response.Results.Add(itemResult);
								break;
							}
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
						if (!options.ContinueOnError)
						{
							response.Success = false;
							response.Results.Add(itemResult);
							break;
						}
					}
					response.Results.Add(itemResult);
				}
				response.Success = failureCount == 0;
				if (string.IsNullOrEmpty(response.Message))
				{
					response.Message = $"Processed {totalItems} object translation(s): {successCount} succeeded, {failureCount} failed";
				}
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Batch operation failed: " + ex2.Message;
			}
			stopwatch.Stop();
			AuditEvent.Default.Emit("get object translations", response.Success, OperationType.Read, connectionInfo);
			response.Summary = new BatchSummary
			{
				TotalItems = totalItems,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	private static string GetObjectTypeName(NamedMetadataObject obj)
	{
		if (!(obj is Model))
		{
			if (!(obj is Table))
			{
				if (!(obj is Measure))
				{
					if (!(obj is Column))
					{
						if (!(obj is Hierarchy))
						{
							if (obj is Level)
							{
								return "Level";
							}
							return "Unknown";
						}
						return "Hierarchy";
					}
					return "Column";
				}
				return "Measure";
			}
			return "Table";
		}
		return "Model";
	}

	private static Dictionary<string, string> GetObjectIdentifiers(NamedMetadataObject obj)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		if (!(obj is Model model))
		{
			if (!(obj is Table table))
			{
				if (!(obj is Measure measure))
				{
					if (!(obj is Column column))
					{
						if (!(obj is Hierarchy hierarchy))
						{
							if (obj is Level level)
							{
								dictionary["LevelName"] = level.Name;
								if (level.Hierarchy?.Table != null)
								{
									dictionary["TableName"] = level.Hierarchy.Table.Name;
									dictionary["HierarchyName"] = level.Hierarchy.Name;
								}
							}
						}
						else
						{
							dictionary["HierarchyName"] = hierarchy.Name;
							if (hierarchy.Table != null)
							{
								dictionary["TableName"] = hierarchy.Table.Name;
							}
						}
					}
					else
					{
						dictionary["ColumnName"] = column.Name;
						if (column.Table != null)
						{
							dictionary["TableName"] = column.Table.Name;
						}
					}
				}
				else
				{
					dictionary["MeasureName"] = measure.Name;
					if (measure.Table != null)
					{
						dictionary["TableName"] = measure.Table.Name;
					}
				}
			}
			else
			{
				dictionary["TableName"] = table.Name;
			}
		}
		else
		{
			dictionary["ModelName"] = model.Name ?? "";
		}
		return dictionary;
	}

	private static void CopyIdentificationProperties(ObjectTranslationBase source, ObjectTranslationBase target)
	{
		target.ModelName = source.ModelName;
		target.TableName = source.TableName;
		target.MeasureName = source.MeasureName;
		target.ColumnName = source.ColumnName;
		target.HierarchyName = source.HierarchyName;
		target.LevelName = source.LevelName;
	}
}
