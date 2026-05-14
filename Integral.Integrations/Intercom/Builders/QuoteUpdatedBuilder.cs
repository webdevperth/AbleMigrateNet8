
namespace Integral.Integrations.Intercom.Builders {
  public class QuoteUpdatedBuilder : BaseEventBuilder<QuoteUpdatedBuilder> {
    public QuoteUpdatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public QuoteUpdatedBuilder WithQuote(int quoteId, string quoteTitle) {
      AddMetadata(IntercomMetadataConstants.QuoteId, quoteId);
      AddMetadata(IntercomMetadataConstants.QuoteTitle, quoteTitle ?? "");
      return this;
    }

    public QuoteUpdatedBuilder WithClientCompany(int? clientCompanyId, string clientCompanyName) {
      if (clientCompanyId.HasValue) {
        AddMetadata(IntercomMetadataConstants.ClientCompanyId, clientCompanyId.Value);
      }

      AddMetadata(IntercomMetadataConstants.ClientCompanyName, clientCompanyName ?? "");
      return this;
    }

    public QuoteUpdatedBuilder WithQuoteValue(decimal quoteValue) {
      // Convert decimal price to cents for monetary amount
      int quoteValueInCents = (int)(quoteValue * 100);
      AddMetadataMonetary(IntercomMetadataConstants.QuoteValue, quoteValueInCents);
      return this;
    }

    protected override void Validate() {
      base.Validate();
      ValidateRequiredMetadata(IntercomMetadataConstants.QuoteId);
    }
  }
}
