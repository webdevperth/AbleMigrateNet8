using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class SurveyViewer_Focus : AppCode.PageBaseClasses.SurveyViewerPartialBase {

    public class QueryKeys {
      public const string RowSortBy = "sort";
    }

    public class Enums {
      public enum RowSortBy { SelfScore, RaterScore }
    }

    public Dictionary<string, (string DisplayText, Enums.RowSortBy OptionEnum)>
      RowSortOptions = new Dictionary<string, (string DisplayText, Enums.RowSortBy Value)>(StringComparer.OrdinalIgnoreCase) {
        { "self", ("Self Score", Enums.RowSortBy.SelfScore) },
        { "rater", ("Rater Score", Enums.RowSortBy.RaterScore) }
      };

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public SortedList<double, QuestionInfo> SortedScores;
    public Enums.RowSortBy QueryRowSortBy;
    public PathHelper.Partials.SurveyViewer_Focus_ShowSection ShowSection;
    public bool ShowScores, ShowParticipantHeadings;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      var defaultSort = HasRaters ? Enums.RowSortBy.RaterScore : Enums.RowSortBy.SelfScore;

      QueryRowSortBy = WebHelper.GetQueryStringEnum(QueryKeys.RowSortBy, defaultSort);

      ShowSection = WebHelper.GetQueryStringEnum(PathHelper.AbleUrlKeys.SurveyViewerFocusPart, PathHelper.Partials.SurveyViewer_Focus_ShowSection.Both);

      ShowScores = !IsViewingParticipantPage;
      ShowParticipantHeadings = IsViewingParticipantPage;

      GetQuestionInfo();
      if (BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org) {
        GetOrgNorms();
      }
      if (SingleSurveyPartId > 0) {
        GetIndividualScores();
      } else {
        GetGroupScores();
      }

      return Page();
    }

    void GetQuestionInfo() {

      // First get question text, categories and max scores.
      DbHelper.Common.Query($@"
        WITH IntakeCodeIds
        AS (
          SELECT Value AS IntakeCodeId
          FROM STRING_SPLIT(@IntakeCodeIds, ',')
        )
        SELECT gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gq.GblQuestionId, gq.QuestionTextSelf, MAX(sc.ValueScore) AS MaxScore
        FROM IntakeCodeIds ic WITH (NOLOCK)
        INNER JOIN sv_360_Codes sic ON sic.CodeId = ic.IntakeCodeId
        INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId AND sit.AnswerTypeDescr = 'date'
        INNER JOIN sv_360_Questions sq ON sq.SurveyId = sit.SurveyId
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.AnswerTypeId = sq.AnswerTypeId
        {HeadingConditionSQL.EnsureStartsWith("WHERE ", true)}
        GROUP BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gq.GblQuestionId, gq.QuestionTextSelf
        HAVING MAX(sc.ValueScore) > 0
        ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gq.GblQuestionId",
        dr => {
          var cat = new QuestionInfo() {
            CategoryHeadingId = dr.GetInt("RptQnGrpHeadingId"),
            CategoryHeading = dr.GetString("RptQnGrpHeading"),
            GblQuestionId = dr.GetInt("GblQuestionId"),
            GblQuestionText = dr.GetString("QuestionTextSelf"),
            MaxScore = (int)(dr.GetDoubleOrNull("MaxScore") ?? 0)
          };
          Questions.Add(cat);
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList()));
    }

    void GetIndividualScores() {

      DbHelper.Common.Query($@"
        SELECT gq.GblQuestionId,
          gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount,
          gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount,
          CAST(AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS SelfScore,
          CAST(AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS RaterScore
        FROM sv_360_Questions sq
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        LEFT OUTER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        LEFT OUTER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        WHERE sq.SurveyId = @SurveyId
          AND (sp.PartId IS NULL OR (sp.Completed IS NOT NULL AND (sp.PartId = @PartId OR sp.Self_PartId = @PartId)))
          {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}
        GROUP BY gq.GblQuestionId, gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount, gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount
        ORDER BY gq.GblQuestionId",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
          if (gqInfo != null) {
            gqInfo.SurveySelfScore = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero);
            gqInfo.SurveyRaterScore = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero);
            gqInfo.SurveySortScore = QueryRowSortBy == Enums.RowSortBy.RaterScore ? gqInfo.SurveyRaterScore : gqInfo.SurveySelfScore;
            int globalSelfTotal = dr.GetIntOrNull("GlobalNormSelfTotal") ?? 0;
            int globalSelfCount = dr.GetIntOrNull("GlobalNormSelfCount") ?? 0;
            if (globalSelfTotal == 0 || globalSelfCount == 0) {
              gqInfo.NormSelfScore = null;
            } else {
              gqInfo.NormSelfScore = ((decimal)globalSelfTotal / globalSelfCount).Round(1, MidpointRounding.AwayFromZero);
            }
            int globalRaterTotal = dr.GetIntOrNull("GlobalNormRaterTotal") ?? 0;
            int globalRaterCount = dr.GetIntOrNull("GlobalNormRaterCount") ?? 0;
            if (globalRaterTotal == 0 || globalRaterCount == 0) {
              gqInfo.NormRaterScore = null;
            } else {
              gqInfo.NormRaterScore = ((decimal)globalRaterTotal / globalRaterCount).Round(1, MidpointRounding.AwayFromZero);
            }

            // Adjust min/max for view. Note Min() ignores nulls which is handy.
            ScoreMinValue = (int)((new decimal?[] { ScoreMinValue, gqInfo.SurveySelfScore, gqInfo.SurveyRaterScore, gqInfo.NormSelfScore, gqInfo.NormRaterScore }).Min() ?? 0);
            ScoreMaxValue = (int)((new decimal?[] { ScoreMaxValue, gqInfo.SurveySelfScore, gqInfo.SurveyRaterScore, gqInfo.NormSelfScore, gqInfo.NormRaterScore }).Max() ?? 0);
          }
        },
        DbHelper.Common.NewSqlParameter("SurveyId", SingleSurveyId),
        DbHelper.Common.NewSqlParameter("PartId", SingleSurveyPartId)
      );
    }

    void GetGroupScores() {
      DbHelper.Common.Query($@"
        WITH IntakeCodeIds
        AS (
          SELECT Value AS IntakeCodeId
          FROM STRING_SPLIT(@IntakeCodeIds, ',')
        )
        SELECT gq.GblQuestionId,
          gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount,
          gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount,
          CAST(AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS DECIMAL(4,1)) AS AvgSelf,
          CAST(AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS DECIMAL(4,1)) AS AvgRater
        FROM IntakeCodeIds ic WITH (NOLOCK)
        INNER JOIN sv_360_Codes sic ON sic.CodeId = ic.IntakeCodeId
        INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId
        INNER JOIN sv_Survey sv ON sv.sv_id = sit.SurveyId
        INNER JOIN sv_360_Questions sq ON sq.SurveyId = sit.SurveyId
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        LEFT OUTER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        LEFT OUTER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        WHERE sit.AnswerTypeDescr = 'date'
          AND (sp.PartId IS NULL OR (sp.Completed IS NOT NULL AND sp.DateGroupCode = sic.Code))
          {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}
        GROUP BY gq.GblQuestionId, gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount, gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount
        ORDER BY gq.GblQuestionId",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
          if (gqInfo != null) {
            gqInfo.SurveySelfScore = dr.GetDecimalOrNull("AvgSelf").Round(1, MidpointRounding.AwayFromZero);
            gqInfo.SurveyRaterScore = dr.GetDecimalOrNull("AvgRater").Round(1, MidpointRounding.AwayFromZero);
            gqInfo.SurveySortScore = QueryRowSortBy == Enums.RowSortBy.RaterScore ? gqInfo.SurveyRaterScore : gqInfo.SurveySelfScore;
            int globalSelfTotal = dr.GetIntOrNull("GlobalNormSelfTotal") ?? 0;
            int globalSelfCount = dr.GetIntOrNull("GlobalNormSelfCount") ?? 0;
            if (globalSelfTotal == 0 || globalSelfCount == 0) {
              gqInfo.NormSelfScore = null;
            } else {
              gqInfo.NormSelfScore = ((decimal)globalSelfTotal / globalSelfCount).Round(1, MidpointRounding.AwayFromZero);
            }
            int globalRaterTotal = dr.GetIntOrNull("GlobalNormRaterTotal") ?? 0;
            int globalRaterCount = dr.GetIntOrNull("GlobalNormRaterCount") ?? 0;
            if (globalRaterTotal == 0 || globalRaterCount == 0) {
              gqInfo.NormRaterScore = null;
            } else {
              gqInfo.NormRaterScore = ((decimal)globalRaterTotal / globalRaterCount).Round(1, MidpointRounding.AwayFromZero);
            }

            // Adjust min/max for view. Note Min() ignores nulls which is handy.
            ScoreMinValue = (int)((new decimal?[] { ScoreMinValue, gqInfo.SurveySelfScore, gqInfo.SurveyRaterScore, gqInfo.NormSelfScore, gqInfo.NormRaterScore }).Min() ?? 0);
            ScoreMaxValue = (int)((new decimal?[] { ScoreMaxValue, gqInfo.SurveySelfScore, gqInfo.SurveyRaterScore, gqInfo.NormSelfScore, gqInfo.NormRaterScore }).Max() ?? 0);
          }
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList())
      );
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
          SELECT gqs.GblQuestionId,
            SUM(gqs.ScoreSum) AS NormSelfTotal, SUM(gqs.ScoreCount) AS NormSelfCount,
            SUM(gqs.ScoreSumRaters) AS NormRaterTotal, SUM(gqs.ScoreCountRaters) AS NormRaterCount
          FROM CompanyIds
          INNER JOIN sv_Survey sv ON CompanyIds.SvCompanyId = sv.SvCompanyId
          INNER JOIN sv_SurveyGblQnScores gqs ON gqs.SurveyId = sv.sv_id
          INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gqs.GblQuestionId
          WHERE gqs.GblAnswerTypeId = @GblAnswerTypeId
            {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}
          GROUP BY gqs.GblQuestionId",
          dr => {
            int gqId = dr.GetInt("GblQuestionId");
            var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
            if (gqInfo != null) {
              int orgSelfTotal = dr.GetIntOrNull("NormSelfTotal") ?? 0;
              int orgSelfCount = dr.GetIntOrNull("NormSelfCount") ?? 0;
              if (orgSelfTotal == 0 || orgSelfCount == 0) {
                gqInfo.NormSelfScore = null;
              } else {
                gqInfo.NormSelfScore = ((decimal)orgSelfTotal / orgSelfCount).Round(1, MidpointRounding.AwayFromZero);
              }
              int orgRaterTotal = dr.GetIntOrNull("NormRaterTotal") ?? 0;
              int orgRaterCount = dr.GetIntOrNull("NormRaterCount") ?? 0;
              if (orgRaterTotal == 0 || orgRaterCount == 0) {
                gqInfo.NormRaterScore = null;
              } else {
                gqInfo.NormRaterScore = ((decimal)orgRaterTotal / orgRaterCount).Round(1, MidpointRounding.AwayFromZero);
              }

              // Adjust min/max for view. Note Min() ignores nulls which is handy.
              ScoreMinValue = (int)((new decimal?[] { ScoreMinValue, gqInfo.SurveySelfScore, gqInfo.SurveyRaterScore, gqInfo.NormSelfScore, gqInfo.NormRaterScore }).Min() ?? 0);
              ScoreMaxValue = (int)((new decimal?[] { ScoreMaxValue, gqInfo.SurveySelfScore, gqInfo.SurveyRaterScore, gqInfo.NormSelfScore, gqInfo.NormRaterScore }).Max() ?? 0);
            }
          },
          DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList()),
          DbHelper.Common.NewSqlParameter("GblAnswerTypeId", ScoringGblAnswerTypeId)
        );
      } catch (Exception) {
        // Ignore for now.
      }
    }

    public string RenderQuestions() {

      var sb = new StringBuilder();

      Action<string, string> categoryStart = (categoryName, className) => {
        sb.Append("<div class=\"boxBorder ").Append(className).Append("\">");
        sb.Append("<div class=\"boxTitle\"><div class=\"catCircle\"></div><h4>").Append(categoryName.HTMLEncode()).Append("</h4></div>");
      };

      Action categoryEnd = () => {
        sb.Append("</div>");
      };

      Action<QuestionInfo> questionDetail = (qnItem) => {
        sb.Append("<div class=\"question ").Append(GetBenchComparisonRowClass(qnItem)).Append("\">");
        sb.Append("<div class=\"qnText\">");
        sb.Append("<div class=\"sectionTitle\">").Append(qnItem.CategoryHeading.HTMLEncode()).Append("</div>");
        sb.Append("<div>").Append(qnItem.GblQuestionText.HTMLEncode()).Append("</div>");
        sb.Append("</div>");
        if (ShowScores) {
          sb.Append("<div class=\"qnBars\"><div class=\"scoreBars\">");
          sb.Append(WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Self, ScoreMinValue, ScoreMaxValue, "Self", qnItem.SurveySelfScore, NormDisplayName, qnItem.NormSelfScore));
          if (RaterCount > 0) {
            sb.Append(WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Rater, ScoreMinValue, ScoreMaxValue, "Rater", qnItem.SurveyRaterScore, NormDisplayName, qnItem.NormRaterScore));
          }
          sb.Append("</div></div>");
        }
        sb.Append("</div>");
      };

      ShowQuestions(categoryStart, categoryEnd, questionDetail);

      return sb.ToString();
    }

    public void ShowQuestions(Action<string, string> categoryStart, Action categoryEnd, Action<QuestionInfo> questionDetail) {

      if (ShowSection != PathHelper.Partials.SurveyViewer_Focus_ShowSection.Lowest) {
        // Highest scoring q's.
        var questions = (from q in Questions where q.SurveySelfScore != null orderby q.SurveySortScore descending select q).Take(ConfigHelper.Reports_FocusTab_RowsPerTable).ToList();
        categoryStart(ShowParticipantHeadings ? "Strengths" : "Highest Scoring Statements", "modeHighest");
        foreach (var question in questions) questionDetail(question);
        categoryEnd();
      }

      if (ShowSection != PathHelper.Partials.SurveyViewer_Focus_ShowSection.Highest) {
        // Lowest scoring q's.
        var questions = (from q in Questions where q.SurveySelfScore != null orderby q.SurveySortScore ascending select q).Take(ConfigHelper.Reports_FocusTab_RowsPerTable).ToList();
        categoryStart(ShowParticipantHeadings ? "Areas For Growth" : "Lowest Scoring Statements", "modeLowest");
        foreach (var question in questions) questionDetail(question);
        categoryEnd();
      }
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
      public decimal? SurveySortScore { get; set; }
      public decimal? NormSelfScore { get; set; }
      public decimal? NormRaterScore { get; set; }
      public int MaxScore { get; set; }
    }

  }
}
