using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class SkillsViewer_Detail : AppCode.PageBaseClasses.SkillsViewerPartialBase {

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public int TableRowCount = 0;

    protected void Page_Load(object sender, EventArgs e) {
      GetQuestions();
      GetProjectScores();
      GetBenchScores();
    }

    void GetQuestions() {

      DbHelper.Common.Query(@"
        SELECT gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gq.GblQuestionId, gq.QuestionTextSelf
        FROM sv_360_Questions sq
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        WHERE sq.SurveyId = @SampleSurveyId
          AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
        GROUP BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gq.GblQuestionId, gq.QuestionTextSelf
        ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gq.GblQuestionId",
        dr => {
          Questions.Add(new QuestionInfo(
            categoryHeadingId: dr.GetInt("RptQnGrpHeadingId"),
            categoryHeading: dr.GetString("RptQnGrpHeading"),
            gblQuestionId: dr.GetInt("GblQuestionId"),
            gblQuestionText: dr.GetString("QuestionTextSelf")
          ));
        },
        GetStandardSqlParams()
      );
    }

    void GetProjectScores() {

      DbHelper.Common.Query($@"
        WITH
          {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Create ProgramJobIds CTE only if there is a ProgramJobId selection.
          ? "ProgramJobIds AS (SELECT Value AS ProgramJobId FROM STRING_SPLIT(@ProgramJobIds, ',')),"
          : "")}
        results
        AS (
          SELECT sq.GblQuestionId, sp.IsSelf,
            SUM(sc.ValueScore) AS ScoreSum,
            COUNT(sc.ValueScore) AS ScoreCount,
            COUNT(DISTINCT sp.PartId) AS PartCount
          FROM sv_Answers sa WITH (NOLOCK)
          INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
          INNER JOIN sv_360_Participants sp ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_360_Questions sq ON sa.QuestionId = sq.QuestionId
          INNER JOIN al_RptQnGrpHgGblQns qgh ON sq.GblQuestionId = qgh.GblQuestionId
          {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Join to ProgramJobIds CTE if included.
            ? "INNER JOIN ProgramJobIds pjs ON pjs.ProgramJobId = sp.AbleProgramJobId"
            : "")}
          INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
          WHERE sp.AbleProjectId = @ProjectId
            AND sp.Completed IS NOT NULL
            AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
            {(SurveyStats.RptQnGroupId != null ? "AND sq.RptQnGroupId = @RptQnGroupId" : "")}
          GROUP BY sq.GblQuestionId, sp.IsSelf
        )
        SELECT
          GblQuestionId,
          SUM(IIF(IsSelf = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1, ScoreCount, NULL)) AS SelfAllScore,
          SUM(IIF(IsSelf = 0, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0, ScoreCount, NULL)) AS RaterAllScore
        FROM results
        GROUP BY GblQuestionId;",
        dr => {
          Questions.Find(q => q.GblQuestionId == dr.GetInt("GblQuestionId"))?.SetScores(
            selfAllScore: dr.GetDecimalOrNull("SelfAllScore"),
            raterAllScore: dr.GetDecimalOrNull("RaterAllScore")
          );
        },
        GetStandardSqlParams()
      );
    }

    void GetBenchScores() {

      try {
        DbHelper.Reports.General.GetNorms_Questions(SurveyStats.SampleSurveyId, SurveyStats.RptQnGroupId, BenchmarkType, qNorm => {
          Questions.Find(q => q.GblQuestionId == qNorm.GblQuestionId)?.SetBenchScores(qNorm.NormResult.SelfNorm, qNorm.NormResult.RaterNorm);
        });
      } catch (Exception) {
        // Ignore for now.
      }
    }

    public void ShowQuestions(Action<string> categoryStart, Action<decimal, decimal> categoryEnd, Action<QuestionInfo> questionDetail) {

      int iQn = 0;
      int? thisHeadingId = null;

      while (iQn < Questions.Count) {

        var qnItem = Questions[iQn];
        thisHeadingId = qnItem.CategoryHeadingId;

        categoryStart(qnItem.CategoryHeading);

        decimal selfSum = 0, raterSum = 0;
        int qnCount = 0;

        while (iQn < Questions.Count) {

          qnItem = Questions[iQn];
          if (thisHeadingId != qnItem.CategoryHeadingId) break;

          questionDetail(qnItem);
          iQn++;

          // For category average.
          selfSum += qnItem.SelfAllScore ?? 0;
          raterSum += qnItem.RaterAllScore ?? 0;
          qnCount++;
        }

        categoryEnd(selfSum / qnCount, raterSum / qnCount);
      }
    }

    public string GetBenchComparisonRowClass(QuestionInfo question) {

      if (question.SelfAllScore != null && question.SelfBenchScore != null && question.SelfAllScore < question.SelfBenchScore) {
        return ""; // was "benchBelow" for red bars, disabled for now (CU-860r0jygu).
      } else {
        return "";
      }
    }

    public class QuestionInfo {
      public int CategoryHeadingId { get; private set; }
      public string CategoryHeading { get; private set; }
      public int GblQuestionId { get; private set; }
      public string GblQuestionText { get; private set; }
      public decimal? SelfAllScore { get; private set; }
      public decimal? RaterAllScore { get; private set; }
      public decimal? SelfBenchScore { get; private set; }
      public decimal? RaterBenchScore { get; private set; }
      public QuestionInfo(
        int categoryHeadingId,
        string categoryHeading,
        int gblQuestionId,
        string gblQuestionText
      ) {
        CategoryHeadingId = categoryHeadingId;
        CategoryHeading = categoryHeading;
        GblQuestionId = gblQuestionId;
        GblQuestionText = gblQuestionText;
      }
      public void SetScores(
        decimal? selfAllScore,
        decimal? raterAllScore
      ) {
        SelfAllScore = selfAllScore;
        RaterAllScore = raterAllScore;
      }
      public void SetBenchScores(
        decimal? selfBenchScore,
        decimal? raterBenchScore
      ) {
        SelfBenchScore = selfBenchScore;
        RaterBenchScore = raterBenchScore;
      }
    }

  }
}
