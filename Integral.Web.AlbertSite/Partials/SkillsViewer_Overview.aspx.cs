using System;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class SkillsViewer_Overview : AppCode.PageBaseClasses.SkillsViewerPartialBase {

    public decimal? SelfBenchScore, RaterBenchScore;
    public decimal? SelfAllScore, RaterAllScore;
    public decimal? SelfPreScore, RaterPreScore;
    public decimal? SelfPostScore, RaterPostScore;
    public int Bench360ResponseCountSelf, Bench360ResponseCountRater;
    public bool IsSingleProgram, ShowPrePostBox;
    public DbHelper.Participants.PrePostScopeEnum PrePostScope;
    DbHelper.AblePrograms.PrePostSurveyState ProgramPrePostSurveyState = null;

    protected void Page_Load(object sender, EventArgs e) {

      IsSingleProgram = SurveyStats.ProgramJobIds?.Count == 1;
      PrePostScope = IsSingleProgram ? DbHelper.Participants.PrePostScopeEnum.Coachee : DbHelper.Participants.PrePostScopeEnum.User;

      if (IsSingleProgram) {
        ProgramPrePostSurveyState = DbHelper.AblePrograms.GetPrePostSurveyState(SurveyStats.ProgramJobIds[0]);
        ShowPrePostBox = ProgramPrePostSurveyState.PreProgramSurveyComplete && ProgramPrePostSurveyState.PostProgramSurveyComplete;
      } else {
        ShowPrePostBox = SurveyStats.HasPreSurvey;
      }

      GetOverviewScores();

      if (SurveyStats.HasPreSurvey) {
        GetPrePostScores();
      }

      GetBenchScores();
      GetBenchResponseCount();
    }

    void GetOverviewScores() {

      DbHelper.Common.Query($@"
        {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Create ProgramJobIds CTE only if there is a ProgramJobId selection.
        ? "WITH ProgramJobIds AS (SELECT Value AS ProgramJobId FROM STRING_SPLIT(@ProgramJobIds, ','))"
        : "")}
        SELECT sp.IsSelf, SUM(sc.ValueScore) AS ScoreSum, COUNT(sc.ValueScore) AS ScoreCount
        FROM sv_360_Participants sp WITH (NOLOCK)
        INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
        INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Join to ProgramJobIds CTE if included.
          ? "INNER JOIN ProgramJobIds pjs ON pjs.ProgramJobId = sp.AbleProgramJobId"
          : "")}
        WHERE sp.AbleProjectId = @ProjectId
          AND {DbHelper.Reports.SkillsViewer.SQLWhereConditions.Completed360Skills("sv", "sp", "sq")}
        GROUP BY sp.IsSelf",
        dr => {
          decimal? scoreSum = dr.GetDecimalOrNull("ScoreSum");
          decimal? scoreCount = dr.GetDecimalOrNull("ScoreCount");
          decimal? score = null;
          if (scoreSum != null && scoreCount > 0) {
            score = (scoreSum.Value / scoreCount.Value).Round(1, MidpointRounding.AwayFromZero);
          }
          if (dr.GetBoolFromInt("IsSelf")) {
            SelfAllScore = score;
          } else {
            RaterAllScore = score;
          }
        },
        GetStandardSqlParams()
      );
    }

    void GetPrePostScores() {

      string prePostFlagColumn = PrePostScope == DbHelper.Participants.PrePostScopeEnum.Coachee ? "sp.PrePost360ForCoachee" : "sp.PrePost360ForUser";

      DbHelper.Common.Query($@"
        {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Create ProgramJobIds CTE only if there is a ProgramJobId selection.
        ? "WITH ProgramJobIds AS (SELECT Value AS ProgramJobId FROM STRING_SPLIT(@ProgramJobIds, ','))"
        : "")}
        SELECT sp.IsSelf, {prePostFlagColumn} AS PrePostFlag,
          SUM(sc.ValueScore) AS ScoreSum,
          COUNT(sc.ValueScore) AS ScoreCount,
          COUNT(DISTINCT sp.PartId) AS PartCount
        FROM sv_360_Participants sp WITH (NOLOCK)
        INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
        INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
        INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
        INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        {(!SurveyStats.ProgramJobIds.IsNullOrEmpty() // Join to ProgramJobIds CTE if included.
          ? "INNER JOIN ProgramJobIds pjs ON pjs.ProgramJobId = sp.AbleProgramJobId"
          : "")}
        WHERE sp.AbleProjectId = @ProjectId
          AND {DbHelper.Reports.SkillsViewer.SQLWhereConditions.Completed360Skills("sv", "sp", "sq")}
          AND {prePostFlagColumn} > 0
        GROUP BY sp.IsSelf, {prePostFlagColumn}",
        dr => {
          int partCount = dr.GetIntOrNull("PartCount") ?? 0;
          decimal? scoreSum = dr.GetDecimalOrNull("ScoreSum");
          decimal? scoreCount = dr.GetDecimalOrNull("ScoreCount");
          decimal? score = null;
          if (scoreSum != null && scoreCount > 0) {
            score = (scoreSum.Value / scoreCount.Value).Round(1, MidpointRounding.AwayFromZero);
          }
          int prePostFlag = dr.GetInt("PrePostFlag", 0);
          if (prePostFlag == DbHelper.Participants.PrePostFlags.IsPreSurvey) {
            if (dr.GetBoolFromInt("IsSelf")) {
              SelfPreScore = score;
              SurveyStats.SelfPreCount = partCount;
            } else {
              RaterPreScore = score;
              SurveyStats.RaterPreCount = partCount;
            }
          } else if (prePostFlag == DbHelper.Participants.PrePostFlags.IsPostSurvey) {
            if (dr.GetBoolFromInt("IsSelf")) {
              SelfPostScore = score;
              SurveyStats.SelfPostCount = partCount;
            } else {
              RaterPostScore = score;
              SurveyStats.RaterPostCount = partCount;
            }
          }
        },
        GetStandardSqlParams()
      );
    }

    void GetBenchScores() {

      try {

        var normResult = DbHelper.Reports.General.GetNorms(
          gblAnswerTypeId: SurveyStats.PrimaryGblAnswerTypeId,
          rptQnGroupIdOrNullForAll: ConfigHelper.RptQnGroupId_SkillsViewer,
          orgCompanyId: BenchmarkType == DbHelper.Reports.NormEnum.Org ? (int?)SurveyStats.SvCompanyId : null);

        SelfBenchScore = normResult.SelfNorm;
        RaterBenchScore = normResult.RaterNorm;

      } catch (Exception) {
        // Ignore for now.
      }
    }

    void GetBenchResponseCount() {
      try {
        DbHelper.Reports.General.GetResponseCountForCompanyOrGlobal(
          SurveyStats.PrimaryGblAnswerTypeId,
          BenchmarkType == DbHelper.Reports.NormEnum.Org ? (int?)SurveyStats.SvCompanyId : null,
          out Bench360ResponseCountSelf, out Bench360ResponseCountRater);
      } catch (Exception) {
        // Ignore for now.
      }
    }

  }
}
