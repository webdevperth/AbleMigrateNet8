
namespace Integral.Integrations.Intercom.Builders {
  public class QuoteAcceptedBuilder : BaseEventBuilder<QuoteAcceptedBuilder> {
    public QuoteAcceptedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public QuoteAcceptedBuilder WithQuote(int quoteId, string quoteTitle) {
      AddMetadata(IntercomMetadataConstants.QuoteId, quoteId);
      AddMetadata(IntercomMetadataConstants.QuoteTitle, quoteTitle ?? "");
      return this;
    }

    public QuoteAcceptedBuilder WithClientCompany(int? clientCompanyId, string clientCompanyName) {
      if (clientCompanyId.HasValue) {
        AddMetadata(IntercomMetadataConstants.ClientCompanyId, clientCompanyId.Value);
      }

      AddMetadata(IntercomMetadataConstants.ClientCompanyName, clientCompanyName ?? "");
      return this;
    }

    public QuoteAcceptedBuilder WithQuoteValue(decimal quoteValue) {
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
