
namespace Integral.Integrations.Intercom.Builders {
  public class CalendlyUrlUpdatedBuilder : BaseEventBuilder<CalendlyUrlUpdatedBuilder> {
    public CalendlyUrlUpdatedBuilder(IntercomEventQueue queue, string eventName) : base(queue, eventName) {
    }

    public CalendlyUrlUpdatedBuilder WithCalendlyUrlName(string calendlyUrlName) {
      AddMetadata(IntercomMetadataConstants.CalendlyUrlName, calendlyUrlName);
      return this;
    }

    public CalendlyUrlUpdatedBuilder WithOldCalendlyUrlName(string oldCalendlyUrlName) {
      AddMetadata(IntercomMetadataConstants.OldCalendlyUrlName, oldCalendlyUrlName);
      return this;
    }
  }
}
