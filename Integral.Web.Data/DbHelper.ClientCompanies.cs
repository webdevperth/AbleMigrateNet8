using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class ClientCompanies {

      private static string SQLWhereCompanyAccessibleToUser(AbleUser.AbleUserInfo user) {

        if (user == null || user.CurrentRole == ConfigHelper.UserRole.Unset) {
          return "0=1"; // No access.
        }

        if (user.CurrentRole == ConfigHelper.UserRole.Admin) {
          return string.Empty; // No restrictions.
        }

        string sql = "(";

        sql += $@"
          cmp.CreatedByUserId = @UserId
          OR cmp.SvCompanyId = @UserClientCompanyId
          {SQL_IN(SQL_ANDOR.OR, "cmp.SvCompanyId", user.PLCForCompanyIds)}
          {SQL_IN(SQL_ANDOR.OR, "cmp.SvCompanyId", user.PCForCompanyIds)}
          {SQL_IN(SQL_ANDOR.OR, "cmp.SvCompanyId", user.CoachForCompanyIds)}
          {SQL_IN(SQL_ANDOR.OR, "cmp.SvCompanyId", user.SalesPartnerForCompanyIds)}
          {SQL_IN(SQL_ANDOR.OR, "cmp.SvCompanyId", user.FacilitatorForCompanyIds)}";

        if (user.OrgId != ConfigHelper.IntegralTenantOrgId) {
          sql += " OR cmp.OrgId = @UserTenantOrgId";
        }

        sql += ")";

        return sql;
      }

      // Get both Jarvis and Able companies which have surveys with questions in the new global categories.
      // Note Jarvis surveys are found by question -> survey -> job -> company
      // whereas Able surveys are found by question -> participant (in same survey as qn) -> coachee -> job -> project -> company
      public static List<SurveyViewerCompanyInfo> GetCompaniesForSurveyViewer() {
        var cmpList = new List<SurveyViewerCompanyInfo>();
        Common.Query($@"
          SELECT cmp.SvCompanyId, cmp.CompanyGUID, cmp.CompanyName, cmp.OrgId, COUNT(DISTINCT ij.SurveyId) AS SurveyCount
          FROM sv_SurveyCompany cmp WITH (NOLOCK)
          INNER JOIN id_Job ij ON ij.CompanyId = cmp.SvCompanyId
          INNER JOIN sv_360_Questions sq ON sq.SurveyId = ij.SurveyId
          INNER JOIN al_RptQnGrpHgGblQns gh ON gh.GblQuestionId = sq.GblQuestionId
          WHERE cmp.OrgId = 1
          GROUP BY cmp.SvCompanyId, cmp.CompanyGUID, cmp.OrgId, cmp.CompanyName

          UNION ALL

          SELECT cmp.SvCompanyId, cmp.CompanyGUID, cmp.CompanyName, cmp.OrgId, COUNT(DISTINCT sp.SurveyId) AS SurveyCount
          FROM sv_SurveyCompany cmp WITH (NOLOCK)
          INNER JOIN al_Project ap ON cmp.SvCompanyId = ap.SvCompanyId
          INNER JOIN id_Job ij ON ap.JobNumber = ij.JobNumber
          INNER JOIN al_Coachees ac ON ij.JobId = ac.ProgramJobId
          INNER JOIN sv_360_Participants sp ON ac.CoacheeId = sp.AbleCoacheeId
          INNER JOIN sv_360_Questions sq ON sq.SurveyId = sp.SurveyId
          INNER JOIN al_RptQnGrpHgGblQns gh ON gh.GblQuestionId = sq.GblQuestionId
          WHERE cmp.OrgId > 1
          GROUP BY cmp.SvCompanyId, cmp.CompanyGUID, cmp.OrgId, cmp.CompanyName

          ORDER BY cmp.CompanyName, cmp.OrgId DESC",
          dr => {
            var cmpInfo = new SurveyViewerCompanyInfo(
              dr.GetInt("SvCompanyId"),
              dr.GetGuid("CompanyGUID"),
              dr.GetInt("OrgId"),
              dr.GetString("CompanyName"),
              dr.GetInt("SurveyCount")
            );
            cmpList.Add(cmpInfo);
          }
        );
        return cmpList;
      }

      public static List<BriefCompanyInfo> GetCompanyList(AbleUser.AbleUserInfo userInfo) {

        string whereClause = "";
        if (userInfo?.IsAbleAdmin == false) {
          whereClause = "cmp.OrgId = @OrgId";
        }

        var cmpList = new List<BriefCompanyInfo>();
        Common.Query($@"
          SELECT cmp.SvCompanyId, cmp.OrgId, cmp.CompanyName, cmp.WebSiteUrl, cmp.CompanyGUID, cmp.ClientLeadUserId
          FROM sv_SurveyCompany cmp WITH (NOLOCK)
          WHERE cmp.OrgId > 1 {(whereClause.EnsureStartsWith(" AND ", true))}
          ORDER BY cmp.CompanyName",
          dr => {
            var cmpInfo = new BriefCompanyInfo(
              dr.GetInt("SvCompanyId"),
              dr.GetGuid("CompanyGUID"),
              dr.GetInt("OrgId"),
              dr.GetString("CompanyName"),
              dr.GetIntOrNull("ClientLeadUserId")
            );
            cmpList.Add(cmpInfo);
          },
          Common.NewSqlParameter("OrgId", userInfo?.OrgId)
        );
        return cmpList;
      }

      public static AlbertCompanyInfo GetCompanyInfoOrNull(int companyId) {
        return GetCompanyInfoOrNull(companyId, null);
      }

      public static AlbertCompanyInfo GetCompanyInfoOrNull(int companyId, AbleUser.AbleUserInfo loggedInUser) {
        return GetCompanyInfoOrNull(null, companyId, loggedInUser);
      }

      public static AlbertCompanyInfo GetCompanyInfoOrNull(SqlTransaction trans, int companyId, AbleUser.AbleUserInfo loggedInUser) {

        var sqlConditionList = new List<string>();
        if (loggedInUser?.IsAbleAdmin == false) {
          sqlConditionList.AddIfNotNullOrEmpty(SQLWhereCompanyAccessibleToUser(loggedInUser));
        }

        return GetCompanyInfoOrNull(trans,
          $"cmp.SvCompanyId = @SurveyCompanyId {sqlConditionList.Join(" AND ").EnsureStartsWith("AND ", true)}",
          Common.NewSqlParameter("SurveyCompanyId", companyId),
          Common.NewSqlParameter("UserId", loggedInUser?.UserId),
          Common.NewSqlParameter("UserTenantOrgId", loggedInUser?.OrgId),
          Common.NewSqlParameter("UserClientCompanyId", loggedInUser?.ClientCompanyId)
        );
      }

      public static BriefCompanyInfo GetBriefCompanyInfoOrNull(int companyId) {
        return GetBriefCompanyInfoOrNull(companyId, null);
      }

      public static BriefCompanyInfo GetBriefCompanyInfoOrNull(int companyId, AbleUser.AbleUserInfo loggedInUser) {

        BriefCompanyInfo companyInfo = null;

        var sqlConditionList = new List<string>();
        if (loggedInUser?.IsAbleAdmin == false) {
          sqlConditionList.Add(SQLWhereCompanyAccessibleToUser(loggedInUser));
        }

        Common.Query($@"
          SELECT
            cmp.OrgId, cmp.CompanyName, cmp.CompanyGUID, cmp.ClientLeadUserId
          FROM sv_SurveyCompany cmp
          WHERE cmp.SvCompanyId = @CompanyId {sqlConditionList.Join(" AND ").EnsureStartsWith("AND ", true)}",
          dr => {
            companyInfo = new BriefCompanyInfo(
              companyId,
              dr.GetGuid("CompanyGUID"),
              dr.GetInt("OrgId"),
              dr.GetString("CompanyName"),
              dr.GetIntOrNull("ClientLeadUserId")
            );
          },
          Common.NewSqlParameter("@CompanyId", companyId),
          Common.NewSqlParameter("UserId", loggedInUser?.UserId),
          Common.NewSqlParameter("UserTenantOrgId", loggedInUser?.OrgId),
          Common.NewSqlParameter("UserClientCompanyId", loggedInUser?.ClientCompanyId)
        );

        return companyInfo;
      }

      private static AlbertCompanyInfo GetCompanyInfoOrNull(SqlTransaction trans, string sqlWhereConditions, params SqlParameter[] sqlWhereParams) {

        AlbertCompanyInfo companyInfo = null;

        var parameters = new List<SqlParameter>
        {
          Common.NewSqlParameter("@CoacheeStatusId_OnBoarding", CoacheeProgramStatus.GetStatus_Onboarding().ProgramStatusId),
          Common.NewSqlParameter("@CoacheeStatusId_Paused", CoacheeProgramStatus.GetStatus_Paused().ProgramStatusId),
          Common.NewSqlParameter("@CoacheeStatusId_Active", CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId),
        };

        if (sqlWhereParams != null) parameters.AddRange(sqlWhereParams);

        Common.Query(trans, $@"

          WITH TotalParticipants AS (
            SELECT UserId
            FROM sv_User u
            WHERE u.IsParticipant = 1 AND u.ClientCompanyId = @SurveyCompanyId
            UNION
            SELECT DISTINCT ac.UserId
            FROM al_Coachees ac
            WHERE ac.DeletedUtc IS NULL AND ac.CompanyId = @SurveyCompanyId
          ),

          ActiveParticipantsInfo AS (
            SELECT COUNT(DISTINCT ac.UserId) AS TotalActiveCoachees
            FROM al_Coachees ac
            WHERE ac.DeletedUtc IS NULL AND ac.CompanyId = @SurveyCompanyId
              AND ac.ProgramStatusId IN (@CoacheeStatusId_OnBoarding, @CoacheeStatusId_Paused, @CoacheeStatusId_Active)
          )

          SELECT
            cmp.SvCompanyId, cmp.OrgId, cmp.CompanyName, cmp.WebSiteUrl, cmp.CompanyGUID,
            cmp.City, cmp.CountryId, cmp.NumberOfStaff, cmp.SectorId, cmp.ClientLeadUserId, cmp.AI_Context, cmp.ActiveLearnerCount, cmp.DisplayLogoInNavBar, cmp.CreatedByUserId,
            (SELECT COUNT(UserId) from TotalParticipants) as TotalCoachees,
            (SELECT TotalActiveCoachees from ActiveParticipantsInfo) as TotalActiveCoachees,
            sv.TotalEvalsCompleted, sv.Total360sCompleted,
            dp.TotalDevPlansCompleted,
            u.FirstName as ClientLeadFirstName, u.LastName as ClientLeadLastName,

          ( SELECT STRING_AGG(su.UserId, ',')
            FROM (
              SELECT pj.LeadConsultantUserId AS UserId
              FROM id_Job pj
              INNER JOIN al_Project ap ON ap.JobNumber = pj.JobNumber
              WHERE ap.SvCompanyId = cmp.SvCompanyId
              UNION
              SELECT pj.ProjectCoordinatorUserId
              FROM id_Job pj
              INNER JOIN al_Project ap ON ap.JobNumber = pj.JobNumber
              WHERE ap.SvCompanyId = cmp.SvCompanyId
            ) AS su
          ) AS PLCPCUserIds,

          ( SELECT STRING_AGG(su.UserId, ',')
            FROM (
              SELECT pj.Partner_UserId AS UserId
              FROM id_Job pj
              INNER JOIN al_Project ap ON ap.JobNumber = pj.JobNumber
              WHERE ap.SvCompanyId = cmp.SvCompanyId
            ) AS su
          ) AS SalesPUserIds,

          ( SELECT STRING_AGG(su.UserId, ',')
            FROM (
              SELECT DISTINCT ac.CoachUserId AS UserId
              FROM al_Coachees ac
              INNER JOIN id_Job pj ON pj.JobId = ac.ProgramJobId
              INNER JOIN al_Project ap ON ap.JobNumber = pj.JobNumber
              WHERE ap.SvCompanyId = cmp.SvCompanyId
            ) AS su
          ) AS CoachUserIds,

          ( SELECT STRING_AGG(su.UserId, ',')
            FROM (
              SELECT we.KeyFacilitatorUserId AS UserId
              FROM ev_WorkshopEvent we
              INNER JOIN id_Job pj ON pj.JobId = we.ProgramJobId
              INNER JOIN al_Project ap ON ap.JobNumber = pj.JobNumber
              WHERE ap.SvCompanyId = cmp.SvCompanyId
              UNION
              SELECT we.CoFacilitatorUserId
              FROM ev_WorkshopEvent we
              INNER JOIN id_Job pj ON pj.JobId = we.ProgramJobId
              INNER JOIN al_Project ap ON ap.JobNumber = pj.JobNumber
              WHERE ap.SvCompanyId = cmp.SvCompanyId
            ) AS su
          ) AS FacilitatorUserIds,

          (
            SELECT STRING_AGG(su.UserId, ',')
            FROM (
              SELECT DISTINCT upa.UserId AS UserId
              FROM al_UserProjectAccess upa
              INNER JOIN al_Project ap ON ap.ProjectId = upa.ProjectId
              WHERE ap.SvCompanyId = cmp.SvCompanyId
            ) AS su
          ) AS ProjectAccessUserIds

          FROM sv_SurveyCompany cmp
          LEFT OUTER JOIN sv_User u ON u.UserId = cmp.ClientLeadUserId

          CROSS APPLY (
            SELECT
              COUNT(IIF({AlbertSurveys.SQLWhereConditions.EvalSurveys("sv")}, 1, NULL)) AS TotalEvalsCompleted,
              COUNT(IIF({AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}, 1, NULL)) AS Total360sCompleted
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            WHERE sp.AbleSvCompanyId = cmp.SvCompanyId
              AND sp.IsSelf = 1
              AND sp.Completed IS NOT NULL
          ) AS sv

          CROSS APPLY (
            SELECT COUNT(*) AS TotalDevPlansCompleted
            FROM sv_360_Participants dpsp
            INNER JOIN sv_Survey dpsv ON dpsv.sv_id = dpsp.SurveyId
            WHERE dpsp.AbleSvCompanyId = cmp.SvCompanyId
              AND {AlbertSurveys.SQLWhereConditions.DevelopmentPlanSurveys("dpsv")}
              AND dpsp.PercentCompleted = 100
          ) AS dp

          {sqlWhereConditions.EnsureStartsWith("WHERE ", true)}

          ORDER BY cmp.CompanyName",

          dr => {
            companyInfo = new AlbertCompanyInfo(
              dr.GetInt("SvCompanyId"),
              dr.GetGuid("CompanyGUID"),
              dr.GetInt("OrgId"),
              dr.GetString("CompanyName"),
              dr.GetString("WebSiteUrl"),
              dr.GetString("City"),
              dr.GetIntOrNull("CountryId"),
              dr.GetIntOrNull("NumberOfStaff"),
              dr.GetIntOrNull("SectorId"),
              dr.GetIntOrNull("ClientLeadUserId"),
              dr.GetString("AI_Context"),
              dr.GetString("ClientLeadFirstName"),
              dr.GetString("ClientLeadLastName"),
              dr.GetInt("TotalCoachees", 0),
              dr.GetInt("TotalActiveCoachees", 0),
              dr.GetInt("TotalEvalsCompleted", 0),
              dr.GetInt("Total360sCompleted", 0),
              dr.GetInt("TotalDevPlansCompleted", 0),
              dr.GetInt("ActiveLearnerCount", 0),
              dr.GetBoolFromInt("DisplayLogoInNavBar"),
              dr.GetIntOrNull("CreatedByUserId")
            );

            companyInfo.CoachUserIds = dr.GetString("CoachUserIds").ToIntList();
            companyInfo.PLCPCUserIds = dr.GetString("PLCPCUserIds").ToIntList();
            companyInfo.SalesPartnerUserIds = dr.GetString("SalesPUserIds").ToIntList();
            companyInfo.FacilitatorUserIds = dr.GetString("FacilitatorUserIds").ToIntList();
            companyInfo.ProjectAccessUserIds = dr.GetString("ProjectAccessUserIds").ToIntList();
          },
          parameters.ToArray()
        );
        return companyInfo;
      }

      public static BriefCompanyInfo CreateCompanyBrief(SqlTransaction trans, int orgId, string companyName, int? clientLeadUserId) {

        BriefCompanyInfo newCompanyInfo = null;

        Common.Query(trans, @"
          INSERT INTO sv_SurveyCompany (OrgId, CompanyName, ClientLeadUserId)
          OUTPUT INSERTED.SvCompanyId, INSERTED.CompanyGUID
          VALUES (@OrgId, @CompanyName, @ClientLeadUserId)",
          dr => {
            newCompanyInfo = new BriefCompanyInfo(
              companyId: dr.GetInt("SvCompanyId"),
              companyGUID: dr.GetGuid("CompanyGUID"),
              orgId: orgId,
              companyName: companyName,
              clientLeadUserId: clientLeadUserId
            );
          },
          Common.NewSqlParameter("@OrgId", orgId),
          Common.NewSqlParameter("@CompanyName", companyName),
          Common.NewSqlParameter("@ClientLeadUserId", clientLeadUserId));

        return newCompanyInfo;
      }

      public static void CreateCompany(SqlTransaction trans, int orgId, AlbertCompanyInfo companyInfo) {

        Common.Query(trans, @"
          INSERT INTO sv_SurveyCompany (OrgId, CompanyName, WebSiteUrl, City, CountryId, NumberOfStaff, SectorId, ClientLeadUserId, AI_Context, DisplayLogoInNavBar, CreatedByUserId)
          OUTPUT INSERTED.SvCompanyId, INSERTED.CompanyGUID
          VALUES (@OrgId, @CompanyName, @WebSiteUrl, @City, @CountryId, @NumberOfStaff, @SectorId, @ClientLeadUserId, @AI_Context, @DisplayLogoInNavBar, @CreatedByUserId)",
          dr => {
            companyInfo.CompanyId = dr.GetInt("SvCompanyId");
            companyInfo.CompanyGUID = dr.GetGuid("CompanyGUID");
          },
          Common.NewSqlParameter("OrgId", orgId),
          Common.NewSqlParameter("CompanyName", companyInfo.CompanyName),
          Common.NewSqlParameter("WebSiteUrl", companyInfo.WebSiteUrl.EmptyIfNull()),
          Common.NewSqlParameter("City", companyInfo.City.EmptyIfNull()),
          Common.NewSqlParameter("CountryId", companyInfo.CountryId),
          Common.NewSqlParameter("NumberOfStaff", companyInfo.NumberOfStaff),
          Common.NewSqlParameter("SectorId", companyInfo.SectorId),
          Common.NewSqlParameter("ClientLeadUserId", companyInfo.ClientLeadUserId),
          Common.NewSqlParameter("AI_Context", companyInfo.AI_Context.EmptyIfNull()),
          Common.NewSqlParameter("DisplayLogoInNavBar", companyInfo.DisplayLogoInNavBar),
          Common.NewSqlParameter("CreatedByUserId", companyInfo.CreatedByUserId)
        );
      }

      public static AlbertCompanyInfo CreateCompany(SqlTransaction trans, int orgId, string companyName) {

        var companyInfo = new AlbertCompanyInfo();
        companyInfo.CompanyName = companyName;
        CreateCompany(trans, orgId, companyInfo);
        return companyInfo;
      }

      public static void UpdateCompany(SqlTransaction trans, AlbertCompanyInfo companyInfo) {

        Common.GetNonQueryInt(trans, @"
          UPDATE sv_SurveyCompany SET
            CompanyName = @CompanyName,
            WebSiteUrl = @WebSiteUrl,
            City = @City,
            CountryId = @CountryId,
            NumberOfStaff = @NumberOfStaff,
            SectorId = @SectorId,
            ClientLeadUserId = @ClientLeadUserId,
            AI_Context = @AI_Context,
            DisplayLogoInNavBar = @DisplayLogoInNavBar
          WHERE
            SvCompanyId = @CompanyId",
          Common.NewSqlParameter("CompanyId", companyInfo.CompanyId),
          Common.NewSqlParameter("CompanyName", companyInfo.CompanyName),
          Common.NewSqlParameter("WebSiteUrl", companyInfo.WebSiteUrl),
          Common.NewSqlParameter("City", companyInfo.City.EmptyIfNull()),
          Common.NewSqlParameter("CountryId", companyInfo.CountryId),
          Common.NewSqlParameter("NumberOfStaff", companyInfo.NumberOfStaff),
          Common.NewSqlParameter("SectorId", companyInfo.SectorId),
          Common.NewSqlParameter("ClientLeadUserId", companyInfo.ClientLeadUserId),
          Common.NewSqlParameter("AI_Context", companyInfo.AI_Context.EmptyIfNull()),
          Common.NewSqlParameter("DisplayLogoInNavBar", companyInfo.DisplayLogoInNavBar)
        );
      }

      public static List<AlbertCompanyInfo> GetCompanyList_BySearch(string searchTerm, bool statusInactive, AbleUser.AbleUserInfo forUser) {

        if (forUser == null || forUser.CurrentRole == ConfigHelper.UserRole.Unset) return null;

        string sqlWhereClause = string.Empty;

        // If there's a search term
        if (!searchTerm.IsNullOrEmpty()) {
          sqlWhereClause = sqlWhereClause.AppendWithSeparator(
            " AND ", $@"
            ( u.FirstName LIKE '%' + @SearchTerm + '%'
              OR u.LastName LIKE '%' + @SearchTerm + '%'
              OR u.Email LIKE '%' + @SearchTerm + '%'
              OR cmp.CompanyName LIKE '%' + @SearchTerm + '%'
              OR cmp.City LIKE '%' + @SearchTerm + '%'
              OR s.SectorName LIKE '%' + @SearchTerm + '%'
              OR c.CountryName LIKE '%' + @SearchTerm + '%'
              OR c.CountryCode LIKE '%' + @SearchTerm + '%'
            )");
        }

        if (statusInactive) {
          sqlWhereClause = sqlWhereClause.AppendWithSeparator(
            " AND ",
            "(prj.TotalActivePrograms = 0 OR prj.TotalActivePrograms IS NULL)");
        } else {
          sqlWhereClause = sqlWhereClause.AppendWithSeparator(
            " AND ",
            "prj.TotalActivePrograms > 0");
        }

        sqlWhereClause = sqlWhereClause.AppendWithSeparator(
          " AND ",
          SQLWhereCompanyAccessibleToUser(forUser));

        return GetCompanyList(
          sqlWhereClause,
          Common.NewSqlParameter("SearchTerm", searchTerm),
          Common.NewSqlParameter("UserId", forUser?.UserId),
          Common.NewSqlParameter("UserTenantOrgId", forUser?.OrgId),
          Common.NewSqlParameter("UserClientCompanyId", forUser?.ClientCompanyId)
        );
      }

      private static List<AlbertCompanyInfo> GetCompanyList(string whereClause, params SqlParameter[] sqlWhereParams) {

        var companyList = new List<AlbertCompanyInfo>();

        string sql = $@"
        SELECT
          cmp.svCompanyId, cmp.CompanyName, cmp.OrgId, o.OrgName, cmp.ClientLeadUserId, cmp.City, cmp.NumberOfStaff, cmp.ActiveLearnerCount, cmp.DisplayLogoInNavBar, cmp.CreatedByUserId,
          u.FirstName as ClientLeadFirstName, u.LastName as ClientLeadLastName, u.Email as ClientLeadEmail,
          prj.TotalProjects, prj.TotalPrograms, prj.TotalActivePrograms,
          cd.TotalDeliveryRevenue, cd.TotalCompletedDeliveryRevenue,
          qdw.TotalDealWonRevenue, qdo.TotalDealOutstandingRevenue,
          ac.TotalCoachees, ac.TotalActiveCoachees,
          s.SectorName, c.CountryName, c.CountryCode,
          sub.TotalSubscriptions, sub.TotalActiveSubscriptions
        FROM sv_SurveyCompany cmp
        INNER JOIN sv_Organisation o ON o.OrgId = cmp.OrgId
        LEFT OUTER JOIN sv_Sector s ON s.SectorId = cmp.SectorId
        LEFT OUTER JOIN id_Country c ON c.CountryId = cmp.CountryId
        LEFT OUTER JOIN sv_User u ON u.UserId = cmp.ClientLeadUserId

        CROSS APPLY (
          SELECT
            COUNT(pr.ProjectId) AS TotalProjects,
            COUNT(j.JobId) AS TotalPrograms,
            SUM(IIF(j.ProgramStatusId <= @ProgramStatusId_Complete, 1, 0)) AS TotalActivePrograms
          FROM al_Project pr
          INNER JOIN id_Job j ON j.JobNumber = pr.JobNumber
          WHERE pr.SvCompanyId = cmp.SvCompanyId
        ) AS prj

        CROSS APPLY (
          SELECT
            SUM(cmpt.ComponentPrice) AS TotalDeliveryRevenue,
            SUM(IIF(cmpt.CompletedDateUtc <= GETUTCDATE(), cmpt.ComponentPrice, 0)) AS TotalCompletedDeliveryRevenue
          FROM al_Project pr
          INNER JOIN id_Job j ON j.JobNumber = pr.JobNumber
          INNER JOIN al_Component cmpt ON cmpt.ProgramJobId = j.JobId
          WHERE pr.SvCompanyId = cmp.SvCompanyId
            AND j.ProgramStatusId <= @ProgramStatusId_Complete
        ) AS cd

        CROSS APPLY (
          SELECT
            SUM(q.ClientAcceptedAmount) AS TotalDealWonRevenue
          FROM al_Project pr
          INNER JOIN al_Quote q ON q.JobNumber = pr.JobNumber
          WHERE pr.SvCompanyId = cmp.SvCompanyId
            AND q.DeletedUtc IS NULL
            AND (q.QuoteStatusId = @QuoteStatusId_Accepted OR q.ClientAcceptedUtc IS NOT NULL)
        ) AS qdw

        CROSS APPLY (
          SELECT
            SUM(qi.UnitPrice * qi.Quantity) AS TotalDealOutstandingRevenue
          FROM al_Project pr
          INNER JOIN al_Quote q ON q.JobNumber = pr.JobNumber
          INNER JOIN al_QuoteItem qi ON qi.QuoteId = q.QuoteId
          WHERE pr.SvCompanyId = cmp.SvCompanyId
            AND q.DeletedUtc IS NULL
            AND (q.QuoteStatusId = @QuoteStatusId_Draft OR q.QuoteStatusId = @QuoteStatusId_ClientSigning)
        ) AS qdo

        CROSS APPLY (
          SELECT
            COUNT(DISTINCT u.UserId) AS TotalCoachees,
            SUM(IIF(ac.ProgramStatusId BETWEEN @CoacheeStatusId_OnBoarding AND @CoacheeStatusId_Paused, 1, 0)) AS TotalActiveCoachees
          FROM al_Project pr
          INNER JOIN id_Job j ON j.JobNumber = pr.JobNumber
          INNER JOIN al_Coachees ac ON j.JobId = ac.ProgramJobId
          INNER JOIN sv_User u ON u.UserId = ac.UserId
          WHERE pr.SvCompanyId = cmp.SvCompanyId
            AND ac.DeletedUtc IS NULL
            AND (pr.SvCompanyId = cmp.SvCompanyId OR u.IsParticipant = 1 AND u.ClientCompanyId = cmp.SvCompanyId)
        ) AS ac

        CROSS APPLY (
          SELECT
            COUNT(*) AS TotalSubscriptions,
            SUM(IIF(us.SubscriptionEndUtc >= GETUTCDATE(), 1, 0)) AS TotalActiveSubscriptions
          FROM al_Project pr
          INNER JOIN id_Job j ON j.JobNumber = pr.JobNumber
          INNER JOIN al_Coachees ac ON j.JobId = ac.ProgramJobId
          INNER JOIN al_UserSubscription us ON us.UserId = ac.UserId
          WHERE pr.SvCompanyId = cmp.SvCompanyId
            AND ac.DeletedUtc IS NULL
        ) AS sub

        WHERE cmp.CompanyName <> ''
          AND cmp.OrgId <> @JarvisOrgId
          {whereClause.EnsureStartsWith(" AND ", true)}
        ORDER BY cmp.CompanyName";

        var sqlParams = new List<SqlParameter>() {
          Common.NewSqlParameter("@JarvisOrgId", ConfigHelper.JarvisOrgId),
          Common.NewSqlParameter("@ProgramStatusId_Active", AlbertProgramStatus.Statuses.Active.ProgramStatusId),
          Common.NewSqlParameter("@ProgramStatusId_Setup", AlbertProgramStatus.Statuses.Setup.ProgramStatusId),
          Common.NewSqlParameter("@ProgramStatusId_Complete", AlbertProgramStatus.Statuses.Complete.ProgramStatusId),
          Common.NewSqlParameter("@QuoteStatusId_Draft", AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.draft).QuoteStatusId),
          Common.NewSqlParameter("@QuoteStatusId_ClientSigning", AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.client).QuoteStatusId),
          Common.NewSqlParameter("@QuoteStatusId_Accepted", AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusId),
          Common.NewSqlParameter("@CoacheeStatusId_OnBoarding", CoacheeProgramStatus.GetStatus_Onboarding().ProgramStatusId),
          Common.NewSqlParameter("@CoacheeStatusId_Paused", CoacheeProgramStatus.GetStatus_Paused().ProgramStatusId),
          Common.NewSqlParameter("@CoacheeStatusId_Active", CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId)
        };
        foreach (var whereParam in sqlWhereParams) {
          var foundParam = sqlParams.Find(p => p.ParameterName.Equals(whereParam.ParameterName, StringComparison.OrdinalIgnoreCase));
          if (foundParam != null) foundParam.Value = whereParam.Value; // replace value of existing parameter
          else sqlParams.Add(whereParam); // add new parameter
        }

        Common.Query(sql, sqlParams,
          dr => {
            companyList.Add(new AlbertCompanyInfo(
                dr.GetInt("SvCompanyId"),
                dr.GetString("CompanyName"),
                dr.GetInt("OrgId"),
                dr.GetString("OrgName"),
                dr.GetInt("NumberOfStaff", 0),
                dr.GetIntOrNull("ClientLeadUserId"),
                dr.GetString("ClientLeadFirstName"),
                dr.GetString("ClientLeadLastName"),
                dr.GetString("ClientLeadEmail"),
                dr.GetInt("TotalProjects", 0),
                dr.GetInt("TotalPrograms", 0),
                dr.GetInt("TotalActivePrograms", 0),
                dr.GetInt("TotalCoachees", 0),
                dr.GetInt("TotalActiveCoachees", 0),
                dr.GetDecimal("TotalDealWonRevenue", 0),
                dr.GetDecimal("TotalDealOutstandingRevenue", 0),
                dr.GetDecimal("TotalDeliveryRevenue", 0),
                dr.GetDecimal("TotalCompletedDeliveryRevenue", 0),
                dr.GetInt("TotalSubscriptions", 0),
                dr.GetInt("TotalActiveSubscriptions", 0),
                dr.GetInt("ActiveLearnerCount", 0),
                dr.GetBoolFromInt("DisplayLogoInNavBar"),
                dr.GetIntOrNull("CreatedByUserId")
              )
            );
          }
        );

        return companyList;
      }

      public static void UpdateActiveLearnerCounts(DateTime fromDate) {

        Common.GetNonQueryInt(@"

          UPDATE sc
          SET sc.ActiveLearnerCount = ac.ActiveLearnerCount
          FROM sv_SurveyCompany sc
          INNER JOIN (
            SELECT sc.SvCompanyId, ac.ActiveLearnerCount
            FROM sv_SurveyCompany sc
            CROSS APPLY (
              SELECT COUNT(*) AS ActiveLearnerCount
              FROM al_Coachees ac
              WHERE ac.CompanyId = sc.SvCompanyId
              AND (
                EXISTS (
                  SELECT 1
                  FROM al_AIMessage aim
                  WHERE aim.UserId = ac.UserId
                    AND aim.IsFromAI = 0
                    AND aim.SentUtc >= @FromDate
                )
                OR EXISTS (
                  SELECT 1
                  FROM ev_WorkshopEvent we
                  WHERE we.ProgramJobId = ac.ProgramJobId
                    AND we.StartDateUtc BETWEEN @FromDate AND GETUTCDATE()
                )
                OR EXISTS (
                  SELECT 1
                  FROM id_CoachingSession cs
                  WHERE cs.AbleCoacheeId = ac.CoacheeId
                    AND cs.ApptDateUTC BETWEEN @FromDate AND GETUTCDATE()
                )
                OR EXISTS (
                  SELECT 1
                  FROM sv_360_Participants sp
                  INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
                  WHERE sp.AbleCoacheeId = ac.CoacheeId
                    AND sv.SurveyTypeCode IN (@SurveyType_Pulse, @SurveyType_Intake, @SurveyType_360, @SurveyType_Eval, @SurveyType_DevPlan)
                    AND sp.Completed >= @FromDate
                )
                OR EXISTS (
                  SELECT 1
                  FROM al_UserContent uc
                  WHERE uc.UserId = ac.UserId
                    AND uc.ViewedUtc >= @FromDate
                )
              )
            ) AS ac
          ) ac ON sc.SvCompanyId = ac.SvCompanyId;",

          Common.NewSqlParameter("@SurveyType_Pulse", ConfigHelper.SurveyTypeCodes.Pulse360),
          Common.NewSqlParameter("@SurveyType_Intake", ConfigHelper.SurveyTypeCodes.Intake),
          Common.NewSqlParameter("@SurveyType_360", ConfigHelper.SurveyTypeCodes.Able360),
          Common.NewSqlParameter("@SurveyType_Eval", ConfigHelper.SurveyTypeCodes.Eval),
          Common.NewSqlParameter("@SurveyType_DevPlan", ConfigHelper.SurveyTypeCodes.DevPlan),
          Common.NewSqlParameter("FromDate", fromDate)
        );
      }

      public static bool CompanyNameExists(string companyName) {
        return Common.GetScalarQueryInt(@"
          IF EXISTS (
            SELECT 1
            FROM sv_SurveyCompany
            WHERE CompanyName = @CompanyName
          )
          BEGIN
            SELECT 1
          END
          ELSE
          BEGIN
            SELECT 0
          END",
          Common.NewSqlParameter("CompanyName", companyName)) > 0;
      }

      public class AlbertCompanyInfo {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int OrgId { get; set; }
        public string OrgName { get; set; }
        public int? NumberOfStaff { get; set; }
        public Guid CompanyGUID { get; internal set; }
        public string WebSiteUrl { get; set; }
        public string City { get; set; }
        public int? CountryId { get; set; }
        public int? SectorId { get; set; }
        public int? ClientLeadUserId { get; set; }
        public string ClientLeadFirstName { get; private set; }
        public string ClientLeadLastName { get; private set; }
        public string ClientLeadEmail { get; private set; }
        public string AI_Context { get; set; }
        public int TotalProjects { get; private set; }
        public int TotalPrograms { get; private set; }
        public int TotalActivePrograms { get; private set; }
        public int TotalCoachees { get; private set; }
        public int TotalActiveCoachees { get; private set; }
        public decimal TotalDealWonRevenue { get; private set; }
        public decimal TotalDealOutstandingRevenue { get; private set; }
        public decimal TotalDeliveryRevenue { get; private set; }
        public decimal TotalCompletedDeliveryRevenue { get; private set; }
        public int TotalSubscriptions { get; private set; }
        public int TotalActiveSubscriptions { get; private set; }
        public int TotalEvalsCompleted { get; set; }
        public int Total360sCompleted { get; set; }
        public int TotalDevPlansCompleted { get; set; }
        public int ActiveLearnerCount { get; private set; }
        public bool DisplayLogoInNavBar { get; private set; }
        public int? CreatedByUserId { get; private set; }

        internal List<int> CoachUserIds { private get; set; }
        internal List<int> PLCPCUserIds { private get; set; }
        internal List<int> SalesPartnerUserIds { private get; set; }
        internal List<int> FacilitatorUserIds { private get; set; }
        internal List<int> ProjectAccessUserIds { private get; set; }

        public bool IsCoachInCompany(int userId) => CoachUserIds != null && CoachUserIds.Contains(userId);
        public bool IsPLCPCCompany(int userId) => PLCPCUserIds != null && PLCPCUserIds.Contains(userId);
        public bool IsSalesPartnerInCompany(int userId) => SalesPartnerUserIds != null && SalesPartnerUserIds.Contains(userId);
        public bool IsFacilitatorInCompany(int userId) => FacilitatorUserIds != null && FacilitatorUserIds.Contains(userId);
        public bool IsProjectAccessInCompany(int userId) => ProjectAccessUserIds != null && ProjectAccessUserIds.Contains(userId);

        public AlbertCompanyInfo() { }

        // Used for lists.
        public AlbertCompanyInfo(
          int companyId,
          string companyName,
          int orgId,
          string orgName,
          int? numberOfStaff,
          int? clientLeadUserId,
          string clientLeadFirstName,
          string clientLeadLastName,
          string clientLeadEmail,
          int totalProjects,
          int totalPrograms,
          int totalActivePrograms,
          int totalCoachees,
          int totalActiveCoachees,
          decimal totalDealWonRevenue,
          decimal totalDealOutstandingRevenue,
          decimal totalDeliveryRevenue,
          decimal totalCompletedDeliveryRevenue,
          int totalSubscriptions,
          int totalActiveSubscriptions,
          int activeLearnerCount,
          bool displayLogoInNavBar,
          int? createdByUserId
        ) {
          this.CompanyId = companyId;
          this.CompanyName = companyName;
          this.OrgId = orgId;
          this.OrgName = orgName;
          this.NumberOfStaff = numberOfStaff;
          this.ClientLeadUserId = clientLeadUserId;
          this.ClientLeadFirstName = clientLeadFirstName;
          this.ClientLeadLastName = clientLeadLastName;
          this.ClientLeadEmail = clientLeadEmail;
          this.TotalProjects = totalProjects;
          this.TotalPrograms = totalPrograms;
          this.TotalActivePrograms = totalActivePrograms;
          this.TotalCoachees = totalCoachees;
          this.TotalActiveCoachees = totalActiveCoachees;
          this.TotalDealWonRevenue = totalDealWonRevenue;
          this.TotalDealOutstandingRevenue = totalDealOutstandingRevenue;
          this.TotalDeliveryRevenue = totalDeliveryRevenue;
          this.TotalCompletedDeliveryRevenue = totalCompletedDeliveryRevenue;
          this.TotalSubscriptions = totalSubscriptions;
          this.TotalActiveSubscriptions = totalActiveSubscriptions;
          this.ActiveLearnerCount = activeLearnerCount;
          this.DisplayLogoInNavBar = displayLogoInNavBar;
          this.CreatedByUserId = createdByUserId;
        }

        // Used for full company info.
        public AlbertCompanyInfo(
          int companyId,
          Guid companyGUID,
          int orgId,
          string companyName,
          string webSiteUrl,
          string city,
          int? countryId,
          int? numberOfStaff,
          int? sectorId,
          int? clientLeadUserId,
          string aI_Context,
          string clientLeadFirstName = null,
          string clientLeadLastName = null,
          int totalCoachees = 0,
          int totalActiveCoachees = 0,
          int totalEvalsCompleted = 0,
          int total360sCompleted = 0,
          int totalDevPlansCompleted = 0,
          int activeLearnerCount = 0,
          bool displayLogoInNavBar = false,
          int? createdByUserId = null
        ) {
          this.CompanyId = companyId;
          this.CompanyGUID = companyGUID;
          this.OrgId = orgId;
          this.CompanyName = companyName;
          this.WebSiteUrl = webSiteUrl;
          this.City = city;
          this.CountryId = countryId;
          this.NumberOfStaff = numberOfStaff;
          this.SectorId = sectorId;
          this.ClientLeadUserId = clientLeadUserId;
          this.AI_Context = aI_Context;
          this.TotalCoachees = totalCoachees;
          this.TotalActiveCoachees = totalActiveCoachees;
          this.TotalEvalsCompleted = totalEvalsCompleted;
          this.Total360sCompleted = total360sCompleted;
          this.TotalDevPlansCompleted = totalDevPlansCompleted;
          this.ClientLeadFirstName = clientLeadFirstName;
          this.ClientLeadLastName = clientLeadLastName;
          this.ActiveLearnerCount = activeLearnerCount;
          this.DisplayLogoInNavBar = displayLogoInNavBar;
          this.CreatedByUserId = createdByUserId;
        }

        public string ClientLeadFullName() {
          return $"{ClientLeadFirstName} {ClientLeadLastName}";
        }
      }

      public class BriefCompanyInfo {
        public int CompanyId { get; internal set; }
        public Guid CompanyGUID { get; internal set; }
        public int OrgId { get; internal set; }
        public string CompanyName { get; internal set; }
        public int? ClientLeadUserId { get; internal set; }

        public BriefCompanyInfo() { }

        public BriefCompanyInfo(
          int companyId,
          Guid companyGUID,
          int orgId,
          string companyName,
          int? clientLeadUserId
        ) {
          this.CompanyId = companyId;
          this.CompanyGUID = companyGUID;
          this.OrgId = orgId;
          this.CompanyName = companyName;
          this.ClientLeadUserId = clientLeadUserId;
        }
      }

      public class SurveyViewerCompanyInfo : BriefCompanyInfo {

        public int SurveyCount { get; internal set; }

        public SurveyViewerCompanyInfo(
          int companyId,
          Guid companyGUID,
          int orgId,
          string companyName,
          int surveyCount
        ) {
          this.CompanyId = companyId;
          this.CompanyGUID = companyGUID;
          this.OrgId = orgId;
          this.CompanyName = companyName;
          this.SurveyCount = surveyCount;
        }
      }

    }
  }
}
