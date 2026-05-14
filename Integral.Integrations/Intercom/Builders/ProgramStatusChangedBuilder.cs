
namespace Integral.Integrations.Intercom.Builders {
  public class ProgramStatusChangedBuilder : BaseEventBuilder<ProgramStatusChangedBuilder> {
    public ProgramStatusChangedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public ProgramStatusChangedBuilder WithProgram(int? programId, string programName) {
      if (programId.HasValue) {
        AddMetadata(IntercomMetadataConstants.ProgramId, programId.Value);
      }

      AddMetadata(IntercomMetadataConstants.ProgramName, programName ?? "");
      return this;
    }

    public ProgramStatusChangedBuilder WithCompany(int? companyId, string companyName) {
      if (companyId.HasValue) {
        AddMetadata(IntercomMetadataConstants.CompanyId, companyId.Value);
      }

      AddMetadata(IntercomMetadataConstants.CompanyName, companyName ?? "");
      return this;
    }

    public ProgramStatusChangedBuilder WithOldStatus(string oldStatus) {
      AddMetadata(IntercomMetadataConstants.OldStatus, oldStatus ?? "");
      return this;
    }

    public ProgramStatusChangedBuilder WithNewStatus(string newStatus) {
      AddMetadata(IntercomMetadataConstants.NewStatus, newStatus ?? "");
      return this;
    }

    public ProgramStatusChangedBuilder WithParticipantCount(int participantCount) {
      AddMetadata(IntercomMetadataConstants.ParticipantCount, participantCount);
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.ProgramId);
      ValidateRequiredMetadata(IntercomMetadataConstants.NewStatus);
    }
  }
}
