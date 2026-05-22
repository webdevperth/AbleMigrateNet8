using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class CoacheeReport_Detail : AppCode.PageBaseClasses.CoacheeReportPartialBase {

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public int TableRowCount = 0;

    public IActionResult OnGet() {

      ScoreMinValue = 0;

      GetQuestions();
      GetSurveyScores();
      GetBenchScores();

      return Page();
    }

    void GetQuestions() {

      // First get question text, categories and max scores.
      DbHelper.Common.Query($@"
        SELECT gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gq.GblQuestionId, gq.QuestionTextSelf
        FROM sv_360_Questions sq
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        WHERE sq.SurveyId = @TemplateSurveyId
          AND sq.GblAnswerTypeId = @GblAnswerTypeId
        GROUP BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gq.GblQuestionId, gq.QuestionTextSelf
        ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gq.GblQuestionId",
        dr => {
          int categoryHeadingId = dr.GetInt("RptQnGrpHeadingId");
          var cat = new QuestionInfo() {
            CategoryHeadingId = dr.GetInt("RptQnGrpHeadingId"),
            CategoryHeading = dr.GetString("RptQnGrpHeading"),
            GblQuestionId = dr.GetInt("GblQuestionId"),
            GblQuestionText = dr.GetString("QuestionTextSelf")
          };
          Questions.Add(cat);
        },
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", GblAnswerTypeId),
        DbHelper.Common.NewSqlParameter("TemplateSurveyId", SurveyId)
      );
    }

    void GetSurveyScores() {

      DbHelper.Common.Query($@"
        SELECT sq.GblQuestionId,
          AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS SelfScore,
          AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS RaterScore,
          MIN(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS RaterMinScore,
          MAX(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS RaterMaxScore
        FROM sv_360_Participants sp
        INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        WHERE sp.Completed IS NOT NULL
          AND (sp.PartId = @PartId OR sp.Self_PartId = @PartId)
          AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
        GROUP BY sq.GblQuestionId",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
          if (gqInfo == null) return;
          gqInfo.SurveySelfScore = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero);
          gqInfo.SurveyRaterScore = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero);
          gqInfo.SurveyRaterMinScore = dr.GetDecimalOrNull("RaterMinScore").Round(1, MidpointRounding.AwayFromZero);
          gqInfo.SurveyRaterMaxScore = dr.GetDecimalOrNull("RaterMaxScore").Round(1, MidpointRounding.AwayFromZero);
        },
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", GblAnswerTypeId),
        DbHelper.Common.NewSqlParameter("PartId", ParticipantId)
      );
    }

    void GetBenchScores() {

      try {
        DbHelper.Reports.General.GetNorms_Questions(SurveyId, null, BenchmarkType, qNorm => {
          var gqInfo = Questions.Find(c => c.GblQuestionId == qNorm.GblQuestionId);
          if (gqInfo == null) return;
          gqInfo.NormSelfScore = qNorm.NormResult.SelfNorm;
          gqInfo.NormRaterScore = qNorm.NormResult.RaterNorm;
        });
      } catch (Exception) {
        // Ignore for now.
      }
    }

    // Returns Questions grouped by Category (preserving order).
    public List<CategoryGroup> GetCategoryGroups() {

      var groups = new List<CategoryGroup>();
      int iQn = 0;

      while (iQn < Questions.Count) {

        var qnItem = Questions[iQn];
        int? thisHeadingId = qnItem.CategoryHeadingId;

        var group = new CategoryGroup() { CategoryHeading = qnItem.CategoryHeading };
        groups.Add(group);

        while (iQn < Questions.Count) {
          qnItem = Questions[iQn];
          if (thisHeadingId != qnItem.CategoryHeadingId) break;
          group.Questions.Add(qnItem);
          iQn++;
        }
      }

      return groups;
    }

    public string GetBenchComparisonRowClass(QuestionInfo question) {

      if (question.SurveySelfScore != null && question.NormSelfScore != null && question.SurveySelfScore < question.NormSelfScore) {
        return ""; // was "benchBelow" for red bars, disabled for now (CU-860r0jygu).
      } else {
        return "";
      }
    }

    public class QuestionInfo {
      public int CategoryHeadingId { get; set; }
      public string CategoryHeading { get; set; }
      public int GblQuestionId { get; set; }
      public string GblQuestionText { get; set; }
      public decimal? SurveySelfScore { get; set; }
      public decimal? SurveyRaterScore { get; set; }
      public decimal? SurveyRaterMinScore { get; set; }
      public decimal? SurveyRaterMaxScore { get; set; }
      public decimal? NormSelfScore { get; set; }
      public decimal? NormRaterScore { get; set; }
    }

    public class CategoryGroup {
      public string CategoryHeading { get; set; }
      public List<QuestionInfo> Questions { get; set; } = new List<QuestionInfo>();
    }

  }
}
