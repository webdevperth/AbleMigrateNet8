using System;

namespace Integral.Integrations.Intercom.Builders {
  public class ProfileCompletedBuilder : BaseEventBuilder<ProfileCompletedBuilder> {
    public ProfileCompletedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public ProfileCompletedBuilder WithCompletionPercentage(int completionPercentage) {
      AddMetadata(IntercomMetadataConstants.CompletionPercentage, completionPercentage);
      return this;
    }

    public ProfileCompletedBuilder WithCompletedDate(DateTimeOffset completedDate) {
      AddMetadataDate(IntercomMetadataConstants.CompletedDate, completedDate);
      return this;
    }

    public ProfileCompletedBuilder WithProfileType(string profileType) {
      AddMetadata(IntercomMetadataConstants.ProfileType, profileType);
      return this;
    }
  }
}
