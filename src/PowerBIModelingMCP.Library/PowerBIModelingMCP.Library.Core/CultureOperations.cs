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

public static class CultureOperations
{
	public static void ValidateCultureDefinition(CultureBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture name is required", ErrorSource.User);
		}
		if (!IsValidCultureName(def.Name))
		{
			throw new McpExceptionWithSource("Invalid culture name format: " + def.Name + ". Expected format like 'en-US', 'fr-FR', etc.", ErrorSource.User, "Invalid culture name format supplied. Expected format like 'en-US', 'fr-FR', etc.");
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
	}

	public static List<string> GetValidCultureNames(bool includeNeutralCultures = true, bool includeUserCustomCultures = false)
	{
		CultureTypes cultureTypes = CultureTypes.SpecificCultures;
		if (includeNeutralCultures)
		{
			cultureTypes |= CultureTypes.NeutralCultures;
		}
		if (includeUserCustomCultures)
		{
			cultureTypes |= CultureTypes.UserCustomCulture;
		}
		return (from c in CultureInfo.GetCultures(cultureTypes)
			where !string.IsNullOrEmpty(c.Name)
			select c.Name into name
			orderby name
			select name).ToList();
	}

	public static async Task<List<CultureList>> ListCultures(string? connectionName = null)
	{
		List<CultureList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<CultureList> list = ListCulturesInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list cultures", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list cultures", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<CultureList> ListCulturesInternal(Database db)
	{
		return (from c in db.Model.Cultures.Where((Culture c) => !c.IsRemoved).Select(delegate(Culture c)
			{
				int num = 0;
				try
				{
					num = new CultureInfo(c.Name).LCID;
				}
				catch
				{
					num = 0;
				}
				return new CultureList
				{
					Name = c.Name,
					LCID = num,
					TranslationCount = (c.ObjectTranslations?.Count ?? 0)
				};
			})
			orderby c.Name
			select c).ToList();
	}

	private static CultureGet GetCultureInternal(Database db, string cultureName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		ValidationHelpers.ValidateObjectName(cultureName, "cultureName");
		return MapCultureToGet(db.Model.Cultures.Find(cultureName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture '" + cultureName + "' not found", ErrorSource.User));
	}

	internal static OperationResult CreateCultureInternal(IConnectionInfo info, CultureDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateCultureDefinition(def, isCreate: true);
		if (info.Database.Model.Cultures.Find(def.Name) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture '" + def.Name + "' already exists", ErrorSource.User);
		}
		Culture culture = new Culture
		{
			Name = def.Name
		};
		if (def.Annotations != null)
		{
			ApplyAnnotations(culture, def.Annotations);
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToCulture(culture, def.ExtendedProperties);
		}
		info.Database.Model.Cultures.Add(culture);
		TransactionOperations.RecordOperation(info, "Created culture '" + def.Name + "' in model " + info.Database.Model.Name);
		ConnectionOperations.SaveChangesWithRollback(info, "create culture", OperationType.Create);
		return OperationResult.CreateSuccess("Culture '" + def.Name + "' created successfully", def.Name, ObjectType.Culture, Operation.Create);
	}

	private static OperationResult UpdateCultureInternal(IConnectionInfo info, CultureDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateCultureDefinition(update, isCreate: false);
		Culture culture = info.Database.Model.Cultures.Find(update.Name);
		if (culture == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture '" + update.Name + "' not found", ErrorSource.User);
		}
		bool flag = false;
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(culture, update.Annotations, (Culture c) => c.Annotations))
		{
			flag = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool flag2 = culture.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceExtendedProperties(culture, update.ExtendedProperties, (Culture c) => c.ExtendedProperties);
			if (flag2 || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		if (!flag)
		{
			return OperationResult.CreateSuccess("Culture '" + update.Name + "' is already in the requested state", update.Name, ObjectType.Culture, Operation.Update, hasChanges: false);
		}
		TransactionOperations.RecordOperation(info, "Updated culture '" + update.Name + "' in model " + info.Database.Model.Name);
		ConnectionOperations.SaveChangesWithRollback(info, "update culture", OperationType.Update);
		return OperationResult.CreateSuccess("Culture '" + update.Name + "' updated successfully", update.Name, ObjectType.Culture, Operation.Update);
	}

	private static OperationResult DeleteCultureInternal(IConnectionInfo info, string cultureName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidationHelpers.ValidateObjectName(cultureName, "cultureName");
		Culture culture = info.Database.Model.Cultures.Find(cultureName);
		if (culture == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture '" + cultureName + "' not found", ErrorSource.User);
		}
		info.Database.Model.Cultures.Remove(culture);
		TransactionOperations.RecordOperation(info, "Deleted culture '" + cultureName + "' from model " + info.Database.Model.Name);
		ConnectionOperations.SaveChangesWithRollback(info, "delete culture", OperationType.Delete);
		return OperationResult.CreateSuccess("Culture '" + cultureName + "' deleted successfully", cultureName, ObjectType.Culture, Operation.Delete);
	}

	private static OperationResult RenameCultureInternal(IConnectionInfo info, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidationHelpers.ValidateObjectName(oldName, "oldName");
		ValidationHelpers.ValidateObjectName(newName, "newName");
		Culture obj = info.Database.Model.Cultures.Find(oldName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture '" + oldName + "' not found", ErrorSource.User);
		if (info.Database.Model.Cultures.Find(newName) != null && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture '" + newName + "' already exists", ErrorSource.User);
		}
		obj.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed culture from '{oldName}' to '{newName}' in model {info.Database.Model.Name}");
		ConnectionOperations.SaveChangesWithRollback(info, "rename culture", OperationType.Update, CheckpointMode.AfterRequestRename);
		return OperationResult.CreateSuccess($"Culture renamed from '{oldName}' to '{newName}' successfully", newName, ObjectType.Culture, Operation.Update);
	}

	public static List<CultureDetails> GetValidCultureDetails(bool includeNeutralCultures = true, bool includeUserCustomCultures = false)
	{
		CultureTypes cultureTypes = CultureTypes.SpecificCultures;
		if (includeNeutralCultures)
		{
			cultureTypes |= CultureTypes.NeutralCultures;
		}
		if (includeUserCustomCultures)
		{
			cultureTypes |= CultureTypes.UserCustomCulture;
		}
		return (from c in CultureInfo.GetCultures(cultureTypes)
			where !string.IsNullOrEmpty(c.Name)
			select new CultureDetails
			{
				Name = c.Name,
				LCID = c.LCID,
				DisplayName = c.DisplayName,
				EnglishName = c.EnglishName,
				IsNeutralCulture = c.IsNeutralCulture,
				IsUserCustomCulture = ((c.CultureTypes & CultureTypes.UserCustomCulture) != 0)
			} into ci
			orderby ci.Name
			select ci).ToList();
	}

	public static CultureDetails? GetCultureDetailsByName(string cultureName)
	{
		if (string.IsNullOrWhiteSpace(cultureName))
		{
			return null;
		}
		try
		{
			CultureInfo cultureInfo = new CultureInfo(cultureName);
			return new CultureDetails
			{
				Name = cultureInfo.Name,
				LCID = cultureInfo.LCID,
				DisplayName = cultureInfo.DisplayName,
				EnglishName = cultureInfo.EnglishName,
				IsNeutralCulture = cultureInfo.IsNeutralCulture,
				IsUserCustomCulture = ((cultureInfo.CultureTypes & CultureTypes.UserCustomCulture) != 0)
			};
		}
		catch (CultureNotFoundException)
		{
			return null;
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	public static CultureDetails? GetCultureDetailsByLCID(int lcid)
	{
		try
		{
			CultureInfo cultureInfo = new CultureInfo(lcid);
			return new CultureDetails
			{
				Name = cultureInfo.Name,
				LCID = cultureInfo.LCID,
				DisplayName = cultureInfo.DisplayName,
				EnglishName = cultureInfo.EnglishName,
				IsNeutralCulture = cultureInfo.IsNeutralCulture,
				IsUserCustomCulture = ((cultureInfo.CultureTypes & CultureTypes.UserCustomCulture) != 0)
			};
		}
		catch (CultureNotFoundException)
		{
			return null;
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	private static bool IsValidCultureName(string cultureName)
	{
		if (string.IsNullOrWhiteSpace(cultureName))
		{
			return false;
		}
		try
		{
			new CultureInfo(cultureName);
			return true;
		}
		catch (CultureNotFoundException)
		{
			return false;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	private static CultureGet MapCultureToGet(Culture culture)
	{
		CultureGet cultureGet = new CultureGet
		{
			Name = culture.Name,
			ModifiedTime = culture.ModifiedTime,
			StructureModifiedTime = culture.StructureModifiedTime,
			IsRemoved = culture.IsRemoved,
			Annotations = new List<KeyValuePair<string, string>>(),
			ExtendedProperties = new List<PowerBIModelingMCP.Library.Common.DataStructures.ExtendedProperty>()
		};
		foreach (Annotation annotation in culture.Annotations)
		{
			cultureGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		cultureGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromCulture(culture);
		cultureGet.LinguisticMetadataReference = null;
		cultureGet.ObjectTranslationReferences.Clear();
		return cultureGet;
	}

	private static void ApplyAnnotations(Culture culture, List<KeyValuePair<string, string>> annotations)
	{
		foreach (KeyValuePair<string, string> annotation in annotations)
		{
			if (!string.IsNullOrWhiteSpace(annotation.Key))
			{
				Annotation metadataObject = new Annotation
				{
					Name = annotation.Key,
					Value = (annotation.Value ?? string.Empty)
				};
				culture.Annotations.Add(metadataObject);
			}
		}
	}

	public static async Task<string> ExportTMDL(string? connectionName, string cultureName, ExportTmdl options)
	{
		if (string.IsNullOrEmpty(cultureName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture name cannot be null or empty", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string item = ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(connectionInfo.Database.Model.Cultures.Find(cultureName) ?? throw new ArgumentException("Culture '" + cultureName + "' not found"), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
				AuditEvent.Default.Emit("export culture to TMDL", success: true, OperationType.Read, connectionInfo);
				result = item;
			}
			catch
			{
				AuditEvent.Default.Emit("export culture to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> CreateCultures(string? connectionName, List<CultureDefinition> cultures, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, cultures, options, "Create", "Created", "cultures", (CultureDefinition item) => item.Name, delegate(BatchItemContext<CultureDefinition> ctx)
		{
			OperationResult operationResult = CreateCultureInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = operationResult.Success;
			ctx.Result.Message = (ctx.Result.Success ? ("Successfully created culture '" + ctx.Item.Name + "'") : ("Failed to create culture '" + ctx.Item.Name + "': " + operationResult.Message));
			if (ctx.Result.Success && ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Created culture '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> UpdateCultures(string? connectionName, List<CultureDefinition> cultures, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, cultures, options, "Update", "Updated", "cultures", (CultureDefinition item) => item.Name, delegate(BatchItemContext<CultureDefinition> ctx)
		{
			OperationResult operationResult = UpdateCultureInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = operationResult.Success;
			ctx.Result.Message = (operationResult.HasChanges ? ("Successfully updated culture '" + ctx.Item.Name + "'") : ("Culture '" + ctx.Item.Name + "' updated (no changes detected)"));
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Updated culture '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> DeleteCultures(string? connectionName, List<CultureReference> cultures, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, cultures, options, "Delete", "Deleted", "cultures", (CultureReference item) => item.Name, delegate(BatchItemContext<CultureReference> ctx)
		{
			DeleteCultureInternal(ctx.Connection, ctx.Item.Name);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully deleted culture '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Deleted culture '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetCultures(string? connectionName, List<CultureReference> cultures, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (cultures == null || !cultures.Any())
		{
			response.Success = false;
			response.Message = "No cultures provided for retrieval";
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int successCount = 0;
		int failureCount = 0;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < cultures.Count; i++)
				{
					CultureReference cultureReference = cultures[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = cultureReference.Name
					};
					try
					{
						CultureGet cultureInternal = GetCultureInternal(connectionInfo.Database, cultureReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved culture '" + cultureReference.Name + "'";
						itemResult.Data = cultureInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving culture '" + cultureReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				response.Success = failureCount == 0;
				response.Message = $"Processed {cultures.Count} culture(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = cultures.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get cultures", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = cultures.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameCultures(string? connectionName, List<CultureRename> cultures, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, cultures, options, "Rename", "Renamed", "cultures", (CultureRename item) => item.CurrentName, delegate(BatchItemContext<CultureRename> ctx)
		{
			RenameCultureInternal(ctx.Connection, ctx.Item.CurrentName, ctx.Item.NewName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed culture '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed culture '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'");
			}
		});
	}
}
