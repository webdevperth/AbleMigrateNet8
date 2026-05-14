using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Reports {

      public class SkillsViewer {

        // Return info & stats for surveys matching the given criteria.
        // Note that for reports, usually only 1 result is desired (i.e. survey selection criteria is sufficiently precise).
        public static List<SurveyStats> GetSurveyStatsByType(string projectJobNumber, List<int> programJobIdsOrNullForAll) {

          if (projectJobNumber.IsNullOrEmpty()) throw new ArgumentException("projectJobNumber is required");

          var statsList = new List<SurveyStats>();

          Common.Query($@"
            WITH
              {(!programJobIdsOrNullForAll.IsNullOrEmpty() // Create ProgramJobIds CTE only if there is a ProgramJobId selection.
              ? "ProgramJobIds AS (SELECT Value AS ProgramJobId FROM STRING_SPLIT(@ProgramJobIds, ',')),"
              : "")}
            stats AS (
              SELECT
                  ap.SvCompanyId,
                  ssc.CompanyName,
                  ap.ProjectId,
                  sv.SurveyTypeCode,
                  gat.GblAnswerTypeId, gat.GblAnswerTypeDescr,
                COUNT(DISTINCT ij.JobId) AS ProgramCount,
                COUNT(DISTINCT sv.sv_id) AS SurveyCount,
                MAX(sv.sv_id) AS SampleSurveyId,
                MAX(IIF(sp.IsSelf = 1, sp.Completed, NULL)) AS SelfLatestCompleted,
                COUNT(DISTINCT sp.AbleCoacheeId) AS CoacheeCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 1, sp.PartId, NULL)) AS SelfAllCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 1 AND sp.IsSelfPreSurvey = 1, sp.PartId, NULL)) AS SelfPreCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 1 AND sp.IsSelfPostSurvey = 1, sp.PartId, NULL)) AS SelfPostCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 0, sp.PartId, NULL)) AS RaterAllCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 0 AND sp.IsSelfPreSurvey = 1, sp.PartId, NULL)) AS RaterPreCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 0 AND sp.IsSelfPostSurvey = 1, sp.PartId, NULL)) AS RaterPostCount
              FROM sv_360_Participants sp WITH (NOLOCK)
              INNER JOIN id_Job ij ON ij.JobId = sp.AbleProgramJobId
              {(!programJobIdsOrNullForAll.IsNullOrEmpty() // Join to ProgramJobIds CTE if included.
                ? "INNER JOIN ProgramJobIds pjs ON pjs.ProgramJobId = ij.JobId"
                : "")}
              INNER JOIN al_Project ap ON ij.JobNumber = ap.JobNumber
              INNER JOIN sv_SurveyCompany ssc ON ap.SvCompanyId = ssc.SvCompanyId
              INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
              INNER JOIN sv_GblAnswerTypes gat ON sv.PrimaryGblAnswerTypeId = gat.GblAnswerTypeId
              WHERE ij.JobNumber = @ProjectJobNumber
                AND {AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
                AND sp.Completed IS NOT NULL
              GROUP BY ap.SvCompanyId, ssc.CompanyName, ap.ProjectId, sv.SurveyTypeCode, gat.GblAnswerTypeId, gat.GblAnswerTypeDescr
            )
            SELECT
              stats.SvCompanyId, stats.CompanyName, stats.ProjectId, stats.SurveyTypeCode, stats.GblAnswerTypeId,
              stats.GblAnswerTypeDescr, stats.ProgramCount, stats.SurveyCount, stats.SampleSurveyId,
              stats.SelfLatestCompleted, stats.CoacheeCount,
              stats.SelfAllCount, stats.SelfPreCount, stats.SelfPostCount,
              stats.RaterAllCount, stats.RaterPreCount, stats.RaterPostCount,
              sc.MinScore, sc.MaxScore
            FROM stats
            CROSS APPLY (
              SELECT MIN(sc.ValueScore) AS MinScore,
                     MAX(sc.ValueScore) AS MaxScore
              FROM sv_360_AnswerTypes sat
              INNER JOIN sv_360_Codes sc ON sat.AnswerTypeId = sc.AnswerTypeId
              WHERE sat.SurveyId = stats.SampleSurveyId
                AND sat.GblAnswerTypeId = stats.GblAnswerTypeId
            ) AS sc
            ORDER BY stats.SurveyTypeCode, stats.GblAnswerTypeId;",
            dr => {
              string surveyTypeCode = dr.GetString("SurveyTypeCode");
              statsList.Add(new SurveyStats(
                companyId: dr.GetInt("SvCompanyId"),
                companyName: dr.GetString("CompanyName"),
                projectId: dr.GetInt("ProjectId"),
                projectJobNumber: projectJobNumber,
                programJobIds: programJobIdsOrNullForAll,
                programCount: dr.GetInt("ProgramCount"),
                surveyTypeCode: surveyTypeCode,
                surveyReportType: surveyTypeCode == ConfigHelper.SurveyTypeCodes.Eval ? ConfigHelper.ReportTypes.Eval : ConfigHelper.ReportTypes.Able360,
                primaryGblAnswerTypeId: dr.GetInt("GblAnswerTypeId"),
                gblAnswerTypeName: dr.GetString("GblAnswerTypeDescr"),
                surveyCount: dr.GetInt("SurveyCount"),
                sampleSurveyId: dr.GetInt("SampleSurveyId"),
                scaleMinScore: dr.GetInt("MinScore"),
                scaleMaxScore: dr.GetInt("MaxScore"),
                selfLatestCompleted: dr.GetDateTimeOrNull("SelfLatestCompleted"),
                selfAllCount: dr.GetInt("SelfAllCount"),
                selfPreCount: dr.GetInt("SelfPreCount"),
                selfPostCount: dr.GetInt("SelfPostCount"),
                raterAllCount: dr.GetInt("RaterAllCount"),
                raterPreCount: dr.GetInt("RaterPreCount"),
                raterPostCount: dr.GetInt("RaterPostCount"),
                coacheeCount: dr.GetInt("CoacheeCount")
              ));
            },
            Common.NewSqlParameter("ProjectJobNumber", projectJobNumber),
            Common.NewSqlParameter("ProgramJobIds", programJobIdsOrNullForAll.ToStringList(""))
          );

          return statsList;
        }

        public static List<ProjectSelectInfo> GetAllProjectsSelectList() {
          return GetProjectSelectList(null);
        }

        public static List<ProjectSelectInfo> GetProjectSelectList(List<string> projectJobNumbers) {

          var pl = new List<ProjectSelectInfo>();

          Common.Query($@"
            {(projectJobNumbers.IsNullOrEmpty() ? "" : "WITH JobNumbers AS (SELECT Value AS JobNumber FROM STRING_SPLIT(@ProjectJobNumbers, ','))")}
            SELECT
              ap.JobNumber, ap.ProjectName, ap.FriendlyProjectTitle,
              sc.SvCompanyId, sc.CompanyName,
              COUNT(DISTINCT sp.IntakeCodeId) AS IntakeCount
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
            INNER JOIN id_Job ij ON sp.AbleProgramJobId = ij.JobId
            INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber
            {(projectJobNumbers.IsNullOrEmpty() ? "" : "INNER JOIN JobNumbers jn ON ap.JobNumber = jn.JobNumber")}
            INNER JOIN sv_SurveyCompany sc ON sc.SvCompanyId = ap.SvCompanyId
            WHERE {AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
              AND sp.IsSelf = 1
            GROUP BY ap.JobNumber, ap.ProjectName, ap.FriendlyProjectTitle, sc.SvCompanyId, sc.CompanyName
            HAVING COUNT(sp.Completed) >= @MinSelfResponses
            ORDER BY ap.JobNumber",
            dr => {
              pl.Add(new ProjectSelectInfo(
                dr.GetString("JobNumber"),
                dr.GetString("ProjectName"),
                dr.GetString("FriendlyProjectTitle"),
                dr.GetInt("SvCompanyId"),
                dr.GetString("CompanyName"),
                dr.GetIntOrNull("IntakeCount")
              ));
            },
            Common.NewSqlParameter("ProjectJobNumbers", projectJobNumbers.Join(",")),
            Common.NewSqlParameter("MinSelfResponses", ConfigHelper.Reports_SkillsViewer_MinSelfResponses)
          );

          return pl;
        }

        public static List<ProgramListInfo> GetProgramSelectList(DbHelper.Projects.ProjectInfo projectInfo) {

          var pl = new List<ProgramListInfo>(0);

          Common.Query($@"
            SELECT
              j.JobId, j.JobName, j.AbleProgramStartDateUtc,
              COUNT(DISTINCT sp.IntakeCodeId) AS IntakeCount
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            INNER JOIN id_Job j ON sp.AbleProgramJobId = j.JobId
            INNER JOIN al_Project ap ON j.JobNumber = ap.JobNumber
            WHERE {AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
              AND sp.IsSelf = 1
              AND j.JobNumber = @JobNumber
            GROUP BY j.JobId, j.JobName, j.AbleProgramStartDateUtc
            HAVING COUNT(sp.Completed) >= @MinSelfResponses
            ORDER BY j.JobName;",
            dr => {
              pl.Add(new ProgramListInfo(
                dr.GetDateTimeOrNull("AbleProgramStartDateUtc"),
                dr.GetInt("JobId"),
                dr.GetString("JobName"),
                dr.GetIntOrNull("IntakeCount")
              ));
            },
            Common.NewSqlParameter("JobNumber", projectInfo.JobNumber),
            Common.NewSqlParameter("MinSelfResponses", ConfigHelper.Reports_SkillsViewer_MinSelfResponses)
          );

          return pl;
        }

        public class SQLWhereConditions {

          public static string Completed360Skills(string surveyTableAlias, string participantTableAlias, string questionTableAlias) {
            return $@"{AlbertSurveys.SQLWhereConditions.Standard360Surveys(surveyTableAlias)}
            AND {participantTableAlias}.Completed IS NOT NULL
            AND {questionTableAlias}.GblAnswerTypeId = {surveyTableAlias}.PrimaryGblAnswerTypeId
            AND {questionTableAlias}.RptQnGroupId = {ConfigHelper.RptQnGroupId_SkillsViewer}";
          }
        }

        public class ProjectSelectInfo {

          public string ProjectJobNumber { get; private set; }
          public string ProjectName { get; private set; }
          public string ProjectFriendlyTitle { get; private set; }
          public int SvCompanyId { get; private set; }
          public string CompanyName { get; private set; }
          public int? IntakeCount { get; private set; }

          public ProjectSelectInfo(string projectJobNumber, string projectName, string projectFriendlyTitle, int svCompanyId, string companyName, int? intakeCount = null) {
            ProjectJobNumber = projectJobNumber;
            ProjectName = projectName;
            ProjectFriendlyTitle = projectFriendlyTitle;
            SvCompanyId = svCompanyId;
            CompanyName = companyName;
            IntakeCount = intakeCount;
          }
        }

        public class ProgramListInfo {

          public DateTime? StartDateUtc { get; private set; }
          public int ProgramJobId { get; private set; }
          public string JobName { get; private set; }
          public int? IntakeCount { get; private set; }

          public ProgramListInfo(DateTime? startDateUtc, int programJobId, string jobName, int? intakeCount = null) {
            StartDateUtc = startDateUtc;
            ProgramJobId = programJobId;
            JobName = jobName;
            IntakeCount = intakeCount;
          }
        }

        public class SurveyStats {

          public int SvCompanyId { get; private set; }
          public string CompanyName { get; private set; }
          public int ProjectId { get; private set; }
          public string ProjectJobNumber { get; private set; }
          public List<int> ProgramJobIds { get; private set; }
          public int ProgramCount { get; private set; }
          public string ReportType { get; private set; }
          public int PrimaryGblAnswerTypeId { get; private set; }
          public string GblAnswerTypeName { get; private set; }
          public int? RptQnGroupId { get; private set; }
          public string SurveyTypeCode { get; private set; }

          public int SurveyCount { get; private set; }
          public int SampleSurveyId { get; private set; }
          public int ScaleMinScore { get; private set; }
          public int ScaleMaxScore { get; private set; }
          public DateTime? SelfLatestCompleted { get; private set; }
          public int CoacheeCount { get; private set; }
          public int SelfAllCount { get; private set; }
          public int SelfPreCount { get; set; }
          public int SelfPostCount { get; set; }
          public int RaterAllCount { get; private set; }
          public int RaterPreCount { get; set; }
          public int RaterPostCount { get; set; }

          public bool HasSelfs => SelfAllCount > 0;
          public bool HasRaters => RaterAllCount > 0;
          public bool HasPreSurvey => SelfPreCount > 0 || RaterPreCount > 0;
          public bool HasPrePost => SelfPreCount > 0 && SelfPostCount > 0;

          internal SurveyStats(

            int companyId, string companyName, int projectId, string projectJobNumber, List<int> programJobIds, int programCount,
            string surveyTypeCode, string surveyReportType, int primaryGblAnswerTypeId, string gblAnswerTypeName,

            int surveyCount, int sampleSurveyId,
            int scaleMinScore, int scaleMaxScore,
            int coacheeCount, DateTime? selfLatestCompleted,
            int selfAllCount, int selfPreCount, int selfPostCount,
            int raterAllCount, int raterPreCount, int raterPostCount) {

            SvCompanyId = companyId;
            CompanyName = companyName;
            ProjectId = projectId;
            ProjectJobNumber = projectJobNumber;
            ProgramJobIds = programJobIds.NullIfEmpty();
            ProgramCount = programCount;
            SurveyTypeCode = surveyTypeCode;
            ReportType = surveyReportType;
            PrimaryGblAnswerTypeId = primaryGblAnswerTypeId;
            GblAnswerTypeName = gblAnswerTypeName;
            SurveyCount = surveyCount;
            SampleSurveyId = sampleSurveyId;
            RptQnGroupId = ConfigHelper.RptQnGroupId_SkillsViewer;
            ScaleMinScore = scaleMinScore;
            ScaleMaxScore = scaleMaxScore;
            CoacheeCount = coacheeCount;
            SelfLatestCompleted = selfLatestCompleted;
            SelfAllCount = selfAllCount;
            SelfPreCount = selfPreCount;
            SelfPostCount = selfPostCount;
            RaterAllCount = raterAllCount;
            RaterPreCount = raterPreCount;
            RaterPostCount = raterPostCount;
          }

          internal void SetMinMaxScore(int minScore, int maxScore) {
            ScaleMinScore = minScore;
            ScaleMaxScore = maxScore;
          }
        }

      }
    }
  }
}
