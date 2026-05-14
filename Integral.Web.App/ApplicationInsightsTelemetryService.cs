using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System;
using System.Collections.Generic;
using System.Threading;
using Integral.Web.Services;

namespace Integral.Web {
  public enum ExternalUserKind {
    CurrentUser,
    Coach,
    Leader,
    Client,
    Admin
  }

  public struct CurrentUserDetails {
    public ExternalUserId User;
    public Guid SessionId;
  }


  /// <summary>
  /// Service implementation for sending telemetry to Application Insights using the official SDK
  /// </summary>
  public class ApplicationInsightsTelemetryService : HelperBase<ApplicationInsightsTelemetryService>, ITelemetryService {

    private readonly TelemetryClient _telemetryClient;

    // AsyncLocal storage for ExternalIntegrationUserId to propagate user context to DAL and other places where
    // we don't want to create a cyclic dependency on the web module.
    //
    // AsyncLocal flows with ExecutionContext, so it works correctly with async/await and thread pool scenarios
    // Unlike ThreadStatic, values don't leak between requests when threads are reused
    private readonly AsyncLocal<CurrentUserDetails?> _currentUserId = new AsyncLocal<CurrentUserDetails?>();

    /// <summary>
    /// Sets the current user ID for the current execution context.
    /// </summary>
    public void SetCurrentUserContext(ExternalUserId? userId, Guid? sessionGuid) {
      if (userId != null) {
        _currentUserId.Value = new CurrentUserDetails() { User = userId.Value, SessionId = sessionGuid.Value };
      }
    }

    /// <summary>
    /// Clears the current user ID for the current execution context.
    /// </summary>
    public void ClearCurrentUserContext() {
      _currentUserId.Value = null;
    }

    /// <summary>
    /// Gets the current user ID from AsyncLocal storage, or null if not set.
    /// Works correctly across async/await boundaries.
    /// </summary>
    public CurrentUserDetails? GetCurrentUserContext() {
      return _currentUserId.Value;
    }

    /// <summary>
    /// Creates a new ApplicationInsightsTelemetryService with the provided TelemetryClient.
    /// Each instance maintains its own AsyncLocal user context for thread-safe operation.
    /// </summary>
    /// <param name="telemetryClient">The TelemetryClient to use for sending telemetry. Can be null for no-op behavior.</param>
    public ApplicationInsightsTelemetryService(TelemetryClient telemetryClient) {
      _telemetryClient = telemetryClient;
    }

    /// <summary>
    /// Start building an exception telemetry event
    /// </summary>
    public IExceptionTelemetryBuilder Exception(Exception exception) {
      return new ExceptionBuilder(_telemetryClient, GetCurrentUserContext(), exception);
    }

    /// <summary>
    /// Start building a JavaScript error telemetry event
    /// </summary>
    public IJavaScriptErrorTelemetryBuilder JavaScriptError(string errorMessage, string stackTrace = null) {
      return new JavaScriptErrorBuilder(_telemetryClient, GetCurrentUserContext(), errorMessage, stackTrace);
    }

    /// <summary>
    /// Start building a custom event telemetry
    /// </summary>
    public IEventTelemetryBuilder Event(string eventName) {
      return new EventBuilder(_telemetryClient, GetCurrentUserContext(), eventName);
    }

    /// <summary>
    /// Shutdown Application Insights (flush pending telemetry)
    /// Note: TelemetryClient disposal should be handled by the creator (typically the HTTP module)
    /// </summary>
    public void Shutdown() {
      if (_telemetryClient != null) {
        LogHelper.DebugWrite("Flushing ApplicationInsights telemetry...");
        _telemetryClient.Flush();
        // Wait for telemetry to be sent
        System.Threading.Thread.Sleep(2000);
        LogHelper.DebugWrite("ApplicationInsights flushed");
      }
    }


    /// <summary>
    /// Base class for telemetry builders - never throws exceptions
    /// </summary>
    public abstract class TelemetryBuilder<T> where T : TelemetryBuilder<T> {
      protected TelemetryClient Client;
      protected Dictionary<string, string> Properties = new Dictionary<string, string>();
      protected Dictionary<string, double> Metrics = new Dictionary<string, double>();
      protected Dictionary<ExternalUserKind, ExternalUserId?> UserKeys = new Dictionary<ExternalUserKind, ExternalUserId?>();

      protected string OperationName { get; set; }
      protected string SessionId { get; set; }

      protected TelemetryBuilder(TelemetryClient client, CurrentUserDetails? currentUser) {
        Client = client;

        // If currentUser is provided, add it to UserKeys
        if (currentUser.HasValue) {
          UserKeys[ExternalUserKind.CurrentUser] = currentUser.Value.User;
          Properties[ApplicationInsightsConstants.SessionId] = $"{currentUser.Value.SessionId}";
        }

        // Automatically add environment to all telemetry
        try {
          Properties[ApplicationInsightsConstants.Environment] = ConfigHelper.EnvironmentType ?? "Unknown";
        } catch {
          // Never throw from telemetry
        }
      }

      /// <summary>
      /// Apply standard fields and custom properties to a telemetry object
      /// </summary>
      protected void ApplyToTelemetry(Microsoft.ApplicationInsights.DataContracts.ISupportProperties telemetry) {
        try {
          var telemetryItem = telemetry as Microsoft.ApplicationInsights.Channel.ITelemetry;

          // Add HTTP context first (so it can be overridden by explicit values)
          if (telemetryItem != null) {
            AddHttpContextToTelemetry(telemetryItem);
          }

          // Set standard fields (not custom properties) - these override HTTP context values
          if (telemetryItem != null) {
            // Set current user if provided
            if (UserKeys.ContainsKey(ExternalUserKind.CurrentUser)) {
              telemetryItem.Context.User.AuthenticatedUserId = UserKeys[ExternalUserKind.CurrentUser].ToString();
            }

            if (!string.IsNullOrEmpty(OperationName)) {
              telemetryItem.Context.Operation.Name = OperationName;
            }
            if (!string.IsNullOrEmpty(SessionId)) {
              telemetryItem.Context.Session.Id = SessionId;
            }
          }

          // Add environment automatically to all telemetry
          try {
            telemetry.Properties[ApplicationInsightsConstants.Environment] = ConfigHelper.EnvironmentType;
          } catch {
            // Ignore if environment cannot be determined
          }

          // Add custom properties
          foreach (var prop in Properties) {
            telemetry.Properties[prop.Key] = prop.Value;
          }

          // Add metrics (if supported)
          var metricsSupport = telemetry as Microsoft.ApplicationInsights.DataContracts.ISupportMetrics;
          if (metricsSupport != null) {
            foreach (var metric in Metrics) {
              metricsSupport.Metrics[metric.Key] = metric.Value;
            }
          }

          if (UserKeys.ContainsKey(ExternalUserKind.Coach)) {
            telemetry.Properties[ApplicationInsightsConstants.CoachId] = UserKeys[ExternalUserKind.Coach].ToString();
          }

          if (UserKeys.ContainsKey(ExternalUserKind.Leader)) {
            telemetry.Properties[ApplicationInsightsConstants.CoacheeId] = UserKeys[ExternalUserKind.Leader].ToString();
          }

          if (UserKeys.ContainsKey(ExternalUserKind.Client)) {
            telemetry.Properties[ApplicationInsightsConstants.ClientId] = UserKeys[ExternalUserKind.Client].ToString();
          }

          if (UserKeys.ContainsKey(ExternalUserKind.Admin)) {
            telemetry.Properties[ApplicationInsightsConstants.AdminId] = UserKeys[ExternalUserKind.Admin].ToString();
          }
        } catch {
          // Never throw from telemetry
        }
      }

      public T WithProperty(string key, string value) {
        try {
          if (!string.IsNullOrEmpty(key) && value != null) {
            Properties[key] = value;
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      public T WithProperty(string key, object value) {
        try {
          if (!string.IsNullOrEmpty(key) && value != null) {
            Properties[key] = value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with an integer value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, int value) {
        try {
          if (!string.IsNullOrEmpty(key)) {
            Properties[key] = value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with a nullable integer value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, int? value) {
        try {
          if (!string.IsNullOrEmpty(key) && value.HasValue) {
            Properties[key] = value.Value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with a boolean value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, bool value) {
        try {
          if (!string.IsNullOrEmpty(key)) {
            Properties[key] = value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with a nullable boolean value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, bool? value) {
        try {
          if (!string.IsNullOrEmpty(key) && value.HasValue) {
            Properties[key] = value.Value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with a Guid value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, Guid value) {
        try {
          if (!string.IsNullOrEmpty(key)) {
            Properties[key] = value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with a nullable Guid value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, Guid? value) {
        try {
          if (!string.IsNullOrEmpty(key) && value.HasValue) {
            Properties[key] = value.Value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with a long value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, long value) {
        try {
          if (!string.IsNullOrEmpty(key)) {
            Properties[key] = value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with a nullable long value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, long? value) {
        try {
          if (!string.IsNullOrEmpty(key) && value.HasValue) {
            Properties[key] = value.Value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with a decimal value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, decimal value) {
        try {
          if (!string.IsNullOrEmpty(key)) {
            Properties[key] = value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      /// <summary>
      /// Add a property with a nullable decimal value (automatically converted to string)
      /// </summary>
      public T WithProperty(string key, decimal? value) {
        try {
          if (!string.IsNullOrEmpty(key) && value.HasValue) {
            Properties[key] = value.Value.ToString();
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      public T WithMetric(string key, double value) {
        try {
          if (!string.IsNullOrEmpty(key)) {
            Metrics[key] = value;
          }
        } catch {
          // Never throw from telemetry
        }
        return (T)this;
      }

      public T WithOperation(string operationName) {
        try {
          if (!string.IsNullOrEmpty(operationName)) {
            OperationName = operationName;
          }
          return (T)this;
        } catch {
          return (T)this;
        }
      }

      /// <summary>
      /// Add operation context to provide additional detail about the operation (e.g., "IntercomException", "DatabaseTimeout")
      /// This is stored as a custom property for better filtering and grouping in Application Insights
      /// </summary>
      public T WithOperationContext(string operationContext) {
        try {
          if (!string.IsNullOrEmpty(operationContext)) {
            Properties[ApplicationInsightsConstants.OperationContext] = operationContext;
          }
          return (T)this;
        } catch {
          return (T)this;
        }
      }

      /// <summary>
      /// Set the user ID using ExternalIntegrationUserId for consistency with other external integrations
      /// </summary>
      public T AddExternalUserId(ExternalUserKind externalUserKind = ExternalUserKind.CurrentUser, ExternalUserId? userId = null) {
        if (!userId.HasValue) {
          return (T)this;
        }

        UserKeys[externalUserKind] = userId;
        return (T)this;
      }

      public T WithPageUrl(string url) {
        try {
          return WithProperty(ApplicationInsightsConstants.PageUrl, url ?? "null");
        } catch {
          return (T)this;
        }
      }

      public T WithHttpMethod(string method) {
        try {
          return WithProperty(ApplicationInsightsConstants.HttpMethod, method ?? "null");
        } catch {
          return (T)this;
        }
      }

      public T WithReferrer(string referrer) {
        try {
          return WithProperty(ApplicationInsightsConstants.Referrer, referrer ?? "null");
        } catch {
          return (T)this;
        }
      }

      public abstract void Track();
    }

    /// <summary>
    /// Builder for exception telemetry - never throws exceptions
    /// </summary>
    public class ExceptionBuilder : TelemetryBuilder<ExceptionBuilder>, IExceptionTelemetryBuilder {
      private readonly Exception _exception;

      internal ExceptionBuilder(TelemetryClient client, CurrentUserDetails? currentUser, Exception exception)
        : base(client, currentUser) {
        _exception = exception;
      }

      // Explicit interface implementations
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, string value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, object value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, int value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, int? value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, bool value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, bool? value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, Guid value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, Guid? value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, long value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, long? value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, decimal value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithProperty(string key, decimal? value) => WithProperty(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithMetric(string key, double value) => WithMetric(key, value);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithOperation(string operationName) => WithOperation(operationName);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithOperationContext(string operationContext) => WithOperationContext(operationContext);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.AddExternalUserId(ExternalUserKind externalUserKind, ExternalUserId? userId) => AddExternalUserId(externalUserKind, userId);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithPageUrl(string url) => WithPageUrl(url);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithHttpMethod(string method) => WithHttpMethod(method);
      IExceptionTelemetryBuilder ITelemetryBuilder<IExceptionTelemetryBuilder>.WithReferrer(string referrer) => WithReferrer(referrer);

      public override void Track() {
        try {
          if (Client == null || _exception == null) {
            LogHelper.DebugWrite($"ApplicationInsights ExceptionBuilder.Track() skipped: Client={Client != null}, Exception={_exception != null}");
            return;
          }

          var exceptionTelemetry = new ExceptionTelemetry(_exception);
          ApplyToTelemetry(exceptionTelemetry);
          Client.TrackException(exceptionTelemetry);
          LogHelper.DebugWrite($"ApplicationInsights exception tracked: {_exception.GetType().Name} - {_exception.Message}");
        } catch (Exception ex) {
          // Log the error so we can diagnose why exception tracking is failing
          LogHelper.LogError($"ApplicationInsights exception tracking failed: {ex.Message} - Original exception: {_exception?.Message}");
        }
      }
    }

    /// <summary>
    /// Builder for event telemetry - never throws exceptions
    /// </summary>
    public class EventBuilder : TelemetryBuilder<EventBuilder>, IEventTelemetryBuilder {
      private readonly string _eventName;

      internal EventBuilder(TelemetryClient client, CurrentUserDetails? currentUser, string eventName)
        : base(client, currentUser) {
        _eventName = eventName;
      }

      // Explicit interface implementations
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, string value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, object value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, int value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, int? value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, bool value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, bool? value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, Guid value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, Guid? value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, long value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, long? value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, decimal value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, decimal? value) => WithProperty(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithMetric(string key, double value) => WithMetric(key, value);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithOperation(string operationName) => WithOperation(operationName);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithOperationContext(string operationContext) => WithOperationContext(operationContext);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.AddExternalUserId(ExternalUserKind externalUserKind, ExternalUserId? userId) => AddExternalUserId(externalUserKind, userId);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithPageUrl(string url) => WithPageUrl(url);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithHttpMethod(string method) => WithHttpMethod(method);
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithReferrer(string referrer) => WithReferrer(referrer);

      public override void Track() {
        try {
          if (Client == null || string.IsNullOrWhiteSpace(_eventName)) return;

          var eventTelemetry = new EventTelemetry(_eventName);
          ApplyToTelemetry(eventTelemetry);
          Client.TrackEvent(eventTelemetry);
        } catch {
          // Never throw from telemetry
        }
      }
    }

    /// <summary>
    /// Builder for JavaScript error telemetry - never throws exceptions
    /// </summary>
    public class JavaScriptErrorBuilder : TelemetryBuilder<JavaScriptErrorBuilder>, IJavaScriptErrorTelemetryBuilder {
      private readonly string _errorMessage;
      private readonly string _stackTrace;

      internal JavaScriptErrorBuilder(TelemetryClient client, CurrentUserDetails? currentUser, string errorMessage, string stackTrace = null)
        : base(client, currentUser) {
        _errorMessage = errorMessage;
        _stackTrace = stackTrace;
      }

      // Explicit interface implementations for ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, string value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, object value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, int value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, int? value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, bool value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, bool? value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, Guid value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, Guid? value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, long value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, long? value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, decimal value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithProperty(string key, decimal? value) => WithProperty(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithMetric(string key, double value) => WithMetric(key, value);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithOperation(string operationName) => WithOperation(operationName);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithOperationContext(string operationContext) => WithOperationContext(operationContext);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.AddExternalUserId(ExternalUserKind externalUserKind, ExternalUserId? userId) => AddExternalUserId(externalUserKind, userId);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithPageUrl(string url) => WithPageUrl(url);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithHttpMethod(string method) => WithHttpMethod(method);
      IJavaScriptErrorTelemetryBuilder ITelemetryBuilder<IJavaScriptErrorTelemetryBuilder>.WithReferrer(string referrer) => WithReferrer(referrer);

      public IJavaScriptErrorTelemetryBuilder WithErrorUrl(string errorUrl) {
        try {
          return WithProperty(ApplicationInsightsConstants.ErrorUrl, errorUrl ?? "null");
        } catch {
          return this;
        }
      }

      public IJavaScriptErrorTelemetryBuilder WithErrorLine(int? errorLine) {
        try {
          return WithProperty(ApplicationInsightsConstants.ErrorLine, errorLine?.ToString() ?? "null");
        } catch {
          return this;
        }
      }

      public IJavaScriptErrorTelemetryBuilder WithErrorColumn(int? errorColumn) {
        try {
          return WithProperty(ApplicationInsightsConstants.ErrorColumn, errorColumn?.ToString() ?? "null");
        } catch {
          return this;
        }
      }

      public IJavaScriptErrorTelemetryBuilder WithBrowserUserAgent(string userAgent) {
        try {
          return WithProperty(ApplicationInsightsConstants.BrowserUserAgent, userAgent ?? "null");
        } catch {
          return this;
        }
      }

      public IJavaScriptErrorTelemetryBuilder WithQueryString(string queryString) {
        try {
          return WithProperty(ApplicationInsightsConstants.QueryString, queryString ?? "null");
        } catch {
          return this;
        }
      }

      public override void Track() {
        try {
          if (Client == null) return;

          // Create a JavaScript exception
          var jsException = new Exception(_errorMessage);
          var exceptionTelemetry = new ExceptionTelemetry(jsException);

          // Add stack trace as a property
          if (!string.IsNullOrWhiteSpace(_stackTrace)) {
            Properties[ApplicationInsightsConstants.JavaScriptStackTrace] = _stackTrace;
          }

          // Mark this as a JavaScript error
          Properties[ApplicationInsightsConstants.IsJavaScriptError] = "true";

          // Apply standard fields and custom properties
          ApplyToTelemetry(exceptionTelemetry);

          Client.TrackException(exceptionTelemetry);
        } catch {
          // Never throw from telemetry
        }
      }
    }

    /// <summary>
    /// Add HTTP context to telemetry using standard Application Insights fields
    /// </summary>
    private static void AddHttpContextToTelemetry(Microsoft.ApplicationInsights.Channel.ITelemetry telemetry) {
      try {
        if (SystemWeb.HasRequest) {
          // Use standard Application Insights fields
          telemetry.Context.Operation.Name = $"{SystemWeb.RequestMethod} {SystemWeb.RequestRawUrl}";

          // Add custom properties for things not covered by standard fields
          var telemetryWithProps = telemetry as Microsoft.ApplicationInsights.DataContracts.ISupportProperties;
          if (telemetryWithProps != null) {
            telemetryWithProps.Properties[ApplicationInsightsConstants.RequestUserAgent] = SystemWeb.RequestUserAgent;
          }
        }
      } catch {
        // Ignore errors getting HTTP context
      }
    }
  }

  /// <summary>
  /// No-op implementation of ITelemetryService that discards all telemetry
  /// Used when Application Insights is not configured
  /// </summary>
  public class NoOpTelemetryService : ITelemetryService {
    public IExceptionTelemetryBuilder Exception(Exception exception) => new NoOpTelemetryBuilder();
    public IJavaScriptErrorTelemetryBuilder JavaScriptError(string errorMessage, string stackTrace = null) => new NoOpJavaScriptErrorTelemetryBuilder();
    public IEventTelemetryBuilder Event(string eventName) => new NoOpTelemetryBuilder();
    public void SetCurrentUserContext(ExternalUserId? userId, Guid? sessionGuid) { }
    public CurrentUserDetails? GetCurrentUserContext() => null;
    public void ClearCurrentUserContext() { }
    public void Shutdown() { }

    private class NoOpTelemetryBuilder : IExceptionTelemetryBuilder, IEventTelemetryBuilder {
      public IExceptionTelemetryBuilder WithProperty(string key, string value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, object value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, int value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, int? value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, bool value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, bool? value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, Guid value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, Guid? value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, long value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, long? value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, decimal value) => this;
      public IExceptionTelemetryBuilder WithProperty(string key, decimal? value) => this;
      public IExceptionTelemetryBuilder WithMetric(string key, double value) => this;
      public IExceptionTelemetryBuilder WithOperation(string operationName) => this;
      public IExceptionTelemetryBuilder WithOperationContext(string operationContext) => this;
      public IExceptionTelemetryBuilder AddExternalUserId(ExternalUserKind externalUserKind = ExternalUserKind.CurrentUser, ExternalUserId? userId = null) => this;
      public IExceptionTelemetryBuilder WithPageUrl(string url) => this;
      public IExceptionTelemetryBuilder WithHttpMethod(string method) => this;
      public IExceptionTelemetryBuilder WithReferrer(string referrer) => this;
      public void Track() { }

      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, string value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, object value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, int value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, int? value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, bool value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, bool? value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, Guid value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, Guid? value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, long value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, long? value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, decimal value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithProperty(string key, decimal? value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithMetric(string key, double value) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithOperation(string operationName) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithOperationContext(string operationContext) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.AddExternalUserId(ExternalUserKind externalUserKind, ExternalUserId? userId) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithPageUrl(string url) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithHttpMethod(string method) => this;
      IEventTelemetryBuilder ITelemetryBuilder<IEventTelemetryBuilder>.WithReferrer(string referrer) => this;
    }

    private class NoOpJavaScriptErrorTelemetryBuilder : IJavaScriptErrorTelemetryBuilder {
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, string value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, object value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, int value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, int? value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, bool value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, bool? value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, Guid value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, Guid? value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, long value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, long? value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, decimal value) => this;
      public IJavaScriptErrorTelemetryBuilder WithProperty(string key, decimal? value) => this;
      public IJavaScriptErrorTelemetryBuilder WithMetric(string key, double value) => this;
      public IJavaScriptErrorTelemetryBuilder WithOperation(string operationName) => this;
      public IJavaScriptErrorTelemetryBuilder WithOperationContext(string operationContext) => this;
      public IJavaScriptErrorTelemetryBuilder AddExternalUserId(ExternalUserKind externalUserKind = ExternalUserKind.CurrentUser, ExternalUserId? userId = null) => this;
      public IJavaScriptErrorTelemetryBuilder WithPageUrl(string url) => this;
      public IJavaScriptErrorTelemetryBuilder WithHttpMethod(string method) => this;
      public IJavaScriptErrorTelemetryBuilder WithReferrer(string referrer) => this;
      public IJavaScriptErrorTelemetryBuilder WithErrorUrl(string errorUrl) => this;
      public IJavaScriptErrorTelemetryBuilder WithErrorLine(int? errorLine) => this;
      public IJavaScriptErrorTelemetryBuilder WithErrorColumn(int? errorColumn) => this;
      public IJavaScriptErrorTelemetryBuilder WithBrowserUserAgent(string userAgent) => this;
      public IJavaScriptErrorTelemetryBuilder WithQueryString(string queryString) => this;
      public void Track() { }
    }
  }
}
