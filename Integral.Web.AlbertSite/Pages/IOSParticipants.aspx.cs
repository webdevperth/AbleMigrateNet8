using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Net.Mail;
using System.Text;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class IOSParticipants : System.Web.UI.Page {

    // Note: At the moment, for CoJ, this relies on the user's "AccessOnlySurveyId" value
    //       to be set to the survey they are managing.
    //       In future, they should be able to pick from any survey belonging to their company.

    public const string divCodeQueryParam = "dc";
    public DbHelper.Participants.ParticipantInfo partListItem;
    public DbHelper.OrgSurveys.SurveyInfo surveyInfo;

    protected void Page_Load(object sender, EventArgs e) {

      var userInfo = SessionHelper.GetUserInfoOrNull();
      if (userInfo.AccessOnlySurveyId == null) {
        SessionHelper.LogOut();
        WebHelper.Redirect(PathHelper.Pages.Home());
        return;
      }

      int surveyId = (int)userInfo.AccessOnlySurveyId;
      surveyInfo = DbHelper.OrgSurveys.GetSurveyInfo(surveyId);

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (ajax.Action == "update") {
            DoUpdate(ajax, surveyId);
          } else {
            string urlDivCodeId = WebHelper.GetFormValue(divCodeQueryParam);
            if (urlDivCodeId != null) WritePartList(surveyId, urlDivCodeId);
          }
        });
        WebHelper.EndRequest();
        return;
      }

    }

    public string GetOrgLevelOptions() {

      // Note: This is only for a "flat" list of divisions, without "directorates".
      // For now, directorate structures are done manually in the HTML for those specific surveys.

      var html = new StringBuilder();

      string sql = @"
        SELECT dc.CodeId, dc.Value
          FROM sv_360_Codes dc
          INNER JOIN sv_360_AnswerTypes dt ON dc.AnswerTypeId = dt.AnswerTypeId
        WHERE SurveyId = @SurveyId
        AND dt.AnswerTypeDescr = 'division'
        ORDER BY IIF(dc.RptOrder1 > 0, dc.RptOrder1, dc.Code)
        ";

      using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
        using (var cmd = new SqlCommand(sql, conn)) {
          cmd.Parameters.AddInt("@SurveyId", surveyInfo.SurveyId);
          conn.Open();
          using (SqlDataReader dr = cmd.ExecuteReader()) {
            while (dr.Read()) {
              string divisionName = dr.GetString("Value");
              int divisionCodeId = dr.GetInt("CodeId");
              html.Append("<option value=\"" + divisionCodeId + "\">" + divisionName.HTMLEncode() + "</option>");
            }
          }
        }
      }
      return html.ToString();
    }

    void WritePartList(int surveyId, string urlDivCodeId) {

      int divCodeId = 0;
      if (!int.TryParse(urlDivCodeId, out divCodeId)) return;
      var partList = DbHelper.Participants.GetOrgParticipantList(surveyId, divCodeId);

      if (partList == null) return;

      // Sort by email address.
      var dicSorted = new SortedDictionary<string, DbHelper.Participants.ParticipantInfo>();
      foreach (var partInfo in partList.Participants) {
        dicSorted.Add(partInfo.Email, partInfo);
      }

      foreach (var item in dicSorted) {

        var partInfo = item.Value;

        DateTime? reportSent = SessionHelper.UtcToUserTime(partInfo.ReportSentUtc);

        SystemWeb.ResponseWriteLine($@"
        <tr class=""rowPart"" height=""37"">
          <td class=""type-email"">{partInfo.Email.HTMLEncode()}</td>
          <td class=""colAccess""><input type=""checkbox"" {(partInfo.IsSelfManager ? "checked" : "")} name=""accessPartUIDs"" value=""{partInfo.PartUID}"" /></td>
          <td class=""colSend""><input type=""checkbox"" name=""sendPartUIDs"" value=""{partInfo.PartUID}"" /></td>
          <td class=""colLink""><a target=""_blank"" href=""{PathHelper.Reports.OldIOSReport(partInfo.SurveyUID, partInfo.PartUID, true)}"">view report</a></td>
          <td class=""colSent"">{WebHelper.DisplayDate(reportSent, "&nbsp;")}</td>
        </tr>");
      }
    }

    void DoUpdate(AjaxSubmitHelper ajax, int surveyId) {

      int divCodeId = WebHelper.GetFormValueIntOrDefault("divCodeId", 0);
      string accessPartUIds = WebHelper.GetFormUIDList("accessPartUIDs");
      string sendPartUIds = WebHelper.GetFormUIDList("sendPartUIDs");
      int sendCount = 0;

      if (accessPartUIds == null || sendPartUIds == null || divCodeId == 0) {
        ajax.AddDialogMessage("Unable to update.<br/>Please reload page and try again.");
        return;
      }

      if (!accessPartUIds.IsNullOrEmpty()) SetAccess(ajax, divCodeId, surveyId, accessPartUIds);
      if (ajax.BadFieldCount > 0) return;

      if (!sendPartUIds.IsNullOrEmpty()) {
        sendCount = SendLinks(ajax, divCodeId, surveyId, sendPartUIds);
        if (ajax.BadFieldCount > 0) return;
      }

      ajax.AddDialogMessage("Participants Updated.<br/><br/>" + (sendCount == 0 ? "No" : sendCount.ToString()) + " email" + (sendCount > 1 ? "s" : "") + " sent.");

    }

    void SetAccess(AjaxSubmitHelper ajax, int divCodeId, int surveyId, string accessPartUIds) {
      // Set access.

      using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
        using (var cmd = new SqlCommand(@"

        UPDATE sv_360_Participants
        SET IsSelfMgr = 0
        WHERE SurveyId = @SurveyId
          AND OrgDivisionCodeId = @OrgDivisionCodeId;

        UPDATE sv_360_Participants
        SET IsSelfMgr = 1
        WHERE SurveyId = @SurveyId
          AND OrgDivisionCodeId = @OrgDivisionCodeId
          AND UniqueId IN ('" + accessPartUIds.Replace(",", "','") + @"')

        ", conn)) {
          cmd.Parameters.AddInt("@SurveyId", surveyId);
          cmd.Parameters.AddInt("@OrgDivisionCodeId", divCodeId);
          conn.Open();
          cmd.ExecuteNonQuery();
        }
      }

    }

    int SendLinks(AjaxSubmitHelper ajax, int divCodeId, int surveyId, string sendPartUIds) {

      // Send emails with links.
      // Returns number of emails sent.
      // TODO: move SQL to function in DbHelper.OrgReports

      int sendCount = 0;
      MailAddress replyTo;

      using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
        using (var cmd = new SqlCommand(@"

        SELECT s.sv_uniqueid, p.PartId, p.Name, p.Email, p.UniqueId
        FROM sv_360_Participants p
        INNER JOIN sv_Survey s ON s.sv_id = p.SurveyId
        WHERE p.SurveyId = @SurveyId
          AND p.OrgDivisionCodeId = @OrgDivisionCodeId
          AND UniqueId IN ('" + sendPartUIds.Replace(",", "','") + @"')

        ", conn)) {
          cmd.Parameters.AddInt("@SurveyId", surveyId);
          cmd.Parameters.AddInt("@OrgDivisionCodeId", divCodeId);
          conn.Open();
          using (SqlDataReader dr = cmd.ExecuteReader()) {
            while (dr.Read()) {

              string surveyUId = dr.GetString("sv_uniqueid");
              string partUId = dr.GetString("UniqueId");
              int partId = dr.GetInt("PartId");
              string partEmail = dr.GetString("Email");
              string partName = "EOS Participant";

              if (ConfigHelper.IsStagingServer) {
                partName = partEmail; // To show who it is for
                partEmail = "andrew@integral.global"; // hack for testing
              }

              string bodyHtml;

              if (surveyInfo.SurveyId == 3028) {

                bodyHtml = $@"
                  <h1>Access to the City of Joondalup Employee Opinion Survey Online Dashboard</h1>
                  <p>In November 2018 CoJ staff completed Integral’s Employee Opinion Survey.
                  Below is your personal link to access the results so that you might contribute
                  to the City’s response to employee feedback.</p>
                  <p><a target=""_blank"" href=""{PathHelper.Reports.OldIOSReport(surveyUId, partUId, true)}"">View the EOS Online Dashboard</a></p>
                  <p>If you have any questions about the dashboard, please contact me via email or phone.</p>
                  <p>
                    <b>Glenn Heaperman</b><br/>
                    <b>Manager Human Resources</b><br/>
                    <b>City of Joondalup</b><br/>
                    Tel: 08 9400 4326<br/>
                    Mobile: 0424 289 859<br/>
                    Email: <a href=""mailto:Glenn Heaperman <glenn.heaperman@joondalup.wa.gov.au>?subject=Re: City of Joondalup Employee Opinion Survey"">glenn.heaperman@joondalup.wa.gov.au</a><br/>
                  </p>";
                replyTo = new MailAddress("glenn.heaperman@joondalup.wa.gov.au", "Glenn Heaperman");

              } else {

                bodyHtml = $@"
                  <h1>Access to the Employee Opinion Survey Online Dashboard</h1>
                  <p>Recently your organisation completed Integral’s Employee Opinion Survey.
                  Below is your personal link to access the results, so that you might contribute
                  to your organisation's response to employee feedback.</p>
                  <p><a target=""_blank"" href=""{PathHelper.Reports.OldIOSReport(surveyUId, partUId, true)}"">View the EOS Online Dashboard</a></p>
                  <p>If you have any questions about the dashboard, please contact me via email or phone.</p>
                  <p>
                    <b>Integral Development</b><br/>
                    Tel: 08 9400 4326<br/>
                    Email: <a href=""mailto:cj@integral.global>?subject=Re: Employee Opinion Survey"">cj@integral.global</a>
                  </p>";
                replyTo = new MailAddress("cj@integral.global", "Integral Admin");
              }

              var emailOk = AlbertEmails.SendGenericEmail(
                null,
                "Integral's Employee Opinion Survey", "", bodyHtml,
                false,
                "Integral Development", ConfigHelper.Email_Coordinator_Address,
                null, out _,
                new MailAddress(partEmail, partName));

              if (emailOk) {
                sendCount++;
                DbHelper.Participants.UpdateReportSent(partId);
              }

            }
          }
        }
      }

      return sendCount;

    }

  }
}
