using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  // This is page should be arrived at by a form POST from the quote client view page.
  // Form includes the quote item IDs that the user is signing off on (not including un-ticked ones)

  public class QuoteQwilrSignOff : PageModel {

    public DbHelper.AbleQuotes.QuoteInfo QuoteInfo { get; protected set; }
    public DbHelper.AbleUser.AbleUserInfo ContactUserInfo = null;
    public string ClientFirstName = "";
    public string ClientLastName = "";
    public string ClientEmailAddress = "";
    public decimal TotalAmount;
    public string QuoteItemIds;
    public List<int> QuoteItemIdList;

    public bool FormVisible = false;
    public bool AcceptedVisible = false;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      string quoteGuidStr = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuoteGuid);
      Guid quoteGuid;

      if (!Guid.TryParse(quoteGuidStr, out quoteGuid)) {
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

      ContactUserInfo = DbHelper.AbleUser.GetUserByIdOrNull(QuoteInfo.ContactUserId, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      if (ContactUserInfo != null) {
        ClientFirstName = ContactUserInfo.FirstName;
        ClientLastName = ContactUserInfo.LastName;
        ClientEmailAddress = ContactUserInfo.EmailAddress;
      }

      // If already accepted, stop here.
      if (QuoteInfo.ClientAcceptedUtc != null) {
        AcceptedVisible = true;
        return Page();
      }

      // Not yet accepted, show form and continue.
      FormVisible = true;

      // Total amount of select items.
      TotalAmount = 0;
      QuoteItemIds = "";
      QuoteItemIdList = new List<int>();

      var itemIds = ("" + WebHelper.GetFormValue("quoteitemid")).ToIntList();

      foreach (var item in QuoteInfo.QuoteItems) {

        bool isAccepted = false;

        if (itemIds != null && itemIds.Contains(item.QuoteItemId)) {
          isAccepted = true;
          TotalAmount += (decimal)item.PriceXQty(0);
          if (QuoteItemIds.Length > 0) QuoteItemIds += ",";
          QuoteItemIds += item.QuoteItemId.ToString();
          QuoteItemIdList.Add(item.QuoteItemId);
        }

        DbHelper.Common.GetNonQueryInt("UPDATE al_QuoteItem SET IsAccepted = @IsAccepted WHERE QuoteItemId = @QuoteItemId",
          DbHelper.Common.NewSqlParameter("IsAccepted", isAccepted ? 1 : 0),
          DbHelper.Common.NewSqlParameter("QuoteItemId", item.QuoteItemId));
      }

      if (SystemWeb.IsAjaxPost) {
        AjaxSubmitHelper.Process(ajax => {
          if (ajax.Action == "complete") {
            CompleteSign(ajax);
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    class FormValues {
      public string ClientFirstName;
      public string ClientLastName;
      public string ClientEmail;
      public string AccFirstName;
      public string AccLastName;
      public string AccEmail;
    }

    void CompleteSign(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();

      formValues.ClientFirstName = ajax.CheckFieldRegex("ClientFirstName", "Client First Name", AppHelper.Regex.GeneralText, true, "Required");
      formValues.ClientLastName = ajax.CheckFieldRegex("ClientLastName", "Client Last Name", AppHelper.Regex.GeneralText, true, "Required");
      formValues.ClientEmail = ajax.CheckFieldRegex("ClientEmail", "Client Email", AppHelper.Regex.GeneralText, true, "Required");

      formValues.AccFirstName = ajax.CheckFieldRegex("AccFirstName", "Account First Name", AppHelper.Regex.GeneralText, true, "Required");
      formValues.AccLastName = ajax.CheckFieldRegex("AccLastName", "Accounts Last Name", AppHelper.Regex.GeneralText, true, "Required");
      formValues.AccEmail = ajax.CheckFieldRegex("AccEmail", "Accounts Email", AppHelper.Regex.GeneralText, true, "Required");

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

      DbHelper.Common.GetNonQueryInt(@"
        UPDATE al_Quote SET
          ClientAcceptedUtc = GETUTCDATE(),
          ClientAcceptedAmount = @TotalAmount,
          ClientFirstName = @ClientFirstName,
          ClientLastName = @ClientLastName,
          ClientEmailAddress = @ClientEmailAddress,
          AccPayFirstName = @AccPayFirstName,
          AccPayLastName = @AccPayLastName,
          AccPayEmailAddress = @AccPayEmailAddress,
          QuoteStatusId = @QuoteStatusId
        WHERE QuoteId = @QuoteId",

        DbHelper.Common.NewSqlParameter("QuoteId", QuoteInfo.QuoteId),

        DbHelper.Common.NewSqlParameter("TotalAmount", TotalAmount),
        DbHelper.Common.NewSqlParameter("ClientFirstName", formValues.ClientFirstName.LimitLengthTo(50)),
        DbHelper.Common.NewSqlParameter("ClientLastName", formValues.ClientLastName.LimitLengthTo(50)),
        DbHelper.Common.NewSqlParameter("ClientEmailAddress", formValues.ClientEmail.LimitLengthTo(100)),
        DbHelper.Common.NewSqlParameter("AccPayFirstName", formValues.AccFirstName.LimitLengthTo(50)),
        DbHelper.Common.NewSqlParameter("AccPayLastName", formValues.AccLastName.LimitLengthTo(50)),
        DbHelper.Common.NewSqlParameter("AccPayEmailAddress", formValues.AccEmail.LimitLengthTo(100)),
        DbHelper.Common.NewSqlParameter("QuoteStatusId", DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusId)
      );

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
              .WithQuoteValue(TotalAmount),
            operationName: "QuoteQwilrSignOff_QuoteAccepted",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["QuoteId"] = QuoteInfo.QuoteId,
              ["QuoteAcceptedAmount"] = TotalAmount,
              ["ContactUserId"] = ContactUserInfo.UserId
            }
          );
        }
      }

      var quoteSignInfo = new QuoteSignInfo(QuoteInfo.PublicGuid.ToString());
      foreach (int quoteItemId in QuoteItemIdList) {
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
            .WithProperty(ApplicationInsightsConstants.QuoteId, QuoteInfo.QuoteId)
            .WithProperty(ApplicationInsightsConstants.QuoteGuid, QuoteInfo.PublicGuid.ToString())
            .WithProperty("TotalAmount", TotalAmount)
            .Track();
          postException = ex;
        }
      }

      if (!postSuccess) {
        if (ConfigHelper.IsDevServer) {
          ajax.AddDialogMessage("Failed to complete, please try again later." + postResultJSON);
        } else {
          EmailHelper.SendInternalSupportEmail(
            "QuoteQwilrSignOff - failed EventGrid post.",
            "postResultJSON = " + postResultJSON + "<br/>postException = " + (postException == null ? "null" : postException.ToString())
          );
        }
        return;
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
