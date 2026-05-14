using System;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.AppCode {

  /// <summary>
  /// Initializes ApplicationInsights telemetry infrastructure on application start.
  /// Called from Global.asax Application_Start via Initialize().
  /// </summary>
  public static class ApplicationInsightsBootstrap {

    private static bool _isInitialized = false;
    private static readonly object _initLock = new object();
    private static Microsoft.ApplicationInsights.TelemetryClient _telemetryClient;
    private static readonly NoOpTelemetryService _noOpTelemetryService = new NoOpTelemetryService();

    public static void Initialize() {
      if (!_isInitialized) {
        lock (_initLock) {
          if (!_isInitialized) {
            InitializeApplicationInsightsService();
            _isInitialized = true;
          }
        }
      }
    }

    private static void InitializeApplicationInsightsService() {
      try {

        // Connection string priority: Environment variable > ConfigHelper > ApplicationInsights.config
        var config = Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration.Active;

        // If no connection string yet, try ConfigHelper (for local dev when env var isn't set)
        if (string.IsNullOrWhiteSpace(config.ConnectionString)) {
          string configConnectionString = ConfigHelper.AppInsights.ConnectionString;

          if (!string.IsNullOrWhiteSpace(configConnectionString)) {
            config.ConnectionString = configConnectionString;
          }
        }

        if (!string.IsNullOrWhiteSpace(config.ConnectionString)) {
          _telemetryClient = new Microsoft.ApplicationInsights.TelemetryClient(config);
          _telemetryClient.Context.Cloud.RoleName = ConfigHelper.EnvironmentType ?? "Unknown";
          _telemetryClient.Context.Cloud.RoleInstance = Environment.MachineName;

        } else {
          LogHelper.WriteStartupLogLine("Application Insights Telemetry DISABLED");
        }

        // Register factory that creates new service instances with the shared TelemetryClient
        // CurrentUser is captured when the builder is created via GetCurrentUserId()
        ServiceLocator.Instance.Register<ITelemetryService>(() => {
          if (_telemetryClient != null) {
            var client = new ApplicationInsightsTelemetryService(_telemetryClient);

            // only inject this if it's loaded, we dont want to take a database round trip to get it
            if (SystemWeb.HasSession && SessionHelper.UserInfo != null) {
              client.SetCurrentUserContext(SessionHelper.AsExternalUserId(), SessionHelper.GetSessionGuid());
            }

            return (ITelemetryService)client;
          } else {
            return (ITelemetryService)_noOpTelemetryService;
          }
        });

        LogHelper.WriteStartupLogLine("Application Insights telemetry service registered");

      } catch (Exception) {
        // Register no-op factory as fallback
        ServiceLocator.Instance.Register<ITelemetryService>(() => _noOpTelemetryService);
      }
    }
  }
}
