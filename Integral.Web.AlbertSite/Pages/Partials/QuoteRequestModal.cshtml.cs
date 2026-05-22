using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class QuoteRequestModal : AppCode.PageBaseClasses.LoggedInPageModel {

    public string GeneralInfoHtml;

    public class FormFields {
      public const string RequestText = "RequestText";
    }

    public class AjaxAction {
      public const string SendQuoteRequest = "SendQuoteRequest";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Partner Details";

      // Is there a job id or "new" in the querystring?
      string projectJobNumber = ("" + WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber)).RegexMatchStringOrNull("^[a-z0-9_-]+$", RegexOptions.IgnoreCase);

      if (!projectJobNumber.IsNullOrEmptyOrWhitespace()) {

        ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(projectJobNumber);
      }

      GeneralInfoHtml = GetGeneralInfoHtml();

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {
          if (PageAjaxAction == AjaxAction.SendQuoteRequest) {
            SendQuoteRequest(ajax);
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    public string GetGeneralInfoHtml() {

      string html = "";

      if (ProjectInfo != null) {

        string projectName = (ProjectInfo.FriendlyProjectTitle.IsNullOrEmptyOrWhitespace() ? ProjectInfo.ProjectName : ProjectInfo.FriendlyProjectTitle);

        html += GetRowInfoHtml("Project Name:", $"{ProjectInfo.JobNumber} - {projectName}");
        html += GetRowInfoHtml("Company Name:", ProjectInfo.ClientCompanyName);

      }

      return html;
    }

    private string GetRowInfoHtml(string label, string text) {
      return WebHelper.GetTextDisplayRow(label, 9, text);
    }

    public void SendQuoteRequest(AjaxSubmitHelper ajax) {

      string requestText = ajax.CheckFieldPlainText(FormFields.RequestText, "Request Text", true, "Please specify your request for quote.");

      if (requestText.IsNullOrEmptyOrWhitespace()) return;

      // If request doesn't come from a project, get the company id from the user info, otherwise get it from the project info.
      int? companyId = ProjectInfo == null ? userInfo.ClientCompanyId : ProjectInfo.CompanyId;

      var clientCompany = new DbHelper.ClientCompanies.AlbertCompanyInfo();
      if (companyId != null) {
        clientCompany = DbHelper.ClientCompanies.GetCompanyInfoOrNull(companyId.Value, SessionHelper.GetUserInfoOrNull());
      }

      // If client company wasn't set, send null. It will be processed on the other end.
      bool sentEmail = AlbertEmails.SendQuoteRequestEmail(userInfo, companyId != null ? clientCompany : null, ProjectInfo, requestText);

      if (sentEmail) {

        string pageRedirect = ProjectInfo == null ? PathHelper.Pages.QuoteList() : PathHelper.Pages.ProjectQuotes(ProjectInfo.JobNumber);

        ajax.SetRedirectUrl(pageRedirect, "Your request for quote has been sent.");

      } else {

        ajax.AddDialogMessage("There was an error sending your request for quote. Please try again.");
      }
    }


  }
}
