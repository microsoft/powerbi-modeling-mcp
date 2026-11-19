using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AnalysisServices;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Console;

public class EnhancedRefreshService : IEnhancedRefreshService
{
	private readonly ModelingClientConfig _config;

	public EnhancedRefreshService(ModelingClientConfig config)
	{
		_config = config ?? throw new ArgumentNullException("config");
	}

	private static string RefreshesEndpoint(string? workspaceId, string datasetId, string? requestId = null)
	{
		string text = (string.IsNullOrEmpty(workspaceId) ? ("v1.0/myorg/datasets/" + datasetId + "/refreshes") : $"v1.0/myorg/groups/{workspaceId}/datasets/{datasetId}/refreshes");
		if (!string.IsNullOrEmpty(requestId))
		{
			return text + "/" + requestId;
		}
		return text;
	}

	public async Task<EnhancedRefreshResult> StartRefreshAsync(string? workspaceId, string datasetId, string refreshType = "automatic", string? tableName = null, string? partitionName = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		string endpoint = RefreshesEndpoint(workspaceId, datasetId);
		JsonObject requestBody = new JsonObject { ["retryCount"] = 0 };
		string text = MapRefreshType(refreshType);
		if (!string.IsNullOrEmpty(text))
		{
			requestBody["type"] = text;
		}
		if (!string.IsNullOrEmpty(tableName))
		{
			JsonObject jsonObject = new JsonObject { ["table"] = tableName };
			if (!string.IsNullOrEmpty(partitionName))
			{
				jsonObject["partition"] = partitionName;
			}
			requestBody["objects"] = new JsonArray { jsonObject };
		}
		try
		{
			using HttpClient httpClient = await GetAuthenticatedHttpClientAsync();
			StringContent content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!response.IsSuccessStatusCode)
			{
				string value = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				return new EnhancedRefreshResult
				{
					Success = false,
					Message = $"Failed to start refresh: {(int)response.StatusCode} {response.ReasonPhrase}. {value}"
				};
			}
			string text2 = null;
			if (response.Headers.TryGetValues("x-ms-request-id", out IEnumerable<string> values))
			{
				text2 = values.FirstOrDefault();
			}
			return new EnhancedRefreshResult
			{
				Success = true,
				RequestId = text2,
				Message = "Refresh started successfully. Request ID: " + (text2 ?? "(unavailable)")
			};
		}
		catch (HttpRequestException ex)
		{
			return new EnhancedRefreshResult
			{
				Success = false,
				Message = "Failed to start refresh: " + ex.Message
			};
		}
	}

	public async Task<EnhancedRefreshStatusResult> GetRefreshStatusAsync(string? workspaceId, string datasetId, string requestId, CancellationToken cancellationToken = default(CancellationToken))
	{
		string endpoint = RefreshesEndpoint(workspaceId, datasetId, requestId);
		try
		{
			using HttpClient httpClient = await GetAuthenticatedHttpClientAsync();
			using HttpResponseMessage httpResponse = await httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!httpResponse.IsSuccessStatusCode)
			{
				string value = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				return new EnhancedRefreshStatusResult
				{
					Status = "Error",
					Message = $"Failed to check refresh status: {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}. {value}"
				};
			}
			string json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			JsonNode jsonNode;
			try
			{
				jsonNode = JsonNode.Parse(json);
			}
			catch (JsonException ex)
			{
				return new EnhancedRefreshStatusResult
				{
					Status = "Error",
					Message = "Failed to parse refresh status response: " + ex.Message
				};
			}
			string text = jsonNode?["status"]?.GetValue<string>() ?? "Unknown";
			string startTime = jsonNode?["startTime"]?.GetValue<string>();
			string text2 = jsonNode?["endTime"]?.GetValue<string>();
			string text3 = jsonNode?["serviceExceptionJson"]?.ToJsonString();
			string text4 = "Refresh status: " + text + ((text2 != null) ? (" (completed at " + text2 + ")") : string.Empty);
			if (!string.IsNullOrEmpty(text3))
			{
				text4 = text4 + " Error: " + text3;
			}
			return new EnhancedRefreshStatusResult
			{
				Status = text,
				RequestId = requestId,
				StartTime = startTime,
				EndTime = text2,
				Message = text4
			};
		}
		catch (HttpRequestException ex2)
		{
			return new EnhancedRefreshStatusResult
			{
				Status = "Error",
				Message = "Failed to check refresh status: " + ex2.Message
			};
		}
	}

	public async Task CancelRefreshAsync(string? workspaceId, string datasetId, string requestId, CancellationToken cancellationToken = default(CancellationToken))
	{
		string endpoint = RefreshesEndpoint(workspaceId, datasetId, requestId);
		using HttpClient httpClient = await GetAuthenticatedHttpClientAsync();
		HttpResponseMessage response = await httpClient.DeleteAsync(endpoint, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!response.IsSuccessStatusCode)
		{
			string value = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			throw new HttpRequestException($"Failed to cancel refresh: {(int)response.StatusCode} {response.ReasonPhrase}. {value}");
		}
	}

	internal virtual async Task<HttpClient> GetAuthenticatedHttpClientAsync()
	{
		AccessToken accessToken = await AuthService.GetAccessTokenAsync();
		string uriString = "https://" + _config.XMLAEndpoint?.TrimEnd('/') + "/";
		HttpClient httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri(uriString);
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
		return httpClient;
	}

	private static string? MapRefreshType(string? refreshType)
	{
		return refreshType?.ToLowerInvariant() switch
		{
			"full" => "Full", 
			"automatic" => "Automatic", 
			"calculate" => "Calculate", 
			"dataonly" => "DataOnly", 
			"clearvalues" => "ClearValues", 
			"defragment" => "Defragment", 
			_ => null, 
		};
	}
}
