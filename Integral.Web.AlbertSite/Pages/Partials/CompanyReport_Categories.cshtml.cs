using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class CompanyReport_Categories : AppCode.PageBaseClasses.CompanyReportPartialBase {

    // Expose protected base members for Razor access.
    public new DbHelper.Reports.Company.SurveyStats SurveyStats => base.SurveyStats;
    public new PathHelper.SurveyViewerBenchmarkEnum BenchmarkType => base.BenchmarkType;
    public new string BenchmarkDisplayName => base.BenchmarkDisplayName;

    public List<CategoryInfo> Categories = new List<CategoryInfo>();
    public int TableRowCount = 0;

    public IActionResult OnGet() {
      GetCategories();
      GetCompanyScores();
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
        WHERE sv.sv_id = @SampleSurveyId
          AND sq.GblAnswerTypeId = @GblAnswerTypeId
        GROUP BY gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gh.RptQnGrpHeadingSort
        ORDER BY gh.RptQnGrpHeadingSort",
        dr => {
          Categories.Add(new CategoryInfo(dr.GetInt("RptQnGrpHeadingId"), dr.GetString("RptQnGrpHeading")));
        },
        GetStandardSqlParams()
      );
    }

    void GetCompanyScores() {

      DbHelper.Common.Query($@"
        WITH results
        AS (
          SELECT gqh.RptQnGrpHeadingId, sp.IsSelf, sp.IsSelfPreSurvey, sp.IsSelfPostSurvey,
            SUM(sc.ValueScore) AS ScoreSum,
            COUNT(sc.ValueScore) AS ScoreCount,
            COUNT(DISTINCT sp.PartId) AS PartCount
          FROM sv_Answers sa WITH (NOLOCK)
          INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
          INNER JOIN sv_360_Participants sp ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_360_Questions sq ON sa.QuestionId = sq.QuestionId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
          INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
          WHERE sp.AbleSvCompanyId = @SvCompanyId
            AND sp.Completed IS NOT NULL
            AND sv.SurveyTypeCode = @SurveyTypeCode
            AND sq.GblAnswerTypeId = @GblAnswerTypeId
          GROUP BY gqh.RptQnGrpHeadingId, sp.IsSelf, sp.IsSelfPreSurvey, sp.IsSelfPostSurvey
        )
        SELECT
          RptQnGrpHeadingId,
          SUM(IIF(IsSelf = 1, PartCount, 0)) AS SelfCount,
          SUM(IIF(IsSelf = 0, PartCount, 0)) AS RaterCount,
          SUM(IIF(IsSelf = 1 AND IsSelfPreSurvey = 1, PartCount, 0)) AS SelfPreCount,
          SUM(IIF(IsSelf = 0 AND IsSelfPreSurvey = 1, PartCount, 0)) AS RaterPreCount,
          SUM(IIF(IsSelf = 1 AND IsSelfPostSurvey = 1, PartCount, 0)) AS SelfPostCount,
          SUM(IIF(IsSelf = 0 AND IsSelfPostSurvey = 1, PartCount, 0)) AS RaterPostCount,
          SUM(IIF(IsSelf = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1, ScoreCount, NULL)) AS SelfAllScore,
          SUM(IIF(IsSelf = 0, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0, ScoreCount, NULL)) AS RaterAllScore,
          SUM(IIF(IsSelf = 1 AND IsSelfPreSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1 AND IsSelfPreSurvey = 1, ScoreCount, NULL)) AS SelfPreScore,
          SUM(IIF(IsSelf = 0 AND IsSelfPreSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0 AND IsSelfPreSurvey = 1, ScoreCount, NULL)) AS RaterPreScore,
          SUM(IIF(IsSelf = 1 AND IsSelfPostSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 1 AND IsSelfPostSurvey = 1, ScoreCount, NULL)) AS SelfPostScore,
          SUM(IIF(IsSelf = 0 AND IsSelfPostSurvey = 1, ScoreSum, NULL)) / SUM(IIF(IsSelf = 0 AND IsSelfPostSurvey = 1, ScoreCount, NULL)) AS RaterPostScore
        FROM results
        GROUP BY RptQnGrpHeadingId;",
        dr => {
          int categoryId = dr.GetInt("RptQnGrpHeadingId");
          var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
          if (category != null) {
            category.SetScores(
              selfAllScore: dr.GetDecimalOrNull("SelfAllScore").Round(1, MidpointRounding.AwayFromZero),
              raterAllScore: dr.GetDecimalOrNull("RaterAllScore").Round(1, MidpointRounding.AwayFromZero),
              selfPreScore: dr.GetDecimalOrNull("SelfPreScore").Round(1, MidpointRounding.AwayFromZero),
              raterPreScore: dr.GetDecimalOrNull("RaterPreScore").Round(1, MidpointRounding.AwayFromZero),
              selfPostScore: dr.GetDecimalOrNull("SelfPostScore").Round(1, MidpointRounding.AwayFromZero),
              raterPostScore: dr.GetDecimalOrNull("RaterPostScore").Round(1, MidpointRounding.AwayFromZero)
            );
          }
        },
        GetStandardSqlParams()
      );
    }

    void GetBenchScores() {

      try {
        DbHelper.Common.Query($@"
          SELECT gqh.RptQnGrpHeadingId,
            SUM(sgs.ScoreSum) AS ScoreSumSelfs, SUM(sgs.ScoreCount) AS ScoreCountSelfs,
            SUM(sgs.ScoreSumRaters) AS ScoreSumRaters, SUM(sgs.ScoreCountRaters) AS ScoreCountRaters
          FROM sv_SurveyGblQnScores sgs WITH (NOLOCK)
          INNER JOIN sv_Survey sv ON sgs.SurveyId = sv.sv_id
          INNER JOIN al_RptQnGrpHgGblQns gqh ON sgs.GblQuestionId = gqh.GblQuestionId
          INNER JOIN sv_360_Questions sq ON sgs.GblQuestionId = sq.GblQuestionId
          WHERE sgs.GblAnswerTypeId = @GblAnswerTypeId
            AND sq.SurveyId = @SampleSurveyId
            AND sv.SurveyTypeCode = @SurveyTypeCode
            {(BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org ? "AND sv.SvCompanyId = @SvCompanyId" : "")}
          GROUP BY gqh.RptQnGrpHeadingId",
          dr => {
            int categoryId = dr.GetInt("RptQnGrpHeadingId");
            var category = Categories.Find(c => c.CategoryHeadingId == categoryId);
            if (category != null) {
              var scoreSumSelfs = dr.GetDecimal("ScoreSumSelfs", 0);
              var scoreCountSelfs = dr.GetDecimal("ScoreCountSelfs", 0);
              var scoreSumRaters = dr.GetDecimal("ScoreSumRaters", 0);
              var scoreCountRaters = dr.GetDecimal("ScoreCountRaters", 0);
              category.SetBenchScores(
                selfBenchScore: scoreCountSelfs == 0 ? null : (decimal?)(scoreSumSelfs / scoreCountSelfs).Round(1, MidpointRounding.AwayFromZero),
                raterBenchScore: scoreCountRaters == 0 ? null : (decimal?)(scoreSumRaters / scoreCountRaters).Round(1, MidpointRounding.AwayFromZero)
              );
            }
          },
          GetStandardSqlParams()
        );
      } catch (Exception) {
        // Ignore for now.
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

    public class CategoryInfo {

      public int CategoryHeadingId { get; private set; }
      public string CategoryHeading { get; private set; }
      public decimal? SelfBenchScore { get; private set; }
      public decimal? RaterBenchScore { get; private set; }
      public decimal? SelfAllScore { get; private set; }
      public decimal? RaterAllScore { get; private set; }
      public decimal? SelfPreScore { get; private set; }
      public decimal? RaterPreScore { get; private set; }
      public decimal? SelfPostScore { get; private set; }
      public decimal? RaterPostScore { get; private set; }

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
