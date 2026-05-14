using System.Collections.Generic;
using System;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class OrgSurveys {

      public const string Org_Default_Directorate_Title = "Directorate";

      public const int Min_Survey_Participants_Completed = 3; // Min completed for survey itself to be considered "completed".
      public const int Standard_GblAnswerTypeId = 2706;
      public const int LinkLevel_Current = 0;     // Used in tables such as sv_DivisionCodeLinksOrdered, LinkLevel 0 = current survey (the OriginSurveyId)
      public const int LinkLevel_Previous = 1;    // As above, LinkLevel 1 = the survey previous to OriginSurveyId (i.e. same survey as the origin's sv_PreviousSurveyId value)
      public const int IOI_GblQuestionId_First = 1;
      public const int IOI_GblQuestionId_Last = 32;
      public const int VitalOrg_GblQnGroupId = 46; // "Extra Questions" global group, which must be excluded from norms as they are specific to the single survey.

      public readonly static string SQL_BETWEEN_IOIGblQns = " BETWEEN " + IOI_GblQuestionId_First + " AND " + IOI_GblQuestionId_Last + " ";
      public const string DimensionQuestionColumnPrefix = "Dimension";

      public enum SurveyGroupIds : int {
        LocalGovernment = 161
      }

      public enum DimensionEnum { ReportSection, Quadrants, Drivers }

      public class DimensionInfo {
        public int Dimension { get; private set; }
        public string AnswerTypeDescr { get; private set; }
        public string QuestionColumnName { get; private set; }

        public DimensionInfo(int dimension, string answerTypeDescr) {
          Dimension = dimension;
          AnswerTypeDescr = answerTypeDescr;
          QuestionColumnName = DimensionQuestionColumnPrefix + dimension;
        }
      }

      private static readonly Dictionary<DimensionEnum, DimensionInfo> DimensionLookup = new Dictionary<DimensionEnum, DimensionInfo>() {
        { DimensionEnum.ReportSection, new DimensionInfo(1, "Report Section") },
        { DimensionEnum.Quadrants, new DimensionInfo(3, "Dimension") },
        { DimensionEnum.Drivers, new DimensionInfo(4, "Drivers") }
      };

      public static DimensionInfo GetDimensionInfo(DimensionEnum dimension) {
        return DimensionLookup[dimension];
      }

      public static SurveyInfo GetSurveyInfo(int surveyId, int? orgId = null) {
        return GetSurveyInfo(GetSurveyBy.SurveyId, surveyId, null, orgId);
      }

      public static SurveyInfo GetSurveyInfo(string surveyUID) {
        return GetSurveyInfo(GetSurveyBy.SurveyUID, null, surveyUID);
      }

      private enum GetSurveyBy { SurveyId, SurveyUID }

      private static SurveyInfo GetSurveyInfo(GetSurveyBy getSurveyBy, int? surveyId, string surveyUID, int? orgId = null) {

        if (getSurveyBy == GetSurveyBy.SurveyId && surveyId == null) return null;
        if (getSurveyBy == GetSurveyBy.SurveyUID && surveyUID.IsNullOrEmpty()) return null;

        // TODO use a cache for this data.

        SurveyInfo surveyInfo = null;

        var whereSQL = new List<string>();
        if (getSurveyBy == GetSurveyBy.SurveyId) whereSQL.Add("sv.sv_id = @SurveyId");
        if (getSurveyBy == GetSurveyBy.SurveyUID) whereSQL.Add("sv.sv_uniqueid = @SurveyUID");
        if (orgId > 0) whereSQL.Add("sv.sv_OrgId = @OrgId");

        Common.Query($@"
          SELECT sv.sv_id, sv_uniqueid, sv.sv_OrgId, sv.sv_ReportType, sv.SvCompanyId, sv.sv_SectorId,
                 sv.sv_createdUTC, sv.CreatedByPortalUserId,
                 sv.sv_title, sv.sv_sent, sv.sv_closedate, sv.SurveyTimeZoneId, sv.GroupReportAvailable,
                 sv.sv_SelfEmailSubject, sv.sv_SelfEmailText,
                 sv.OrgAltDirectorateTitle, sv.OrgReportDisableDrivers, sv.OrgReportMinResponsesToShow,
                 dc.IntakeCloseDateSelf, dc.JobId, dc.JobName,
                 sp.ParticipantCount, sp.CompletedCount
          FROM sv_Survey sv
          OUTER APPLY (
            SELECT TOP 1 dc.IntakeCloseDateSelf, ij.JobId, ij.JobName
            FROM sv_360_AnswerTypes dt
            INNER JOIN sv_360_Codes dc ON dc.AnswerTypeId = dt.AnswerTypeId
            INNER JOIN sv_JoinJobsAndIntakes ij_dc ON ij_dc.DateCodeId = dc.CodeId
            INNER JOIN id_Job ij ON ij.JobId = ij_dc.JobId
            WHERE dt.SurveyId = sv.sv_id
              AND dt.AnswerTypeDescr = 'date'
              AND dc.Code = 1
          ) AS dc
          CROSS APPLY (
            SELECT COUNT(sp.PartId) AS ParticipantCount, COUNT(sp.Completed) AS CompletedCount
            FROM sv_360_Participants sp
            WHERE sp.SurveyId = sv.sv_id
          ) AS sp
          WHERE {whereSQL.Join(" AND ")}",
          dr => {
            surveyInfo = new SurveyInfo(
              surveyId: dr.GetInt(DbTable.Surveys.IdentityColumn),
              surveyUID: dr.GetString("sv_uniqueid"),
              surveyName: dr.GetString("sv_title"),
              programJobId: dr.GetIntOrNull("JobId"),
              programName: dr.GetString("JobName"),
              companyId: dr.GetInt("SvCompanyId"),
              sectorId: dr.GetIntOrNull("sv_SectorId"),
              createdUtc: dr.GetDateTime("sv_createdUTC"),
              createdByUserId: dr.GetIntOrNull("CreatedByPortalUserId"),
              surveyTimeZone: TimeHelper.GetTimeZoneInstanceOrDefaultAusWST(dr.GetString("SurveyTimeZoneId")),
              closeDateLocal: dr.GetDateTimeOrNull("IntakeCloseDateSelf") ?? dr.GetDateTime("sv_closedate"),
              launchDate: dr.GetDateTimeOrNull("sv_sent"), // sv_sent is "Launch Date".
              reportType: dr.GetString("sv_ReportType"),
              participantCount: dr.GetInt("ParticipantCount"),
              completedCount: dr.GetInt("CompletedCount"),
              groupReportAvailable: dr.GetBoolFromInt("GroupReportAvailable"),
              invitationEmailSubject_Self: dr.GetString("sv_SelfEmailSubject"),
              invitationEmailBodyTemplate_Self: dr.GetString("sv_SelfEmailText"),
              invitationEmailSubject_Rater: "",
              invitationEmailBodyTemplate_Rater: "",
              orgReportDisableDrivers: dr.GetBoolFromInt("OrgReportDisableDrivers"),
              orgReportMinResponsesToShow: dr.GetIntOrNull("OrgReportMinResponsesToShow"),
              orgAltDirectorateTitle: dr.GetString("OrgAltDirectorateTitle")
            );
          },
          Common.NewSqlParameter("SurveyId", surveyId),
          Common.NewSqlParameter("SurveyUID", surveyUID),
          Common.NewSqlParameter("OrgId", orgId)
        );

        return surveyInfo;
      }

      public static List<SurveyListItem> GetSurveysForCompany(int companyId) {

        var surveys = new List<SurveyListItem>();

        Common.Query($@"
          SELECT sv.sv_id, sv_uniqueid, sv.sv_OrgId, sv.sv_ReportType, sv.SvCompanyId, sv.sv_SectorId,
            sv.sv_createdUTC, sv.CreatedByPortalUserId, sv.ProgramJobId,
            sv.sv_title, sv.sv_sent, sv.sv_closedate, sv.SurveyTimeZoneId, sv.GroupReportAvailable,
            sv.OrgAltDirectorateTitle, sv.OrgReportMinResponsesToShow,
            dc.IntakeCloseDateSelf, dc.JobId, dc.JobName,
            sp.ParticipantCount, sp.CompletedCount
          FROM sv_Survey sv
          LEFT OUTER JOIN id_Job ij ON ij.JobId = sv.ProgramJobId
          OUTER APPLY (
            SELECT TOP 1 dc.IntakeCloseDateSelf, ij.JobId, ij.JobName
            FROM sv_360_AnswerTypes dt
            INNER JOIN sv_360_Codes dc ON dc.AnswerTypeId = dt.AnswerTypeId
            INNER JOIN sv_JoinJobsAndIntakes ij_dc ON ij_dc.DateCodeId = dc.CodeId
            INNER JOIN id_Job ij ON ij.JobId = ij_dc.JobId
            WHERE dt.SurveyId = sv.sv_id
              AND dt.AnswerTypeDescr = 'date'
              AND dc.Code = 1
          ) AS dc
          CROSS APPLY (
            SELECT COUNT(sp.PartId) AS ParticipantCount, COUNT(sp.Completed) AS CompletedCount
            FROM sv_360_Participants sp
            WHERE sp.SurveyId = sv.sv_id
          ) AS sp
          WHERE sv.SvCompanyId = @CompanyId
          AND {AlbertSurveys.SQLWhereConditions.IOSSurveys("sv")}
          ORDER BY sv.sv_createdUTC DESC;",
          dr => {
            surveys.Add(new SurveyListItem(
              surveyId: dr.GetInt(DbTable.Surveys.IdentityColumn),
              surveyUID: dr.GetString("sv_uniqueid"),
              surveyName: dr.GetString("sv_title"),
              programJobId: dr.GetIntOrNull("JobId"),
              programName: dr.GetString("JobName"),
              companyId: dr.GetInt("SvCompanyId"),
              sectorId: dr.GetIntOrNull("sv_SectorId"),
              createdUtc: dr.GetDateTime("sv_createdUTC"),
              createdByUserId: dr.GetIntOrNull("CreatedByPortalUserId"),
              surveyTimeZone: TimeHelper.GetTimeZoneInstanceOrDefaultAusWST(dr.GetString("SurveyTimeZoneId")),
              closeDateLocal: dr.GetDateTimeOrNull("IntakeCloseDateSelf") ?? dr.GetDateTime("sv_closedate"),
              launchDate: dr.GetDateTimeOrNull("sv_sent"), // sv_sent is "Launch Date".
              reportType: dr.GetString("sv_ReportType"),
              groupReportAvailable: dr.GetBoolFromInt("GroupReportAvailable"),
              orgReportMinResponsesToShow: dr.GetIntOrNull("OrgReportMinResponsesToShow"),
              orgAltDirectorateTitle: dr.GetString("OrgAltDirectorateTitle"),
              participantCount: dr.GetInt("ParticipantCount"),
              completedCount: dr.GetInt("CompletedCount")
            ));
          },
          Common.NewSqlParameter("CompanyId", companyId)
        );

        return surveys;
      }

      public static List<LatestCompletedSurveys> GetLatestCompletedSurveys(int companyId, int maxPreviousSurveys) {

        // Get latest completed surveys for this company (i.e. more than x participants completed)
        // TODO: 1. Cache the ids in coachee record.
        //   OR: 2. New column in Survey table "Completed" (datetime?) which is set when enough raters have submitted surveys.
        // ALSO: Store the "survey month" in Survey record (DATE of yyyy.mm.01), to make grouping by month easier.
        //
        // Feb-2019: Note this function may not be needed anymore due to the new back-link method
        //           which also obtains previous Org survey IDs.

        // First in list (index 0) is latest survey id,
        // 2nd in list (index 1) is previous survey to that,
        // etc. to maximum of 4 surveys.
        var surveys = new List<LatestCompletedSurveys>();

        Common.Query($@"
          SELECT TOP {maxPreviousSurveys + 1} s.sv_id, s.sv_title, s.sv_ReportTitle, s.AltYearTitle
          FROM sv_Survey s
          INNER JOIN sv_360_Participants p ON s.sv_id = p.SurveyId AND p.Completed IS NOT NULL
          WHERE s.SvCompanyId = @CompanyId
            AND s.sv_ReportType = @ReportType_IOS
            AND s.AltYearTitle > ''
          GROUP BY s.sv_id, s.sv_title, s.sv_ReportTitle, s.AltYearTitle, s.sv_createdUTC
          HAVING COUNT(p.Completed) > @MinCompleted
          ORDER BY s.sv_createdUTC DESC",
          dr => {
            surveys.Add(new LatestCompletedSurveys(
              dr.GetInt("sv_id"),
              dr.GetString("sv_title"),
              dr.GetString("AltYearTitle")
            ));
          },
          Common.NewSqlParameter("CompanyId", companyId),
          Common.NewSqlParameter("ReportType_IOS", ConfigHelper.ReportTypes.IOS),
          Common.NewSqlParameter("MinCompleted", Min_Survey_Participants_Completed)
        );

        return surveys;
      }

      private static int GetResponsePercent(int participantCount, int completedCount) {
        return (int)Math.Floor((double)completedCount / participantCount * 100); // Floor so it doesn't round up to 100.
      }

      private static bool GetIsClosed(DateTime CloseDateLocal, TimeZoneInfo surveyTimeZone) {
        return DateTime.UtcNow >= CloseDateLocal.ToUniversalTime(surveyTimeZone);
      }

      public class SurveyInfo {

        // Note that CloseDate and LaunchDate are in survey-local time, not UTC.

        public int SurveyId;
        public string SurveyUID;
        public string SurveyName;
        public int? ProgramJobId { get; }
        public string ProgramName { get; }
        public int CompanyId;
        public int? SectorId;
        public DateTime CreatedUtc;
        public int? CreatedByUserId;
        public DateTime? LaunchDate;
        public DateTime CloseDateLocal;
        public TimeZoneInfo SurveyTimeZone;
        public string ReportType;
        public int ParticipantCount;
        public int CompletedCount;
        public string InvitationEmailSubject_Self;
        public string InvitationEmailBodyTemplate_Self;
        public string InvitationEmailSubject_Rater;
        public string InvitationEmailBodyTemplate_Rater;
        public bool GroupReportAvailable;
        public string OrgAltDirectorateTitle;
        public bool OrgReportDisableDrivers;
        public int? OrgReportMinResponsesToShow;

        private int _responsePercent;
        private bool _isClosed;

        public SurveyInfo(
          int surveyId, string surveyUID, string surveyName,
          int? programJobId, string programName,
          int companyId, int? sectorId, DateTime createdUtc, int? createdByUserId,
          TimeZoneInfo surveyTimeZone, DateTime closeDateLocal, DateTime? launchDate,
          int participantCount, int completedCount,
          string reportType, bool groupReportAvailable,
          string invitationEmailSubject_Self, string invitationEmailBodyTemplate_Self,
          string invitationEmailSubject_Rater, string invitationEmailBodyTemplate_Rater,
          bool orgReportDisableDrivers, int? orgReportMinResponsesToShow,
          string orgAltDirectorateTitle) {

          this.SurveyId = surveyId;
          this.SurveyUID = surveyUID;
          this.SurveyName = surveyName;
          this.ProgramJobId = programJobId;
          this.ProgramName = programName;
          this.CompanyId = companyId;
          this.SectorId = sectorId;
          this.CreatedUtc = createdUtc;
          this.CreatedByUserId = createdByUserId;
          this.CloseDateLocal = closeDateLocal;
          this.LaunchDate = launchDate;
          this.SurveyTimeZone = surveyTimeZone;
          this.ReportType = reportType;
          this.GroupReportAvailable = groupReportAvailable;
          this.InvitationEmailSubject_Self = invitationEmailSubject_Self;
          this.InvitationEmailBodyTemplate_Self = invitationEmailBodyTemplate_Self;
          this.InvitationEmailSubject_Rater = invitationEmailSubject_Rater;
          this.InvitationEmailBodyTemplate_Rater = invitationEmailBodyTemplate_Rater;
          this.OrgAltDirectorateTitle = orgAltDirectorateTitle;
          this.OrgReportDisableDrivers = orgReportDisableDrivers;
          this.OrgReportMinResponsesToShow = orgReportMinResponsesToShow;
          this.ParticipantCount = participantCount;
          this.CompletedCount = completedCount;

          _isClosed = GetIsClosed(closeDateLocal, surveyTimeZone);
          _responsePercent = GetResponsePercent(participantCount, completedCount);
        }

        public string GetDirectorateTitle() {
          return OrgAltDirectorateTitle.IsNullOrEmpty() ? Org_Default_Directorate_Title : OrgAltDirectorateTitle;
        }

        public int ResponsePercent => _responsePercent;

        public bool IsClosed => _isClosed;
      }

      public class SurveyListItem {

        // Note that CloseDate and LaunchDate are in survey-local time, not UTC.

        public int SurveyId { get; }
        public string SurveyUID { get; }
        public string SurveyName { get; }
        public int? ProgramJobId { get; }
        public string ProgramName { get; }
        public int CompanyId { get; }
        public int? SectorId { get; }
        public DateTime CreatedUtc { get; }
        public int? CreatedByUserId { get; }
        public DateTime? LaunchDate { get; }
        public DateTime CloseDateLocal { get; }
        public TimeZoneInfo SurveyTimeZone { get; }
        public string ReportType { get; }
        public bool GroupReportAvailable { get; }
        public string OrgAltDirectorateTitle { get; }
        public int? OrgReportMinResponsesToShow { get; }
        public int ParticipantCount { get; }
        public int CompletedCount { get; }

        private int _responsePercent;
        private bool _isClosed;

        public SurveyListItem(
          int surveyId, string surveyUID, string surveyName,
          int? programJobId, string programName,
          int companyId, int? sectorId,
          DateTime createdUtc, int? createdByUserId,
          TimeZoneInfo surveyTimeZone, DateTime closeDateLocal, DateTime? launchDate,
          string reportType, bool groupReportAvailable, int? orgReportMinResponsesToShow,
          string orgAltDirectorateTitle, int participantCount, int completedCount
        ) {
          this.SurveyId = surveyId;
          this.SurveyUID = surveyUID;
          this.SurveyName = surveyName;
          this.ProgramJobId = programJobId;
          this.ProgramName = programName;
          this.CompanyId = companyId;
          this.SectorId = sectorId;
          this.CreatedUtc = createdUtc;
          this.CreatedByUserId = createdByUserId;
          this.CloseDateLocal = closeDateLocal;
          this.LaunchDate = launchDate;
          this.SurveyTimeZone = surveyTimeZone;
          this.ReportType = reportType;
          this.GroupReportAvailable = groupReportAvailable;
          this.OrgAltDirectorateTitle = orgAltDirectorateTitle;
          this.OrgReportMinResponsesToShow = orgReportMinResponsesToShow;
          this.ParticipantCount = participantCount;
          this.CompletedCount = completedCount;

          _isClosed = GetIsClosed(closeDateLocal, surveyTimeZone);
          _responsePercent = GetResponsePercent(participantCount, completedCount);
        }

        public string GetDirectorateTitle() {
          return OrgAltDirectorateTitle.IsNullOrEmpty() ? Org_Default_Directorate_Title : OrgAltDirectorateTitle;
        }

        public int ResponsePercent => _responsePercent;

        public bool IsClosed => _isClosed;
      }

      public class LatestCompletedSurveys {
        public int SurveyId { get; private set; }
        public string SurveyTitle { get; private set; }
        public string SurveyYear { get; private set; }
        public LatestCompletedSurveys(
          int surveyId,
          string surveyTitle,
          string surveyYear
        ) {
          SurveyId = surveyId;
          SurveyTitle = surveyTitle;
          SurveyYear = surveyYear;
        }
      }
    }
  }
}
