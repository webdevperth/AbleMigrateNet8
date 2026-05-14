using System;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class QuoteQwilrView : System.Web.UI.Page {

    public DbHelper.AbleQuotes.QuoteInfo QuoteInfo { get; protected set; }
    public decimal TotalAmount;
    public bool HasOptionals = false;

    protected void Page_Load(object sender, EventArgs e) {

      string quoteGuidStr = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuoteGuid);
      if (!Guid.TryParse(quoteGuidStr, out Guid quoteGuid)) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return;
      }

      QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(quoteGuid);
      if (QuoteInfo == null) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return;
      }

      // If already accepted, redirect.
      if (QuoteInfo.ClientAcceptedUtc != null) {
        WebHelper.Redirect(PathHelper.Pages.QuoteQwilrSignOff(QuoteInfo.PublicGuid));
        return;
      }

      if (QuoteInfo.QuoteTitle.IsNullOrEmpty()) QuoteInfo.QuoteTitle = QuoteInfo.ProjectName;
      TotalAmount = 0;

      HasOptionals = false;
      foreach (var opt in QuoteInfo.QuoteItems) {
        if (opt.OptionalInfo.IsOptional) { HasOptionals = true; break; }
      }
    }

    public string GetOptionOrHidden(DbHelper.AbleQuotes.QuoteInfo.QuoteItemInfo quoteItem) {
      if (quoteItem.OptionalInfo.IsOptional) {
        return "<input name=\"quoteitemid\" type=\"checkbox\" " + (quoteItem.OptionalInfo.DefaultSelected ? "checked" : "") + " value=\"" + quoteItem.QuoteItemId + "\" class=\"icheck item-optional\" />";
      } else {
        return "<input name=\"quoteitemid\" type=\"hidden\" value=\"" + quoteItem.QuoteItemId + "\" />";
      }
    }

  }
}
