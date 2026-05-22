using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class CompanyReport_PrePost : AppCode.PageBaseClasses.CompanyReportPartialBase {

    // Expose protected base members for Razor access.
    public new DbHelper.Reports.Company.SurveyStats SurveyStats => base.SurveyStats;
    public new PathHelper.SurveyViewerBenchmarkEnum BenchmarkType => base.BenchmarkType;
    public new string BenchmarkDisplayName => base.BenchmarkDisplayName;

    public List<DbHelper.Reports.PrePost.QuestionInfo> Questions = new List<DbHelper.Reports.PrePost.QuestionInfo>();
    public int TableRowCount = 0;

    public IActionResult OnGet() {

      Questions = DbHelper.Reports.PrePost.GetQuestions(GetStandardSqlParams());

      GetCompanyScores();
      GetBenchScores();
      return Page();
    }

    void GetCompanyScores() {

      DbHelper.Common.Query($@"
        WITH results
        AS (
          SELECT sq.GblQuestionId, sp.IsSelf, sp.IsSelfPreSurvey, sp.IsSelfPostSurvey,
            SUM(sc.ValueScore) AS ScoreSum,
            COUNT(sc.ValueScore) AS ScoreCount,
            COUNT(DISTINCT sp.PartId) AS PartCount
          FROM sv_Answers sa WITH (NOLOCK)
          INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
          INNER JOIN sv_360_Participants sp ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_360_Questions sq ON sa.QuestionId = sq.QuestionId
          INNER JOIN al_RptQnGrpHgGblQns qgh ON sq.GblQuestionId = qgh.GblQuestionId
          INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
          WHERE {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            AND sq.GblAnswerTypeId = @GblAnswerTypeId
            AND sq.RptQnGroupId = @RptQnGroupId
            AND sp.AbleSvCompanyId = @SvCompanyId
            AND sp.Completed IS NOT NULL
          GROUP BY sq.GblQuestionId, sp.IsSelf, sp.IsSelfPreSurvey, sp.IsSelfPostSurvey
        )
        SELECT
          GblQuestionId,
          SUM(IIF(IsSelf = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1, ScoreCount, NULL)) AS SelfAllScore,
          SUM(IIF(IsSelf = 0, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0, ScoreCount, NULL)) AS RaterAllScore,
          SUM(IIF(IsSelf = 1 AND IsSelfPreSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1 AND IsSelfPreSurvey = 1, ScoreCount, NULL)) AS SelfPreScore,
          SUM(IIF(IsSelf = 0 AND IsSelfPreSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0 AND IsSelfPreSurvey = 1, ScoreCount, NULL)) AS RaterPreScore,
          SUM(IIF(IsSelf = 1 AND IsSelfPostSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1 AND IsSelfPostSurvey = 1, ScoreCount, NULL)) AS SelfPostScore,
          SUM(IIF(IsSelf = 0 AND IsSelfPostSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0 AND IsSelfPostSurvey = 1, ScoreCount, NULL)) AS RaterPostScore
        FROM results
        GROUP BY GblQuestionId;",
        dr => {
          if (!dr.IsDBNull("GblQuestionId")) {
            Questions.Find(q => q.GblQuestionId == dr.GetInt("GblQuestionId"))?.SetScores(
              scoreSelfAll: dr.GetDecimalOrNull("SelfAllScore").Round(1, MidpointRounding.AwayFromZero),
              scoreRaterAll: dr.GetDecimalOrNull("RaterAllScore").Round(1, MidpointRounding.AwayFromZero),
              scoreSelfPre: dr.GetDecimalOrNull("SelfPreScore").Round(1, MidpointRounding.AwayFromZero),
              scoreRaterPre: dr.GetDecimalOrNull("RaterPreScore").Round(1, MidpointRounding.AwayFromZero),
              scoreSelfPost: dr.GetDecimalOrNull("SelfPostScore").Round(1, MidpointRounding.AwayFromZero),
              scoreRaterPost: dr.GetDecimalOrNull("RaterPostScore").Round(1, MidpointRounding.AwayFromZero)
            );
          }
        },
        GetStandardSqlParams()
      );
    }

    void GetBenchScores() {

      try {
        DbHelper.Common.Query($@"
          SELECT sgs.GblQuestionId,
            SUM(sgs.ScoreSumSelfPre) AS ScoreSumSelfPre, SUM(sgs.ScoreCountSelfPre) AS ScoreCountSelfPre,
            SUM(sgs.ScoreSumSelfPost) AS ScoreSumSelfPost, SUM(sgs.ScoreCountSelfPost) AS ScoreCountSelfPost,
            SUM(sgs.ScoreSumRaterPre) AS ScoreSumRaterPre, SUM(sgs.ScoreCountRaterPre) AS ScoreCountRaterPre,
            SUM(sgs.ScoreSumRaterPost) AS ScoreSumRaterPost, SUM(sgs.ScoreCountRaterPost) AS ScoreCountRaterPost
          FROM sv_SurveyGblQnScores sgs WITH (NOLOCK)
          INNER JOIN sv_Survey sv ON sgs.SurveyId = sv.sv_id
          INNER JOIN sv_360_Questions sq ON sgs.GblQuestionId = sq.GblQuestionId
          WHERE sgs.GblAnswerTypeId = @GblAnswerTypeId
            AND sq.SurveyId = @SampleSurveyId
            AND sq.RptQnGroupId = @RptQnGroupId
            AND {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            {(BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org ? "AND (sv.SvCompanyId = @SvCompanyId)" : "")}
          GROUP BY sgs.GblQuestionId",
          dr => {
            (decimal Sum, int Count) selfPre = (dr.GetIntOrNull("ScoreSumSelfPre") ?? 0, dr.GetIntOrNull("ScoreCountSelfPre") ?? 0);
            (decimal Sum, int Count) selfPost = (dr.GetIntOrNull("ScoreSumSelfPost") ?? 0, dr.GetIntOrNull("ScoreCountSelfPost") ?? 0);
            (decimal Sum, int Count) raterPre = (dr.GetIntOrNull("ScoreSumRaterPre") ?? 0, dr.GetIntOrNull("ScoreCountRaterPre") ?? 0);
            (decimal Sum, int Count) raterPost = (dr.GetIntOrNull("ScoreSumRaterPost") ?? 0, dr.GetIntOrNull("ScoreCountRaterPost") ?? 0);
            Questions.Find(q => q.GblQuestionId == dr.GetInt("GblQuestionId"))?.SetBenchScores(
              benchScoreSelfPre: selfPre.Count == 0 ? 0 : Math.Round(selfPre.Sum / selfPre.Count, 1, MidpointRounding.AwayFromZero),
              benchScoreRaterPre: raterPre.Count == 0 ? 0 : Math.Round(raterPre.Sum / raterPre.Count, 1, MidpointRounding.AwayFromZero),
              benchScoreSelfPost: selfPost.Count == 0 ? 0 : Math.Round(selfPost.Sum / selfPost.Count, 1, MidpointRounding.AwayFromZero),
              benchScoreRaterPost: raterPost.Count == 0 ? 0 : Math.Round(raterPost.Sum / raterPost.Count, 1, MidpointRounding.AwayFromZero)
            );
          },
          GetStandardSqlParams()
        );
      } catch (Exception) {
        // Ignore for now.
      }
    }

    // Exposes Questions partitioned by category, in the same order encountered.
    public IEnumerable<CategoryGroup> GroupedQuestions {
      get {
        int iQn = 0;
        while (iQn < Questions.Count) {
          var qnItem = Questions[iQn];
          int? thisHeadingId = qnItem.CategoryHeadingId;
          var group = new CategoryGroup(qnItem.CategoryHeading);
          while (iQn < Questions.Count) {
            qnItem = Questions[iQn];
            if (thisHeadingId != qnItem.CategoryHeadingId) break;
            group.Questions.Add(qnItem);
            iQn++;
          }
          yield return group;
        }
      }
    }

    public class CategoryGroup {
      public string CategoryHeading { get; }
      public List<DbHelper.Reports.PrePost.QuestionInfo> Questions { get; } = new List<DbHelper.Reports.PrePost.QuestionInfo>();
      public CategoryGroup(string categoryHeading) {
        CategoryHeading = categoryHeading;
      }
    }

  }
}
