using System;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class CoacheeReport_Overview : AppCode.PageBaseClasses.CoacheeReportPartialBase {

    public decimal? ScoreSelf, ScoreRater;
    public decimal? NormSelf, NormRater;
    public decimal? PreSurveyScoreSelf, PreSurveyScoreRater;
    public int PreSurveyRaterCount;
    public bool CanShowAISummaryText;
    public string AISummaryText;

    protected void Page_Load(object sender, EventArgs e) {

      GetSurveyScores();
      GetNorms();

      if (Is360Survey) {
        CanShowAISummaryText = true;
        AISummaryText = GetAISummaryText();
      }
    }

    void GetSurveyScores() {

      DbHelper.Common.Query($@"
        SELECT
          AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS SelfScore,
          AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS RaterScore
        FROM sv_360_Participants sp
        INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
        WHERE (sp.PartId = @PartId OR sp.Self_PartId = @PartId)
          AND sp.Completed IS NOT NULL
          AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId",
        dr => {
          ScoreSelf = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero, 0);
          ScoreRater = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero, 0);
        },
        DbHelper.Common.NewSqlParameter("PartId", ParticipantId)
      );

      if (PreSurveyPartId > 0) {
        // Get pre survey average for the participant.
        DbHelper.Common.Query($@"
          SELECT sp.IsSelf, AVG(sc.ValueScore) AS AvgScore, COUNT(DISTINCT sp.PartId) AS PartCount
          FROM sv_Answers sa
          INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
          INNER JOIN sv_360_Questions sq ON sa.QuestionId = sq.QuestionId
          INNER JOIN sv_360_Participants sp ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
          WHERE (sp.PartId = @PreSurveyPartId OR sp.Self_PartId = @PreSurveyPartId)
            AND sp.Completed IS NOT NULL
            AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
          GROUP BY sp.IsSelf",
          dr => {
            if (dr.GetInt("IsSelf") == 1) {
              PreSurveyScoreSelf = dr.GetDecimalOrNull("AvgScore").Round(1, MidpointRounding.AwayFromZero, 0);
            } else {
              PreSurveyScoreRater = dr.GetDecimalOrNull("AvgScore").Round(1, MidpointRounding.AwayFromZero, 0);
              PreSurveyRaterCount = dr.GetInt("PartCount");
            }
          },
          DbHelper.Common.NewSqlParameter("PreSurveyPartId", PreSurveyPartId)
        );
      }
    }

    void GetNorms() {

      try {

        var normResult = DbHelper.Reports.General.GetNorms(
          gblAnswerTypeId: GblAnswerTypeId,
          rptQnGroupIdOrNullForAll: null,
          orgCompanyId: BenchmarkType == DbHelper.Reports.NormEnum.Org ? (int?)CompanyId : null);

        NormSelf = normResult.SelfNorm;
        NormRater = normResult.RaterNorm;

      } catch (Exception) {
        // Ignore for now.
      }
    }

    string GetAISummaryText() {

      return DbHelper.Common.GetScalarQuery(@"
        SELECT AICoachSummaryText
        FROM sv_ParticipantAICoachSummary
        WHERE ParticipantId = @ParticipantId;",
        DbHelper.Common.NewSqlParameter("ParticipantId", ParticipantId)
      ).ToStringOrEmptyIfNull();
    }

    public string GetOverallBoxTitle() {
      return $"Overall {(HasPreSurvey ? "Post" : "")} Score";
    }

  }
}
