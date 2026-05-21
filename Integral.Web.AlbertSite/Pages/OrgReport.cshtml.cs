using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class OrgReport : AppCode.PageBaseClasses.LoggedInPageModel {

    string urlSurveyUId, urlPartUId;
    string urlOption;
    public DbHelper.OrgReportsCached.ReportData reportData;
    public DbHelper.OrgReportsCached.ReportParticipantInfo reportParticipantInfo;
    public DbHelper.OrgSurveys.SurveyInfo surveyInfo;
    public int NoContentStatusCode;
    public bool ReportIsAvailable = false;
    public bool InvalidIdsVisible = false;
    public string RequestedCtrlName;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      NoContentStatusCode = WebHelper.GetHttpStatusCode(WebHelper.HttpStatusEnum.NoContent);

      if (PageAjaxAction == "getdata") {
        WebHelper.EndRequest();
        return new EmptyResult();
      }

      // Check if requesting usercontrol content.
      RequestedCtrlName = WebHelper.GetQueryStringValue("ctrlname").EmptyIfNull().ToLower();
      if (!RequestedCtrlName.IsNullOrEmpty()) return Page();

      // TODO: On the first load of this page (i.e. not loading a usercontrol),
      // check if there are completed participants.

      OrgReports.GetExternalIOSUIDsFromQuery(out urlSurveyUId, out urlPartUId);

      if (!SessionHelper.AppAccess.Reports.CanViewExternalIOSReport(urlSurveyUId)) {
        RespondNotAvailable();
        return Page();
      }

      urlOption = WebHelper.GetQueryStringValue("option").EmptyIfNull();
      surveyInfo = DbHelper.OrgSurveys.GetSurveyInfo(urlSurveyUId);

      if (OrgReports.IsReloadRequested()) DbHelper.OrgReportsCached.ReportData_ClearCache(urlSurveyUId);

      reportParticipantInfo = DbHelper.OrgReportsCached.GetReportParticipantInfo(urlSurveyUId, urlPartUId);
      reportData = OrgReports.GetReportData(urlSurveyUId, urlPartUId);
      if (reportParticipantInfo == null || reportData == null) {
        RespondNotAvailable();
        return Page();
      }

      ReportIsAvailable = true;

      return Page();
    } // load

    void RespondNotAvailable() {
      InvalidIdsVisible = true;
      ReportIsAvailable = false;
    }
  }
}
