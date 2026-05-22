using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class SurveyViewer_PrePost : AppCode.PageBaseClasses.SurveyViewerPartialBase {

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public int TableRowCount = 0;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      GetQuestionScores();

      if (SinglePreSurveyPartId > 0) {
        GetPreSurveyScores();
        GetPrePostNorms();
      }

      return Page();
    }

    void GetPrePostNorms() {

      // Averages for pre and post surveys in current project.
      try {
        DbHelper.Common.Query(@"
          WITH cp AS (
            SELECT sv.sv_ReportType, ac.ProgramJobId, ij.JobNumber
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
            INNER JOIN al_Coachees ac ON ac.CoacheeId = sp.AbleCoacheeId
            INNER JOIN id_Job ij ON ij.JobId = ac.ProgramJobId
            WHERE sp.PartId = @PartId
          )
          SELECT
            sq.GblQuestionId, spSelf.IsSelfPreSurvey, spSelf.IsSelfPostSurvey, sp.IsSelf,
            CAST(AVG(sc.ValueScore) AS DECIMAL(4,1)) AS AvgScore
          FROM id_Job ij
          INNER JOIN cp ON ij.JobNumber = cp.JobNumber
          INNER JOIN al_Coachees ac ON ac.ProgramJobId = ij.JobId
          INNER JOIN sv_360_Participants spSelf ON spSelf.AbleCoacheeId = ac.CoacheeId
          INNER JOIN sv_360_Participants sp ON sp.PartId = spSelf.PartId OR sp.Self_PartId = spSelf.PartId
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
          INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
          WHERE sv.sv_ReportType = cp.sv_ReportType
            AND sq.GblAnswerTypeId = @GblAnswerTypeId
            AND sq.RptQnGroupId = @RptQnGroupId
            AND (spSelf.IsSelfPreSurvey = 1 OR spSelf.IsSelfPostSurvey = 1)
            AND sp.Completed IS NOT NULL
            AND ij.JobId <> 9558 -- test program
          GROUP BY sq.GblQuestionId, spSelf.IsSelfPreSurvey, spSelf.IsSelfPostSurvey, sp.IsSelf
          ORDER BY sq.GblQuestionId, sp.IsSelf;",
          dr => {
            int gqId = dr.GetInt("GblQuestionId");
            var avgScore = dr.GetDecimalOrNull("AvgScore");
            if (avgScore != null) {
              avgScore = avgScore.Value.Round(1, MidpointRounding.AwayFromZero);
              var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
              if (gqInfo != null) {
                if (dr.GetInt("IsSelfPreSurvey") == 1) {
                  if (dr.GetInt("IsSelf") == 1) {
                    gqInfo.PreNormSelfScore = avgScore.Value;
                  } else {
                    gqInfo.PreNormRaterScore = avgScore.Value;
                  }
                } else {
                  if (dr.GetInt("IsSelf") == 1) {
                    gqInfo.NormSelfScore = avgScore.Value;
                  } else {
                    gqInfo.NormRaterScore = avgScore.Value;
                  }
                }
              }
            }
          },
          DbHelper.Common.NewSqlParameter("PartId", SingleSurveyPartId),
          DbHelper.Common.NewSqlParameter("GblAnswerTypeId", ScoringGblAnswerTypeId),
          DbHelper.Common.NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_SkillsViewer)
        );
      } catch (Exception) {
        // Ignore for now.
      }
    }

    void GetQuestionScores() {

      // First get question text, categories and max scores.
      DbHelper.Common.Query(@"
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
        WHERE sq.RptQnGroupId = @RptQnGroupId
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
        DbHelper.Common.NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_SkillsViewer),
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList()));

      // Get self score for questions in survey.
      DbHelper.Common.Query(@"
        SELECT gq.GblQuestionId,
          gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount,
          gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount,
          CAST(AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS SelfScore,
          CAST(AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS RaterScore
        FROM sv_360_Questions sq
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        LEFT OUTER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        LEFT OUTER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        WHERE sq.SurveyId = @SurveyId
          AND (sp.PartId IS NULL
            OR (sp.Completed IS NOT NULL
            AND (sp.PartId = @PartId OR sp.Self_PartId = @PartId)))
          AND sq.RptQnGroupId = @RptQnGroupId
        GROUP BY gq.GblQuestionId, gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount, gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount
        ORDER BY gq.GblQuestionId",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
          if (gqInfo != null) {
            gqInfo.SurveySelfScore = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero);
            gqInfo.SurveyRaterScore = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero);
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
          }
        },
        DbHelper.Common.NewSqlParameter("SurveyId", SingleSurveyId),
        DbHelper.Common.NewSqlParameter("PartId", SingleSurveyPartId),
        DbHelper.Common.NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_SkillsViewer)
      );
    }

    void GetPreSurveyScores() {

      DbHelper.Common.Query(@"
        SELECT sp.IsSelf, sq.GblQuestionId, AVG(sc.ValueScore) AS AvgScore
        FROM sv_360_Participants sp
        INNER JOIN sv_Answers sa ON sp.PartId = sa.ParticipantId
        INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        WHERE
          (sp.PartId = @PartId OR sp.Self_PartId = @PartId)
          AND sp.Completed IS NOT NULL
          AND sq.RptQnGroupId = @RptQnGroupId
        GROUP BY sp.IsSelf, sq.GblQuestionId",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
          if (gqInfo != null) {
            if (dr.GetInt("IsSelf") == 1) {
              gqInfo.PreSurveySelfScore = dr.GetDecimalOrNull("AvgScore").Round(1, MidpointRounding.AwayFromZero);
            } else {
              gqInfo.PreSurveyRaterScore = dr.GetDecimalOrNull("AvgScore").Round(1, MidpointRounding.AwayFromZero);
            }
          }
        },
        DbHelper.Common.NewSqlParameter("PartId", SinglePreSurveyPartId),
        DbHelper.Common.NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_SkillsViewer)
      );
    }

    public string RenderQuestions() {

      var sb = new StringBuilder();

      Action<string> categoryStart = (categoryName) => {
        sb.Append("<div class=\"boxBorder\">");
        sb.Append("<div class=\"boxTitle\"><h4>").Append(categoryName.HTMLEncode()).Append("</h4></div>");
      };

      Action categoryEnd = () => {
        sb.Append("</div>");
      };

      Action<QuestionInfo> questionDetail = (qnItem) => {
        string preSurveySelfStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.PreSurveySelfScore, qnItem.MaxScore)}\"";
        string surveySelfStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.SurveySelfScore, qnItem.MaxScore)}\"";
        string preSurveyRaterStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.PreSurveyRaterScore, qnItem.MaxScore)}\"";
        string surveyRaterStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.SurveyRaterScore, qnItem.MaxScore)}\"";
        string preNormSelfStyle = $"style=\"left:{WebHelper.GetCSSPercentFromRatio(qnItem.PreNormSelfScore, qnItem.MaxScore)}\"";
        string preNormRaterStyle = $"style=\"left:{WebHelper.GetCSSPercentFromRatio(qnItem.PreNormRaterScore, qnItem.MaxScore)}\"";
        string normSelfStyle = $"style=\"left:{WebHelper.GetCSSPercentFromRatio(qnItem.NormSelfScore, qnItem.MaxScore)}\"";
        string normRaterStyle = $"style=\"left:{WebHelper.GetCSSPercentFromRatio(qnItem.NormRaterScore, qnItem.MaxScore)}\"";

        sb.Append("<div class=\"question ").Append(GetBenchComparisonRowClass(qnItem)).Append("\">");
        sb.Append("<div class=\"qnText\">").Append(qnItem.GblQuestionText.HTMLEncode()).Append("</div>");
        sb.Append("<div class=\"qnBars col-md-3\"><div class=\"scoreBars\">");

        sb.Append("<div class=\"scoreBar preScoreBar\">");
        sb.Append("<div class=\"barTitle\">Self-Pre</div>");
        sb.Append("<div class=\"barBg\">");
        sb.Append("<span class=\"barLine barSelf\" ").Append(preSurveySelfStyle).Append("></span>");
        sb.Append("<span class=\"barDot dotSelf\" title=\"").Append(NormDisplayName).Append(" Norm = ").Append(GetScoreFormatted(qnItem.PreNormSelfScore)).Append("\" ").Append(preNormSelfStyle).Append("></span>");
        sb.Append("</div>");
        sb.Append("<div class=\"barScore\">").Append(qnItem.PreSurveySelfScore == null ? "NA" : qnItem.PreSurveySelfScore.ToString("0.0", "")).Append("</div>");
        sb.Append("</div>");

        sb.Append("<div class=\"scoreBar\">");
        sb.Append("<div class=\"barTitle\">Self-Post</div>");
        sb.Append("<div class=\"barBg\">");
        sb.Append("<span class=\"barLine barSelf\" ").Append(surveySelfStyle).Append("></span>");
        sb.Append("<span class=\"barDot dotSelf\" title=\"").Append(NormDisplayName).Append(" Norm = ").Append(GetScoreFormatted(qnItem.NormSelfScore)).Append("\" ").Append(normSelfStyle).Append("></span>");
        sb.Append("</div>");
        sb.Append("<div class=\"barScore\">").Append(qnItem.SurveySelfScore == null ? "NA" : qnItem.SurveySelfScore.ToString("0.0", "")).Append("</div>");
        sb.Append("</div>");

        if (RaterCount > 0) {
          sb.Append("<div class=\"scoreBar preScoreBar\">");
          sb.Append("<div class=\"barTitle\">Rater-Pre</div>");
          sb.Append("<div class=\"barBg\">");
          sb.Append("<span class=\"barLine barRater\" ").Append(preSurveyRaterStyle).Append("></span>");
          sb.Append("<span class=\"barDot dotRater\" title=\"").Append(NormDisplayName).Append(" Norm = ").Append(GetScoreFormatted(qnItem.PreNormRaterScore)).Append("\" ").Append(preNormRaterStyle).Append("></span>");
          sb.Append("</div>");
          sb.Append("<div class=\"barScore\">").Append(qnItem.PreSurveyRaterScore == null ? "NA" : qnItem.PreSurveyRaterScore.ToString("0.0", "")).Append("</div>");
          sb.Append("</div>");

          sb.Append("<div class=\"scoreBar\">");
          sb.Append("<div class=\"barTitle\">Rater-Post</div>");
          sb.Append("<div class=\"barBg\">");
          sb.Append("<span class=\"barLine barRater\" ").Append(surveyRaterStyle).Append("></span>");
          sb.Append("<span class=\"barDot dotRater\" title=\"").Append(NormDisplayName).Append(" Norm = ").Append(GetScoreFormatted(qnItem.NormRaterScore)).Append("\" ").Append(normRaterStyle).Append("></span>");
          sb.Append("</div>");
          sb.Append("<div class=\"barScore\">").Append(qnItem.SurveyRaterScore == null ? "NA" : qnItem.SurveyRaterScore.ToString("0.0", "")).Append("</div>");
          sb.Append("</div>");
        }

        sb.Append("</div></div>");
        sb.Append("</div>");
      };

      ShowQuestions(categoryStart, categoryEnd, questionDetail);

      return sb.ToString();
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

      if (question.SurveySelfScore != null && question.NormSelfScore != null && question.SurveySelfScore < question.NormSelfScore) {
        return "benchBelow";
      } else {
        return "";
      }
    }

    public string GetBenchComparisonText(QuestionInfo question) {

      if (question.SurveySelfScore == null || question.NormSelfScore == null) {
        return "n/a";
      } else if (question.SurveySelfScore == question.NormSelfScore) {
        return "Equal to Global Norm";
      } else {
        return $"{Math.Abs(question.NormSelfScore.Value).ToString("0")} points {(question.SurveySelfScore > question.NormSelfScore ? "above " : "below ")} Global Norm.";
      }
    }

    public class QuestionInfo {
      public int CategoryHeadingId { get; set; }
      public string CategoryHeading { get; set; }
      public int GblQuestionId { get; set; }
      public string GblQuestionText { get; set; }
      public decimal? SurveySelfScore { get; set; }
      public decimal? SurveyRaterScore { get; set; }
      public decimal? PreSurveySelfScore { get; set; }
      public decimal? PreSurveyRaterScore { get; set; }
      public decimal? NormSelfScore { get; set; }
      public decimal? NormRaterScore { get; set; }
      public decimal? PreNormSelfScore { get; set; }
      public decimal? PreNormRaterScore { get; set; }
      public int MaxScore { get; set; }
    }

  }
}
