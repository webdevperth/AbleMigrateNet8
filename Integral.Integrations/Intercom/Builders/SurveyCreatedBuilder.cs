
namespace Integral.Integrations.Intercom.Builders {
  public class SurveyCreatedBuilder : BaseEventBuilder<SurveyCreatedBuilder> {
    public SurveyCreatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public SurveyCreatedBuilder WithSurvey(int surveyId, string surveyTitle) {
      AddMetadata(IntercomMetadataConstants.SurveyId, surveyId);
      AddMetadata(IntercomMetadataConstants.SurveyTitle, surveyTitle ?? "");
      return this;
    }

    public SurveyCreatedBuilder WithProgram(int? programId, string programName) {
      if (programId.HasValue) {
        AddMetadata(IntercomMetadataConstants.ProgramId, programId.Value);
      }

      AddMetadata(IntercomMetadataConstants.ProgramName, programName ?? "");
      return this;
    }

    public SurveyCreatedBuilder WithParticipantCount(int participantCount) {
      AddMetadata(IntercomMetadataConstants.ParticipantCount, participantCount);
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.SurveyId);
    }
  }
}
