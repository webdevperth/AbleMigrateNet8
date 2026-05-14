using Integral.Web;
using Integral.Web.Services;
using Intercom.Clients;
using Intercom.Core;
using Intercom.Factories;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using System;
using System.Threading;

namespace Integral.Integrations.Intercom {
  /// <summary>
  /// Worker thread for processing Intercom events from the queue.
  /// Graceful shutdown is wired by the host calling Stop() during application shutdown.
  /// </summary>
  public class IntercomWorkerThread {

    private readonly int _workerId;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IntercomEventQueue _queue;
    private readonly EventsClient _eventsClient;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly int _maxRetries;
    private readonly int _batchSize;

    public IntercomWorkerThread(int workerId, IntercomEventQueue queue, string accessToken, int maxRetries = 3, int batchSize = 10) {
      if (string.IsNullOrEmpty(accessToken)) {
        throw new ArgumentException("Access token is required", nameof(accessToken));
      }

      if (queue == null) {
        throw new ArgumentNullException(nameof(queue));
      }

      _workerId = workerId;
      _queue = queue;
      _maxRetries = maxRetries;
      _batchSize = batchSize;
      _cancellationTokenSource = new CancellationTokenSource();

      // Initialize Intercom API client
      var restClientFactory = new RestClientFactory(new Authentication(accessToken));
      _eventsClient = new EventsClient(restClientFactory);

      // Initialize Polly resilience pipeline with timeout and retry
      // Total operation timeout: 120 seconds (covers all retry attempts)
      // Retry attempts: 3 with exponential backoff starting at 2 seconds
      _retryPipeline = new ResiliencePipelineBuilder()
        .AddTimeout(new TimeoutStrategyOptions {
          Timeout = TimeSpan.FromSeconds(120)
        })
        .AddRetry(new RetryStrategyOptions {
          MaxRetryAttempts = maxRetries,
          Delay = TimeSpan.FromSeconds(2),
          BackoffType = DelayBackoffType.Exponential,
          UseJitter = true,
          MaxDelay = TimeSpan.FromSeconds(40),
          ShouldHandle =
            new PredicateBuilder().Handle<Exception>(ex =>
              !(ex is OperationCanceledException || ex is ThreadAbortException || ex is TimeoutRejectedException))
        })
        .Build();

      _thread = new Thread(WorkerLoop) {
        IsBackground = true,
        Name = $"IntercomWorker-{workerId}"
      };
    }

    public void Start() {
      _thread.Start(_cancellationTokenSource.Token);

      LogHelper.DebugWrite($"Intercom worker {_workerId} started");
    }

    /// <summary>
    /// Graceful shutdown — invoked by the host during application shutdown.
    /// </summary>
    public void Stop(bool immediate) {
      LogHelper.DebugWrite($"Intercom worker {_workerId} stopping (immediate: {immediate})");

      // Signal cancellation to the worker thread
      _cancellationTokenSource.Cancel();

      if (!immediate) {
        // Try to gracefully process remaining items (max 30 seconds)
        int remainingItems = _queue.Count;
        if (remainingItems > 0) {
          LogHelper.DebugWrite($"Intercom worker {_workerId} processing {remainingItems} remaining items");
          DateTime shutdownDeadline = DateTime.UtcNow.AddSeconds(30);

          while (_queue.TryDequeue(out IntercomQueuedEvent item) && DateTime.UtcNow < shutdownDeadline) {
            try {
              ProcessEvent(item);
            } catch (Exception ex) {
              LogHelper.DebugWrite($"Intercom worker {_workerId} error during shutdown: {ex.Message}");
            }
          }
        }
      }

      // Wait for thread to finish (max 5 seconds)
      if (_thread.IsAlive) {
        _thread.Join(TimeSpan.FromSeconds(5));
      }

      // Dispose cancellation token source
      _cancellationTokenSource.Dispose();

      LogHelper.DebugWrite($"Intercom worker {_workerId} stopped");
    }

    private void WorkerLoop(object state) {
      var token = (CancellationToken)state;

      try {
        while (!token.IsCancellationRequested) {
          try {
            // Wait for items in queue (max 30 seconds) or cancellation
            var waitHandles = new[] { _queue.ItemEnqueuedEvent, token.WaitHandle };
            WaitHandle.WaitAny(waitHandles, TimeSpan.FromSeconds(30));

            // Check if we're cancelled before processing
            if (token.IsCancellationRequested) {
              break;
            }

            ProcessQueueItems(token);

          } catch (OperationCanceledException) {
            // Cancellation requested - exit gracefully
            LogHelper.DebugWrite($"Intercom worker {_workerId} cancellation requested");
            break;
          } catch (ThreadAbortException) {
            // Thread is being aborted - exit gracefully
            LogHelper.DebugWrite($"Intercom worker {_workerId} thread abort detected");
            break;
          } catch (Exception ex) {
            // Log error but continue processing - worker thread must never crash
            LogHelper.DebugWrite($"Intercom worker {_workerId} error in main loop: {ex.Message}");

            var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("IntercomWorkerThread_MainLoop")
              .WithProperty("WorkerId", _workerId)
              .Track();

            // Brief sleep to prevent tight error loops, but respect cancellation
            token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
          }
        }
      } catch (Exception ex) {
        // Ultimate safety net - should never reach here, but if it does, log and exit gracefully
        LogHelper.DebugWrite($"Intercom worker {_workerId} CRITICAL: Unhandled exception in outer loop: {ex.Message}");

        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("IntercomWorkerThread_CriticalError")
          .WithProperty("WorkerId", _workerId)
          .Track();
      }

      LogHelper.DebugWrite($"Intercom worker {_workerId} exiting main loop");
    }

    private void ProcessQueueItems(CancellationToken token) {
      try {
        int processedCount = 0;

        while (processedCount < _batchSize && !token.IsCancellationRequested && _queue.TryDequeue(out IntercomQueuedEvent item)) {
          try {
            ProcessEvent(item);
            processedCount++;
          } catch (Exception ex) {
            // Log error but continue processing other items
            LogHelper.LogError($"Intercom worker {_workerId} error processing event {item?.Event?.EventName}: {ex.Message}");

            var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("IntercomWorkerThread_ProcessEvent")
              .WithProperty("WorkerId", _workerId)
              .WithProperty("EventName", item?.Event?.EventName)
              .WithProperty("EventId", item?.EventId.ToString())
              .WithProperty(ApplicationInsightsConstants.UserEmail, item?.Event?.Email)
              .Track();
          }
        }

        // Reset signal after processing batch
        _queue.ResetSignal();

      } catch (Exception ex) {
        // Outer safety net for ProcessQueueItems
        LogHelper.LogError($"Intercom worker {_workerId} error in ProcessQueueItems: {ex.Message}");

        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("IntercomWorkerThread_ProcessQueueItems")
          .WithProperty("WorkerId", _workerId)
          .Track();
      }
    }

    private void ProcessEvent(IntercomQueuedEvent queuedEvent) {
      if (queuedEvent == null || queuedEvent.Event == null) {
        LogHelper.DebugWrite($"Intercom worker {_workerId} received null event");
        return;
      }

      try {
        // Use Polly pipeline for retry with exponential backoff
        _retryPipeline.Execute(ct => {
          // Check for cancellation
          ct.ThrowIfCancellationRequested();

          // Build and send the event
          var intercomEvent = queuedEvent.Event.Build();
          _eventsClient.Create(intercomEvent);

          LogHelper.DebugWrite(
            $"Intercom worker {_workerId} sent event successfully: {queuedEvent.Event.EventName} (EventId: {queuedEvent.EventId})");
        }, _cancellationTokenSource.Token);

      } catch (OperationCanceledException) {
        // Shutdown requested - don't log as error
        LogHelper.DebugWrite(
          $"Intercom worker {_workerId} event processing cancelled (shutdown): {queuedEvent.Event.EventName} (EventId: {queuedEvent.EventId})");
        throw; // Re-throw to stop processing loop
      } catch (TimeoutRejectedException ex) {
        // Timeout exceeded (120 seconds) - drop the event
        LogHelper.LogError(
          $"Intercom worker {_workerId} event dropped due to timeout (exceeded 120 seconds): {queuedEvent.Event.EventName} (EventId: {queuedEvent.EventId})");

        // Track timeout to Application Insights
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("IntercomWorkerThread_ProcessEvent")
          .WithOperationContext("TimeoutExceeded")
          .WithProperty("WorkerId", _workerId)
          .WithProperty("EventName", queuedEvent.Event.EventName)
          .WithProperty("EventId", queuedEvent.EventId.ToString())
          .WithProperty(ApplicationInsightsConstants.UserEmail, queuedEvent.Event.Email)
          .Track();

        // Log custom event for dropped event tracking
        TrackDroppedEvent(telemetry, queuedEvent, ex, "TimeoutExceeded");
      } catch (Exception ex) {
        // All retries exhausted or unrecoverable error
        LogHelper.LogError(
          $"Intercom worker {_workerId} event permanently failed after all retries: {queuedEvent.Event.EventName} (EventId: {queuedEvent.EventId}) - {ex.Message}");

        // Track failure to Application Insights
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("IntercomWorkerThread_ProcessEvent")
          .WithOperationContext("RetriesExhausted")
          .WithProperty("WorkerId", _workerId)
          .WithProperty("EventName", queuedEvent.Event.EventName)
          .WithProperty("EventId", queuedEvent.EventId.ToString())
          .WithProperty(ApplicationInsightsConstants.UserEmail, queuedEvent.Event.Email)
          .WithProperty("MaxRetries", _maxRetries)
          .Track();

        // Log custom event for dropped event tracking
        TrackDroppedEvent(telemetry, queuedEvent, ex, "RetriesExhausted");
      }
    }

    private void TrackDroppedEvent(ITelemetryService telemetry, IntercomQueuedEvent queuedEvent, Exception ex, string reason) {
      if (telemetry == null || queuedEvent?.Event == null) {
        return;
      }

      var eventBuilder = telemetry.Event("IntercomEventDropped")
        .WithProperty("worker_id", _workerId)
        .WithProperty("event_id", queuedEvent.EventId.ToString())
        .WithProperty("event_name", queuedEvent.Event.EventName)
        .WithProperty("reason", reason)
        .WithProperty("error_message", ex.Message)
        .WithProperty("user_id", queuedEvent.Event.UserId?.ToString() ?? "")
        .WithProperty("email", queuedEvent.Event.Email ?? "")
        .WithProperty("internal_id", queuedEvent.Event.InternalId);

      // Add metadata
      if (queuedEvent.Event.Metadata != null && queuedEvent.Event.Metadata.Count > 0) {
        eventBuilder.WithProperty("metadata", Newtonsoft.Json.JsonConvert.SerializeObject(queuedEvent.Event.Metadata));
      }

      eventBuilder.Track();
    }
  }
}
