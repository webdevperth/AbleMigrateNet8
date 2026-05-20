using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class CoacheeReportPartialBase : LoggedInPageModel {

    // Querystring inputs.
    public int ParticipantUserId;
    public int IntakeCodeId, PreIntakeCodeId, PreSurveyPartId;
    public DbHelper.Reports.NormEnum BenchmarkType;

    public int SurveyId, ParticipantId, GblAnswerTypeId;
    public int CoacheeId, ProgramJobId, CompanyId;
    public string SurveyUId, SurveyTypeCode, SurveyReportType, NormDisplayName;
    public int ScoreMinValue, ScoreMaxValue, SelfCount, RaterCount;
    public int? ClonedFromSurveyId;
    public bool HasSelfs, HasRaters, HasPreSurvey, Is360Survey;
    public DateTime? LatestCompletedUtc = null;
    public DbHelper.AlbertSurveys.FeedbackOptionEnum FeedbackOption;
    public DbHelper.AlbertSurveys.SurveyTypeEnum SurveyType;
    public bool IsSelfOnly;
    public bool AllowReportWithoutRaters, Hide360ReportNorms;

    protected override void InitializePage() {

      base.InitializePage();

      if (WebHelper.IsRequestExiting()) return;

      // "Context Coachee" is the coachee from which the report was accessed.
      // It may be different to the survey's coachee, since surveys from past programs can appear on a later coachee's page.
      // However permissions to view the report are based on the coachee and program being viewed by the user, so
      // the "context" (i.e. the coachee/program wheree the report was accessed) is required.

      var contextCoacheeGuid = WebHelper.GetQueryStringGuid(PathHelper.AbleUrlKeys.CoacheeGuid) ?? Guid.Empty;

      IntakeCodeId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.SurveyIntakeCodeId) ?? 0;
      PreIntakeCodeId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.PreSurveyIntakeId) ?? 0;
      PreSurveyPartId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.PreSurveyPartId) ?? 0;

      if (!Enum.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyViewerBenchmark), true, out BenchmarkType)) {
        BenchmarkType = DbHelper.Reports.NormEnum.Global;
      }

      GetSharedSurveyInfo();

      var contextCoachee = DbHelper.AlbertCoachees.GetCoacheeInfo(contextCoacheeGuid);
      if (contextCoachee == null || (!SessionHelper.AppAccess.Reports.CanViewIndividualReport(contextCoachee) && !IsViewingSharedSurvey)) {
        WebHelper.WriteAndEnd("No access to report.");
        return;
      }

      // UserId of the coachee Guid passed to the partial.
      // Must be the same userId as the survey participant we are reporting on.
      ParticipantUserId = contextCoachee.UserId ?? 0;

      if (IntakeCodeId == 0) {
        WebHelper.WriteAndEnd("Survey intake IDs not provided.");
        return;
      }

      if (BenchmarkType == DbHelper.Reports.NormEnum.Org) {
        NormDisplayName = "Organisation";
      } else {
        NormDisplayName = "Global";
      }

      HasPreSurvey = PreSurveyPartId > 0;

      // Get intake and survey info.
      // Note we are getting the self participant info by IntakeCodeId + UserId, which should be unique,
      // i.e. it's assumed a person would only appear once as a self in any survey intake.
      DbHelper.Common.Query($@"
        SELECT COUNT(*) OVER() AS TotalCount,
          ac.CoacheeId, ac.ProgramJobId, ac.CompanyId,
          sv.sv_id, sv.sv_uniqueid, sv.sv_ReportType, sv.SurveyTypeCode, sv.sv_FeedbackOption,
          sv.ClonedFromSvId, sv.PrimaryGblAnswerTypeId, sv.AllowReportWithoutRaters, sv.Hide360ReportNorms,
          sp.PartId, sp.UniqueId AS PartUniqueId, sp.Completed,
          sc.MinScore, sc.MaxScore,
          spr.RaterCount, spr.LatestCompletedUtc
        FROM sv_360_Participants sp
        INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
        INNER JOIN al_Coachees ac ON ac.CoacheeId = sp.AbleCoacheeId
        CROSS APPLY (
          SELECT MIN(sc.ValueScore) AS MinScore, MAX(sc.ValueScore) AS MaxScore
          FROM sv_360_Codes sc
          INNER JOIN sv_360_AnswerTypes at ON at.AnswerTypeId = sc.AnswerTypeId
          WHERE at.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
        ) AS sc
        CROSS APPLY (
          SELECT COUNT(*) AS RaterCount, MAX(spr.Completed) AS LatestCompletedUtc
          FROM sv_360_Participants spr
          WHERE spr.Self_PartId = sp.PartId
            AND spr.Completed IS NOT NULL
        ) AS spr
        WHERE sp.IntakeCodeId = @IntakeCodeId
          AND sp.IsSelf = 1
          AND ac.UserId = @UserId",
        dr => {
          if (dr.GetInt("TotalCount") > 1) {
            WebHelper.WriteAndEnd("Multiple participants found for same User.");
            return;
          }
          CoacheeId = dr.GetInt("CoacheeId");
          ProgramJobId = dr.GetInt("ProgramJobId");
          CompanyId = dr.GetInt("CompanyId");
          SurveyId = dr.GetInt("sv_id");
          GblAnswerTypeId = dr.GetInt("PrimaryGblAnswerTypeId");
          ParticipantId = dr.GetInt("PartId");
          SurveyUId = dr.GetString("sv_uniqueid");
          SurveyTypeCode = dr.GetString("SurveyTypeCode");
          SurveyReportType = dr.GetString("sv_ReportType");
          ClonedFromSurveyId = dr.GetIntOrNull("ClonedFromSvId");
          ScoreMinValue = dr.GetInt("MinScore");
          ScoreMaxValue = dr.GetInt("MaxScore");
          SelfCount = dr.IsDBNull("Completed") ? 0 : 1;
          RaterCount = dr.GetInt("RaterCount");
          LatestCompletedUtc = dr.GetDateTimeOrNull("LatestCompletedUtc") ?? dr.GetDateTimeOrNull("Completed");
          AllowReportWithoutRaters = dr.GetBoolFromInt("AllowReportWithoutRaters");
          Hide360ReportNorms = dr.GetBoolFromInt("Hide360ReportNorms");
          FeedbackOption = (DbHelper.AlbertSurveys.FeedbackOptionEnum)dr.GetInt("sv_FeedbackOption");
          SurveyType = DbHelper.AlbertSurveys.GetSurveyType(SurveyTypeCode);
          IsSelfOnly = DbHelper.AlbertSurveys.IsSelfOnly(FeedbackOption, SurveyType);
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeId", IntakeCodeId),
        DbHelper.Common.NewSqlParameter("UserId", contextCoachee.UserId)
      );

      if (!IsSelfOnly) { // If raters are allowed.
        if (RaterCount == 0 && !AllowReportWithoutRaters) { // If no raters and not allowed to view report without them.
          WebHelper.WriteAndEnd("No completed raters in this survey.");
          return;
        }
      }

      HasSelfs = SelfCount > 0;
      HasRaters = RaterCount > 0;
      HasPreSurvey = PreIntakeCodeId > 0;
      Is360Survey = SurveyTypeCode == ConfigHelper.SurveyTypeCodes.Able360;
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
