using Integral.Web;
using Stripe.Events;
using System;

namespace Integral.Integrations.Intercom.Builders {
  public class CoachingSessionUpdatedBuilder : BaseEventBuilder<CoachingSessionUpdatedBuilder> {
    public CoachingSessionUpdatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public CoachingSessionUpdatedBuilder WithSessionId(int sessionId) {
      AddMetadata(IntercomMetadataConstants.SessionId, sessionId);
      return this;
    }

    public CoachingSessionUpdatedBuilder WithOldStartTime(DateTimeOffset oldStartTime) {
      AddMetadataDate(IntercomMetadataConstants.OldStartTime, oldStartTime);
      return this;
    }

    public CoachingSessionUpdatedBuilder WithNewStartTime(DateTimeOffset newStartTime) {
      AddMetadataDate(IntercomMetadataConstants.NewStartTime, newStartTime);
      return this;
    }

    public CoachingSessionUpdatedBuilder WithSource(string source) {
      AddMetadata(IntercomMetadataConstants.Source, source);
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.SessionId);
    }

    public CoachingSessionUpdatedBuilder WithCoach(ExternalUserId? coachId) {
      if (!coachId.HasValue) {
        return this;
      }

      AddMetadata(IntercomMetadataConstants.CoachUserId, coachId.Value.ToString());
      return this;
    }
  }
}
