using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  using static ReportHelper.HeatMap;

  public class SkillsViewer_HeatMap : AppCode.PageBaseClasses.SkillsViewerPartialBase {

    // Expose protected base members as public for Razor view access.
    public new DbHelper.Reports.SkillsViewer.SurveyStats SurveyStats => base.SurveyStats;
    public new string BenchmarkDisplayName => base.BenchmarkDisplayName;
    public new DbHelper.Reports.NormEnum BenchmarkType => base.BenchmarkType;

    public List<HeatMapColumn> HeatMapColumns = new List<HeatMapColumn>();
    public List<HeatMapRow> HeatMapRows = new List<HeatMapRow>();
    public Enums.RowSortBy QueryRowSortBy = Enums.RowSortBy.Unsorted;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

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

    void GetHeatMapColums() {

      DbHelper.Common.Query(@"
        SELECT gh.RptQnGrpHeadingId, gh.RptQnGrpHeading
        FROM sv_360_Questions sq
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gqh.RptQnGrpHeadingId = gh.RptQnGrpHeadingId
        WHERE sq.SurveyId = @SampleSurveyId
          AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
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

      DbHelper.Common.Query($@"
        SELECT
          gqh.RptQnGrpHeadingId,
          SUM(sgs.ScoreSum) AS ScoreSumSelfs, SUM(sgs.ScoreCount) AS ScoreCountSelfs,
          SUM(sgs.ScoreSumRaters) AS ScoreSumRaters, SUM(sgs.ScoreCountRaters) AS ScoreCountRaters
        FROM sv_SurveyGblQnScores sgs
        INNER JOIN sv_Survey sv ON sgs.SurveyId = sv.sv_id
        INNER JOIN al_RptQnGrpHgGblQns gqh ON sgs.GblQuestionId = gqh.GblQuestionId
        WHERE sgs.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
          {(BenchmarkType == DbHelper.Reports.NormEnum.Org ? "AND sv.SvCompanyId = @SvCompanyId" : "")}
        GROUP BY gqh.RptQnGrpHeadingId",
        dr => {

          int columnEntityId = dr.GetInt("RptQnGrpHeadingId");
          var heatMapColumn = HeatMapColumns.Find(c => c.ColumnEntityId == columnEntityId);
          if (heatMapColumn == null) return;

          int scoreCount = dr.GetInt("ScoreCountSelfs", 0);
          long scoreSum = dr.GetInt("ScoreSumSelfs", 0);
          heatMapColumn.SetBenchScore(scoreCount, scoreSum);
        },
        GetStandardSqlParams()
      );
    }

    void GetHeatMapRows() {

      DbHelper.Common.Query(@"
        SELECT JobId, JobName
        FROM id_Job
        WHERE JobNumber = @JobNumber
        ORDER BY AbleProgramStartDateUtc, JobId",
        dr => {
          int rowEntityId = dr.GetInt("JobId");
          string rowTitle = dr.GetString("JobName");
          if (SurveyStats.ProgramJobIds.IsNullOrEmpty() || SurveyStats.ProgramJobIds.Contains(rowEntityId)) {
            HeatMapRows.Add(new HeatMapRow(rowEntityId, rowTitle, 0, HeatMapColumns));
          }
        },
        DbHelper.Common.NewSqlParameter("JobNumber", ProjectInfo.JobNumber)
      );
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

      // TODO: Put program & participant CTEs in a common function used by all reports.
      DbHelper.Common.Query($@"
        WITH
        {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Create ProgramJobIds CTE only if there is a ProgramJobId selection.
        ? "ProgramJobIds AS (SELECT Value AS ProgramJobId FROM STRING_SPLIT(@ProgramJobIds, ',')),"
        : "")}
        PartIds AS (
          SELECT sp.AbleProgramJobId, sp.PartId, sp.IsSelf,
            COUNT(*) OVER (PARTITION BY sp.IsSelf) AS PartCount
          FROM sv_360_Participants sp
          {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Join to ProgramJobIds CTE if included.
            ? "INNER JOIN ProgramJobIds pjs ON pjs.ProgramJobId = sp.AbleProgramJobId"
            : "")}
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          WHERE sp.AbleProjectId = @ProjectId
            AND sp.Completed IS NOT NULL
            AND {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            AND sv.PrimaryGblAnswerTypeId = {ConfigHelper.GblAnsTypeId_Standard360}
            {GetIsSelfSQLCondition()}
          GROUP BY sp.AbleProgramJobId, sp.PartId, sp.IsSelf
        )
        SELECT pj.JobId, rptgh.RptQnGrpHeadingId,
          COUNT(sc.ValueScore) AS ScoreCount,
          SUM(sc.ValueScore) AS ScoreSum
        FROM sv_Answers sa
        INNER JOIN PartIds pids ON pids.PartId = sa.ParticipantId
        INNER JOIN id_Job pj ON pj.JobId = pids.AbleProgramJobId
        INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        INNER JOIN al_RptQnGrpHgGblQns rptghg ON rptghg.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings rptgh ON rptgh.RptQnGrpHeadingId = rptghg.RptQnGrpHeadingId
        WHERE sq.GblAnswerTypeId = {ConfigHelper.GblAnsTypeId_Standard360}
          {(SurveyStats.RptQnGroupId != null ? "AND sq.RptQnGroupId = @RptQnGroupId" : "")}
        GROUP BY pj.JobId, rptgh.RptQnGrpHeadingId
        ORDER BY pj.JobId, rptgh.RptQnGrpHeadingId;",
        dr => {
          int rowEntityId = dr.GetInt("JobId");
          var thisRow = HeatMapRows.Find(r => r.RowEntityId == rowEntityId);
          if (thisRow == null) return;

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
