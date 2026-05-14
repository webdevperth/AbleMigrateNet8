using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class OrganisationUsers {

      public enum ProgramStatusEnum {
        Inactive, // User has never been active.
        Active,   // User is currently active in a program.
        Completed // User has been active and completed their latest program.
      }
      public const string SubscriptionTitle_NoSubscription = "No Subscription";

      private static OrganisationUserInfo GetOrganisationUserList(
        string whereClause,
        int? offsetRows = null,
        int? fetchRows = null,
        params SqlParameter[] sqlWhereParams) {

        var orgUserInfo = new OrganisationUserInfo();

        string sql = $@"
          WITH UsersInCompany AS (
            SELECT UserId
            FROM sv_User u
            WHERE u.IsParticipant = 1 AND u.ClientCompanyId = @CompanyId
            UNION
            SELECT DISTINCT ac.UserId
            FROM al_Coachees ac
            WHERE ac.DeletedUtc IS NULL AND ac.CompanyId = @CompanyId
          ),
          ActiveParticipants AS (
            SELECT DISTINCT UserId
            FROM al_Coachees ac
            WHERE  ac.DeletedUtc IS NULL AND ac.CompanyId = @CompanyId
            AND ac.ProgramStatusId IN (@CoacheeStatusId_OnBoarding, @CoacheeStatusId_Paused, @CoacheeStatusId_Active)
          ),
          ParticipantInfo AS (
            SELECT
              ac.UserId,
              COUNT(DISTINCT ac.ProgramJobId) AS TotalPrograms,
              SUM(ac.SessionsAllocated) AS CoachingSessionsAllocated,
              SUM(ac.SessionsCompleted) AS CoachingSessionsCompleted,
              SUM(CASE WHEN ac.ProgramStatusId IN(@CoacheeStatusId_OnBoarding, @CoacheeStatusId_Paused, @CoacheeStatusId_Active) THEN 1 ELSE 0 END) AS TotalActiveCoachees
            FROM al_Coachees ac
            WHERE ac.DeletedUtc IS NULL AND ac.CompanyId = @CompanyId
            GROUP BY ac.UserId
          )
          SELECT
            COUNT(*) OVER() AS TotalRows,
            u.UserId, u.UserGuid, u.FirstName, u.LastName, u.Email, u.Mobile, u.ClientCompanyId,
            {Subscriptions.User.GetSubscriptionOuterApplySelectionSQL},
            {AbleUser.GetUserActivityLeftJoinSelectionSQL},
            ISNULL(acc.TotalPrograms, 0) AS TotalPrograms,
            ISNULL(acc.CoachingSessionsAllocated, 0) AS CoachingSessionsAllocated,
            ISNULL(acc.CoachingSessionsCompleted, 0) AS CoachingSessionsCompleted,
            ISNULL(acc.TotalActiveCoachees, 0) AS TotalActiveCoachees
          FROM UsersInCompany uic
          INNER JOIN sv_User u ON uic.UserId = u.UserId
          LEFT JOIN ParticipantInfo acc ON uic.UserId = acc.UserId
          LEFT JOIN ActiveParticipants ap ON uic.UserId = ap.UserId
          {AbleUser.GetUserActivityInfoLeftJoinSQL("u", "")}
          {Subscriptions.User.GetUserSubscriptionOuterApplySQL("u")}
          {whereClause.EnsureStartsWith(" WHERE ", true)}
          ORDER BY u.FirstName, u.LastName";

        if (offsetRows >= 0 && fetchRows > 0) {
          orgUserInfo.OffsetRows = offsetRows;
          orgUserInfo.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        if (ConfigHelper.IsDevServer) orgUserInfo.SqlText = sql;

        var sqlParams = new List<SqlParameter>(AbleUser.GetUserActivityInfoParamsSQL());

        foreach (var whereParam in sqlWhereParams) {
          var foundParam = sqlParams.Find(p => p.ParameterName.Equals(whereParam.ParameterName, StringComparison.OrdinalIgnoreCase));
          if (foundParam != null) foundParam.Value = whereParam.Value;
          else sqlParams.Add(whereParam);
        }

        Common.Query(sql, sqlParams, dr => {
          if (orgUserInfo.TotalRows == 0) orgUserInfo.TotalRows = dr.GetInt("TotalRows");
          var orgInfo = new OrgUserInfo(
            dr.GetInt("UserId"),
            dr.GetGuid("UserGuid"),
            dr.GetString("FirstName"),
            dr.GetString("LastName"),
            dr.GetString("Email"),
            dr.GetString("Mobile"),
            dr.GetIntOrNull("ClientCompanyId"),
            dr.GetInt("TotalPrograms", 0),
            dr.GetInt("TotalActiveCoachees", 0),
            dr.GetInt("CoachingSessionsAllocated", 0),
            dr.GetInt("CoachingSessionsCompleted", 0),
            Subscriptions.User.GetUserSubscriptionInfo(dr),
            AbleUser.GetUserActivityInfo(dr)
          );
          orgUserInfo.OrganisationUserInfoList.Add(orgInfo);
        });

        return orgUserInfo;
      }

      public static OrganisationUserInfo GetOrganisationUserList_BySearchTerm(int companyId, string searchTerm, bool statusInactive, int offsetRows, int fetchRows) {

        string sqlWhereClause = string.Empty;

        // If there's a search term
        if (!searchTerm.IsNullOrEmpty()) {
          sqlWhereClause = $@"
            ( u.FirstName LIKE '%' + @SearchTerm + '%'
              OR u.LastName LIKE '%' + @SearchTerm + '%'
              OR u.Email LIKE '%' + @SearchTerm + '%'
              OR us.SubscriptionName LIKE '%' + @SearchTerm + '%'
            ";

          if (SubscriptionTitle_NoSubscription.ToLower().Contains(searchTerm.ToLower())) {
            sqlWhereClause += " OR NOT EXISTS (SELECT 1 FROM al_UserSubscription sub WHERE sub.UserId = u.UserId)";
          }

          sqlWhereClause += ")";
        }

        sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + $" ap.UserId IS {(statusInactive ? "" : "NOT")} NULL";
        sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + $" u.DeletedUtc IS NULL";

        return GetOrganisationUserList(
          sqlWhereClause,
          offsetRows, fetchRows,
          Common.NewSqlParameter("SearchTerm", searchTerm),
          Common.NewSqlParameter("CompanyId", companyId));
      }

      public static OrganisationUserInfo GetOrganisationParticipants(int CompanyId) {
        return GetOrganisationUserList("", null, null,
          Common.NewSqlParameter("CompanyId", CompanyId));
      }

      public static ProfileInfo GetProfileInfo(Guid userGuid, bool activityOnlyWhereLeaderCanView360Report = false) {

        ProfileInfo profileInfo = null;

        var sqlParams = new List<SqlParameter>(AbleUser.GetUserActivityInfoParamsSQL());
        sqlParams.Add(Common.NewSqlParameter("UserGuid", userGuid));

        Common.Query($@"
          SELECT
            u.UserId, u.UserGuid, u.FirstName, u.LastName, u.Email, u.ClientCompanyId,
            ac_all.TotalSessionsAllocated, ac_all.TotalSessionsCompleted, ac_all.LatestNextApptDateUTC,
            prg.LastPrg_CoacheeId, prg.LastPrg_ProgramJobId, prg.LastPrg_JobNumber, prg.LastPrg_JobName, prg.LastPrg_CompanyName,
            {Subscriptions.User.GetSubscriptionOuterApplySelectionSQL},
            {AbleUser.GetUserActivityLeftJoinSelectionSQL}
          FROM sv_User u
          {AbleUser.GetUserActivityInfoLeftJoinSQL("u", "", activityOnlyWhereLeaderCanView360Report)}
          CROSS APPLY (
            SELECT
              SUM(ac.SessionsAllocated) AS TotalSessionsAllocated,
              SUM(ac.SessionsCompleted) AS TotalSessionsCompleted,
              MAX(ac.NextApptDateUTC) AS LatestNextApptDateUTC
            FROM al_Coachees ac
            WHERE ac.UserId = u.UserId
          ) AS ac_all
          {Subscriptions.User.GetUserSubscriptionOuterApplySQL("u")}
          OUTER APPLY (
            SELECT TOP 1
              ac.CoacheeId AS LastPrg_CoacheeId,
              ac.ProgramJobId AS LastPrg_ProgramJobId,
              ij.JobNumber AS LastPrg_JobNumber,
              ij.JobName AS LastPrg_JobName,
              cmp.CompanyName AS LastPrg_CompanyName
            FROM al_Coachees ac
            INNER JOIN id_Job ij ON ac.ProgramJobId = ij.JobId
            LEFT JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = ij.CompanyId
            WHERE ac.DeletedUtc IS NULL AND ac.UserId = u.UserId
            ORDER BY ac.CoacheeId DESC
          ) AS prg
          WHERE u.UserGuid = @UserGuid
            AND u.IsParticipant = 1",

          sqlParams,

          dr => {
            profileInfo = new ProfileInfo(
              userId: dr.GetInt("UserId"),
              userGuid: dr.GetGuid("UserGuid"),
              clientCompanyId: dr.GetIntOrNull("ClientCompanyId"),
              firstName: dr.GetString("FirstName"),
              lastName: dr.GetString("LastName"),
              email: dr.GetString("Email"),
              totalSessionsCompleted: dr.GetIntOrNull("TotalSessionsCompleted") ?? 0,
              totalSessionsAllocated: dr.GetIntOrNull("TotalSessionsAllocated") ?? 0,
              latestNextApptDateUtc: dr.GetDateTimeOrNull("LatestNextApptDateUTC"),
              latestProgramCoacheeId: dr.GetIntOrNull("LastPrg_CoacheeId"),
              latestProgramJobId: dr.GetIntOrNull("LastPrg_ProgramJobId"),
              latestProgramJobNumber: dr.GetString("LastPrg_JobNumber"),
              latestProgramName: dr.GetString("LastPrg_JobName"),
              lastestCompanyName: dr.GetString("LastPrg_CompanyName"),
              userActivity: AbleUser.GetUserActivityInfo(dr),
              userSubscription: Subscriptions.User.GetUserSubscriptionInfo(dr)
            );
          }
        );

        return profileInfo;
      }

      public static List<OrgParticipantBasicInfo> GetOrgParticipantsBasicInfoNotInProgram(int companyId, int programId) {
        return GetOrganisationParticipants_BasicInfo(
          " AND NOT EXISTS(SELECT 1 FROM al_Coachees c WHERE c.ProgramJobId = @ProgramId and c.EmailAddress = u.Email)  ",
          Common.NewSqlParameter("CompanyId", companyId),
          Common.NewSqlParameter("ProgramId", programId));
      }

      private static List<OrgParticipantBasicInfo> GetOrganisationParticipants_BasicInfo(
        string whereClause,
        params SqlParameter[] sqlWhereParams) {

        var orgUserInfo = new List<OrgParticipantBasicInfo>();

        string sql = $@"

          SELECT
            u.UserId, u.UserGuid, u.FirstName, u.LastName, u.Email, u.Mobile, u.ClientCompanyId
          FROM sv_User u
          LEFT OUTER JOIN al_Coachees ac ON u.UserId = ac.UserId
          LEFT OUTER JOIN id_Job j ON j.JobId = ac.ProgramJobId
          WHERE
            (u.IsParticipant = 1 AND (u.ClientCompanyId = @CompanyId OR j.CompanyId = @CompanyId))
            {whereClause.EnsureStartsWith(" AND ", true)}
          GROUP BY
            u.UserId, u.UserGuid, u.FirstName, u.LastName, u.Email, u.Mobile, u.ClientCompanyId

          ORDER BY u.FirstName";

        Common.Query(sql,
          dr => {
            orgUserInfo.Add(new OrgParticipantBasicInfo(
              dr.GetInt("UserId"),
              dr.GetGuid("UserGuid"),
              dr.GetString("FirstName"),
              dr.GetString("LastName"),
              dr.GetString("Email"),
              dr.GetString("Mobile"),
              dr.GetIntOrNull("ClientCompanyId")));
          },
          sqlWhereParams
        );

        return orgUserInfo;
      }

      public class OrgParticipantBasicInfo {

        public int UserId { get; private set; }
        public Guid UserGuid { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string FullName { get; private set; }
        public string Email { get; private set; }
        public string MobilePhone { get; private set; }
        public int? CompanyId { get; private set; }

        public OrgParticipantBasicInfo(
          int userId,
          Guid userGuid,
          string firstName,
          string lastName,
          string email,
          string mobilePhone,
          int? companyId
        ) {
          this.UserId = userId;
          this.UserGuid = userGuid;
          this.FirstName = firstName;
          this.LastName = lastName;
          this.FullName = firstName + " " + lastName;
          this.Email = email;
          this.MobilePhone = mobilePhone;
          this.CompanyId = companyId;
        }
      }

      public class ProfileInfo {

        public int UserId { get; private set; }
        public Guid UserGuid { get; private set; }
        public int? ClientCompanyId { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }
        public int? LatestProgramCoacheeId { get; private set; }
        public int? LatestProgramJobId { get; private set; }
        public string LatestProgramJobNumber { get; private set; }
        public string LatestProgramName { get; private set; }
        public string LastestCompanyName { get; private set; }
        public int TotalSessionsCompleted { get; private set; }
        public int TotalSessionsAllocated { get; private set; }
        public DateTime? LatestNextApptDateUtc { get; private set; }
        public DateTime? MaxApptDateUtc { get; private set; }
        public AbleUser.UserActivityInfo UserActivity { get; private set; }
        public Subscriptions.User.UserSubscriptionInfo UserSubscription { get; private set; }

        public ProfileInfo(
          int userId, Guid userGuid, int? clientCompanyId, string firstName, string lastName, string email,
          int totalSessionsCompleted, int totalSessionsAllocated, DateTime? latestNextApptDateUtc,
          int? latestProgramCoacheeId, int? latestProgramJobId, string latestProgramJobNumber, string latestProgramName, string lastestCompanyName,
          AbleUser.UserActivityInfo userActivity, Subscriptions.User.UserSubscriptionInfo userSubscription
        ) {
          UserId = userId;
          UserGuid = userGuid;
          ClientCompanyId = clientCompanyId;
          FirstName = firstName;
          LastName = lastName;
          Email = email;
          LatestProgramCoacheeId = latestProgramCoacheeId;
          LatestProgramJobId = latestProgramJobId;
          LatestProgramJobNumber = latestProgramJobNumber;
          LatestProgramName = latestProgramName;
          LastestCompanyName = lastestCompanyName;
          TotalSessionsCompleted = totalSessionsCompleted;
          TotalSessionsAllocated = totalSessionsAllocated;
          LatestNextApptDateUtc = latestNextApptDateUtc;
          MaxApptDateUtc = GetMaxApptDateUtc();
          UserSubscription = userSubscription;
          UserActivity = userActivity;
        }

        private DateTime? GetMaxApptDateUtc() {
          if (UserActivity?.LastestCoachingSessionUtc == null && LatestNextApptDateUtc == null) return null;
          return (new DateTime[] { UserActivity?.LastestCoachingSessionUtc ?? DateTime.MinValue, UserActivity?.LastestCoachingSessionUtc ?? DateTime.MinValue }).Max();
        }
      }

      public class OrgUserInfo {

        public int UserId { get; private set; }
        public Guid UserGuid { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string FullName { get; private set; }
        public string Email { get; private set; }
        public string MobilePhone { get; private set; }
        public int? CompanyId { get; private set; }
        public int TotalPrograms { get; private set; }
        public int TotalActivePrograms { get; private set; }
        public int CoachingSessionsAllocated { get; private set; }
        public int CoachingSessionsCompleted { get; private set; }
        public ProgramStatusEnum ProgramStatus { get; private set; }
        public Subscriptions.User.UserSubscriptionInfo UserSubscription { get; private set; }
        public AbleUser.UserActivityInfo UserActivity { get; private set; }

        public OrgUserInfo(
          int userId,
          Guid userGuid,
          string firstName,
          string lastName,
          string email,
          string mobilePhone,
          int? companyId,
          int totalPrograms,
          int totalActivePrograms,
          int coachingSessionsAllocated,
          int coachingSessionsCompleted,
          Subscriptions.User.UserSubscriptionInfo userSubscription,
          AbleUser.UserActivityInfo userActivity
        ) {
          this.UserId = userId;
          this.UserGuid = userGuid;
          this.FirstName = firstName;
          this.LastName = lastName;
          this.FullName = firstName + " " + lastName;
          this.Email = email;
          this.MobilePhone = mobilePhone;
          this.CompanyId = companyId;
          this.TotalPrograms = totalPrograms;
          this.TotalActivePrograms = totalActivePrograms;
          this.CoachingSessionsAllocated = coachingSessionsAllocated;
          this.CoachingSessionsCompleted = coachingSessionsCompleted;
          this.ProgramStatus = GetProgramStatus();
          this.UserSubscription = userSubscription;
          this.UserActivity = userActivity;
        }

        private ProgramStatusEnum GetProgramStatus() {
          if (this.TotalPrograms == 0) return ProgramStatusEnum.Inactive; // Has never been in a program.
          else if (this.TotalActivePrograms > 0) return ProgramStatusEnum.Active; // User has currently active programs.
          else return ProgramStatusEnum.Completed; // Doesn't have any active programs, but has been active.
        }

      }

      public class OrganisationUserInfo {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public string SqlText { get; internal set; }
        public List<OrgUserInfo> OrganisationUserInfoList { get; internal set; }
        public OrganisationUserInfo() {
          OffsetRows = null;
          FetchRows = null;
          TotalRows = 0;
          OrganisationUserInfoList = new List<OrgUserInfo>();
        }
      }
    }
  }
}
