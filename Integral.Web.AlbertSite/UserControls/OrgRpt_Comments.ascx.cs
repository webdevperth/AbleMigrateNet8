using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.UserControls {

  public partial class OrgRpt_Comments : OrgReportControlBase {

    protected void Page_Load(object sender, EventArgs e) {

      if (reportParticipantInfo == null) {
        WebHelper.EndRequest();
        return;
      }

      if (WebHelper.GetQueryStringValue("tabmode") != null) {

        string mode = WebHelper.GetQueryStringValue("tabmode");
        int questionNumber = 0;

        if (!int.TryParse(WebHelper.GetQueryStringValue("qn"), out questionNumber)) return; // || questionNumber <= 0) return;

        if (mode == "themes") {
          GetThemes(questionNumber);
        } else if (mode == "responses") {
          GetResponses(questionNumber);
        }
        WebHelper.EndRequest();
        return;
      }

    }

    public void GetThemes(int questionNumber) {

      // Get qn info for this qn number.
      var qnInfo = reportData.OpenTextQuestions.Find(x => x.QuestionNum == questionNumber);
      if (qnInfo == null) return;

      int themeCount = 0;
      int maxResponses = 0;
      Response.Write("<table class='themeTable' width='100%'>");
      foreach (var theme in DbHelper.OrgReportsCached.GetOpenTextThemes(reportData, qnInfo.AnswerTypeId)) {
        themeCount++;
        if (themeCount == 1) {
          maxResponses = theme.ResponseCount;
          LogHelper.DebugWrite("maxResponses = " + maxResponses);
        }
        int barWidthPerc = (int)((double)theme.ResponseCount / maxResponses * 100);
        if (barWidthPerc == 0) barWidthPerc = 1;
        LogHelper.DebugWrite("barWidthPerc = " + theme.ResponseCount + " / " + maxResponses + " * 100 = " + barWidthPerc);

        Response.Write(""
          + "<tr>"
          + "<td class='colText'>" + theme.ThemeText.HTMLEncode() + "</td>"
          + "<td class='colCount'>" + theme.ResponseCount + "</td>"
          + "<td class='colBar'><div class='bar' style='width:" + barWidthPerc + "%;'>&nbsp;</div></td>"
          + "</tr>");
      }
      Response.Write("</table>");
    }

    public void GetResponses(int questionNumber) {

      if (!reportParticipantInfo.CanAccessTextResponses) return;

      // Get qn info for this qn number.
      var qnInfo = reportData.OpenTextQuestions.Find(x => x.QuestionNum == questionNumber);
      if (qnInfo == null) return;

      // Get responses for this question.
      foreach (string txt in DbHelper.OrgReportsCached.GetOpenTextResponses(reportData, qnInfo.AnswerTypeId))
        Response.Write("<p>" + txt.HTMLEncode() + "</p>\n");

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

