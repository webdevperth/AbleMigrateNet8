using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  using static ReportHelper.HeatMap;

  public class CompanyReport_HeatMap : AppCode.PageBaseClasses.CompanyReportPartialBase {

    // Expose protected base members for Razor access.
    public new DbHelper.Reports.Company.SurveyStats SurveyStats => base.SurveyStats;
    public new PathHelper.SurveyViewerBenchmarkEnum BenchmarkType => base.BenchmarkType;
    public new string BenchmarkDisplayName => base.BenchmarkDisplayName;

    const int OtherRowDummyEntityId = 0;

    public List<HeatMapColumn> HeatMapColumns = new List<HeatMapColumn>();
    public List<HeatMapRow> HeatMapRows = new List<HeatMapRow>();
    public Enums.RowSortBy QueryRowSortBy = Enums.RowSortBy.Unsorted;

    public IActionResult OnGet() {

      QueryRowSortBy = RowSortOptions.FindOrDefault(WebHelper.GetQueryStringValue(QueryKeys.RowSortBy) ?? "", RowSortOptions[DefaultSortKey]).OptionEnum;

      // Define columns first. Each row contains a list of the column data.
      GetHeatMapColums();
      GetHeatMapColumBenchScores();

      GetHeatMapRows();
      GetHeatMapRowData();
      RemoveUnusedRows();

      SortHeatMapRows();
      return Page();
    }

    void GetHeatMapColums() { // Just the column heading titles and IDs.

      DbHelper.Common.Query(@"
        SELECT gh.RptQnGrpHeadingId, gh.RptQnGrpHeading
        FROM sv_360_Questions sq
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gqh.RptQnGrpHeadingId = gh.RptQnGrpHeadingId
        WHERE sq.SurveyId = @SampleSurveyId
          AND sq.GblAnswerTypeId = @GblAnswerTypeId
        GROUP BY gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gh.RptQnGrpHeadingSort
        ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId",
        dr => {
          HeatMapColumns.Add(new HeatMapColumn(
            dr.GetInt("RptQnGrpHeadingId"),
            dr.GetString("RptQnGrpHeading")
          ));
        },
        GetStandardSqlParams()
      );
    }

    void GetHeatMapColumBenchScores() {
      // Org or Global benchmark scores for each column.

      DbHelper.Common.Query($@"
        SELECT
          gqh.RptQnGrpHeadingId,
          SUM(sgs.ScoreSum) AS ScoreSumSelfs, SUM(sgs.ScoreCount) AS ScoreCountSelfs,
          SUM(sgs.ScoreSumRaters) AS ScoreSumRaters, SUM(sgs.ScoreCountRaters) AS ScoreCountRaters
        FROM sv_SurveyGblQnScores sgs WITH (NOLOCK)
        INNER JOIN sv_Survey sv ON sgs.SurveyId = sv.sv_id
        INNER JOIN al_RptQnGrpHgGblQns gqh ON sgs.GblQuestionId = gqh.GblQuestionId
        WHERE {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
          AND sv.PrimaryGblAnswerTypeId = @GblAnswerTypeId
          {(BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org ? "AND sv.SvCompanyId = @SvCompanyId" : "")}
        GROUP BY gqh.RptQnGrpHeadingId",
        dr => {
          int columnEntityId = dr.GetInt("RptQnGrpHeadingId");
          var heatMapColumn = HeatMapColumns.Find(c => c.ColumnEntityId == columnEntityId);
          if (heatMapColumn == null) return;

          int scoreCount;
          long scoreSum;
          scoreCount = dr.GetInt("ScoreCountSelfs", 0);
          scoreSum = dr.GetInt("ScoreSumSelfs", 0);
          /*
          if (QueryRowSortBy == Enums.RowSortBy.SelfScore) {
            scoreCount = dr.GetInt("ScoreCountSelfs", 0);
            scoreSum = dr.GetInt("ScoreSumSelfs", 0);
          } else if (QueryRowSortBy == Enums.RowSortBy.RaterScore) {
            scoreCount = dr.GetInt("ScoreCountRaters", 0);
            scoreSum = dr.GetInt("ScoreSumRaters", 0);
          } else {
            scoreCount = dr.GetInt("ScoreCountSelfs", 0) + dr.GetInt("ScoreCountRaters", 0);
            scoreSum = dr.GetInt("ScoreSumSelfs", 0) + dr.GetInt("ScoreSumRaters", 0);
          }
          */
          heatMapColumn.SetBenchScore(scoreCount, scoreSum);
        },
        GetStandardSqlParams()
      );
    }

    void GetHeatMapRows() {

      // This "other" program will contain results for programs that don't have enough responses to be listed separately.
      var otherRow = new HeatMapRow(OtherRowDummyEntityId, "Other", 0, HeatMapColumns);

      DbHelper.Common.Query($@"
        SELECT ij.JobId, CONCAT(ij.JobNumber, ': ', ij.JobName) AS JobName, sp.ResponseCount
        FROM id_Job ij
        INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber
        CROSS APPLY (
          SELECT COUNT(sp.Completed) AS ResponseCount
          FROM sv_360_Participants sp
          INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
          WHERE sp.AbleProgramJobId = ij.JobId
            AND sp.IsSelf = 1
            AND {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            AND sv.PrimaryGblAnswerTypeId = @GblAnswerTypeId
        ) AS sp
        WHERE ap.SvCompanyId = @SvCompanyId
        ORDER BY ij.AbleProgramStartDateUtc, ij.JobId",
        dr => {
          int rowEntityId = dr.GetInt("JobId");
          string rowTitle = dr.GetString("JobName");
          int responseCount = dr.GetIntOrNull("ResponseCount") ?? 0;
          if (SurveyStats.ProgramJobIds.IsNullOrEmpty() || SurveyStats.ProgramJobIds.Contains(rowEntityId)) {
            if (responseCount >= ConfigHelper.Reports_SkillsViewer_MinSelfResponses) {
              HeatMapRows.Add(new HeatMapRow(rowEntityId, rowTitle, responseCount, HeatMapColumns));
            } else {
              otherRow.AddResponseCount(responseCount); // For items with less than min required responses.
            }
          }
        },
        GetStandardSqlParams()
      );

      HeatMapRows.Add(otherRow); // Ensure "other" is the last row.
    }

    string GetIsSelfSQLCondition() {
      if (QueryRowSortBy == Enums.RowSortBy.SelfScore) {
        return "AND sp.IsSelf = 1";
      } else if (QueryRowSortBy == Enums.RowSortBy.RaterScore) {
        return "AND sp.IsSelf = 0";
      }
      return "";
    }

    void GetHeatMapRowData() {

      DbHelper.Common.Query($@"
        SELECT ij.JobId, gqh.RptQnGrpHeadingId,
          COUNT(sc.ValueScore) AS ScoreCount,
          SUM(sc.ValueScore) AS ScoreSum
        FROM id_Job ij
        INNER JOIN sv_360_Participants sp ON ij.JobId = sp.AbleProgramJobId
        INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        WHERE {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
          AND sq.GblAnswerTypeId = @GblAnswerTypeId
          AND sp.AbleSvCompanyId = @SvCompanyId
          AND sp.Completed IS NOT NULL
          {GetIsSelfSQLCondition()}
        GROUP BY ij.JobId, gqh.RptQnGrpHeadingId
        ORDER BY ij.JobId, gqh.RptQnGrpHeadingId;",
        dr => {
          int rowEntityId = dr.GetInt("JobId");
          var thisRow = HeatMapRows.Find(r => r.RowEntityId == rowEntityId);
          if (thisRow == null) thisRow = HeatMapRows.Find(r => r.RowEntityId == OtherRowDummyEntityId);

          int columnEntityId = dr.GetInt("RptQnGrpHeadingId");
          int columnIndex = HeatMapColumns.FindIndex(c => c.ColumnEntityId == columnEntityId);
          if (columnIndex < 0) return;

          decimal? columnBenchScore = HeatMapColumns[columnIndex].BenchScore; // Benchmark score for the column.
          ScoreInfo columnScore = thisRow.ColumnScores[columnIndex].ScoreInfo;
          if (columnScore == null) return;

          int scoreCount = dr.GetInt("ScoreCount");
          long scoreSum = dr.GetIntOrNull("ScoreSum") ?? 0;

          columnScore.SetScore(scoreCount, scoreSum, columnBenchScore);
          thisRow.RowScore.AccumulateScore(scoreCount, scoreSum);
        },
        GetStandardSqlParams()
      );
    }

    void RemoveUnusedRows() {

      if (HeatMapRows.IsNullOrEmpty() || HeatMapColumns.IsNullOrEmpty()) return;

      var removeRowEntityIds = new List<int>();

      // For each row, if there are no scores in any column, remove it.
      foreach (var row in HeatMapRows) {
        int rowEntityId = row.RowEntityId;
        if (row.ColumnScores.Find(c => c.ScoreInfo?.Score != null) == null) {
          removeRowEntityIds.Add(rowEntityId);
        }
      }
      if (removeRowEntityIds.Count > 0) {
        HeatMapRows.RemoveAll(row => removeRowEntityIds.Contains(row.RowEntityId));
      }
    }

    private void SortHeatMapRows() {
      if (QueryRowSortBy == Enums.RowSortBy.Unsorted) return;
      HeatMapRows.Sort((r1, r2) => (int)((r2.RowScore.Score ?? 0) * 100) - (int)((r1.RowScore.Score ?? 0) * 100));
    }

  }
}
