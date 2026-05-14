using System;

namespace Integral.Web.Services {
  /// <summary>
  /// Service for tracking telemetry to Application Insights
  /// </summary>
  public interface ITelemetryService {
    /// <summary>
    /// Start building an exception telemetry event
    /// </summary>
    IExceptionTelemetryBuilder Exception(Exception exception);

    /// <summary>
    /// Start building a JavaScript error telemetry event
    /// </summary>
    IJavaScriptErrorTelemetryBuilder JavaScriptError(string errorMessage, string stackTrace = null);

    /// <summary>
    /// Start building a custom event telemetry
    /// </summary>
    IEventTelemetryBuilder Event(string eventName);

    /// <summary>
    /// Sets the current user ID for the current execution context
    /// </summary>
    void SetCurrentUserContext(ExternalUserId? userId, Guid? sessionGuid);

    /// <summary>
    /// Gets the current user details from AsyncLocal storage, or null if not set
    /// </summary>
    CurrentUserDetails? GetCurrentUserContext();

    /// <summary>
    /// Clears the current user ID for the current execution context
    /// </summary>
    void ClearCurrentUserContext();

    /// <summary>
    /// Shutdown telemetry service (flush pending telemetry)
    /// </summary>
    void Shutdown();
  }

  /// <summary>
  /// Base interface for all telemetry builders
  /// </summary>
  public interface ITelemetryBuilder<T> where T : ITelemetryBuilder<T> {
    T WithProperty(string key, string value);
    T WithProperty(string key, object value);
    T WithProperty(string key, int value);
    T WithProperty(string key, int? value);
    T WithProperty(string key, bool value);
    T WithProperty(string key, bool? value);
    T WithProperty(string key, Guid value);
    T WithProperty(string key, Guid? value);
    T WithProperty(string key, long value);
    T WithProperty(string key, long? value);
    T WithProperty(string key, decimal value);
    T WithProperty(string key, decimal? value);
    T WithMetric(string key, double value);
    T WithOperation(string operationName);
    T WithOperationContext(string operationContext);
    T AddExternalUserId(ExternalUserKind externalUserKind = ExternalUserKind.CurrentUser, ExternalUserId? userId = null);
    T WithPageUrl(string url);
    T WithHttpMethod(string method);
    T WithReferrer(string referrer);
    void Track();
  }

  /// <summary>
  /// Builder for exception telemetry
  /// </summary>
  public interface IExceptionTelemetryBuilder : ITelemetryBuilder<IExceptionTelemetryBuilder> {
  }

  /// <summary>
  /// Builder for event telemetry
  /// </summary>
  public interface IEventTelemetryBuilder : ITelemetryBuilder<IEventTelemetryBuilder> {
  }

  /// <summary>
  /// Builder for JavaScript error telemetry
  /// </summary>
  public interface IJavaScriptErrorTelemetryBuilder : ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder> {
    IJavaScriptErrorTelemetryBuilder WithErrorUrl(string errorUrl);
    IJavaScriptErrorTelemetryBuilder WithErrorLine(int? errorLine);
    IJavaScriptErrorTelemetryBuilder WithErrorColumn(int? errorColumn);
    IJavaScriptErrorTelemetryBuilder WithBrowserUserAgent(string userAgent);
    IJavaScriptErrorTelemetryBuilder WithQueryString(string queryString);
  }
}
