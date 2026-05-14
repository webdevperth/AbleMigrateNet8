using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class CoacheeReport_Focus : AppCode.PageBaseClasses.CoacheeReportPartialBase {

    public enum SortOptions { SelfScore, RaterScore, Difference }

    private const string UrlSortBy_Self = "self";
    private const string UrlSortBy_Rater = "rater";
    private const string UrlSortBy_Difference = "difference";

    public Dictionary<string, (string DisplayText, SortOptions SortOption)>
      RowSortOptions = new Dictionary<string, (string DisplayText, SortOptions Value)>(StringComparer.OrdinalIgnoreCase) {
        { UrlSortBy_Self, ("Self Score", SortOptions.SelfScore) },
        { UrlSortBy_Rater, ("Rater Score", SortOptions.RaterScore) },
        { UrlSortBy_Difference, ("Difference", SortOptions.Difference) }
      };

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public SortedList<double, QuestionInfo> SortedScores;
    public SortOptions RowSortBy;
    private string QuerySortBy;

    protected void Page_Load(object sender, EventArgs e) {

      QuerySortBy = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SortBy);

      if (QuerySortBy == null || !RowSortOptions.ContainsKey(QuerySortBy)) {
        QuerySortBy = HasRaters ? UrlSortBy_Rater : UrlSortBy_Self; // Default to self sort if no raters.
      }

      RowSortBy = RowSortOptions[QuerySortBy].SortOption;

      ScoreMinValue = 0;

      GetQuestions();
      GetSurveyScores();
      GetBenchScores();
    }

    public string GetSortButtons() {

      var buttons = new List<WebHelper.ButtonGroupButton>();
      foreach (var option in RowSortOptions) {
        if (!HasRaters && option.Value.SortOption != SortOptions.SelfScore) continue; // Only show Self button if no raters.
        buttons.Add(new WebHelper.ButtonGroupButton(option.Value.DisplayText, option.Key));
      }
      return WebHelper.GetButtonGroupButtons(PathHelper.AbleUrlKeys.SortBy, buttons, QuerySortBy);
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
          AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS RaterScore
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
          gqInfo.SortByScore = null;
          if (RowSortBy == SortOptions.SelfScore) {
            gqInfo.SortByScore = gqInfo.SurveySelfScore;
          } else if (RowSortBy == SortOptions.RaterScore) {
            gqInfo.SortByScore = gqInfo.SurveyRaterScore;
          } else if (RowSortBy == SortOptions.Difference) {
            if (gqInfo.SurveyRaterScore.HasValue && gqInfo.SurveySelfScore.HasValue) {
              gqInfo.SortByScore = gqInfo.SurveySelfScore.Value - gqInfo.SurveyRaterScore.Value;
            }
          }
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

    public void ShowQuestions(Action<string, string> categoryStart, Action categoryEnd, Action<QuestionInfo> questionDetail) {

      List<QuestionInfo> questions;

      // "Highest" box.
      if (RowSortBy == SortOptions.Difference) {
        categoryStart("Largest Overestimates", "modeHighest");
        questions = (from q in Questions
                     where q.SortByScore > 0 && q.SortByScore <= ConfigHelper.Reports_FocusMaxDifference
                     orderby q.SortByScore descending
                     select q).Take(ConfigHelper.Reports_FocusTab_RowsPerTable).ToList();
      } else {
        categoryStart("Highest Scoring Statements", "modeHighest");
        questions = (from q in Questions
                     where q.SortByScore != null
                     orderby q.SortByScore descending
                     select q).Take(ConfigHelper.Reports_FocusTab_RowsPerTable).ToList();
      }
      foreach (var question in questions) {
        questionDetail(question);
      }
      categoryEnd();

      // "Lowest" box.
      if (RowSortBy == SortOptions.Difference) {
        categoryStart("Largest Underestimates", "modeLowest");
        questions = (from q in Questions
                     where q.SortByScore < 0 && q.SortByScore >= -ConfigHelper.Reports_FocusMaxDifference
                     orderby q.SortByScore ascending
                     select q).Take(ConfigHelper.Reports_FocusTab_RowsPerTable).ToList();
      } else {
        categoryStart("Lowest Scoring Statements", "modeLowest");
        questions = (from q in Questions
                     where q.SortByScore != null
                     orderby q.SortByScore ascending
                     select q).Take(ConfigHelper.Reports_FocusTab_RowsPerTable).ToList();
      }
      foreach (var question in questions) {
        questionDetail(question);
      }
      categoryEnd();
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
      public decimal? SortByScore { get; set; }
      public decimal? NormSelfScore { get; set; }
      public decimal? NormRaterScore { get; set; }
    }

  }
}
