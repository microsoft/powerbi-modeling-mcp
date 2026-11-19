using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class MeasureOperations
{
	public class MeasureOperationResult
	{
		public string State { get; set; } = string.Empty;

		public string? ErrorMessage { get; set; }

		public string MeasureName { get; set; } = string.Empty;

		public string TableName { get; set; } = string.Empty;

		public bool HasChanges { get; set; }
	}

	public class MeasureValidationResult
	{
		public bool IsValid { get; set; }

		public string? ObjectState { get; set; }

		public string? ErrorMessage { get; set; }

		public string Expression { get; set; } = string.Empty;

		public string? Message { get; set; }

		public long ValidationTimeMs { get; set; }
	}

	internal static PostCommitDaxValidator.Target? ResolveMeasureForValidation(IConnectionInfo conn, MeasureDefinition def)
	{
		if (def == null || string.IsNullOrEmpty(def.Name))
		{
			return null;
		}
		Microsoft.AnalysisServices.Tabular.Database database = conn?.Database;
		if (database == null)
		{
			return null;
		}
		Microsoft.AnalysisServices.Tabular.Measure measure = null;
		if (!string.IsNullOrEmpty(def.TableName))
		{
			measure = database.Model.Tables.Find(def.TableName)?.Measures.Find(def.Name);
		}
		if (measure == null)
		{
			foreach (Table table in database.Model.Tables)
			{
				Microsoft.AnalysisServices.Tabular.Measure measure2 = table.Measures.Find(def.Name);
				if (measure2 != null)
				{
					measure = measure2;
					break;
				}
			}
		}
		if (measure == null)
		{
			return null;
		}
		List<PostCommitDaxValidator.Check> list = new List<PostCommitDaxValidator.Check>
		{
			new PostCommitDaxValidator.Check(string.Empty, measure.State.ToString(), measure.ErrorMessage)
		};
		if (measure.FormatStringDefinition != null)
		{
			list.Add(new PostCommitDaxValidator.Check("formatStringDefinition", measure.FormatStringDefinition.State.ToString(), measure.FormatStringDefinition.ErrorMessage));
		}
		if (measure.DetailRowsDefinition != null)
		{
			list.Add(new PostCommitDaxValidator.Check("detailRowsDefinition", measure.DetailRowsDefinition.State.ToString(), measure.DetailRowsDefinition.ErrorMessage));
		}
		return new PostCommitDaxValidator.Target("Measure", $"'{measure.Name}' on table '{measure.Table?.Name}'", list);
	}

	public static void ValidateMeasureDefinition(MeasureBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Measure definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (isCreate && string.IsNullOrWhiteSpace(def.Expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression is required for creation", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(def.DataType) && !Enum.TryParse<Microsoft.AnalysisServices.DataType>(def.DataType, ignoreCase: true, out var _))
		{
			string[] names = Enum.GetNames(typeof(Microsoft.AnalysisServices.DataType));
			throw new McpExceptionWithSource("Invalid DataType '" + def.DataType + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid DataType supplied. Valid values are: " + string.Join(", ", names) + ".");
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

	internal static Microsoft.AnalysisServices.Tabular.Measure FindMeasureInternal(Model model, string measureName)
	{
		if (model == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("model cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrEmpty(measureName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("measureName is required", ErrorSource.User);
		}
		foreach (Table table in model.Tables)
		{
			Microsoft.AnalysisServices.Tabular.Measure measure = table.Measures.Find(measureName);
			if (measure != null)
			{
				return measure;
			}
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Measure [" + measureName + "] not found in the model", ErrorSource.User);
	}

	public static async Task<(List<TableMeasureList> measures, int totalCount)> ListMeasures(string? connectionName, List<string>? tableNames, int? maxResults)
	{
		(List<TableMeasureList> measures, int totalCount) result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				int totalCount;
				List<TableMeasureList> item = ListMeasuresInternal(connectionInfo.Database, tableNames, maxResults, out totalCount);
				AuditEvent.Default.Emit("list measures", success: true, OperationType.Read, connectionInfo);
				result = (measures: item, totalCount: totalCount);
			}
			catch
			{
				AuditEvent.Default.Emit("list measures", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<TableMeasureList> ListMeasuresInternal(Microsoft.AnalysisServices.Tabular.Database db, List<string>? tableNames, int? maxResults, out int totalCount)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		IEnumerable<Table> source;
		if (tableNames != null && tableNames.Any())
		{
			List<Table> list = new List<Table>();
			foreach (string tableName in tableNames)
			{
				Table table = db.Model.Tables.Find(tableName);
				if (table == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
				}
				list.Add(table);
			}
			source = list;
		}
		else
		{
			source = db.Model.Tables;
		}
		List<TableMeasureList> list2 = (from t in source
			select new TableMeasureList
			{
				TableName = t.Name,
				Measures = t.Measures.Select((Microsoft.AnalysisServices.Tabular.Measure m) => new MeasureList
				{
					Name = m.Name,
					Description = ((!string.IsNullOrEmpty(m.Description)) ? m.Description : null),
					DisplayFolder = ((!string.IsNullOrEmpty(m.DisplayFolder)) ? m.DisplayFolder : null),
					IsHidden = (m.IsHidden ? new bool?(true) : ((bool?)null)),
					FormatString = ((!string.IsNullOrWhiteSpace(m.FormatString)) ? m.FormatString : null)
				}).ToList()
			} into g
			where g.Measures.Any()
			select g).ToList();
		totalCount = list2.Sum((TableMeasureList g) => g.Measures.Count);
		if (maxResults.HasValue && maxResults.Value > 0)
		{
			int num = maxResults.Value;
			List<TableMeasureList> list3 = new List<TableMeasureList>();
			foreach (TableMeasureList item in list2)
			{
				if (num <= 0)
				{
					break;
				}
				if (item.Measures.Count <= num)
				{
					list3.Add(item);
					num -= item.Measures.Count;
					continue;
				}
				list3.Add(new TableMeasureList
				{
					TableName = item.TableName,
					Measures = item.Measures.Take(num).ToList()
				});
				num = 0;
			}
			return list3;
		}
		return list2;
	}

	public static async Task<List<Dictionary<string, string>>> ListAllMeasures(string? connectionName)
	{
		List<Dictionary<string, string>> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			Microsoft.AnalysisServices.Tabular.Database database = connectionInfo.Database;
			List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
			foreach (Table table in database.Model.Tables)
			{
				foreach (Microsoft.AnalysisServices.Tabular.Measure measure in table.Measures)
				{
					list.Add(new Dictionary<string, string>
					{
						["TableName"] = table.Name,
						["MeasureName"] = measure.Name,
						["Expression"] = measure.Expression ?? "",
						["IsHidden"] = measure.IsHidden.ToString()
					});
				}
			}
			result = list;
		}
		return result;
	}

	internal static MeasureGet GetMeasureInternal(Microsoft.AnalysisServices.Tabular.Database db, string measureName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(measureName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("measureName is required", ErrorSource.User);
		}
		Microsoft.AnalysisServices.Tabular.Measure measure = FindMeasureInternal(db.Model, measureName);
		Table table = measure.Table;
		MeasureGet measureGet = new MeasureGet
		{
			TableName = table.Name,
			Name = measure.Name,
			Expression = measure.Expression,
			Description = measure.Description,
			FormatString = measure.FormatString,
			IsHidden = measure.IsHidden,
			IsSimpleMeasure = measure.IsSimpleMeasure,
			DisplayFolder = measure.DisplayFolder,
			DataType = measure.DataType.ToString(),
			DataCategory = measure.DataCategory,
			LineageTag = measure.LineageTag,
			SourceLineageTag = measure.SourceLineageTag,
			DetailRowsExpression = measure.DetailRowsDefinition?.Expression,
			FormatStringExpression = measure.FormatStringDefinition?.Expression,
			FormatStringExpressionState = measure.FormatStringDefinition?.State.ToString(),
			FormatStringExpressionErrorMessage = measure.FormatStringDefinition?.ErrorMessage,
			DetailRowsExpressionState = measure.DetailRowsDefinition?.State.ToString(),
			DetailRowsExpressionErrorMessage = measure.DetailRowsDefinition?.ErrorMessage,
			State = measure.State.ToString(),
			ErrorMessage = measure.ErrorMessage,
			ModifiedTime = measure.ModifiedTime,
			StructureModifiedTime = measure.StructureModifiedTime
		};
		if (measure.KPI != null)
		{
			KPIDefinition kPIDefinition = new KPIDefinition
			{
				StatusExpression = measure.KPI.StatusExpression,
				StatusGraphic = measure.KPI.StatusGraphic,
				TrendExpression = measure.KPI.TrendExpression,
				TrendGraphic = measure.KPI.TrendGraphic,
				TargetExpression = measure.KPI.TargetExpression,
				TargetFormatString = measure.KPI.TargetFormatString,
				TargetDescription = measure.KPI.TargetDescription,
				StatusDescription = measure.KPI.StatusDescription,
				TrendDescription = measure.KPI.TrendDescription,
				Annotations = new List<KeyValuePair<string, string>>()
			};
			foreach (Microsoft.AnalysisServices.Tabular.Annotation annotation in measure.KPI.Annotations)
			{
				kPIDefinition.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
			}
			measureGet.KPI = System.Text.Json.JsonSerializer.Serialize(kPIDefinition);
		}
		measureGet.Annotations = new List<KeyValuePair<string, string>>();
		foreach (Microsoft.AnalysisServices.Tabular.Annotation annotation2 in measure.Annotations)
		{
			measureGet.Annotations.Add(new KeyValuePair<string, string>(annotation2.Name, annotation2.Value));
		}
		measureGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromMeasure(measure);
		return measureGet;
	}

	internal static MeasureOperationResult CreateMeasureInternal(IConnectionInfo info, MeasureDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateMeasureDefinition(def, isCreate: true);
		Microsoft.AnalysisServices.Tabular.Database database = info.Database;
		foreach (Table table2 in database.Model.Tables)
		{
			if (table2.Measures.Contains(def.Name))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure '{def.Name}' already exists in table '{table2.Name}'", ErrorSource.User);
			}
		}
		if (!string.IsNullOrWhiteSpace(def.TableName))
		{
			Table table = database.Model.Tables.Find(def.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + def.TableName + "' not found", ErrorSource.User);
			Microsoft.AnalysisServices.Tabular.Measure measure = new Microsoft.AnalysisServices.Tabular.Measure
			{
				Name = def.Name,
				Expression = def.Expression
			};
			ApplyMeasureProperties(measure, def);
			table.Measures.Add(measure);
			TransactionOperations.RecordOperation(info, $"Created measure '{def.Name}' in table '{table.Name}'");
			ConnectionOperations.SaveChangesWithRollback(info, "create measure", OperationType.Create);
			return new MeasureOperationResult
			{
				State = measure.State.ToString(),
				ErrorMessage = measure.ErrorMessage,
				MeasureName = measure.Name,
				TableName = table.Name
			};
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Must specify host table name when creating a measure.", ErrorSource.User);
	}

	internal static MeasureOperationResult UpdateMeasureInternal(IConnectionInfo info, MeasureDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ValidateMeasureDefinition(update, isCreate: false);
		Microsoft.AnalysisServices.Tabular.Measure measure = FindMeasureInternal(info.Database.Model, update.Name);
		Table table = measure.Table;
		if (!string.IsNullOrEmpty(update.TableName) && string.Compare(table.Name, update.TableName, ignoreCase: true) != 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure [{update.Name}] doesn't belong to table '{update.TableName}'.", ErrorSource.User);
		}
		if (!ApplyMeasureUpdates(measure, update))
		{
			return new MeasureOperationResult
			{
				State = measure.State.ToString(),
				ErrorMessage = measure.ErrorMessage,
				MeasureName = measure.Name,
				TableName = table.Name,
				HasChanges = false
			};
		}
		TransactionOperations.RecordOperation(info, $"Updated measure '{update.Name}' in table '{table.Name}'");
		ConnectionOperations.SaveChangesWithRollback(info, "update measure", OperationType.Update);
		return new MeasureOperationResult
		{
			State = measure.State.ToString(),
			ErrorMessage = measure.ErrorMessage,
			MeasureName = measure.Name,
			TableName = table.Name,
			HasChanges = true
		};
	}

	internal static void RenameMeasureInternal(IConnectionInfo info, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrEmpty(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrEmpty(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		Microsoft.AnalysisServices.Tabular.Database database = info.Database;
		Microsoft.AnalysisServices.Tabular.Measure measure = FindMeasureInternal(database.Model, oldName);
		Table table = measure.Table;
		foreach (Table table2 in database.Model.Tables)
		{
			if (table2.Measures.Contains(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure '{newName}' already exists in table '{table2.Name}'", ErrorSource.User);
			}
		}
		measure.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed measure '{oldName}' to '{newName}' in table '{table.Name}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename measure", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	internal static void DeleteMeasureInternal(IConnectionInfo info, string measureName, bool shouldCascadeDelete)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrEmpty(measureName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("measureName is required", ErrorSource.User);
		}
		Microsoft.AnalysisServices.Tabular.Database database = info.Database;
		Microsoft.AnalysisServices.Tabular.Measure measure = FindMeasureInternal(database.Model, measureName);
		Table table = measure.Table;
		List<string> list = StructuralDependencyHelper.CheckAndDeleteDependenciesIfRequired(database, measure, shouldCascadeDelete);
		if (!shouldCascadeDelete && list.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete measure '" + measureName + "' because it is used by: " + string.Join(", ", list), ErrorSource.User);
		}
		table.Measures.Remove(measure);
		TransactionOperations.RecordOperation(info, $"Deleted measure '{measureName}' from table '{table.Name}'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete measure", OperationType.Delete);
	}

	internal static void MoveMeasureInternal(IConnectionInfo info, string targetTableName, string measureName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrEmpty(targetTableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("targetTableName is required", ErrorSource.User);
		}
		if (string.IsNullOrEmpty(measureName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("measureName is required", ErrorSource.User);
		}
		Microsoft.AnalysisServices.Tabular.Database database = info.Database;
		Table table = database.Model.Tables.Find(targetTableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Target table '" + targetTableName + "' not found", ErrorSource.User);
		Microsoft.AnalysisServices.Tabular.Measure measure = FindMeasureInternal(database.Model, measureName);
		Table table2 = measure.Table;
		if (table2 == table)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Measure '{measureName}' is already in table '{targetTableName}'", ErrorSource.User);
		}
		List<string> list = StructuralDependencyHelper.CheckAndDeleteDependenciesIfRequired(database, measure, cascadeDelete: true);
		Microsoft.AnalysisServices.Tabular.Measure measure2 = new Microsoft.AnalysisServices.Tabular.Measure();
		measure.CopyTo(measure2);
		table2.Measures.Remove(measure);
		table.Measures.Add(measure2);
		foreach (string item in list)
		{
			if (item.Contains("Perspective"))
			{
				string text = item.Split(',')[0].Replace("Perspective: ", "");
				Microsoft.AnalysisServices.Tabular.Perspective perspective = database.Model.Perspectives.Find(text);
				if (perspective != null)
				{
					PerspectiveTable perspectiveTable = perspective.PerspectiveTables.Find(targetTableName);
					if (perspectiveTable == null)
					{
						perspectiveTable = new PerspectiveTable
						{
							Table = table
						};
						perspective.PerspectiveTables.Add(perspectiveTable);
					}
					PerspectiveMeasureDefinition def = new PerspectiveMeasureDefinition
					{
						MeasureName = measure2.Name,
						TableName = table.Name
					};
					PerspectiveOperations.AddMeasureToPerspectiveTableInternal(info, text, def);
				}
			}
			else if (item.Contains("Culture"))
			{
				string[] array = item.Split(',');
				string text2 = array[0].Replace("Culture: ", "");
				string value = array[1].Replace(" Measure Translation: ", "");
				string property = array[2].Replace(" TranslatedProperty: ", "");
				if (database.Model.Cultures.Find(text2) != null)
				{
					ObjectTranslationDefinition translationDef = new ObjectTranslationDefinition
					{
						CultureName = text2,
						ObjectType = "Measure",
						MeasureName = measure2.Name,
						Property = property,
						Value = value
					};
					ObjectTranslationOperations.CreateObjectTranslationInternal(info, translationDef);
				}
			}
		}
		TransactionOperations.RecordOperation(info, $"Moved measure '{measureName}' from table '{table2.Name}' to table '{targetTableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "move measure", OperationType.Update);
	}

	public static async Task<MeasureValidationResult> ValidateMeasureExpression(string? connectionName, string expression)
	{
		if (string.IsNullOrWhiteSpace(expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("expression is required", ErrorSource.User);
		}
		MeasureValidationResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			Model model = connectionInfo.Database.Model;
			Table table = model.Tables.FirstOrDefault() ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("No tables found in the model", ErrorSource.User);
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Table table2 in model.Tables)
			{
				foreach (Microsoft.AnalysisServices.Tabular.Measure measure2 in table2.Measures)
				{
					hashSet.Add(measure2.Name);
				}
			}
			int num = 1;
			string text;
			do
			{
				text = $"__TempMeasure{num}";
				num++;
			}
			while (hashSet.Contains(text));
			MeasureValidationResult measureValidationResult = new MeasureValidationResult
			{
				Expression = expression
			};
			Stopwatch stopwatch = Stopwatch.StartNew();
			Microsoft.AnalysisServices.Tabular.Measure measure = null;
			bool flag = false;
			try
			{
				measure = new Microsoft.AnalysisServices.Tabular.Measure
				{
					Name = text,
					Expression = expression
				};
				table.Measures.Add(measure);
				flag = true;
				ConnectionOperations.SaveChangesIfNeeded(connectionInfo);
				measureValidationResult.ObjectState = measure.State.ToString();
				if (measure.State != ObjectState.Ready)
				{
					measureValidationResult.IsValid = false;
					measureValidationResult.ErrorMessage = measure.ErrorMessage ?? "Unknown error during validation.";
				}
				else
				{
					measureValidationResult.IsValid = true;
					measureValidationResult.Message = "Expression is valid.";
				}
			}
			catch (Exception ex)
			{
				measureValidationResult.IsValid = false;
				measureValidationResult.ErrorMessage = ex.Message;
			}
			finally
			{
				stopwatch.Stop();
				measureValidationResult.ValidationTimeMs = stopwatch.ElapsedMilliseconds;
				if (flag && measure != null)
				{
					try
					{
						table.Measures.Remove(measure);
						ConnectionOperations.SaveChangesIfNeeded(connectionInfo, CheckpointMode.ForceEvenInTransaction);
					}
					catch (Exception)
					{
					}
				}
			}
			result = measureValidationResult;
		}
		return result;
	}

	private static void ApplyMeasureProperties(Microsoft.AnalysisServices.Tabular.Measure measure, MeasureBase def)
	{
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			measure.Description = def.Description;
		}
		if (!string.IsNullOrWhiteSpace(def.FormatString))
		{
			measure.FormatString = def.FormatString;
		}
		measure.IsHidden = def.IsHidden == true;
		measure.IsSimpleMeasure = def.IsSimpleMeasure == true;
		if (!string.IsNullOrWhiteSpace(def.DisplayFolder))
		{
			measure.DisplayFolder = def.DisplayFolder;
		}
		if (!string.IsNullOrWhiteSpace(def.DataType))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot set the data type of a new measure explicitly.", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(def.DataCategory))
		{
			measure.DataCategory = def.DataCategory;
		}
		measure.LineageTag = (string.IsNullOrWhiteSpace(def.LineageTag) ? Guid.NewGuid().ToString() : def.LineageTag);
		if (!string.IsNullOrWhiteSpace(def.SourceLineageTag))
		{
			measure.SourceLineageTag = def.SourceLineageTag;
		}
		if (!string.IsNullOrWhiteSpace(def.DetailRowsExpression))
		{
			if (measure.DetailRowsDefinition == null)
			{
				measure.DetailRowsDefinition = new DetailRowsDefinition();
			}
			measure.DetailRowsDefinition.Expression = def.DetailRowsExpression;
		}
		if (!string.IsNullOrWhiteSpace(def.FormatStringExpression))
		{
			if (measure.FormatStringDefinition == null)
			{
				measure.FormatStringDefinition = new FormatStringDefinition();
			}
			measure.FormatStringDefinition.Expression = def.FormatStringExpression;
		}
		if (!string.IsNullOrWhiteSpace(def.KPI))
		{
			try
			{
				KPIDefinition kPIDefinition = System.Text.Json.JsonSerializer.Deserialize<KPIDefinition>(def.KPI);
				if (kPIDefinition != null)
				{
					KPI kPI = new KPI();
					ApplyKPIProperties(kPI, kPIDefinition);
					measure.KPI = kPI;
				}
			}
			catch (Exception ex)
			{
				throw new McpExceptionWithSource("Invalid KPI definition: " + ex.Message, ex, null, "Invalid KPI definition; exception type: '" + ex.GetType().Name + "'.");
			}
		}
		if (def.Annotations != null)
		{
			AnnotationHelpers.ApplyAnnotations(measure, def.Annotations, (Microsoft.AnalysisServices.Tabular.Measure m) => m.Annotations);
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToMeasure(measure, def.ExtendedProperties);
		}
	}

	private static void ApplyKPIProperties(KPI kpi, KPIDefinition kpiDef)
	{
		if (!string.IsNullOrWhiteSpace(kpiDef.StatusExpression))
		{
			kpi.StatusExpression = kpiDef.StatusExpression;
		}
		if (!string.IsNullOrWhiteSpace(kpiDef.StatusGraphic))
		{
			kpi.StatusGraphic = kpiDef.StatusGraphic;
		}
		if (!string.IsNullOrWhiteSpace(kpiDef.TrendExpression))
		{
			kpi.TrendExpression = kpiDef.TrendExpression;
		}
		if (!string.IsNullOrWhiteSpace(kpiDef.TrendGraphic))
		{
			kpi.TrendGraphic = kpiDef.TrendGraphic;
		}
		if (!string.IsNullOrWhiteSpace(kpiDef.TargetExpression))
		{
			kpi.TargetExpression = kpiDef.TargetExpression;
		}
		if (!string.IsNullOrWhiteSpace(kpiDef.TargetFormatString))
		{
			kpi.TargetFormatString = kpiDef.TargetFormatString;
		}
		if (!string.IsNullOrWhiteSpace(kpiDef.TargetDescription))
		{
			kpi.TargetDescription = kpiDef.TargetDescription;
		}
		if (!string.IsNullOrWhiteSpace(kpiDef.StatusDescription))
		{
			kpi.StatusDescription = kpiDef.StatusDescription;
		}
		if (!string.IsNullOrWhiteSpace(kpiDef.TrendDescription))
		{
			kpi.TrendDescription = kpiDef.TrendDescription;
		}
		if (kpiDef.Annotations != null)
		{
			AnnotationHelpers.ApplyAnnotations(kpi, kpiDef.Annotations, (KPI k) => k.Annotations);
		}
	}

	private static bool ApplyMeasureUpdates(Microsoft.AnalysisServices.Tabular.Measure measure, MeasureDefinition update)
	{
		bool flag = false;
		if (update.Expression != null && measure.Expression != update.Expression)
		{
			if (string.IsNullOrWhiteSpace(update.Expression))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression cannot be empty. Use a valid DAX expression.", ErrorSource.User);
			}
			measure.Expression = update.Expression;
			flag = true;
		}
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (measure.Description != text)
			{
				measure.Description = text;
				flag = true;
			}
		}
		if (update.FormatString != null)
		{
			string text2 = (string.IsNullOrEmpty(update.FormatString) ? null : update.FormatString);
			if (measure.FormatString != text2)
			{
				measure.FormatString = text2;
				flag = true;
			}
		}
		if (update.IsHidden.HasValue && measure.IsHidden != update.IsHidden.Value)
		{
			measure.IsHidden = update.IsHidden.Value;
			flag = true;
		}
		if (update.IsSimpleMeasure.HasValue && measure.IsSimpleMeasure != update.IsSimpleMeasure.Value)
		{
			measure.IsSimpleMeasure = update.IsSimpleMeasure.Value;
			flag = true;
		}
		if (update.DisplayFolder != null)
		{
			string text3 = (string.IsNullOrEmpty(update.DisplayFolder) ? null : update.DisplayFolder);
			if (measure.DisplayFolder != text3)
			{
				measure.DisplayFolder = text3;
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.DataType))
		{
			throw new InvalidOperationException("Cannot change the data type of measure explicitly.");
		}
		if (update.DataCategory != null)
		{
			string text4 = (string.IsNullOrEmpty(update.DataCategory) ? null : update.DataCategory);
			if (measure.DataCategory != text4)
			{
				measure.DataCategory = text4;
				flag = true;
			}
		}
		if (update.LineageTag != null)
		{
			string text5 = (string.IsNullOrEmpty(update.LineageTag) ? null : update.LineageTag);
			if (measure.LineageTag != text5)
			{
				measure.LineageTag = text5;
				flag = true;
			}
		}
		if (update.SourceLineageTag != null)
		{
			string text6 = (string.IsNullOrEmpty(update.SourceLineageTag) ? null : update.SourceLineageTag);
			if (measure.SourceLineageTag != text6)
			{
				measure.SourceLineageTag = text6;
				flag = true;
			}
		}
		if (update.DetailRowsExpression != null)
		{
			string text7 = measure.DetailRowsDefinition?.Expression;
			if (string.IsNullOrEmpty(update.DetailRowsExpression))
			{
				if (measure.DetailRowsDefinition != null)
				{
					measure.DetailRowsDefinition = null;
					flag = true;
				}
			}
			else if (text7 != update.DetailRowsExpression)
			{
				if (measure.DetailRowsDefinition == null)
				{
					measure.DetailRowsDefinition = new DetailRowsDefinition();
				}
				measure.DetailRowsDefinition.Expression = update.DetailRowsExpression;
				flag = true;
			}
		}
		if (update.FormatStringExpression != null)
		{
			string text8 = measure.FormatStringDefinition?.Expression;
			if (string.IsNullOrEmpty(update.FormatStringExpression))
			{
				if (measure.FormatStringDefinition != null)
				{
					measure.FormatStringDefinition = null;
					flag = true;
				}
			}
			else if (text8 != update.FormatStringExpression)
			{
				if (measure.FormatStringDefinition == null)
				{
					measure.FormatStringDefinition = new FormatStringDefinition();
				}
				measure.FormatStringDefinition.Expression = update.FormatStringExpression;
				flag = true;
			}
		}
		flag = ApplyKPIUpdates(measure, update.KPI ?? string.Empty) || flag;
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(measure, update.Annotations, (Microsoft.AnalysisServices.Tabular.Measure m) => m.Annotations))
		{
			flag = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num = measure.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceExtendedProperties(measure, update.ExtendedProperties, (Microsoft.AnalysisServices.Tabular.Measure m) => m.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		return flag;
	}

	private static bool ApplyKPIUpdates(Microsoft.AnalysisServices.Tabular.Measure measure, string kpiJson)
	{
		bool flag = false;
		if (string.IsNullOrEmpty(kpiJson))
		{
			if (measure.KPI != null)
			{
				measure.KPI = null;
				flag = true;
			}
		}
		else
		{
			try
			{
				KPIDefinition kPIDefinition = System.Text.Json.JsonSerializer.Deserialize<KPIDefinition>(kpiJson);
				if (kPIDefinition != null)
				{
					if (measure.KPI == null)
					{
						measure.KPI = new KPI();
						flag = true;
					}
					flag = ApplyKPIPropertyUpdates(measure.KPI, kPIDefinition) || flag;
				}
			}
			catch (Exception ex)
			{
				throw new McpExceptionWithSource("Invalid KPI definition: " + ex.Message, ex, ErrorSource.User, "Invalid KPI definition; exception type: '" + ex.GetType().Name + "'.");
			}
		}
		return flag;
	}

	private static bool ApplyKPIPropertyUpdates(KPI kpi, KPIDefinition kpiDef)
	{
		bool flag = false;
		if (kpiDef.StatusExpression != null)
		{
			string text = (string.IsNullOrEmpty(kpiDef.StatusExpression) ? null : kpiDef.StatusExpression);
			if (kpi.StatusExpression != text)
			{
				kpi.StatusExpression = text;
				flag = true;
			}
		}
		if (kpiDef.StatusGraphic != null)
		{
			string text2 = (string.IsNullOrEmpty(kpiDef.StatusGraphic) ? null : kpiDef.StatusGraphic);
			if (kpi.StatusGraphic != text2)
			{
				kpi.StatusGraphic = text2;
				flag = true;
			}
		}
		if (kpiDef.TrendExpression != null)
		{
			string text3 = (string.IsNullOrEmpty(kpiDef.TrendExpression) ? null : kpiDef.TrendExpression);
			if (kpi.TrendExpression != text3)
			{
				kpi.TrendExpression = text3;
				flag = true;
			}
		}
		if (kpiDef.TrendGraphic != null)
		{
			string text4 = (string.IsNullOrEmpty(kpiDef.TrendGraphic) ? null : kpiDef.TrendGraphic);
			if (kpi.TrendGraphic != text4)
			{
				kpi.TrendGraphic = text4;
				flag = true;
			}
		}
		if (kpiDef.TargetExpression != null)
		{
			string text5 = (string.IsNullOrEmpty(kpiDef.TargetExpression) ? null : kpiDef.TargetExpression);
			if (kpi.TargetExpression != text5)
			{
				kpi.TargetExpression = text5;
				flag = true;
			}
		}
		if (kpiDef.TargetFormatString != null)
		{
			string text6 = (string.IsNullOrEmpty(kpiDef.TargetFormatString) ? null : kpiDef.TargetFormatString);
			if (kpi.TargetFormatString != text6)
			{
				kpi.TargetFormatString = text6;
				flag = true;
			}
		}
		if (kpiDef.TargetDescription != null)
		{
			string text7 = (string.IsNullOrEmpty(kpiDef.TargetDescription) ? null : kpiDef.TargetDescription);
			if (kpi.TargetDescription != text7)
			{
				kpi.TargetDescription = text7;
				flag = true;
			}
		}
		if (kpiDef.StatusDescription != null)
		{
			string text8 = (string.IsNullOrEmpty(kpiDef.StatusDescription) ? null : kpiDef.StatusDescription);
			if (kpi.StatusDescription != text8)
			{
				kpi.StatusDescription = text8;
				flag = true;
			}
		}
		if (kpiDef.TrendDescription != null)
		{
			string text9 = (string.IsNullOrEmpty(kpiDef.TrendDescription) ? null : kpiDef.TrendDescription);
			if (kpi.TrendDescription != text9)
			{
				kpi.TrendDescription = text9;
				flag = true;
			}
		}
		if (kpiDef.Annotations != null)
		{
			flag = AnnotationHelpers.ReplaceAnnotations(kpi, kpiDef.Annotations, (KPI k) => k.Annotations) || flag;
		}
		return flag;
	}

	public static async Task<string> ExportTMDL(string? connectionName, string measureName, ExportTmdl options)
	{
		if (string.IsNullOrEmpty(measureName))
		{
			throw new ArgumentException("measureName is required");
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string text = ExportTMDLInternal(connectionInfo.Database, measureName, options);
				AuditEvent.Default.Emit("export measure to TMDL", success: true, OperationType.Read, connectionInfo);
				result = text;
			}
			catch
			{
				AuditEvent.Default.Emit("export measure to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static string ExportTMDLInternal(Microsoft.AnalysisServices.Tabular.Database db, string measureName, ExportTmdl options)
	{
		if (db == null)
		{
			throw new ArgumentException("Database cannot be null");
		}
		if (string.IsNullOrEmpty(measureName))
		{
			throw new ArgumentException("measureName is required");
		}
		return ExportContentProcessor.ProcessExportContent(Microsoft.AnalysisServices.Tabular.TmdlSerializer.SerializeObject(FindMeasureInternal(db.Model, measureName) ?? throw new ArgumentException("Measure '" + measureName + "' not found"), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
	}

	public static async Task<BatchOperationResponse> CreateMeasures(string? connectionName, List<MeasureDefinition> measures, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, measures, options, "Create", "Created", "measures", (MeasureDefinition m) => m.Name, delegate(BatchItemContext<MeasureDefinition> ctx)
		{
			CreateMeasureInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully created measure '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Created measure '" + ctx.Item.Name + "'");
			}
		}, delegate(IConnectionInfo conn, List<MeasureDefinition> items, BatchOperationResponse response, List<string> warnings, string? txId, bool owns, bool txFailed, int fc)
		{
			PostCommitDaxValidator.Append(conn, warnings, response.Results, items, txId, owns, txFailed, fc, "created", (MeasureDefinition def) => ResolveMeasureForValidation(conn, def));
		});
	}

	public static async Task<BatchOperationResponse> UpdateMeasures(string? connectionName, List<MeasureDefinition> measures, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, measures, options, "Update", "Updated", "measures", (MeasureDefinition m) => m.Name, delegate(BatchItemContext<MeasureDefinition> ctx)
		{
			MeasureOperationResult measureOperationResult = UpdateMeasureInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = (measureOperationResult.HasChanges ? ("Successfully updated measure '" + ctx.Item.Name + "'") : ("Measure '" + ctx.Item.Name + "' updated (no changes detected)"));
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Updated measure '" + ctx.Item.Name + "'");
			}
		}, delegate(IConnectionInfo conn, List<MeasureDefinition> items, BatchOperationResponse response, List<string> warnings, string? txId, bool owns, bool txFailed, int fc)
		{
			PostCommitDaxValidator.Append(conn, warnings, response.Results, items, txId, owns, txFailed, fc, "updated", (MeasureDefinition def) => ResolveMeasureForValidation(conn, def));
		});
	}

	public static async Task<BatchOperationResponse> DeleteMeasures(string? connectionName, List<MeasureReference> measures, bool shouldCascadeDelete, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, measures, options, "Delete", "Deleted", "measures", (MeasureReference m) => m.Name, delegate(BatchItemContext<MeasureReference> ctx)
		{
			DeleteMeasureInternal(ctx.Connection, ctx.Item.Name, shouldCascadeDelete);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully deleted measure '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Deleted measure '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetMeasures(string? connectionName, List<MeasureReference> measures, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (measures == null || !measures.Any())
		{
			response.Success = false;
			response.Message = "No measures provided for retrieval";
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
				for (int i = 0; i < measures.Count; i++)
				{
					MeasureReference measureReference = measures[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = measureReference.Name
					};
					try
					{
						MeasureGet measureInternal = GetMeasureInternal(connectionInfo.Database, measureReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved measure '" + measureReference.Name + "'";
						itemResult.Data = measureInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving measure '" + measureReference.Name + "': " + ex.Message;
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
				response.Message = $"Processed {measures.Count} measure(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = measures.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get measures", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = measures.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameMeasures(string? connectionName, List<MeasureRename> measures, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, measures, options, "Rename", "Renamed", "measures", (MeasureRename m) => m.CurrentName, delegate(BatchItemContext<MeasureRename> ctx)
		{
			RenameMeasureInternal(ctx.Connection, ctx.Item.CurrentName, ctx.Item.NewName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed measure '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed measure '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> MoveMeasures(string? connectionName, List<MeasureMove> measures, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, measures, options, "Move", "Moved", "measures", (MeasureMove m) => m.Name, delegate(BatchItemContext<MeasureMove> ctx)
		{
			MoveMeasureInternal(ctx.Connection, ctx.Item.DestinationTableName, ctx.Item.Name);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully moved measure '{ctx.Item.Name}' to table '{ctx.Item.DestinationTableName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Moved measure '{ctx.Item.Name}' to table '{ctx.Item.DestinationTableName}'");
			}
		});
	}
}
