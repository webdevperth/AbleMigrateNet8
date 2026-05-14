using System;
using System.Text;
using SelectPdf;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class QuotePublicPDF : System.Web.UI.Page {

    public DbHelper.AbleQuotes.QuoteInfo QuoteInfo { get; protected set; }
    public DbHelper.AbleUser.AbleUserInfo ContactUserInfo = null;

    public string ClientFirstName = "";
    public string ClientLastName = "";
    public string ClientEmailAddress = "";
    public bool QuoteGSTApplicable;
    public decimal QuoteTotalExGST;
    public decimal QuoteTotalGST;
    public decimal QuoteTotalIncGST;
    public PathHelper.QuotePublicPDFPageEnum PDFPage;

    public bool Panel_Overview_Visible = false;
    public bool Panel_Team_Visible = false;
    public bool Panel_Costing_Visible = false;
    public bool Panel_Contact_Visible = false;

    protected void Page_Load(object sender, EventArgs e) {

      string quoteGuidStr = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuoteGuid);
      if (!Guid.TryParse(quoteGuidStr, out var quoteGuid)) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return;
      }

      QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(quoteGuid);
      if (QuoteInfo == null) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return;
      }

      if (QuoteInfo.QuoteTitle.IsNullOrEmpty()) QuoteInfo.QuoteTitle = QuoteInfo.ProjectName;
      QuoteGSTApplicable = DbHelper.XeroTaxType.GetGSTApplicableFromQuoteTaxTypeOrNull(QuoteInfo.XeroTaxType).ToBooleanOrDefault(false);

      ContactUserInfo = DbHelper.AbleUser.GetUserByIdOrNull(QuoteInfo.ContactUserId, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      if (ContactUserInfo != null) {
        ClientFirstName = ContactUserInfo.FirstName;
        ClientLastName = ContactUserInfo.LastName;
        ClientEmailAddress = ContactUserInfo.EmailAddress;
      }

      // If quote accepted, remove items that were not accepted.
      if (QuoteInfo.IsAccepted) QuoteInfo.QuoteItems.RemoveAll(item => item.OptionalInfo.IsOptional && item.IsAccepted == false);

      // Total quote amount.
      QuoteTotalExGST = 0;
      foreach (var item in QuoteInfo.QuoteItems) {
        if (AddToQuoteTotal(item)) QuoteTotalExGST += item.UnitPrice.GetValueOrDefault(0) * item.Quantity.GetValueOrDefault(0);
      }
      QuoteTotalGST = Math.Round(QuoteTotalExGST / 10, 2, MidpointRounding.AwayFromZero);
      QuoteTotalIncGST = QuoteTotalExGST + QuoteTotalGST;

      if (!Enum.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuotePublicPDFPage), true, out PDFPage)) {

        RunPDF();
        return;

      } else {

        switch (PDFPage) {
          case PathHelper.QuotePublicPDFPageEnum.overview:
            Panel_Overview_Visible = true;
            break;
          case PathHelper.QuotePublicPDFPageEnum.team:
            Panel_Team_Visible = true;
            break;
          case PathHelper.QuotePublicPDFPageEnum.costing:
            Panel_Costing_Visible = true;
            break;
          case PathHelper.QuotePublicPDFPageEnum.contact:
            Panel_Contact_Visible = true;
            break;
        }
        return;
      }

    }

    void RunPDF() {

      var conv = new HtmlToPdf();

      try {

        var footerSection = new PdfHtmlSection(
          $@"<div>HelloAble Pty Ltd <a href=""mailto:admin@helloable.co"">admin@helloable.co</a></div>",
          ConfigHelper.SiteDomain);

        conv.Options.PdfDocumentInformation.Title = "Able Quote";
        conv.Options.MaxPageLoadTime = 20;
        conv.Options.PdfPageSize = PdfPageSize.A4;
        conv.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
        conv.Options.MarginTop = 20;
        conv.Options.MarginBottom = 20;
        conv.Options.MarginLeft = 30;
        conv.Options.MarginRight = 30;
        conv.Options.JavaScriptEnabled = false;
        conv.Options.RenderingEngine = RenderingEngine.WebKitRestricted;
        conv.Options.DisplayFooter = true;
        conv.Footer.Height = 20;
        conv.Footer.Add(footerSection);

        // See: https://selectpdf.com/docs/ConvertMultipleUrlsToPdf.htm
        var doc1 = conv.ConvertUrl(PathHelper.Pages.QuotePublicPDF(QuoteInfo.PublicGuid, PathHelper.QuotePublicPDFPageEnum.costing, true));
        var doc2 = conv.ConvertUrl(PathHelper.Pages.QuotePublicPDF(QuoteInfo.PublicGuid, PathHelper.QuotePublicPDFPageEnum.contact, true));
        var doc = new PdfDocument();
        doc.Append(doc1);
        doc.Append(doc2);
        doc.Save(Response.OutputStream);
        doc.Close();
        doc1.Close();
        doc2.Close();

      } catch (Exception ex) {

        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("RunPDF")
          .FromSession()
          .WithProperty("QuoteId", QuoteInfo.QuoteId)
          .WithProperty("QuoteGuid", QuoteInfo.PublicGuid.ToString())
          .WithProperty("QuoteTitle", QuoteInfo.QuoteTitle)
          .Track();

        string message = "Oops! PDF conversion failed. Please try again later.";
        if (ConfigHelper.IsDevServer) {
          message += "<br>" + ex.Message
          + "<br>SiteDomain (ensure this is correct for your environment): " + ConfigHelper.SiteDomain
          + "<br>If you're using a strict firewall, ensure that bin/Select.Html.dep can communicate out.";
        }

        WebHelper.WriteAndEnd(message);
        return;
      }

      Response.Headers.Add("Content-Disposition", "inline; filename=\"Able Quote - " + QuoteInfo.QuoteTitle + " (" + DateTime.Now.AddHours(8).ToString("d MMM yyyy") + ").pdf\"");
      WebHelper.EndRequest(WebHelper.HttpContentType.pdf);
    }

    public string GetOptional(DbHelper.AbleQuotes.QuoteInfo.QuoteItemInfo quoteItem) {
      if (quoteItem.OptionalInfo.IsOptional) {
        if (quoteItem.OptionalInfo.DefaultSelected) {
          return "Included";
        } else {
          return "Excluded";
        }
      } else {
        return "";
      }
    }

    public bool AddToQuoteTotal(DbHelper.AbleQuotes.QuoteInfo.QuoteItemInfo quoteItem) {
      if (quoteItem.OptionalInfo.IsOptional && !quoteItem.OptionalInfo.DefaultSelected) {
        return false;
      } else {
        return true;
      }
    }

    public string GetTeamHtml() {
      var html = new StringBuilder();
      foreach (var quoteTeamUser in QuoteInfo.QuoteTeamUsers) {
        var userInfo = DbHelper.AbleUser.GetUserByIdOrNull(quoteTeamUser.UserId, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
        if (userInfo == null) continue;
        html.Append(GetTeamUserHtml(userInfo));
      }
      return html.ToString();
    }

    private string GetTeamUserHtml(DbHelper.AbleUser.AbleUserInfo userInfo) {

      return $@"
        <div class=""team-person"">
          <div class=""team-photo""><img alt="""" src=""{PathHelper.Images.UserPhoto(userInfo, PathHelper.Images.UserPhotoSize.Large, true)}"" onerror=""this.className='no-img';"" /></div>
          <div class=""team-name"">{userInfo.GetFullName().HTMLEncode()}</div>
          <div class=""team-role""></div>
          <div class=""team-bio"">{userInfo.AbleBioShort.HTMLEncode()}</div>
          <div class=""team-link"">{(userInfo.AbleWebProfileUrl.IsNullOrEmpty()
            ? string.Empty
            : $@"<a href=""{userInfo.AbleWebProfileUrl.HTMLEncode()}"" target=""_blank"">Full Profile</a>")}</div>
        </div>";
    }

  }
}
