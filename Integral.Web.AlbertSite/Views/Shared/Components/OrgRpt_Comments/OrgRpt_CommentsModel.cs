using System.Collections.Generic;
using System.Text;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the OrgRpt_Comments ViewComponent. Mirrors the public API of the
  // legacy UserControls/OrgRpt_Comments.ascx.cs codebehind so the .cshtml view
  // can call GetQuestions exactly as the original <%= GetQuestions(...) %> did.
  //
  // GetThemes/GetResponses originally Response.Write'd their output for the AJAX
  // sub-request path; they're returned as strings here so the ViewComponent can
  // emit them via Content(...). HTML structure is byte-for-byte identical.
  public class OrgRpt_CommentsModel {

    public DbHelper.OrgReportsCached.ReportData reportData;
    public DbHelper.OrgReportsCached.ReportParticipantInfo reportParticipantInfo;

    public string GetThemes(int questionNumber) {

      var sb = new StringBuilder();

      // Get qn info for this qn number.
      var qnInfo = reportData.OpenTextQuestions.Find(x => x.QuestionNum == questionNumber);
      if (qnInfo == null) return "";

      int themeCount = 0;
      int maxResponses = 0;
      sb.Append("<table class='themeTable' width='100%'>");
      foreach (var theme in DbHelper.OrgReportsCached.GetOpenTextThemes(reportData, qnInfo.AnswerTypeId)) {
        themeCount++;
        if (themeCount == 1) {
          maxResponses = theme.ResponseCount;
          LogHelper.DebugWrite("maxResponses = " + maxResponses);
        }
        int barWidthPerc = (int)((double)theme.ResponseCount / maxResponses * 100);
        if (barWidthPerc == 0) barWidthPerc = 1;
        LogHelper.DebugWrite("barWidthPerc = " + theme.ResponseCount + " / " + maxResponses + " * 100 = " + barWidthPerc);

        sb.Append(""
          + "<tr>"
          + "<td class='colText'>" + theme.ThemeText.HTMLEncode() + "</td>"
          + "<td class='colCount'>" + theme.ResponseCount + "</td>"
          + "<td class='colBar'><div class='bar' style='width:" + barWidthPerc + "%;'>&nbsp;</div></td>"
          + "</tr>");
      }
      sb.Append("</table>");
      return sb.ToString();
    }

    public string GetResponses(int questionNumber) {

      if (!reportParticipantInfo.CanAccessTextResponses) return "";

      var sb = new StringBuilder();

      // Get qn info for this qn number.
      var qnInfo = reportData.OpenTextQuestions.Find(x => x.QuestionNum == questionNumber);
      if (qnInfo == null) return "";

      // Get responses for this question.
      foreach (string txt in DbHelper.OrgReportsCached.GetOpenTextResponses(reportData, qnInfo.AnswerTypeId))
        sb.Append("<p>" + txt.HTMLEncode() + "</p>\n");

      return sb.ToString();
    }

    public string GetQuestions(string qnHTMLTemplate) {

      string html = "";
      int qnCount = 0;
      foreach (var qnInfo in reportData.OpenTextQuestions) {
        qnCount++;
        html += qnHTMLTemplate.ReplaceTags(
          new Dictionary<string, string>() {
            { "active", qnCount == 1 ? "active" : "" },
            { "qnNum", qnInfo.QuestionNum.ToString() },
            { "qnText", qnInfo.QuestionText }
          }
        );
      }
      return html;
    }

  }
}
