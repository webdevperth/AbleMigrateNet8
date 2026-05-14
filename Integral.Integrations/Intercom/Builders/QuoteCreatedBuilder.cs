
namespace Integral.Integrations.Intercom.Builders {
  public class QuoteCreatedBuilder : BaseEventBuilder<QuoteCreatedBuilder> {
    public QuoteCreatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public QuoteCreatedBuilder WithQuote(int quoteId, string quoteTitle) {
      AddMetadata(IntercomMetadataConstants.QuoteId, quoteId);
      AddMetadata(IntercomMetadataConstants.QuoteTitle, quoteTitle ?? "");
      return this;
    }

    public QuoteCreatedBuilder WithClientCompany(int? clientCompanyId, string clientCompanyName) {
      if (clientCompanyId.HasValue) {
        AddMetadata(IntercomMetadataConstants.ClientCompanyId, clientCompanyId.Value);
      }

      AddMetadata(IntercomMetadataConstants.ClientCompanyName, clientCompanyName ?? "");
      return this;
    }

    public QuoteCreatedBuilder WithQuoteValue(decimal quoteValue) {
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
