using System;

namespace Integral.Integrations.Intercom {
  /// <summary>
  /// Represents an Intercom event that is queued for background processing.
  /// Retry logic is handled by Polly in IntercomBackgroundWorker.
  /// </summary>
  public class IntercomQueuedEvent {
    public Guid EventId { get; set; }
    public IntercomEvent Event { get; set; }
    public DateTimeOffset QueuedAtUtc { get; set; }

    public IntercomQueuedEvent(IntercomEvent intercomEvent) {
      EventId = Guid.NewGuid();
      Event = intercomEvent;
      QueuedAtUtc = DateTimeOffset.UtcNow;
    }
  }
}
