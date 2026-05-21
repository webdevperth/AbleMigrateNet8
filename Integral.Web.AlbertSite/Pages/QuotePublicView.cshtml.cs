using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class QuotePublicView : PageModel {

    public DbHelper.AbleQuotes.QuoteInfo QuoteInfo { get; protected set; }
    public DbHelper.AbleUser.AbleUserInfo ContactUserInfo = null;
    public string ClientFirstName = "";
    public string ClientLastName = "";
    public string ClientEmailAddress = "";
    public bool QuoteGSTApplicable;
    public bool QuoteHasOverview, QuoteHasTeam;
    public bool QuoteHasUrlId, QuoteHasWebPageUrl, QuoteHasQwilrEmbedded, QuoteHasPDF;
    public bool ShowProposalTab;
    public bool CanViewSigningTab;
    public string SalesContentUrl;

    public class TabName {
      public const string Overview = "overview";
      public const string Proposal = "proposal";
      public const string Team = "team";
      public const string Costing = "costing";
      public const string Accept = "accept";
    }

    public class FormFields {
      public const string QuoteItemIdChecked = "QuoteItemIdChecked";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      string quoteGuidStr = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuoteGuid);
      if (!Guid.TryParse(quoteGuidStr, out Guid quoteGuid)) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return new EmptyResult();
      }

      QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(quoteGuid);
      if (QuoteInfo == null) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return new EmptyResult();
      }

      if (QuoteInfo.QuoteTitle.IsNullOrEmpty()) {
        QuoteInfo.QuoteTitle = QuoteInfo.ProjectName;
      }

      ContactUserInfo = DbHelper.AbleUser.GetUserByIdOrNull(QuoteInfo.ContactUserId, DbHelper.AbleUser.RegisteredFilter.Any); // Note these can include unregistered users.

      if (ContactUserInfo != null) {

        ClientFirstName = ContactUserInfo.FirstName;
        ClientLastName = ContactUserInfo.LastName;
        ClientEmailAddress = ContactUserInfo.EmailAddress;

        if (!ContactUserInfo.IsRegistered &&
          (ContactUserInfo.InvitedByUserId == null || ContactUserInfo.InvitedByUserId != QuoteInfo.OwnerUserId || ContactUserInfo.InviteCode.IsNullOrEmpty())) {
          // Update invite details to get invite code.
          DbHelper.AbleUser.UpdateInviteDetails(ContactUserInfo, QuoteInfo.OwnerUserId);
        }
      }

      if (QuoteInfo.IsAccepted) {
        return Page(); // stop here if already accepted.
      }

      QuoteGSTApplicable = DbHelper.XeroTaxType.GetGSTApplicableFromQuoteTaxTypeOrNull(QuoteInfo.XeroTaxType).ToBooleanOrDefault(false);
      QuoteHasOverview = QuoteInfo.CoverLetterHtml.RegexIsMatch(@">\s*[a-zA-Z]"); // Has visible content between html tags.
      QuoteHasTeam = QuoteInfo.QuoteTeamUsers != null && QuoteInfo.QuoteTeamUsers.Count > 0;

      if (QuoteInfo.QuoteSalesContentTypeId == ConfigHelper.QuoteSalesContentTypeId.UrlList) {
        var salesContentUrls = DbHelper.AbleQuotes.GetSalesContentUrls(QuoteInfo.TenantOrgId);
        var salesContentUrl = salesContentUrls.Find(sc => sc.QuoteSalesContentUrlId == QuoteInfo.QuoteSalesContentUrlId);
        if (salesContentUrl != null) SalesContentUrl = salesContentUrl.Url;
      }

      QuoteHasUrlId = QuoteInfo.QuoteSalesContentTypeId == ConfigHelper.QuoteSalesContentTypeId.UrlList && !SalesContentUrl.IsNullOrEmpty();
      QuoteHasWebPageUrl = QuoteInfo.QuoteSalesContentTypeId == ConfigHelper.QuoteSalesContentTypeId.WebPageUrl && !QuoteInfo.QuoteSalesContentWebPageUrl.IsNullOrEmpty();
      QuoteHasQwilrEmbedded = (QuoteInfo.QuoteSalesContentTypeId == null || QuoteInfo.QuoteSalesContentTypeId == ConfigHelper.QuoteSalesContentTypeId.Qwilr) && !QuoteInfo.QwilrUrl.IsNullOrEmpty();
      QuoteHasPDF = QuoteInfo.QuoteSalesContentTypeId == ConfigHelper.QuoteSalesContentTypeId.PDF;
      ShowProposalTab = QuoteHasUrlId || QuoteHasWebPageUrl || QuoteHasQwilrEmbedded || QuoteHasPDF;

      // Set CanViewSigningTab to false if they are team members.
      string quoteUserId = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.UserGuid);
      if (quoteUserId != null) {
        var userGuid = new Guid(quoteUserId);
        CanViewSigningTab = !SessionHelper.AppAccess.Quotes.CanSignQuote(QuoteInfo, userGuid);
      } else {
        CanViewSigningTab = true;
      }

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          CompleteSign(ajax);
        });
        return new EmptyResult();
      }

      return Page();
    }

    public string GetClientLinkToAbleHtml() {

      string quoteDetailsUrl = PathHelper.Pages.QuoteDetails(QuoteInfo.PublicGuid);
      string linkUrl, linkText;

      if (SessionHelper.IsUserLoggedIn || (ContactUserInfo != null && ContactUserInfo.IsRegistered)) {
        linkText = "See the quote details in Able";
        linkUrl = quoteDetailsUrl;
      } else {
        SessionHelper.SetLoginRedirectUrl(quoteDetailsUrl);
        linkText = "Click here to complete registration and see the quote details";
        linkUrl = PathHelper.Pages.RegisterInvited(ContactUserInfo.InviteCode);
      }

      return $"<a class=\"btn btn-sm btn-primary mt20\" href=\"{linkUrl}\">{linkText}</a>";
    }

    public string GetOptional(DbHelper.AbleQuotes.QuoteInfo.QuoteItemInfo quoteItem) {

      if (quoteItem.OptionalInfo.IsOptional) {
        return "<input name=\"" + FormFields.QuoteItemIdChecked + "\" type=\"checkbox\" " + (quoteItem.OptionalInfo.DefaultSelected ? "checked" : "") + " value=\"" + quoteItem.QuoteItemId + "\" class=\"icheck item-optional\" />";
      } else {
        return "<input name=\"" + FormFields.QuoteItemIdChecked + "\" type=\"hidden\" value=\"" + quoteItem.QuoteItemId + "\" />";
      }
    }

    public string GetTeamHtml() {

      var html = new StringBuilder();

      foreach (var quoteTeamUser in QuoteInfo.QuoteTeamUsers) {
        var userInfo = DbHelper.AbleUser.GetUserByIdOrNull(quoteTeamUser.UserId, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
        if (userInfo == null)
          continue;
        html.Append(GetTeamUserHtml(userInfo));
      }

      return html.ToString();
    }

    private string GetTeamUserHtml(DbHelper.AbleUser.AbleUserInfo userInfo) {

      string bioEditLink = "";

      if (SessionHelper.IsUserRoleAdmin) {
        bioEditLink = " <a title=\"Admin: Edit Bio\" target=\"_blank\" href=\""
          + PathHelper.Pages.CoachEdit(userInfo.UserId)
          + "\">" + WebHelper.Icon.Edit + "</a>";
      }

      return $@"
        <div class=""team-person-info"">
          <div class=""team-photo-sm""><img alt="""" src=""{PathHelper.Images.UserPhoto(userInfo, PathHelper.Images.UserPhotoSize.Large, true)}"" onerror=""this.className='no-img';"" /></div>
          <div class=""team-name"">{userInfo.GetFullName().HTMLEncode()}</div>
          <div class=""team-role""></div>
          <div class=""team-bio"">{userInfo.AbleBioShort.HTMLEncode() + bioEditLink}</div>
          <div class=""team-link"">{(userInfo.AbleWebProfileUrl.IsNullOrEmpty()
            ? string.Empty
            : "<a href=\"" + userInfo.AbleWebProfileUrl.HTMLEncode() + "\" target=\"_blank\">Full Profile</a>")}</div>
        </div>";
    }

    // Submitted acceptance form.

    void CompleteSign(AjaxSubmitHelper ajax) {

      if (!CanViewSigningTab) {
        ajax.AddDialogMessage("You do not have access for this action.");
        return;
      }

      var formValues = new WebHelper.QuoteSigning_FormValues();

      formValues.ClientFirstName = ajax.CheckFieldRegex("ClientFirstName", "Client First Name", AppHelper.Regex.GeneralText, true, "Required");
      formValues.ClientLastName = ajax.CheckFieldRegex("ClientLastName", "Client Last Name", AppHelper.Regex.GeneralText, true, "Required");
      formValues.ClientEmail = ajax.CheckFieldRegex("ClientEmail", "Client Email", AppHelper.Regex.Email, true, "Required");

      formValues.AccFirstName = ajax.CheckFieldRegex("AccFirstName", "Account First Name", AppHelper.Regex.GeneralText, true, "Required");
      formValues.AccLastName = ajax.CheckFieldRegex("AccLastName", "Accounts Last Name", AppHelper.Regex.GeneralText, true, "Required");
      formValues.AccEmail = ajax.CheckFieldRegex("AccEmail", "Accounts Email", AppHelper.Regex.Email, true, "Required");

      if (ajax.BadFieldCount > 0) {
        ajax.ClearBadFields();
        ajax.AddDialogMessage("Please provide all contact details.");
        return;
      }

      var agreed = ajax.CheckFieldBool("agree", "1");
      if (!agreed) {
        ajax.AddDialogMessage("Please agree to the Terms and Conditions.");
        return;
      }

      // Success.

      List<int> quoteItemIdList;

      try {

        SaveQuote(formValues, out quoteItemIdList, out decimal quoteTotalExGST);

      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CompleteSign_SaveQuote")
          .FromSession()
          .WithProperty("QuoteId", QuoteInfo.QuoteId)
          .WithProperty("QuoteGuid", QuoteInfo.PublicGuid.ToString())
          .Track();
        ajax.AddDialogMessage("Problem saving quote, please try again.", ex);
        return;
      }

      // Send quote acceptance email to Client.
      try {
        AlbertEmails.SendQuoteAcceptanceEmail(ContactUserInfo, QuoteInfo.PublicGuid);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CompleteSign_SendQuoteAcceptanceEmail")
          .FromSession()
          .AddExternalUserId(ExternalUserKind.Client, ConfigHelper.UserRole.Client.ToExternalUserId(ContactUserInfo?.UserGuid))
          .WithProperty("QuoteId", QuoteInfo.QuoteId)
          .WithProperty("QuoteGuid", QuoteInfo.PublicGuid.ToString())
          .Track();
        // ignore
      }

      var quoteSignInfo = new QuoteSignInfo(QuoteInfo.PublicGuid.ToString());
      foreach (int quoteItemId in quoteItemIdList) {
        quoteSignInfo.LineItems.Add(new LineItem(quoteItemId));
      }

      var payload = new UpdatePayload(ConfigHelper.IsLiveServer ? "quotesign" : "quotesign-test", quoteSignInfo);

      // Note the endpoint expects an array of objects.
      var postPayload = new List<UpdatePayload>();
      postPayload.Add(payload);

      bool postSuccess = false;
      string postResultJSON = "";
      Exception postException = null;

      if (ConfigHelper.IsDevServer) {
        postSuccess = true;
      } else {
        try {
          var postResult = Integrations.EventGrid.Invoice.PostPayload(ConfigHelper.EventGridEndpoint, postPayload, ConfigHelper.EventGridAccessKey_Quotes);
          postSuccess = postResult.Success;
          postResultJSON = postResult.Json;
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("CompleteSign_EventGridPost")
            .FromSession()
            .WithProperty("QuoteId", QuoteInfo.QuoteId)
            .WithProperty("QuoteGuid", QuoteInfo.PublicGuid.ToString())
            .Track();
          postException = ex;
        }
      }

      if (!postSuccess) {
        if (ConfigHelper.IsDevServer) {
          ajax.AddDialogMessage("Failed eventgrid post.<br/>" + postResultJSON);
        } else {
          EmailHelper.SendInternalSupportEmail(
            "QuoteSign - failed EventGrid post.",
            "postResultJSON = " + postResultJSON + "<br/>postException = " + (postException == null ? "null" : postException.ToString())
          );
        }
        return;
      }
    }

    void SaveQuote(WebHelper.QuoteSigning_FormValues formValues, out List<int> quoteItemIdList, out decimal quoteTotalExGST) {

      // Total amount of select items and mark each optional item as selected or not.
      // Mark all non-optional items as Accepted.
      // Mark optional items as accepted if ticked, otherwise mark not accepted.

      var itemIds = ("" + WebHelper.GetFormValue(FormFields.QuoteItemIdChecked)).ToIntList(); // Ticked item IDs as an int list.

      // Note need temp vars here as we can't work with out params inside a lambda.
      var quoteItemIdList_temp = new List<int>();
      decimal quoteTotalExGST_temp = 0;

      DbHelper.Common.UsingTransaction(trans => {

        foreach (var item in QuoteInfo.QuoteItems) {

          bool isAccepted = false;

          if (!item.OptionalInfo.IsOptional) {
            isAccepted = true; // All non-optional items are accepted.
          } else if (itemIds != null && itemIds.Contains(item.QuoteItemId)) {
            isAccepted = true; // Ticked optional items are accepted.
          }

          if (isAccepted) {
            quoteTotalExGST_temp += (decimal)item.PriceXQty(0);
            quoteItemIdList_temp.Add(item.QuoteItemId);
          }

          DbHelper.AbleQuotes.UpdateQuoteItemAccepted(trans, item.QuoteItemId, isAccepted);
        }

        DbHelper.AbleQuotes.UpdateQuoteAccepted(trans, QuoteInfo.QuoteId, quoteTotalExGST_temp, formValues);

        return true; // Commit trans.
      });

      // Assign the out params.
      quoteTotalExGST = quoteTotalExGST_temp;
      quoteItemIdList = quoteItemIdList_temp;

      // Send Intercom event for quote acceptance - associate with contact user
      if (ContactUserInfo != null && ContactUserInfo.UserGuid != Guid.Empty) {
        // Determine contact user role for ExternalUserId (default to Client if unable to determine)
        ConfigHelper.UserRole contactUserRole = ConfigHelper.UserRole.Client;
        if (ContactUserInfo.IsAbleCoach) {
          contactUserRole = ConfigHelper.UserRole.Coach;
        } else if (ContactUserInfo.IsParticipant) {
          contactUserRole = ConfigHelper.UserRole.Leader;
        } else if (ContactUserInfo.IsAbleClient) {
          contactUserRole = ConfigHelper.UserRole.Client;
        }

        var contactExternalUserId = contactUserRole.ToExternalUserId(ContactUserInfo.UserGuid);
        if (contactExternalUserId.HasValue) {
          SendEvent(
            intercom => intercom.QuoteAccepted()
              .WithUser(ContactUserInfo.UserId, contactExternalUserId.Value)
              .WithQuote(QuoteInfo.QuoteId, QuoteInfo.QuoteTitle)
              .WithClientCompany(QuoteInfo.CompanyId, QuoteInfo.CompanyName ?? "")
              .WithQuoteValue(quoteTotalExGST_temp),
            operationName: "QuotePublicView_QuoteAccepted",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["QuoteId"] = QuoteInfo.QuoteId,
              ["QuoteAcceptedAmount"] = quoteTotalExGST_temp,
              ["ContactUserId"] = ContactUserInfo.UserId
            }
          );
        }
      }
    }

    class UpdatePayload {
      public string id { get; set; }
      public string subject { get; set; }
      public string eventType { get; set; }
      public DateTime eventTime { get; set; }
      public QuoteSignInfo data { get; set; }
      public UpdatePayload(string subject, QuoteSignInfo data) {
        this.id = "1";
        this.subject = "1";
        this.eventType = subject;
        this.eventTime = DateTime.UtcNow;
        this.data = data;
      }
      public string ToJson() {
        return JsonConvert.SerializeObject(this);
      }
    }

    class QuoteSignInfo {
      public DateTime Date { get; private set; }
      public string Type { get; private set; }
      public string Reference { get; private set; }
      public List<LineItem> LineItems { get; private set; }
      public QuoteSignInfo(string reference) {
        Date = DateTime.UtcNow;
        Type = "QuoteSign";
        Reference = reference;
        LineItems = new List<LineItem>();
      }
    }

    class LineItem {
      public int QuoteItemId;
      public LineItem(int quoteItemId) {
        QuoteItemId = quoteItemId;
      }
    }

    class PostResult {
      public bool Success { get; internal set; }
      public string Json { get; internal set; }
    }

  }
}
