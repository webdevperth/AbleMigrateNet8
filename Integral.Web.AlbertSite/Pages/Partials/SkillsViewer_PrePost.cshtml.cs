using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class SkillsViewer_PrePost : AppCode.PageBaseClasses.SkillsViewerPartialBase {

    // Expose protected base members as public for Razor view access.
    public new DbHelper.Reports.SkillsViewer.SurveyStats SurveyStats => base.SurveyStats;
    public new string BenchmarkDisplayName => base.BenchmarkDisplayName;
    public new DbHelper.Reports.NormEnum BenchmarkType => base.BenchmarkType;

    public List<DbHelper.Reports.PrePost.QuestionInfo> Questions = new List<DbHelper.Reports.PrePost.QuestionInfo>();
    public int TableRowCount = 0;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      Questions = DbHelper.Reports.PrePost.GetQuestions(GetStandardSqlParams());

      if (SurveyStats.ProgramJobIds?.Count == 1) {
        GetSingleProgramScores(SurveyStats.ProgramJobIds[0]);
      } else {
        GetProjectScores();
      }

      return Page();
    }

    void GetSingleProgramScores(int programJobId) {

      DbHelper.Common.Query($@"
        SELECT sq.GblQuestionId, sp.IsSelf, sp.PrePost360ForCoachee, AVG(sc.ValueScore) AS AvgScore
        FROM sv_360_Participants sp
        INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
        WHERE sp.AbleProgramJobId = @ProgramJobId
          AND sp.Completed IS NOT NULL
          AND {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
          AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
          AND sq.RptQnGroupId = @RptQnGroupId
          AND sp.PrePost360ForCoachee > 0
        GROUP BY sq.GblQuestionId, sp.IsSelf, sp.PrePost360ForCoachee",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(q => q.GblQuestionId == gqId);
          if (gqInfo != null) {
            decimal? avgScore = dr.GetDecimalOrNull("AvgScore").Round(1, MidpointRounding.AwayFromZero);
            bool isSelf = dr.GetBoolFromInt("IsSelf");
            int prePost360ForCoachee = dr.GetInt("PrePost360ForCoachee");
            if (isSelf) {
              if (prePost360ForCoachee == 1) {
                gqInfo.ScoreSelfPre = avgScore;
              } else {
                gqInfo.ScoreSelfPost = avgScore;
              }
            } else {
              if (prePost360ForCoachee == 1) {
                gqInfo.ScoreRaterPre = avgScore;
              } else {
                gqInfo.ScoreRaterPost = avgScore;
              }
            }
          }
        },
        DbHelper.Common.NewSqlParameter("ProgramJobId", programJobId),
        DbHelper.Common.NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_SkillsViewer)
      );

    }

    void GetProjectScores() {

      DbHelper.Common.Query($@"
        WITH
          {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Create ProgramJobIds CTE only if there is a ProgramJobId selection.
          ? "ProgramJobIds AS (SELECT Value AS ProgramJobId FROM STRING_SPLIT(@ProgramJobIds, ',')),"
          : "")}
        results
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
          {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Join to ProgramJobIds CTE if included.
            ? "INNER JOIN ProgramJobIds pjs ON pjs.ProgramJobId = sp.AbleProgramJobId"
            : "")}
          WHERE sp.AbleProjectId = @ProjectId
            AND sp.Completed IS NOT NULL
            AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
            AND sq.RptQnGroupId = @RptQnGroupId
            AND sv.SvCompanyId = @SvCompanyId
            {(SurveyStats.RptQnGroupId != null // If RptQnGroupId specified, only include surveys with that qn group.
              ? "AND EXISTS (SELECT NULL FROM sv_360_Questions sq WHERE sv.sv_id = sq.SurveyId AND sq.RptQnGroupId = @RptQnGroupId)"
              : "")}
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
          WHERE sgs.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
            AND sq.SurveyId = @SampleSurveyId
            AND sq.RptQnGroupId = @RptQnGroupId
            AND {DbHelper.AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            {(BenchmarkType == DbHelper.Reports.NormEnum.Org ? "AND (sv.SvCompanyId = @SvCompanyId)" : "")}
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

  }
}
