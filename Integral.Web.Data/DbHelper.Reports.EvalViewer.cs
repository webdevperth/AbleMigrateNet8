using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Reports {

      public class EvalViewer {

        public class SqlConditions {
          public string SurveyConditionSQL { get; internal set; } = "";
          public string HeadingConditionSQL { get; internal set; } = "";
          public string SurveyAndHeadingConditionSQL { get; internal set; } = "";
        }

        public static SqlConditions GetSQLConditionsForReport(ConfigHelper.EvalType evalType) {

          var rtn = new SqlConditions();

          if (evalType == ConfigHelper.EvalType.Coaching) {

            rtn.SurveyConditionSQL = $"sv.ClonedFromSvId IN({ConfigHelper.EvalScoreTemplateSurveyIds.Coaching.ToStringList()})";
            rtn.HeadingConditionSQL = $"gqh.RptQnGrpHeadingId IN({ConfigHelper.EvalScoreReportHeadingIds.Coaching.ToStringList()})";

          } else if (evalType == ConfigHelper.EvalType.Workshop) {

            rtn.SurveyConditionSQL = $"(sv.ClonedFromSvId IN({ConfigHelper.EvalScoreTemplateSurveyIds.Workshops.ToStringList()}) OR sv.WorkshopEventId IS NOT NULL)";
            rtn.HeadingConditionSQL = $"gqh.RptQnGrpHeadingId IN({ConfigHelper.EvalScoreReportHeadingIds.Workshops.ToStringList()})";

          } else if (evalType == ConfigHelper.EvalType.PostProgram) {

            rtn.SurveyConditionSQL = $"sv.ClonedFromSvId IN({ConfigHelper.EvalScoreTemplateSurveyIds.EndProgram.ToStringList()})";
            rtn.HeadingConditionSQL = $"gqh.RptQnGrpHeadingId IN({ConfigHelper.EvalScoreReportHeadingIds.Program.ToStringList()})";
          }

          rtn.SurveyAndHeadingConditionSQL = rtn.SurveyConditionSQL.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + rtn.HeadingConditionSQL;

          return rtn;
        }

        public class ProgramSelectItem {

          public DateTime? StartDateUtc { get; private set; }
          public int ProgramJobId { get; private set; }
          public string JobName { get; private set; }
          public int? SurveyCount { get; private set; }

          public ProgramSelectItem(DateTime? startDateUtc, int programJobId, string jobName, int? surveyCount = null) {
            StartDateUtc = startDateUtc;
            ProgramJobId = programJobId;
            JobName = jobName;
            SurveyCount = surveyCount;
          }
        }

        public static List<ProgramSelectItem> GetProgramSelectList(Projects.ProjectInfo projectInfo) {

          var programList = new List<ProgramSelectItem>(0);

          Common.Query($@"
            SELECT j.JobId, j.JobName, j.AbleProgramStartDateUtc, svs.SurveyCount
            FROM id_Job j
            CROSS APPLY (
              SELECT COUNT(DISTINCT sp.IntakeCodeId) AS SurveyCount
              FROM sv_360_Participants sp
              INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
              INNER JOIN sv_Survey svt ON svt.sv_id = sv.ClonedFromSvId
              WHERE sp.AbleProgramJobId = j.JobId
                AND sp.Completed IS NOT NULL
                AND {AlbertSurveys.SQLWhereConditions.EvalSurveys("sv")}
                AND EXISTS (SELECT 1 FROM sv_360_Questions sq WHERE sq.SurveyId = sp.SurveyId AND sq.RptQnGroupId = @RptQnGroupId)
            ) AS svs
            WHERE j.JobNumber = @JobNumber
              AND svs.SurveyCount > 0
            ORDER BY j.JobName;",
            dr => {
              programList.Add(new ProgramSelectItem(
                dr.GetDateTimeOrNull("AbleProgramStartDateUtc"),
                dr.GetInt("JobId"),
                dr.GetString("JobName"),
                dr.GetIntOrNull("SurveyCount")
              ));
            },
            Common.NewSqlParameter("JobNumber", projectInfo.JobNumber),
            Common.NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_EvalViewer)
          );

          return programList;
        }

        public class SurveySelectItem {
          public int SurveyTemplateId { get; internal set; }
          public int IntakeCodeId { get; internal set; }
          public string IntakeName { get; internal set; }
          public DateTime IntakeCloseDateLocal { get; internal set; }
          public bool HasTextAnswers { get; internal set; }
          public string SurveyTitle { get; internal set; }
          public string SurveyPublicTitle { get; internal set; }
        }

        public static List<SurveySelectItem> GetSurveySelectList(string projectJobNumber, List<int> programJobIdList, ConfigHelper.EvalType evalType) {

          var surveyList = new List<SurveySelectItem>();

          var sqlConditions = GetSQLConditionsForReport(evalType);

          if (projectJobNumber.IsNullOrEmpty() || programJobIdList.IsNullOrEmpty()) return surveyList;

          Common.Query($@"
            WITH selectedJobIds
            AS
            (
              SELECT Value AS JobId
              FROM STRING_SPLIT(@ProgramJobIdString, ',')
            )
            SELECT sv.sv_id, sv.ClonedFromSvId, sv.sv_title, sv.sv_360GroupName,
              intake.CodeId AS IntakeCodeId, intake.IntakeCloseDate, intake.value AS IntakeName,
              IIF(MIN(sta.TextAnswerId) IS NULL, 0, 1) AS HasTextAnswers,
              we.WorkshopEventId, we.WorkshopTitle, we.EvalFirstOrLast, sv.WorkshopEvalFirstOrLast,
              ij.JobId, ij.JobName
            FROM sv_360_Participants sp
            INNER JOIN selectedJobIds selj ON selj.JobId = sp.AbleProgramJobId
            INNER JOIN id_Job ij ON ij.JobId = sp.AbleProgramJobId
            INNER JOIN sv_360_Codes intake ON sp.IntakeCodeId = intake.CodeId
            INNER JOIN sv_360_AnswerTypes sat ON intake.AnswerTypeId = sat.AnswerTypeId
            INNER JOIN sv_Survey sv ON sv.sv_id = sat.SurveyId
            INNER JOIN sv_Survey svt ON svt.sv_id = sv.ClonedFromSvId
            LEFT OUTER JOIN ev_WorkshopEvent we ON sv.WorkshopEventId = we.WorkshopEventId
            OUTER APPLY (
              SELECT TOP 1 sta.TextAnswerId
              FROM sv_Answers sa
              INNER JOIN sv_360_TextAnswers sta ON sta.AnswerId = sa.AnswerId
              WHERE sa.ParticipantId = sp.PartId
                AND LEN(CAST(sta.ta_textanswer AS VARCHAR(20))) > 10
            ) AS sta
            WHERE ij.JobNumber = @JobNumber
              AND {AlbertSurveys.SQLWhereConditions.EvalSurveys("sv")}
              {sqlConditions.SurveyConditionSQL.EnsureStartsWith("AND ", true)}
              AND intake.IntakeTotalCompleted > 0
              AND EXISTS (
                SELECT 1
                FROM sv_360_Questions sq
                WHERE sq.SurveyId = sv.sv_id
                AND sq.RptQnGroupId = @RptQnGroupId
              )
            GROUP BY
              sv.sv_id, sv.ClonedFromSvId, sv.sv_title, sv.sv_360GroupName,
              intake.CodeId, intake.IntakeCloseDate, intake.value,
              we.WorkshopEventId, we.WorkshopTitle, we.EvalFirstOrLast, sv.WorkshopEvalFirstOrLast,
              ij.JobId, ij.JobName
            ORDER BY intake.IntakeCloseDate DESC;",
            dr => {
              surveyList.Add(
                new SurveySelectItem() {
                  SurveyTemplateId = dr.GetInt("ClonedFromSvId"),
                  IntakeCodeId = dr.GetInt("IntakeCodeId"),
                  IntakeName = dr.GetString("IntakeName"),
                  IntakeCloseDateLocal = dr.GetDateTime("IntakeCloseDate"),
                  HasTextAnswers = dr.GetBoolFromInt("HasTextAnswers"),
                  SurveyTitle = dr.GetString("sv_title"),
                  SurveyPublicTitle = dr.GetString("sv_360GroupName")
                }
              );
            },
            Common.NewSqlParameter("JobNumber", projectJobNumber),
            Common.NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_EvalViewer),
            Common.NewSqlParameter("ProgramJobIdString", programJobIdList.ToStringList())
          );

          return surveyList;
        }

        public static SurveyStats GetSurveyStats(string projectJobNumber, List<int> programJobIds, List<int> intakeCodeIds, ConfigHelper.EvalType evalType) {

          if (projectJobNumber.IsNullOrEmpty()) throw new ArgumentException("projectJobNumber is required");
          if (programJobIds.IsNullOrEmpty()) throw new ArgumentException("programJobIds is required");
          if (intakeCodeIds.IsNullOrEmpty()) throw new ArgumentException("intakeCodeIds is required");

          SurveyStats surveyStats = null;

          var sqlConditions = GetSQLConditionsForReport(evalType);

          Common.Query($@"
            WITH
            ProgramJobIds AS ( SELECT Value AS ProgramJobId FROM STRING_SPLIT(@ProgramJobIds,',') ),
            IntakeCodeIds AS ( SELECT Value AS IntakeCodeId FROM STRING_SPLIT(@IntakeCodeIds,',') ),
            stats AS (
              SELECT
                ap.SvCompanyId, ssc.CompanyName, ap.ProjectId
                COUNT(DISTINCT ij.JobId) AS ProgramCount,
                COUNT(DISTINCT sv.sv_id) AS SurveyCount,
                COUNT(DISTINCT sp.IntakeCodeId) AS IntakeCount,
                COUNT(DISTINCT sv.ClonedFromSvId) AS SurveyTemplateCount,
                COUNT(DISTINCT sv.PrimaryGblAnswerTypeId) AS AnswerTypeCount,
                MAX(sv.PrimaryGblAnswerTypeId) AS PrimaryGblAnswerTypeId,
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
              INNER JOIN IntakeCodeIds ic ON ic.IntakeCodeId = sp.IntakeCodeId
              INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
              INNER JOIN ProgramJobIds pjs ON pjs.ProgramJobId = sp.AbleProgramJobId
              INNER JOIN id_Job ij ON ij.JobId = sp.AbleProgramJobId
              INNER JOIN al_Project ap ON ij.JobNumber = ap.JobNumber
              INNER JOIN sv_SurveyCompany ssc ON ap.SvCompanyId = ssc.SvCompanyId
              WHERE ij.JobNumber = @ProjectJobNumber
                AND {AlbertSurveys.SQLWhereConditions.EvalSurveys("sv")}
                {sqlConditions.SurveyConditionSQL.EnsureStartsWith("AND ", true)}
                AND sp.Completed IS NOT NULL
                AND EXISTS (SELECT NULL FROM sv_360_Questions sq WHERE sv.sv_id = sq.SurveyId AND sq.RptQnGroupId = @RptQnGroupId)
              GROUP BY ap.SvCompanyId, ssc.CompanyName, ap.ProjectId
            )
            SELECT
              stats.SvCompanyId, stats.CompanyName, stats.ProjectId,
              stats.ProgramCount, stats.SurveyCount, stats.IntakeCount,
              stats.SurveyTemplateCount, stats.AnswerTypeCount, stats.CoacheeCount,
              stats.PrimaryGblAnswerTypeId, stats.SampleSurveyId, stats.SelfLatestCompleted,
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
            ORDER BY stats.sv_ReportType, stats.GblAnswerTypeId;",
            dr => {
              surveyStats = new SurveyStats(
                projectJobNumber: projectJobNumber,
                programJobIds: programJobIds,
                intakeCodeIds: intakeCodeIds,
                companyId: dr.GetInt("SvCompanyId"),
                companyName: dr.GetString("CompanyName"),
                projectId: dr.GetInt("ProjectId"),
                programCount: dr.GetInt("ProgramCount"),
                surveyCount: dr.GetInt("SurveyCount"),
                intakeCount: dr.GetInt("IntakeCount"),
                surveyTemplateCount: dr.GetInt("SurveyTemplateCount"),
                primaryGblAnswerTypeId: dr.GetInt("PrimaryGblAnswerTypeId"),
                answerTypeCount: dr.GetInt("AnswerTypeCount"),
                sampleSurveyId: dr.GetInt("SampleSurveyId"),
                selfLatestCompleted: dr.GetDateTimeOrNull("SelfLatestCompleted"),
                scaleMinScore: dr.GetInt("MinScore"),
                scaleMaxScore: dr.GetInt("MaxScore"),
                selfAllCount: dr.GetInt("SelfAllCount"),
                selfPreCount: dr.GetInt("SelfPreCount"),
                selfPostCount: dr.GetInt("SelfPostCount"),
                raterAllCount: dr.GetInt("RaterAllCount"),
                raterPreCount: dr.GetInt("RaterPreCount"),
                raterPostCount: dr.GetInt("RaterPostCount"),
                coacheeCount: dr.GetInt("CoacheeCount")
              );
            },
            Common.NewSqlParameter("ProjectJobNumber", projectJobNumber),
            Common.NewSqlParameter("ProgramJobIds", programJobIds.ToStringList("")),
            Common.NewSqlParameter("IntakeCodeIds", intakeCodeIds.ToStringList("")),
            Common.NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_EvalViewer)
          );

          return surveyStats;
        }

        public class SurveyStats {

          public int SvCompanyId { get; private set; }
          public string CompanyName { get; private set; }
          public int ProjectId { get; private set; }
          public string ProjectJobNumber { get; private set; }
          public List<int> ProgramJobIds { get; private set; }
          public List<int> IntakeCodeIds { get; private set; }
          public int ProgramCount { get; private set; }
          public int PrimaryGblAnswerTypeId { get; private set; }
          public int? RptQnGroupId { get; private set; }

          public int SurveyCount { get; private set; }
          public int IntakeCount { get; private set; }
          public int SurveyTemplateCount { get; private set; }
          public int AnswerTypeCount { get; private set; }
          public int SampleSurveyId { get; private set; }
          public int ScaleMinScore { get; private set; }
          public int ScaleMaxScore { get; private set; }
          public DateTime? SelfLatestCompleted { get; private set; }
          public int CoacheeCount { get; private set; }
          public int SelfAllCount { get; private set; }
          public int SelfPreCount { get; private set; }
          public int SelfPostCount { get; private set; }
          public int RaterAllCount { get; private set; }
          public int RaterPreCount { get; private set; }
          public int RaterPostCount { get; private set; }

          public bool HasSelfs => SelfAllCount > 0;
          public bool HasRaters => RaterAllCount > 0;
          public bool HasPreSurvey => SelfPreCount > 0 || RaterPreCount > 0;

          internal SurveyStats(

            int companyId, string companyName, int projectId, string projectJobNumber, List<int> programJobIds, List<int> intakeCodeIds,
            int programCount, int primaryGblAnswerTypeId,
            int surveyCount, int intakeCount, int surveyTemplateCount, int answerTypeCount,
            int sampleSurveyId, int scaleMinScore, int scaleMaxScore,
            int coacheeCount, DateTime? selfLatestCompleted,
            int selfAllCount, int selfPreCount, int selfPostCount,
            int raterAllCount, int raterPreCount, int raterPostCount) {

            SvCompanyId = companyId;
            CompanyName = companyName;
            ProjectId = projectId;
            ProjectJobNumber = projectJobNumber;
            ProgramJobIds = programJobIds;
            IntakeCodeIds = intakeCodeIds;
            ProgramCount = programCount;
            PrimaryGblAnswerTypeId = primaryGblAnswerTypeId;
            SurveyCount = surveyCount;
            IntakeCount = intakeCount;
            SurveyTemplateCount = surveyTemplateCount;
            AnswerTypeCount = answerTypeCount;
            SampleSurveyId = sampleSurveyId;
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
