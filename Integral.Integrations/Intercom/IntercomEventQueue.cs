using Integral.Web;
using System.Collections.Concurrent;
using System.Threading;

namespace Integral.Integrations.Intercom {
  /// <summary>
  /// Thread-safe queue for managing Intercom events awaiting processing.
  /// Uses AutoResetEvent for efficient worker thread signaling.
  /// WARNING: Events are stored in-memory only. On application restart, all queued events are lost.
  /// </summary>
  public class IntercomEventQueue {
    private const int MaxQueueSize = 10000;

    private readonly ConcurrentQueue<IntercomQueuedEvent> _queue = new ConcurrentQueue<IntercomQueuedEvent>();
    private readonly AutoResetEvent _itemEnqueuedEvent = new AutoResetEvent(false);

    /// <summary>
    /// Event that signals when an item is enqueued (for worker thread signaling)
    /// </summary>
    public WaitHandle ItemEnqueuedEvent => _itemEnqueuedEvent;

    /// <summary>
    /// Number of items currently in the queue
    /// </summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Create a new event queue instance
    /// </summary>
    public IntercomEventQueue() { }

    /// <summary>
    /// Add a new event to the queue and signal waiting workers
    /// </summary>
    public virtual void Enqueue(IntercomEvent intercomEvent) {
      if (_queue.Count >= MaxQueueSize) {
        LogHelper.LogError(
          $"Intercom queue full ({MaxQueueSize} items), dropping event: {intercomEvent.EventName}");
        return;
      }

      var queuedEvent = new IntercomQueuedEvent(intercomEvent);
      _queue.Enqueue(queuedEvent);

      // Signal waiting worker threads that an item is available
      _itemEnqueuedEvent.Set();
    }

    /// <summary>
    /// Try to dequeue the next event ready for processing
    /// </summary>
    public bool TryDequeue(out IntercomQueuedEvent queuedEvent) {
      return _queue.TryDequeue(out queuedEvent);
    }

    /// <summary>
    /// Reset the signal (called by workers after processing batch)
    /// </summary>
    public void ResetSignal() {
      _itemEnqueuedEvent.Reset();
    }

    /// <summary>
    /// Get the current queue size
    /// </summary>
    public int GetQueueSize() {
      return _queue.Count;
    }

    /// <summary>
    /// Check if the queue has any items ready for processing
    /// </summary>
    public bool HasItemsToProcess() {
      return _queue.Count > 0;
    }
  }
}
