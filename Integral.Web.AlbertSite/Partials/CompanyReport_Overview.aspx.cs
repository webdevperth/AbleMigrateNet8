using System;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class CompanyReport_Overview : AppCode.PageBaseClasses.CompanyReportPartialBase {

    public decimal? SelfBenchScore, RaterBenchScore;
    public decimal? SelfAllScore, RaterAllScore;
    public decimal? SelfPreScore, RaterPreScore;
    public decimal? SelfPostScore, RaterPostScore;

    protected void Page_Load(object sender, EventArgs e) {
      GetCompanyScores();
      GetBenchScores();
    }

    void GetCompanyScores() {

      DbHelper.Common.Query($@"
        WITH results
        AS (
          SELECT sp.IsSelf, sp.IsSelfPreSurvey, sp.IsSelfPostSurvey,
            SUM(sc.ValueScore) AS ScoreSum,
            COUNT(sc.ValueScore) AS ScoreCount
          FROM sv_Answers sa WITH (NOLOCK)
          INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
          INNER JOIN sv_360_Participants sp ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_360_Questions sq ON sa.QuestionId = sq.QuestionId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
          INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
          WHERE sp.AbleSvCompanyId = @SvCompanyId
            AND sp.Completed IS NOT NULL
            AND {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            AND sq.GblAnswerTypeId = @GblAnswerTypeId
          GROUP BY sp.IsSelf, sp.IsSelfPreSurvey, sp.IsSelfPostSurvey
        )
        SELECT
          SUM(IIF(IsSelf = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1, ScoreCount, NULL)) AS SelfAllScore,
          SUM(IIF(IsSelf = 0, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0, ScoreCount, NULL)) AS RaterAllScore,
          SUM(IIF(IsSelf = 1 AND IsSelfPreSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1 AND IsSelfPreSurvey = 1, ScoreCount, NULL)) AS SelfPreScore,
          SUM(IIF(IsSelf = 0 AND IsSelfPreSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0 AND IsSelfPreSurvey = 1, ScoreCount, NULL)) AS RaterPreScore,
          SUM(IIF(IsSelf = 1 AND IsSelfPostSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1 AND IsSelfPostSurvey = 1, ScoreCount, NULL)) AS SelfPostScore,
          SUM(IIF(IsSelf = 0 AND IsSelfPostSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0 AND IsSelfPostSurvey = 1, ScoreCount, NULL)) AS RaterPostScore
        FROM results;",
        dr => {
          SelfAllScore = dr.GetDecimalOrNull("SelfAllScore").Round(1, MidpointRounding.AwayFromZero, 0);
          RaterAllScore = dr.GetDecimalOrNull("RaterAllScore").Round(1, MidpointRounding.AwayFromZero, 0);
          SelfPreScore = dr.GetDecimalOrNull("SelfPreScore").Round(1, MidpointRounding.AwayFromZero, 0);
          RaterPreScore = dr.GetDecimalOrNull("RaterPreScore").Round(1, MidpointRounding.AwayFromZero, 0);
          SelfPostScore = dr.GetDecimalOrNull("SelfPostScore").Round(1, MidpointRounding.AwayFromZero, 0);
          RaterPostScore = dr.GetDecimalOrNull("RaterPostScore").Round(1, MidpointRounding.AwayFromZero, 0);
        },
        GetStandardSqlParams()
      );
    }

    void GetBenchScores() {

      // Org scores for questions in selected intakes.
      try {
        DbHelper.Common.Query($@"
          SELECT
            SUM(sgs.ScoreSum) AS ScoreSumSelfs, SUM(sgs.ScoreCount) AS ScoreCountSelfs,
            SUM(sgs.ScoreSumRaters) AS ScoreSumRaters, SUM(sgs.ScoreCountRaters) AS ScoreCountRaters
          FROM sv_SurveyGblQnScores sgs WITH (NOLOCK)
          INNER JOIN sv_Survey sv ON sgs.SurveyId = sv.sv_id
          WHERE sgs.GblAnswerTypeId = @GblAnswerTypeId
            AND {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            {(BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org ? "AND sv.SvCompanyId = @SvCompanyId" : "")}",
          dr => {
            var scoreSumSelfs = dr.GetDecimal("ScoreSumSelfs", 0);
            var scoreCountSelfs = dr.GetDecimal("ScoreCountSelfs", 0);
            var scoreSumRaters = dr.GetDecimal("ScoreSumRaters", 0);
            var scoreCountRaters = dr.GetDecimal("ScoreCountRaters", 0);
            SelfBenchScore = scoreCountSelfs == 0 ? null : (decimal?)(scoreSumSelfs / scoreCountSelfs).Round(1, MidpointRounding.AwayFromZero);
            RaterBenchScore = scoreCountRaters == 0 ? null : (decimal?)(scoreSumRaters / scoreCountRaters).Round(1, MidpointRounding.AwayFromZero);
          },
          GetStandardSqlParams()
        );
      } catch (Exception) {
        // Ignore for now.
      }
    }
  }
}
