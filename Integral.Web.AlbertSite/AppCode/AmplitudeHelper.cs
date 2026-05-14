using Integral.Integrations.Amplitude;
using Integral.Web.Services;
using System;

namespace Integral.Web {

  public class AmplitudeHelper {

    /// <summary>
    /// Safely sends an Amplitude event with automatic exception handling.
    /// Guards against exceptions during event tracking.
    /// Uses GetService for both Amplitude and telemetry (not GetRequiredService) as they may not be available depending on timing.
    /// This method is exception-safe and will never throw.
    /// </summary>
    /// <param name="trackEvent">Callback that tracks the event using the Amplitude service.</param>
    /// <param name="operationName">Name for telemetry tracking (e.g., "TrackPageView")</param>
    /// <param name="eventName">The name of the event being tracked (for logging purposes)</param>
    /// <param name="request">Optional HttpRequest for logging the page URL</param>
    /// <param name="telemetryProperties">Optional properties to include in telemetry if an exception occurs</param>
    public static void SendEvent(
      Action<IAmplitudeService> trackEvent,
      string operationName,
      string eventName = null,
      string requestRawUrl = null,
      System.Collections.Generic.Dictionary<string, object> telemetryProperties = null) {

      try {
        var amplitude = ServiceLocator.Instance.GetService<IAmplitudeService>();
        if (amplitude != null) {
          trackEvent(amplitude);
        }
      } catch (Exception ex) {
        // Telemetry service might not be available yet depending on timing
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        if (telemetry != null) {
          var telemetryBuilder = telemetry.Exception(ex)
            .WithOperation(operationName);

          if (requestRawUrl != null) {
            telemetryBuilder.WithPageUrl(requestRawUrl);
          }

          if (!string.IsNullOrEmpty(eventName)) {
            telemetryBuilder.WithProperty("EventName", eventName);
          }

          if (telemetryProperties != null) {
            foreach (var prop in telemetryProperties) {
              telemetryBuilder.WithProperty(prop.Key, prop.Value);
            }
          }

          telemetryBuilder.Track();
        } else {
          // Fallback to debug logging if telemetry service is not available
          LogHelper.DebugWrite($"Failed to send Amplitude event '{operationName}' ({eventName ?? "unknown"}): {ex.Message}");
        }
      }
    }

    /// <summary>
    /// Convenience overload that automatically captures Request from current Http Context.
    /// </summary>
    public static void SendEvent(
      Action<IAmplitudeService> trackEvent,
      string operationName,
      string eventName,
      System.Collections.Generic.Dictionary<string, object> telemetryProperties) {

      SendEvent(trackEvent, operationName, eventName, SystemWeb.RequestRawUrl, telemetryProperties);
    }

    /// <summary>
    /// Convenience overload with no telemetry properties.
    /// </summary>
    public static void SendEvent(
      Action<IAmplitudeService> trackEvent,
      string operationName,
      string eventName = null) {

      SendEvent(trackEvent, operationName, eventName, SystemWeb.RequestRawUrl, null);
    }

    public static string GetSessionCreatedId() {

      var sessionCreated = SessionHelper.GetSessionCreatedUtc();
      return sessionCreated != null ? sessionCreated.ToString("yyyyMMddHHmmssff") : "1"; // Default to 1 if no session as in amplitude.
    }

    /// <summary>
    /// Creates an ExternalUserId from the current session's user info and role.
    /// Format: [First letter of role]-[user GUID] (e.g., "C-a1b2c3d4-e5f6-7890-abcd-ef1234567890" for Coach)
    /// </summary>
    public static ExternalUserId? GetCurrentExternalUserId() {
      var userInfo = SessionHelper.GetUserInfoOrNull();
      if (userInfo == null) {
        return null;
      }

      var userRole = SessionHelper.GetUserRole();
      return userRole.ToExternalUserId(userInfo.UserGuid);
    }

    /// <summary>
    /// Creates an AmplitudeDeviceId from the current session GUID.
    /// Format: GUID in "D" format (e.g., "00000000-0000-0000-0000-000000000000")
    /// </summary>
    public static AmplitudeDeviceId GetCurrentAmplitudeDevice() {
      return new AmplitudeDeviceId(SessionHelper.GetSessionGuid().ToString("D"));
    }

    /// <summary>
    /// Track an event with optional event properties and user properties.
    /// User properties support all Amplitude operations ($set, $setOnce, $add, $append, etc.).
    /// This method is exception-safe and will never throw.
    /// </summary>
    /// <param name="userId">Required. The external user ID to track this event for.</param>
    /// <param name="eventName">Required. The name of the event to track.</param>
    /// <param name="eventProperties">Optional event-specific properties.</param>
    /// <param name="userPropertyOperations">Optional user property operations.</param>
    /// <param name="deviceId">Optional. The Amplitude device ID (session ID).</param>
    public static void TrackEvent(ExternalUserId userId, string eventName, System.Collections.Generic.Dictionary<string, object> eventProperties = null, System.Collections.Generic.List<AmplitudeUserProperty> userPropertyOperations = null, AmplitudeDeviceId? deviceId = null) {
      try {
        var service = ServiceLocator.Instance.GetService<IAmplitudeService>();
        service?.TrackEvent(userId, deviceId, eventName, eventProperties, userPropertyOperations);
      } catch (System.Exception ex) {
        LogHelper.DebugWrite($"Failed to queue Amplitude event {eventName}: {ex.Message}");
      }
    }

    /// <summary>
    /// Track an event for a specific user (for use in webhook handlers or background tasks without session).
    /// This method is exception-safe and will never throw.
    /// </summary>
    /// <param name="userId">Required. The external user ID to track this event for.</param>
    /// <param name="eventName">Required. The name of the event to track.</param>
    /// <param name="eventProperties">Optional event-specific properties.</param>
    /// <param name="deviceId">Optional. The Amplitude device ID (session ID).</param>
    public static void TrackEventForUser(ExternalUserId userId, string eventName, System.Collections.Generic.Dictionary<string, object> eventProperties = null, AmplitudeDeviceId? deviceId = null) {
      try {
        var service = ServiceLocator.Instance.GetService<IAmplitudeService>();
        service?.TrackEvent(userId, deviceId, eventName, eventProperties, userPropertyOperations: null);
      } catch (System.Exception ex) {
        LogHelper.DebugWrite($"Failed to queue Amplitude event {eventName}: {ex.Message}");
      }
    }

  }

  /// <summary>
  /// Extension methods for Amplitude-specific user info processing
  /// </summary>
  public static class AmplitudeUserInfoExtensions {

    /// <summary>
    /// Gets the plan type string for a learner based on their subscription.
    /// Maps: FoundationFree -> "free", Embedding -> "embed", AI_Coaching -> "ai_coach", AnalyticsPro -> "analytics_pro"
    /// Returns null if user has no subscription or subscription is not recognized.
    /// </summary>
    public static string GetLearnerPlanType(this DbHelper.AbleUser.AbleUserInfo userInfo) {
      if (userInfo?.UserSubscription == null) {
        return null;
      }

      switch (userInfo.UserSubscription.SubscriptionId) {
        case ConfigHelper.SubscriptionId.FoundationFree:
          return "free";
        case ConfigHelper.SubscriptionId.Embedding:
          return "embed";
        case ConfigHelper.SubscriptionId.AI_Coaching:
          return "ai_coach";
        case ConfigHelper.SubscriptionId.AnalyticsPro:
          return "analytics_pro";
        default:
          return null;
      }
    }
  }
}
