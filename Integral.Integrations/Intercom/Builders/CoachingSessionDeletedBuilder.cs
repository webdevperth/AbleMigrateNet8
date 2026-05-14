using Integral.Web;

namespace Integral.Integrations.Intercom.Builders {
  public class CoachingSessionDeletedBuilder : BaseEventBuilder<CoachingSessionDeletedBuilder> {
    public CoachingSessionDeletedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public CoachingSessionDeletedBuilder WithSessionId(int sessionId) {
      AddMetadata(IntercomMetadataConstants.SessionId, sessionId);
      return this;
    }

    public CoachingSessionDeletedBuilder WithSource(string source) {
      AddMetadata(IntercomMetadataConstants.Source, source);
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.SessionId);
    }

    public CoachingSessionDeletedBuilder WithCoach(ExternalUserId? coachId) {
      if (!coachId.HasValue) {
        return this;
      }

      AddMetadata(IntercomMetadataConstants.CoachUserId, coachId.Value.ToString());
      return this;

    }
  }
}
