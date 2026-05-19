using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the OrgRpt_Focus ViewComponent. Mirrors the public API of the
  // legacy UserControls/OrgRpt_Focus.ascx.cs codebehind so the .cshtml view
  // can call the same helper methods used by the original <% %> blocks.
  public class OrgRpt_FocusModel {

    public DbHelper.OrgReportsCached.ReportData reportData;

    public List<OrgReports.FocusTable> TableList = new List<OrgReports.FocusTable>();
    public OrgReports.FocusTableRow currentTableRow;

    SortedList<double, DbHelper.OrgReportsCached.QuestionInfo> sortedScores;

    const int Max_Rows_Per_Table = 4; // Maximum number of rows (i.e. questions) in each table.

    public void Initialize() {

      // Create sorted list of scores.
      sortedScores = new SortedList<double, DbHelper.OrgReportsCached.QuestionInfo>();
      foreach (var qnItem in reportData.LikertQuestions) {
        if (qnItem.Score_Survey_Filtered.Avg != null) {
          double uniqueScoreKey = Math.Ceiling((double)qnItem.Score_Survey_Filtered.Avg * 1000) + (qnItem.Sort / 1000.0);
          sortedScores.Add(uniqueScoreKey, qnItem);
        }
      }

      TableList = new List<OrgReports.FocusTable>() {
        new OrgReports.FocusTable("Highest Scoring Statements", OrgReports.FocusTableMode.Highest),
        new OrgReports.FocusTable("Lowest Scoring Statements", OrgReports.FocusTableMode.Lowest)
      };
    }

    public List<OrgReports.FocusTableRow> GetTableRows(OrgReports.FocusTable table) {
      // Return list of rows, each being an accumulation of question scores for the current table's dimension.

      var rows = new List<OrgReports.FocusTableRow>(); // The result set to output for binding.
      var sortedQnInfo = sortedScores.Values; // Questions sorted by scores.

      // Determine loop start & direction (down for lowest to highest, up for highest to lowest)
      bool isHighestFirst = table.TableMode == OrgReports.FocusTableMode.Highest;
      int index = isHighestFirst ? sortedQnInfo.Count - 1 : 0; // start at bottom or top
      int step = isHighestFirst ? -1 : 1; // direction up or down
      int qnCount = 0; // count up to max items allowed.

      while (index >= 0 && index < sortedQnInfo.Count && qnCount < Max_Rows_Per_Table) {
        var qnInfo = sortedQnInfo[index];
        rows.Add(new OrgReports.FocusTableRow(
          qnInfo.AutoNumber.ToString(),
          qnInfo.QuestionText,
          qnInfo.ToQuestionScores(reportData.ReportFilters.BenchType),
          qnInfo.DriverTitle));
        index += step;
        qnCount += 1;
      }

      return rows;
    }

    public string GetScoreBarHTML(OrgReports.FocusTableRow tableRow) {
      return tableRow.GetScoreBarHTML(reportData.ReportFilters.BenchType);
    }
  }
}
