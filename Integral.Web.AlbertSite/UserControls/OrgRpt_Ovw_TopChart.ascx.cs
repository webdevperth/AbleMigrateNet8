using System;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.UserControls {

  public partial class OrgRpt_Ovw_TopChart : OrgReportControlBase {

    public string xAxisLabel, selfScoreDS, selfBenchMarkDS;
    public int surveyScoreRounded, benchScoreRounded, benchDifference;

    protected void Page_Load(object sender, EventArgs e) {

      GetChartData();

    } // load

    void GetChartData() {

      // Scores for chart.

      xAxisLabel = "[";
      selfScoreDS = "[";
      selfBenchMarkDS = "[";

      for (int linkLevel = DbHelper.OrgReports.Max_Previous_Surveys; linkLevel >= 0; linkLevel--) {

        var svInfo = reportData.SurveyInfo_GetSurveyAtLinkLevel(linkLevel);

        if (svInfo != null) {

          var svScores = reportData.YearlyIOIChart_GetScores(linkLevel);

          double? scoreSurvey = svScores.SurveyScore_Filtered;
          double? scoreBench = svScores.GetBenchScoreByType(reportData.ReportFilters.BenchType).RoundAwayFromZeroOrNull();

          // Add to json strings for chart data.
          xAxisLabel += (xAxisLabel.Length > 1 ? "," : "") + "'" + svInfo.SurveyYear + "'";
          selfScoreDS += (selfScoreDS.Length > 1 ? "," : "") + GetScoreFormatted(scoreSurvey, false);
          selfBenchMarkDS += (selfBenchMarkDS.Length > 1 ? "," : "") + GetScoreFormatted(scoreBench, false);

          if (linkLevel == 0) {
            // Current scores for left panel.
            surveyScoreRounded = scoreSurvey.RoundAwayFromZeroOrNull().GetValueOrDefault(0); // Won't ever be null for current survey.
            benchScoreRounded = scoreBench.RoundAwayFromZeroOrNull().GetValueOrDefault(0);
            benchDifference = surveyScoreRounded - benchScoreRounded;
          }
        }
      }

      xAxisLabel += "]";
      selfScoreDS += "]";
      selfBenchMarkDS += "]";

    } // GetChartData

    public string GetScoreFormatted(double? score) {
      if (score == null) return "-";
      return ((double)score * 10).ToString("0");
    }

    public string GetScoreFormatted(double? scoreValue, bool replaceNA) {
      if (scoreValue.HasValue) return scoreValue.Value.RoundAwayFromZero().ToString();
      if (replaceNA) return "NA";
      else return "";
    }

  }
}
