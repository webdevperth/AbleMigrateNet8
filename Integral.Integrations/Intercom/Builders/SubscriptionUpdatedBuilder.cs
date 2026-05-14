using Integral.Web;

namespace Integral.Integrations.Intercom.Builders {
  public class SubscriptionUpdatedBuilder : BaseEventBuilder<SubscriptionUpdatedBuilder> {
    public SubscriptionUpdatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public SubscriptionUpdatedBuilder WithParticipant(ExternalUserId participantId, string participantEmail) {
      AddMetadata(IntercomMetadataConstants.ParticipantId, participantId.ToString());
      AddMetadata(IntercomMetadataConstants.ParticipantEmail, participantEmail);

      return this;
    }

    protected override void Validate() {
      base.Validate();

      ValidateRequiredMetadata(IntercomMetadataConstants.ParticipantId);
      ValidateRequiredMetadata(IntercomMetadataConstants.ParticipantEmail);
    }

    public SubscriptionUpdatedBuilder WithOrganisation(int userOrgId, string userOrgName) {
      AddMetadata(IntercomMetadataConstants.OrganisationId, userOrgId);
      AddMetadata(IntercomMetadataConstants.OrganisationName, userOrgName);

      return this;
    }

    public SubscriptionUpdatedBuilder WithProject(int projectInfoProjectId, string projectInfoProjectName) {
      AddMetadata(IntercomMetadataConstants.ProjectId, projectInfoProjectId);
      AddMetadata(IntercomMetadataConstants.ProjectName, projectInfoProjectName);

      return this;
    }

    public SubscriptionUpdatedBuilder WithSubscriptionDetails(string subscriptionType, int quantity, decimal unitPrice) {
      AddMetadata(IntercomMetadataConstants.SubscriptionType, subscriptionType);
      AddMetadata(IntercomMetadataConstants.SubscriptionQuantity, quantity);

      // Convert decimal prices to cents for monetary amounts
      int unitPriceInCents = (int)(unitPrice * 100);
      int totalValueInCents = (int)(quantity * unitPrice * 100);

      AddMetadataMonetary(IntercomMetadataConstants.SubscriptionUnitPrice, unitPriceInCents);
      AddMetadataMonetary(IntercomMetadataConstants.SubscriptionTotalValue, totalValueInCents);

      return this;
    }
  }
}
