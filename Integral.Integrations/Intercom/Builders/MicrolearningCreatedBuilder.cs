using System;

namespace Integral.Integrations.Intercom.Builders {
  public class MicrolearningCreatedBuilder : BaseEventBuilder<MicrolearningCreatedBuilder> {
    public MicrolearningCreatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public MicrolearningCreatedBuilder WithMicrolearningId(int microlearningId) {
      AddMetadata(IntercomMetadataConstants.MicrolearningId, microlearningId);
      return this;
    }

    public MicrolearningCreatedBuilder WithTitle(string title) {
      if (!string.IsNullOrEmpty(title)) {
        AddMetadata(IntercomMetadataConstants.Title, title);
      }

      return this;
    }

    public MicrolearningCreatedBuilder WithCreatedDate(DateTimeOffset createdDate) {
      AddMetadataDate(IntercomMetadataConstants.CreatedDate, createdDate);
      return this;
    }

    public MicrolearningCreatedBuilder WithCategory(string category) {
      if (!string.IsNullOrEmpty(category)) {
        AddMetadata(IntercomMetadataConstants.Category, category);
      }

      return this;
    }

    public MicrolearningCreatedBuilder WithContentType(string contentType) {
      if (!string.IsNullOrEmpty(contentType)) {
        AddMetadata(IntercomMetadataConstants.ContentType, contentType);
      }

      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.MicrolearningId);
    }
  }
}
