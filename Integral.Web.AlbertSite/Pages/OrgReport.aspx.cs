using System;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class OrgReport : AppCode.PageBaseClasses.LoggedInPageBase {

    string urlSurveyUId, urlPartUId;
    string urlOption;
    public DbHelper.OrgReportsCached.ReportData reportData;
    public DbHelper.OrgReportsCached.ReportParticipantInfo reportParticipantInfo;
    public DbHelper.OrgSurveys.SurveyInfo surveyInfo;
    public int NoContentStatusCode;
    public bool ReportIsAvailable = false;
    public bool InvalidIdsVisible = false;
    public string RequestedCtrlName;

    protected void Page_Load(object sender, EventArgs e) {

      NoContentStatusCode = WebHelper.GetHttpStatusCode(WebHelper.HttpStatusEnum.NoContent);

      if (PageAjaxAction == "getdata") {
        WebHelper.EndRequest();
      }

      // Check if requesting usercontrol content.
      RequestedCtrlName = WebHelper.GetQueryStringValue("ctrlname").EmptyIfNull().ToLower();
      if (!RequestedCtrlName.IsNullOrEmpty()) return;

      // TODO: On the first load of this page (i.e. not loading a usercontrol),
      // check if there are completed participants.

      OrgReports.GetExternalIOSUIDsFromQuery(out urlSurveyUId, out urlPartUId);

      if (!SessionHelper.AppAccess.Reports.CanViewExternalIOSReport(urlSurveyUId)) {
        RespondNotAvailable();
        return;
      }

      urlOption = WebHelper.GetQueryStringValue("option").EmptyIfNull();
      surveyInfo = DbHelper.OrgSurveys.GetSurveyInfo(urlSurveyUId);

      if (OrgReports.IsReloadRequested()) DbHelper.OrgReportsCached.ReportData_ClearCache(urlSurveyUId);

      reportParticipantInfo = DbHelper.OrgReportsCached.GetReportParticipantInfo(urlSurveyUId, urlPartUId);
      reportData = OrgReports.GetReportData(urlSurveyUId, urlPartUId);
      if (reportParticipantInfo == null || reportData == null) {
        RespondNotAvailable();
        return;
      }

      ReportIsAvailable = true;

    } // load

    void RespondNotAvailable() {
      InvalidIdsVisible = true;
      ReportIsAvailable = false;
    }
  }
}
