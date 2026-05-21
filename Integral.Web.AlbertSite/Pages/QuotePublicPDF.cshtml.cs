using System;
using System.Text;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class QuotePublicPDF : PageModel {

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

    public IActionResult OnGet() {

      string quoteGuidStr = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuoteGuid);
      if (!Guid.TryParse(quoteGuidStr, out var quoteGuid)) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return new EmptyResult();
      }

      QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(quoteGuid);
      if (QuoteInfo == null) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return new EmptyResult();
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

        return RunPDF();

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
        return Page();
      }

    }

    IActionResult RunPDF() {

      // PDF generation is temporarily stubbed.
      // The Select.HtmlToPdf NuGet package (v21.2.0) only ships net20/net40 assemblies and
      // is not consumable from net8.0. A replacement (e.g. PuppeteerSharp, DinkToPdf, or a
      // newer SelectPdf.NETCore SKU) needs to be wired up before re-enabling PDF generation here.
      string message = "PDF generation is temporarily unavailable while the underlying PDF library is being upgraded for .NET 8.";
      WebHelper.WriteAndEnd(message);
      return new EmptyResult();
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
