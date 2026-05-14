using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class CoacheeReport_PrePost : AppCode.PageBaseClasses.CoacheeReportPartialBase {

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public int TableRowCount = 0;

    protected void Page_Load(object sender, EventArgs e) {

      GetQuestions();

      GetPostSurveyScores();

      if (PreSurveyPartId > 0) {
        GetPreSurveyScores();
      }

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

    void GetPrePostNorms() {

      // Averages for pre and post surveys in current project.
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
                  gqInfo.PreSelfNorm = avgScore.Value;
                } else {
                  gqInfo.PreRaterNorm = avgScore.Value;
                }
              } else {
                if (dr.GetInt("IsSelf") == 1) {
                  gqInfo.PostSelfNorm = avgScore.Value;
                } else {
                  gqInfo.PostRaterNorm = avgScore.Value;
                }
              }
            }
          }
        },
        DbHelper.Common.NewSqlParameter("PartId", ParticipantId),
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", GblAnswerTypeId),
        DbHelper.Common.NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_SkillsViewer)
      );
    }

    void GetPostSurveyScores() {

      DbHelper.Common.Query(@"
        SELECT sq.GblQuestionId,
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
        GROUP BY sq.GblQuestionId",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
          if (gqInfo == null) return;
          gqInfo.PostSelfScore = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero);
          gqInfo.PostRaterScore = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero);
        },
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", GblAnswerTypeId),
        DbHelper.Common.NewSqlParameter("PartId", ParticipantId)
      );
    }

    void GetPreSurveyScores() {

      DbHelper.Common.Query(@"
        SELECT sq.GblQuestionId,
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
        GROUP BY sq.GblQuestionId",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
          if (gqInfo == null) return;
          gqInfo.PreSelfScore = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero);
          gqInfo.PreRaterScore = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero);
        },
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", GblAnswerTypeId),
        DbHelper.Common.NewSqlParameter("PartId", PreSurveyPartId)
      );
    }

    public string GetDotStyle(decimal? score, decimal? maximum) {
      if (score == null || score == 0) return "style=\"display:none;\"";
      return $"style=\"left:{WebHelper.GetCSSPercentFromRatio(score, maximum)}\"";
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

      if (question.PostSelfScore != null && question.PostSelfNorm != null && question.PostSelfScore < question.PostSelfNorm) {
        return "benchBelow";
      } else {
        return "";
      }
    }

    public class QuestionInfo {
      public int CategoryHeadingId { get; set; }
      public string CategoryHeading { get; set; }
      public int GblQuestionId { get; set; }
      public string GblQuestionText { get; set; }
      public decimal? PreSelfScore { get; set; }
      public decimal? PreRaterScore { get; set; }
      public decimal? PostSelfScore { get; set; }
      public decimal? PostRaterScore { get; set; }
      public decimal? PreSelfNorm { get; set; }
      public decimal? PreRaterNorm { get; set; }
      public decimal? PostSelfNorm { get; set; }
      public decimal? PostRaterNorm { get; set; }
    }

  }
}
