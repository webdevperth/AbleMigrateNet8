using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class SkillsViewer_Categories : AppCode.PageBaseClasses.SkillsViewerPartialBase {

    // Expose protected base members as public for Razor view access.
    public new DbHelper.Reports.SkillsViewer.SurveyStats SurveyStats => base.SurveyStats;
    public new string BenchmarkDisplayName => base.BenchmarkDisplayName;
    public new DbHelper.Reports.NormEnum BenchmarkType => base.BenchmarkType;

    public List<CategoryInfo> Categories = new List<CategoryInfo>();
    public int TableRowCount = 0;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      WebHelper.QueryStringToEnum(PathHelper.AbleUrlKeys.PrePostScope, out PathHelper.PrePostScopeEnum prePostScope, PathHelper.PrePostScopeEnum.None);

      GetCategories();
      AddQuestionsToCategories();
      GetProjectScores(prePostScope);
      GetBenchScores();

      return Page();
    }

    void GetCategories() {

      DbHelper.Common.Query(@"
        SELECT gh.RptQnGrpHeadingId, gh.RptQnGrpHeading
        FROM sv_360_Questions sq
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gqh.RptQnGrpHeadingId = gh.RptQnGrpHeadingId
        WHERE sq.SurveyId = @SampleSurveyId
          AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
        GROUP BY gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gh.RptQnGrpHeadingSort
        ORDER BY gh.RptQnGrpHeadingSort",
        dr => {
          Categories.Add(new CategoryInfo(dr.GetInt("RptQnGrpHeadingId"), dr.GetString("RptQnGrpHeading")));
        },
        GetStandardSqlParams()
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
        GetStandardSqlParams()
      );
    }

    void GetProjectScores(PathHelper.PrePostScopeEnum prePostScope) {

      string prePostScopeColumn;
      switch (prePostScope) {
        case PathHelper.PrePostScopeEnum.User:
          prePostScopeColumn = "PrePost360ForUser";
          break;
        case PathHelper.PrePostScopeEnum.Coachee:
          prePostScopeColumn = "PrePost360ForCoachee";
          break;
        default:
          prePostScopeColumn = "NULL"; // Guaranteed to not return prepost values.
          break;
      }

      DbHelper.Common.Query($@"
        WITH
          {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Create ProgramJobIds CTE only if there is a ProgramJobId selection.
          ? "ProgramJobIds AS (SELECT Value AS ProgramJobId FROM STRING_SPLIT(@ProgramJobIds, ',')),"
          : "")}
        results
        AS (
          SELECT gqh.RptQnGrpHeadingId, sp.IsSelf, sp.PrePost360ForCoachee, sp.PrePost360ForUser,
            SUM(sc.ValueScore) AS ScoreSum,
            COUNT(sc.ValueScore) AS ScoreCount,
            COUNT(DISTINCT sp.PartId) AS PartCount
          FROM sv_Answers sa
          INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
          INNER JOIN sv_360_Participants sp ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_360_Questions sq ON sa.QuestionId = sq.QuestionId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
          {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Join to ProgramJobIds CTE if included.
            ? "INNER JOIN ProgramJobIds pjs ON pjs.ProgramJobId = sp.AbleProgramJobId"
            : "")}
          INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
          WHERE {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            AND sp.AbleProjectId = @ProjectId
            AND sp.Completed IS NOT NULL
            AND sq.RptQnGroupId = @RptQnGroupId
          GROUP BY gqh.RptQnGrpHeadingId, sp.IsSelf, sp.PrePost360ForCoachee, sp.PrePost360ForUser
        )
        SELECT
          RptQnGrpHeadingId,
          SUM(IIF(IsSelf = 1, PartCount, 0)) AS SelfCount,
          SUM(IIF(IsSelf = 0, PartCount, 0)) AS RaterCount,
          SUM(IIF(IsSelf = 1 AND {prePostScopeColumn} = 1, PartCount, 0)) AS SelfPreCount,
          SUM(IIF(IsSelf = 0 AND {prePostScopeColumn} = 1, PartCount, 0)) AS RaterPreCount,
          SUM(IIF(IsSelf = 1 AND {prePostScopeColumn} = 2, PartCount, 0)) AS SelfPostCount,
          SUM(IIF(IsSelf = 0 AND {prePostScopeColumn} = 2, PartCount, 0)) AS RaterPostCount,
          SUM(IIF(IsSelf = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1, ScoreCount, NULL)) AS SelfAllScore,
          SUM(IIF(IsSelf = 0, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0, ScoreCount, NULL)) AS RaterAllScore,
          SUM(IIF(IsSelf = 1 AND {prePostScopeColumn} = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1 AND PrePost360ForCoachee = 1, ScoreCount, NULL)) AS SelfPreScore,
          SUM(IIF(IsSelf = 0 AND {prePostScopeColumn} = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0 AND PrePost360ForCoachee = 1, ScoreCount, NULL)) AS RaterPreScore,
          SUM(IIF(IsSelf = 1 AND {prePostScopeColumn} = 2, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1 AND PrePost360ForCoachee = 2, ScoreCount, NULL)) AS SelfPostScore,
          SUM(IIF(IsSelf = 0 AND {prePostScopeColumn} = 2, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0 AND PrePost360ForCoachee = 2, ScoreCount, NULL)) AS RaterPostScore
        FROM results
        GROUP BY RptQnGrpHeadingId;",
        dr => {
          int categoryId = dr.GetInt("RptQnGrpHeadingId");
          var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
          if (category == null) return;
          category.SetScores(
            selfAllScore: dr.GetDecimalOrNull("SelfAllScore"),
            raterAllScore: dr.GetDecimalOrNull("RaterAllScore"),
            selfPreScore: dr.GetDecimalOrNull("SelfPreScore"),
            raterPreScore: dr.GetDecimalOrNull("RaterPreScore"),
            selfPostScore: dr.GetDecimalOrNull("SelfPostScore"),
            raterPostScore: dr.GetDecimalOrNull("RaterPostScore")
          );
        },
        GetStandardSqlParams()
      );
    }

    void GetBenchScores() {

      try {
        DbHelper.Reports.General.GetNorms_Questions(SurveyStats.SampleSurveyId, SurveyStats.RptQnGroupId, BenchmarkType, qNorm => {
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
        if (selfCount > 0) cat.SelfBenchScore = (selfTotal / selfCount).Round(1, MidpointRounding.AwayFromZero);
        if (raterCount > 0) cat.RaterBenchScore = (raterTotal / raterCount).Round(1, MidpointRounding.AwayFromZero);
      }
    }

    public string GetBenchComparisonRowClass(CategoryInfo category) {

      if (category.SelfAllScore != null && category.SelfBenchScore != null && category.SelfAllScore < category.SelfBenchScore) {
        return ""; // was "benchBelow" for red bars, disabled for now (CU-860r0jygu).
      } else {
        return "";
      }
    }

    public string GetBenchComparisonText(CategoryInfo category) {

      if (category.SelfAllScore == null || category.SelfBenchScore == null) {
        return "-";
      } else if (category.SelfAllScore == category.SelfBenchScore) {
        return $"Equal to {BenchmarkDisplayName} Norm";
      } else {
        var diff = Math.Abs(category.SelfAllScore.Value - category.SelfBenchScore.Value);
        return $"{diff.ToString("0.0")} point{(diff != 1 ? "s" : "")} {(category.SelfAllScore > category.SelfBenchScore ? "above " : "below ")} {BenchmarkDisplayName} Norm";
      }
    }

    public string GetPrePostComparisonText(decimal? postSurveyScore, decimal? preSurveyScore = null) {

      if (postSurveyScore == null || preSurveyScore == null) {
        return "";
      } else if (preSurveyScore.Value == postSurveyScore.Value) {
        return "Equal to Pre Self Score";
      } else {
        var diff = postSurveyScore.Value - preSurveyScore.Value;
        return $"{Math.Abs(diff).ToString("0.0")} point{(Math.Abs(diff) != 1 ? "s" : "")} {(postSurveyScore.Value > preSurveyScore.Value ? "above " : "below ")} Pre-Survey";
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

      public int CategoryHeadingId { get; private set; }
      public string CategoryHeading { get; private set; }
      public decimal? SelfBenchScore { get; set; }
      public decimal? RaterBenchScore { get; set; }
      public decimal? SelfAllScore { get; private set; }
      public decimal? RaterAllScore { get; private set; }
      public decimal? SelfPreScore { get; private set; }
      public decimal? RaterPreScore { get; private set; }
      public decimal? SelfPostScore { get; private set; }
      public decimal? RaterPostScore { get; private set; }

      public List<QuestionInfo> QuestionList = new List<QuestionInfo>();

      public CategoryInfo(int categoryHeadingId, string categoryHeading) {
        CategoryHeadingId = categoryHeadingId;
        CategoryHeading = categoryHeading;
      }

      public void SetBenchScores(
        decimal? selfBenchScore,
        decimal? raterBenchScore) {
        SelfBenchScore = selfBenchScore;
        RaterBenchScore = raterBenchScore;
      }

      public void SetScores(
        decimal? selfAllScore,
        decimal? raterAllScore,
        decimal? selfPreScore,
        decimal? raterPreScore,
        decimal? selfPostScore,
        decimal? raterPostScore) {
        SelfAllScore = selfAllScore;
        RaterAllScore = raterAllScore;
        SelfPreScore = selfPreScore;
        RaterPreScore = raterPreScore;
        SelfPostScore = selfPostScore;
        RaterPostScore = raterPostScore;
      }
    }

  }
}
