using Integral.Web;

namespace Integral.Integrations.Intercom.Builders {
  public class TeamMemberInvitedBuilder : BaseEventBuilder<TeamMemberInvitedBuilder> {
    public TeamMemberInvitedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public TeamMemberInvitedBuilder WithInvitedUser(ExternalUserId invitedUserId, string invitedName, string invitedEmail) {
      AddMetadata(IntercomMetadataConstants.InvitedUserId, invitedUserId.ToString());
      AddMetadata(IntercomMetadataConstants.InvitedName, invitedName ?? "");
      AddMetadata(IntercomMetadataConstants.InvitedEmail, invitedEmail);
      return this;
    }

    public TeamMemberInvitedBuilder WithInvitedRole(string invitedRole) {
      AddMetadata(IntercomMetadataConstants.InvitedRole, invitedRole);
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.InvitedEmail);
    }
  }
}
