using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class CoacheeReport_Categories : AppCode.PageBaseClasses.CoacheeReportPartialBase {

    public List<CategoryInfo> Categories = new List<CategoryInfo>();
    public int TableRowCount = 0;

    public IActionResult OnGet() {

      GetCategories();
      AddQuestionsToCategories();
      GetScores();
      GetBenchScores();

      return Page();
    }

    void GetCategories() {

      DbHelper.Common.Query($@"
        SELECT gh.RptQnGrpHeadingId, gh.RptQnGrpHeading
        FROM sv_360_Questions sq
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        WHERE sq.SurveyId = @SampleSurveyId
          AND sq.GblAnswerTypeId = @GblAnswerTypeId
        GROUP BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading
        ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId",
        dr => {
          var cat = new CategoryInfo() {
            CategoryHeadingId = dr.GetInt("RptQnGrpHeadingId"),
            CategoryHeading = dr.GetString("RptQnGrpHeading"),
          };
          Categories.Add(cat);
        },
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", GblAnswerTypeId),
        DbHelper.Common.NewSqlParameter("SampleSurveyId", SurveyId)
      );
    }

    void AddQuestionsToCategories() {

      DbHelper.Common.Query(@"
        SELECT sq.GblQuestionId, gqh.RptQnGrpHeadingId
        FROM sv_360_Questions sq
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
        WHERE sq.SurveyId = @SampleSurveyId
          AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
        GROUP BY sq.GblQuestionId, gqh.RptQnGrpHeadingId",
        dr => {
          var cat = Categories.Find(c => c.CategoryHeadingId == dr.GetInt("RptQnGrpHeadingId"));
          if (cat == null) return;
          cat.QuestionList.Add(new QuestionInfo(dr.GetInt("GblQuestionId")));
        },
        DbHelper.Common.NewSqlParameter("SampleSurveyId", SurveyId)
      );
    }

    void GetBenchScores() {

      try {
        DbHelper.Reports.General.GetNorms_Questions(SurveyId, null, BenchmarkType, qNorm => {
          // Get category that contains the question and assign the norm result for it.
          var cat = Categories.Find(c => c.QuestionList.Find(q => q.GblQuestionId == qNorm.GblQuestionId) != null);
          if (cat != null) {
            var question = cat.QuestionList.Find(q => q.GblQuestionId == qNorm.GblQuestionId);
            question.NormResult = qNorm.NormResult;
          }
        });
      } catch (Exception) {
        // Ignore for now.
      }

      // Roll up question averages into categories.
      foreach (var cat in Categories) {
        decimal selfTotal = 0, selfCount = 0, raterTotal = 0, raterCount = 0;
        foreach (var question in cat.QuestionList) {
          if (question?.NormResult == null) continue;
          selfTotal += question.NormResult.SelfTotal;
          selfCount += question.NormResult.SelfCount;
          raterTotal += question.NormResult.RaterTotal;
          raterCount += question.NormResult.RaterCount;
        }
        if (selfCount > 0) cat.NormSelfScore = (selfTotal / selfCount).Round(1, MidpointRounding.AwayFromZero);
        if (raterCount > 0) cat.NormRaterScore = (raterTotal / raterCount).Round(1, MidpointRounding.AwayFromZero);
      }
    }

    void GetScores() {

      // Get scores for categories in survey.
      DbHelper.Common.Query($@"
        SELECT gqh.RptQnGrpHeadingId,
          AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS SelfScore,
          AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS RaterScore
        FROM sv_360_Participants sp
        INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = sq.GblQuestionId
        INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        WHERE sp.Completed IS NOT NULL
          AND (sp.PartId = @PartId OR sp.Self_PartId = @PartId)
          AND sq.GblAnswerTypeId = @GblAnswerTypeId
        GROUP BY gqh.RptQnGrpHeadingId",
        dr => {
          int categoryId = dr.GetInt("RptQnGrpHeadingId");
          var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
          if (category == null) return;
          category.SurveySelfScore = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero);
          category.SurveyRaterScore = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero);
        },
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", GblAnswerTypeId),
        DbHelper.Common.NewSqlParameter("PartId", ParticipantId)
      );

      if (PreSurveyPartId > 0) {
        // Pre survey scores.
        DbHelper.Common.Query($@"
          SELECT
            gqh.RptQnGrpHeadingId,
            CAST(AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS AvgSelf,
            CAST(AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS AvgRater
          FROM sv_360_Participants sp
          INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
          INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
          INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
          WHERE (sp.PartId = @PartId OR sp.Self_PartId = @PartId)
            AND sp.Completed IS NOT NULL
            AND sq.GblAnswerTypeId = @GblAnswerTypeId
          GROUP BY gqh.RptQnGrpHeadingId",
          dr => {
            int categoryId = dr.GetInt("RptQnGrpHeadingId");
            var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
            if (category == null) return;
            category.PreSurveySelfScore = dr.GetDecimalOrNull("AvgSelf").Round(1, MidpointRounding.AwayFromZero);
            category.PreSurveyRaterScore = dr.GetDecimalOrNull("AvgRater").Round(1, MidpointRounding.AwayFromZero);
          },
          DbHelper.Common.NewSqlParameter("GblAnswerTypeId", GblAnswerTypeId),
          DbHelper.Common.NewSqlParameter("PartId", PreSurveyPartId)
        );
      }
    }

    public string GetBenchComparisonRowClass(CategoryInfo category) {

      if (category.SurveySelfScore != null && category.NormSelfScore != null && category.SurveySelfScore < category.NormSelfScore) {
        return ""; // was "benchBelow" for red bars, disabled for now (CU-860r0jygu).
      } else {
        return "";
      }
    }

    public string GetBenchComparisonText(CategoryInfo category) {

      if (category.SurveySelfScore == null || category.NormSelfScore == null) {
        return "-";
      } else if (category.SurveySelfScore == category.NormSelfScore) {
        return $"Equal to {NormDisplayName} Norm";
      } else {
        var diff = Math.Abs(category.SurveySelfScore.Value - category.NormSelfScore.Value);
        return $"{diff.ToString("0.0")} point{(diff != 1 ? "s" : "")} {(category.SurveySelfScore > category.NormSelfScore ? "above " : "below ")} {NormDisplayName} Norm";
      }
    }

    public string GetPrePostComparisonText(decimal? surveyScore, decimal? preSurveyScore = null) {

      if (surveyScore == null || preSurveyScore == null) {
        return "";
      } else if (preSurveyScore.Value == surveyScore.Value) {
        return "Equal to Pre Self Score";
      } else {
        var diff = surveyScore.Value - preSurveyScore.Value;
        return $"{Math.Abs(diff).ToString("0.0")} point{(Math.Abs(diff) != 1 ? "s" : "")} {(surveyScore.Value > preSurveyScore.Value ? "above " : "below ")} Pre-Survey";
      }
    }

    public class QuestionInfo {
      public int GblQuestionId;
      public DbHelper.Reports.General.NormResult NormResult { get; set; }
      public QuestionInfo(int gblQuestionId) {
        GblQuestionId = gblQuestionId;
        NormResult = new DbHelper.Reports.General.NormResult(0, 0, 0, 0);
      }
    }

    public class CategoryInfo {

      public int CategoryHeadingId { get; set; }
      public string CategoryHeading { get; set; }
      public decimal? SurveySelfScore { get; set; }
      public decimal? SurveyRaterScore { get; set; }
      public decimal? PreSurveySelfScore { get; set; }
      public decimal? PreSurveyRaterScore { get; set; }
      public decimal? NormSelfScore { get; set; }
      public decimal? NormRaterScore { get; set; }

      public List<QuestionInfo> QuestionList = new List<QuestionInfo>();
    }

  }
}
