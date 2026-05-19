using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the OrgRpt_Ovw_IOIDirs ViewComponent. Mirrors the public API of the
  // legacy UserControls/OrgRpt_Ovw_IOIDirs.ascx.cs codebehind so the .cshtml view
  // can read the same dirLabels / dataSetsJson fields used by the original <%= %> blocks.
  public class OrgRpt_Ovw_IOIDirsModel {

    public DbHelper.OrgReportsCached.ReportData reportData;

    public string dirLabels; // Division/Directorate labels along the chart x axis.
    public string dataSetsJson;  // Dataset JSON for chart.

    string[] levelScores = { };

    public void GetChartData() {

      // IOI avgs for latest surveys.
      // Replace latest with currentScoreRounded (which is influenced by the filters)

      Array.Resize<string>(ref levelScores, reportData.GetMaxUsedLinkLevel() + 1);

      dirLabels = "";
      dataSetsJson = "";

      List<DbHelper.OrgReportsCached.DivChartScores> chartScores;
      if (reportData.ReportFilters.HasDivisions) {
        chartScores = reportData.DivisionChartScores;
      } else {
        chartScores = reportData.DirectorateChartScores;
      }

      int dataSetsToShow = 0;

      foreach (var scoreInfo in chartScores) {
        // If showing divisions (not directorates) and this one isn't in the selected list, then skip it.
        if (!reportData.ReportFilters.HasDivisions || reportData.ReportFilters.DivisionCodeIds.Contains(scoreInfo.DivCodeId)) {
          dataSetsToShow++;
          AddDataSet(scoreInfo);
        }
      }

      // Ensure there are at least 5 datasets so the chart isn't too spread out.
      // Extra datasets on the end should appear blank.
      if (dataSetsToShow > 0) {
        while (dataSetsToShow < 5) {
          dataSetsToShow++;
          AddDataSet(new DbHelper.OrgReportsCached.DivChartScores("")); // Blank title, no scores.
        }
      }

      // Output scores as a string of json used in the chart js code.
      for (var linkLevel = reportData.GetMaxUsedLinkLevel(); linkLevel >= 0; linkLevel--) {
        var svInfo = reportData.SurveyInfo_GetSurveyAtLinkLevel(linkLevel);
        if (svInfo != null) {
          if (!dataSetsJson.IsNullOrEmpty()) dataSetsJson += ",";
          dataSetsJson += GetDataSetJson(
            svInfo.SurveyYear.ToString(),
            levelScores[linkLevel],
            OrgReports.LinkLevelColours[linkLevel]);
        }
      }

    }

    void AddDataSet(DbHelper.OrgReportsCached.DivChartScores divInfo) {
      // Name of this division or directorate for x axis label.
      dirLabels += (dirLabels.IsNullOrEmpty() ? "" : ",") + "'" + divInfo.DivTitle.Replace("'", "\'") + "'";

      // Record score for each link level.
      for (var linkLevel = 0; linkLevel <= reportData.GetMaxUsedLinkLevel(); linkLevel++) {
        if (!levelScores[linkLevel].IsNullOrEmpty()) levelScores[linkLevel] += ",";
        levelScores[linkLevel] += divInfo.GetScore(linkLevel).RoundAwayFromZeroOrNull().ToStringOrEmptyIfNull();
      }
    }

    string GetDataSetJson(string label, string levelScores, string backgroundColor) {
      return "{\n"
          + "  label: '" + label + "',\n"
          + "  data: [" + levelScores + "],\n"
          + "  backgroundColor: '" + backgroundColor + "',\n"
          + "  borderWidth: 0\n"
          + "}\n";
    }

    public string GetScoreFormatted(double? scoreValue, bool replaceNA) {
      if (scoreValue.HasValue) return scoreValue.Value.RoundAwayFromZero().ToString();
      if (replaceNA) return "NA";
      else return "";
    }

  }
}
