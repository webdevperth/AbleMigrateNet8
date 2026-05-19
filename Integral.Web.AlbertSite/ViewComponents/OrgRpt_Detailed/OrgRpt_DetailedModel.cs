using System.Text;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the OrgRpt_Detailed ViewComponent. Mirrors the public API of the
  // legacy UserControls/OrgRpt_Detailed.ascx.cs codebehind so the .cshtml view
  // can call GetQuestions() exactly as the original <%= GetQuestions() %> did.
  public class OrgRpt_DetailedModel {

    public DbHelper.OrgReportsCached.ReportData reportData;

    public string GetQuestions() {

      var qnData = reportData.LikertQuestions;
      var sb = new StringBuilder();
      int iQn = 0;
      int thisHeadingSort;

      while (iQn < qnData.Count) {

        var qnItem = qnData[iQn];

        sb.AppendLine("<div class=\"heading\"><h5>" + qnItem.ReportSectionTitle.HTMLEncode() + "</h5></div>");

        thisHeadingSort = qnItem.ReportSectionCode;

        while (iQn < qnData.Count) {

          qnItem = qnData[iQn];

          if (thisHeadingSort != qnItem.ReportSectionCode) break;

          sb.AppendLine("<div class=\"question row\">");
          sb.AppendLine("<div class=\"qnText col-md-9\">"
            + "<span class=\"qnNum\">" + qnItem.AutoNumber + ".</span>"
            + qnItem.QuestionText.HTMLEncode() + "</div>");
          sb.AppendLine("<div class=\"qnBars col-md-3\">");
          sb.AppendLine(OrgReports.HTML.GetScoreBarHTML(
            reportData.ReportFilters.BenchType,
            qnItem.Score_Survey_Filtered.Avg,
            qnItem.GetBenchmarkScore(reportData.ReportFilters.BenchType).Avg));
          // sb.AppendLine(
          //   "prev = " + qnItem.Scores.ScorePreviousSelf.Avg.GetValueOrDefault(0).RoundAwayFromZero()
          //   + ", bench = " + qnItem.Scores.ScoreBenchSelf.Avg.GetValueOrDefault(0).RoundAwayFromZero());
          sb.AppendLine("</div>");
          sb.AppendLine("</div>");

          iQn++;
        }
      }

      return sb.ToString();
    }

  }
}
