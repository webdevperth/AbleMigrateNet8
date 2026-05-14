using System;
using System.Collections.Generic;
using Integral.Web;
using Integral.Integrations.Intercom;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.AppCode {

  /// <summary>
  /// Initializes Intercom integration on application start:
  ///   - IIntercomJwtService: Frontend JWT creation for widget authentication
  ///   - IIntercomEventService: Backend event tracking with queue and worker threads
  /// Workers implement IRegisteredObject so they shut down gracefully when the app pool recycles.
  /// </summary>
  public static class IntercomBootstrap {

    private static bool _isInitialized = false;
    private static readonly object _initLock = new object();
    private static List<IntercomWorkerThread> _workers;
    private static IntercomEventQueue _eventQueue;

    public static void Initialize() {
      if (!_isInitialized) {
        lock (_initLock) {
          if (!_isInitialized) {
            InitializeIntercomServices();
            _isInitialized = true;
          }
        }
      }
    }

    private static void InitializeIntercomServices() {
      try {
        // Get configuration
        string frontendApiKey = ConfigHelper.Intercom.FrontendApiKey;
        string backendAccessToken = ConfigHelper.Intercom.BackendAccessToken;
        int expirationMinutes = ConfigHelper.Intercom.ExpirationMinutes;

        bool hasBackendKey = !string.IsNullOrWhiteSpace(backendAccessToken);

        // Always initialize and register JWT service (frontend authentication)
        // Service will be a no-op if frontend API key is missing
        var jwtService = new IntercomJwtService(frontendApiKey, expirationMinutes);
        ServiceLocator.Instance.Register<IIntercomJwtService>(() => jwtService);
        LogHelper.WriteStartupLogLine("Intercom JWT service registered");

        // Always initialize and register Event service (backend tracking)
        // Create the event queue

        if (!hasBackendKey) {
          _eventQueue = new NoOpIntercomEventQueue();
        } else {
          _eventQueue = new IntercomEventQueue();
        }

        // Create the event service with the queue
        var eventService = new IntercomEventService(_eventQueue);

        // Register the service in the ServiceLocator
        ServiceLocator.Instance.Register<IIntercomEventService>(() => eventService);
        LogHelper.WriteStartupLogLine("Intercom event service registered");

        // Only start worker threads if we have a backend access token
        if (hasBackendKey) {
          InitializeWorkerThreads(backendAccessToken);
        } else {
          LogHelper.WriteStartupLogLine("Intercom worker threads not started.");
        }

      } catch (Exception ex) {
        LogHelper.WriteStartupLogLine($"Intercom initialization failed: {ex.Message}");

        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("IntercomBootstrap_Initialize")
          .Track();
      }
    }

    private static void InitializeWorkerThreads(string backendAccessToken) {
      try {
        int workerCount = ConfigHelper.Intercom.WorkerThreadCount;
        _workers = new List<IntercomWorkerThread>();

        for (int i = 0; i < workerCount; i++) {
          var worker = new IntercomWorkerThread(i, _eventQueue, backendAccessToken);
          worker.Start();
          _workers.Add(worker);
        }

        LogHelper.WriteStartupLogLine($"Intercom worker threads started: {workerCount} thread(s) processing queue");
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("IntercomBootstrap_InitWorkers")
          .Track();
      }
    }
  }
}
