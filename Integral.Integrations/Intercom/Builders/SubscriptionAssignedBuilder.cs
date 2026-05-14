using Integral.Web;

namespace Integral.Integrations.Intercom.Builders {
  /// <summary>
  /// Builder for tracking when a subscription is assigned to a participant (admin allocation, not purchase)
  /// </summary>
  public class SubscriptionAssignedBuilder : BaseEventBuilder<SubscriptionAssignedBuilder> {
    public SubscriptionAssignedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public SubscriptionAssignedBuilder WithParticipant(ExternalUserId participantId, string participantEmail) {
      AddMetadata(IntercomMetadataConstants.ParticipantId, participantId.ToString());
      AddMetadata(IntercomMetadataConstants.ParticipantEmail, participantEmail);

      return this;
    }

    protected override void Validate() {
      base.Validate();

      ValidateRequiredMetadata(IntercomMetadataConstants.ParticipantId);
      ValidateRequiredMetadata(IntercomMetadataConstants.ParticipantEmail);
    }

    public SubscriptionAssignedBuilder WithOrganisation(int userOrgId, string userOrgName) {
      AddMetadata(IntercomMetadataConstants.OrganisationId, userOrgId);
      AddMetadata(IntercomMetadataConstants.OrganisationName, userOrgName);

      return this;
    }

    public SubscriptionAssignedBuilder WithProject(int projectInfoProjectId, string projectInfoProjectName) {
      AddMetadata(IntercomMetadataConstants.ProjectId, projectInfoProjectId);
      AddMetadata(IntercomMetadataConstants.ProjectName, projectInfoProjectName);

      return this;
    }

    public SubscriptionAssignedBuilder WithSubscriptionDetails(string subscriptionType, decimal unitPrice) {
      AddMetadata(IntercomMetadataConstants.SubscriptionType, subscriptionType);

      // Convert decimal price to cents for monetary amount
      int unitPriceInCents = (int)(unitPrice * 100);
      AddMetadataMonetary(IntercomMetadataConstants.SubscriptionUnitPrice, unitPriceInCents);

      return this;
    }
  }
}
