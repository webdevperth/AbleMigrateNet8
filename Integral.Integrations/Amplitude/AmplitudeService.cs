using Integral.Web;
using Integral.Web.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Integral.Integrations.Amplitude {

  /// <summary>
  /// Implementation of IAmplitudeService that queues events for background processing.
  /// This service is registered via ServiceLocator and uses an AmplitudeEventQueue instance.
  /// </summary>
  public class AmplitudeService : IAmplitudeService {

    private readonly AmplitudeEventQueue _queue;

    public AmplitudeService(AmplitudeEventQueue queue) {
      _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    /// <summary>
    /// Track an event with optional event and user properties.
    /// Supports all Amplitude user property operations ($set, $setOnce, $add, $append, etc.).
    /// </summary>
    public void TrackEvent(ExternalUserId userId, AmplitudeDeviceId? deviceId, string eventName,
                           Dictionary<string, object> eventProperties = null,
                           List<AmplitudeUserProperty> userPropertyOperations = null) {
      try {
        // Build user_properties with proper operation groups if provided
        Dictionary<string, object> wrappedUserProperties = null;
        if (userPropertyOperations != null && userPropertyOperations.Count > 0) {
          wrappedUserProperties = BuildUserPropertiesPayload(userPropertyOperations);
        }

        var evt = new AmplitudeEvent {
          UserId = userId,
          SessionId = deviceId,
          EventType = eventName,
          Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
          EventProperties = eventProperties,
          UserProperties = wrappedUserProperties
        };

        _queue.Enqueue(new AmplitudeQueueItem {
          Type = AmplitudeQueueItemType.Event,
          Data = evt,
          EnqueuedAt = DateTime.UtcNow
        });

      } catch (Exception ex) {
        var applicationInsights = ServiceLocator.Instance.GetService<ITelemetryService>();
        applicationInsights?.Exception(ex).Track();
      }
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
        var attribute = memberInfo.GetCustomAttribute<System.Runtime.Serialization.EnumMemberAttribute>();
        if (attribute != null) {
          return attribute.Value;
        }
      }

      // Fallback to $set if no attribute found
      return "$set";
    }

    /// <summary>
    /// Identify a user with user properties.
    /// </summary>
    public void IdentifyUser(ExternalUserId? userId, AmplitudeDeviceId deviceId,
                             List<AmplitudeUserProperty> userProperties) {
      if (!userId.HasValue) {
        return;
      }

      try {
        var identify = new AmplitudeIdentify {
          UserId = userId.Value,
          SessionId = deviceId,
          UserProperties = userProperties ?? new List<AmplitudeUserProperty>()
        };

        _queue.Enqueue(new AmplitudeQueueItem {
          Type = AmplitudeQueueItemType.Identify,
          Data = identify,
          EnqueuedAt = DateTime.UtcNow
        });

        LogHelper.DebugWrite($"Amplitude identify queued (UserId: {userId}, DeviceId: {deviceId})");
      } catch (Exception ex) {
        var applicationInsights = ServiceLocator.Instance.GetService<ITelemetryService>();
        applicationInsights?.Exception(ex).Track();
      }
    }

    /// <summary>
    /// Shutdown the service.
    /// Worker threads are managed separately by AmplitudeBootstrap via IRegisteredObject.
    /// </summary>
    public void Shutdown() {
      // No-op: Worker threads handle their own shutdown via IRegisteredObject
    }
  }

  /// <summary>
  /// No-op implementation of IAmplitudeService used when Amplitude is not configured.
  /// </summary>
  public class NoOpAmplitudeService : IAmplitudeService {

    public void TrackEvent(ExternalUserId userId, AmplitudeDeviceId? deviceId, string eventName,
                           Dictionary<string, object> eventProperties = null,
                           List<AmplitudeUserProperty> userPropertyOperations = null) {
      // No-op
    }

    public void IdentifyUser(ExternalUserId? userId, AmplitudeDeviceId deviceId,
                             List<AmplitudeUserProperty> userProperties) {
      // No-op
    }

    public void Shutdown() {
      // No-op
    }
  }
}
