using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class ProjectUserAccess {

      // Note: Every row has 3 IDs - Project, Program and User.
      // User is never null.
      // User can have access at 2 levels - Project level, or Program level.
      // A row with both Project ID (JobNumber) and ProgramJobId NOT NULL means user has Program-level access.
      // A row with Project ID (JobNumber) and ProgramJobId NULL means user has Project-level access.

      public static bool UserIsInProjectAccess(string projectJobNumber, int userId) {
        int hasAccess = Common.GetScalarQueryInt(@"
          IF EXISTS (
            SELECT NULL
            FROM al_UserProjectAccess pua
            INNER JOIN al_Project prj ON pua.ProjectId = prj.ProjectId
            WHERE prj.JobNumber = @JobNumber
              AND pua.ProgramJobId IS NULL
              AND pua.UserId = @UserId
          )
          SELECT 1;
          ELSE
          SELECT 0;",
          Common.NewSqlParameter("JobNumber", projectJobNumber),
          Common.NewSqlParameter("UserId", userId)
        );
        return hasAccess == 1 ? true : false;
      }

      public static bool UserHasProgramAccess(int programJobId, int userId) {
        return Common.GetScalarQueryInt(@"
          IF EXISTS
            ( SELECT NULL FROM al_UserProjectAccess pua WHERE pua.ProgramJobId = @ProgramJobId AND pua.UserId = @UserId )
          SELECT 1;
          ELSE
          SELECT 0;",
          Common.NewSqlParameter("ProgramJobId", programJobId),
          Common.NewSqlParameter("UserId", userId)).ToBooleanOrDefault(false);
      }

      public static List<ProjectAccessInfo> GetProjectAccessUsers(string projectJobNumber) {
        var list = new List<ProjectAccessInfo>();
        ProjectAccessInfo ai = null;
        Common.Query(@"
          SELECT DISTINCT u.UserId, u.FirstName, u.LastName, u.Email, u.Mobile,
            j2.JobId, j2.JobName, j2.UserProjectAccessId, j2.IsReadOnly,
            o.OrgId, o.OrgName
          FROM sv_User u
          INNER JOIN al_UserProjectAccess upa ON u.UserId = upa.UserId
          INNER JOIN id_Job j ON upa.ProgramJobId = j.JobId
          INNER JOIN sv_Organisation o ON o.OrgId = u.OrgId
          CROSS APPLY (
            SELECT j2.JobId, j2.JobName, upa2.UserProjectAccessId, upa2.IsReadOnly
            FROM id_Job j2
            LEFT OUTER JOIN al_UserProjectAccess upa2 ON upa2.ProgramJobId = j2.JobId AND upa2.UserId = u.UserId
            WHERE j2.JobNumber = @JobNumber
          ) AS j2
          WHERE j.JobNumber = @JobNumber
          ORDER BY u.FirstName, u.LastName, u.UserId, j2.JobName, j2.JobId",
          dr => {
            if (ai == null || ai.UserId != dr.GetInt("UserID")) {
              ai = new ProjectAccessInfo() {
                UserId = dr.GetInt("UserID"),
                FirstName = dr.GetString("FirstName"),
                LastName = dr.GetString("LastName"),
                Email = dr.GetString("Email"),
                Mobile = dr.GetString("Mobile"),
                OrgId = dr.GetInt("OrgId"),
                OrgName = dr.GetString("OrgName"),
                Programs = new List<ProjectAccessInfo.ProgramInfo>()
              };
              list.Add(ai);
            }
            ai.Programs.Add(new ProjectAccessInfo.ProgramInfo() {
              ProgramJobId = dr.GetInt("JobId"),
              ProgramJobName = dr.GetString("JobName"),
              HasAccess = !dr.IsDBNull("UserProjectAccessId"),
              IsReadOnly = dr.GetBoolFromInt("IsReadOnly")
            });
          },
          Common.NewSqlParameter("JobNumber", projectJobNumber)
        );
        return list;
      }

      public static void SetProgramAccess(SqlTransaction trans, int projectId, int programJobId, int userId) {

        // Note: UPDLOCK, SERIALIZABLE requires a transaction for scope, so if trans is null, add a transaction to SQL.
        // Ref: https://sqlperformance.com/2020/09/locking/upsert-anti-pattern

        Common.GetNonQueryInt(trans, $@"
          {(trans == null ? "BEGIN TRANSACTION;" : "")}
          INSERT al_UserProjectAccess (ProjectId, ProgramJobId, UserId)
          SELECT @ProjectId, @ProgramJobId, @UserId
          WHERE NOT EXISTS (
            SELECT NULL FROM al_UserProjectAccess pua WITH (UPDLOCK, SERIALIZABLE)
            WHERE pua.ProjectId = @ProjectId AND pua.UserId = @UserId
              AND (@ProgramJobId IS NULL AND pua.ProgramJobId IS NULL OR pua.ProgramJobId = @ProgramJobId));
          {(trans == null ? "COMMIT TRANSACTION;" : "")}",
          Common.NewSqlParameter("ProjectId", projectId),
          Common.NewSqlParameter("ProgramJobId", programJobId),
          Common.NewSqlParameter("UserId", userId)
        );
      }

      public static void DeleteProgramAccess(SqlTransaction trans, int projectId, int programJobId, int userId) {
        Common.GetNonQueryInt(trans, @"
          DELETE FROM al_UserProjectAccess
            WHERE ProjectId = @ProjectId
              AND UserId = @UserId
              AND ((@ProgramJobId IS NULL AND ProgramJobId IS NULL) OR (@ProgramJobId IS NOT NULL AND ProgramJobId = @ProgramJobId))",
          Common.NewSqlParameter("ProjectId", projectId),
          Common.NewSqlParameter("ProgramJobId", programJobId),
          Common.NewSqlParameter("UserId", userId)
        );
      }

      public class ProjectAccessInfo {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
        public int OrgId { get; set; }
        public string OrgName { get; set; }
        public List<ProgramInfo> Programs { get; set; }
        public class ProgramInfo {
          public int ProgramJobId { get; set; }
          public string ProgramJobName { get; set; }
          public bool HasAccess { get; set; }
          public bool IsReadOnly { get; set; }
        }
        public string FullName => $"{FirstName} {LastName}";
      }

    }
  }
}
