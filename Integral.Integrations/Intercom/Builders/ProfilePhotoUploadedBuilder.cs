using System;

namespace Integral.Integrations.Intercom.Builders {
  public class ProfilePhotoUploadedBuilder : BaseEventBuilder<ProfilePhotoUploadedBuilder> {
    public ProfilePhotoUploadedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public ProfilePhotoUploadedBuilder WithPhotoId(int photoId) {
      AddMetadata(IntercomMetadataConstants.PhotoId, photoId);
      return this;
    }

    public ProfilePhotoUploadedBuilder WithUploadDate(DateTimeOffset uploadDate) {
      AddMetadataDate(IntercomMetadataConstants.UploadDate, uploadDate);
      return this;
    }

    public ProfilePhotoUploadedBuilder WithPhotoUrl(string photoUrl) {
      if (!string.IsNullOrEmpty(photoUrl)) {
        AddMetadataRichLink(IntercomMetadataConstants.Photo, photoUrl, "View Photo");
      }

      return this;
    }
  }
}
