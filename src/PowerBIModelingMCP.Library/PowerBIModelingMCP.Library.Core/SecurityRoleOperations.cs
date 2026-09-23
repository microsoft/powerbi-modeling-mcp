using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class SecurityRoleOperations
{
	internal static PostCommitDaxValidator.Target? ResolveTablePermissionForValidation(IConnectionInfo conn, TablePermissionDefinition def)
	{
		if (def == null || string.IsNullOrEmpty(def.RoleName) || string.IsNullOrEmpty(def.TableName) || string.IsNullOrWhiteSpace(def.FilterExpression))
		{
			return null;
		}
		Database database = conn?.Database;
		if (database == null)
		{
			return null;
		}
		ModelRole modelRole = database.Model.Roles.Find(def.RoleName);
		if (modelRole == null)
		{
			return null;
		}
		Table table = database.Model.Tables.Find(def.TableName);
		if (table == null)
		{
			return null;
		}
		TablePermission tablePermission = modelRole.TablePermissions.FirstOrDefault((TablePermission tp) => tp.Table == table);
		if (tablePermission == null)
		{
			return null;
		}
		List<PostCommitDaxValidator.Check> checks = new List<PostCommitDaxValidator.Check>
		{
			new PostCommitDaxValidator.Check(string.Empty, tablePermission.State.ToString(), tablePermission.ErrorMessage)
		};
		return new PostCommitDaxValidator.Target("Row-level security filter", $"on table '{table.Name}' in role '{modelRole.Name}'", checks);
	}

	public static async Task<List<ModelRoleList>> ListModelRoles(string? connectionName)
	{
		List<ModelRoleList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<ModelRoleList> list = ListModelRolesInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list security roles", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list security roles", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<ModelRoleList> ListModelRolesInternal(Database db)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		List<ModelRoleList> list = new List<ModelRoleList>();
		foreach (ModelRole role in db.Model.Roles)
		{
			list.Add(new ModelRoleList
			{
				Name = role.Name,
				Description = ((!string.IsNullOrEmpty(role.Description)) ? role.Description : null),
				ModelPermission = role.ModelPermission.ToString(),
				TableNames = ((role.TablePermissions.Count > 0) ? (from tp in role.TablePermissions
					select tp.Table?.Name into name
					where !string.IsNullOrEmpty(name)
					select name).Cast<string>().ToList() : null)
			});
		}
		return list;
	}

	public static async Task<ModelRoleGet> GetModelRole(string? connectionName, string roleName)
	{
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		ModelRoleGet modelRoleInternal;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			modelRoleInternal = GetModelRoleInternal(connectionInfo.Database, roleName);
		}
		return modelRoleInternal;
	}

	private static ModelRoleGet GetModelRoleInternal(Database db, string roleName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		ModelRole modelRole = db.Model.Roles.Find(roleName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Model role '" + roleName + "' not found", ErrorSource.User);
		ModelRoleGet modelRoleGet = new ModelRoleGet
		{
			Name = modelRole.Name,
			Description = modelRole.Description,
			ModelPermission = modelRole.ModelPermission.ToString(),
			TablePermissions = new List<Dictionary<string, string>>(),
			Annotations = new List<KeyValuePair<string, string>>(),
			ExtendedProperties = new List<PowerBIModelingMCP.Library.Common.DataStructures.ExtendedProperty>()
		};
		foreach (TablePermission tablePermission in modelRole.TablePermissions)
		{
			modelRoleGet.TablePermissions.Add(new Dictionary<string, string>
			{
				["TableName"] = tablePermission.Table?.Name ?? "",
				["FilterExpression"] = tablePermission.FilterExpression ?? "",
				["MetadataPermission"] = tablePermission.MetadataPermission.ToString()
			});
		}
		foreach (Annotation annotation in modelRole.Annotations)
		{
			modelRoleGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		modelRoleGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromModelRole(modelRole);
		return modelRoleGet;
	}

	private static void CreateModelRoleInternal(Database db, ModelRoleDefinition def)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ModelRoleDefinition cannot be null", ErrorSource.User);
		}
		if (db.Model.Roles.Contains(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Model role '" + def.Name + "' already exists", ErrorSource.User);
		}
		ModelRole modelRole = new ModelRole
		{
			Name = def.Name
		};
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			modelRole.Description = def.Description;
		}
		if (!string.IsNullOrWhiteSpace(def.ModelPermission))
		{
			if (!Enum.TryParse<ModelPermission>(def.ModelPermission, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(ModelPermission));
				throw new McpExceptionWithSource("Invalid ModelPermission '" + def.ModelPermission + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid ModelPermission supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			modelRole.ModelPermission = result;
		}
		else
		{
			modelRole.ModelPermission = ModelPermission.Read;
		}
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				modelRole.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToModelRole(modelRole, def.ExtendedProperties);
		}
		db.Model.Roles.Add(modelRole);
	}

	private static OperationResult UpdateModelRoleInternal(Database db, ModelRoleDefinition update)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ModelRoleDefinition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(update.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required to identify the role to update", ErrorSource.User);
		}
		ModelRole modelRole = db.Model.Roles.Find(update.Name) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Role '" + update.Name + "' not found", ErrorSource.User);
		bool flag = false;
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (modelRole.Description != text)
			{
				modelRole.Description = text;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.ModelPermission))
		{
			if (!Enum.TryParse<ModelPermission>(update.ModelPermission, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(ModelPermission));
				throw new McpExceptionWithSource("Invalid ModelPermission '" + update.ModelPermission + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid ModelPermission supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			if (modelRole.ModelPermission != result)
			{
				modelRole.ModelPermission = result;
				flag = true;
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(modelRole, update.Annotations, (ModelRole obj) => obj.Annotations))
		{
			flag = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num = modelRole.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceExtendedProperties(modelRole, update.ExtendedProperties, (ModelRole obj) => obj.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		if (!flag)
		{
			return OperationResult.CreateSuccess("Role '" + update.Name + "' is already in the requested state", update.Name, ObjectType.SecurityRole, Operation.Update, hasChanges: false);
		}
		return OperationResult.CreateSuccess("Role '" + update.Name + "' updated successfully", update.Name, ObjectType.SecurityRole, Operation.Update);
	}

	private static void DeleteModelRoleInternal(Database db, string roleName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		ModelRole metadataObject = db.Model.Roles.Find(roleName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Role '" + roleName + "' not found", ErrorSource.User);
		db.Model.Roles.Remove(metadataObject);
	}

	private static void RenameModelRoleInternal(IConnectionInfo info, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		Database database = info.Database;
		ModelRole modelRole = database.Model.Roles.Find(oldName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Model role '" + oldName + "' not found", ErrorSource.User);
		if (database.Model.Roles.Contains(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Model role '" + newName + "' already exists", ErrorSource.User);
		}
		modelRole.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed model role from '{oldName}' to '{newName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename model role", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	private static TablePermissionOperationResult CreateTablePermissionInternal(Database db, TablePermissionDefinition def)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TablePermissionDefinition cannot be null", ErrorSource.User);
		}
		ValidateTablePermissionDefinition(def, isCreate: true);
		ModelRole modelRole = db.Model.Roles.Find(def.RoleName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Role '" + def.RoleName + "' not found", ErrorSource.User);
		Table table = db.Model.Tables.Find(def.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + def.TableName + "' not found", ErrorSource.User);
		if (modelRole.TablePermissions.FirstOrDefault((TablePermission tp) => tp.Table == table) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table permission already exists for table '{def.TableName}' in role '{def.RoleName}'", ErrorSource.User);
		}
		TablePermission tablePermission = new TablePermission
		{
			Table = table
		};
		if (!string.IsNullOrWhiteSpace(def.FilterExpression))
		{
			tablePermission.FilterExpression = def.FilterExpression;
		}
		if (!string.IsNullOrWhiteSpace(def.MetadataPermission))
		{
			tablePermission.MetadataPermission = Enum.Parse<MetadataPermission>(def.MetadataPermission, ignoreCase: true);
		}
		else
		{
			tablePermission.MetadataPermission = MetadataPermission.Default;
		}
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				tablePermission.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToTablePermission(tablePermission, def.ExtendedProperties);
		}
		modelRole.TablePermissions.Add(tablePermission);
		return new TablePermissionOperationResult
		{
			State = tablePermission.State.ToString(),
			ErrorMessage = tablePermission.ErrorMessage,
			RoleName = def.RoleName,
			TableName = def.TableName
		};
	}

	private static TablePermissionOperationResult UpdateTablePermissionInternal(Database db, TablePermissionDefinition update)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TablePermissionDefinition cannot be null", ErrorSource.User);
		}
		ModelRole obj = db.Model.Roles.Find(update.RoleName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Role '" + update.RoleName + "' not found", ErrorSource.User);
		Table table = db.Model.Tables.Find(update.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + update.TableName + "' not found", ErrorSource.User);
		TablePermission tablePermission = obj.TablePermissions.FirstOrDefault((TablePermission tp) => tp.Table == table) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table permission not found for table '{update.TableName}' in role '{update.RoleName}'", ErrorSource.User);
		bool flag = false;
		if (update.FilterExpression != null)
		{
			string text = (string.IsNullOrEmpty(update.FilterExpression) ? null : update.FilterExpression);
			if (tablePermission.FilterExpression != text)
			{
				tablePermission.FilterExpression = text;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.MetadataPermission))
		{
			if (!Enum.TryParse<MetadataPermission>(update.MetadataPermission, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(MetadataPermission));
				throw new McpExceptionWithSource("Invalid MetadataPermission '" + update.MetadataPermission + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid MetadataPermission supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			if (tablePermission.MetadataPermission != result)
			{
				tablePermission.MetadataPermission = result;
				flag = true;
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(tablePermission, update.Annotations, (TablePermission tablePermission2) => tablePermission2.Annotations))
		{
			flag = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num = tablePermission.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceExtendedProperties(tablePermission, update.ExtendedProperties, (TablePermission tablePermission2) => tablePermission2.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		if (!flag)
		{
			return new TablePermissionOperationResult
			{
				State = tablePermission.State.ToString(),
				ErrorMessage = tablePermission.ErrorMessage,
				RoleName = update.RoleName,
				TableName = update.TableName,
				HasChanges = false
			};
		}
		return new TablePermissionOperationResult
		{
			State = tablePermission.State.ToString(),
			ErrorMessage = tablePermission.ErrorMessage,
			RoleName = update.RoleName,
			TableName = update.TableName,
			HasChanges = true
		};
	}

	private static void DeleteTablePermissionInternal(Database db, string roleName, string tableName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		ModelRole obj = db.Model.Roles.Find(roleName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Role '" + roleName + "' not found", ErrorSource.User);
		Table table = db.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		TablePermission metadataObject = obj.TablePermissions.FirstOrDefault((TablePermission tp) => tp.Table == table) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table permission not found for table '{tableName}' in role '{roleName}'", ErrorSource.User);
		obj.TablePermissions.Remove(metadataObject);
	}

	public static async Task<TmdlExportResult> ExportTMDL(string? connectionName, string roleName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		TmdlExportResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string content = TmdlSerializer.SerializeObject(connectionInfo.Database.Model.Roles.Find(roleName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Model role '" + roleName + "' not found", ErrorSource.User), options.SerializationOptions.ToMetadataSerializationOptions());
				(string Content, bool IsTruncated, string? SavedFilePath, List<string> Warnings) tuple = ExportContentProcessor.ProcessExportContent(content, options);
				string item = tuple.Content;
				bool item2 = tuple.IsTruncated;
				string item3 = tuple.SavedFilePath;
				List<string> item4 = tuple.Warnings;
				TmdlExportResult tmdlExportResult = TmdlExportResult.CreateSuccess(roleName, "Role", content, item, item2, item3, item4, options);
				AuditEvent.Default.Emit("export security role to TMDL", success: true, OperationType.Read, connectionInfo);
				result = tmdlExportResult;
			}
			catch (Exception ex)
			{
				AuditEvent.Default.Emit("export security role to TMDL", success: false, OperationType.Read, connectionInfo);
				result = TmdlExportResult.CreateFailure(roleName, "Role", ex.Message, (ex is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : ErrorSource.System);
			}
		}
		return result;
	}

	public static async Task<TmslExportResult> ExportTMSL(string? connectionName, string roleName, ExportTmsl tmslOptions)
	{
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		if (tmslOptions == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tmslOptions is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tmslOptions.TmslOperationType))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TmslOperationType is required in tmslOptions", ErrorSource.User);
		}
		TmslExportResult result2;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				ModelRole metadataObject = connectionInfo.Database.Model.Roles.Find(roleName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Model role '" + roleName + "' not found", ErrorSource.User);
				if (!Enum.TryParse<TmslOperationType>(tmslOptions.TmslOperationType, ignoreCase: true, out var result))
				{
					Enum.GetNames(typeof(TmslOperationType));
					throw new McpExceptionWithSource("Invalid TmslOperationType '" + tmslOptions.TmslOperationType + "'. Valid values for roles: Create, CreateOrReplace, Alter, Delete (Refresh not supported)", ErrorSource.User, "Invalid TmslOperationType supplied. Valid values for roles: Create, CreateOrReplace, Alter, Delete (Refresh not supported).");
				}
				if (result == TmslOperationType.Refresh)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Refresh operations are not supported for roles. Valid operations: Create, CreateOrReplace, Alter, Delete", ErrorSource.User);
				}
				TmslExportResult tmslExportResult = TmslExportResult.FromLegacyResult(TmslScriptingService.GenerateScript(options: new TmslOperationRequest
				{
					OperationType = result,
					IncludeRestricted = (tmslOptions.IncludeRestricted == true)
				}, metadataObject: metadataObject, operationType: result));
				(string Content, bool IsTruncated, string? SavedFilePath, List<string> Warnings) tuple = ExportContentProcessor.ProcessExportContent(tmslExportResult.Content, tmslOptions);
				string item = tuple.Content;
				bool item2 = tuple.IsTruncated;
				string item3 = tuple.SavedFilePath;
				List<string> item4 = tuple.Warnings;
				tmslExportResult.Content = item;
				tmslExportResult.IsTruncated = item2;
				tmslExportResult.SavedFilePath = item3;
				tmslExportResult.Warnings.AddRange(item4);
				tmslExportResult.AppliedOptions = tmslOptions;
				AuditEvent.Default.Emit("export security role to TMSL", success: true, OperationType.Read, connectionInfo);
				result2 = tmslExportResult;
			}
			catch (Exception ex)
			{
				AuditEvent.Default.Emit("export security role to TMSL", success: false, OperationType.Read, connectionInfo);
				result2 = TmslExportResult.CreateFailure(roleName, "Role", tmslOptions.TmslOperationType, ex.Message, (ex is McpExceptionWithSource mcpExceptionWithSource) ? mcpExceptionWithSource.ErrorSource : ErrorSource.System);
			}
		}
		return result2;
	}

	public static async Task CreateModelRole(string? connectionName, ModelRoleDefinition def)
	{
		ValidateModelRoleDefinition(def, isCreate: true);
		await using IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName);
		CreateModelRoleInternal(connectionInfo.Database, def);
		TransactionOperations.RecordOperation(connectionInfo, "Created model role '" + def.Name + "'");
		ConnectionOperations.SaveChangesWithRollback(connectionInfo, "create model role", OperationType.Create);
	}

	public static async Task<OperationResult> UpdateModelRole(string? connectionName, ModelRoleDefinition update)
	{
		ValidateModelRoleDefinition(update, isCreate: false);
		OperationResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			OperationResult operationResult = UpdateModelRoleInternal(connectionInfo.Database, update);
			if (operationResult.HasChanges)
			{
				TransactionOperations.RecordOperation(connectionInfo, "Updated role '" + update.Name + "'");
				ConnectionOperations.SaveChangesWithRollback(connectionInfo, "update role", OperationType.Update);
			}
			result = operationResult;
		}
		return result;
	}

	public static async Task DeleteModelRole(string? connectionName, string roleName)
	{
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		await using IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName);
		DeleteModelRoleInternal(connectionInfo.Database, roleName);
		TransactionOperations.RecordOperation(connectionInfo, "Deleted role '" + roleName + "'");
		ConnectionOperations.SaveChangesWithRollback(connectionInfo, "delete role", OperationType.Delete);
	}

	public static async Task RenameModelRole(string? connectionName, string oldName, string newName)
	{
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		RenameModelRoleInternal(info, oldName, newName);
		TransactionOperations.RecordOperation(info, $"Renamed model role from '{oldName}' to '{newName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename model role", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	public static async Task<TablePermissionOperationResult> CreateTablePermission(string? connectionName, TablePermissionDefinition def)
	{
		ValidateTablePermissionDefinition(def, isCreate: true);
		TablePermissionOperationResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TablePermissionOperationResult tablePermissionOperationResult = CreateTablePermissionInternal(connectionInfo.Database, def);
			TransactionOperations.RecordOperation(connectionInfo, $"Created table permission for '{def.TableName}' in role '{def.RoleName}'");
			ConnectionOperations.SaveChangesWithRollback(connectionInfo, "create table permission", OperationType.Create);
			result = tablePermissionOperationResult;
		}
		return result;
	}

	public static async Task<TablePermissionOperationResult> UpdateTablePermission(string? connectionName, TablePermissionDefinition update)
	{
		ValidateTablePermissionDefinition(update, isCreate: false);
		TablePermissionOperationResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TablePermissionOperationResult tablePermissionOperationResult = UpdateTablePermissionInternal(connectionInfo.Database, update);
			if (tablePermissionOperationResult.HasChanges)
			{
				TransactionOperations.RecordOperation(connectionInfo, $"Updated table permission for '{update.TableName}' in role '{update.RoleName}'");
				ConnectionOperations.SaveChangesWithRollback(connectionInfo, "update table permission", OperationType.Update);
			}
			result = tablePermissionOperationResult;
		}
		return result;
	}

	public static async Task DeleteTablePermission(string? connectionName, string roleName, string tableName)
	{
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		await using IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName);
		DeleteTablePermissionInternal(connectionInfo.Database, roleName, tableName);
		TransactionOperations.RecordOperation(connectionInfo, $"Deleted table permission for '{tableName}' in role '{roleName}'");
		ConnectionOperations.SaveChangesWithRollback(connectionInfo, "delete table permission", OperationType.Delete);
	}

	public static async Task<List<Dictionary<string, string>>> GetTablePermissions(string? connectionName, string roleName)
	{
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		List<Dictionary<string, string>> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				ModelRole obj = connectionInfo.Database.Model.Roles.Find(roleName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Role '" + roleName + "' not found", ErrorSource.User);
				List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
				foreach (TablePermission tablePermission in obj.TablePermissions)
				{
					list.Add(new Dictionary<string, string>
					{
						["TableName"] = tablePermission.Table?.Name ?? "",
						["FilterExpression"] = tablePermission.FilterExpression ?? "",
						["MetadataPermission"] = tablePermission.MetadataPermission.ToString()
					});
				}
				AuditEvent.Default.Emit("list table permissions", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list table permissions", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static TablePermissionGet GetTablePermissionInternal(IConnectionInfo info, string roleName, string tableName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(roleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("roleName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		Database database = info.Database;
		ModelRole obj = database.Model.Roles.Find(roleName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Role '" + roleName + "' not found", ErrorSource.User);
		Table table = database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		TablePermission tablePermission = obj.TablePermissions.FirstOrDefault((TablePermission tp) => tp.Table == table) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Table permission not found for table '{tableName}' in role '{roleName}'", ErrorSource.User);
		TablePermissionGet tablePermissionGet = new TablePermissionGet
		{
			RoleName = roleName,
			TableName = tableName,
			FilterExpression = tablePermission.FilterExpression,
			MetadataPermission = tablePermission.MetadataPermission.ToString(),
			State = tablePermission.State.ToString(),
			ErrorMessage = tablePermission.ErrorMessage,
			ModifiedTime = tablePermission.ModifiedTime,
			Annotations = new List<KeyValuePair<string, string>>(),
			ExtendedProperties = new List<PowerBIModelingMCP.Library.Common.DataStructures.ExtendedProperty>()
		};
		foreach (Annotation annotation in tablePermission.Annotations)
		{
			tablePermissionGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		tablePermissionGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromTablePermission(tablePermission);
		return tablePermissionGet;
	}

	public static async Task<List<Dictionary<string, object>>> GetEffectivePermissions(string? connectionName)
	{
		List<Dictionary<string, object>> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				Database database = connectionInfo.Database;
				List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
				foreach (ModelRole role in database.Model.Roles)
				{
					List<string> list2 = new List<string>();
					List<string> list3 = new List<string>();
					Dictionary<string, object> item = new Dictionary<string, object>
					{
						["RoleName"] = role.Name,
						["ModelPermission"] = role.ModelPermission.ToString(),
						["MemberCount"] = role.Members.Count,
						["TablesWithRLS"] = list2,
						["TablesWithoutRLS"] = list3
					};
					foreach (Table table in database.Model.Tables)
					{
						TablePermission tablePermission = role.TablePermissions.FirstOrDefault((TablePermission tp) => tp.Table == table);
						if (tablePermission != null && !string.IsNullOrWhiteSpace(tablePermission.FilterExpression))
						{
							list2.Add(table.Name);
						}
						else
						{
							list3.Add(table.Name);
						}
					}
					list.Add(item);
				}
				AuditEvent.Default.Emit("get effective permissions", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("get effective permissions", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	public static void ValidateModelRoleDefinition(ModelRoleBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ModelRole definition cannot be null", ErrorSource.User);
		}
		if (isCreate && string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(def.ModelPermission) && !Enum.IsDefined(typeof(ModelPermission), def.ModelPermission))
		{
			string[] names = Enum.GetNames(typeof(ModelPermission));
			throw new McpExceptionWithSource("Invalid ModelPermission '" + def.ModelPermission + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid ModelPermission supplied. Valid values are: " + string.Join(", ", names) + ".");
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

	public static void ValidateTablePermissionDefinition(TablePermissionDefinition def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TablePermission definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.RoleName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("RoleName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TableName is required", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(def.MetadataPermission) && !Enum.IsDefined(typeof(MetadataPermission), def.MetadataPermission))
		{
			string[] names = Enum.GetNames(typeof(MetadataPermission));
			throw new McpExceptionWithSource("Invalid MetadataPermission '" + def.MetadataPermission + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid MetadataPermission supplied. Valid values are: " + string.Join(", ", names) + ".");
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

	public static async Task<BatchOperationResponse> CreateModelRoles(string? connectionName, List<ModelRoleDefinition> roles, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, roles, options, "Create", "Created", "roles", (ModelRoleDefinition item) => item.Name, delegate(BatchItemContext<ModelRoleDefinition> ctx)
		{
			ValidateModelRoleDefinition(ctx.Item, isCreate: true);
			CreateModelRoleInternal(ctx.Connection.Database, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully created role '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Created role '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> UpdateModelRoles(string? connectionName, List<ModelRoleDefinition> roles, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, roles, options, "Update", "Updated", "roles", (ModelRoleDefinition item) => item.Name, delegate(BatchItemContext<ModelRoleDefinition> ctx)
		{
			ValidateModelRoleDefinition(ctx.Item, isCreate: false);
			OperationResult operationResult = UpdateModelRoleInternal(ctx.Connection.Database, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = (operationResult.HasChanges ? ("Successfully updated role '" + ctx.Item.Name + "'") : ("Role '" + ctx.Item.Name + "' updated (no changes detected)"));
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Updated role '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> DeleteModelRoles(string? connectionName, List<ModelRoleReference> roles, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, roles, options, "Delete", "Deleted", "roles", (ModelRoleReference item) => item.Name, delegate(BatchItemContext<ModelRoleReference> ctx)
		{
			DeleteModelRoleInternal(ctx.Connection.Database, ctx.Item.Name);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully deleted role '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Deleted role '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetModelRoles(string? connectionName, List<ModelRoleReference> roles, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (roles == null || !roles.Any())
		{
			response.Success = false;
			response.Message = "No roles provided for retrieval";
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
				for (int i = 0; i < roles.Count; i++)
				{
					ModelRoleReference modelRoleReference = roles[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = modelRoleReference.Name
					};
					try
					{
						ModelRoleGet modelRoleInternal = GetModelRoleInternal(connectionInfo.Database, modelRoleReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved role '" + modelRoleReference.Name + "'";
						itemResult.Data = modelRoleInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving role '" + modelRoleReference.Name + "': " + ex.Message;
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
				response.Message = $"Processed {roles.Count} role(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = roles.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get security roles", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = roles.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameModelRoles(string? connectionName, List<ModelRoleRename> roles, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, roles, options, "Rename", "Renamed", "roles", (ModelRoleRename item) => item.CurrentName, delegate(BatchItemContext<ModelRoleRename> ctx)
		{
			RenameModelRoleInternal(ctx.Connection, ctx.Item.CurrentName, ctx.Item.NewName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed role '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed role '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> CreateTablePermissions(string? connectionName, List<TablePermissionDefinition> permissions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "CreatePermission",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (permissions == null || !permissions.Any())
		{
			response.Success = false;
			response.Message = "No table permissions provided for creation";
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
		IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName);
		BatchOperationResponse result;
		try
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(conn, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < permissions.Count; i++)
				{
					TablePermissionDefinition tablePermissionDefinition = permissions[i];
					string itemIdentifier = tablePermissionDefinition.RoleName + "/" + tablePermissionDefinition.TableName;
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = itemIdentifier
					};
					try
					{
						ValidateTablePermissionDefinition(tablePermissionDefinition, isCreate: true);
						TablePermissionOperationResult tablePermissionOperationResult = CreateTablePermissionInternal(conn.Database, tablePermissionDefinition);
						string[] source = new string[3] { "Ready", "NoData", "CalculationNeeded" };
						itemResult.Success = Enumerable.Contains(source, tablePermissionOperationResult.State);
						itemResult.Message = (itemResult.Success ? $"Successfully created table permission for '{tablePermissionDefinition.TableName}' in role '{tablePermissionDefinition.RoleName}'" : $"Created table permission for '{tablePermissionDefinition.TableName}' in role '{tablePermissionDefinition.RoleName}' with state: {tablePermissionOperationResult.State}");
						if (itemResult.Success)
						{
							successCount++;
							if (transactionId != null)
							{
								TransactionOperations.RecordOperation(conn, $"Created table permission for '{tablePermissionDefinition.TableName}' in role '{tablePermissionDefinition.RoleName}'");
							}
						}
						else
						{
							failureCount++;
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = $"Error creating table permission for '{tablePermissionDefinition.TableName}' in role '{tablePermissionDefinition.RoleName}': {ex.Message}";
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(conn, response, transactionId, ownsTransaction, permissions.Count, ref successCount, ref failureCount, "Created", "table permissions");
				PostCommitDaxValidator.Append(conn, warnings, response.Results, permissions, transactionId, ownsTransaction, transactionFailed, failureCount, "created", (TablePermissionDefinition def) => ResolveTablePermissionForValidation(conn, def));
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(conn);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = "CreatePermission operation failed: " + ex2.Message;
				failureCount = permissions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = permissions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		finally
		{
			if (conn != null)
			{
				await conn.DisposeAsync();
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> UpdateTablePermissions(string? connectionName, List<TablePermissionDefinition> permissions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "UpdatePermission",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (permissions == null || !permissions.Any())
		{
			response.Success = false;
			response.Message = "No table permissions provided for update";
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
		IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName);
		BatchOperationResponse result;
		try
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(conn, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < permissions.Count; i++)
				{
					TablePermissionDefinition tablePermissionDefinition = permissions[i];
					string itemIdentifier = tablePermissionDefinition.RoleName + "/" + tablePermissionDefinition.TableName;
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = itemIdentifier
					};
					try
					{
						ValidateTablePermissionDefinition(tablePermissionDefinition, isCreate: false);
						TablePermissionOperationResult tablePermissionOperationResult = UpdateTablePermissionInternal(conn.Database, tablePermissionDefinition);
						itemResult.Success = true;
						itemResult.Message = (tablePermissionOperationResult.HasChanges ? $"Successfully updated table permission for '{tablePermissionDefinition.TableName}' in role '{tablePermissionDefinition.RoleName}'" : $"Table permission for '{tablePermissionDefinition.TableName}' in role '{tablePermissionDefinition.RoleName}' updated (no changes detected)");
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(conn, $"Updated table permission for '{tablePermissionDefinition.TableName}' in role '{tablePermissionDefinition.RoleName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = $"Error updating table permission for '{tablePermissionDefinition.TableName}' in role '{tablePermissionDefinition.RoleName}': {ex.Message}";
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(conn, response, transactionId, ownsTransaction, permissions.Count, ref successCount, ref failureCount, "Updated", "table permissions");
				PostCommitDaxValidator.Append(conn, warnings, response.Results, permissions, transactionId, ownsTransaction, transactionFailed, failureCount, "updated", (TablePermissionDefinition def) => ResolveTablePermissionForValidation(conn, def));
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(conn);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = "UpdatePermission operation failed: " + ex2.Message;
				failureCount = permissions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = permissions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		finally
		{
			if (conn != null)
			{
				await conn.DisposeAsync();
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> DeleteTablePermissions(string? connectionName, List<TablePermissionReference> permissions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "DeletePermission",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (permissions == null || !permissions.Any())
		{
			response.Success = false;
			response.Message = "No table permissions provided for deletion";
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
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < permissions.Count; i++)
				{
					TablePermissionReference tablePermissionReference = permissions[i];
					string itemIdentifier = tablePermissionReference.RoleName + "/" + tablePermissionReference.TableName;
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = itemIdentifier
					};
					try
					{
						DeleteTablePermissionInternal(connectionInfo.Database, tablePermissionReference.RoleName, tablePermissionReference.TableName);
						itemResult.Success = true;
						itemResult.Message = $"Successfully deleted table permission for '{tablePermissionReference.TableName}' in role '{tablePermissionReference.RoleName}'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, $"Deleted table permission for '{tablePermissionReference.TableName}' in role '{tablePermissionReference.RoleName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = $"Error deleting table permission for '{tablePermissionReference.TableName}' in role '{tablePermissionReference.RoleName}': {ex.Message}";
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, permissions.Count, ref successCount, ref failureCount, "Deleted", "table permissions");
			}
			catch (Exception ex2)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = "DeletePermission operation failed: " + ex2.Message;
				failureCount = permissions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = permissions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> GetTablePermissionsById(string? connectionName, List<TablePermissionReference> permissions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "GetPermission",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (permissions == null || !permissions.Any())
		{
			response.Success = false;
			response.Message = "No table permissions provided for retrieval";
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
				for (int i = 0; i < permissions.Count; i++)
				{
					TablePermissionReference tablePermissionReference = permissions[i];
					string itemIdentifier = tablePermissionReference.RoleName + "/" + tablePermissionReference.TableName;
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = itemIdentifier
					};
					try
					{
						TablePermissionGet tablePermissionInternal = GetTablePermissionInternal(connectionInfo, tablePermissionReference.RoleName, tablePermissionReference.TableName);
						itemResult.Success = true;
						itemResult.Message = $"Successfully retrieved table permission for '{tablePermissionReference.TableName}' in role '{tablePermissionReference.RoleName}'";
						itemResult.Data = tablePermissionInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = $"Error retrieving table permission for '{tablePermissionReference.TableName}' in role '{tablePermissionReference.RoleName}': {ex.Message}";
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
				response.Message = $"Processed {permissions.Count} table permission(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "GetPermission operation failed: " + ex2.Message;
				failureCount = permissions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get table permissions", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = permissions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}
}
