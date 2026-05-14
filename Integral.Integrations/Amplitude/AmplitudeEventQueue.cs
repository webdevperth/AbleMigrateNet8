using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Integral.Integrations.Amplitude {

  /// <summary>
  /// Queue for Amplitude events and identify operations.
  /// Instances are created and managed by AmplitudeBootstrap.
  /// </summary>
  public class AmplitudeEventQueue {

    private readonly ConcurrentQueue<AmplitudeQueueItem> _queue;
    private readonly ManualResetEvent _itemEnqueuedEvent;

    public AmplitudeEventQueue() {
      _queue = new ConcurrentQueue<AmplitudeQueueItem>();
      _itemEnqueuedEvent = new ManualResetEvent(false);
    }

    public virtual void Enqueue(AmplitudeQueueItem item) {
      _queue.Enqueue(item);
      _itemEnqueuedEvent.Set(); // Signal worker threads
    }

    public bool TryDequeue(out AmplitudeQueueItem item) {
      return _queue.TryDequeue(out item);
    }

    public WaitHandle ItemEnqueuedEvent => _itemEnqueuedEvent;

    public int Count => _queue.Count;

    public void ResetSignal() {
      if (_queue.IsEmpty) {
        _itemEnqueuedEvent.Reset();
      }
    }
  }

  /// <summary>
  /// A no-op queue that discards all events (used when Amplitude is not configured)
  /// </summary>
  internal class NoOpAmplitudeEventQueue : AmplitudeEventQueue {
    public override void Enqueue(AmplitudeQueueItem item) {
      // No-op: discard the event
    }
  }
}
