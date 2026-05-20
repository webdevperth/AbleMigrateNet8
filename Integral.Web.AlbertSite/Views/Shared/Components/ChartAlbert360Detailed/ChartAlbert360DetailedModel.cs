using System;
using System.Collections.Generic;
using System.Text;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the ChartAlbert360Detailed ViewComponent. Mirrors the public API of
  // the legacy UserControls/ChartAlbert360Detailed.ascx.cs codebehind so the
  // .cshtml view can call GetQuestions() exactly as the original <%= DetailsHtml %>
  // did.
  public class ChartAlbert360DetailedModel {

    public DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo;
    public DbHelper.Reports.Coachee360.Coachee360Results reportResults;

    public string GetQuestions() {

      var sb = new StringBuilder();

      // String-template HTML kept verbatim from the legacy codebehind. Refactoring
      // into Razor markup has wider styling implications and is out of scope.
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

          string barsHtml = barsHtmlTemplate;
          if (reportResults.SurveyInfo.IsRatersOnly) {
            barsHtml = barsHtml.ReplaceTags("[", "]", new Dictionary<string, string>() {
              { "SelfDisplayClass", "displaynone" }
            });
          } else {
            barsHtml = barsHtml.ReplaceTags("[", "]", new Dictionary<string, string>() {
              { "SelfDisplayClass", "" },
              { "scoreself%", GetFormattedScore(qnItem.Scores.ScoreSelf.Avg) },
              { "scoreselfbench%", GetFormattedScore(qnItem.Scores.ScoreBenchSelf.Avg) },
              { "scoreself100", GetFormattedScore(qnItem.Scores.ScoreSelf.Avg) },
            });
          }
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

      return sb.ToString();
    }

    public string GetFormattedScore(double? score) {
      if (score == null) return "";
      return Math.Round((double)score * 10, 0, MidpointRounding.AwayFromZero).ToString();
    }

  }
}
