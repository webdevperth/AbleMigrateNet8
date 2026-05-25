using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AlbertCoaches {

      public enum SearchModeEnum { SearchPartners, SearchAllUsers }

      public static List<AlbertCoachInfo> GetPendingInvitesByUser(AbleUser.UserIdentity forUser) {

        if (forUser == null) return null;

        return GetCoachInfoList(
          null,
          $@" u.InvitedByUserId = @InvitedByUserId
          AND u.UserId <> @UserId_Unassigned
          AND u.RegisteredUtc IS NULL",
          true,
          Common.NewSqlParameter("InvitedByUserId", forUser.UserId),
          Common.NewSqlParameter("UserId_Unassigned", ConfigHelper.UserId.Unassigned)
        );
      }

      public static List<AlbertCoachInfo> GetPendingInvitesByOthersInOrg(AbleUser.UserIdentity forUser) {

        if (forUser == null) return null;

        return GetCoachInfoList(
          null,
          $@" u.OrgId = @UserOrgId
          AND u.InvitedByUserId <> @InvitedByUserId
          AND u.UserId <> @UserId_Unassigned
          AND u.RegisteredUtc IS NULL",
          true,
          Common.NewSqlParameter("InvitedByUserId", forUser.UserId),
          Common.NewSqlParameter("UserOrgId", forUser.OrgId),
          Common.NewSqlParameter("UserId_Unassigned", ConfigHelper.UserId.Unassigned)
        );
      }

      // Get coach by id.
      public static AlbertCoachInfo GetCoachInfo(int coachUserId, bool onlyPartners = true) {
        return GetCoachInfo(null, coachUserId, onlyPartners);
      }

      public static AlbertCoachInfo GetCoachInfo(SqlTransaction trans, int coachUserId, bool onlyPartners = true) {
        return GetCoachInfo(trans,
          "u.UserId = @UserId",
          onlyPartners,
          Common.NewSqlParameter("UserId", coachUserId)
        );
      }

      public static AlbertCoachInfo GetCoachInfo(SqlTransaction trans, string emailAddress, bool onlyPartners = true) {
        return GetCoachInfo(trans,
          "u.Email = @EmailAddress",
          onlyPartners,
          Common.NewSqlParameter("EmailAddress", emailAddress)
        );
      }

      private static AlbertCoachInfo GetCoachInfo(SqlTransaction trans, string sqlWhereClause, bool onlyPartners = true, params SqlParameter[] sqlWhereParams) {

        if (sqlWhereClause.IsNullOrEmpty()) throw new ArgumentException("sqlWhereClause is required.");

        var coachInfo = GetCoachInfoList(trans, sqlWhereClause, onlyPartners, sqlWhereParams);

        if (coachInfo.Count == 0 || coachInfo == null) return null;

        return coachInfo.FirstOrDefault();
      }

      public static UserListViewInfoPaged GetCoachInfoList_BySearch(
        string searchTerm,
        bool statusInactive,
        bool CanViewHiddenPartners,
        bool includePartnerInSelection,
        int userId,
        int? orgId = null,
        bool includeUnassigned = false,
        AbleUser.RegisteredFilter includeUnregistered = AbleUser.RegisteredFilter.Any,
        DbHelper.AlbertCoaches.SearchModeEnum searchMode = SearchModeEnum.SearchPartners,
        int? offsetRows = null,
        int? fetchRows = null,
        List<int> partnerTagIds = null) {

        string sqlWhereClause = GetBasicWhereClause_CoachInfoList(CanViewHiddenPartners, includeUnassigned, includeUnregistered);

        // If there's a search term
        if (!searchTerm.IsNullOrEmpty()) {
          sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + $@"
            ( u.FirstName LIKE '%' + @SearchTerm + '%'
              OR u.LastName LIKE '%' + @SearchTerm + '%'
              OR u.Email LIKE '%' + @SearchTerm + '%'
              OR o.OrgName LIKE '%' + @SearchTerm + '%'
              OR u.AbleBioShort LIKE '%' + @SearchTerm + '%'
              OR tag.TagNames LIKE '%' + @SearchTerm + '%'
              OR pb.Personal_Background LIKE '%' + @SearchTerm + '%'
              OR pb.Personal_MyWhy LIKE '%' + @SearchTerm + '%'
              OR pb.Personal_HowIWork LIKE '%' + @SearchTerm + '%'
              OR pb.Personal_WhatIDo LIKE '%' + @SearchTerm + '%'
              OR pb.Personal_WhatILove LIKE '%' + @SearchTerm + '%'
              OR pb.Professional_Introduction LIKE '%' + @SearchTerm + '%'
              OR pb.Professional_Background LIKE '%' + @SearchTerm + '%'
              OR pb.Professional_Strengths LIKE '%' + @SearchTerm + '%'
              OR pb.Professional_RecentWork LIKE '%' + @SearchTerm + '%'
              OR pb.Professional_Impact LIKE '%' + @SearchTerm + '%'
              OR pb.Professional_Credentials LIKE '%' + @SearchTerm + '%'
            )";
        }

        bool onlyPartners = searchMode == SearchModeEnum.SearchPartners;
        if (onlyPartners) {
          // Partner status.
          if (statusInactive) {
            sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + $"u.IsPartnerActive = 0";
          } else {
            sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + $"u.IsPartnerActive = 1";
          }
        }

        // If there's an org ID
        if (orgId != null) {
          sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + " u.OrgId = @OrgId";
        }

        if (!partnerTagIds.IsNullOrEmpty()) {
          // Get Partners who have all the selected tags
          sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + $@"
            (
              SELECT COUNT(DISTINCT ptu.PartnerTagId)
              FROM al_PartnerTagToUser ptu
              WHERE ptu.UserId = u.UserId
                AND ptu.PartnerTagId IN ({string.Join(",", partnerTagIds)})
            ) = {partnerTagIds.Count}";
        }

        string orderByClause = "ud.FirstName, ud.LastName";

        if (includePartnerInSelection) {
          orderByClause = orderByClause.EnsureStartsWith("CASE WHEN ud.UserId = @UserId THEN 0 ELSE 1 END,");
        }

        return GetCoachInfoListView(
          null,
          sqlWhereClause,
          orderByClause,
          onlyPartners,
          offsetRows,
          fetchRows,
          Common.NewSqlParameter("SearchTerm", searchTerm),
          Common.NewSqlParameter("OrgId", orgId),
          Common.NewSqlParameter("CoachUserId_Unassigned", ConfigHelper.UserId.Unassigned),
          Common.NewSqlParameter("@UserId", userId)
        );
      }

      public static List<AlbertCoachInfo> GetCoachesInOrg(bool CanViewHiddenPartners, bool CanViewInactivePartners, int orgId, bool includeUnassigned = false, AbleUser.RegisteredFilter includeUnregistered = AbleUser.RegisteredFilter.Any) {

        string sqlWhereClause = GetBasicWhereClause_CoachInfoList(CanViewHiddenPartners, includeUnassigned, includeUnregistered, CanViewInactivePartners);

        sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + " u.OrgId = @OrgId";

        return GetCoachInfoList(
          null,
          sqlWhereClause,
          true,
          Common.NewSqlParameter("OrgId", orgId),
          Common.NewSqlParameter("CoachUserId_Unassigned", ConfigHelper.UserId.Unassigned)
        );
      }

      public static List<AlbertCoachInfo> GetCoachInfoList(bool includeUnassigned = false, AbleUser.RegisteredFilter includeUnregistered = AbleUser.RegisteredFilter.Any) {
        return GetCoachInfoList(
          null,
          GetBasicWhereClause_CoachInfoList(true, includeUnassigned, includeUnregistered),
          true,
          Common.NewSqlParameter("CoachUserId_Unassigned", ConfigHelper.UserId.Unassigned)
        );
      }

      public static List<AlbertCoachInfo> GetCoachInfoList_SelfAssignment() {

        string sqlWhereClause = GetBasicWhereClause_CoachInfoList(true, false, AbleUser.RegisteredFilter.OnlyRegistered, false);

        // Get Partners who have requires tags for self selection
        sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + $@"
          EXISTS (
            SELECT 1
            FROM al_PartnerTagToUser ptu
            WHERE ptu.UserId = u.UserId
              AND ptu.PartnerTagId IN ({string.Join(",", ConfigHelper.PartnerTagId.RequiredTagsForCoachSelfSelectionByParticipant)})
          )";


        return GetCoachInfoList(
          null,
          sqlWhereClause,
          true,
          Common.NewSqlParameter("CoachUserId_Unassigned", ConfigHelper.UserId.Unassigned)
        );
      }

      private static string GetBasicWhereClause_CoachInfoList(bool CanViewHiddenPartners, bool includeUnassigned = false, AbleUser.RegisteredFilter includeUnregistered = AbleUser.RegisteredFilter.Any, bool CanViewInactivePartners = true) {

        string sqlWhereClause = "";

        // If user cannot see hidden partners
        if (!CanViewHiddenPartners) {

          sqlWhereClause = "u.ProfileHiddenUtc IS NULL";
        }

        if (!includeUnassigned) {
          sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + "u.UserId <> @CoachUserId_Unassigned";
        }

        if (includeUnregistered == AbleUser.RegisteredFilter.OnlyRegistered) {

          sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + "u.RegisteredUtc IS NOT NULL";

        } else if (includeUnregistered == AbleUser.RegisteredFilter.OnlyUnregistered) {

          sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + "u.RegisteredUtc IS NULL";
        }

        if (!CanViewInactivePartners) {
          sqlWhereClause = sqlWhereClause.EnsureEndsWith(" AND ", StringExt.Ensure.IfNotBlank) + $"u.IsPartnerActive = 1";
        }

        return sqlWhereClause;
      }

      private static List<AlbertCoachInfo> GetCoachInfoList(SqlTransaction trans, string whereClause, bool onlyPartners = true, params SqlParameter[] sqlWhereParams) {

        var coachInfo = new List<AlbertCoachInfo>();

        string wherePartners = onlyPartners ? "u.IsAlbertCoach = 1" : "";

        Common.Query(trans, $@"

          SELECT
            {AbleUser.GetUserBasicColumsSQL},
            u.CalendlyUrlName, u.AbleBioShort, u.AbleWebProfileUrl, u.DateOfBirth,
            u.RoleTitle, u.City, u.Country, u.OrgRoleId,
            u.UserContractTypeId, u.XeroContactId,
            ui.FirstName as InvitedByFirstName, ui.LastName as InvitedByLastName,
            pb.PartnerBioId, pb.LastUpdatedUtc, pb.Personal_Background, pb.Personal_MyWhy, pb.Personal_HowIWork,
            pb.Personal_WhatIDo, pb.Personal_WhatILove, pb.Professional_Introduction, pb.Professional_Background, pb.CoachCardBio,
            pb.Professional_Strengths, pb.Professional_RecentWork, pb.Professional_Impact, pb.Professional_Credentials,
            ac.Coachees,
            tag.TagIdStr, tag.TagNames,
            uct.IncludeInPayRuns,
            ISNULL(tcs.TotalCoachingSessionsCompleted, 0) AS TotalCoachingSessionsCompleted
          FROM sv_User u
          INNER JOIN sv_Organisation o ON o.OrgId = u.OrgId
          LEFT OUTER JOIN sv_User ui ON ui.UserId = u.InvitedByUserId
          LEFT OUTER JOIN al_PartnerBio pb ON pb.PartnerUserId = u.UserId
          LEFT OUTER JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = u.ClientCompanyId
          LEFT OUTER JOIN al_UserContractType uct ON uct.UserContractTypeId = u.UserContractTypeId
          CROSS APPLY (SELECT COUNT(*) AS Coachees FROM al_Coachees ac WHERE ac.CoachUserId = u.UserId AND ac.DeletedUtc IS NULL) AS ac
          CROSS APPLY (SELECT COUNT(*) AS TotalCoachingSessionsCompleted FROM al_Component co WHERE co.CoachingSessionId IS NOT NULL AND co.PartnerUserId = u.UserId AND co.CompletedDateUtc <= GETUTCDATE()) AS tcs
          CROSS APPLY (
            SELECT STRING_AGG(tagu.PartnerTagId, ',') AS TagIdStr, STRING_AGG(pt.TagName, ',') as TagNames
            FROM al_PartnerTagToUser tagu
            LEFT OUTER JOIN al_PartnerTag pt ON pt.PartnerTagId = tagu.PartnerTagId
            WHERE tagu.UserId = u.UserId
          ) AS tag
          {Subscriptions.User.GetUserSubscriptionOuterApplySQL("u")}
          {AbleUser.GetLatestCoacheeOuterApplySQL("u", AbleUser.LatestCoacheeColPrefix, AbleUser.LatestCoacheeJoinAlias)}
          {AbleUser.GetLatestCoacheeOuterApplySQL("u", AbleUser.LatestCoachingColPrefix, AbleUser.LatestCoachingJoinAlias, mustHaveCoaching: true)}
          WHERE {whereClause} {wherePartners.EnsureStartsWith(" AND ", true)}
          ORDER BY u.IsPartnerActive desc, u.FirstName, u.LastName",

          dr => {
            coachInfo.Add(new AlbertCoachInfo(dr));
          },

          sqlWhereParams
        );
        return coachInfo;
      }

      private static UserListViewInfoPaged GetCoachInfoListView(SqlTransaction trans, string whereClause, string orderByClause, bool onlyPartners = true,

        int? offsetRows = null, int? fetchRows = null, params SqlParameter[] sqlWhereParams) {

        var coachInfo = new UserListViewInfoPaged();

        string wherePartners = onlyPartners ? "AND u.IsAlbertCoach = 1" : "";

        string sql = $@"
          WITH UserTags AS (
            SELECT
              tagu.UserId,
              STRING_AGG(tagu.PartnerTagId, ',') AS TagIdStr,
              STRING_AGG(pt.TagName, ',') AS TagNames
            FROM al_PartnerTagToUser tagu
            LEFT JOIN al_PartnerTag pt ON pt.PartnerTagId = tagu.PartnerTagId
            GROUP BY tagu.UserId
          ),
          UserDetails AS (
            SELECT
              {AbleUser.GetUserIdentityColumsSQL("u", "o")},
              o.OrgName,
              tag.TagIdStr, tag.TagNames
            FROM sv_User u
            LEFT JOIN al_PartnerBio pb ON pb.PartnerUserId = u.UserId
            INNER JOIN sv_Organisation o ON o.OrgId = u.OrgId
            LEFT JOIN UserTags tag ON tag.UserId = u.UserId
            WHERE u.IsAbleUser = 1 {whereClause.EnsureStartsWith(" AND ", true)} {wherePartners}
          ),
          TotalRows AS (
            SELECT COUNT(*) AS TotalRows
            FROM UserDetails
          )

          SELECT
            tot.TotalRows,
            {AbleUser.GetUserIdentityColumsSQL("ud", "ud")},
            ud.OrgName,
            ud.TagIdStr, ud.TagNames
          FROM UserDetails ud
          CROSS JOIN TotalRows tot
          {orderByClause.EnsureStartsWith("ORDER BY ", true)}";

        if (offsetRows >= 0 && fetchRows > 0) {
          coachInfo.OffsetRows = offsetRows;
          coachInfo.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        if (ConfigHelper.IsDevServer) coachInfo.SqlText = sql;

        Common.Query(trans, sql,

            dr => {
              if (coachInfo.TotalRows == 0) coachInfo.TotalRows = dr.GetInt("TotalRows");
              var coachInfoListView = new UserListInfo(dr);
              if (ConfigHelper.IsDevServer) coachInfo.SqlText = sql;
              coachInfo.UserListInfo.Add(coachInfoListView);
            },

            sqlWhereParams
        );
        return coachInfo;
      }

      public class CoachUserRolesInfo {
        public bool IsPractitioner { get; set; }
        public bool IsClient { get; set; }
        public bool IsParticipant { get; set; }
        public CoachUserRolesInfo() { }
        public CoachUserRolesInfo(bool isPractitioner, bool isClient, bool isParticipant) {
          this.IsPractitioner = isPractitioner;
          this.IsClient = isClient;
          this.IsParticipant = isParticipant;
        }
      }

      public static void CreateCoach(int orgId, AlbertCoachInfo coachInfo, CoachUserRolesInfo coachUserRoles) {

        try {
          using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
            string sql = @"
              INSERT INTO sv_User (OrgId, FirstName, LastName, Email, Mobile,
                TimeZoneIdIANA, TimeZoneIdWindows, CalendlyUrlName, AbleWebProfileUrl, IsAlbertCoach, IsClient, IsParticipant, XeroContactId, DateOfBirth, City, Country, OrgRoleId, RoleTitle)
              OUTPUT INSERTED.UserId
              VALUES (@OrgId, @FirstName, @LastName, @Email, @Mobile,
                @TimeZoneIdIANA, @TimeZoneIdWindows, @CalendlyUrlName, @AbleWebProfileUrl, @IsAlbertCoach, @IsClient, @IsParticipant, @XeroContactId, @DateOfBirth, @City, @Country, @OrgRoleId, @RoleTitle)";
            using (var cmd = new SqlCommand(sql, conn)) {
              cmd.Parameters.AddInt("@OrgId", orgId);
              cmd.Parameters.AddTinyIntFromBool("@IsAlbertCoach", coachUserRoles.IsPractitioner);
              cmd.Parameters.AddTinyIntFromBool("@IsClient", coachUserRoles.IsClient);
              cmd.Parameters.AddTinyIntFromBool("@IsParticipant", coachUserRoles.IsParticipant);
              cmd.Parameters.AddVarChar("@FirstName", 100, coachInfo.FirstName);
              cmd.Parameters.AddVarChar("@LastName", 100, coachInfo.LastName);
              cmd.Parameters.AddVarChar("@Email", 100, coachInfo.EmailAddress);
              cmd.Parameters.AddVarChar("@Mobile", 100, coachInfo.MobileNumber);
              cmd.Parameters.AddVarChar("@TimeZoneIdIANA", 50, coachInfo.TimeZoneIdIana);
              cmd.Parameters.AddVarChar("@TimeZoneIdWindows", 100, coachInfo.TimeZoneIdWindows);
              cmd.Parameters.AddVarChar("@CalendlyUrlName", 100, coachInfo.CalendlyUrlName);
              cmd.Parameters.AddVarChar("@AbleWebProfileUrl", 100, coachInfo.WebProfileUrl);
              cmd.Parameters.AddInt("@XeroContactId", coachInfo.XeroContactId);
              cmd.Parameters.AddDateTime("@DateOfBirth", coachInfo.DateOfBirth);
              cmd.Parameters.AddVarChar("@City", 100, coachInfo.City);
              cmd.Parameters.AddVarChar("@Country", 100, coachInfo.Country);
              cmd.Parameters.AddInt("@OrgRoleId", coachInfo.OrgRoleId);
              cmd.Parameters.AddVarChar("@RoleTitle", 100, coachInfo.RoleTitle);
              conn.Open();
              using (var dr = cmd.ExecuteReader()) {
                dr.Read();
                coachInfo.UserId = dr.GetInt("UserId");
              }
            }
          }

          // If user created, update IsPartnerActive flag.
          UpdateIsPartnerActive(null, coachInfo);

        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(CreateCoach))
            .WithProperty(DalApplicationInsightsConstants.OrgId, orgId)
            .WithProperty(DalApplicationInsightsConstants.FirstName, coachInfo?.FirstName)
            .WithProperty(DalApplicationInsightsConstants.LastName, coachInfo?.LastName)
            .WithProperty(DalApplicationInsightsConstants.Email, coachInfo?.EmailAddress)
            .WithProperty(DalApplicationInsightsConstants.IsPractitioner, coachUserRoles?.IsPractitioner)
            .WithProperty(DalApplicationInsightsConstants.IsClient, coachUserRoles?.IsClient)
            .WithProperty(DalApplicationInsightsConstants.IsParticipant, coachUserRoles?.IsParticipant)
            .Track();
          throw;
        }
      }

      public static void UpdateSettingsEngage(AlbertCoachInfo coachInfo) {

        try {
          using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
            string sql = @"
              UPDATE al_Coachees SET
                DisableNudges = @DisableNudges,
                PulseSurveyEnabled = @PulseSurveyEnabled
              WHERE
                CoacheeId = @CoacheeId";
            using (var cmd = new SqlCommand(sql, conn)) {
              cmd.Parameters.AddInt("@CoacheeId", coachInfo.LatestCoacheeInfo.CoacheeId);
              cmd.Parameters.AddTinyIntFromBool("@DisableNudges", coachInfo.LatestCoacheeInfo.DisableNudges);
              cmd.Parameters.AddTinyIntFromBool("@PulseSurveyEnabled", coachInfo.LatestCoacheeInfo.PulseSurveyEnabled);
              conn.Open();
              cmd.ExecuteNonQuery();
            }
          }
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(UpdateSettingsEngage))
            .WithProperty(DalApplicationInsightsConstants.CoacheeId, coachInfo?.LatestCoacheeInfo?.CoacheeId)
            .WithProperty(DalApplicationInsightsConstants.DisableNudges, coachInfo?.LatestCoacheeInfo?.DisableNudges)
            .WithProperty(DalApplicationInsightsConstants.PulseSurveyEnabled, coachInfo?.LatestCoacheeInfo?.PulseSurveyEnabled)
            .Track();
          throw;
        }
      }

      public static bool UpdateCoach(SqlTransaction trans, AlbertCoachInfo coachInfo, CoachUserRolesInfo coachUserRoles) {

        if (trans == null) throw new ArgumentNullException(nameof(trans), "Transaction required.");

        bool userUpdated = Common.GetNonQueryInt(trans, @"
          UPDATE sv_User SET
            LoginName = @Email,
            FirstName = @FirstName,
            LastName = @LastName,
            RoleTitle = @RoleTitle,
            Email = @Email,
            Mobile = @Mobile,
            TimeZoneIdIANA = @TimeZoneIdIANA,
            TimeZoneIdWindows = @TimeZoneIdWindows,
            CalendlyUrlName = @CalendlyUrlName,
            AbleWebProfileUrl = @AbleWebProfileUrl,
            XeroContactId = @XeroContactId,
            IsAlbertCoach = @IsAlbertCoach,
            IsClient = @IsClient,
            IsParticipant = @IsParticipant,
            DateOfBirth = @DateOfBirth,
            City = @City,
            Country = @Country,
            OrgRoleId = @OrgRoleId,
            ProfileHiddenUtc = @ProfileHiddenUtc
          WHERE
            UserId = @UserId",
          Common.NewSqlParameter("UserId", coachInfo.UserId),
          Common.NewSqlParameter("FirstName", coachInfo.FirstName),
          Common.NewSqlParameter("LastName", coachInfo.LastName),
          Common.NewSqlParameter("RoleTitle", coachInfo.RoleTitle),
          Common.NewSqlParameter("Email", coachInfo.EmailAddress),
          Common.NewSqlParameter("Mobile", coachInfo.MobileNumber),
          Common.NewSqlParameter("TimeZoneIdIANA", coachInfo.TimeZoneIdIana),
          Common.NewSqlParameter("TimeZoneIdWindows", coachInfo.TimeZoneIdWindows),
          Common.NewSqlParameter("CalendlyUrlName", coachInfo.CalendlyUrlName),
          Common.NewSqlParameter("AbleWebProfileUrl", coachInfo.WebProfileUrl),
          Common.NewSqlParameter("XeroContactId", coachInfo.XeroContactId),
          Common.NewSqlParameter("IsAlbertCoach", coachUserRoles.IsPractitioner),
          Common.NewSqlParameter("IsClient", coachUserRoles.IsClient),
          Common.NewSqlParameter("IsParticipant", coachUserRoles.IsParticipant),
          Common.NewSqlParameter("DateOfBirth", coachInfo.DateOfBirth),
          Common.NewSqlParameter("City", coachInfo.City),
          Common.NewSqlParameter("Country", coachInfo.Country),
          Common.NewSqlParameter("OrgRoleId", coachInfo.OrgRoleId),
          Common.NewSqlParameter("ProfileHiddenUtc", coachInfo.ProfileHiddenUtc)
        ) == 1;

        if (!userUpdated) return false;

        // Update IsPartnerActive flag.
        return (UpdateIsPartnerActive(trans, coachInfo));
      }

      public static void CreatePartnerBio(AlbertCoachInfo coachInfo) {

        coachInfo.PartnerBioId = Common.GetScalarQueryInt(@"
          INSERT INTO al_PartnerBio (
            PartnerUserId, LastUpdatedUtc, Personal_Background, Personal_MyWhy, Personal_HowIWork, Personal_WhatIDo,
            Personal_WhatILove, Professional_Introduction, Professional_Background, Professional_Strengths, Professional_RecentWork,
            Professional_Impact, Professional_Credentials, CoachCardBio
          )
          OUTPUT INSERTED.PartnerBioId
          VALUES (
            @PartnerUserId, @LastUpdatedUtc, @Personal_Background, @Personal_MyWhy, @Personal_HowIWork, @Personal_WhatIDo,
            @Personal_WhatILove, @Professional_Introduction, @Professional_Background, @Professional_Strengths, @Professional_RecentWork,
            @Professional_Impact, @Professional_Credentials, @CoachCardBio
          )",
          Common.NewSqlParameter("PartnerUserId", coachInfo.UserId),
          Common.NewSqlParameter("PartnerBioId", coachInfo.PartnerBioId),
          Common.NewSqlParameter("LastUpdatedUtc", DateTime.UtcNow),
          Common.NewSqlParameter("Personal_Background", coachInfo.PartnerBio_Personal_Background),
          Common.NewSqlParameter("Personal_MyWhy", coachInfo.PartnerBio_Personal_MyWhy),
          Common.NewSqlParameter("Personal_HowIWork", coachInfo.PartnerBio_Personal_HowIWork),
          Common.NewSqlParameter("Personal_WhatIDo", coachInfo.PartnerBio_Personal_WhatIDo),
          Common.NewSqlParameter("Personal_WhatILove", coachInfo.PartnerBio_Personal_WhatILove),
          Common.NewSqlParameter("Professional_Introduction", coachInfo.PartnerBio_Professional_Introduction),
          Common.NewSqlParameter("Professional_Background", coachInfo.PartnerBio_Professional_Background),
          Common.NewSqlParameter("Professional_Strengths", coachInfo.PartnerBio_Professional_Strengths),
          Common.NewSqlParameter("Professional_RecentWork", coachInfo.PartnerBio_Professional_RecentWork),
          Common.NewSqlParameter("Professional_Impact", coachInfo.PartnerBio_Professional_Impact),
          Common.NewSqlParameter("Professional_Credentials", coachInfo.PartnerBio_Professional_Credentials),
          Common.NewSqlParameter("CoachCardBio", coachInfo.PartnerBio_CoachCardBio)
        );

        UpdatePartnerShortBio(null, coachInfo.UserId, coachInfo.BioShort);
      }

      public static void UpdatePartnerBio(AlbertCoachInfo coachInfo) {

        try {
          using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
            string sql = @"
              UPDATE al_PartnerBio SET
                LastUpdatedUtc = @LastUpdatedUtc,
                Personal_Background = @Personal_Background,
                Personal_MyWhy = @Personal_MyWhy,
                Personal_HowIWork = @Personal_HowIWork,
                Personal_WhatIDo = @Personal_WhatIDo,
                Personal_WhatILove = @Personal_WhatILove,
                Professional_Introduction = @Professional_Introduction,
                Professional_Background = @Professional_Background,
                Professional_Strengths = @Professional_Strengths,
                Professional_RecentWork = @Professional_RecentWork,
                Professional_Impact = @Professional_Impact,
                Professional_Credentials = @Professional_Credentials,
                CoachCardBio = @CoachCardBio
              WHERE
                PartnerBioId = @PartnerBioId";
            using (var cmd = new SqlCommand(sql, conn)) {
              cmd.Parameters.AddInt("@PartnerBioId", coachInfo.PartnerBioId);
              cmd.Parameters.AddDateTime("@LastUpdatedUtc", DateTime.UtcNow);
              cmd.Parameters.AddVarCharMax("@Personal_Background", coachInfo.PartnerBio_Personal_Background);
              cmd.Parameters.AddVarCharMax("@Personal_MyWhy", coachInfo.PartnerBio_Personal_MyWhy);
              cmd.Parameters.AddVarCharMax("@Personal_HowIWork", coachInfo.PartnerBio_Personal_HowIWork);
              cmd.Parameters.AddVarCharMax("@Personal_WhatIDo", coachInfo.PartnerBio_Personal_WhatIDo);
              cmd.Parameters.AddVarCharMax("@Personal_WhatILove", coachInfo.PartnerBio_Personal_WhatILove);
              cmd.Parameters.AddVarCharMax("@Professional_Introduction", coachInfo.PartnerBio_Professional_Introduction);
              cmd.Parameters.AddVarCharMax("@Professional_Background", coachInfo.PartnerBio_Professional_Background);
              cmd.Parameters.AddVarCharMax("@Professional_Strengths", coachInfo.PartnerBio_Professional_Strengths);
              cmd.Parameters.AddVarCharMax("@Professional_RecentWork", coachInfo.PartnerBio_Professional_RecentWork);
              cmd.Parameters.AddVarCharMax("@Professional_Impact", coachInfo.PartnerBio_Professional_Impact);
              cmd.Parameters.AddVarCharMax("@Professional_Credentials", coachInfo.PartnerBio_Professional_Credentials);
              cmd.Parameters.AddVarCharMax("@CoachCardBio", coachInfo.PartnerBio_CoachCardBio);
              conn.Open();
              cmd.ExecuteNonQuery();
              UpdatePartnerShortBio(cmd.Transaction, coachInfo.UserId, coachInfo.BioShort);
            }
          }
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(UpdatePartnerBio))
            .WithProperty(DalApplicationInsightsConstants.PartnerBioId, coachInfo?.PartnerBioId)
            .Track();
          throw;
        }
      }

      public static void UpdatePartnerShortBio(SqlTransaction trans, int partnerId, string ableBioShort) {
        Common.GetNonQueryInt(trans, @"
          UPDATE sv_User SET
            AbleBioShort = @AbleBioShort
          WHERE
            UserId = @UserId",
          Common.NewSqlParameter("UserId", partnerId),
          Common.NewSqlParameter("AbleBioShort", ableBioShort, true));
      }

      // Update IsPartnerActive based on the current state of the user profile, tags, etc.
      // This must be called when a user is updated or their contract is changed.
      public static bool UpdateIsPartnerActive(SqlTransaction trans, AlbertCoachInfo coachInfo) {

        if (coachInfo == null) return false;

        bool isPartnerActive;

        // Not explicitly setting a value so use these rules.
        if (coachInfo.OrgId == ConfigHelper.IntegralTenantOrgId) {
          // Integral has these rules.
          isPartnerActive =
            coachInfo.IsProfileComplete
            && coachInfo.HasContract
            && coachInfo.TagCount > 0
            && (coachInfo.HasOrientationTag || coachInfo.HasTechnicalOnboardingTag);
        } else {
          // Other tenants always active.
          isPartnerActive = true;
        }

        // Only update db if needed.
        if (isPartnerActive == coachInfo.IsPartnerActive) return true;

        bool userUpdated = Common.GetNonQueryInt(trans, @"
          UPDATE sv_User
          SET IsPartnerActive = @IsPartnerActive
          WHERE UserId = @UserId",
          Common.NewSqlParameter("UserId", coachInfo.UserId),
          Common.NewSqlParameter("IsPartnerActive", isPartnerActive)
        ) == 1;

        if (userUpdated) {
          // Ensure user object is aligned with the db update.
          coachInfo.IsPartnerActive = isPartnerActive;
        }

        return userUpdated;
      }

      // Update IsPartnerActive based on the current state of the user profile, tags, etc.
      // This must be called when a user is updated or their contract is changed.
      public static bool UpdateContractTypeId(SqlTransaction trans, AlbertCoachInfo coachInfo, int userContractTypeId) {

        if (trans == null) throw new ArgumentNullException(nameof(trans), "Transaction required.");

        bool userUpdated = Common.GetNonQueryInt(trans, @"
          UPDATE sv_User
          SET UserContractTypeId = @UserContractTypeId
          WHERE UserId = @UserId",
          Common.NewSqlParameter("UserId", coachInfo.UserId),
          Common.NewSqlParameter("UserContractTypeId", userContractTypeId)
        ) == 1;

        if (!userUpdated) return false;

        // Update user's IsPartnerActive flag.
        if (!UpdateIsPartnerActive(trans, coachInfo)) return false;

        // Ensure user object is aligned with the db update.
        coachInfo.UserContractTypeId = userContractTypeId;

        return true;
      }

      public static void UpdateHidePartnerProfile(SqlTransaction trans, int partnerId, bool hideProfile) {
        DateTime? profileHiddenUtc = null;
        if (hideProfile) profileHiddenUtc = DateTime.UtcNow;

        Common.GetNonQueryInt(trans, @"
          UPDATE sv_User SET
            ProfileHiddenUtc = @ProfileHiddenUtc
          WHERE
            UserId = @UserId",
          Common.NewSqlParameter("UserId", partnerId),
          Common.NewSqlParameter("ProfileHiddenUtc", profileHiddenUtc));
      }

      public static PartnerMetricsInfo GetPartnerMetricsInfo(int partnerUserId) {
        var results = new PartnerMetricsInfo();

        // Get accurate dates for calculation of UpcomingPayrunDetails subquery.
        PayRuns.GetUpcomingPayRunPeriodDates(1,
          out DateTime previousPeriodEndUtc, out DateTime thisPeriodEndUtc,
          out DateTime dataPeriodStartUtc, out DateTime dataPeriodEndUtc);

        Common.Query($@"
          WITH LatestPayRunDate AS (
            SELECT
              MAX(pr.AdjustmentsEndDateUtc) AS LatestAdjustmentsEndDateUtc,
              MAX(pr.ProcessedDateUtc) AS LatestProcessedDateUtc
            FROM id_PayRunItems pri
            INNER JOIN id_PayRun pr ON pri.PayRunId = pr.PayRunId
            WHERE pri.ForUserId = @UserId
          ),

          UserEarnings AS (
            SELECT
              SUM(
                CASE
                  WHEN pr.AdjustmentsEndDateUtc >=
                    CASE -- Calculate from most recent July 1st (Finacial year)
                      WHEN GETDATE() >= DATEADD(YEAR, DATEDIFF(YEAR, 0, GETDATE()), '1900-07-01') THEN DATEADD(YEAR, DATEDIFF(YEAR, 0, GETDATE()), '1900-07-01') -- Calculate from this years 1st of July
                      ELSE DATEADD(YEAR, DATEDIFF(YEAR, 0, GETDATE()) - 1, '1900-07-01') -- Calculate from last years 1st of July
                    END
                  THEN pri.PartnerRevenue
                ELSE 0
              END) AS YearTotalEarnings,
              SUM(pri.PartnerRevenue) AS LifetimeTotalEarnings
            FROM id_PayRunItems pri
            LEFT JOIN id_PayRun pr ON pr.PayRunId = pri.PayRunId
            WHERE pri.ForUserId = @UserId
          ),

          WorkshopAndSessionDelivery AS (
            SELECT
              SUM(CASE WHEN (cmp.CompletedDateUtc IS NULL OR cmp.CompletedDateUtc > GETUTCDATE()) AND cmp.WorkshopEventId IS NOT NULL AND (we.WorkshopStatusId IN (@WorkshopStatusId_Confirmed, @WorkshopStatusId_Estimated)) THEN 1 ELSE 0 END) AS TotalWorkshopsToBeDelivered,
              SUM(CASE WHEN cmp.WorkshopEventId IS NOT NULL THEN 1 ELSE 0 END) AS TotalWorkshops,
              SUM(CASE WHEN (cmp.CompletedDateUtc IS NULL OR cmp.CompletedDateUtc > GETUTCDATE()) AND cmp.CoacheeId IS NOT NULL AND c.ProgramStatusId <> @StatusId_EndProgram THEN 1 ELSE 0 END) AS TotalSessionsToBeDelivered,
              SUM(CASE WHEN cmp.CoacheeId IS NOT NULL THEN 1 ELSE 0 END) AS TotalSessions
            FROM al_Component cmp
            LEFT JOIN al_Coachees c ON c.CoacheeId = cmp.CoacheeId
            LEFT OUTER JOIN ev_WorkshopEvent we ON we.WorkshopEventId = cmp.WorkshopEventId AND we.WorkshopStatusId <> @WorkshopStatusId_Cancelled
            WHERE cmp.QuoteItemId IS NOT NULL AND (cmp.PartnerUserId = @UserId OR we.KeyFacilitatorUserId = @UserId OR we.CoFacilitatorUserId = @UserId)
          ),

          UpcomingPayrunDetails AS (
            SELECT TOP 1
              SUM(CASE WHEN tcmp.PartnerUserId = @UserId AND j.Partner_DeliveryPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_DeliveryPercentage ELSE 0 END) AS TotalUpcomingPayrunAsPartner,
              SUM(CASE WHEN j.LeadConsultantUserId = @UserId AND j.Partner_PLCPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_PLCPercentage ELSE 0 END) AS TotalUpcomingPayrunAsPLC,
              SUM(CASE WHEN j.Partner_UserId = @UserId AND j.Partner_SalesDeliveryPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_SalesDeliveryPercentage ELSE 0 END) AS TotalUpcomingPayrunAsSales,
              SUM(
                CASE WHEN tcmp.PartnerUserId = @UserId AND j.Partner_DeliveryPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_DeliveryPercentage ELSE 0 END +
                CASE WHEN j.LeadConsultantUserId = @UserId AND j.Partner_PLCPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_PLCPercentage ELSE 0 END +
                CASE WHEN j.Partner_UserId = @UserId AND j.Partner_SalesDeliveryPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_SalesDeliveryPercentage ELSE 0 END
              ) AS TotalUpcomingPayrun
            FROM al_Component tcmp
            LEFT JOIN id_Job j ON j.JobId = tcmp.ProgramJobId
            LEFT JOIN al_Project prj ON prj.JobNumber = j.JobNumber
            LEFT JOIN ev_WorkshopEvent w ON w.WorkshopEventId = tcmp.WorkshopEventId
            LEFT JOIN sv_User u ON u.UserId = @UserId
            WHERE
            tcmp.CompletedDateUtc BETWEEN @PeriodStartUtc AND @PeriodEndUtc
            AND NOT EXISTS(SELECT NULL FROM id_PayRunItems pri WHERE pri.ComponentId = tcmp.ComponentId)
            AND tcmp.CompletedDateUtc IS NOT NULL
            AND tcmp.QuoteItemId IS NOT NULL
            AND prj.InvoiceInstructionTypeId NOT IN (@InvoiceInstructionTypeId_NoTransaction)
            AND u.UserContractTypeId IN (@UserContractTypeId_Partner_Casual, @UserContractTypeId_Partner_Contract, @UserContractTypeId_SalariedConsultant, @UserContractTypeId_SalariedOps)
            AND (w.WorkshopStatusId IN (@WorkshopStatusId_Confirmed, @WorkshopStatusId_Cancelled) OR tcmp.WorkshopEventId IS NULL)
            AND tcmp.PartnerUserId NOT IN (@UserId_Unassigned, @UserId_Revenue_PandL, @UserId_Revenue_Lost)
            AND (
              (tcmp.PartnerUserId = @UserId AND j.Partner_DeliveryPercentage > 0)
              OR (j.LeadConsultantUserId = @UserId AND j.Partner_PLCPercentage > 0)
              OR (j.Partner_UserId = @UserId AND j.Partner_SalesDeliveryPercentage > 0)
            )
          ),

          TotalRevenueToDeliver AS (
            SELECT
              SUM(CASE WHEN tcmp.PartnerUserId = @UserId AND j.Partner_DeliveryPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_DeliveryPercentage ELSE 0 END) AS TotalRevToDeliverAsPartner,
              SUM(CASE WHEN j.LeadConsultantUserId = @UserId AND j.Partner_PLCPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_PLCPercentage ELSE 0 END) AS TotalRevToDeliverAsPLC,
              SUM(CASE WHEN j.Partner_UserId = @UserId AND j.Partner_SalesDeliveryPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_SalesDeliveryPercentage ELSE 0 END) AS TotalRevToDeliverAsSales,
              SUM(
                CASE WHEN tcmp.PartnerUserId = @UserId AND j.Partner_DeliveryPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_DeliveryPercentage ELSE 0 END +
                CASE WHEN j.LeadConsultantUserId = @UserId AND j.Partner_PLCPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_PLCPercentage ELSE 0 END +
                CASE WHEN j.Partner_UserId = @UserId AND j.Partner_SalesDeliveryPercentage > 0 THEN tcmp.ComponentPrice * j.Partner_SalesDeliveryPercentage ELSE 0 END
              ) AS TotalRevenueToDeliver
            FROM al_Component tcmp
            LEFT JOIN id_Job j ON j.JobId = tcmp.ProgramJobId
            LEFT JOIN id_PayRunItems tpri ON tpri.ComponentId = tcmp.ComponentId
            LEFT JOIN al_Project prj ON prj.JobNumber = j.JobNumber
            LEFT JOIN ev_WorkshopEvent w ON w.WorkshopEventId = tcmp.WorkshopEventId
            LEFT JOIN sv_User u ON u.UserId = @UserId
            WHERE tpri.PayRunId IS NULL
            AND (tcmp.CompletedDateUtc IS NULL OR tcmp.CompletedDateUtc > GETUTCDATE()) AND tcmp.QuoteItemId IS NOT NULL
            AND prj.InvoiceInstructionTypeId NOT IN (@InvoiceInstructionTypeId_NoTransaction)
            AND u.UserContractTypeId IN (@UserContractTypeId_Partner_Casual, @UserContractTypeId_Partner_Contract, @UserContractTypeId_SalariedConsultant, @UserContractTypeId_SalariedOps)
            AND (w.WorkshopStatusId IN (@WorkshopStatusId_Confirmed, @WorkshopStatusId_Cancelled) OR tcmp.WorkshopEventId IS NULL)
            AND tcmp.PartnerUserId NOT IN (@UserId_Unassigned, @UserId_Revenue_PandL, @UserId_Revenue_Lost)
            AND (
              (tcmp.PartnerUserId = @UserId AND j.Partner_DeliveryPercentage > 0)
              OR (j.LeadConsultantUserId = @UserId AND j.Partner_PLCPercentage > 0)
              OR (j.Partner_UserId = @UserId AND j.Partner_SalesDeliveryPercentage > 0)
            )
          ),
          DealsWon AS (
            SELECT SUM(q.ClientAcceptedAmount) As TotalDealsWon
            FROM al_Quote q
            WHERE q.OwnerUserId = @UserId AND q.ClientAcceptedUtc IS NOT NULL AND q.DeletedUtc IS NULL
          )

          SELECT
            u.UserId,
            ue.YearTotalEarnings, ue.LifetimeTotalEarnings,
            dlv.TotalWorkshopsToBeDelivered, dlv.TotalWorkshops, dlv.TotalSessionsToBeDelivered, dlv.TotalSessions,
            uprv.TotalUpcomingPayrunAsPartner, uprv.TotalUpcomingPayrunAsPLC, uprv.TotalUpcomingPayrunAsSales, uprv.TotalUpcomingPayrun,
            tdrv.TotalRevToDeliverAsPartner, tdrv.TotalRevToDeliverAsPLC, tdrv.TotalRevToDeliverAsSales, tdrv.TotalRevenueToDeliver,
            dw.TotalDealsWon
          FROM sv_User u
          CROSS JOIN UpcomingPayrunDetails uprv
          CROSS JOIN WorkshopAndSessionDelivery dlv
          CROSS JOIN UserEarnings ue
          CROSS JOIN TotalRevenueToDeliver tdrv
          CROSS JOIN DealsWon dw
          WHERE u.UserId = @UserId",
          dr => {
            results = new PartnerMetricsInfo(dr);
          },
          Common.NewSqlParameter("UserId", partnerUserId),
          Common.NewSqlParameter("UserId_Unassigned", ConfigHelper.UserId.Unassigned),
          Common.NewSqlParameter("UserId_Revenue_PandL", ConfigHelper.UserId.RevenuePandL),
          Common.NewSqlParameter("UserId_Revenue_Lost", ConfigHelper.UserId.RevenueLost),
          Common.NewSqlParameter("WorkshopStatusId_Confirmed", WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId),
          Common.NewSqlParameter("WorkshopStatusId_Cancelled", WorkshopStatus.WorkshopStatus_Cancelled.WorkshopStatusId),
          Common.NewSqlParameter("WorkshopStatusId_Estimated", WorkshopStatus.WorkshopStatus_Estimated.WorkshopStatusId),
          Common.NewSqlParameter("UserContractTypeId_Partner_Casual", ConfigHelper.UserContractTypeId.PartnerCasual),
          Common.NewSqlParameter("UserContractTypeId_Partner_Contract", ConfigHelper.UserContractTypeId.PartnerContract),
          Common.NewSqlParameter("UserContractTypeId_SalariedConsultant", ConfigHelper.UserContractTypeId.SalariedConsultant),
          Common.NewSqlParameter("UserContractTypeId_SalariedOps", ConfigHelper.UserContractTypeId.SalariedOps),
          Common.NewSqlParameter("InvoiceInstructionTypeId_NoTransaction", ConfigHelper.InvoiceInstructionTypeId_NoTransaction),
          Common.NewSqlParameter("Upcoming_Start_DaysBeforeLatestAdjustment", ConfigHelper.PayRun.UpcomingPeriod1_StartDateLagDays),
          Common.NewSqlParameter("PayRunPeriodDays", ConfigHelper.PayRun.FixedPeriodDays),
          Common.NewSqlParameter("StatusId_EndProgram", CoacheeProgramStatus.GetStatus_EndProgram().ProgramStatusId),
          Common.NewSqlParameter("PeriodStartUtc", dataPeriodStartUtc),
          Common.NewSqlParameter("PeriodEndUtc", dataPeriodEndUtc)
        );
        return results;

      }

      public class PartnerMetricsInfo {
        public int UserId { get; protected set; }
        public decimal? YearTotalEarnings { get; protected set; }
        public decimal? LifetimeTotalEarnings { get; protected set; }
        public int TotalWorkshopsToBeDelivered { get; protected set; }
        public int TotalWorkshops { get; protected set; }
        public int TotalSessionsToBeDelivered { get; protected set; }
        public int TotalSessions { get; protected set; }
        public decimal? TotalUpcomingPayrunAsPartner { get; protected set; }
        public decimal? TotalUpcomingPayrunAsPLC { get; protected set; }
        public decimal? TotalUpcomingPayrunAsSales { get; protected set; }
        public decimal? TotalUpcomingPayrun { get; protected set; }
        public decimal? TotalRevToDeliverAsPartner { get; protected set; }
        public decimal? TotalRevToDeliverAsPLC { get; protected set; }
        public decimal? TotalRevToDeliverAsSales { get; protected set; }
        public decimal? TotalRevenueToDeliver { get; protected set; }
        public decimal? TotalDealsWon { get; protected set; }
        public PartnerMetricsInfo() { }
        public PartnerMetricsInfo(SqlDataReader dr) {
          UserId = dr.GetInt("UserId");
          YearTotalEarnings = dr.GetDecimalOrNull("YearTotalEarnings");
          LifetimeTotalEarnings = dr.GetDecimalOrNull("LifetimeTotalEarnings");
          TotalWorkshopsToBeDelivered = dr.GetIntOrDefault("TotalWorkshopsToBeDelivered", 0);
          TotalWorkshops = dr.GetIntOrDefault("TotalWorkshops", 0);
          TotalSessionsToBeDelivered = dr.GetIntOrDefault("TotalSessionsToBeDelivered", 0);
          TotalSessions = dr.GetIntOrDefault("TotalSessions", 0);
          TotalUpcomingPayrunAsPartner = dr.GetDecimalOrNull("TotalUpcomingPayrunAsPartner");
          TotalUpcomingPayrunAsPLC = dr.GetDecimalOrNull("TotalUpcomingPayrunAsPLC");
          TotalUpcomingPayrunAsSales = dr.GetDecimalOrNull("TotalUpcomingPayrunAsSales");
          TotalUpcomingPayrun = dr.GetDecimalOrNull("TotalUpcomingPayrun");
          TotalRevToDeliverAsPartner = dr.GetDecimalOrNull("TotalRevToDeliverAsPartner");
          TotalRevToDeliverAsPLC = dr.GetDecimalOrNull("TotalRevToDeliverAsPLC");
          TotalRevToDeliverAsSales = dr.GetDecimalOrNull("TotalRevToDeliverAsSales");
          TotalRevenueToDeliver = dr.GetDecimalOrNull("TotalRevenueToDeliver");
          TotalDealsWon = dr.GetDecimalOrNull("TotalDealsWon");
        }
      }

      public class UserListViewInfoPaged {

        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public string SqlText { get; internal set; }
        public List<UserListInfo> UserListInfo { get; internal set; }

        public UserListViewInfoPaged() {
          OffsetRows = null;
          FetchRows = null;
          TotalRows = 0;
          UserListInfo = new List<UserListInfo>();
        }
      }

      public class UserListInfo : AbleUser.UserIdentity {

        public string OrgName { get; set; }
        public string TagIdStr { get; private set; }
        public List<int> TagIdList { get; private set; } = new List<int>();

        public UserListInfo(SqlDataReader dr) {

          SetIdentityProps(dr);

          this.OrgName = dr.GetString("OrgName");
          this.TagIdStr = dr.GetString("TagIdStr");
          if (!this.TagIdStr.IsNullOrEmpty()) {
            this.TagIdList = this.TagIdStr.ToIntList();
          }

        }
      }

      public class AlbertCoachInfo : AbleUser.AbleUserBasicInfo {

        public int Coachees { get; set; }
        public string CalendlyUrlName { get; set; } // e.g. "lizzie-moyle"
        public string BioShort { get; set; }
        public string WebProfileUrl { get; set; }
        public int TagCount { get; private set; }
        public bool HasOrientationTag { get; set; }
        public bool HasTechnicalOnboardingTag { get; set; }
        public string TagIdStr { get; private set; }
        public List<int> TagIdList { get; private set; } = new List<int>();
        public string InvitedByFirstName { get; private set; }
        public string InvitedByLastName { get; private set; }
        public int? PartnerBioId { get; internal set; }
        public DateTime? PartnerBio_LastUpdatedUtc { get; internal set; }
        public string PartnerBio_Personal_Background { get; set; }
        public string PartnerBio_Personal_MyWhy { get; set; }
        public string PartnerBio_Personal_HowIWork { get; set; }
        public string PartnerBio_Personal_WhatIDo { get; set; }
        public string PartnerBio_Personal_WhatILove { get; set; }
        public string PartnerBio_Professional_Introduction { get; set; }
        public string PartnerBio_Professional_Background { get; set; }
        public string PartnerBio_Professional_Strengths { get; set; }
        public string PartnerBio_Professional_RecentWork { get; set; }
        public string PartnerBio_Professional_Impact { get; set; }
        public string PartnerBio_Professional_Credentials { get; set; }
        public string PartnerBio_CoachCardBio { get; set; }
        public int? XeroContactId { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Country { get; set; }
        public int? OrgRoleId { get; set; }
        public int TotalCoachingSessionsCompleted { get; private set; }

        public AlbertCoachInfo() {
          SetTimeZoneIdIana(ConfigHelper.DefaultTimeZoneIdIana);
        }

        public AlbertCoachInfo(SqlDataReader dr) {

          SetBasicFields(dr);

          this.Coachees = dr.GetInt("Coachees");
          this.CalendlyUrlName = dr.GetString("CalendlyUrlName");
          this.WebProfileUrl = dr.GetString("AbleWebProfileUrl");
          this.ProfileHiddenUtc = dr.GetDateTimeOrNull("ProfileHiddenUtc");
          this.InvitedByFirstName = dr.GetString("InvitedByFirstName");
          this.InvitedByLastName = dr.GetString("InvitedByLastName");

          this.TagIdStr = dr.GetString("TagIdStr");
          if (!this.TagIdStr.IsNullOrEmpty()) {
            this.TagIdList = this.TagIdStr.ToIntList();
          }

          // Partner Bio
          this.PartnerBioId = dr.GetIntOrNull("PartnerBioId");
          this.BioShort = dr.GetString("AbleBioShort");
          this.PartnerBio_LastUpdatedUtc = dr.GetDateTimeOrNull("LastUpdatedUtc");
          this.PartnerBio_Personal_Background = dr.GetString("Personal_Background");
          this.PartnerBio_Personal_MyWhy = dr.GetString("Personal_MyWhy");
          this.PartnerBio_Personal_HowIWork = dr.GetString("Personal_HowIWork");
          this.PartnerBio_Personal_WhatIDo = dr.GetString("Personal_WhatIDo");
          this.PartnerBio_Personal_WhatILove = dr.GetString("Personal_WhatILove");
          this.PartnerBio_Professional_Introduction = dr.GetString("Professional_Introduction");
          this.PartnerBio_Professional_Background = dr.GetString("Professional_Background");
          this.PartnerBio_Professional_Strengths = dr.GetString("Professional_Strengths");
          this.PartnerBio_Professional_RecentWork = dr.GetString("Professional_RecentWork");
          this.PartnerBio_Professional_Impact = dr.GetString("Professional_Impact");
          this.PartnerBio_Professional_Credentials = dr.GetString("Professional_Credentials");
          this.PartnerBio_CoachCardBio = dr.GetString("CoachCardBio");

          this.XeroContactId = dr.GetIntOrNull("XeroContactId");
          this.DateOfBirth = dr.GetDateTimeOrNull("DateOfBirth", true);
          this.Country = dr.GetString("Country", true);
          this.OrgRoleId = dr.GetIntOrNull("OrgRoleId", true);
          this.TotalCoachingSessionsCompleted = dr.GetInt("TotalCoachingSessionsCompleted");
        }

        public bool IsProfileComplete => !FirstName.IsNullOrEmptyOrWhitespace() && !LastName.IsNullOrEmptyOrWhitespace()
          && !EmailAddress.IsNullOrEmptyOrWhitespace() && !CalendlyUrlName.IsNullOrEmptyOrWhitespace()
          && !WebProfileUrl.IsNullOrEmptyOrWhitespace() && !BioShort.IsNullOrEmptyOrWhitespace();

        public string GetInvitedByFullName() => (InvitedByFirstName + " " + InvitedByLastName).Trim();
      }

      // Used to maintain a cache of CoachInfo to save db calls in loops.
      public class CoachInfoCache {

        List<AlbertCoachInfo> _coachInfoCache;

        public CoachInfoCache() {

          _coachInfoCache = new List<AlbertCoachInfo>();
        }

        // Return Coach in cache if found, or from db then add to cache, or null if not found.
        public AlbertCoachInfo GetCoachInfoOrNull(int coachUserId) {

          var coachInfo = _coachInfoCache.Find(c => c.UserId == coachUserId);

          if (coachInfo == null) {
            coachInfo = GetCoachInfo(coachUserId); // Get from db if not in cache.
            if (coachInfo != null) _coachInfoCache.Add(coachInfo); // If found, add to cache.
          }

          return coachInfo;
        }
      }

    }
  }
}
