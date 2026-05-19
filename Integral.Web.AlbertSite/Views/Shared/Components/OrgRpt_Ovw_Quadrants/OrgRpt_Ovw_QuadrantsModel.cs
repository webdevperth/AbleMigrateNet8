using System.Collections.Generic;
using System.Text;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the OrgRpt_Ovw_Quadrants ViewComponent. Mirrors the public API of the
  // legacy UserControls/OrgRpt_Ovw_Quadrants.ascx.cs codebehind so the .cshtml view
  // can call GetTableRows/GetScoreBarHTML exactly as the original <% %> blocks did.
  public class OrgRpt_Ovw_QuadrantsModel {

    public DbHelper.OrgReportsCached.ReportData reportData;

    public List<OrgReports.CategoryTable> TableList;
    public StringBuilder debugScoreLog;

    public void Initialize() {

      debugScoreLog = new StringBuilder();

      TableList = new List<OrgReports.CategoryTable>() {
        new OrgReports.CategoryTable("Quadrants", DbHelper.OrgReports.ValidDimensions.Quadrants)
      };
    }

    public List<OrgReports.CategoryTableRow> GetTableRows(OrgReports.CategoryTable currentTable) {
      // Return list of rows, each being an accumulation of question scores for the current table's dimension.

      var rows = new Dictionary<int, OrgReports.CategoryTableRow>();

      foreach (var qnInfo in reportData.LikertQuestions) {

        int dimCode = 0; string dimTitle = "[no title]";

        switch (currentTable.Dimension) {
          case DbHelper.OrgReports.ValidDimensions.Quadrants:
            dimCode = qnInfo.QuadrantCode;
            dimTitle = qnInfo.QuadrantTitle;
            break;
          case DbHelper.OrgReports.ValidDimensions.ReportSections:
            if (qnInfo.ReportSectionCode != 1) { // Ignore IOI (code 1) questions
              dimCode = qnInfo.ReportSectionCode;
              dimTitle = qnInfo.ReportSectionTitle;
            }
            break;
          case DbHelper.OrgReports.ValidDimensions.Drivers:
            dimCode = qnInfo.DriverCode;
            dimTitle = qnInfo.DriverTitle;
            break;
        }
        if (dimCode > 0) {
          var scores = qnInfo.ToQuestionScores(reportData.ReportFilters.BenchType);
          debugScoreLog.AppendLine(dimCode + ", " + scores.ScoreSelf.Sum + ", " + scores.ScoreSelf.Count);
          if (!rows.ContainsKey(dimCode)) {
            rows.Add(dimCode, new OrgReports.CategoryTableRow(dimTitle, scores));
            rows[dimCode].RowCount = rows.Count;
          } else {
            rows[dimCode].AccumulateScores(scores);
          }
        }
      }

      // Order rows correctly.
      var rowsOrdered = new List<OrgReports.CategoryTableRow>();
      foreach (int code in DbHelper.OrgReports.GetDimensionCodeOrder(reportData.SurveyInfo.SurveyId, DbHelper.OrgReports.ValidDimensions.Quadrants)) {
        rowsOrdered.Add(rows[code]);
      }

      return rowsOrdered;
    }

    public string GetDebugScoreLog() {
      return debugScoreLog.ToString();
    }

    public string GetScoreBarHTML(OrgReports.CategoryTableRow currentTableRow) {
      return currentTableRow.GetScoreBarHTML(reportData.ReportFilters.BenchType);
    }

  }
}
