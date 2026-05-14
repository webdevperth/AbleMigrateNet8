using System;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.AppCode {

  public class OrgReportControlBase : System.Web.UI.UserControl {

    protected DbHelper.OrgReportsCached.ReportData reportData;
    protected DbHelper.OrgReportsCached.ReportParticipantInfo reportParticipantInfo;

    protected bool ignorePartCount = false; // In derived class, set this to true to ignore the partCount check below.

    protected void Page_Init(object sender, EventArgs e) {

      LoadReportData();
    }

    public void LoadReportData() {

      // Try to get survey UID from "external" Org report url, then by "internal" (Client logged in) URL.
      // They both use different query keys, and need to retain backward compatibility with the external links.
      OrgReports.GetExternalIOSUIDsFromQuery(out string surveyUID, out string partUID);
      if (surveyUID.IsNullOrEmpty()) {
        surveyUID = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId);
      }

      // URL flag for testing to clear cached report results and get the latest data.
      bool clearCachedResults = !WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.IOSClearCachedResults).IsNullOrEmpty();

      reportData = OrgReports.GetReportData(surveyUID, partUID, clearCachedResults);

      if (reportData == null) {
        WebHelper.EndRequest();
        return;
      }

      reportParticipantInfo = OrgReports.GetReportParticipantInfo(surveyUID, partUID);

      if (reportParticipantInfo == null) {
        WebHelper.EndRequest();
        return;
      }

      if (!ignorePartCount && reportData.ParticipantCount < reportData.SurveyInfo.OrgReportMinResponsesToShow) {
        // Return a custom code to indicate not enough data and exit.
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return;
      }

    }

  }

}
