using System.Collections.Generic;
using Integral.Web;

namespace Integral.Integrations.Amplitude {

  /// <summary>
  /// Service interface for Amplitude analytics integration.
  /// Provides methods for tracking events and identifying users.
  /// </summary>
  public interface IAmplitudeService {

    /// <summary>
    /// Track an event with optional event and user properties.
    /// This method is exception-safe and will queue the event for background processing.
    /// Supports all Amplitude user property operations ($set, $setOnce, $add, $append, etc.).
    /// </summary>
    /// <param name="userId">Required. The external user ID</param>
    /// <param name="deviceId">Optional. The amplitude device ID (session ID)</param>
    /// <param name="eventName">The name of the event to track</param>
    /// <param name="eventProperties">Optional event-specific properties</param>
    /// <param name="userPropertyOperations">Optional user property operations (supports all Amplitude operations: $set, $setOnce, $add, etc.)</param>
    void TrackEvent(ExternalUserId userId, AmplitudeDeviceId? deviceId, string eventName,
                    Dictionary<string, object> eventProperties = null,
                    List<AmplitudeUserProperty> userPropertyOperations = null);

    /// <summary>
    /// Identify a user with user properties.
    /// This method is exception-safe and will queue the identification for background processing.
    /// </summary>
    /// <param name="userId">The external user ID</param>
    /// <param name="deviceId">The amplitude device ID (session ID)</param>
    /// <param name="userProperties">List of user properties with operations ($set, $add, etc.)</param>
    void IdentifyUser(ExternalUserId? userId, AmplitudeDeviceId deviceId,
                      List<AmplitudeUserProperty> userProperties);

    /// <summary>
    /// Shutdown the service gracefully.
    /// Note: Worker threads are managed by AmplitudeBootstrap and will be shut down via IRegisteredObject.
    /// </summary>
    void Shutdown();
  }
}
