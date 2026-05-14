using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;

namespace Integral.Web {

  using static DbHelper.Common;

  public partial class DbHelper : HelperBase<DbHelper> {

    public class Modules : Content {

      private const string TblPfx = "mod";

      private static string GetParticipantModuleInfoSelectSql(string topSql = "", string extraJoinsSql = "", string whereClauseSql = "", string orderBySql = "") {
        // Note this is already restricted to pax enrolled
        // and program-specific modules, so no where clause needed.

        return $@"

          WITH UserModules AS (

            -- Get all existing enrolments (including those with NULL ProgramJobId)
            SELECT cme.UserId, cme.ContentModuleId, cme.ProgramJobId
            FROM al_ContentModuleEnrolment cme
            WHERE cme.UserId = @UserId

            UNION

            -- Get modules assigned via programs but NOT already enrolled in
            SELECT c.UserId, pcm.ContentModuleId, pcm.ProgramJobId
            FROM al_Program_ContentModule pcm
            INNER JOIN al_Coachees c ON c.UserId = @UserId AND c.DeletedUtc IS NULL AND c.ProgramJobId = pcm.ProgramJobId
            WHERE NOT EXISTS (
              SELECT 1
              FROM al_ContentModuleEnrolment cme2
              WHERE cme2.UserId = c.UserId
                AND cme2.ContentModuleId = pcm.ContentModuleId
                -- Allow multiple program enrolments, but if it's *already enrolled* in any way, don't add it again
            )
          )

          SELECT {topSql}
          um.UserId, um.ContentModuleId AS ModuleId, cm.ModuleGuid,
          cme.EnrolledUtc, cme.LastViewedUtc,
          pcm.ProgramJobId, pcm.ModuleStartUtc, pcm.ModuleDueUtc,
          CASE WHEN tl.TotalLessonsInModule IS NULL THEN 0 ELSE tl.TotalLessonsInModule END AS TotalLessonsInModule,
          CASE WHEN ml.TotalCompletedLessons IS NULL THEN 0 ELSE ml.TotalCompletedLessons END AS TotalCompletedLessons

          FROM UserModules um
          INNER JOIN al_ContentModule cm ON cm.ModuleId = um.ContentModuleId
          LEFT JOIN al_ContentModuleEnrolment cme ON um.UserId = cme.UserId AND um.ContentModuleId = cme.ContentModuleId AND (um.ProgramJobId = cme.ProgramJobId OR cme.ProgramJobId IS NULL)
          LEFT JOIN al_Program_ContentModule pcm  ON um.ContentModuleId = pcm.ContentModuleId AND um.ProgramJobId = pcm.ProgramJobId

          LEFT JOIN (
            SELECT cmd.ModuleId,
            COUNT(*) AS TotalLessonsInModule
            FROM al_ContentModuleDetails cmd
            GROUP BY cmd.ModuleId
          ) tl ON tl.ModuleId = um.ContentModuleId

          LEFT JOIN (
            SELECT cmd.ModuleId,
            COUNT(*) AS TotalCompletedLessons
            FROM al_ContentModuleDetails cmd
            LEFT JOIN al_UserContent uc ON uc.ContentId = cmd.ContentId AND uc.UserId = @UserId
            WHERE uc.CompletedUtc IS NOT NULL
            GROUP BY cmd.ModuleId
          ) ml ON ml.ModuleId = um.ContentModuleId

          {extraJoinsSql}
          {whereClauseSql.EnsureStartsWith(" WHERE ", true)}
          {orderBySql}
        ";
      }

      private static string GetProgramExtraSelectFields() {
        return "pcm.ProgramContentModuleId, pcm.ProgramJobId, pcm.AddedUtc, pcm.AddedByUserId, pcm.ModuleDisplayOrder, pcm.ModuleStartUtc, pcm.ModuleDueUtc ";
      }

      private static string GetProgramExtraJoinsSql() {
        return $"LEFT JOIN al_Program_ContentModule pcm ON pcm.ContentModuleId = {TblPfx}.ModuleId";
      }

      private static string GetWhereClauseForUserRole(DbHelper.AbleUser.AbleUserBasicInfo forUser, ConfigHelper.UserRole forUserRole) {

        var whereClauses = new List<string>();

        if (forUserRole == ConfigHelper.UserRole.Admin) {

          whereClauses.Add($"(({TblPfx}.PublishedUtc IS NOT NULL AND mau.OrgId = @UserOrgId) OR {TblPfx}.AuthorUserId = @UserId)");

        } else if (forUserRole == ConfigHelper.UserRole.TenantOrgAdmin
          || (forUserRole == ConfigHelper.UserRole.Coach && forUser.IsTenantOrgAdmin)) {

          whereClauses.Add($"(mau.OrgId = @UserOrgId OR {TblPfx}.AuthorUserId = @UserId)");

        } else if (forUserRole == ConfigHelper.UserRole.Coach) {

          whereClauses.Add($"((mau.OrgId = @UserOrgId AND {TblPfx}.PublishedUtc IS NOT NULL AND {TblPfx}.ShowToPartners = 1) OR {TblPfx}.AuthorUserId = @UserId)");

        } else if (forUserRole == ConfigHelper.UserRole.Leader) {

          whereClauses.Add($@"
            {TblPfx}.ShowToParticipants = 1
            AND {TblPfx}.PublishedUtc IS NOT NULL
            AND NOT EXISTS (
              SELECT 1
              FROM al_ContentModuleEnrolment cme
              WHERE cme.UserId = @UserId
                AND cme.ContentModuleId = mod.ModuleId
            )");

        } else {
          throw new InvalidOperationException($"Unhandled {nameof(forUserRole)}: {forUserRole}");
        }

        return whereClauses.Join(" AND ");
      }

      public static List<ModuleInfo> GetModuleForUserAndSearchTerm(
        SqlTransaction trans,
        DbHelper.AbleUser.AbleUserBasicInfo forUser,
        ConfigHelper.UserRole forUserRole,
        string searchTerm = null) {

        if (forUser == null || forUserRole == ConfigHelper.UserRole.Unset) return null;

        var whereClause = GetWhereClauseForUserRole(forUser, forUserRole);

        if (!searchTerm.IsNullOrEmptyOrWhitespace()) {
          whereClause += $@"
            AND
            (    {TblPfx}.ModuleTitle LIKE '%' + @SearchTerm + '%'
              OR {TblPfx}.ModuleSummary LIKE '%' + @SearchTerm + '%'
              OR mau.FirstName LIKE '%' + @SearchTerm + '%'
              OR mau.LastName LIKE '%' + @SearchTerm + '%'
            )";
        }

        var moduleList = new List<ModuleInfo>();

        Query(
          trans,
          GetModuleSelectQuery(
            whereConditionsSQL: whereClause
          ),
          dr => {
            moduleList.Add(new ModuleInfo(dr, forUserRole));
          },
          NewSqlParameter("UserId", forUser.UserId),
          NewSqlParameter("SearchTerm", searchTerm),
          NewSqlParameter("UserOrgId", forUser.OrgId)
        );

        return moduleList;
      }

      // Get module with check for user access.
      public static ModuleInfo GetModuleInfo(SqlTransaction trans, Guid moduleGuid, DbHelper.AbleUser.AbleUserBasicInfo forUser, ConfigHelper.UserRole forUserRole) {

        var moduleList = GetModuleInfoList(
          sqlTransaction: trans,
          whereConditionsSQL: $"{TblPfx}.ModuleGuid = @ModuleGuid" + GetWhereClauseForUserRole(forUser, forUserRole).EnsureStartsWith(" AND "),
          extraSelectFields: null,
          extraJoinsSQL: null,
          topQuery: null,
          userRole: forUserRole,
          NewSqlParameter("UserId", forUser.UserId),
          NewSqlParameter("ModuleGuid", moduleGuid),
          NewSqlParameter("UserOrgId", forUser.OrgId)
        );

        if (moduleList.IsNullOrEmpty()) return null;
        return moduleList[0];
      }

      // Get module with check for user access.
      public static ModuleInfo GetModuleInfo(SqlTransaction trans, int moduleId, DbHelper.AbleUser.AbleUserBasicInfo forUser, ConfigHelper.UserRole forUserRole) {

        var moduleList = GetModuleInfoList(
          sqlTransaction: trans,
          whereConditionsSQL: $"{TblPfx}.ModuleId = @ModuleId" + GetWhereClauseForUserRole(forUser, forUserRole).EnsureStartsWith(" AND "),
          extraSelectFields: null,
          extraJoinsSQL: null,
          topQuery: null,
          userRole: forUserRole,
          NewSqlParameter("UserId", forUser.UserId),
          NewSqlParameter("ModuleId", moduleId),
          NewSqlParameter("UserOrgId", forUser.OrgId)
        );

        if (moduleList.IsNullOrEmpty()) return null;
        return moduleList[0];
      }

      // Get module with no restriction.
      // Only use this internally or if already known that user can access it.
      public static ModuleInfo GetModuleInfo(SqlTransaction trans, int moduleId) {

        var sqlParamList = new List<SqlParameter>();
        sqlParamList.Add(NewSqlParameter("ModuleId", moduleId));

        var moduleList = GetModuleInfoList(
          sqlTransaction: trans,
          whereConditionsSQL: $"{TblPfx}.ModuleId = @ModuleId",
          extraSelectFields: null,
          extraJoinsSQL: null,
          topQuery: null,
          userRole: ConfigHelper.UserRole.Unset,
          sqlParamList.ToArray()
        );

        if (moduleList.IsNullOrEmpty()) return null;
        return moduleList[0];
      }

      public static ContentModulesInProgramInfo GetModuleInProgram(Guid? moduleGuid, int? programJobId) {

        if (moduleGuid == null) {
          throw new ArgumentException("Module Guid required");
        } else if (programJobId == null) {
          throw new ArgumentException("ProgramId required");
        }

        var moduleList = new List<ContentModulesInProgramInfo>();

        Query(
          GetModuleSelectQuery(
            whereConditionsSQL: $"pcm.ProgramJobId = @ProgramJobId AND {TblPfx}.ModuleGuid = @ModuleGuid",
            extraSelectFields: GetProgramExtraSelectFields(),
            extraJoinsSQL: GetProgramExtraJoinsSql(),
            topQuery: string.Empty),
          dr => {
            moduleList.Add(new ContentModulesInProgramInfo(dr, ConfigHelper.UserRole.Unset));
          },
          NewSqlParameter("ProgramJobId", programJobId),
          NewSqlParameter("ModuleGuid", moduleGuid)
        );

        if (moduleList.Count > 0) return moduleList[0];

        return null;
      }

      public static List<ContentModulesInProgramInfo> GetModulesInProgram(AblePrograms.AbleProgramInfo program, string searchTerm = "") {

        string whereClause = $"pcm.ProgramJobId = @ProgramJobId";

        if (!searchTerm.IsNullOrEmptyOrWhitespace()) {
          whereClause += $@"
            AND (    {TblPfx}.ModuleTitle LIKE '%' + @SearchTerm + '%'
                  OR {TblPfx}.ModuleSummary LIKE '%' + @SearchTerm + '%'
                  OR mau.FirstName LIKE '%' + @SearchTerm + '%'
                  OR mau.LastName LIKE '%' + @SearchTerm + '%'
                )";
        }

        var sqlParamList = new List<SqlParameter>();
        sqlParamList.Add(NewSqlParameter("ProgramJobId", program.ProgramJobId));
        sqlParamList.Add(NewSqlParameter("SearchTerm", searchTerm));

        var moduleList = new List<ContentModulesInProgramInfo>();

        Query(
          GetModuleSelectQuery(
            whereConditionsSQL: whereClause,
            extraSelectFields: GetProgramExtraSelectFields(),
            extraJoinsSQL: GetProgramExtraJoinsSql(),
            topQuery: ""),
          sqlParamList,
          dr => {
            moduleList.Add(new ContentModulesInProgramInfo(dr, ConfigHelper.UserRole.Unset));
          }
        );

        return moduleList;
      }

      private static string GetModuleSelectQuery(
        string whereConditionsSQL,
        string extraSelectFields = "",
        string extraJoinsSQL = "",
        string topQuery = "",
        string orderBySQL = "") {

        return $@"

          WITH ModuleLessons AS (
            SELECT cmd.ModuleId, COUNT(*) AS TotalLessons
            FROM al_ContentModuleDetails cmd
            GROUP BY cmd.ModuleId
          )

          SELECT {topQuery}
            {TblPfx}.ModuleId, {TblPfx}.ModuleGuid, {TblPfx}.CreatedUtc, {TblPfx}.ModuleTitle, {TblPfx}.ModuleSummary, {TblPfx}.ModuleDescriptionHtml,
            {TblPfx}.AuthorUserId, {TblPfx}.ShowToParticipants, {TblPfx}.ShowToPartners, {TblPfx}.PublishedUtc,
            mau.FirstName AS AuthorFirstName, mau.LastName AS AuthorLastName,
            mau.Email AS AuthorEmail, mau.UserGuid AS AuthorUserGuid,
            mau.OrgId AS AuthorTenantOrgId,
            ISNULL(ml.TotalLessons, 0) AS TotalLessons, mc.ContentGuidsInModule
            {extraSelectFields.EnsureStartsWith(", ", true)}
          FROM al_ContentModule {TblPfx}
          INNER JOIN sv_User mau ON mau.UserId = {TblPfx}.AuthorUserId
          LEFT JOIN ModuleLessons ml on ml.ModuleId = {TblPfx}.ModuleId
          LEFT JOIN (
            SELECT
              cmd.ModuleId,
              STRING_AGG(CAST(c.ContentGuid AS varchar(36)), ',')
                WITHIN GROUP (ORDER BY cmd.DisplayOrder) AS ContentGuidsInModule
            FROM al_ContentModuleDetails cmd
            INNER JOIN al_Content c ON c.ContentId = cmd.ContentId
            GROUP BY cmd.ModuleId
          ) mc ON mc.ModuleId = mod.ModuleId

          {extraJoinsSQL.EnsureStartsWith(" ", true)}

          WHERE {TblPfx}.DeletedUtc IS NULL
            {whereConditionsSQL.EnsureStartsWith("AND ", true)}

          ORDER BY {(orderBySQL.IsNullOrEmpty() ? $"{TblPfx}.CreatedUtc DESC" : orderBySQL)}";
      }

      private static List<ModuleInfo> GetModuleInfoList(
        SqlTransaction sqlTransaction,
        string whereConditionsSQL,
        string extraSelectFields,
        string extraJoinsSQL,
        string topQuery,
        ConfigHelper.UserRole userRole = ConfigHelper.UserRole.Unset,
        params SqlParameter[] whereParams) {

        var sqlParamList = new List<SqlParameter>(whereParams);
        var moduleList = new List<ModuleInfo>();

        Query(
          sqlTransaction,
          GetModuleSelectQuery(whereConditionsSQL, extraSelectFields, extraJoinsSQL, topQuery),
          sqlParamList,
          dr => {
            moduleList.Add(new ModuleInfo(dr, userRole));
          }
        );

        return moduleList;
      }

      public static ModuleInfo AddModule(SqlTransaction trans, ModuleInfo moduleInfo) {

        int moduleId = GetScalarQueryInt(
          trans,
          $@"
          INSERT INTO al_ContentModule
            (AuthorUserId, ModuleTitle, ModuleSummary, ModuleDescriptionHtml, ShowToParticipants, ShowToPartners, PublishedUtc)
          OUTPUT INSERTED.ModuleId
          VALUES
            (@AuthorUserId, @ModuleTitle, @ModuleSummary, @ModuleDescriptionHtml, @ShowToParticipants, @ShowToPartners, @PublishedUtc)",
          NewSqlParameter("AuthorUserId", moduleInfo.AuthorUserId),
          NewSqlParameter("ModuleTitle", moduleInfo.ModuleTitle),
          NewSqlParameter("ModuleSummary", moduleInfo.ModuleSummary),
          NewSqlParameter("ModuleDescriptionHtml", moduleInfo.ModuleDescriptionHtml),
          NewSqlParameter("ShowToParticipants", moduleInfo.ShowToParticipants),
          NewSqlParameter("ShowToPartners", moduleInfo.ShowToPartners),
          NewSqlParameter("PublishedUtc", moduleInfo.PublishedUtc)
        );

        return GetModuleInfo(trans, moduleId);
      }

      public static bool UpdateModule(ModuleInfo moduleInfo) {

        return GetNonQueryInt($@"
          UPDATE al_ContentModule
          SET
            AuthorUserId = @AuthorUserId,
            ModuleTitle = @ModuleTitle,
            ModuleSummary = @ModuleSummary,
            ModuleDescriptionHtml = @ModuleDescriptionHtml,
            ShowToParticipants = @ShowToParticipants,
            ShowToPartners = @ShowToPartners,
            PublishedUtc = @PublishedUtc
          WHERE
            ModuleGuid = @ModuleGuid",
          NewSqlParameter("AuthorUserId", moduleInfo.AuthorUserId),
          NewSqlParameter("ModuleGuid", moduleInfo.ModuleGuid),
          NewSqlParameter("ModuleTitle", moduleInfo.ModuleTitle),
          NewSqlParameter("ModuleSummary", moduleInfo.ModuleSummary),
          NewSqlParameter("ModuleDescriptionHtml", moduleInfo.ModuleDescriptionHtml),
          NewSqlParameter("ShowToParticipants", moduleInfo.ShowToParticipants),
          NewSqlParameter("ShowToPartners", moduleInfo.ShowToPartners),
          NewSqlParameter("PublishedUtc", moduleInfo.PublishedUtc)
        ) == 1;
      }

      public static bool DeleteModule(ModuleInfo moduleInfo) {

        if (moduleInfo == null) throw new ArgumentException("moduleInfo is null");

        return GetNonQueryInt($@"
          UPDATE al_ContentModule
          SET
            DeletedUtc = @DeletedUtc
          WHERE
            ModuleGuid = @ModuleGuid",
          NewSqlParameter("ModuleGuid", moduleInfo.ModuleGuid),
          NewSqlParameter("DeletedUtc", DateTime.UtcNow)
        ) == 1;
      }

      public static bool AddContentToModule(ModuleContentDetailsInfo moduleContent, ContentInfo contentInfo) {

        bool rowInserted = GetNonQueryInt($@"
          INSERT INTO al_ContentModuleDetails
            (ModuleId, ContentId, AddedByUserId, DisplayOrder)
          VALUES
            (@ModuleId, @ContentId, @AddedByUserId,
            ISNULL((SELECT MAX(DisplayOrder) + 1 FROM al_ContentModuleDetails WHERE ModuleId = @ModuleId), 1))",
          NewSqlParameter("ModuleId", moduleContent.ModuleId),
          NewSqlParameter("ContentId", moduleContent.ContentId),
          NewSqlParameter("AddedByUserId", moduleContent.AddedByUserId)
        ) > 0;

        if (rowInserted) {
          moduleContent.ContentGuid = contentInfo.ContentGuid;
          moduleContent.ContentTypeId = contentInfo.ContentTypeId;
          moduleContent.ContentTitle = contentInfo.ContentTitle;
          moduleContent.ContentSummary = contentInfo.ContentSummary;
          moduleContent.AuthorUserId = contentInfo.AuthorUserId;
          moduleContent.AuthorFirstName = contentInfo.AuthorFirstName;
          moduleContent.AuthorLastName = contentInfo.AuthorLastName;
        }

        return rowInserted;
      }

      public static bool RemoveContentFromModule(DbHelper.Modules.ModuleContentDetailsInfo contentInfo) {

        return GetNonQueryInt($@"
          DELETE FROM al_ContentModuleDetails WHERE ContentId = @ContentId and ModuleId = @ModuleId

          UPDATE al_ContentModuleDetails
          SET DisplayOrder = DisplayOrder - 1
          WHERE ModuleId = @ModuleId
            AND DisplayOrder > @DisplayOrder;",
          NewSqlParameter("ContentId", contentInfo.ContentId),
          NewSqlParameter("ModuleId", contentInfo.ModuleId),
          NewSqlParameter("DisplayOrder", contentInfo.DisplayOrder)
        ) > 0;
      }

      public static bool UpdateContentDisplayOrderInModule(SqlTransaction trans, DbHelper.Modules.ModuleContentDetailsInfo contentInfo, int displayOrder) {

        return GetNonQueryInt(trans, $@"
          UPDATE al_ContentModuleDetails
          SET DisplayOrder = @DisplayOrder
          WHERE ModuleId = @ModuleId
            AND ContentId = @ContentId;",
          NewSqlParameter("ContentId", contentInfo.ContentId),
          NewSqlParameter("ModuleId", contentInfo.ModuleId),
          NewSqlParameter("DisplayOrder", displayOrder)
        ) > 0;
      }

      public static List<ModuleContentDetailsInfo> GetModuleContentList(int moduleId, ConfigHelper.UserRole forUserRole) {

        if (forUserRole == ConfigHelper.UserRole.Unset) return null;

        return GetModuleContentList(
          $"cmd.ModuleId = @ModuleId",
          NewSqlParameter("ModuleId", moduleId)
        );
      }

      private static List<ModuleContentDetailsInfo> GetModuleContentList(string whereConditionsSQL, params SqlParameter[] whereParams) {

        var sqlParamList = new List<SqlParameter>(whereParams);

        var moduleList = new List<ModuleContentDetailsInfo>();

        Query(
          GetModuleContentListSql(whereConditionsSQL, "", "", ""),
          sqlParamList,
          dr => {
            moduleList.Add(new ModuleContentDetailsInfo(dr));
          }
        );

        return moduleList;
      }

      public static ParticipantModuleInfo GetParticipantModuleForActionCard(SqlTransaction trans, int userId) {

        ParticipantModuleInfo participantModuleInfo = null;

        Query(
          trans,
          GetParticipantModuleInfoSelectSql(
            topSql: "TOP 1",
            orderBySql: @"
              ORDER BY
              CASE
                -- Linked to program, not enrolled
                WHEN pcm.ProgramJobId IS NOT NULL AND cme.EnrolmentId IS NULL THEN 1

                -- Enrolled but incomplete
                WHEN cme.EnrolmentId IS NOT NULL AND ISNULL(ml.TotalCompletedLessons, 0) < ISNULL(tl.TotalLessonsInModule, 0) THEN 2

                -- Everything else
                ELSE 3
              END,

              CASE
                WHEN cme.EnrolmentId IS NOT NULL AND ISNULL(ml.TotalCompletedLessons, 0) < ISNULL(tl.TotalLessonsInModule, 0)
                THEN ISNULL(tl.TotalLessonsInModule, 0) - ISNULL(ml.TotalCompletedLessons, 0)
                ELSE NULL
              END ASC"),
          dr => {
            participantModuleInfo = new ParticipantModuleInfo(dr);
          },
          NewSqlParameter("UserId", userId)
        );

        if (participantModuleInfo != null) {
          participantModuleInfo.ModuleInfo = GetModuleInfo(trans, participantModuleInfo.ModuleId);
        }

        return participantModuleInfo;
      }

      public static List<ParticipantModuleInfo> GetParticipantModulesInProject(SqlTransaction trans, string jobNumber, int userId) {

        var moduleList = new List<ParticipantModuleInfo>();

        Query(
          trans,
          GetParticipantModuleInfoSelectSql(
            extraJoinsSql: "LEFT JOIN id_Job j ON pcm.ProgramJobId = j.JobId",
            whereClauseSql: "j.JobNumber = @JobNumber"),
          dr => {
            moduleList.Add(new ParticipantModuleInfo(dr));
          },
          NewSqlParameter("UserId", userId),
          NewSqlParameter("@JobNumber", jobNumber)
        );

        if (!moduleList.IsNullOrEmpty()) {
          foreach (var mod in moduleList) {
            mod.ModuleInfo = GetModuleInfo(trans, mod.ModuleId);
          }
        }

        return moduleList;
      }

      public static List<ParticipantModuleInfo> GetParticipantModuleInfo(SqlTransaction trans, DbHelper.AbleUser.AbleUserBasicInfo user, ConfigHelper.UserRole userRole) {

        // do: check access
        var moduleList = new List<ParticipantModuleInfo>();

        Query(
          trans,
          GetParticipantModuleInfoSelectSql(
            orderBySql: @"
              ORDER BY
                CASE
                  WHEN cme.EnrolmentId IS NOT NULL AND ISNULL(ml.TotalCompletedLessons, 0) < ISNULL(tl.TotalLessonsInModule, 0) THEN 1
                  WHEN cme.EnrolmentId IS NULL THEN 2
                  WHEN ISNULL(ml.TotalCompletedLessons, 0) = ISNULL(tl.TotalLessonsInModule, 0) THEN 3
                  ELSE 3
                END,

                CASE
                  WHEN cme.EnrolmentId IS NOT NULL AND ISNULL(tl.TotalLessonsInModule, 0) > 0
                  THEN CAST(ISNULL(ml.TotalCompletedLessons, 0) AS FLOAT) / ISNULL(tl.TotalLessonsInModule, 1)
                  ELSE NULL
                END DESC"),
          dr => {
            moduleList.Add(new ParticipantModuleInfo(dr));
          },
          NewSqlParameter("UserId", user.UserId)
        );

        if (!moduleList.IsNullOrEmpty()) {
          foreach (var mod in moduleList) {
            mod.ModuleInfo = GetModuleInfo(trans, mod.ModuleId);
          }
        }

        return moduleList;
      }

      public static ParticipantModuleInfo GetParticipantModuleInfo(
        SqlTransaction trans,
        Guid moduleGuid,
        DbHelper.AbleUser.AbleUserBasicInfo user,
        ConfigHelper.UserRole userRole) {

        // Note GetParticipantModuleInfoSelectSql() is already restricted to
        // pax-enrolled and program-specific modules, so no WHERE clause needed.

        ParticipantModuleInfo participantModuleInfo = null;

        Query(
          trans,
          GetParticipantModuleInfoSelectSql(
            whereClauseSql: "cm.ModuleGuid = @ModuleGuid"),
          dr => {
            participantModuleInfo = new ParticipantModuleInfo(dr);
          },
          NewSqlParameter("UserId", user.UserId),
          NewSqlParameter("ModuleGuid", moduleGuid)
        );

        if (participantModuleInfo != null) {
          participantModuleInfo.ModuleInfo = GetModuleInfo(trans, participantModuleInfo.ModuleId);
        }

        return participantModuleInfo;
      }

      public static List<UserModuleContentInfo> GetUserModuleContentInfo(SqlTransaction trans, int moduleId, int userId, ConfigHelper.UserRole showingForUserRoleType) {

        if (showingForUserRoleType == ConfigHelper.UserRole.Unset) return null;

        var sqlParamList = new List<SqlParameter>();
        sqlParamList.Add(NewSqlParameter("ModuleId", moduleId));
        sqlParamList.Add(NewSqlParameter("UserId", userId));

        var moduleList = new List<UserModuleContentInfo>();

        Query(
          trans,
          GetModuleContentListSql(
            $"cmd.ModuleId = @ModuleId",
            "uc.ViewedUtc, uc.QuizScore, uc.FavouriteUtc, uc.CompletedUtc",
            "LEFT OUTER JOIN al_UserContent uc ON uc.ContentId = c.ContentId AND uc.UserId = @UserId",
            ""),
          sqlParamList,
          dr => {
            moduleList.Add(new UserModuleContentInfo(dr));
          }
        );

        return moduleList;
      }

      private static string GetModuleContentListSql(
        string whereConditionsSQL,
        string extraSelectFields,
        string extraJoinsSQL,
        string topQuery) {

        return $@"

          SELECT {topQuery}
            cmd.ModuleId, cmd.ContentId, cmd.AddedByUserId, cmd.DisplayOrder,
            c.ContentGuid, c.ContentTypeId, c.ContentTitle, c.AuthorUserId, c.ContentSummary,
            cau.UserGuid AS AuthorUserGuid, cau.OrgId AS AuthorTenantOrgId,
            cau.FirstName AS AuthorFirstName, cau.LastName AS AuthorLastName, cau.Email AS AuthorEmail
            {extraSelectFields.EnsureStartsWith(", ", true)}
          FROM al_ContentModuleDetails cmd
          LEFT JOIN al_Content c ON c.ContentId = cmd.ContentId
          INNER JOIN sv_User cau ON cau.UserId = c.AuthorUserId
          {extraJoinsSQL.EnsureStartsWith(" ", true)}
          WHERE c.DeletedUtc IS NULL
            {whereConditionsSQL.EnsureStartsWith("AND ", true)}
          ORDER BY cmd.DisplayOrder";
      }

      public static bool EnrolParticipantsInModule(SqlTransaction trans, int moduleId, int? programJobId) {

        return GetNonQueryInt(trans, $@"
          INSERT INTO al_ContentModuleEnrolment (ContentModuleId, UserId, ProgramJobId, EnrolledUtc, LastViewedUtc)
          SELECT
            @ContentModuleId,
            UserId,
            @ProgramJobId,
            @EnrolledUtc,
            @LastViewedUtc
          FROM al_Coachees
          WHERE ProgramJobId = @ProgramJobId and DeletedUtc IS NULL",
          NewSqlParameter("ContentModuleId", moduleId),
          NewSqlParameter("ProgramJobId", programJobId),
          NewSqlParameter("EnrolledUtc", DateTime.UtcNow),
          NewSqlParameter("LastViewedUtc", DateTime.UtcNow)
        ) > 0;
      }

      public static bool EnrolUserInModuleInProgram(int moduleId, int userId, int programJobId) {
        return EnrolUserInModule(moduleId, userId, programJobId);
      }

      public static bool EnrolUserInModule(int moduleId, int userId) {
        return EnrolUserInModule(moduleId, userId, null);
      }

      private static bool EnrolUserInModule(int moduleId, int userId, int? programJobId) {

        return GetScalarQueryInt($@"
          INSERT INTO al_ContentModuleEnrolment
            (ContentModuleId, UserId, ProgramJobId, EnrolledUtc, LastViewedUtc)
          OUTPUT INSERTED.EnrolmentId
          VALUES
            (@ContentModuleId, @UserId, @ProgramJobId, @EnrolledUtc, @LastViewedUtc)",
          NewSqlParameter("ContentModuleId", moduleId),
          NewSqlParameter("UserId", userId),
          NewSqlParameter("ProgramJobId", programJobId),
          NewSqlParameter("EnrolledUtc", DateTime.UtcNow),
          NewSqlParameter("LastViewedUtc", DateTime.UtcNow)
        ) > 0;
      }

      public static bool UpdateModuleLastViewed(SqlTransaction trans, int moduleId, int userId) {

        return GetNonQueryInt(trans, $@"

          UPDATE al_ContentModuleEnrolment
          SET LastViewedUtc = @LastViewedUtc
          WHERE ContentModuleId = @ContentModuleId
            AND UserId = @UserId;
          ",
          NewSqlParameter("ContentModuleId", moduleId),
          NewSqlParameter("UserId", userId),
          NewSqlParameter("LastViewedUtc", DateTime.UtcNow)
        ) > 0;
      }

      public static bool RemoveModuleFromProgram(SqlTransaction trans, ContentModulesInProgramInfo contentModuleInProgramInfo) {

        return GetNonQueryInt(trans, $@"

          BEGIN TRANSACTION;

          DELETE FROM al_Program_ContentModule WHERE ProgramContentModuleId = @ProgramContentModuleId

          UPDATE al_Program_ContentModule
          SET ModuleDisplayOrder = ModuleDisplayOrder - 1
          WHERE ProgramJobId = @ProgramJobId
            AND ModuleDisplayOrder > @ModuleDisplayOrder;

          COMMIT TRANSACTION;",
          NewSqlParameter("ProgramContentModuleId", contentModuleInProgramInfo.ProgramContentModuleId),
          NewSqlParameter("ProgramJobId", contentModuleInProgramInfo.ProgramJobId),
          NewSqlParameter("ModuleDisplayOrder", contentModuleInProgramInfo.ModuleDisplayOrder)
        ) > 0;
      }

      public static bool AddModuleToProgram(SqlTransaction trans, ContentModulesInProgramInfo contentModuleInProgramInfo) {

        return GetNonQueryInt(trans, $@"
          INSERT INTO al_Program_ContentModule
            (ProgramJobId, ContentModuleId, AddedUtc, AddedByUserId, ModuleDisplayOrder, ModuleStartUtc, ModuleDueUtc)
          OUTPUT INSERTED.ProgramContentModuleId
          VALUES
            (@ProgramJobId, @ContentModuleId, @AddedUtc, @AddedByUserId, @ModuleDisplayOrder, @StartUtc, @DueUtc)",
          NewSqlParameter("ProgramJobId", contentModuleInProgramInfo.ProgramJobId),
          NewSqlParameter("ContentModuleId", contentModuleInProgramInfo.ModuleInfo.ModuleId),
          NewSqlParameter("AddedUtc", contentModuleInProgramInfo.AddedUtc),
          NewSqlParameter("AddedByUserId", contentModuleInProgramInfo.AddedByUserId),
          NewSqlParameter("ModuleDisplayOrder", contentModuleInProgramInfo.ModuleDisplayOrder),
          NewSqlParameter("StartUtc", contentModuleInProgramInfo.ModuleStartUtc),
          NewSqlParameter("DueUtc", contentModuleInProgramInfo.ModuleDueUtc)
        ) > 0;
      }

      public class ModuleInfo {

        public int ModuleId { get; private set; }
        public Guid ModuleGuid { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public string ModuleTitle { get; set; }
        public int AuthorUserId { get; set; }
        public int AuthorTenantOrgId { get; set; }
        public string ModuleDescriptionHtml { get; set; }
        public string ModuleSummary { get; set; }
        public bool ShowToParticipants { get; set; }
        public bool ShowToPartners { get; set; }
        public DateTime? PublishedUtc { get; set; }
        public string AuthorFirstName { get; private set; }
        public string AuthorLastName { get; private set; }
        public string AuthorEmail { get; private set; }
        public Guid AuthorUserGuid { get; private set; }
        public int TotalLessons { get; private set; }
        public List<Guid> ContentInModule { get; private set; }

        public ModuleInfo() { }

        public ModuleInfo(SqlDataReader dr, ConfigHelper.UserRole userRole = ConfigHelper.UserRole.Unset) {
          SetModuleInfoFields(dr, userRole);
        }

        internal void SetModuleInfoFields(SqlDataReader dr, ConfigHelper.UserRole userRole = ConfigHelper.UserRole.Unset) {
          this.ModuleId = dr.GetInt("ModuleId");
          this.ModuleGuid = dr.GetGuid("ModuleGuid");
          this.CreatedUtc = dr.GetDateTime("CreatedUtc");
          this.ModuleTitle = dr.GetString("ModuleTitle");
          this.ModuleSummary = dr.GetString("ModuleSummary");
          this.ModuleDescriptionHtml = dr.GetString("ModuleDescriptionHtml");
          this.AuthorUserId = dr.GetInt("AuthorUserId");
          this.AuthorTenantOrgId = dr.GetInt("AuthorTenantOrgId");
          this.ShowToParticipants = dr.GetBoolFromInt("ShowToParticipants", false);
          this.ShowToPartners = dr.GetBoolFromInt("ShowToPartners", false);
          this.AuthorFirstName = dr.GetString("AuthorFirstName");
          this.AuthorLastName = dr.GetString("AuthorLastName");
          this.AuthorEmail = dr.GetString("AuthorEmail");
          this.AuthorUserGuid = dr.GetGuid("AuthorUserGuid");
          this.PublishedUtc = dr.GetDateTimeOrNull("PublishedUtc");
          this.TotalLessons = dr.GetInt("TotalLessons");

          string contentGuidsInModule = dr.GetString("ContentGuidsInModule");
          ContentInModule = contentGuidsInModule.IsNullOrEmpty() ? new List<Guid>()
            : contentGuidsInModule.Split(',').Select(guidStr => Guid.TryParse(guidStr, out var guid) ? guid : (Guid?)null).Where(g => g.HasValue).Select(g => g.Value).ToList();

        }

        public string GetAuthorFullName() {
          return AuthorFirstName + " " + AuthorLastName;
        }

        public bool IsPublished => PublishedUtc.HasValue;
      }

      public class ModuleContentDetailsInfo {
        public int ModuleId { get; set; }
        public int ContentId { get; set; }
        public int AddedByUserId { get; set; }
        public int DisplayOrder { get; set; }
        public Guid ContentGuid { get; internal set; }
        public int ContentTypeId { get; internal set; }
        public string ContentTitle { get; internal set; }
        public string ContentSummary { get; internal set; }
        public int AuthorUserId { get; internal set; }
        public int AuthorTenantOrgId { get; internal set; }
        public string AuthorFirstName { get; internal set; }
        public string AuthorLastName { get; internal set; }
        public DbHelper.Content.ContentTypeEnum ContentType => DbHelper.Content.ContentTypeIds.FirstOrDefault(kvp => kvp.Value == ContentTypeId).Key;
        public ModuleContentDetailsInfo() { }
        public ModuleContentDetailsInfo(SqlDataReader dr) {
          SetModuleContentFields(dr);
        }
        internal void SetModuleContentFields(SqlDataReader dr) {
          this.ModuleId = dr.GetInt("ModuleId");
          this.ContentId = dr.GetInt("ContentId");
          this.AddedByUserId = dr.GetInt("AddedByUserId");
          this.DisplayOrder = dr.GetInt("DisplayOrder");
          this.ContentGuid = dr.GetGuid("ContentGuid");
          this.ContentTypeId = dr.GetInt("ContentTypeId");
          this.ContentTitle = dr.GetString("ContentTitle");
          this.ContentSummary = dr.GetString("ContentSummary");
          this.AuthorUserId = dr.GetInt("AuthorUserId");
          this.AuthorTenantOrgId = dr.GetInt("AuthorTenantOrgId");
          this.AuthorFirstName = dr.GetString("AuthorFirstName");
          this.AuthorLastName = dr.GetString("AuthorLastName");
        }
      }

      public class UserModuleContentInfo {
        public int ModuleId { get; private set; }
        public int ContentId { get; private set; }
        public Guid ContentGuid { get; private set; }
        public DateTime? ViewedUtc { get; private set; }
        public decimal? QuizScore { get; private set; }
        public DateTime? FavouriteUtc { get; private set; }
        public DateTime? CompletedUtc { get; private set; }
        public ModuleContentDetailsInfo ModuleContentDetailsInfo { get; private set; }

        public UserModuleContentInfo() { }
        public UserModuleContentInfo(SqlDataReader dr) {
          this.ModuleId = dr.GetInt("ModuleId");
          this.ContentId = dr.GetInt("ContentId");
          this.ContentGuid = dr.GetGuid("ContentGuid");
          this.ViewedUtc = dr.GetDateTimeOrNull("ViewedUtc");
          this.QuizScore = dr.GetDecimalOrNull("QuizScore");
          this.FavouriteUtc = dr.GetDateTimeOrNull("FavouriteUtc");
          this.CompletedUtc = dr.GetDateTimeOrNull("CompletedUtc");
          this.ModuleContentDetailsInfo = new ModuleContentDetailsInfo(dr);
        }

        public bool IsCompleted => this.CompletedUtc != null;
      }

      public class ParticipantModuleInfo {
        public int UserId { get; private set; }
        public int ModuleId { get; private set; }
        public DateTime? EnrolledUtc { get; private set; }
        public DateTime? LastViewedUtc { get; private set; }
        public int? ProgramJobId { get; private set; }
        public DateTime? ModuleStartUtc { get; private set; }
        public DateTime? ModuleDueUtc { get; private set; }
        public ModuleInfo ModuleInfo { get; internal set; }
        public int TotalLessonsInModule { get; private set; }
        public int TotalCompletedLessons { get; private set; }
        public ParticipantModuleInfo() { }
        public ParticipantModuleInfo(SqlDataReader dr) {
          this.UserId = dr.GetInt("UserId");
          this.ModuleId = dr.GetInt("ModuleId");
          this.EnrolledUtc = dr.GetDateTimeOrNull("EnrolledUtc");
          this.LastViewedUtc = dr.GetDateTimeOrNull("LastViewedUtc");
          this.ProgramJobId = dr.GetIntOrNull("ProgramJobId");
          this.ModuleStartUtc = dr.GetDateTimeOrNull("ModuleStartUtc");
          this.ModuleDueUtc = dr.GetDateTimeOrNull("ModuleDueUtc");
          this.TotalLessonsInModule = dr.GetInt("TotalLessonsInModule");
          this.TotalCompletedLessons = dr.GetInt("TotalCompletedLessons");
        }

        public bool IsUserEnrolled => this.EnrolledUtc != null;
        public bool IsLinkedToProgram => this.ProgramJobId != null;
        public bool IsEnrolmentPending => !IsUserEnrolled && IsLinkedToProgram;
        public bool IsCompleted => TotalLessonsInModule == TotalCompletedLessons;
        public int CompletedPercentage => IsUserEnrolled && TotalLessonsInModule > 0 && TotalCompletedLessons > 0 ? (int)(TotalCompletedLessons * 100) / TotalLessonsInModule : 0;
      }

      public class ContentModulesInProgramInfo {
        public ModuleInfo ModuleInfo { get; set; }
        public int ProgramContentModuleId { get; private set; }
        public int ProgramJobId { get; private set; }
        public DateTime AddedUtc { get; private set; }
        public int AddedByUserId { get; private set; }
        public int ModuleDisplayOrder { get; private set; }
        public DateTime? ModuleStartUtc { get; private set; }
        public DateTime? ModuleDueUtc { get; private set; }

        internal ContentModulesInProgramInfo(SqlDataReader dr, ConfigHelper.UserRole userRole) {
          this.ModuleInfo = new ModuleInfo(dr, userRole);
          this.ProgramContentModuleId = dr.GetInt("ProgramContentModuleId");
          this.ProgramJobId = dr.GetInt("ProgramJobId");
          this.AddedUtc = dr.GetDateTime("AddedUtc");
          this.AddedByUserId = dr.GetInt("AddedByUserId");
          this.ModuleDisplayOrder = dr.GetInt("ModuleDisplayOrder");
          this.ModuleStartUtc = dr.GetDateTimeOrNull("ModuleStartUtc");
          this.ModuleDueUtc = dr.GetDateTimeOrNull("ModuleDueUtc");
        }

        public ContentModulesInProgramInfo(ModuleInfo moduleInfo, int programId, DateTime addedUtc, int addedByUserId, int displayOrder, DateTime? moduleStartUtc, DateTime? moduleDueUtc) {
          this.ModuleInfo = moduleInfo;
          this.ProgramJobId = programId;
          this.AddedUtc = addedUtc;
          this.AddedByUserId = addedByUserId;
          this.ModuleDisplayOrder = displayOrder;
          this.ModuleStartUtc = moduleStartUtc;
          this.ModuleDueUtc = moduleDueUtc;
        }
      }
    }
  }
}
