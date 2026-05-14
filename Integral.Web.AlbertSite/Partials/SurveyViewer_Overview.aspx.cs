using System;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class SurveyViewer_Overview : AppCode.PageBaseClasses.SurveyViewerPartialBase {

    public decimal? ScoreSelf, ScoreRater;
    public decimal? NormSelf, NormRater;
    public decimal? PreSurveyScoreSelf, PreSurveyScoreRater;
    public int PreSurveyRaterCount;
    public bool CanShowAISummaryText;
    public string AISummaryText;

    protected void Page_Load(object sender, EventArgs e) {

      GetGlobalNorms();

      if (BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org) {
        GetOrgNorms();
      }

      if (IsEvalViewer) {
        GetProgramScores();
      }

      if (Is360Survey && IsIndividualReport && !IsViewingParticipantPage) {
        CanShowAISummaryText = true;
        AISummaryText = GetAISummaryText();
      }
    }

    void GetProgramScores() {

      DbHelper.Common.Query($@"
        WITH IntakeCodeIds
        AS (
          SELECT value AS IntakeCodeId
          FROM STRING_SPLIT(@IntakeCodeIds, ',')
        )
        SELECT AVG(sc.ValueScore) AS SelfScore, COUNT(DISTINCT sp.PartId) AS SelfCount
        FROM sv_Answers sa
        INNER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        INNER JOIN IntakeCodeIds ic ON ic.IntakeCodeId = sp.IntakeCodeId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = sq.GblQuestionId
        INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
        INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        WHERE sp.Completed IS NOT NULL
          AND sq.GblAnswerTypeId = @GblAnswerTypeId
          {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}",
        dr => {
          ScoreSelf = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero, 0);
          SelfCount = dr.GetInt("SelfCount");
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList()),
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", ScoringGblAnswerTypeId),
        DbHelper.Common.NewSqlParameter("TemplateSurveyId", TemplateSurveyId)
      );
    }

    void GetGlobalNorms() {

      // Scores for selected intakes.
      try {
        DbHelper.Common.Query($@"
          WITH IntakeCodeIds
          AS (
            SELECT value AS IntakeCodeId
            FROM STRING_SPLIT(@IntakeCodeIds, ',')
          )
          SELECT
            SUM(glq.GlobalNormSelfTotal) AS NormSelfTotal,
            SUM(glq.GlobalNormSelfCount) AS NormSelfCount,
            SUM(glq.GlobalNormRaterTotal) AS NormRaterTotal,
            SUM(glq.GlobalNormRaterCount) AS NormRaterCount,
            AVG(IIF(sp.IsSelf = 1, sac.ValueScore, NULL)) AS SelfScore,
            AVG(IIF(sp.IsSelf = 0, sac.ValueScore, NULL)) AS RaterScore
          FROM sv_360_Codes sic WITH (NOLOCK)
          INNER JOIN IntakeCodeIds ic ON sic.CodeId = ic.IntakeCodeId
          INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId
          INNER JOIN sv_360_Questions sq ON sq.SurveyId = sit.SurveyId
          INNER JOIN sv_GblQuestions glq ON glq.GblQuestionId = sq.GblQuestionId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = glq.GblQuestionId
          INNER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
          INNER JOIN sv_360_Codes sac ON sac.CodeId = sa.CodeId
          INNER JOIN sv_360_Participants sp ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          WHERE sp.Completed IS NOT NULL
            AND sq.GblAnswerTypeId = @GblAnswerTypeId
            {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}
            {(SingleSurveyPartId > 0 ? "AND (sp.PartId = @PartId OR sp.Self_PartId = @PartId)" : "")}",
          dr => {
            ScoreSelf = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero, 0);
            ScoreRater = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero, 0);
            int normSelfTotal = dr.GetIntOrNull("NormSelfTotal") ?? 0;
            int normSelfCount = dr.GetIntOrNull("NormSelfCount") ?? 0;
            if (normSelfTotal == 0 || normSelfCount == 0) {
              NormSelf = null;
            } else {
              NormSelf = ((decimal)normSelfTotal / normSelfCount).Round(1, MidpointRounding.AwayFromZero);
            }
            int normRaterTotal = dr.GetIntOrNull("NormRaterTotal") ?? 0;
            int normRaterCount = dr.GetIntOrNull("NormRaterCount") ?? 0;
            if (normRaterTotal == 0 || normRaterCount == 0) {
              NormRater = null;
            } else {
              NormRater = ((decimal)normRaterTotal / normRaterCount).Round(1, MidpointRounding.AwayFromZero);
            }
          },
          DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList()),
          DbHelper.Common.NewSqlParameter("GblAnswerTypeId", ScoringGblAnswerTypeId),
          DbHelper.Common.NewSqlParameter("PartId", SingleSurveyPartId)
        );
      } catch (Exception) {
        // Ignore for now.
      }

      if (SinglePreSurveyPartId > 0) {
        // Get pre survey average for the participant.
        DbHelper.Common.Query($@"
          SELECT sp.IsSelf, AVG(sc.ValueScore) AS AvgScore, COUNT(DISTINCT sp.PartId) AS PartCount
          FROM sv_Answers sa
          INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
          INNER JOIN sv_360_Questions sq ON sa.QuestionId = sq.QuestionId
          INNER JOIN sv_360_Participants sp ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
          WHERE sq.GblAnswerTypeId = @GblAnswerTypeId
            AND (sp.PartId = @PreSurveyPartId OR sp.Self_PartId = @PreSurveyPartId)
            AND sp.Completed IS NOT NULL
            {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}
          GROUP BY sp.IsSelf",
          dr => {
            if (dr.GetInt("IsSelf") == 1) {
              PreSurveyScoreSelf = dr.GetDecimalOrNull("AvgScore").Round(1, MidpointRounding.AwayFromZero, 0);
            } else {
              PreSurveyScoreRater = dr.GetDecimalOrNull("AvgScore").Round(1, MidpointRounding.AwayFromZero, 0);
              PreSurveyRaterCount = dr.GetInt("PartCount");
            }
          },
          DbHelper.Common.NewSqlParameter("GblAnswerTypeId", ScoringGblAnswerTypeId),
          DbHelper.Common.NewSqlParameter("PreSurveyPartId", SinglePreSurveyPartId),
          DbHelper.Common.NewSqlParameter("SurveyPartId", SingleSurveyPartId)
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
          SELECT
            SUM(gqs.ScoreSum) AS NormSelfTotal, SUM(gqs.ScoreCount) AS NormSelfCount,
            SUM(gqs.ScoreSumRaters) AS NormRaterTotal, SUM(gqs.ScoreCountRaters) AS NormRaterCount
          FROM CompanyIds
          INNER JOIN sv_Survey sv ON CompanyIds.SvCompanyId = sv.SvCompanyId
          INNER JOIN sv_SurveyGblQnScores gqs ON gqs.SurveyId = sv.sv_id
          WHERE gqs.GblAnswerTypeId = @GblAnswerTypeId
            {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}",
          dr => {
            int normSelfTotal = dr.GetIntOrNull("NormSelfTotal") ?? 0;
            int normSelfCount = dr.GetIntOrNull("NormSelfCount") ?? 0;
            if (normSelfTotal == 0 || normSelfCount == 0) {
              NormSelf = null;
            } else {
              NormSelf = ((decimal)normSelfTotal / normSelfCount).Round(1, MidpointRounding.AwayFromZero);
            }
            int normRaterTotal = dr.GetIntOrNull("NormRaterTotal") ?? 0;
            int normRaterCount = dr.GetIntOrNull("NormRaterCount") ?? 0;
            if (normRaterTotal == 0 || normRaterCount == 0) {
              NormRater = null;
            } else {
              NormRater = ((decimal)normRaterTotal / normRaterCount).Round(1, MidpointRounding.AwayFromZero);
            }
          },
          DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList()),
          DbHelper.Common.NewSqlParameter("GblAnswerTypeId", ScoringGblAnswerTypeId)
        );
      } catch (Exception) {
        // Ignore for now.
      }
    }

    string GetAISummaryText() {

      return DbHelper.Common.GetScalarQuery(@"
        SELECT AICoachSummaryText
        FROM sv_ParticipantAICoachSummary
        WHERE ParticipantId = @ParticipantId;",
        DbHelper.Common.NewSqlParameter("ParticipantId", SingleSurveyPartId)
      ).ToStringOrEmptyIfNull();
    }

    public string GetOverallBoxTitle() {

      if (IsViewingParticipantPage) return "Overall Leadership Score";

      if (IsEvalViewer) return "Eval Score";

      return $"Overall 360 {(HasPreSurvey ? "Post" : "")} Score";
    }

  }
}
