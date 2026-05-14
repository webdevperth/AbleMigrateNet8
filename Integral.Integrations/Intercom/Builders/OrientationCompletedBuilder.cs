using System;

namespace Integral.Integrations.Intercom.Builders {
  public class OrientationCompletedBuilder : BaseEventBuilder<OrientationCompletedBuilder> {
    public OrientationCompletedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public OrientationCompletedBuilder WithOrientationId(int orientationId) {
      AddMetadata(IntercomMetadataConstants.OrientationId, orientationId);
      return this;
    }

    public OrientationCompletedBuilder WithCompletedDate(DateTimeOffset completedDate) {
      AddMetadataDate(IntercomMetadataConstants.CompletedDate, completedDate);
      return this;
    }

    public OrientationCompletedBuilder WithDurationMinutes(int durationMinutes) {
      AddMetadata(IntercomMetadataConstants.DurationMinutes, durationMinutes);
      return this;
    }
  }
}
