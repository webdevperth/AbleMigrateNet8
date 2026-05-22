using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class SurveyViewer_Categories : AppCode.PageBaseClasses.SurveyViewerPartialBase {

    public List<CategoryInfo> Categories = new List<CategoryInfo>();
    public int TableRowCount = 0;
    public bool HideComparisonColumn;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      HideComparisonColumn = IsViewingParticipantPage;

      GetCategoryScores();

      if (BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org) {
        GetOrgNorms();
      }

      return Page();
    }

    void GetCategoryScores() {

      // First get max scores for each category.
      DbHelper.Common.Query($@"
        WITH IntakeCodeIds
        AS (
          SELECT value AS IntakeCodeId
          FROM STRING_SPLIT(@IntakeCodeIds, ',')
        )
        SELECT gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, MAX(sc.ValueScore) AS MaxScore
        FROM IntakeCodeIds ic WITH (NOLOCK)
        INNER JOIN sv_360_Codes sic ON sic.CodeId = ic.IntakeCodeId
        INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId AND sit.AnswerTypeDescr = 'date'
        INNER JOIN sv_360_Questions sq ON sq.SurveyId = sit.SurveyId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.AnswerTypeId = sq.AnswerTypeId
        {HeadingConditionSQL.EnsureStartsWith("WHERE ", true)}
        GROUP BY gh.RptQnGrpHeading, gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId
        ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId",
        dr => {
          int maxScore = (int)(dr.GetDoubleOrNull("MaxScore") ?? 0);
          if (maxScore > 0) {
            var cat = new CategoryInfo() {
              CategoryHeadingId = dr.GetInt("RptQnGrpHeadingId"),
              CategoryHeading = dr.GetString("RptQnGrpHeading"),
              CategoryMaxScore = maxScore
            };
            Categories.Add(cat);
          }
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList())
      );

      if (SingleSurveyPartId > 0) {
        GetIndividualScores();
      } else {
        GetGroupScores();
      }
    }

    void GetIndividualScores() {

      // Get scores for categories in survey.
      DbHelper.Common.Query($@"
        SELECT gh.RptQnGrpHeadingId,
          SUM(gq.GlobalNormSelfTotal) AS GlobalNormSelfTotal,
          SUM(gq.GlobalNormSelfCount) AS GlobalNormSelfCount,
          SUM(gq.GlobalNormRaterTotal) AS GlobalNormRaterTotal,
          SUM(gq.GlobalNormRaterCount) AS GlobalNormRaterCount,
          CAST(AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS SelfScore,
          CAST(AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS RaterScore
        FROM sv_360_Questions sq
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        LEFT OUTER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        LEFT OUTER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        WHERE sq.SurveyId = @SurveyId
          AND (sp.PartId IS NULL
            OR (sp.Completed IS NOT NULL
            AND (sp.PartId = @PartId OR sp.Self_PartId = @PartId)))
        GROUP BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId
        ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId",
        dr => {
          int categoryId = dr.GetInt("RptQnGrpHeadingId");
          var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
          if (category != null) {
            category.SurveySelfScore = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero);
            category.SurveyRaterScore = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero);
            int globalSelfTotal = dr.GetIntOrNull("GlobalNormSelfTotal") ?? 0;
            int globalSelfCount = dr.GetIntOrNull("GlobalNormSelfCount") ?? 0;
            if (globalSelfTotal == 0 || globalSelfCount == 0) {
              category.NormSelfScore = null;
            } else {
              category.NormSelfScore = ((decimal)globalSelfTotal / globalSelfCount).Round(1, MidpointRounding.AwayFromZero);
            }
            int globalRaterTotal = dr.GetIntOrNull("GlobalNormRaterTotal") ?? 0;
            int globalRaterCount = dr.GetIntOrNull("GlobalNormRaterCount") ?? 0;
            if (globalRaterTotal == 0 || globalRaterCount == 0) {
              category.NormRaterScore = null;
            } else {
              category.NormRaterScore = ((decimal)globalRaterTotal / globalRaterCount).Round(1, MidpointRounding.AwayFromZero);
            }
          }
        },
        DbHelper.Common.NewSqlParameter("SurveyId", SingleSurveyId),
        DbHelper.Common.NewSqlParameter("PartId", SingleSurveyPartId)
      );

      if (SinglePreSurveyPartId > 0) {
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
          GROUP BY gqh.RptQnGrpHeadingId",
          dr => {
            int categoryId = dr.GetInt("RptQnGrpHeadingId");
            var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
            if (category != null) {
              category.PreSurveySelfScore = dr.GetDecimalOrNull("AvgSelf").Round(1, MidpointRounding.AwayFromZero);
              category.PreSurveyRaterScore = dr.GetDecimalOrNull("AvgRater").Round(1, MidpointRounding.AwayFromZero);
            }
          },
          DbHelper.Common.NewSqlParameter("PartId", SinglePreSurveyPartId)
        );
      }
    }

    void GetGroupScores() {

      // Get scores for categories in survey.
      DbHelper.Common.Query($@"
        WITH IntakeCodeIds
        AS (
          SELECT Value AS IntakeCodeId
          FROM STRING_SPLIT(@IntakeCodeIds, ',')
        )
        SELECT
          gh.RptQnGrpHeadingId,
          SUM(gq.GlobalNormSelfTotal) AS GlobalNormSelfTotal,
          SUM(gq.GlobalNormSelfCount) AS GlobalNormSelfCount,
          SUM(gq.GlobalNormRaterTotal) AS GlobalNormRaterTotal,
          SUM(gq.GlobalNormRaterCount) AS GlobalNormRaterCount,
          CAST(AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS AvgSelf,
          CAST(AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS AvgRater
        FROM IntakeCodeIds ic WITH (NOLOCK)
        INNER JOIN sv_360_Codes sic ON sic.CodeId = ic.IntakeCodeId
        INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId
        INNER JOIN sv_360_Questions sq ON sq.SurveyId = sit.SurveyId
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        LEFT OUTER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        LEFT OUTER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        WHERE sit.AnswerTypeDescr = 'date'
          AND (sp.PartId IS NULL OR (sp.Completed IS NOT NULL AND sp.DateGroupCode = sic.Code))
        GROUP BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId
        ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId",
        dr => {
          int categoryId = dr.GetInt("RptQnGrpHeadingId");
          var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
          if (category != null) {
            category.SurveySelfScore = dr.GetDecimalOrNull("AvgSelf").Round(1, MidpointRounding.AwayFromZero);
            category.SurveyRaterScore = dr.GetDecimalOrNull("AvgRater").Round(1, MidpointRounding.AwayFromZero);
            int globalSelfTotal = dr.GetIntOrNull("GlobalNormSelfTotal") ?? 0;
            int globalSelfCount = dr.GetIntOrNull("GlobalNormSelfCount") ?? 0;
            if (globalSelfTotal == 0 || globalSelfCount == 0) {
              category.NormSelfScore = null;
            } else {
              category.NormSelfScore = ((decimal)globalSelfTotal / globalSelfCount).Round(1, MidpointRounding.AwayFromZero);
            }
            int globalRaterTotal = dr.GetIntOrNull("GlobalNormRaterTotal") ?? 0;
            int globalRaterCount = dr.GetIntOrNull("GlobalNormRaterCount") ?? 0;
            if (globalRaterTotal == 0 || globalRaterCount == 0) {
              category.NormRaterScore = null;
            } else {
              category.NormRaterScore = ((decimal)globalRaterTotal / globalRaterCount).Round(1, MidpointRounding.AwayFromZero);
            }
          }
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList())
      );

      if (!PreIntakeCodeIdList.IsNullOrEmpty() && SinglePreSurveyPartId > 0) {
        // Pre survey scores.
        DbHelper.Common.Query($@"
          WITH IntakeCodeIds
          AS (
            SELECT Value AS IntakeCodeId
            FROM STRING_SPLIT(@IntakeCodeIds, ',')
          )
          SELECT
            gh.RptQnGrpHeadingId,
            CAST(AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS AvgSelf,
            CAST(AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS AvgRater
          FROM IntakeCodeIds ic WITH (NOLOCK)
          INNER JOIN sv_360_Codes sic ON sic.CodeId = ic.IntakeCodeId
          INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId
          INNER JOIN sv_360_Questions sq ON sq.SurveyId = sit.SurveyId
          INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
          INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
          LEFT OUTER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
          LEFT OUTER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
          LEFT OUTER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
          WHERE sit.AnswerTypeDescr = 'date'
            AND (sp.PartId IS NULL OR (sp.Completed IS NOT NULL AND sp.DateGroupCode = sic.Code))
          GROUP BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId
          ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId",
          dr => {
            int categoryId = dr.GetInt("RptQnGrpHeadingId");
            var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
            if (category != null) {
              category.PreSurveySelfScore = dr.GetDecimalOrNull("AvgSelf").Round(1, MidpointRounding.AwayFromZero);
              category.PreSurveyRaterScore = dr.GetDecimalOrNull("AvgRater").Round(1, MidpointRounding.AwayFromZero);
            }
          },
          DbHelper.Common.NewSqlParameter("IntakeCodeIds", PreIntakeCodeIdList.ToStringList())
        );
      }
    }

    void GetOrgNorms() {

      // Org scores for questions in selected intakes.
      try {
        DbHelper.Common.Query($@"
          WITH IntakeCodeIds
          AS (
            SELECT Value AS IntakeCodeId
            FROM STRING_SPLIT(@IntakeCodeIds, ',')
          ),
          CompanyIds
          AS (
            SELECT DISTINCT (sv.SvCompanyId) AS SvCompanyId
            FROM sv_360_Codes sic WITH (NOLOCK)
            INNER JOIN IntakeCodeIds ic ON sic.CodeId = ic.IntakeCodeId
            INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId
            INNER JOIN sv_Survey sv ON sv.sv_id = sit.SurveyId
          )
          SELECT gqh.RptQnGrpHeadingId,
            SUM(gqs.ScoreSum) AS NormSelfTotal, SUM(gqs.ScoreCount) AS NormSelfCount,
            SUM(gqs.ScoreSumRaters) AS NormRaterTotal, SUM(gqs.ScoreCountRaters) AS NormRaterCount
          FROM CompanyIds
          INNER JOIN sv_Survey sv ON CompanyIds.SvCompanyId = sv.SvCompanyId
          INNER JOIN sv_SurveyGblQnScores gqs ON gqs.SurveyId = sv.sv_id
          INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gqs.GblQuestionId
          WHERE gqs.GblAnswerTypeId = @GblAnswerTypeId
          GROUP BY gqh.RptQnGrpHeadingId",
          dr => {
            int categoryId = dr.GetInt("RptQnGrpHeadingId");
            var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
            if (category != null) {
              int orgSelfTotal = dr.GetIntOrNull("NormSelfTotal") ?? 0;
              int orgSelfCount = dr.GetIntOrNull("NormSelfCount") ?? 0;
              if (orgSelfTotal == 0 || orgSelfCount == 0) {
                category.NormSelfScore = null;
              } else {
                category.NormSelfScore = ((decimal)orgSelfTotal / orgSelfCount).Round(1, MidpointRounding.AwayFromZero);
              }
              int orgRaterTotal = dr.GetIntOrNull("NormRaterTotal") ?? 0;
              int orgRaterCount = dr.GetIntOrNull("NormRaterCount") ?? 0;
              if (orgRaterTotal == 0 || orgRaterCount == 0) {
                category.NormRaterScore = null;
              } else {
                category.NormRaterScore = ((decimal)orgRaterTotal / orgRaterCount).Round(1, MidpointRounding.AwayFromZero);
              }
            }
          },
          DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList()),
          DbHelper.Common.NewSqlParameter("GblAnswerTypeId", ScoringGblAnswerTypeId)
        );
      } catch (Exception) {
        // Ignore for now.
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

    public class CategoryInfo {
      public int CategoryHeadingId { get; set; }
      public string CategoryHeading { get; set; }
      public decimal? SurveySelfScore { get; set; }
      public decimal? SurveyRaterScore { get; set; }
      public decimal? PreSurveySelfScore { get; set; }
      public decimal? PreSurveyRaterScore { get; set; }
      public decimal? NormSelfScore { get; set; }
      public decimal? NormRaterScore { get; set; }
      public int CategoryMaxScore { get; set; }
    }

  }
}
