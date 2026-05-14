using System;
using System.Collections.Generic;
using System.Text;

namespace Integral.Web.PortalSite.UserControls {

  public partial class ChartAlbert360Detailed : System.Web.UI.UserControl {

    Guid urlCoacheeUID;
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo;
    public DbHelper.Reports.Coachee360.Coachee360Results reportResults;

    public string DetailsHtml;

    protected void Page_Load(object sender, EventArgs e) {

      if (!Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeGuid).EmptyIfNull(), out urlCoacheeUID)) return;

      coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(urlCoacheeUID);
      if (coacheeInfo == null) return;

      var sb = new StringBuilder();

      GetQuestions(sb);

      LogHelper.DebugWrite("sb.length = " + sb.Length);

      DetailsHtml = sb.ToString();
    }

    void GetQuestions(StringBuilder sb) {

      string urlSelectedSurveyUId = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId); // selected survey to show.
      reportResults = DbHelper.Reports.Coachee360.GetCoachee360ReportResults(coacheeInfo.CoacheeId, urlSelectedSurveyUId, null);

      string barsHtmlTemplate = @"
        <div class=""scoreBars"">
          <div class=""scoreBar [SelfDisplayClass]"">
            <div class=""barTitle"">Self</div>
            <div class=""barBg"">
              <span class=""barLine barSelf"" style=""width:[scoreself%]%""></span>
              <span class=""barMark""></span>
              <span class=""barMark""></span>
              <span class=""barDot dotSelf"" title=""[scoreselfbench%]"" style=""left:[scoreselfbench%]%""></span>
            </div>
            <div class=""barScore"">[scoreself100]</div>
          </div>
          <div class=""barInterGap [SelfDisplayClass]""></div>
          <div class=""scoreBar"">
            <div class=""barTitle"">Raters</div>
            <div class=""barBg"">
              <span class=""barLine barRater"" style=""width:[scorerater%]%""></span>
              <span class=""barMark""></span>
              <span class=""barMark""></span>
              <span class=""barDot dotRater"" title=""[scoreraterbench%]"" style=""left:[scoreraterbench%]%""></span>
            </div>
            <div class=""barScore"">[scorerater100]</div>
          </div>
        </div>";

      int iQn = 0;
      int? thisHeadingSort = null;

      while (iQn < reportResults.ReportQuestions.Count) {

        var qnItem = reportResults.ReportQuestions[iQn];

        sb.AppendLine("<div class=\"boxBorder\">");
        sb.AppendLine("<div class=\"boxTitle\"><h4>" + qnItem.RptQnGrpHeading.HTMLEncode() + "</h4></div>");

        thisHeadingSort = qnItem.RptQnGrpHeadingSort;
        while (iQn < reportResults.ReportQuestions.Count) {
          qnItem = reportResults.ReportQuestions[iQn];
          if (thisHeadingSort != qnItem.RptQnGrpHeadingSort) break;

          string qnText = reportResults.SurveyInfo.IsRatersOnly ? qnItem.QuestionTextForRater : qnItem.QuestionText;
          qnText = qnText.ReplaceTags(new Dictionary<string, string> {
            { "SelfName", coacheeInfo.FirstName }
          });

          sb.AppendLine("<div class=\"question row\">");
          sb.AppendLine("<div class=\"qnText col-md-9\">"
            + "<span class=\"qnNum\">" + qnItem.AutoNumber + ".</span>"
            + qnText.HTMLEncode() + "</div>");
          sb.AppendLine("<div class=\"qnBars col-md-3\">");

          // Merge in score bar values.
          string barsHtml = barsHtmlTemplate;
          if (reportResults.SurveyInfo.IsRatersOnly) {
            barsHtml = barsHtml.ReplaceTags("[", "]", new Dictionary<string, string>() {
              { "SelfDisplayClass", "displaynone" } // This is raters-only, so hide the self bar.
            });
          } else {
            // Merge in self bar values.
            barsHtml = barsHtml.ReplaceTags("[", "]", new Dictionary<string, string>() {
              { "SelfDisplayClass", "" },
              { "scoreself%", GetFormattedScore(qnItem.Scores.ScoreSelf.Avg) },
              { "scoreselfbench%", GetFormattedScore(qnItem.Scores.ScoreBenchSelf.Avg) },
              { "scoreself100", GetFormattedScore(qnItem.Scores.ScoreSelf.Avg) },
            });
          }
          // Merge in rater bar values.
          barsHtml = barsHtml.ReplaceTags("[", "]", new Dictionary<string, string>() {
              { "scorerater%", GetFormattedScore(qnItem.Scores.ScoreRater.Avg) },
              { "scoreraterbench%", GetFormattedScore(qnItem.Scores.ScoreBenchRater.Avg) },
              { "scorerater100", GetFormattedScore(qnItem.Scores.ScoreRater.Avg) }
            });
          sb.AppendLine(barsHtml);
          sb.AppendLine("</div>");
          sb.AppendLine("</div>");

          iQn++;
        }

        sb.AppendLine("</div>");
      }
    }

    string GetFormattedScore(double? score) {
      if (score == null) return "";
      return Math.Round((double)score * 10, 0, MidpointRounding.AwayFromZero).ToString();
    }

  }
}
