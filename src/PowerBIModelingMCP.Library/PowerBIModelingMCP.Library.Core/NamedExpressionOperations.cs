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

public static class NamedExpressionOperations
{
	public class NamedExpressionOperationResult
	{
		public string State { get; set; } = "Ready";

		public string? ErrorMessage { get; set; }

		public string NamedExpressionName { get; set; } = string.Empty;

		public bool HasChanges { get; set; }

		public List<string>? Warnings { get; set; }
	}

	public static void ValidateNamedExpressionDefinition(NamedExpressionBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Named expression definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (isCreate && string.IsNullOrWhiteSpace(def.Expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression is required for creating named expressions", ErrorSource.User);
		}
		if (isCreate && string.IsNullOrWhiteSpace(def.Kind))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Kind is required for creating named expressions", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(def.Kind) && !Enum.IsDefined(typeof(ExpressionKind), def.Kind))
		{
			string[] names = Enum.GetNames(typeof(ExpressionKind));
			throw new McpExceptionWithSource("Invalid Kind '" + def.Kind + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid Kind supplied. Valid values are: " + string.Join(", ", names) + ".");
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

	public static NamedExpression FindNamedExpression(Model model, string namedExpressionName)
	{
		return model.Expressions.Find(namedExpressionName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Named expression '" + namedExpressionName + "' not found in model", ErrorSource.User);
	}

	public static async Task<List<NamedExpressionList>> ListNamedExpressions(string? connectionName = null)
	{
		List<NamedExpressionList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<NamedExpressionList> list = ListNamedExpressionsInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list named expressions", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list named expressions", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static List<NamedExpressionList> ListNamedExpressionsInternal(Database db)
	{
		return db.Model.Expressions.Select((NamedExpression e) => new NamedExpressionList
		{
			Name = e.Name,
			Description = ((!string.IsNullOrEmpty(e.Description)) ? e.Description : null),
			Kind = e.Kind.ToString(),
			QueryGroupName = e.QueryGroup?.Name
		}).ToList();
	}

	private static NamedExpressionGet GetNamedExpressionInternal(Database db, string namedExpressionName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(namedExpressionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("namedExpressionName is required", ErrorSource.User);
		}
		NamedExpression namedExpression = FindNamedExpression(db.Model, namedExpressionName);
		NamedExpressionGet namedExpressionGet = new NamedExpressionGet
		{
			Name = namedExpression.Name,
			Expression = (namedExpression.Expression ?? string.Empty),
			Description = namedExpression.Description,
			Kind = namedExpression.Kind.ToString(),
			LineageTag = namedExpression.LineageTag,
			SourceLineageTag = namedExpression.SourceLineageTag,
			QueryGroupName = namedExpression.QueryGroup?.Name,
			ModifiedTime = namedExpression.ModifiedTime,
			Annotations = new List<KeyValuePair<string, string>>(),
			ExtendedProperties = new List<PowerBIModelingMCP.Library.Common.DataStructures.ExtendedProperty>()
		};
		if (namedExpressionGet.Annotations != null)
		{
			foreach (Annotation annotation in namedExpression.Annotations)
			{
				namedExpressionGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value ?? string.Empty));
			}
		}
		namedExpressionGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromNamedExpression(namedExpression);
		return namedExpressionGet;
	}

	private static NamedExpressionOperationResult CreateNamedExpressionInternal(IConnectionInfo info, NamedExpressionDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateNamedExpressionDefinition(def, isCreate: true);
		Model model = info.Database.Model;
		if (model.Expressions.Find(def.Name) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Named expression '" + def.Name + "' already exists in the model", ErrorSource.User);
		}
		NamedExpression namedExpression = new NamedExpression
		{
			Name = def.Name,
			Expression = def.Expression
		};
		if (Enum.TryParse<ExpressionKind>(def.Kind, out var result))
		{
			namedExpression.Kind = result;
		}
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			namedExpression.Description = def.Description;
		}
		namedExpression.LineageTag = (string.IsNullOrWhiteSpace(def.LineageTag) ? Guid.NewGuid().ToString() : def.LineageTag);
		if (!string.IsNullOrWhiteSpace(def.SourceLineageTag))
		{
			namedExpression.SourceLineageTag = def.SourceLineageTag;
		}
		List<string> warnings = null;
		if (!string.IsNullOrWhiteSpace(def.QueryGroupName))
		{
			bool wasCreated;
			QueryGroup queryGroup = QueryGroupOperations.FindOrCreateQueryGroup(info.Database, def.QueryGroupName, out wasCreated);
			if (wasCreated)
			{
				warnings = new List<string> { "Query group '" + def.QueryGroupName + "' was automatically created" };
			}
			namedExpression.QueryGroup = queryGroup;
		}
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				namedExpression.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToNamedExpression(namedExpression, def.ExtendedProperties);
		}
		model.Expressions.Add(namedExpression);
		TransactionOperations.RecordOperation(info, "Created named expression '" + def.Name + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "create named expression", OperationType.Create);
		return new NamedExpressionOperationResult
		{
			State = "Ready",
			ErrorMessage = null,
			NamedExpressionName = namedExpression.Name,
			Warnings = warnings
		};
	}

	private static NamedExpressionOperationResult UpdateNamedExpressionInternal(IConnectionInfo info, string namedExpressionName, NamedExpressionDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrEmpty(namedExpressionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("namedExpressionName is required", ErrorSource.User);
		}
		ValidateNamedExpressionDefinition(update, isCreate: false);
		NamedExpression namedExpression = FindNamedExpression(info.Database.Model, namedExpressionName);
		bool flag = false;
		if (update.Name != namedExpressionName)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Name in update definition ('{update.Name}') must match the target named expression name ('{namedExpressionName}'). Use RenameNamedExpression to rename objects.", ErrorSource.User);
		}
		if (update.Expression != null)
		{
			if (string.IsNullOrEmpty(update.Expression))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression cannot be empty. Provide a valid expression or omit this property to keep the current value.", ErrorSource.User);
			}
			if (namedExpression.Expression != update.Expression)
			{
				namedExpression.Expression = update.Expression;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.Kind))
		{
			if (!Enum.TryParse<ExpressionKind>(update.Kind, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(ExpressionKind));
				throw new McpExceptionWithSource("Invalid Kind '" + update.Kind + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid Kind supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			if (namedExpression.Kind != result)
			{
				namedExpression.Kind = result;
				flag = true;
			}
		}
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (namedExpression.Description != text)
			{
				namedExpression.Description = text;
				flag = true;
			}
		}
		if (update.LineageTag != null)
		{
			string text2 = (string.IsNullOrEmpty(update.LineageTag) ? null : update.LineageTag);
			if (namedExpression.LineageTag != text2)
			{
				namedExpression.LineageTag = text2;
				flag = true;
			}
		}
		if (update.SourceLineageTag != null)
		{
			string text3 = (string.IsNullOrEmpty(update.SourceLineageTag) ? null : update.SourceLineageTag);
			if (namedExpression.SourceLineageTag != text3)
			{
				namedExpression.SourceLineageTag = text3;
				flag = true;
			}
		}
		List<string> warnings = null;
		if (update.QueryGroupName != null)
		{
			QueryGroup queryGroup = null;
			if (!string.IsNullOrEmpty(update.QueryGroupName))
			{
				queryGroup = QueryGroupOperations.FindOrCreateQueryGroup(info.Database, update.QueryGroupName, out var wasCreated);
				if (wasCreated)
				{
					warnings = new List<string> { "Query group '" + update.QueryGroupName + "' was automatically created" };
				}
			}
			if (namedExpression.QueryGroup != queryGroup)
			{
				namedExpression.QueryGroup = queryGroup;
				flag = true;
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(namedExpression, update.Annotations, (NamedExpression ne) => ne.Annotations))
		{
			flag = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num = namedExpression.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceNamedExpressionProperties(namedExpression, update.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		if (!flag)
		{
			return new NamedExpressionOperationResult
			{
				State = "Ready",
				ErrorMessage = null,
				NamedExpressionName = namedExpression.Name,
				HasChanges = false,
				Warnings = warnings
			};
		}
		TransactionOperations.RecordOperation(info, "Updated named expression '" + namedExpressionName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "update named expression", OperationType.Update);
		return new NamedExpressionOperationResult
		{
			State = "Ready",
			ErrorMessage = null,
			NamedExpressionName = namedExpression.Name,
			HasChanges = true,
			Warnings = warnings
		};
	}

	private static void RenameNamedExpressionInternal(IConnectionInfo info, string oldName, string newName)
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
		Model model = info.Database.Model;
		NamedExpression namedExpression = FindNamedExpression(model, oldName);
		if (model.Expressions.Find(newName) != null && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Named expression '" + newName + "' already exists in the model", ErrorSource.User);
		}
		namedExpression.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed named expression from '{oldName}' to '{newName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename named expression", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	private static object DeleteNamedExpressionInternal(IConnectionInfo info, string namedExpressionName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(namedExpressionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("namedExpressionName is required", ErrorSource.User);
		}
		Model model = info.Database.Model;
		NamedExpression metadataObject = FindNamedExpression(model, namedExpressionName);
		model.Expressions.Remove(metadataObject);
		TransactionOperations.RecordOperation(info, "Deleted named expression '" + namedExpressionName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete named expression", OperationType.Delete);
		return null;
	}

	internal static NamedExpressionOperationResult CreateParameterInternal(IConnectionInfo info, NamedExpressionDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateParameterDefinition(def, isCreate: true);
		if (!IsParameterExpression(def.Expression))
		{
			def.Expression = BuildParameterExpression(def.Expression);
		}
		def.Kind = "M";
		if (!ValidateParameterExpression(def.Expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid parameter expression format", ErrorSource.User);
		}
		return CreateNamedExpressionInternal(info, def);
	}

	internal static NamedExpressionOperationResult UpdateParameterInternal(IConnectionInfo info, string parameterName, NamedExpressionDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrEmpty(parameterName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("parameterName is required", ErrorSource.User);
		}
		ValidateParameterDefinition(update, isCreate: false);
		if (!string.IsNullOrEmpty(update.Expression) && !IsParameterExpression(update.Expression))
		{
			update.Expression = BuildParameterExpression(update.Expression);
		}
		update.Kind = "M";
		if (!string.IsNullOrEmpty(update.Expression) && !ValidateParameterExpression(update.Expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Invalid parameter expression format", ErrorSource.User);
		}
		return UpdateNamedExpressionInternal(info, parameterName, update);
	}

	public static string BuildParameterExpression(string value, string type = "Text", bool isRequired = true)
	{
		string text = EscapeParameterValue(value, type);
		string text2 = $"[IsParameterQuery=true, Type=\"{type}\", IsParameterQueryRequired={isRequired.ToString().ToLowerInvariant()}]";
		return text + " meta " + text2;
	}

	private static string EscapeParameterValue(string value, string type)
	{
		return type.ToUpperInvariant() switch
		{
			"TEXT" => "\"" + value.Replace("\"", "\"\"") + "\"", 
			"NUMBER" => value, 
			"LOGICAL" => (value.ToLowerInvariant() == "true") ? "true" : "false", 
			_ => "\"" + value.Replace("\"", "\"\"") + "\"", 
		};
	}

	private static void ValidateParameterDefinition(NamedExpressionBase def, bool isCreate)
	{
		ValidateNamedExpressionDefinition(def, isCreate);
		if (!string.IsNullOrEmpty(def.Expression) && IsParameterExpression(def.Expression) && !ValidateParameterExpression(def.Expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Existing parameter expression format is invalid. Missing required metadata properties.", ErrorSource.User);
		}
	}

	private static bool IsParameterExpression(string expression)
	{
		if (!expression.Contains(" meta"))
		{
			return false;
		}
		int num = expression.IndexOf(" meta");
		if (num == -1)
		{
			return false;
		}
		int num2 = num + 5;
		int num3 = -1;
		for (int i = num2; i < expression.Length; i++)
		{
			if (expression[i] == '[')
			{
				num3 = i;
				break;
			}
			if (!char.IsWhiteSpace(expression[i]))
			{
				return false;
			}
		}
		if (num3 == -1)
		{
			return false;
		}
		string text = expression.Substring(num3 + 1);
		int num4 = text.IndexOf(']');
		if (num4 == -1)
		{
			return false;
		}
		string[] array = text.Substring(0, num4).Split(',');
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array2 = array;
		for (int j = 0; j < array2.Length; j++)
		{
			string text2 = array2[j].Trim();
			int num5 = text2.IndexOf('=');
			if (num5 > 0)
			{
				string key = text2.Substring(0, num5).Trim();
				string value = text2.Substring(num5 + 1).Trim();
				dictionary[key] = value;
			}
		}
		if (dictionary.ContainsKey("IsParameterQuery"))
		{
			return dictionary["IsParameterQuery"].Equals("true", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool ValidateParameterExpression(string expression)
	{
		if (!expression.Contains(" meta"))
		{
			return false;
		}
		int num = expression.IndexOf(" meta");
		if (num == -1)
		{
			return false;
		}
		int num2 = num + 5;
		int num3 = -1;
		for (int i = num2; i < expression.Length; i++)
		{
			if (expression[i] == '[')
			{
				num3 = i;
				break;
			}
			if (!char.IsWhiteSpace(expression[i]))
			{
				return false;
			}
		}
		if (num3 == -1)
		{
			return false;
		}
		string text = expression.Substring(num3 + 1);
		int num4 = text.IndexOf(']');
		if (num4 == -1)
		{
			return false;
		}
		string[] array = text.Substring(0, num4).Split(',');
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array2 = array;
		for (int j = 0; j < array2.Length; j++)
		{
			string text2 = array2[j].Trim();
			int num5 = text2.IndexOf('=');
			if (num5 > 0)
			{
				string key = text2.Substring(0, num5).Trim();
				string value = text2.Substring(num5 + 1).Trim();
				dictionary[key] = value;
			}
		}
		if (!dictionary.ContainsKey("IsParameterQuery") || !dictionary["IsParameterQuery"].Equals("true", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (!dictionary.ContainsKey("Type") || string.IsNullOrWhiteSpace(dictionary["Type"]))
		{
			return false;
		}
		if (!dictionary.ContainsKey("IsParameterQueryRequired"))
		{
			return false;
		}
		string text3 = dictionary["IsParameterQueryRequired"].Trim('"');
		if (!text3.Equals("true", StringComparison.OrdinalIgnoreCase) && !text3.Equals("false", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return true;
	}

	public static async Task<string> ExportTMDL(string? connectionName, string namedExpressionName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(namedExpressionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("namedExpressionName is required", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string text = ExportTMDLInternal(connectionInfo.Database, namedExpressionName, options);
				AuditEvent.Default.Emit("export named expression to TMDL", success: true, OperationType.Read, connectionInfo);
				result = text;
			}
			catch
			{
				AuditEvent.Default.Emit("export named expression to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	private static string ExportTMDLInternal(Database db, string namedExpressionName, ExportTmdl options)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrEmpty(namedExpressionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("namedExpressionName is required", ErrorSource.User);
		}
		return ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(db.Model.Expressions.Find(namedExpressionName) ?? throw new ArgumentException("Named expression '" + namedExpressionName + "' not found"), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
	}

	public static async Task<BatchOperationResponse> CreateNamedExpressions(string? connectionName, List<NamedExpressionDefinition> namedExpressions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Create",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (namedExpressions == null || !namedExpressions.Any())
		{
			response.Success = false;
			response.Message = "No named expressions provided for creation";
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
				for (int i = 0; i < namedExpressions.Count; i++)
				{
					NamedExpressionDefinition namedExpressionDefinition = namedExpressions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = namedExpressionDefinition.Name
					};
					try
					{
						NamedExpressionOperationResult namedExpressionOperationResult = CreateNamedExpressionInternal(connectionInfo, namedExpressionDefinition);
						string[] source = new string[3] { "Ready", "NoData", "CalculationNeeded" };
						itemResult.Success = Enumerable.Contains(source, namedExpressionOperationResult.State);
						itemResult.Message = (itemResult.Success ? ("Successfully created named expression '" + namedExpressionDefinition.Name + "'") : ("Failed to create named expression '" + namedExpressionDefinition.Name + "': " + namedExpressionOperationResult.ErrorMessage));
						if (namedExpressionOperationResult.Warnings != null)
						{
							itemResult.Warnings.AddRange(namedExpressionOperationResult.Warnings);
						}
						if (itemResult.Success)
						{
							successCount++;
							if (transactionId != null)
							{
								TransactionOperations.RecordOperation(connectionInfo, "Created named expression '" + namedExpressionDefinition.Name + "'");
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
						itemResult.Message = "Error creating named expression '" + namedExpressionDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, namedExpressions.Count, ref successCount, ref failureCount, "Created", "named expressions");
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
				response.Message = "Create operation failed: " + ex2.Message;
				failureCount = namedExpressions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = namedExpressions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> UpdateNamedExpressions(string? connectionName, List<NamedExpressionDefinition> namedExpressions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Update",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (namedExpressions == null || !namedExpressions.Any())
		{
			response.Success = false;
			response.Message = "No named expressions provided for update";
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
				for (int i = 0; i < namedExpressions.Count; i++)
				{
					NamedExpressionDefinition namedExpressionDefinition = namedExpressions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = namedExpressionDefinition.Name
					};
					try
					{
						NamedExpressionOperationResult namedExpressionOperationResult = UpdateNamedExpressionInternal(connectionInfo, namedExpressionDefinition.Name, namedExpressionDefinition);
						itemResult.Success = true;
						itemResult.Message = (namedExpressionOperationResult.HasChanges ? ("Successfully updated named expression '" + namedExpressionDefinition.Name + "'") : ("Named expression '" + namedExpressionDefinition.Name + "' updated (no changes detected)"));
						if (namedExpressionOperationResult.Warnings != null)
						{
							itemResult.Warnings.AddRange(namedExpressionOperationResult.Warnings);
						}
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Updated named expression '" + namedExpressionDefinition.Name + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error updating named expression '" + namedExpressionDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, namedExpressions.Count, ref successCount, ref failureCount, "Updated", "named expressions");
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
				response.Message = "Update operation failed: " + ex2.Message;
				failureCount = namedExpressions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = namedExpressions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> DeleteNamedExpressions(string? connectionName, List<NamedExpressionReference> namedExpressions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Delete",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (namedExpressions == null || !namedExpressions.Any())
		{
			response.Success = false;
			response.Message = "No named expressions provided for deletion";
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
				for (int i = 0; i < namedExpressions.Count; i++)
				{
					NamedExpressionReference namedExpressionReference = namedExpressions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = namedExpressionReference.Name
					};
					try
					{
						DeleteNamedExpressionInternal(connectionInfo, namedExpressionReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully deleted named expression '" + namedExpressionReference.Name + "'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Deleted named expression '" + namedExpressionReference.Name + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error deleting named expression '" + namedExpressionReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, namedExpressions.Count, ref successCount, ref failureCount, "Deleted", "named expressions");
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
				response.Message = "Delete operation failed: " + ex2.Message;
				failureCount = namedExpressions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = namedExpressions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> GetNamedExpressions(string? connectionName, List<NamedExpressionReference> namedExpressions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (namedExpressions == null || !namedExpressions.Any())
		{
			response.Success = false;
			response.Message = "No named expressions provided for retrieval";
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
				for (int i = 0; i < namedExpressions.Count; i++)
				{
					NamedExpressionReference namedExpressionReference = namedExpressions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = namedExpressionReference.Name
					};
					try
					{
						NamedExpressionGet namedExpressionInternal = GetNamedExpressionInternal(connectionInfo.Database, namedExpressionReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved named expression '" + namedExpressionReference.Name + "'";
						itemResult.Data = namedExpressionInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving named expression '" + namedExpressionReference.Name + "': " + ex.Message;
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
				response.Message = $"Processed {namedExpressions.Count} named expression(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = namedExpressions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get named expressions", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = namedExpressions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameNamedExpressions(string? connectionName, List<NamedExpressionRename> namedExpressions, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Rename",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (namedExpressions == null || !namedExpressions.Any())
		{
			response.Success = false;
			response.Message = "No named expressions provided for renaming";
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
				for (int i = 0; i < namedExpressions.Count; i++)
				{
					NamedExpressionRename namedExpressionRename = namedExpressions[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = namedExpressionRename.CurrentName
					};
					try
					{
						RenameNamedExpressionInternal(connectionInfo, namedExpressionRename.CurrentName, namedExpressionRename.NewName);
						itemResult.Success = true;
						itemResult.Message = $"Successfully renamed named expression '{namedExpressionRename.CurrentName}' to '{namedExpressionRename.NewName}'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, $"Renamed named expression '{namedExpressionRename.CurrentName}' to '{namedExpressionRename.NewName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error renaming named expression '" + namedExpressionRename.CurrentName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, namedExpressions.Count, ref successCount, ref failureCount, "Renamed", "named expressions");
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
				response.Message = "Rename operation failed: " + ex2.Message;
				failureCount = namedExpressions.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = namedExpressions.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> CreateParameters(string? connectionName, List<NamedExpressionDefinition> parameters, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, parameters, options, "CreateParameter", "Created", "parameters", (NamedExpressionDefinition item) => item.Name, delegate(BatchItemContext<NamedExpressionDefinition> ctx)
		{
			NamedExpressionOperationResult namedExpressionOperationResult = CreateParameterInternal(ctx.Connection, ctx.Item);
			string[] source = new string[3] { "Ready", "NoData", "CalculationNeeded" };
			ctx.Result.Success = Enumerable.Contains(source, namedExpressionOperationResult.State);
			ctx.Result.Message = (ctx.Result.Success ? ("Successfully created parameter '" + ctx.Item.Name + "'") : ("Failed to create parameter '" + ctx.Item.Name + "': " + namedExpressionOperationResult.ErrorMessage));
			if (namedExpressionOperationResult.Warnings != null)
			{
				ctx.Result.Warnings.AddRange(namedExpressionOperationResult.Warnings);
			}
			if (ctx.Result.Success && ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Created parameter '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> UpdateParameters(string? connectionName, List<NamedExpressionDefinition> parameters, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, parameters, options, "UpdateParameter", "Updated", "parameters", (NamedExpressionDefinition item) => item.Name, delegate(BatchItemContext<NamedExpressionDefinition> ctx)
		{
			NamedExpressionOperationResult namedExpressionOperationResult = UpdateParameterInternal(ctx.Connection, ctx.Item.Name, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = (namedExpressionOperationResult.HasChanges ? ("Successfully updated parameter '" + ctx.Item.Name + "'") : ("Parameter '" + ctx.Item.Name + "' updated (no changes detected)"));
			if (namedExpressionOperationResult.Warnings != null)
			{
				ctx.Result.Warnings.AddRange(namedExpressionOperationResult.Warnings);
			}
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Updated parameter '" + ctx.Item.Name + "'");
			}
		});
	}
}
