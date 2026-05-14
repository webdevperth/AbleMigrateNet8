using Integral.Web;

namespace Integral.Integrations.Intercom.Builders {
  public class SurveySharedBuilder : BaseEventBuilder<SurveySharedBuilder> {
    public SurveySharedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public SurveySharedBuilder WithSurvey(int surveyId, string surveyName) {
      AddMetadata(IntercomMetadataConstants.SurveyId, surveyId);
      AddMetadata(IntercomMetadataConstants.SurveyTitle, surveyName ?? "");
      return this;
    }

    public SurveySharedBuilder WithSharedWithUser(ExternalUserId sharedWithUserId, string sharedWithEmail, string sharedWithName) {
      AddMetadata(IntercomMetadataConstants.SharedWithUserId, sharedWithUserId.ToString());
      AddMetadata(IntercomMetadataConstants.SharedWithEmail, sharedWithEmail);
      AddMetadata(IntercomMetadataConstants.SharedWithName, sharedWithName ?? "");
      return this;
    }

    public SurveySharedBuilder WithSurveyType(string surveyType) {
      AddMetadata(IntercomMetadataConstants.SurveyType, surveyType ?? "");
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.SurveyId);
      ValidateRequiredMetadata(IntercomMetadataConstants.SharedWithEmail);
    }
  }
}
