
namespace Integral.Integrations.Intercom.Builders {
  public class UserSignedUpBuilder : BaseEventBuilder<UserSignedUpBuilder> {
    public UserSignedUpBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public UserSignedUpBuilder WithUserRole(string userRole) {
      AddMetadata(IntercomMetadataConstants.UserRole, userRole);
      return this;
    }

    public UserSignedUpBuilder WithCompanyName(string companyName) {
      AddMetadata(IntercomMetadataConstants.CompanyName, companyName);
      return this;
    }

    public UserSignedUpBuilder WithIsInvited(bool isInvited) {
      AddMetadata(IntercomMetadataConstants.IsInvited, isInvited ? "true" : "false");
      return this;
    }
  }
}
