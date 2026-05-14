using System;
using System.Threading;
using Integral.Web;
using Integral.Web.Services;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Integral.Integrations.Amplitude {

  public class AmplitudeWorkerThread {

    private readonly int _workerId;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly AmplitudeEventQueue _queue;
    private readonly AmplitudeApiClient _apiClient;
    private readonly ResiliencePipeline _retryPipeline;

    public AmplitudeWorkerThread(int workerId, AmplitudeEventQueue queue, string apiKey, int maxRetries = 5) {
      _workerId = workerId;
      _queue = queue ?? throw new ArgumentNullException(nameof(queue));
      _apiClient = new AmplitudeApiClient(apiKey);
      _cancellationTokenSource = new CancellationTokenSource();

      // Initialize Polly resilience pipeline with timeout and retry
      // Total operation timeout: 120 seconds (covers all retry attempts)
      // Retry attempts: 5 with exponential backoff starting at 2 seconds
      _retryPipeline = new ResiliencePipelineBuilder()
        .AddTimeout(new TimeoutStrategyOptions {
          Timeout = TimeSpan.FromSeconds(120)
        })
        .AddRetry(new RetryStrategyOptions {
          MaxRetryAttempts = maxRetries,
          Delay = TimeSpan.FromSeconds(2),
          BackoffType = DelayBackoffType.Exponential,
          UseJitter = true,
          MaxDelay = TimeSpan.FromSeconds(120),
          ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
            !(ex is OperationCanceledException || ex is ThreadAbortException || ex is TimeoutRejectedException))
        })
        .Build();

      _thread = new Thread(WorkerLoop) {
        IsBackground = true,
        Name = $"AmplitudeWorker-{workerId}"
      };
    }

    public void Start() {
      _thread.Start(_cancellationTokenSource.Token);

      LogHelper.DebugWrite($"Amplitude worker {_workerId} started");
    }

    // Graceful shutdown — invoked by the host during application shutdown.
    public void Stop(bool immediate) {
      LogHelper.DebugWrite($"Amplitude worker {_workerId} stopping (immediate: {immediate})");

      // Signal cancellation to the worker thread
      _cancellationTokenSource.Cancel();

      if (!immediate) {
        // Try to gracefully process remaining items (max 30 seconds)
        int remainingItems = _queue.Count;
        if (remainingItems > 0) {
          LogHelper.DebugWrite($"Amplitude worker {_workerId} processing {remainingItems} remaining items");
          DateTime shutdownDeadline = DateTime.UtcNow.AddSeconds(30);

          while (_queue.TryDequeue(out AmplitudeQueueItem item) && DateTime.UtcNow < shutdownDeadline) {
            try {
              ProcessItem(item, _cancellationTokenSource.Token);
            } catch (Exception ex) {
              LogHelper.DebugWrite($"Amplitude worker {_workerId} error during shutdown: {ex.Message}");
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

      LogHelper.DebugWrite($"Amplitude worker {_workerId} stopped");
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
            LogHelper.DebugWrite($"Amplitude worker {_workerId} cancellation requested");
            break;
          } catch (ThreadAbortException) {
            // Thread is being aborted - exit gracefully
            LogHelper.DebugWrite($"Amplitude worker {_workerId} thread abort detected");
            break;
          } catch (Exception ex) {
            var applicationInsights = ServiceLocator.Instance.GetService<ITelemetryService>();
            applicationInsights?.Exception(ex).Track();

            // Brief sleep to prevent tight error loops, but respect cancellation
            token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
          }
        }
      } catch (Exception ex) {
        var applicationInsights = ServiceLocator.Instance.GetService<ITelemetryService>();
        applicationInsights?.Exception(ex).Track();
      }

      LogHelper.DebugWrite($"Amplitude worker {_workerId} exiting main loop");
    }

    private void ProcessQueueItems(CancellationToken token) {
      try {
        int processedCount = 0;
        const int maxBatchSize = 10; // Process up to 10 items before checking signal

        while (processedCount < maxBatchSize && !token.IsCancellationRequested && _queue.TryDequeue(out AmplitudeQueueItem item)) {
          try {
            ProcessItem(item, token);
            processedCount++;
          } catch (Exception ex) {
            // Log error but continue processing other items
            LogHelper.DebugWrite($"Amplitude worker {_workerId} error processing item: {ex.Message}");
          }
        }

        _queue.ResetSignal();

      } catch (Exception ex) {
        var applicationInsights = ServiceLocator.Instance.GetService<ITelemetryService>();
        applicationInsights?.Exception(ex).Track();
      }
    }

    private void ProcessItem(AmplitudeQueueItem item, CancellationToken token) {
      if (item == null) {
        LogHelper.DebugWrite($"Amplitude worker {_workerId} received null item");
        return;
      }

      try {
        // Use Polly pipeline for retry with exponential backoff
        _retryPipeline.Execute(ct => {
          // Check for cancellation
          ct.ThrowIfCancellationRequested();

          // Send the item based on type
          switch (item.Type) {
            case AmplitudeQueueItemType.Event:
              var evt = item.Data as AmplitudeEvent;
              if (evt == null) {
                throw new Exception("Invalid event data type");
              }
              _apiClient.SendEvent(evt);
              LogHelper.DebugWrite($"Amplitude worker {_workerId} sent event successfully: {evt.EventType}");
              break;

            case AmplitudeQueueItemType.Identify:
              var identify = item.Data as AmplitudeIdentify;
              if (identify == null) {
                throw new Exception("Invalid identify data type");
              }
              _apiClient.SendIdentify(identify);
              LogHelper.DebugWrite($"Amplitude worker {_workerId} sent identify successfully");
              break;

            default:
              throw new Exception($"Unknown item type: {item.Type}");
          }
        }, token);

      } catch (OperationCanceledException) {
        LogHelper.DebugWrite($"Amplitude worker {_workerId} item processing cancelled (shutdown): {item.Type}");
        throw; // Re-throw to stop processing loop
      } catch (TimeoutRejectedException ex) {
        // Timeout exceeded (120 seconds) - drop the event
        LogHelper.LogError($"Amplitude worker {_workerId} event dropped due to timeout (exceeded 120 seconds): {item.Type}");

        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();

        // Track timeout to Application Insights
        telemetry?.Exception(ex)
          .WithOperation("AmplitudeWorkerThread_ProcessItem")
          .WithOperationContext("TimeoutExceeded")
          .WithProperty("WorkerId", _workerId)
          .WithProperty("ItemType", item.Type.ToString())
          .Track();

        // Log custom event for dropped event tracking
        TrackDroppedEvent(telemetry, item, ex, "TimeoutExceeded");
      } catch (Exception ex) {
        // All retries exhausted - event is dropped
        LogHelper.LogError($"Amplitude worker {_workerId} event permanently failed after all retries: {item.Type}");

        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();

        // Log the exception
        telemetry?.Exception(ex)
          .WithOperation("AmplitudeWorkerThread_ProcessItem")
          .WithOperationContext("RetriesExhausted")
          .WithProperty("WorkerId", _workerId)
          .WithProperty("ItemType", item.Type.ToString())
          .Track();

        // Log custom event for dropped event tracking
        TrackDroppedEvent(telemetry, item, ex, "RetriesExhausted");
      }
    }

    private void TrackDroppedEvent(ITelemetryService telemetry, AmplitudeQueueItem item, Exception ex, string reason) {
      if (telemetry == null || item?.Data == null) {
        return;
      }

      var eventBuilder = telemetry.Event("AmplitudeEventDropped")
        .WithProperty("worker_id", _workerId)
        .WithProperty("item_type", item.Type.ToString())
        .WithProperty("reason", reason)
        .WithProperty("error_message", ex.Message);

      // Use polymorphism to add type-specific properties
      item.Data.AddTelemetryProperties(eventBuilder);

      eventBuilder.Track();
    }
  }
}
