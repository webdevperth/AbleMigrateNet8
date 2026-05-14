using Integral.Integrations.Amplitude;
using Integral.Web.Services;
using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.AppCode {

  /// <summary>
  /// Initializes Amplitude integration on application start (Initialize) and provides the
  /// per-request user identification hook (OnEndRequest) called from Global.asax Application_EndRequest.
  /// </summary>
  public static class AmplitudeBootstrap {

    private static bool _isInitialized = false;
    private static readonly object _initLock = new object();

    private static List<AmplitudeWorkerThread> _workers;
    private static int _workerCount;
    private static string _apiKey;
    private static AmplitudeLruCache _cache;
    private static AmplitudeEventQueue _eventQueue;

    public static void Initialize() {
      if (!_isInitialized) {
        lock (_initLock) {
          if (!_isInitialized) {
            // Get API key and worker count from config
            _apiKey = ConfigHelper.Amplitude.ApiKey;

            if (string.IsNullOrEmpty(_apiKey)) {
              LogHelper.WriteStartupLogLine("Amplitude integration disabled - missing API key");

              // Register no-op service
              var noOpService = new NoOpAmplitudeService();
              ServiceLocator.Instance.Register<IAmplitudeService>(() => noOpService);

              _isInitialized = true;
              return;
            }


            try {
              // Create event queue instance
              _eventQueue = new AmplitudeEventQueue();

              // Initialize LRU cache
              _cache = new AmplitudeLruCache(
                ConfigHelper.Amplitude.SessionCacheSize,
                ConfigHelper.Amplitude.SessionCacheTtlSeconds,
                ConfigHelper.Amplitude.SessionCacheMaxAgeSeconds
              );

              // Create and register service via ServiceLocator
              var amplitudeService = new AmplitudeService(_eventQueue);
              ServiceLocator.Instance.Register<IAmplitudeService>(() => amplitudeService);
              LogHelper.WriteStartupLogLine("Amplitude service registered");

              // Initialize worker threads
              _workerCount = ConfigHelper.Amplitude.WorkerThreadCount;
              _workers = new List<AmplitudeWorkerThread>();

              for (int i = 0; i < _workerCount; i++) {
                var worker = new AmplitudeWorkerThread(i, _eventQueue, _apiKey);
                worker.Start();
                _workers.Add(worker);
              }

              LogHelper.WriteStartupLogLine($"Amplitude integration initialized with {_workerCount} workers, cache size {ConfigHelper.Amplitude.SessionCacheSize}, cache TTL {ConfigHelper.Amplitude.SessionCacheTtlSeconds}s, and max age {ConfigHelper.Amplitude.SessionCacheMaxAgeSeconds}s");
            } catch (Exception ex) {
              LogHelper.WriteStartupLogLine($"Amplitude integration failed to initialize: {ex.Message}");

              // Register no-op service as fallback
              var noOpService = new NoOpAmplitudeService();
              ServiceLocator.Instance.Register<IAmplitudeService>(() => noOpService);
            }

            _isInitialized = true;
          }
        }
      }
    }

    /// <summary>
    /// Called from Global.asax Application_EndRequest. Identifies the current user to Amplitude
    /// when their session properties have changed (deduplicated via LRU cache). No-op when
    /// Amplitude is disabled (no API key) — _eventQueue stays null in that case.
    /// </summary>
    public static void OnEndRequest() {
      if (_eventQueue == null) return;

      try {
        if (!SystemWeb.HasRequest) return;

        // Only track for logged-in users
        if (!SessionHelper.IsUserLoggedIn) return;

        // Skip resource requests
        if (IsResourceRequest(SystemWeb.RequestRawUrl)) return;

        var userInfo = SessionHelper.GetUserInfoOrNull();
        if (userInfo == null || userInfo.UserId == 0) return;

        var sessionId = SessionHelper.GetSessionGuid().ToString("D");
        if (string.IsNullOrEmpty(sessionId)) return;

        // Queue user identification (send user properties to Amplitude)
        QueueUserIdentification(userInfo.UserId, sessionId, userInfo);

      } catch (Exception ex) {
        var applicationInsights = ServiceLocator.Instance.GetService<ITelemetryService>();
        applicationInsights?.Exception(ex).Track();
      }
    }

    private static void QueueUserIdentification(int userId, string sessionId,
                                          DbHelper.AbleUser.AbleUserInfo userInfo) {

      // Create strongly-typed Amplitude identifiers
      var amplitudeUserId = AmplitudeHelper.GetCurrentExternalUserId();
      var amplitudeDeviceId = AmplitudeHelper.GetCurrentAmplitudeDevice();

      if (!amplitudeUserId.HasValue) {
        return;
      }

      var userRole = SessionHelper.GetUserRole();

      // Build structured user properties with their operations
      var userProperties = new List<AmplitudeUserProperty> {
        // $set operations - always update these
        new AmplitudeUserProperty(AmplitudeUserPropertyConstants.UserName, $"{userInfo.FirstName} {userInfo.LastName}".Trim(), AmplitudePropertyOperation.Set),
        new AmplitudeUserProperty(AmplitudeUserPropertyConstants.FirstName, userInfo.FirstName, AmplitudePropertyOperation.Set),
        new AmplitudeUserProperty(AmplitudeUserPropertyConstants.LastName, userInfo.LastName, AmplitudePropertyOperation.Set),
        new AmplitudeUserProperty(AmplitudeUserPropertyConstants.Email, userInfo.EmailAddress, AmplitudePropertyOperation.Set),
        new AmplitudeUserProperty(AmplitudeUserPropertyConstants.Environment, ConfigHelper.EnvironmentType, AmplitudePropertyOperation.Set),
        new AmplitudeUserProperty(AmplitudeUserPropertyConstants.UserRole, userRole.ToString(), AmplitudePropertyOperation.Set),
        new AmplitudeUserProperty(AmplitudeUserPropertyConstants.InternalId, userInfo.UserId, AmplitudePropertyOperation.Set),

        // $postInsert operations - add to list if not present
        new AmplitudeUserProperty(AmplitudeUserPropertyConstants.OrgId, userInfo.OrgId, AmplitudePropertyOperation.PostInsert),
        new AmplitudeUserProperty(AmplitudeUserPropertyConstants.OrgName, userInfo.OrgName ?? "", AmplitudePropertyOperation.PostInsert),
      };

      // Add client company info if present
      if (userInfo.ClientCompanyId.HasValue && userInfo.ClientCompanyId.Value > 0) {
        userProperties.Add(new AmplitudeUserProperty(AmplitudeUserPropertyConstants.ClientCompanyId, userInfo.ClientCompanyId.Value, AmplitudePropertyOperation.PostInsert));
        userProperties.Add(new AmplitudeUserProperty(AmplitudeUserPropertyConstants.ClientCompanyName, userInfo.ClientCompanyName ?? "", AmplitudePropertyOperation.PostInsert));
      }

      // Add plan type for learners if available
      var planType = userInfo.GetLearnerPlanType();
      if (!string.IsNullOrEmpty(planType)) {
        userProperties.Add(new AmplitudeUserProperty("plan_type", planType, AmplitudePropertyOperation.Set));
      }

      // Check cache - only send if properties have changed
      // Uses optimized overload that works directly with List<AmplitudeUserProperty>
      if (_cache != null && !_cache.HasChanged(sessionId, userProperties)) {
        return;
      }

      var service = ServiceLocator.Instance.GetService<IAmplitudeService>();
      service?.IdentifyUser(amplitudeUserId, amplitudeDeviceId, userProperties);
    }

    private static bool IsResourceRequest(string rawUrl) {
      var path = rawUrl.ToLower();
      return path.EndsWith(".css") || path.EndsWith(".js") ||
             path.EndsWith(".png") || path.EndsWith(".jpg") ||
             path.EndsWith(".ico") || path.EndsWith(".woff") ||
             path.EndsWith(".woff2") || path.EndsWith(".ttf") ||
             path.EndsWith(".svg") || path.EndsWith(".gif") ||
             path.EndsWith(".jpeg") || path.EndsWith(".eot") ||
             path.Contains("/images/") || path.Contains("/scripts/") ||
             path.Contains("/styles/") || path.Contains("/fonts/");
    }
  }
}
