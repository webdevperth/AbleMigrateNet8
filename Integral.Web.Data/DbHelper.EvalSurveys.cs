using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class EvalSurveys {

      public static List<WorkshopEvalCompletedNotification> GetWorkshopEvalCompletedNotifications(DateTime closeDateFromUtc, int minCompleted) {

        var result = new List<WorkshopEvalCompletedNotification>();
        WorkshopEvalCompletedNotification thisWorkshop = null;

        Common.Query($@"
          WITH wes
          AS (
            SELECT we.WorkshopEventId
            FROM ev_WorkshopEvent we
            INNER JOIN sv_360_Codes sc ON sc.CodeId = we.EvalIntakeCodeId
            INNER JOIN sv_360_Participants sp ON sp.IntakeCodeId = we.EvalIntakeCodeId
            INNER JOIN sv_360_AnswerTypes sat ON sc.AnswerTypeId = sat.AnswerTypeId
            INNER JOIN sv_Survey sv ON sat.SurveyId = sv.sv_id
            WHERE sc.IntakeCloseDate >= @CloseDateFromUtc
              AND sv.WorkshopEvalNotificationSentUtc IS NULL
            GROUP BY we.WorkshopEventId
            HAVING COUNT(sp.Completed) >= @MinCompleted
          )
          SELECT
            we.WorkshopEventId, we.WorkshopTitle, we.ProgramJobId, we.StartDate, we.EvalScoreSum, we.EvalScoreCount,
            we.KeyFacilitatorUserId,
            ij.JobNumber, ij.JobName, ij.LeadConsultantUserId,
            u.UserId, u.FirstName, u.LastName, u.Email, u.InviteCode, u.RegisteredUtc,
            sc.OrgId, sc.SvCompanyId, sc.CompanyName
          FROM wes
          INNER JOIN ev_WorkshopEvent we ON we.WorkshopEventId = wes.WorkshopEventId
          INNER JOIN id_Job ij ON ij.JobId = we.ProgramJobId
          INNER JOIN al_Project ap ON ij.JobNumber = ap.JobNumber
          INNER JOIN sv_SurveyCompany sc ON ap.SvCompanyId = sc.SvCompanyId
          CROSS APPLY (
            SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.InviteCode, u.RegisteredUtc
            FROM sv_User u
            WHERE u.UserId = we.KeyFacilitatorUserId OR u.UserId = ij.LeadConsultantUserId
            UNION
            SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.InviteCode, u.RegisteredUtc
            FROM sv_User u
            INNER JOIN al_UserProjectAccess upa ON u.UserId = upa.UserId
            WHERE upa.ProgramJobId = ij.JobId
          ) AS u
          ORDER BY we.WorkshopEventId, u.UserId;",
          dr => {
            int workshopEventId = dr.GetInt("WorkshopEventId");
            if (thisWorkshop == null || thisWorkshop.WorkshopEventId != workshopEventId) {
              thisWorkshop = new WorkshopEvalCompletedNotification(
                workshopEventId: workshopEventId,
                workshopTitle: dr.GetString("WorkshopTitle"),
                startDate: dr.GetDateTime("StartDate"),
                programJobId: dr.GetInt("ProgramJobId"),
                jobNumber: dr.GetString("JobNumber"),
                jobName: dr.GetString("JobName"),
                keyFacilitatorUserId: dr.GetIntOrNull("KeyFacilitatorUserId"),
                leadConsultantUserId: dr.GetIntOrNull("LeadConsultantUserId"),
                evalScoreSum: dr.GetDecimalOrNull("EvalScoreSum"),
                evalScoreCount: dr.GetDecimalOrNull("EvalScoreCount"),
                orgId: dr.GetInt("OrgId"),
                companyId: dr.GetInt("SvCompanyId"),
                companyName: dr.GetString("CompanyName")
              );
              result.Add(thisWorkshop);
            }
            thisWorkshop.AddUserToNotify(
              userId: dr.GetInt("UserId"),
              firstName: dr.GetString("FirstName"),
              lastName: dr.GetString("LastName"),
              email: dr.GetString("Email"),
              inviteCode: dr.IsDBNull("RegisteredUtc") ? dr.GetString("InviteCode") : null
            );
          },
          Common.NewSqlParameter("CloseDateFromUtc", closeDateFromUtc),
          Common.NewSqlParameter("MinCompleted", minCompleted)
        );

        return result;
      }

      public static bool UpdateWorkshopEvalNotificationSent(int workshopEventId, DateTime setSentUtc) {
        int rowsAffected = Common.UpdateSingleColumn(
          "sv_Survey",
          new Common.SimpleWhereCondition("WorkshopEventId", workshopEventId),
          new Common.ColumnSetting("WorkshopEvalNotificationSentUtc", setSentUtc));
        return rowsAffected > 0 ? true : false;
      }

      public static decimal? GetAverageEvalScoreForCompany(int companyId) {

        decimal? result = null;

        Common.Query(@"
          SELECT CAST(SUM(gqs.ScoreSum) AS REAL) / SUM(gqs.ScoreCount) AS ScoreAvg
          FROM sv_SurveyGblQnScores gqs
          INNER JOIN sv_Survey sv ON sv.sv_id = gqs.SurveyId
          WHERE sv.SvCompanyId = @CompanyId
          AND sv.SurveyTypeCode = 'eval'
          AND gqs.ScoreCount > 0;",
          dr => {
            result = dr.GetDecimalOrNull("ScoreAvg");
          },
          Common.NewSqlParameter("CompanyId", companyId)
        );
        return result;
      }

      public class WorkshopEvalCompletedNotification {

        public int WorkshopEventId { get; private set; }
        public string WorkshopTitle { get; private set; }
        public int ProgramJobId { get; private set; }
        public string JobNumber { get; private set; }
        public string JobName { get; private set; }
        public int? KeyFacilitatorUserId { get; private set; }
        public int? LeadConsultantUserId { get; private set; }
        public decimal? EvalScoreSum { get; private set; }
        public decimal? EvalScoreCount { get; private set; }
        public DateTime StartDate { get; private set; }
        public int OrgId { get; private set; }
        public int CompanyId { get; private set; }
        public string CompanyName { get; private set; }
        public List<NotifyUser> NotifyUsers { get; private set; }

        public class NotifyUser {
          public int UserId { get; private set; }
          public string FirstName { get; private set; }
          public string LastName { get; private set; }
          public string Email { get; private set; }
          public string InviteCode { get; private set; }
          public NotifyUser(int userId, string firstName, string lastName, string email, string inviteCode) {
            UserId = userId;
            FirstName = firstName;
            LastName = lastName;
            Email = email;
            InviteCode = inviteCode;
          }
        }

        public WorkshopEvalCompletedNotification(
          int workshopEventId, string workshopTitle, DateTime startDate,
          int programJobId, string jobNumber, string jobName, int? keyFacilitatorUserId,
          int? leadConsultantUserId, decimal? evalScoreSum, decimal? evalScoreCount,
          int orgId, int companyId, string companyName) {

          WorkshopEventId = workshopEventId;
          WorkshopTitle = workshopTitle;
          StartDate = startDate;
          ProgramJobId = programJobId;
          JobNumber = jobNumber;
          JobName = jobName;
          KeyFacilitatorUserId = keyFacilitatorUserId;
          LeadConsultantUserId = leadConsultantUserId;
          EvalScoreSum = evalScoreSum;
          EvalScoreCount = evalScoreCount;
          OrgId = orgId;
          CompanyId = companyId;
          CompanyName = companyName;
          NotifyUsers = new List<NotifyUser>();
        }

        public decimal? EvalScoreAvg => (EvalScoreCount ?? 0) == 0 ? null : (decimal?)EvalScoreSum.GetValueOrDefault(0) / EvalScoreCount.Value;
        public NotifyUser WorkshopFacilitatorInfo => NotifyUsers.Find(u => u.UserId == KeyFacilitatorUserId);

        public void AddUserToNotify(int userId, string firstName, string lastName, string email, string inviteCode) {
          NotifyUsers.Add(new NotifyUser(userId, firstName, lastName, email, inviteCode));
        }
      }

    }
  }
}
