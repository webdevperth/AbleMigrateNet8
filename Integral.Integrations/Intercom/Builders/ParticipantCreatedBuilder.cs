using Integral.Web;

namespace Integral.Integrations.Intercom.Builders {
  public class ParticipantCreatedBuilder : BaseEventBuilder<ParticipantCreatedBuilder> {
    public ParticipantCreatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public ParticipantCreatedBuilder WithParticipant(ExternalUserId participantId, string participantEmail) {
      AddMetadata(IntercomMetadataConstants.ParticipantId, participantId.ToString());
      AddMetadata(IntercomMetadataConstants.ParticipantEmail, participantEmail);
      return this;
    }

    public ParticipantCreatedBuilder WithProgram(int? programId, string programName) {
      if (programId.HasValue) {
        AddMetadata(IntercomMetadataConstants.ProgramId, programId.Value);
      }

      AddMetadata(IntercomMetadataConstants.ProgramName, programName ?? "");
      return this;
    }

    public ParticipantCreatedBuilder WithCompany(int? companyId, string companyName) {
      if (companyId.HasValue) {
        AddMetadata(IntercomMetadataConstants.CompanyId, companyId.Value);
      }

      AddMetadata(IntercomMetadataConstants.CompanyName, companyName ?? "");
      return this;
    }

    public ParticipantCreatedBuilder WithParticipantName(string participantName) {
      AddMetadata(IntercomMetadataConstants.ParticipantName, participantName ?? "");
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.ParticipantId);
    }
  }
}
