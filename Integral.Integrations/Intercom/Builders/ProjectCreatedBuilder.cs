using System;

namespace Integral.Integrations.Intercom.Builders {
  public class ProjectCreatedBuilder : BaseEventBuilder<ProjectCreatedBuilder> {
    public ProjectCreatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public ProjectCreatedBuilder WithProject(int? projectId, string projectName) {
      if (projectId.HasValue) {
        AddMetadata(IntercomMetadataConstants.ProjectId, projectId.Value);
      }

      AddMetadata(IntercomMetadataConstants.ProjectName, projectName ?? "");
      return this;
    }

    public ProjectCreatedBuilder WithProjectJobNumber(string projectJobNumber) {
      AddMetadata(IntercomMetadataConstants.ProjectJobNumber, projectJobNumber ?? "");
      return this;
    }

    public ProjectCreatedBuilder WithCompany(int? companyId, string companyName) {
      if (companyId.HasValue) {
        AddMetadata(IntercomMetadataConstants.CompanyId, companyId.Value);
      }

      AddMetadata(IntercomMetadataConstants.CompanyName, companyName ?? "");
      return this;
    }

    public ProjectCreatedBuilder WithProjectType(string projectType) {
      AddMetadata(IntercomMetadataConstants.ProjectType, projectType ?? "");
      return this;
    }

    public ProjectCreatedBuilder WithStartDate(DateTimeOffset? startDate) {
      if (startDate.HasValue) {
        AddMetadataDate(IntercomMetadataConstants.StartDate, startDate.Value);
      }

      return this;
    }

    public ProjectCreatedBuilder WithEndDate(DateTimeOffset? endDate) {
      if (endDate.HasValue) {
        AddMetadataDate(IntercomMetadataConstants.EndDate, endDate.Value);
      }

      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.ProjectId);
    }
  }
}
