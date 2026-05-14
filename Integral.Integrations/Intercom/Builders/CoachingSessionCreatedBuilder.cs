using System;
using Integral.Web;

namespace Integral.Integrations.Intercom.Builders {
  public class CoachingSessionCreatedBuilder : BaseEventBuilder<CoachingSessionCreatedBuilder> {
    public CoachingSessionCreatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public CoachingSessionCreatedBuilder WithSessionId(int sessionId) {
      AddMetadata(IntercomMetadataConstants.SessionId, sessionId);
      return this;
    }

    public CoachingSessionCreatedBuilder WithCoach(ExternalUserId? coachExternalUserId) {
      if (coachExternalUserId.HasValue) {
        AddMetadata(IntercomMetadataConstants.CoachUserId, coachExternalUserId.ToString());
      }


      return this;
    }

    public CoachingSessionCreatedBuilder WithSessionStartUtc(DateTimeOffset sessionStartUtc) {
      AddMetadataDate(IntercomMetadataConstants.SessionStartUtc, sessionStartUtc);
      return this;
    }

    public CoachingSessionCreatedBuilder WithDurationMins(int durationMins) {
      AddMetadata(IntercomMetadataConstants.DurationMins, durationMins);
      return this;
    }

    public CoachingSessionCreatedBuilder WithSource(string source) {
      AddMetadata(IntercomMetadataConstants.Source, source);
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.SessionId);
    }
  }
}
