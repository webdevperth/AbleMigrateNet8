using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class SurveyViewerPartialBase : LoggedInPageBase {

    public List<int> IntakeCodeIdList, PreIntakeCodeIdList;
    public int ScoringGblAnswerTypeId = 0;
    public int ScoreMinValue = 0, ScoreMaxValue = 0;
    public int SurveyCount, SingleSurveyId, IntakeCount, SelfCount, RaterCount;
    public int? TemplateSurveyId, ClonedFromSurveyId;
    public ConfigHelper.EvalType EvalType;
    public string SingleSurveyUId, SurveyTypeCode;
    public bool HasSelfs, HasRaters;
    public PathHelper.SurveyViewerBenchmarkEnum BenchmarkType = PathHelper.SurveyViewerBenchmarkEnum.Global;
    public string NormDisplayName;
    public string SurveyReportType;
    public DateTime LatestCompletedUtc;
    public bool HasPreSurvey, Is360Survey;

    public Guid IndividualCoacheeGuid;
    public int SingleSurveyPartId, SinglePreSurveyPartId;
    public bool IsIndividualReport, IsEvalViewer, IsViewingParticipantPage;
    DbHelper.AlbertCoachees.AlbertCoacheeInfo IndividualCoacheeInfo;

    protected string SurveyConditionSQL = "";
    protected string HeadingConditionSQL = "";
    protected string SurveyAndHeadingConditionSQL = "";

    int foundIntakes = 0;
    int distinctAnswerTypes = 0;

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);

      IntakeCodeIdList = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.SurveyIntakeCodeId);
      PreIntakeCodeIdList = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.PreSurveyIntakeId);
      ScoringGblAnswerTypeId = (int)WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.GblAnswerTypeId, 0);
      TemplateSurveyId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.TemplateSurveyId, null);
      EvalType = WebHelper.GetQueryStringEnum(PathHelper.AbleUrlKeys.EvalType, ConfigHelper.EvalType.All);

      // An individual report is passed the CoacheeGuid.
      IsIndividualReport = WebHelper.TryGetQueryStringGuid(PathHelper.AbleUrlKeys.CoacheeGuid, out IndividualCoacheeGuid);

      // The page that called this partial. Could be EvalViewer or SurveyViewer.
      IsEvalViewer = PathHelper.IsReferrerPageURL(PathHelper.Reports.EvalViewer());

      IsViewingParticipantPage = PathHelper.GetPartialViewContext() == PathHelper.PartialViewContext.ParticipantView;

      bool hasAccessToReport = false;

      if (IsIndividualReport) {

        IndividualCoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(IndividualCoacheeGuid);

        if (IsViewingParticipantPage) {
          hasAccessToReport = SessionHelper.AppAccess.Participants.CanViewParticipantSummary(IndividualCoacheeInfo);
        } else {
          hasAccessToReport = SessionHelper.AppAccess.Reports.CanViewIndividualReport(IndividualCoacheeInfo);
        }

      } else if (IsEvalViewer) {

        hasAccessToReport = SessionHelper.AppAccess.Insights.CanViewEvalViewer();

      } else {

        hasAccessToReport = SessionHelper.AppAccess.Insights.CanViewSurveyViewer();
      }

      if (!hasAccessToReport) {
        WebHelper.WriteAndEnd("No access to report.");
        return;
      }

      if (IntakeCodeIdList.IsNullOrEmpty()) {
        WebHelper.WriteAndEnd("Survey intake IDs not provided.");
        return;
      }

      IntakeCount = IntakeCodeIdList.Count;

      if (IsIndividualReport && IntakeCount != 1) {
        WebHelper.WriteAndEnd("Individual report can only cover a single intake.");
        return;
      }

      int.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PreSurveyPartId), out SinglePreSurveyPartId);

      HasPreSurvey = SinglePreSurveyPartId > 0;

      if (!Enum.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyViewerBenchmark), true, out BenchmarkType)) {
        BenchmarkType = PathHelper.SurveyViewerBenchmarkEnum.Global;
      }
      if (BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org) {
        NormDisplayName = "Organisation";
      } else {
        NormDisplayName = "Global";
      }

      if (IsEvalViewer) {

        // Ignore any provided template id, instead use eval rules to determine criteria.
        TemplateSurveyId = null;
        var evalSQLConditions = DbHelper.Reports.EvalViewer.GetSQLConditionsForReport(EvalType);
        SurveyConditionSQL = SurveyConditionSQL.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + evalSQLConditions.SurveyConditionSQL;
        HeadingConditionSQL = HeadingConditionSQL.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + evalSQLConditions.HeadingConditionSQL;

      } else if (TemplateSurveyId > 0) {

        SurveyConditionSQL = SurveyConditionSQL.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + "sv.ClonedFromSvId = " + TemplateSurveyId;

      }

      SurveyAndHeadingConditionSQL = SurveyConditionSQL.AppendWithSeparator(" AND ", HeadingConditionSQL);

      // Get intake and survey count.
      DbHelper.Common.Query($@"
        WITH IntakeCodeIds
        AS (
          SELECT Value AS IntakeCodeId
          FROM STRING_SPLIT(@IntakeCodeIds, ',')
        )
        SELECT
          COUNT(DISTINCT sic.CodeId) AS IntakeCount,
          COUNT(DISTINCT sv.PrimaryGblAnswerTypeId) AS AnswerTypeCount,
          COUNT(DISTINCT sv.sv_id) AS SurveyCount,
          MAX(sv.PrimaryGblAnswerTypeId) AS PrimaryGblAnswerTypeId,
          MAX(sv.ClonedFromSvId) AS ClonedFronSvId,
          MAX(sv.SurveyTypeCode) AS SurveyTypeCode,
          MAX(sv.sv_ReportType) AS ReportType,
          MAX(sv.sv_id) AS SingleSurveyId,
          MAX(sv.sv_uniqueid) AS SingleSurveyUId
        FROM IntakeCodeIds ic WITH (NOLOCK)
        INNER JOIN sv_360_Codes sic ON sic.CodeId = ic.IntakeCodeId
        INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId
        INNER JOIN sv_Survey sv ON sv.sv_id = sit.SurveyId
        WHERE sit.AnswerTypeDescr = 'date'
          {SurveyConditionSQL.EnsureStartsWith("AND ", true)}",
        dr => {
          foundIntakes = dr.GetInt("IntakeCount");
          distinctAnswerTypes = dr.GetInt("AnswerTypeCount");
          ClonedFromSurveyId = dr.GetIntOrNull("ClonedFronSvId");
          SurveyTypeCode = dr.GetString("SurveyTypeCode");
          if (ScoringGblAnswerTypeId == 0) ScoringGblAnswerTypeId = dr.GetIntOrNull("PrimaryGblAnswerTypeId") ?? 0;
          SurveyCount = dr.GetInt("SurveyCount");
          SingleSurveyId = dr.GetInt("SingleSurveyId");
          SingleSurveyUId = dr.GetString("SingleSurveyUId");
          if (IntakeCount == 1) {
            SurveyReportType = dr.GetString("ReportType");
          }
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList())
      );

      var surveyType = DbHelper.AlbertSurveys.GetSurveyType(SurveyTypeCode);
      Is360Survey = surveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Standard360;

      // The number of found intakes must equal the number of requested ones.
      if (foundIntakes != IntakeCodeIdList.Count) {
        WebHelper.WriteAndEnd("Survey intake(s) not found.");
        return;
      }

      // The number of distinct primary answer types in the surveys must equal 1.
      if (distinctAnswerTypes != 1 || ScoringGblAnswerTypeId == 0) {
        WebHelper.WriteAndEnd("One or more selected surveys are incompatible with each other or the viewer.");
        return;
      }

      // Get participant counts in the given intake(s).
      // An individual report is restricted to the participant in a single intake.
      DbHelper.Common.Query($@"
        WITH IntakeCodeIds
        AS (
          SELECT Value AS IntakeCodeId
          FROM STRING_SPLIT(@IntakeCodeIds, ',')
        )
        {(IsIndividualReport
          ? @",SelfPart AS
            ( SELECT sp.PartId
              FROM sv_360_Participants sp
              INNER JOIN al_Coachees ac ON sp.AbleCoacheeId = ac.CoacheeId
              WHERE ac.CoacheeUID = @CoacheeGuid
            )"
          : "")}
        SELECT sp.IsSelf, MAX(sp.Completed) AS LatestCompleted, COUNT(DISTINCT sp.PartId) AS PartCount, MAX(sp.PartId) AS PartId
        FROM IntakeCodeIds ic WITH (NOLOCK)
        INNER JOIN sv_360_Participants sp ON sp.IntakeCodeId = ic.IntakeCodeId
        INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
        {(IsIndividualReport
          ? "INNER JOIN SelfPart ON sp.PartId = SelfPart.PartId OR sp.Self_PartId = SelfPart.PartId"
          : "")}
        WHERE sp.Completed IS NOT NULL
          {SurveyConditionSQL.EnsureStartsWith("AND ", true)}
        GROUP BY sp.IsSelf
        ORDER BY sp.IsSelf",
        dr => {
          // Note this assumes SQL is grouped only by IsSelf, so it only produces 2 rows max - one for self count, one for rater count.
          if (dr.GetInt("IsSelf") == 1) {
            HasSelfs = true;
            SelfCount = dr.GetInt("PartCount");
            LatestCompletedUtc = dr.GetDateTime("LatestCompleted");
            if (SelfCount == 1) SingleSurveyPartId = dr.GetInt("PartId"); // Participant ID of the single self.
          } else {
            RaterCount = dr.GetInt("PartCount");
            HasRaters = true;
          }
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList()),
        DbHelper.Common.NewSqlParameter("CoacheeGuid", IndividualCoacheeGuid)
      );

      if (SelfCount == 0) {
        WebHelper.WriteAndEnd("No participant responses were found.");
        return;
      }

      // Get min & max scores for the primary answer type.
      DbHelper.Common.Query(@"
        SELECT
          MIN(sc.ValueScore) AS MinScore,
          MAX(sc.ValueScore) AS MaxScore
        FROM sv_360_AnswerTypes sat
        INNER JOIN sv_360_Codes sc ON sat.AnswerTypeId = sc.AnswerTypeId
        WHERE sat.SurveyId = @SurveyId
        AND sat.GblAnswerTypeId = @GblAnswerTypeId",
        dr => {
          ScoreMinValue = dr.GetInt("MinScore") - 1; // One lower so the bars aren't zero-width for the minimum score.
          ScoreMaxValue = dr.GetInt("MaxScore");
        },
        DbHelper.Common.NewSqlParameter("SurveyId", SingleSurveyId),
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", ScoringGblAnswerTypeId)
      );

      if (ScoreMaxValue == 0) {
        WebHelper.WriteAndEnd("Can't determine scoring in selected survey(s).");
        return;
      }
    }

    public string GetScoreFormatted(double? scoreValue, string textIfNull = "NA") {
      return GetScoreFormatted((decimal?)scoreValue, textIfNull);
    }
    public string GetScoreFormatted(decimal? scoreValue, string textIfNull = "NA") {
      if (!scoreValue.HasValue) return textIfNull;
      return scoreValue.Value.ToString("0.0");
    }

  }
}
