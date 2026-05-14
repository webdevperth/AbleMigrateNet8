using Newtonsoft.Json;
using System;

namespace Integral.Web {
  /// <summary>
  /// Value object representing an external integration User ID with the format: [Role]-[GUID]
  /// Example: "C-a1b2c3d4-e5f6-7890-abcd-ef1234567890" for a Client user
  /// Role prefixes: A=Admin, P=Provider/Coach, C=Client, L=Leader/Participant, U=Unknown
  /// </summary>
  [JsonConverter(typeof(ExternalUserIdConverter))]
  public struct ExternalUserId : IEquatable<ExternalUserId> {
    private readonly string _value;

    /// <summary>
    /// Creates an ExternalIntegrationUserId from a user role prefix and GUID
    /// </summary>
    /// <param name="rolePrefix">Single character role prefix (A, P, C, L, or U)</param>
    /// <param name="userGuid">User GUID</param>
    public ExternalUserId(char rolePrefix, Guid userGuid) {
      if (userGuid == Guid.Empty) {
        throw new ArgumentException("User GUID cannot be empty", nameof(userGuid));
      }

      _value = $"{rolePrefix}-{userGuid:D}";
    }

    /// <summary>
    /// Returns the string representation of the ExternalIntegrationUserId
    /// </summary>
    public override string ToString() {
      return _value ?? string.Empty;
    }

    /// <summary>
    /// Implicit conversion to string for convenience
    /// </summary>
    public static implicit operator string(ExternalUserId id) {
      return id.ToString();
    }

    public override bool Equals(object obj) {
      return obj is ExternalUserId other && Equals(other);
    }

    public bool Equals(ExternalUserId other) {
      return string.Equals(_value, other._value, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode() {
      return _value?.GetHashCode() ?? 0;
    }

    public static bool operator ==(ExternalUserId left, ExternalUserId right) {
      return left.Equals(right);
    }

    public static bool operator !=(ExternalUserId left, ExternalUserId right) {
      return !left.Equals(right);
    }
  }

  /// <summary>
  /// JSON converter for ExternalUserId - always serializes as string
  /// </summary>
  public class ExternalUserIdConverter : JsonConverter<ExternalUserId> {
    public override void WriteJson(JsonWriter writer, ExternalUserId value, JsonSerializer serializer) {
      writer.WriteValue(value.ToString());
    }

    public override ExternalUserId ReadJson(JsonReader reader, Type objectType, ExternalUserId existingValue, bool hasExistingValue, JsonSerializer serializer) {
      var stringValue = reader.Value?.ToString();
      if (string.IsNullOrEmpty(stringValue)) {
        throw new JsonSerializationException("ExternalUserId cannot be null or empty");
      }

      // Parse the format: [Role]-[GUID]
      var parts = stringValue.Split('-');
      if (parts.Length < 2 || parts[0].Length != 1) {
        throw new JsonSerializationException($"Invalid ExternalUserId format: {stringValue}. Expected format: [Role]-[GUID]");
      }

      var rolePrefix = parts[0][0];
      var guidString = string.Join("-", parts, 1, parts.Length - 1);

      if (!Guid.TryParse(guidString, out var userGuid)) {
        throw new JsonSerializationException($"Invalid GUID in ExternalUserId: {guidString}");
      }

      return new ExternalUserId(rolePrefix, userGuid);
    }
  }
}
