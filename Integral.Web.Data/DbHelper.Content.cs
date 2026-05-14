using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class Content {

      public enum ContentTypeEnum { Text, Image, Document, Url, Video, Quiz, LearningActions }

      private const string TblPfx = "co";

      public static readonly Dictionary<ContentTypeEnum, int> ContentTypeIds = new Dictionary<ContentTypeEnum, int> {
        { ContentTypeEnum.Text, 1 },
        { ContentTypeEnum.Image, 2 },
        { ContentTypeEnum.Document, 3 },
        { ContentTypeEnum.Url, 4 },
        { ContentTypeEnum.Video, 5 },
        { ContentTypeEnum.Quiz, 6 },
        { ContentTypeEnum.LearningActions, 7 }
      };

      private static string ContentTagJoinForSearch => $@"
        OUTER APPLY (
          SELECT pt.TagName, ptc.CategoryName
          FROM al_ContentTag ct
          INNER JOIN al_PartnerTag pt ON pt.PartnerTagId = ct.PartnerTagId
          INNER JOIN al_PartnerTagCategory ptc ON ptc.PartnerTagCategoryId = pt.PartnerTagCategoryId
          WHERE ct.ContentId = {TblPfx}.ContentId
        ) AS ct";

      private static string ContentTagWhereSearchTerm => $@"
        (
             ct.TagName LIKE '%' + @SearchTerm + '%'
          OR ct.CategoryName LIKE '%' + @SearchTerm + '%'
        )";

      private static string GetLeaderExtraJoinsSql() {
        return $@"

          LEFT JOIN (
            SELECT ContentId, UserId
            FROM (
            SELECT cmd.ContentId, cme.UserId
            FROM al_ContentModuleEnrolment cme
            INNER JOIN al_ContentModuleDetails cmd ON cmd.ModuleId = cme.ContentModuleId
            LEFT JOIN al_Program_ContentModule pcm ON pcm.ContentModuleId = cme.ContentModuleId

            UNION

            SELECT pc.ContentId, c.UserId
            FROM al_ProgramContent pc
            LEFT JOIN al_Coachees c
              ON pc.ProgramJobId = c.ProgramJobId
              AND c.DeletedUtc IS NULL
              AND c.CoacheeId IS NOT NULL
            ) AS AccessUnion
          ) plci ON plci.ContentId = {TblPfx}.ContentId AND plci.UserId = @UserId";
      }

      public static ContentInfo AddContent(ContentInfo contentInfo, DbHelper.AbleUser.UserIdentity forUser, ConfigHelper.UserRole userRole) {

        var contentGuid = (Guid)Common.GetScalarQuery($@"
          INSERT INTO al_Content
            (ContentTypeId, AuthorUserId, ContentTitle, ContentHtml, ContentSummary, ShowToParticipants, ShowToPartners, CreatedUtc, ContentUrl, PublishedUtc)
          OUTPUT INSERTED.ContentGUID
          VALUES
            (@ContentTypeId, @AuthorUserId, @ContentTitle, @ContentHtml, @ContentSummary, @ShowToParticipants, @ShowToPartners, @CreatedUtc, @ContentUrl, @PublishedUtc)",
          Common.NewSqlParameter("ContentTypeId", contentInfo.ContentTypeId),
          Common.NewSqlParameter("AuthorUserId", contentInfo.AuthorUserId),
          Common.NewSqlParameter("ContentTitle", contentInfo.ContentTitle),
          Common.NewSqlParameter("ContentHtml", contentInfo.ContentHtml),
          Common.NewSqlParameter("ContentSummary", contentInfo.ContentSummary),
          Common.NewSqlParameter("ShowToParticipants", contentInfo.ShowToParticipants),
          Common.NewSqlParameter("ShowToPartners", contentInfo.ShowToPartners),
          Common.NewSqlParameter("CreatedUtc", DateTime.UtcNow),
          Common.NewSqlParameter("ContentUrl", contentInfo.ContentUrl),
          Common.NewSqlParameter("PublishedUtc", contentInfo.PublishedUtc)
        );

        return GetContentInfo(contentGuid, forUser, userRole);
      }

      public static bool UpdateContent(ContentInfo contentInfo) {

        return Common.GetNonQueryInt($@"
          UPDATE al_Content
          SET
            AuthorUserId = @AuthorUserId,
            ContentTitle = @ContentTitle,
            ContentHtml = @ContentHtml,
            ContentSummary = @ContentSummary,
            ShowToParticipants = @ShowToParticipants,
            ShowToPartners = @ShowToPartners,
            ContentUrl = @ContentUrl,
            PublishedUtc = @PublishedUtc
          WHERE
            ContentGuid = @ContentGuid",
          Common.NewSqlParameter("AuthorUserId", contentInfo.AuthorUserId),
          Common.NewSqlParameter("ContentGuid", contentInfo.ContentGuid),
          Common.NewSqlParameter("ContentTitle", contentInfo.ContentTitle),
          Common.NewSqlParameter("ContentHtml", contentInfo.ContentHtml),
          Common.NewSqlParameter("ContentSummary", contentInfo.ContentSummary),
          Common.NewSqlParameter("ShowToParticipants", contentInfo.ShowToParticipants),
          Common.NewSqlParameter("ShowToPartners", contentInfo.ShowToPartners),
          Common.NewSqlParameter("ContentUrl", contentInfo.ContentUrl),
          Common.NewSqlParameter("PublishedUtc", contentInfo.PublishedUtc)
        ) == 1;
      }

      public static bool DeleteContent(ContentInfo contentInfo) {

        if (contentInfo == null) throw new ArgumentException("contentInfo is null");

        return Common.GetNonQueryInt($@"
          UPDATE al_Content
          SET
            DeletedUtc = @DeletedUtc
          WHERE
            ContentGuid = @ContentGuid",
          Common.NewSqlParameter("ContentGuid", contentInfo.ContentGuid),
          Common.NewSqlParameter("DeletedUtc", DateTime.UtcNow)
        ) == 1;
      }

      public static ContentInfo GetContentInfo(Guid contentGuid, DbHelper.AbleUser.UserIdentity forUser, ConfigHelper.UserRole userRole) {

        var contentList = GetContentInfoList(
          forUser: forUser,
          forUserRole: userRole,
          whereConditionsSQL: $"{TblPfx}.ContentGuid = @ContentGuid",
          extraSelectCols: "",
          extraJoinsSQL: "",
          Common.NewSqlParameter("ContentGuid", contentGuid),
          Common.NewSqlParameter("UserOrgId", forUser?.OrgId),
          Common.NewSqlParameter("UserId", forUser?.UserId)
        );

        if (contentList.IsNullOrEmpty()) return null;
        return contentList[0];
      }

      public static ContentInfo GetContentInfo(int contentId, DbHelper.AbleUser.UserIdentity forUser, ConfigHelper.UserRole userRole) {

        var contentList = GetContentInfoList(
          forUser: forUser,
          forUserRole: userRole,
          whereConditionsSQL: $"{TblPfx}.ContentId = @ContentId",
          extraSelectCols: "",
          extraJoinsSQL: "",
          Common.NewSqlParameter("ContentId ", contentId),
          Common.NewSqlParameter("UserOrgId", forUser?.OrgId),
          Common.NewSqlParameter("UserId", forUser?.UserId)
        );

        if (contentList.IsNullOrEmpty()) return null;
        return contentList[0];
      }

      private static string GetAccessConditionsForUser(DbHelper.AbleUser.UserIdentity forUser, ConfigHelper.UserRole forUserRole) {

        var whereClauses = new List<string>();

        if (forUserRole == ConfigHelper.UserRole.Admin) {

          return $"( {TblPfx}.AuthorUserId = @UserId OR {TblPfx}.PublishedUtc IS NOT NULL )";

        } else if (forUserRole == ConfigHelper.UserRole.Coach) {

          string sql = $"( {TblPfx}.AuthorUserId = @UserId";
          if (forUser.IsTenantOrgAdmin) sql += $" OR u.OrgId = @UserOrgId"; // All in tenant, published or not.
          sql += $" OR ({TblPfx}.PublishedUtc IS NOT NULL AND {TblPfx}.ShowToPartners = 1) )";
          return sql;

        } else if (forUserRole == ConfigHelper.UserRole.Leader) {

          return $"{TblPfx}.PublishedUtc IS NOT NULL AND ({TblPfx}.ShowToParticipants = 1 OR pco.ProgramContentId IS NOT NULL)";

        } else {

          return "0=1";
        }
      }

      public static List<ContentInfo> GetContentForUserAndSearchTerm(DbHelper.AbleUser.UserIdentity forUser, ConfigHelper.UserRole userRole, string searchTerm = "") {

        if (userRole == ConfigHelper.UserRole.Unset) return null;

        string whereClause = GetAccessConditionsForUser(forUser, userRole);
        string extraJoinsSQL = string.Empty;

        if (!searchTerm.IsNullOrEmptyOrWhitespace()) {
          whereClause += $@"
            AND
            (    {TblPfx}.ContentTitle LIKE '%' + @SearchTerm + '%'
              OR cot.ContentTypeName LIKE '%' + @SearchTerm + '%'
              OR {TblPfx}.ContentSummary LIKE '%' + @SearchTerm + '%'
              OR u.FirstName LIKE '%' + @SearchTerm + '%'
              OR u.LastName LIKE '%' + @SearchTerm + '%'
              OR u.FirstName + ' ' + u.LastName LIKE '%' + @SearchTerm + '%'
              OR {ContentTagWhereSearchTerm}
            )";

          // Joins needed when searching.
          extraJoinsSQL = ContentTagJoinForSearch;
        }

        return GetContentInfoList(
          forUser: forUser,
          forUserRole: userRole,
          whereConditionsSQL: whereClause,
          extraSelectCols: null,
          extraJoinsSQL: extraJoinsSQL,
          Common.NewSqlParameter("UserId", forUser.UserId),
          Common.NewSqlParameter("UserOrgId", forUser.OrgId),
          Common.NewSqlParameter("SearchTerm", searchTerm)
        );
      }

      private static string GetContentSelectQuery(string whereConditionsSQL, string extraSelectCols, string extraJoinsSQL, string topQuery) {

        return $@"
          SELECT {topQuery}
            {TblPfx}.ContentId, {TblPfx}.ContentGuid, {TblPfx}.CreatedUtc, {TblPfx}.ContentTypeId, {TblPfx}.ContentTitle, {TblPfx}.AuthorUserId,
            {TblPfx}.ContentHtml, {TblPfx}.ContentSummary, {TblPfx}.ShowToParticipants, {TblPfx}.ShowToPartners, {TblPfx}.ContentUrl, {TblPfx}.PublishedUtc,
            cot.ContentTypeName,
            u.FirstName AS AuthorFirstName, u.LastName AS AuthorLastName, u.Email AS AuthorEmail,
            u.UserGuid AS AuthorUserGuid, u.OrgId AS AuthorTenantOrgId,
            CASE WHEN EXISTS (SELECT 1 FROM al_UserContent uc WHERE uc.ContentId = {TblPfx}.ContentId AND uc.QuizScore IS NOT NULL) THEN 1 ELSE 0 END AS IsRespondedQuiz,
            CASE WHEN lc.ContentId IS NULL THEN 0 ELSE 1 END AS IsLinkedToProgramOrModule
            {extraSelectCols.EnsureStartsWith(", ", true)}
          FROM al_Content {TblPfx}
          INNER JOIN al_ContentType cot ON cot.ContentTypeId = {TblPfx}.ContentTypeId
          INNER JOIN sv_User u ON u.UserId = {TblPfx}.AuthorUserId
          LEFT JOIN (
            SELECT ContentId
            FROM al_ContentModuleDetails
            UNION
            SELECT ContentId
            FROM al_ProgramContent
          ) lc ON lc.ContentId = {TblPfx}.ContentId
          {extraJoinsSQL.EnsureStartsWith(" ", true)}
          WHERE {TblPfx}.DeletedUtc IS NULL
            {whereConditionsSQL.EnsureStartsWith("AND ", true)}
          ORDER BY {TblPfx}.CreatedUtc DESC";
      }

      private static List<ContentInfo> GetContentInfoList(
        DbHelper.AbleUser.UserIdentity forUser,
        ConfigHelper.UserRole forUserRole,
        string whereConditionsSQL,
        string extraSelectCols,
        string extraJoinsSQL,
        params SqlParameter[] whereParams) {

        var sqlParamList = new List<SqlParameter>(whereParams);
        var contentList = new List<ContentInfo>();

        string isAccessibleColumn = "IIF(";
        if (forUserRole == ConfigHelper.UserRole.Admin) {
          isAccessibleColumn += $"1=1";
        } else if (forUserRole == ConfigHelper.UserRole.TenantOrgAdmin) {
          isAccessibleColumn += $"u.OrgId = @UserOrgId OR {TblPfx}.AuthorUserId = @UserId";
        } else if (forUserRole == ConfigHelper.UserRole.Coach) {
          isAccessibleColumn += $"{TblPfx}.AuthorUserId = @UserId";
          if (forUser.IsTenantOrgAdmin) isAccessibleColumn += " OR u.OrgId = @UserOrgId";
          isAccessibleColumn += $" OR ({TblPfx}.PublishedUtc IS NOT NULL AND {TblPfx}.ShowToPartners = 1)";
        } else if (forUserRole == ConfigHelper.UserRole.Leader) {
          isAccessibleColumn += $"plci.ContentId IS NOT NULL OR {TblPfx}.ShowToParticipants = 1";
        } else {
          isAccessibleColumn += "0=1";
        }
        isAccessibleColumn += ", 1, 0) AS IsAccessibleByUser";
        if (!extraSelectCols.IsNullOrEmpty()) extraSelectCols += ",";
        extraSelectCols += isAccessibleColumn;

        if (forUserRole == ConfigHelper.UserRole.Leader) {
          extraJoinsSQL += GetLeaderExtraJoinsSql();
        }

        Common.Query(
          GetContentSelectQuery(whereConditionsSQL, extraSelectCols, extraJoinsSQL, ""),
          sqlParamList,
          dr => {
            contentList.Add(new ContentInfo(dr, false));
          }
        );

        return contentList;
      }

      public static bool RemoveContentFromProgram(SqlTransaction trans, DbHelper.Content.ProgramContentInfo programContentInfo) {

        return Common.GetNonQueryInt(trans, $@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          DELETE FROM al_ProgramContent WHERE ProgramContentId = @ProgramContentId

          UPDATE al_ProgramContent
          SET DisplayOrder = DisplayOrder - 1
          WHERE ProgramJobID = @ProgramJobID
            AND DisplayOrder > @DisplayOrder;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("ProgramContentId", programContentInfo.ProgramContentId),
          Common.NewSqlParameter("ProgramJobID", programContentInfo.ProgramJobId),
          Common.NewSqlParameter("DisplayOrder", programContentInfo.DisplayOrder)
        ) > 0;
      }

      public static bool AddContentToProgram(SqlTransaction trans, ProgramContentInfo programContentInfo) {

        return Common.GetNonQueryInt(trans, $@"
          INSERT INTO al_ProgramContent
            (ProgramJobId, ScheduledSendDateUtc, DisplayOrder, IsContentListed, CreatedByUserId, CreatedUtc, ContentId, WorkshopEventId)
          OUTPUT INSERTED.ProgramContentId
          VALUES
            (@ProgramJobId, @ScheduledSendDateUtc, @DisplayOrder, @IsContentListed, @CreatedByUserId, @CreatedUtc, @ContentId, @WorkshopEventId)",
          Common.NewSqlParameter("ProgramJobId", programContentInfo.ProgramJobId),
          Common.NewSqlParameter("ScheduledSendDateUtc", programContentInfo.ScheduledSendDateUtc),
          Common.NewSqlParameter("DisplayOrder", programContentInfo.DisplayOrder),
          Common.NewSqlParameter("IsContentListed", programContentInfo.IsContentListed),
          Common.NewSqlParameter("CreatedByUserId", programContentInfo.AddedByUserId),
          Common.NewSqlParameter("CreatedUtc", programContentInfo.AddedUtc),
          Common.NewSqlParameter("ContentId", programContentInfo.ContentInfo.ContentId),
          Common.NewSqlParameter("WorkshopEventId", programContentInfo.WorkshopEventId)
        ) > 0;
      }

      public static bool UpsertContentToProgram(SqlTransaction trans, ProgramContentInfo programContentInfo) {

        return Common.GetScalarQueryInt($@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          -- Try to insert a new row if it doesn't already exist
          INSERT INTO al_ProgramContent (ProgramJobId, ScheduledSendDateUtc, DisplayOrder, IsContentListed, CreatedByUserId, CreatedUtc, ContentId, WorkshopEventId)
          SELECT @ProgramJobId, @ScheduledSendDateUtc,
          ISNULL((SELECT MAX(DisplayOrder) + 1 FROM al_ProgramContent WHERE ProgramJobId = @ProgramJobId), 1), -- Calculate DisplayOrder
          @IsContentListed, @CreatedByUserId, @CreatedUtc, @ContentId, @WorkshopEventId
          WHERE NOT EXISTS (
              SELECT 1 FROM al_ProgramContent WITH (UPDLOCK, SERIALIZABLE)
              WHERE ProgramJobId = @ProgramJobId AND ContentId = @ContentId
          );

          -- If no row was inserted, then update the existing row
          IF @@ROWCOUNT = 0
          BEGIN
              UPDATE al_ProgramContent
              SET
                ScheduledSendDateUtc = @ScheduledSendDateUtc,
                IsContentListed = @IsContentListed,
                WorkshopEventId = @WorkshopEventId
              WHERE ProgramJobId = @ProgramJobId AND ContentId = @ContentId;
          END

          -- Return the affected row count to indicate success
          SELECT @@ROWCOUNT;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("ProgramJobId", programContentInfo.ProgramJobId),
          Common.NewSqlParameter("ScheduledSendDateUtc", programContentInfo.ScheduledSendDateUtc),
          Common.NewSqlParameter("DisplayOrder", programContentInfo.DisplayOrder),
          Common.NewSqlParameter("IsContentListed", programContentInfo.IsContentListed),
          Common.NewSqlParameter("CreatedByUserId", programContentInfo.AddedByUserId),
          Common.NewSqlParameter("CreatedUtc", programContentInfo.AddedUtc),
          Common.NewSqlParameter("ContentId", programContentInfo.ContentInfo.ContentId),
          Common.NewSqlParameter("WorkshopEventId", programContentInfo.WorkshopEventId)) > 0;
      }

      public static List<ProgramContentInfo> GetProgramContentListForEmail(AblePrograms.AbleProgramInfo programInfo, DbHelper.AbleUser.UserIdentity forUser) {

        if (programInfo == null) throw new NullReferenceException(nameof(programInfo));
        if (forUser == null) throw new NullReferenceException(nameof(forUser));

        return GetProgramContentList($@"
          (
               pco.ProgramJobId = @ProgramJobId
            OR {TblPfx}.ShowToPartners = 1
            OR {TblPfx}.ShowToParticipants = 1
            OR {TblPfx}.AuthorUserId = @UserId
            {forUser.IsTenantOrgAdmin.ToValue("OR u.OrgId = @AuthorTenantOrgId")}
          )",
          Common.NewSqlParameter("ProgramJobId", programInfo.ProgramJobId),
          Common.NewSqlParameter("UserId", forUser.UserId),
          Common.NewSqlParameter("AuthorTenantOrgId", forUser.OrgId)
        );
      }

      public static List<ProgramContentInfo> GetProgramContentList(AblePrograms.AbleProgramInfo program) {

        return GetProgramContentList(
          $"pco.ProgramJobId = @ProgramJobId",
          Common.NewSqlParameter("ProgramJobId", program.ProgramJobId)
        );
      }

      public static List<ProgramContentInfo> GetProgramContentByJobNumber(string jobNumber, bool onlyScheduled) {

        string whereClause = "";
        if (onlyScheduled) {
          whereClause = "pco.ScheduledSendDateUtc IS NOT NULL";
        }

        return GetProgramContentList(
          $"j.JobNumber = @ProgramJobNumber {whereClause.EnsureStartsWith(" AND ", true)}",
          Common.NewSqlParameter("ProgramJobNumber", jobNumber)
        );
      }

      public static ProgramContentInfo GetProgramContent(AblePrograms.AbleProgramInfo program, Content.ContentInfo content) {

        var programContentInfo = GetProgramContentList(
          $"pco.ProgramJobId = @ProgramJobId AND {TblPfx}.ContentId = @ContentId",
          Common.NewSqlParameter("ProgramJobId", program.ProgramJobId),
          Common.NewSqlParameter("ContentId", content.ContentId)
        );

        if (programContentInfo.IsNullOrEmpty()) return null;
        return programContentInfo[0];
      }

      private static List<ProgramContentInfo> GetProgramContentList(string whereConditionsSQL, params SqlParameter[] whereParams) {

        var sqlParamList = new List<SqlParameter>(whereParams);

        var programContentList = new List<ProgramContentInfo>();

        Common.Query(
          GetContentSelectQuery(whereConditionsSQL,
            @"pco.ProgramContentId, pco.ProgramJobId, pco.ScheduledSendDateUtc, pco.SentDateUtc, pco.DisplayOrder, pco.IsContentListed, pco.CreatedUtc AS AddedUtc,
              pco.CreatedByUserId AS AddedByUserId, au.FirstName AS AddedByFirstName, au.LastName AS AddedByLastName, au.Email AS AddedByEmail,
              j.JobNumber AS ProgramJobNumber, j.JobName as ProgramName,
              pco.WorkshopEventId",
            @"
              LEFT JOIN al_ProgramContent pco on pco.ContentId = co.ContentId
              LEFT JOIN sv_User au ON au.UserId = pco.CreatedByUserId
              LEFT JOIN id_Job j on j.JobId = pco.ProgramJobId", ""),
          sqlParamList,
          dr => {
            programContentList.Add(new ProgramContentInfo(dr));
          }
        );

        return programContentList;
      }

      public static List<ParticipantContentInfo> GetContentListForUser(DbHelper.AbleUser.UserIdentity forUser, string searchTerm) {

        var extraJoinsSQL = new StringBuilder();
        var contentList = new List<ParticipantContentInfo>();
        var sqlParamList = new List<SqlParameter>();

        sqlParamList.Add(Common.NewSqlParameter("UserId", forUser.UserId));

        string whereClause = GetAccessConditionsForUser(forUser, ConfigHelper.UserRole.Leader);

        extraJoinsSQL.Append($@"
          LEFT JOIN al_Coachees ac ON ac.UserId = @UserId AND ac.DeletedUtc IS NULL
          LEFT JOIN al_ProgramContent pco on pco.ProgramJobId = ac.ProgramJobId AND pco.ContentId = {TblPfx}.ContentId
          LEFT JOIN al_UserContent uc ON uc.ContentId = {TblPfx}.ContentId AND uc.UserId = @UserId
          LEFT JOIN id_Job j on j.JobId = pco.ProgramJobId");

        if (!searchTerm.IsNullOrEmptyOrWhitespace()) {

          sqlParamList.Add(Common.NewSqlParameter("SearchTerm", searchTerm));

          if (!whereClause.IsNullOrEmpty()) whereClause += " AND ";
          whereClause += $@"
            (
                 {TblPfx}.ContentTitle LIKE '%' + @SearchTerm + '%'
              OR cot.ContentTypeName LIKE '%' + @SearchTerm + '%'
              OR u.FirstName LIKE '%' + @SearchTerm + '%'
              OR u.LastName LIKE '%' + @SearchTerm + '%'
              OR u.FirstName + ' ' + u.LastName LIKE '%' + @SearchTerm + '%'
              OR {ContentTagWhereSearchTerm}
            )";

          // Joins needed when searching.
          extraJoinsSQL.Append(ContentTagJoinForSearch);
        }

        Common.Query(
          GetContentSelectQuery($@"
            ({whereClause})",
            @"pco.ProgramContentId, pco.ProgramJobId, pco.DisplayOrder, pco.CreatedUtc AS AddedToProgramUtc, pco.WorkshopEventId,
              uc.CompletedUtc, uc.ViewedUtc, uc.FavouriteUtc, uc.RelevanceScore, uc.QuizScore, uc.LearningActionText,
              j.JobNumber AS ProgramJobNumber, j.JobName as ProgramName",
            extraJoinsSQL.ToString(),
            "DISTINCT"),
          sqlParamList,
          dr => {
            contentList.Add(new ParticipantContentInfo(dr));
          }
        );

        return contentList;
      }

      public static bool UpdateContentTags(Content.ContentInfo content, List<ContentTagInfo> existingTags, List<int> selectedTags) {

        if (selectedTags.IsNullOrEmpty()) throw new ArgumentException("Component must have at least one tag");

        var tagsToRemove = new List<ContentTagInfo>();
        var tagsToAdd = new List<int>();

        if (!existingTags.IsNullOrEmpty()) {
          tagsToRemove = existingTags.Where(et => !selectedTags.Contains(et.TagId)).ToList();
          tagsToAdd = selectedTags.Where(st => !existingTags.Any(et => et.TagId == st)).ToList();
        } else {
          tagsToAdd = selectedTags;
        }

        int tagsToUpdate = tagsToAdd.Count + tagsToRemove.Count;
        int tagsUpdated = 0;

        if (!tagsToRemove.IsNullOrEmpty()) {

          var tagsWhereClause = string.Join(" OR ", tagsToRemove.Select((t, index) => $"PartnerTagId = @TagId{index}"));

          var sqlParamList = tagsToRemove.Select((t, index) => new SqlParameter($"@TagId{index}", t.TagId)).ToList();
          sqlParamList.AddRange(sqlParamList);

          int rows = Common.GetNonQueryInt($@"
          DELETE FROM al_ContentTag
          WHERE ContentId = @ContentId AND ({tagsWhereClause})",
          sqlParamList.ToArray());

          if (rows > 0) tagsUpdated = tagsToRemove.Count;
        }

        if (!tagsToAdd.IsNullOrEmpty()) {

          foreach (var tagId in tagsToAdd) {
            var rows = Common.GetScalarQueryInt($@"
              INSERT INTO al_ContentTag (ContentId, PartnerTagId)
              OUTPUT INSERTED.ContentTagId
              VALUES (@ContentId, @PartnerTagId)",
              Common.NewSqlParameter("ContentId", content.ContentId),
              Common.NewSqlParameter("PartnerTagId", tagId)
            );

            if (rows > 0) tagsUpdated++;
          }
        }

        return tagsUpdated == tagsToUpdate;
      }

      public static List<ContentTagInfo> GetContentTagsInfo() {

        return GetTagsInfo("ptc.IsContent = 1", null, null);
      }

      public static List<ContentTagInfo> GetTagsInfoByContentId(Content.ContentInfo content) {

        return GetTagsInfo(
          "EXISTS(Select 1 from al_ContentTag ct WHERE ct.ContentId = @ContentId AND ct.PartnerTagId = pt.PartnerTagId)",
          null,
          Common.NewSqlParameter("ContentId", content.ContentId));
      }

      private static List<ContentTagInfo> GetTagsInfo(string whereClause, string extraJoins, params SqlParameter[] whereParams) {

        var sqlParamList = new List<SqlParameter>();
        if (whereParams != null) sqlParamList = new List<SqlParameter>(whereParams);

        var contentTagInfo = new List<ContentTagInfo>();

        Common.Query($@"
          SELECT
            ptc.PartnerTagCategoryId, ptc.CategoryName,
            pt.PartnerTagId, pt.TagName, pt.PartnerCanEdit, pt.PartnerUIHidden
          FROM al_PartnerTagCategory ptc
          INNER JOIN al_PartnerTag pt ON ptc.PartnerTagCategoryId = pt.PartnerTagCategoryId
          {extraJoins.EmptyIfNull()}
          {whereClause.EnsureStartsWith(" WHERE ", true)}
          ORDER BY pt.TagName",
          dr => {
            contentTagInfo.Add(new ContentTagInfo(
              dr.GetInt("PartnerTagId"),
              dr.GetString("TagName"),
              dr.GetBoolFromInt("PartnerCanEdit"),
              dr.GetBoolFromInt("PartnerUIHidden")
            ));
          },
          sqlParamList.ToArray()
        );
        return contentTagInfo;
      }

      public static int UpsertContentViewed(SqlTransaction trans, Content.ContentInfo content, DbHelper.AbleUser.UserIdentity user) {

        return Common.GetScalarQueryInt($@"
          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          DECLARE @ContentUserId INT;

          -- Try to insert a new row if it doesn't already exist
          INSERT INTO al_UserContent (UserId, ContentId, ViewedUtc)
          SELECT @UserId, @ContentId, @ViewedUtc
          WHERE NOT EXISTS (
              SELECT 1 FROM al_UserContent WITH (UPDLOCK, SERIALIZABLE)
              WHERE Userid = @UserId AND ContentId = @ContentId
          );

          -- If a row was inserted, retrieve its ContentUserId
          IF @@ROWCOUNT = 1
          BEGIN
            SET @ContentUserId = SCOPE_IDENTITY();
          END
          ELSE
          BEGIN
            -- Update the existing row and retrieve its ContentUserId
            UPDATE al_UserContent
            SET ViewedUtc = @ViewedUtc
            OUTPUT INSERTED.UserContent
            WHERE Userid = @UserId AND ContentId = @ContentId;
          END

          -- Return the ContentUserId
          SELECT @ContentUserId;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

        Common.NewSqlParameter("UserId", user.UserId),
        Common.NewSqlParameter("ContentId", content.ContentId),
        Common.NewSqlParameter("ViewedUtc", DateTime.UtcNow));
      }

      public static int UpsertContentCompleted(SqlTransaction trans, Content.ContentInfo content, DbHelper.AbleUser.UserIdentity user) {

        return Common.GetScalarQueryInt($@"
          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          DECLARE @ContentUserId INT;

          -- Try to insert a new row if it doesn't already exist
          INSERT INTO al_UserContent (UserId, ContentId, CompletedUtc)
          SELECT @UserId, @ContentId, @CompletedUtc
          WHERE NOT EXISTS (
              SELECT 1 FROM al_UserContent WITH (UPDLOCK, SERIALIZABLE)
              WHERE Userid = @UserId AND ContentId = @ContentId
          );

          -- If a row was inserted, retrieve its ContentUserId
          IF @@ROWCOUNT = 1
          BEGIN
            SET @ContentUserId = SCOPE_IDENTITY();
          END
          ELSE
          BEGIN
            -- Update the existing row and retrieve its ContentUserId
            UPDATE al_UserContent
            SET CompletedUtc = @CompletedUtc
            OUTPUT INSERTED.UserContent
            WHERE Userid = @UserId AND ContentId = @ContentId;
          END

          -- Return the ContentUserId
          SELECT @ContentUserId;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

        Common.NewSqlParameter("UserId", user.UserId),
        Common.NewSqlParameter("ContentId", content.ContentId),
        Common.NewSqlParameter("CompletedUtc", DateTime.UtcNow));
      }

      public static void UpsertContentFavorite(SqlTransaction trans, Content.ContentInfo content, DbHelper.AbleUser.UserIdentity user, bool isFavorite) {

        DateTime? favoriteDate = null;
        if (isFavorite) {
          favoriteDate = DateTime.UtcNow;
        }

        Common.GetScalarQueryInt($@"
          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          -- Try to insert a new row if it doesn't already exist
          INSERT INTO al_UserContent (UserId, ContentId, FavouriteUtc)
          SELECT @UserId, @ContentId, @FavouriteUtc
          WHERE NOT EXISTS (
              SELECT 1 FROM al_UserContent WITH (UPDLOCK, SERIALIZABLE)
              WHERE Userid = @UserId AND ContentId = @ContentId
          );

          -- If no row was inserted, then update the existing row
          IF @@ROWCOUNT = 0
          BEGIN
              UPDATE al_UserContent
              SET FavouriteUtc = @FavouriteUtc
              WHERE Userid = @UserId AND ContentId = @ContentId;
          END

          -- Return the affected row count to indicate success
          SELECT @@ROWCOUNT;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

        Common.NewSqlParameter("UserId", user.UserId),
        Common.NewSqlParameter("ContentId", content.ContentId),
        Common.NewSqlParameter("FavouriteUtc", favoriteDate));
      }

      public static bool UpsertContentLearningAction(SqlTransaction trans, Content.ContentInfo content, DbHelper.AbleUser.UserIdentity user, string learningAction) {

        return Common.GetScalarQueryInt($@"
          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          -- Try to insert a new row if it doesn't already exist
          INSERT INTO al_UserContent (UserId, ContentId, LearningActionText, CompletedUtc)
          SELECT @UserId, @ContentId, @LearningActionText, @CompletedUTC
          WHERE NOT EXISTS (
              SELECT 1 FROM al_UserContent WITH (UPDLOCK, SERIALIZABLE)
              WHERE Userid = @UserId AND ContentId = @ContentId
          );

          -- If no row was inserted, then update the existing row
          IF @@ROWCOUNT = 0
          BEGIN
              UPDATE al_UserContent
              SET
                CompletedUtc = @CompletedUTC,
                LearningActionText = @LearningActionText
              WHERE Userid = @UserId AND ContentId = @ContentId;
          END

          -- Return the affected row count to indicate success
          SELECT @@ROWCOUNT;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

        Common.NewSqlParameter("UserId", user.UserId),
        Common.NewSqlParameter("ContentId", content.ContentId),
        Common.NewSqlParameter("CompletedUTC", DateTime.UtcNow),
        Common.NewSqlParameter("LearningActionText", learningAction)) > 0;
      }

      public static UserContentInfo GetUserContentInfo(int userContentId) {

        UserContentInfo userContentList = null;

        Common.Query($@"
          SELECT
            uc.UserContent, uc.UserId, uc.ContentId, uc.CreatedUtc, uc.ScheduledSendDateUtc, uc.SentDateUtc, uc.CompletedUtc, uc.ViewedUtc,
            uc.AIRecommendationReason, uc.FavouriteUtc, uc.QuizScore, uc.LearningActionText,
            qr.QuestionId, qr.OptionId, qo.IsCorrect
          FROM al_UserContent uc
          INNER JOIN al_Content c ON c.ContentId = uc.ContentId
          LEFT JOIN al_ContentQuizResponses qr ON qr.UserContentId = uc.UserContent
          LEFT JOIN al_ContentQuizOptions qo ON qo.OptionId = qr.OptionId
          WHERE uc.UserContent = @UserContentId
          ORDER BY uc.UserContent",
          dr => {

            if (userContentList == null) {
              userContentList = new UserContentInfo(dr);
            }

            var questionId = dr.GetIntOrNull("QuestionId");
            if (questionId != null) {
              var userResponseInfo = new UserResponsesInfo(dr);
              userContentList.QuizResponses.Add(userResponseInfo);
            }

          },
          Common.NewSqlParameter("UserContentId", userContentId)
        );
        return userContentList;
      }

      public static List<ScheduledContentInfo> GetScheduledContentToSend() {

        var sqlParamList = new List<SqlParameter>();
        sqlParamList.Add(Common.NewSqlParameter("CurrentUtc", DateTime.UtcNow));

        var scheduledContentList = new List<ScheduledContentInfo>();

        string whereConditionsSQL = $@"
          CAST(pco.ScheduledSendDateUtc AS DATE) = CAST(GETUTCDATE() AS DATE)
          AND pco.SentDateUtc IS NULL
          AND ac.DeletedUtc IS NULL";

        Common.Query(
          GetContentSelectQuery(whereConditionsSQL,
            @"pco.ProgramContentId, pco.ProgramJobId, pco.ScheduledSendDateUtc, pco.SentDateUtc,
              ac.CoacheeId, ac.FirstName AS CoacheeFirstName, ac.LastName AS CoacheeLastName, ac.EmailAddress AS CoacheeEmail,
              j.JobNumber AS ProgramJobNumber,
              ap.SvCompanyId",
            @"
              LEFT JOIN al_ProgramContent pco on pco.ContentId = co.ContentId
              LEFT JOIN al_Coachees ac ON ac.ProgramJobId = pco.ProgramJobId
              LEFT JOIN id_Job j ON j.JobId = pco.ProgramJobId
              LEFT JOIN al_Project ap ON ap.JobNumber = j.JobNumber",
            ""),
          sqlParamList,
          dr => {
            scheduledContentList.Add(new ScheduledContentInfo(dr));
          }
        );

        return scheduledContentList;
      }

      public static bool UpdateScheduledContentSent(int? programContentId) {

        if (!programContentId.HasValue) return false;

        return Common.GetNonQueryInt($@"
          UPDATE al_ProgramContent
          SET SentDateUtc = @CurrentDateUtc
          WHERE ProgramContentId = @ProgramContentId",
          Common.NewSqlParameter("ProgramContentId", programContentId),
          Common.NewSqlParameter("CurrentDateUtc", DateTime.UtcNow)
        ) > 0;
      }

      public static List<QuizInfo> GetQuizContentInfo(Content.ContentInfo content) {

        var quizList = new List<QuizInfo>();
        var sqlParamList = new List<SqlParameter>();

        sqlParamList.Add(Common.NewSqlParameter("ContentId", content.ContentId));

        Common.Query(@"
          SELECT
            qq.QuestionId, qq.Question, qo.OptionId, qo.OptionText, qo.DisplayOrder, qo.IsCorrect
          FROM al_ContentQuizQuestions qq
          INNER JOIN al_ContentQuizOptions qo ON qo.QuestionId = qq.QuestionId
          WHERE qq.ContentId = @ContentId",
          sqlParamList,
          dr => {
            var questionId = dr.GetInt("QuestionId");
            if (quizList.IsNullOrEmpty() || !quizList.Any(q => q.QuestionId == questionId)) {
              quizList.Add(new QuizInfo(dr));
            }

            var questionInfo = quizList.First(q => q.QuestionId == questionId);
            var optionInfo = new QuizOptionsInfo(dr);
            questionInfo.Options.Add(optionInfo);
          }
        );
        return quizList;
      }

      public static void UpdateQuizContent(Content.ContentInfo content, List<QuizInfo> quizInfoList) {

        if (quizInfoList.IsNullOrEmpty()) return;

        var sqlParamList = new List<SqlParameter>();
        sqlParamList.Add(Common.NewSqlParameter("ContentId", content.ContentId));

        Common.UsingTransaction(trans => {

          Common.GetNonQueryInt(trans, $@"
            DELETE FROM al_ContentQuizOptions WHERE QuestionId IN (SELECT QuestionId FROM al_ContentQuizQuestions WHERE ContentId = @ContentId);
            DELETE FROM al_ContentQuizQuestions WHERE ContentId = @ContentId;",
            sqlParamList.ToArray()
          );

          foreach (var quizInfo in quizInfoList) {
            var questionId = Common.GetScalarQueryInt(trans, $@"
              INSERT INTO al_ContentQuizQuestions (ContentId, Question)
              OUTPUT INSERTED.QuestionId
              VALUES (@ContentId, @Question)",
              Common.NewSqlParameter("ContentId", content.ContentId),
              Common.NewSqlParameter("Question", quizInfo.Question)
            );

            if (questionId == 0) return false;

            foreach (var option in quizInfo.Options) {
              var rows = Common.GetNonQueryInt(trans, $@"
                INSERT INTO al_ContentQuizOptions (QuestionId, OptionText, DisplayOrder, IsCorrect)
                VALUES (@QuestionId, @OptionText, @DisplayOrder, @IsCorrect)",
                Common.NewSqlParameter("QuestionId", questionId),
                Common.NewSqlParameter("OptionText", option.OptionText),
                Common.NewSqlParameter("DisplayOrder", option.DisplayOrder),
                Common.NewSqlParameter("IsCorrect", option.IsCorrect)
              );

              if (rows == 0) return false;
            }
          }

          return true;
        });
      }

      public static bool UpdateUserQuizResponses(int userContentId, double quizScore, List<UserResponsesInfo> responseList) {

        if (responseList.IsNullOrEmpty()) return false;

        var sqlParamList = new List<SqlParameter>();
        sqlParamList.Add(Common.NewSqlParameter("UserContentId", userContentId));
        sqlParamList.Add(Common.NewSqlParameter("QuizScore", quizScore.ToString()));

        return Common.UsingTransaction(trans => {

          Common.GetNonQueryInt(trans, $@"
            UPDATE al_UserContent SET QuizScore = @QuizScore, CompletedUtc = @CompletedUtc WHERE UserContent = @UserContentId;",
            Common.NewSqlParameter("UserContentId", userContentId),
            Common.NewSqlParameter("CompletedUtc", DateTime.UtcNow),
            Common.NewSqlParameter("QuizScore", quizScore.ToString())
          );

          foreach (var response in responseList) {
            var responseId = Common.GetScalarQueryInt(trans, $@"
              INSERT INTO al_ContentQuizResponses (UserContentId, QuestionId, OptionId)
              OUTPUT INSERTED.ResponseId
              VALUES (@UserContentId, @QuestionId, @OptionId)",
              Common.NewSqlParameter("UserContentId", userContentId),
              Common.NewSqlParameter("QuestionId", response.QuestionId),
              Common.NewSqlParameter("OptionId", response.OptionId)
            );

            if (responseId == 0) return false;
          }

          return true;
        });
      }

      public class UserContentInfo {
        public int ContentUserId { get; private set; }
        public int UserId { get; private set; }
        public int ContentId { get; private set; }
        public DateTime? CreatedUtc { get; private set; }
        public DateTime? ScheduledSendDateUtc { get; private set; }
        public DateTime? SentDateUtc { get; private set; }
        public DateTime? CompletedUtc { get; private set; }
        public DateTime? ViewedUtc { get; private set; }
        public string AIRecommendationReason { get; private set; }
        public DateTime? FavouriteUtc { get; private set; }
        public decimal? QuizScore { get; private set; }
        public List<UserResponsesInfo> QuizResponses { get; private set; }
        public string LearningActionText { get; private set; }

        public UserContentInfo() { }

        public UserContentInfo(SqlDataReader dr) {
          this.ContentUserId = dr.GetInt("UserContent");
          this.UserId = dr.GetInt("UserId");
          this.ContentId = dr.GetInt("ContentId");
          this.CreatedUtc = dr.GetDateTimeOrNull("CreatedUtc");
          this.ScheduledSendDateUtc = dr.GetDateTimeOrNull("ScheduledSendDateUtc");
          this.SentDateUtc = dr.GetDateTimeOrNull("SentDateUtc");
          this.CompletedUtc = dr.GetDateTimeOrNull("CompletedUtc");
          this.ViewedUtc = dr.GetDateTimeOrNull("ViewedUtc");
          this.AIRecommendationReason = dr.GetString("AIRecommendationReason");
          this.FavouriteUtc = dr.GetDateTimeOrNull("FavouriteUtc");
          this.QuizScore = dr.GetDecimalOrNull("QuizScore");
          this.QuizResponses = new List<UserResponsesInfo>();
          this.LearningActionText = dr.GetString("LearningActionText");
        }

        public UserContentInfo(int contentUserId, int userId, int contentId, DateTime? createdUtc, DateTime? scheduledSendDateUtc, DateTime? sentDateUtc, DateTime? completedUtc, DateTime? viewedUtc) {
          this.ContentUserId = contentUserId;
          this.UserId = userId;
          this.ContentId = contentId;
          this.CreatedUtc = createdUtc;
          this.ScheduledSendDateUtc = scheduledSendDateUtc;
          this.SentDateUtc = sentDateUtc;
          this.CompletedUtc = completedUtc;
          this.ViewedUtc = viewedUtc;
        }

        public bool IsCompleted => this.CompletedUtc != null;
        public bool IsFavorited => this.FavouriteUtc != null;
      }

      public class ContentTagInfo {
        public int CategoryId { get; private set; }
        public int TagId { get; private set; }
        public string TagName { get; private set; }
        public bool PartnerCanEdit { get; private set; }
        public bool PartnerUIHidden { get; private set; }
        public ContentTagInfo(int tagId, string tagName, bool partnerCanEdit, bool partnerUIHidden) {
          this.TagId = tagId;
          this.TagName = tagName;
          this.PartnerCanEdit = partnerCanEdit;
          this.PartnerUIHidden = partnerUIHidden;
        }
      }

      public class ContentInfo {

        public int ContentId { get; private set; }
        public Guid ContentGuid { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public int ContentTypeId { get; set; }
        public string ContentTitle { get; set; }
        public int AuthorUserId { get; set; }
        public int AuthorTenantOrgId { get; set; }
        public string ContentHtml { get; set; }
        public string ContentSummary { get; set; }
        public bool ShowToParticipants { get; set; }
        public bool ShowToPartners { get; set; }
        public DateTime? PublishedUtc { get; set; }
        public string ContentTypeName { get; private set; }
        public string ContentUrl { get; set; }
        public string AuthorFirstName { get; private set; }
        public string AuthorLastName { get; private set; }
        public string AuthorEmail { get; private set; }
        public Guid AuthorUserGuid { get; private set; }
        public ContentTypeEnum ContentType => ContentTypeIds.FirstOrDefault(kvp => kvp.Value == ContentTypeId).Key;
        public bool IsRespondedQuiz { get; private set; }
        public bool IsLinkedToProgramOrModule { get; private set; }
        public bool IsAccessibleByUser { get; private set; }

        public ContentInfo() { }

        public ContentInfo(SqlDataReader dr, bool isSubquery) {
          this.ContentId = dr.GetInt("ContentId");
          this.ContentGuid = dr.GetGuid("ContentGuid");
          this.CreatedUtc = dr.GetDateTime("CreatedUtc");
          this.ContentTypeId = dr.GetInt("ContentTypeId");
          this.ContentTitle = dr.GetString("ContentTitle");
          this.AuthorUserId = dr.GetInt("AuthorUserId");
          this.AuthorTenantOrgId = dr.GetInt("AuthorTenantOrgId");
          this.ContentHtml = dr.GetString("ContentHtml");
          this.ContentSummary = dr.GetString("ContentSummary");
          this.ShowToParticipants = dr.GetBoolFromInt("ShowToParticipants", false);
          this.ShowToPartners = dr.GetBoolFromInt("ShowToPartners", false);
          this.ContentTypeName = dr.GetString("ContentTypeName");
          this.ContentUrl = dr.GetString("ContentUrl");
          this.AuthorFirstName = dr.GetString("AuthorFirstName");
          this.AuthorLastName = dr.GetString("AuthorLastName");
          this.AuthorEmail = dr.GetString("AuthorEmail");
          this.PublishedUtc = dr.GetDateTimeOrNull("PublishedUtc");
          this.IsRespondedQuiz = dr.GetBoolFromInt("IsRespondedQuiz", false);
          this.IsLinkedToProgramOrModule = dr.GetBoolFromInt("IsLinkedToProgramOrModule", false);

          if (!isSubquery) {
            // Only assign when the query returns ContentInfo, not from subqueries (like ParticipantContentInfo or Program)
            this.IsAccessibleByUser = dr.GetBoolFromInt("IsAccessibleByUser", false);
          }
        }

        public string GetAuthorFullName() {
          return AuthorFirstName + " " + AuthorLastName;
        }

        public bool IsPublished => PublishedUtc.HasValue;
      }

      public class ParticipantContentInfo {
        public int? ProgramContentId { get; private set; }
        public int? ProgramJobId { get; private set; }
        public string ProgramJobNumber { get; private set; }
        public int? DisplayOrder { get; private set; }
        public string ProgramName { get; private set; }
        public DateTime? ViewedUtc { get; private set; }
        public DateTime? FavouriteUtc { get; private set; }
        public DateTime? CompletedUtc { get; private set; }
        public bool IsFavourite { get; private set; }
        public DateTime? AddedToProgramUtc { get; private set; }
        public decimal? RelevanceScore { get; private set; }
        public int? WorkshopEventId { get; private set; }
        public ContentInfo ContentInfo { get; set; }
        public decimal? QuizScore { get; private set; }
        public string LearningActionText { get; private set; }

        public ParticipantContentInfo() { }

        public ParticipantContentInfo(SqlDataReader dr) {
          this.ProgramContentId = dr.GetIntOrNull("ProgramContentId");
          this.ProgramJobId = dr.GetIntOrNull("ProgramJobId");
          this.ProgramJobNumber = dr.GetString("ProgramJobNumber");
          this.DisplayOrder = dr.GetIntOrNull("DisplayOrder");
          this.ProgramName = dr.GetString("ProgramName");
          this.ViewedUtc = dr.GetDateTimeOrNull("ViewedUtc");
          this.FavouriteUtc = dr.GetDateTimeOrNull("FavouriteUtc");
          this.IsFavourite = this.FavouriteUtc != null;
          this.AddedToProgramUtc = dr.GetDateTimeOrNull("AddedToProgramUtc");
          this.RelevanceScore = dr.GetDecimalOrNull("RelevanceScore");
          this.WorkshopEventId = dr.GetIntOrNull("WorkshopEventId");
          this.ContentInfo = new ContentInfo(dr, true);
          this.QuizScore = dr.GetDecimalOrNull("QuizScore");
          this.CompletedUtc = dr.GetDateTimeOrNull("CompletedUtc");
          this.LearningActionText = dr.GetString("LearningActionText");
        }

        public bool IsCompleted => this.CompletedUtc != null;
        public bool IsRespondedQuiz => IsCompleted && this.ContentInfo.ContentType == ContentTypeEnum.Quiz;
      }

      public class ProgramContentInfo {
        public int? ProgramContentId { get; private set; }
        public int? ProgramJobId { get; private set; }
        public string ProgramJobNumber { get; private set; }
        public string ProgramName { get; private set; }
        public DateTime? ScheduledSendDateUtc { get; private set; }
        public DateTime? SentDateUtc { get; private set; }
        public int? DisplayOrder { get; set; }
        public bool IsContentListed { get; private set; }
        public int? AddedByUserId { get; private set; }
        public string AddedByFirstName { get; private set; }
        public string AddedByLastName { get; private set; }
        public string AddedByEmail { get; private set; }
        public DateTime? AddedUtc { get; private set; }
        public ContentInfo ContentInfo { get; set; }
        public int? WorkshopEventId { get; private set; }
        public ProgramContentInfo() { }
        public ProgramContentInfo(SqlDataReader dr) {
          this.ProgramContentId = dr.GetIntOrNull("ProgramContentId");
          this.ProgramJobId = dr.GetIntOrNull("ProgramJobId");
          this.ProgramJobNumber = dr.GetString("ProgramJobNumber");
          this.ProgramName = dr.GetString("ProgramName");
          this.ProgramJobId = dr.GetIntOrNull("ProgramJobId");
          this.ScheduledSendDateUtc = dr.GetDateTimeOrNull("ScheduledSendDateUtc");
          this.SentDateUtc = dr.GetDateTimeOrNull("SentDateUtc");
          this.DisplayOrder = dr.GetIntOrNull("DisplayOrder");
          this.IsContentListed = dr.GetBoolFromInt("IsContentListed", false);
          this.AddedByUserId = dr.GetIntOrNull("AddedByUserId");
          this.AddedByFirstName = dr.GetString("AddedByFirstName");
          this.AddedByLastName = dr.GetString("AddedByLastName");
          this.AddedByEmail = dr.GetString("AddedByEmail");
          this.AddedUtc = dr.GetDateTimeOrNull("AddedUtc");
          this.ContentInfo = new ContentInfo(dr, true);
          this.WorkshopEventId = dr.GetIntOrNull("WorkshopEventId");
        }

        public ProgramContentInfo(int programJobId, DateTime? scheduledSendDateUtc, int displayOrder, bool isContentListed,
          int addedByUserId, DateTime addedUtc, ContentInfo contentInfo, int? workshopEventId) {
          this.ProgramJobId = programJobId;
          this.ScheduledSendDateUtc = scheduledSendDateUtc;
          this.DisplayOrder = displayOrder;
          this.IsContentListed = isContentListed;
          this.AddedByUserId = addedByUserId;
          this.AddedUtc = addedUtc;
          this.ContentInfo = contentInfo;
          this.WorkshopEventId = workshopEventId;
        }

        public string GetAddedByFullName() {
          return AddedByFirstName + " " + AddedByLastName;
        }
      }

      public class ScheduledContentInfo {
        public int ProgramContentId { get; private set; }
        public int ProgramJobId { get; private set; }
        public string ProgramJobNumber { get; private set; }
        public int? CompanyId { get; private set; }
        public int? CoacheeId { get; private set; }
        public string CoacheeFirstName { get; private set; }
        public string CoacheeLastName { get; private set; }
        public string CoacheeEmail { get; private set; }
        public DateTime? ScheduledSendDateUtc { get; private set; }
        public string FromEmailName { get; private set; }
        public ContentInfo ContentInfo { get; set; }

        public ScheduledContentInfo(SqlDataReader dr) {
          this.ProgramContentId = dr.GetInt("ProgramContentId");
          this.ProgramJobId = dr.GetInt("ProgramJobId");
          this.ProgramJobNumber = dr.GetString("ProgramJobNumber");
          this.CompanyId = dr.GetIntOrNull("SvCompanyId");
          this.CoacheeId = dr.GetIntOrNull("CoacheeId");
          this.CoacheeFirstName = dr.GetString("CoacheeFirstName");
          this.CoacheeLastName = dr.GetString("CoacheeLastName");
          this.CoacheeEmail = dr.GetString("CoacheeEmail");
          this.ScheduledSendDateUtc = dr.GetDateTimeOrNull("ScheduledSendDateUtc");
          this.ContentInfo = new ContentInfo(dr, true);
        }
      }

      public class QuizInfo {
        public int QuestionId { get; private set; }
        public string Question { get; set; }
        public List<QuizOptionsInfo> Options { get; set; }
        public QuizInfo() { }
        public QuizInfo(SqlDataReader dr) {
          this.QuestionId = dr.GetInt("QuestionId");
          this.Question = dr.GetString("Question");
          this.Options = new List<QuizOptionsInfo>();
        }
        public QuizInfo(string question, List<QuizOptionsInfo> optionsList) {
          this.Question = question;
          this.Options = optionsList;
        }
      }
      public class QuizOptionsInfo {
        public int OptionId { get; private set; }
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
        public int DisplayOrder { get; set; }
        public QuizOptionsInfo() { }
        public QuizOptionsInfo(SqlDataReader dr) {
          this.OptionId = dr.GetInt("OptionId");
          this.OptionText = dr.GetString("OptionText");
          this.IsCorrect = dr.GetBoolFromInt("IsCorrect", false);
          this.DisplayOrder = dr.GetInt("DisplayOrder");
        }
        public QuizOptionsInfo(string optionText, bool isCorrect, int displayOrder) {
          this.OptionText = optionText;
          this.IsCorrect = isCorrect;
          this.DisplayOrder = displayOrder;
        }
      }

      public class UserResponsesInfo {
        public int QuestionId { get; private set; }
        public int OptionId { get; private set; }
        public UserResponsesInfo() { }
        public UserResponsesInfo(SqlDataReader dr) {
          this.QuestionId = dr.GetInt("QuestionId");
          this.OptionId = dr.GetInt("OptionId");
        }
        public UserResponsesInfo(int questionId, int optionId) {
          this.QuestionId = questionId;
          this.OptionId = optionId;
        }
      }
    }
  }
}
