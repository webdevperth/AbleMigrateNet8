using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.Reports;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.UserControls {

  public partial class OrgRpt_Categories : OrgReportControlBase {

    public List<OrgReports.CategoryTable> TableList = new List<OrgReports.CategoryTable>();

    protected void Page_Load(object sender, EventArgs e) {

      TableList.Add(new OrgReports.CategoryTable("Quadrants", DbHelper.OrgReports.ValidDimensions.Quadrants));
      TableList.Add(new OrgReports.CategoryTable("Custom", DbHelper.OrgReports.ValidDimensions.ReportSections));

      if (!reportData.SurveyInfo.OrgReportDisableDrivers) {
        TableList.Add(new OrgReports.CategoryTable("Drivers", DbHelper.OrgReports.ValidDimensions.Drivers));
      }
    }

    public Dictionary<int, OrgReports.CategoryTableRow> GetTableRows(OrgReports.CategoryTable currentTable) {
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
          if (!rows.ContainsKey(dimCode)) {
            rows.Add(dimCode, new OrgReports.CategoryTableRow(dimTitle, scores));
            rows[dimCode].RowCount = rows.Count;
          } else {
            rows[dimCode].AccumulateScores(scores);
          }
        }
      }

      // Order rows correctly.
      var rowsOrdered = new Dictionary<int, OrgReports.CategoryTableRow>();
      foreach (int code in DbHelper.OrgReports.GetDimensionCodeOrder(reportData.SurveyInfo.SurveyId, currentTable.Dimension)) {
        if (rows.ContainsKey(code) && rows[code].ItemCount >= Reports.OrgReports.Min_Questions_Per_Category) rowsOrdered.Add(code, rows[code]);
      }
      return rowsOrdered;
    }

    public string GetScoreBarHTML(OrgReports.CategoryTableRow tableRow) {
      return tableRow.GetScoreBarHTML(reportData.ReportFilters.BenchType);
    }

  }
}
