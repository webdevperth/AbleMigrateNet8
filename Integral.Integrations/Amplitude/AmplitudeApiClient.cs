using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Integral.Web;

namespace Integral.Integrations.Amplitude {

  /// <summary>
  /// API client for sending events and identify calls to Amplitude HTTP API.
  /// Used by AmplitudeWorkerThread for background processing.
  /// </summary>
  public class AmplitudeApiClient {

    private readonly string _apiKey;

    // Static HttpClient instance - best practice for connection pooling and performance
    // HttpClient is thread-safe and should be reused throughout the application lifecycle
    private static readonly HttpClient _httpClient;

    static AmplitudeApiClient() {
      // Initialize HttpClient with configurable timeout
      _httpClient = new HttpClient {
        Timeout = TimeSpan.FromSeconds(ConfigHelper.Amplitude.HttpTimeoutSeconds)
      };
    }

    public AmplitudeApiClient(string apiKey) {
      _apiKey = apiKey;
    }

    public void SendEvent(AmplitudeEvent evt) {
      var payload = new {
        api_key = _apiKey,
        events = new[] { new {
          user_id = evt.UserId,
          device_id = evt.SessionId,
          event_type = evt.EventType,
          time = evt.Time,
          event_properties = evt.EventProperties,
          user_properties = evt.UserProperties
        }}
      };

      var json = JsonConvert.SerializeObject(payload);
      var content = new StringContent(json, Encoding.UTF8, "application/json");

      PostToAmplitude(ConfigHelper.Amplitude.EventApiUrl, content);
    }

    public void SendIdentify(AmplitudeIdentify identify) {
      // Build user_properties with operations grouped
      var wrappedProperties = BuildUserPropertiesPayload(identify.UserProperties);

      // Build identification object as per Amplitude API spec
      var identificationObject = new {
        user_id = identify.UserId,
        device_id = identify.SessionId,
        user_properties = wrappedProperties
      };

      // Wrap in array (API expects array of identification objects)
      var identificationArray = new[] { identificationObject };
      var identificationJson = JsonConvert.SerializeObject(identificationArray);

      // Identify API requires x-www-form-urlencoded format with identification parameter
      var formData = new Dictionary<string, string> {
        { "api_key", _apiKey },
        { "identification", identificationJson }
      };

      var content = new FormUrlEncodedContent(formData);
      PostToAmplitude(ConfigHelper.Amplitude.IdentifyApiUrl, content);
    }

    private Dictionary<string, object> BuildUserPropertiesPayload(List<AmplitudeUserProperty> properties) {
      // Build user_properties grouped by operation type
      var operationGroups = new Dictionary<string, Dictionary<string, object>> {
        { "$set", new Dictionary<string, object>() },
        { "$setOnce", new Dictionary<string, object>() },
        { "$add", new Dictionary<string, object>() },
        { "$append", new Dictionary<string, object>() },
        { "$prepend", new Dictionary<string, object>() },
        { "$unset", new Dictionary<string, object>() },
        { "$preInsert", new Dictionary<string, object>() },
        { "$postInsert", new Dictionary<string, object>() },
        { "$remove", new Dictionary<string, object>() }
      };

      // Group properties by their operation
      foreach (var prop in properties) {
        string operationKey = GetOperationKey(prop.Operation);
        operationGroups[operationKey][prop.Name] = prop.Value;
      }

      // Remove empty operation groups
      var wrappedProperties = new Dictionary<string, object>();
      foreach (var kvp in operationGroups) {
        if (kvp.Value.Count > 0) {
          wrappedProperties[kvp.Key] = kvp.Value;
        }
      }

      return wrappedProperties;
    }

    private string GetOperationKey(AmplitudePropertyOperation operation) {
      var type = operation.GetType();
      var memberInfo = type.GetMember(operation.ToString()).FirstOrDefault();

      if (memberInfo != null) {
        var attribute = memberInfo.GetCustomAttribute<EnumMemberAttribute>();
        if (attribute != null) {
          return attribute.Value;
        }
      }

      // Fallback to $set if no attribute found
      return "$set";
    }

    private void PostToAmplitude(string url, HttpContent content) {
      // Use .Result to make synchronous call (blocking)
      var response = _httpClient.PostAsync(url, content).Result;

      // Read response content
      var responseBody = response.Content.ReadAsStringAsync().Result;

      if (!response.IsSuccessStatusCode) {
        throw new Exception($"Amplitude API returned {response.StatusCode}: {responseBody}");
      }

      LogHelper.DebugWrite($"Amplitude API response: {responseBody}");
    }
  }
}
