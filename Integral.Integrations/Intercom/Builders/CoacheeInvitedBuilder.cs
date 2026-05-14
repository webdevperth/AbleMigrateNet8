namespace Integral.Integrations.Intercom.Builders {
  public class CoacheeInvitedBuilder : BaseEventBuilder<CoacheeInvitedBuilder> {
    public CoacheeInvitedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public CoacheeInvitedBuilder WithCoacheeEmailAddress(string email) {
      AddMetadata(IntercomMetadataConstants.EmailAddress, email);
      return this;
    }

    public CoacheeInvitedBuilder WithOrganisation(int orgId, string orgName) {
      AddMetadata(IntercomMetadataConstants.OrganisationId, orgId);
      AddMetadata(IntercomMetadataConstants.OrganisationName, orgName);

      return this;
    }

    protected override void Validate() {
      base.Validate();

      ValidateRequiredMetadata(IntercomMetadataConstants.EmailAddress);
      ValidateRequiredMetadata(IntercomMetadataConstants.OrganisationId);
      ValidateRequiredMetadata(IntercomMetadataConstants.OrganisationName);
    }
  }
}
