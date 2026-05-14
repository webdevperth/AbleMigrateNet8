using System;

namespace Integral.Integrations.Intercom.Builders {
  public class ContractCompletedBuilder : BaseEventBuilder<ContractCompletedBuilder> {
    public ContractCompletedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public ContractCompletedBuilder WithContractId(int contractId) {
      AddMetadata(IntercomMetadataConstants.ContractId, contractId);
      return this;
    }

    public ContractCompletedBuilder WithContractType(string contractType) {
      AddMetadata(IntercomMetadataConstants.ContractType, contractType);
      return this;
    }

    public ContractCompletedBuilder WithCompletedDate(DateTimeOffset completedDate) {
      AddMetadataDate(IntercomMetadataConstants.CompletedDate, completedDate);
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.ContractId);
    }
  }
}
