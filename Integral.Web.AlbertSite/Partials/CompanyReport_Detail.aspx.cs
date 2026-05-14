using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class CompanyReport_Detail : AppCode.PageBaseClasses.CompanyReportPartialBase {

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public int TableRowCount = 0;

    protected void Page_Load(object sender, EventArgs e) {
      GetQuestions();
      GetCompanyScores();
      GetBenchScores();
    }

    void GetQuestions() {

      DbHelper.Common.Query(@"
        SELECT gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gq.GblQuestionId, gq.QuestionTextSelf
        FROM sv_360_Questions sq WITH (NOLOCK)
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        WHERE sv.sv_id = @SampleSurveyId
          AND sq.GblAnswerTypeId = @GblAnswerTypeId
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

    void GetCompanyScores() {

      DbHelper.Common.Query($@"
        WITH results
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
          INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
          WHERE sp.AbleSvCompanyId = @SvCompanyId
            AND sp.Completed IS NOT NULL
            AND {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            AND sq.GblAnswerTypeId = @GblAnswerTypeId
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
            selfAllScore: dr.GetDecimalOrNull("SelfAllScore").Round(1, MidpointRounding.AwayFromZero),
            raterAllScore: dr.GetDecimalOrNull("RaterAllScore").Round(1, MidpointRounding.AwayFromZero)
          );
        },
        GetStandardSqlParams()
      );
    }

    void GetBenchScores() {

      try {
        DbHelper.Common.Query($@"
          SELECT sgs.GblQuestionId,
            SUM(sgs.ScoreSum) AS ScoreSumSelfs, SUM(sgs.ScoreCount) AS ScoreCountSelfs,
            SUM(sgs.ScoreSumRaters) AS ScoreSumRaters, SUM(sgs.ScoreCountRaters) AS ScoreCountRaters
          FROM sv_SurveyGblQnScores sgs WITH (NOLOCK)
          INNER JOIN sv_Survey sv ON sgs.SurveyId = sv.sv_id
          INNER JOIN sv_360_Questions sq ON sgs.GblQuestionId = sq.GblQuestionId
          WHERE sq.SurveyId = @SampleSurveyId
            AND sq.GblAnswerTypeId = @GblAnswerTypeId
            AND {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            {(BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org ? "AND sv.SvCompanyId = @SvCompanyId" : "")}
          GROUP BY sgs.GblQuestionId",
          dr => {
            var gblQuestionId = dr.GetInt("GblQuestionId");
            var scoreSumSelfs = dr.GetDecimal("ScoreSumSelfs", 0);
            var scoreCountSelfs = dr.GetDecimal("ScoreCountSelfs", 0);
            var scoreSumRaters = dr.GetDecimal("ScoreSumRaters", 0);
            var scoreCountRaters = dr.GetDecimal("ScoreCountRaters", 0);
            Questions.Find(q => q.GblQuestionId == dr.GetInt("GblQuestionId"))?.SetBenchScores(
              selfBenchScore: scoreCountSelfs == 0 ? null : (decimal?)(scoreSumSelfs / scoreCountSelfs).Round(1, MidpointRounding.AwayFromZero),
              raterBenchScore: scoreCountRaters == 0 ? null : (decimal?)(scoreSumRaters / scoreCountRaters).Round(1, MidpointRounding.AwayFromZero)
            );
          },
          GetStandardSqlParams()
        );
      } catch (Exception) {
        // Ignore for now.
      }
    }

    public void ShowQuestions(Action<string> categoryStart, Action categoryEnd, Action<QuestionInfo> questionDetail) {

      int iQn = 0;
      int? thisHeadingId = null;

      while (iQn < Questions.Count) {

        var qnItem = Questions[iQn];
        thisHeadingId = qnItem.CategoryHeadingId;

        categoryStart(qnItem.CategoryHeading);

        while (iQn < Questions.Count) {
          qnItem = Questions[iQn];
          if (thisHeadingId != qnItem.CategoryHeadingId) break;
          questionDetail(qnItem);
          iQn++;
        }

        categoryEnd();
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
