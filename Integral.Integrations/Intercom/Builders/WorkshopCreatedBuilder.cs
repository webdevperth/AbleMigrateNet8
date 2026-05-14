using System;

namespace Integral.Integrations.Intercom.Builders {
  public class WorkshopCreatedBuilder : BaseEventBuilder<WorkshopCreatedBuilder> {
    public WorkshopCreatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public WorkshopCreatedBuilder WithWorkshopId(int workshopId) {
      AddMetadata(IntercomMetadataConstants.WorkshopId, workshopId);
      return this;
    }

    public WorkshopCreatedBuilder WithFriendlyWorkshopId(string friendlyWorkshopId) {
      AddMetadata(IntercomMetadataConstants.FriendlyWorkshopId, friendlyWorkshopId ?? "");
      return this;
    }

    public WorkshopCreatedBuilder WithWorkshopTitle(string workshopTitle) {
      AddMetadata(IntercomMetadataConstants.WorkshopTitle, workshopTitle ?? "");
      return this;
    }

    public WorkshopCreatedBuilder WithProgramId(int programId) {
      AddMetadata(IntercomMetadataConstants.ProgramId, programId);
      return this;
    }

    public WorkshopCreatedBuilder WithCompanyName(string companyName) {
      AddMetadata(IntercomMetadataConstants.CompanyName, companyName ?? "");
      return this;
    }

    public WorkshopCreatedBuilder WithStartDateUtc(DateTimeOffset startDateUtc) {
      AddMetadataDate(IntercomMetadataConstants.StartDateUtc, startDateUtc);
      return this;
    }

    public WorkshopCreatedBuilder WithLocation(string location) {
      AddMetadata(IntercomMetadataConstants.Location, location ?? "");
      return this;
    }

    public WorkshopCreatedBuilder WithIsVirtual(bool isVirtual) {
      AddMetadata(IntercomMetadataConstants.IsVirtual, isVirtual ? "true" : "false");
      return this;
    }

    public WorkshopCreatedBuilder WithRevenue(decimal? revenue, string currency = "AUD") {
      if (revenue.HasValue) {
        AddMetadataMonetary(IntercomMetadataConstants.Revenue, (int)(revenue.Value * 100), currency);
      }

      return this;
    }

    public WorkshopCreatedBuilder WithKeyFacilitatorUserId(int? facilitatorUserId) {
      if (facilitatorUserId.HasValue) {
        AddMetadata(IntercomMetadataConstants.KeyFacilitatorUserId, facilitatorUserId.Value);
      }

      return this;
    }

    public WorkshopCreatedBuilder WithOrganisation(int orgId, string orgName) {
      AddMetadata(IntercomMetadataConstants.OrganisationId, orgId);
      AddMetadata(IntercomMetadataConstants.OrganisationName, orgName ?? "");
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.WorkshopId);
      ValidateRequiredMetadata(IntercomMetadataConstants.FriendlyWorkshopId);
    }
  }
}
