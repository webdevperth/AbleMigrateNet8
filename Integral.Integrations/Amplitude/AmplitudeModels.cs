using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Integral.Web;
using Integral.Web.Services;

namespace Integral.Integrations.Amplitude {

  /// <summary>
  /// Interface for Amplitude data that can be tracked in telemetry
  /// </summary>
  public interface IAmplitudeData {
    /// <summary>
    /// Adds telemetry properties specific to this data type
    /// </summary>
    void AddTelemetryProperties(IEventTelemetryBuilder builder);
  }

  public class AmplitudeEvent : IAmplitudeData {
    [JsonProperty("user_id")]
    public ExternalUserId UserId { get; set; }

    // This is INTENTIONALLY set to device_id as that is what amplitude calls it internally, but in our domain
    // it's better known as a session id, so lets stick with that.
    [JsonProperty("device_id")]
    public AmplitudeDeviceId? SessionId { get; set; }

    [JsonProperty("event_type")]
    public string EventType { get; set; }

    [JsonProperty("time")]
    public long Time { get; set; }

    [JsonProperty("event_properties")]
    public Dictionary<string, object> EventProperties { get; set; }

    [JsonProperty("user_properties")]
    public Dictionary<string, object> UserProperties { get; set; }

    public void AddTelemetryProperties(IEventTelemetryBuilder builder) {
      builder
        .WithProperty("event_name", EventType)
        .WithProperty("user_id", UserId.ToString())
        .WithProperty("device_id", SessionId?.ToString() ?? "");

      if (EventProperties != null) {
        builder.WithProperty("event_properties", Newtonsoft.Json.JsonConvert.SerializeObject(EventProperties));
      }
    }
  }

  public class AmplitudeIdentify : IAmplitudeData {
    [JsonProperty("user_id")]
    public ExternalUserId UserId { get; set; }

    // This is INTENTIONALLY set to device_id as that is what amplitude calls it internally, but in our domain
    // it's better known as a session id, so lets stick with that.
    [JsonProperty("device_id")]
    public AmplitudeDeviceId SessionId { get; set; }

    [JsonProperty("user_properties")]

    // the identity API allows a quite complex set of operations that the event API doesnt, so use these instead
    public List<AmplitudeUserProperty> UserProperties { get; set; }

    public AmplitudeIdentify() {
      UserProperties = new List<AmplitudeUserProperty>();
    }

    public void AddTelemetryProperties(IEventTelemetryBuilder builder) {
      builder
        .WithProperty("user_id", UserId.ToString())
        .WithProperty("device_id", SessionId.ToString());
    }
  }

  /// <summary>
  /// Represents a user property with its operation type
  /// </summary>
  public class AmplitudeUserProperty {
    public string Name { get; set; }
    public object Value { get; set; }
    public AmplitudePropertyOperation Operation { get; set; }

    public AmplitudeUserProperty(string name, object value, AmplitudePropertyOperation operation) {
      Name = name;
      Value = value;
      Operation = operation;
    }

    public override bool Equals(object obj) {
      if (obj == null || GetType() != obj.GetType()) {
        return false;
      }

      var other = (AmplitudeUserProperty)obj;
      return Name == other.Name &&
             Operation == other.Operation &&
             (Value == null && other.Value == null || Value != null && Value.Equals(other.Value));
    }

    public override int GetHashCode() {
      unchecked {
        int hash = 17;
        hash = hash * 23 + (Name != null ? Name.GetHashCode() : 0);
        hash = hash * 23 + Operation.GetHashCode();
        hash = hash * 23 + (Value != null ? Value.GetHashCode() : 0);
        return hash;
      }
    }
  }

  /// <summary>
  /// Amplitude property operations as per https://amplitude.com/docs/apis/analytics/identify#userproperties-supported-operations
  /// </summary>
  public enum AmplitudePropertyOperation {
    /// <summary>Sets the value of a property</summary>
    [EnumMember(Value = "$set")]
    Set,

    /// <summary>Set the value only if the value hasn't already been set</summary>
    [EnumMember(Value = "$setOnce")]
    SetOnce,

    /// <summary>Adds a numeric value to a numeric property</summary>
    [EnumMember(Value = "$add")]
    Add,

    /// <summary>Appends the value to a user property array</summary>
    [EnumMember(Value = "$append")]
    Append,

    /// <summary>Prepends the value to a user property array</summary>
    [EnumMember(Value = "$prepend")]
    Prepend,

    /// <summary>Removes a property</summary>
    [EnumMember(Value = "$unset")]
    Unset,

    /// <summary>Adds values to the beginning of a property list if they don't already exist</summary>
    [EnumMember(Value = "$preInsert")]
    PreInsert,

    /// <summary>Adds values to the end of a property list if they don't already exist</summary>
    [EnumMember(Value = "$postInsert")]
    PostInsert,

    /// <summary>Removes all instances of the values specified from the list</summary>
    [EnumMember(Value = "$remove")]
    Remove
  }

  public class AmplitudeQueueItem {
    public AmplitudeQueueItemType Type { get; set; }
    public IAmplitudeData Data { get; set; }
    public DateTime EnqueuedAt { get; set; }
  }

  public enum AmplitudeQueueItemType {
    Event,
    Identify
  }


  /// <summary>
  /// Represents an Amplitude device ID (session ID in our domain).
  /// Format: GUID string in "D" format (e.g., "00000000-0000-0000-0000-000000000000")
  /// </summary>
  [JsonConverter(typeof(AmplitudeDeviceIdConverter))]
  public struct AmplitudeDeviceId {
    private readonly string _value;

    public AmplitudeDeviceId(string value) {
      if (string.IsNullOrEmpty(value)) {
        throw new ArgumentException("Device ID cannot be null or empty", nameof(value));
      }
      _value = value;
    }

    public override string ToString() {
      return _value;
    }

    public static implicit operator string(AmplitudeDeviceId deviceId) {
      return deviceId._value;
    }
  }

  /// <summary>
  /// JSON converter for AmplitudeDeviceId - always serializes as string
  /// </summary>
  public class AmplitudeDeviceIdConverter : JsonConverter<AmplitudeDeviceId> {
    public override void WriteJson(JsonWriter writer, AmplitudeDeviceId value, JsonSerializer serializer) {
      writer.WriteValue(value.ToString());
    }

    public override AmplitudeDeviceId ReadJson(JsonReader reader, Type objectType, AmplitudeDeviceId existingValue, bool hasExistingValue, JsonSerializer serializer) {
      var stringValue = reader.Value?.ToString();
      if (string.IsNullOrEmpty(stringValue)) {
        throw new JsonSerializationException("AmplitudeDeviceId cannot be null or empty");
      }
      return new AmplitudeDeviceId(stringValue);
    }
  }

  /// <summary>
  /// Extension methods for Amplitude-related types
  /// </summary>
  public static class AmplitudeExtensions {
    /// <summary>
    /// Converts a Guid to an AmplitudeDeviceId.
    /// Returns null if the Guid is null or equals Guid.Empty.
    /// </summary>
    /// <param name="guid">The Guid to convert</param>
    /// <returns>AmplitudeDeviceId or null</returns>
    public static AmplitudeDeviceId? ToAmplitudeDeviceId(this Guid? guid) {
      if (!guid.HasValue || guid.Value == Guid.Empty) {
        return null;
      }
      return new AmplitudeDeviceId(guid.Value.ToString("D"));
    }

    /// <summary>
    /// Converts a Guid to an AmplitudeDeviceId.
    /// Returns null if the Guid equals Guid.Empty.
    /// </summary>
    /// <param name="guid">The Guid to convert</param>
    /// <returns>AmplitudeDeviceId or null</returns>
    public static AmplitudeDeviceId? ToAmplitudeDeviceId(this Guid guid) {
      if (guid == Guid.Empty) {
        return null;
      }
      return new AmplitudeDeviceId(guid.ToString("D"));
    }
  }
}
