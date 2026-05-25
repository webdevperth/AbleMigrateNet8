using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using static Integral.Web.DbHelper.Common;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AblePrograms {

      public const int UniqueId_Assign_MaxTries = 20; // Number of tries to assign a uniqueid before failing.

      private const string UniqueIdValidChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
      private const string UniqueIdValidRegex = "[0-9a-zA-Z]+";
      private const int UniqueIdMinLength = 10;
      private const int UniqueIdMaxLength = 20;
      private const string Param_OwnUser_UserId = "@OwnUser_UserId";
      private const string Param_OwnUser_OrgId = "@OwnUser_OrgId";

      public enum WhereRelatedUserIs { NoCheck, Tenant, Tenant_PLC_PC, Tenant_AnyRelated }

      public static AbleProgramList GetProgramListForCompany(
        int companyId,
        WhereRelatedUserIs whereRelatedUserIs,
        AbleUser.UserIdentity relatedUser = null) {

        var list = GetProgramInfoList(
          topOrNullForAll: null,
          whereRelatedUserIs: whereRelatedUserIs,
          relatedUser: relatedUser,
          extraJoins: null,
          whereConditionsSQL: "j.CompanyId = @CompanyId",
          sqlWhereParams: new List<SqlParameter>() { NewSqlParameter("CompanyId", companyId) },
          sqlOrderBy: "j.AbleProgramStartDateUtc DESC"
        );

        if ((list?.ProgramInfoList?.Count ?? 0) == 0) return null;

        return list;
      }

      public static AbleProgramInfo GetProgramInfoOrNull(int programJobId) {
        return GetProgramInfoOrNull(programJobId, WhereRelatedUserIs.NoCheck);
      }

      public static AbleProgramInfo GetProgramInfoOrNull(
        int programJobId,
        WhereRelatedUserIs whereRelatedUserIs,
        AbleUser.UserIdentity relatedUser = null) {

        var list = GetProgramInfoList(
          topOrNullForAll: 1,
          whereRelatedUserIs: whereRelatedUserIs,
          relatedUser: relatedUser,
          extraJoins: null,
          whereConditionsSQL: "j.JobId = @JobId",
          sqlWhereParams: new List<SqlParameter>() { new SqlParameter("@JobId", SqlDbType.Int) { Value = programJobId } },
          sqlOrderBy: null);

        if ((list?.ProgramInfoList?.Count ?? 0) == 0) return null;

        return list.ProgramInfoList[0];
      }

      public static AbleProgramList GetProgramsByJobNumber(string jobNumber) {
        return GetProgramsByJobNumber(jobNumber, WhereRelatedUserIs.NoCheck, null);
      }
      public static AbleProgramList GetProgramsByJobNumber(
        string jobNumber,
        WhereRelatedUserIs whereRelatedUserIs,
        AbleUser.UserIdentity relatedUser) {

        var list = GetProgramInfoList(
          topOrNullForAll: null,
          whereRelatedUserIs: whereRelatedUserIs,
          relatedUser: relatedUser,
          extraJoins: null,
          whereConditionsSQL: "j.JobNumber = @JobNumber",
          sqlWhereParams: new List<SqlParameter>() { new SqlParameter("@JobNumber", SqlDbType.VarChar, 20) { Value = jobNumber } },
          sqlOrderBy: null);

        if ((list?.ProgramInfoList?.Count ?? 0) == 0) return null;

        return list;
      }

      public static AbleProgramInfo GetProgramInfoFromInvoiceItemOrNull(int invoiceItemId) {

        var list = GetProgramInfoList(
          topOrNullForAll: 1,
          whereRelatedUserIs: WhereRelatedUserIs.NoCheck,
          relatedUser: null,
          extraJoins: "INNER JOIN al_InvoiceItem ii ON ii.ProgramJobId = j.JobId",
          whereConditionsSQL: "ii.InvoiceItemId = @InvoiceItemId",
          sqlWhereParams: NewSqlParameterList(NewSqlParameter("InvoiceItemId", invoiceItemId)),
          sqlOrderBy: null);

        if ((list?.ProgramInfoList?.Count ?? 0) == 0) return null;

        return list.ProgramInfoList[0];
      }

      public class ProjectProgramsListItem {
        public int ProgramJobId { get; private set; }
        public string JobNumber { get; private set; }
        public string JobName { get; private set; }
        public AlbertProgramStatus.ProgramStatusInfo ProgramStatus { get; private set; }
        public DateTime? StartDateUtc { get; private set; }
        public decimal? ExpectedRevenue { get; private set; }
        public decimal TotalInvoiced { get; private set; }
        public decimal TotalXeroInvoiced { get; private set; }
        public ProjectProgramsListItem(
          int programJobId,
          string jobNumber,
          string jobName,
          AlbertProgramStatus.ProgramStatusInfo programStatus,
          DateTime? startDateUtc,
          decimal? expectedRevenue,
          decimal totalInvoiced,
          decimal totalXeroInvoiced
        ) {
          ProgramJobId = programJobId;
          JobNumber = jobNumber;
          JobName = jobName;
          ProgramStatus = programStatus;
          StartDateUtc = startDateUtc;
          ExpectedRevenue = expectedRevenue;
          TotalInvoiced = totalInvoiced;
          TotalXeroInvoiced = totalXeroInvoiced;
        }
      }

      public static AlbertProgramStatus.ProgramStatusInfo GetProgramStatus(int programJobId) {

        AlbertProgramStatus.ProgramStatusInfo programStatus = null;

        Query(
          "SELECT ProgramStatusId FROM id_Job WHERE JobId = @JobId",
          dr => {
            var statusId = dr.GetIntOrNull("ProgramStatusId");
            if (statusId != null) {
              programStatus = AlbertProgramStatus.GetProgramStatusById(statusId.Value);
            }
          },
          NewSqlParameter("JobId", programJobId)
        );

        return programStatus;
      }

      public static List<ProjectProgramsListItem> GetProjectProgramsList(string projectJobNumber) {

        var list = new List<ProjectProgramsListItem>();

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          string sql = @"

            SELECT j.JobId, j.JobNumber, j.JobName, j.ProgramStatusId, j.AbleProgramStartDateUtc, j.TotalRevenueChecksum, ii.TotalInvoiced, ii.TotalXeroInvoiced
            FROM id_Job j
            OUTER APPLY (
              SELECT
                SUM (ii.UnitPrice * ii.Quantity) AS TotalInvoiced,
                SUM (IIF(ii.InvoiceId IS NULL, 0, ii.UnitPrice * ii.Quantity)) AS TotalXeroInvoiced
              FROM al_InvoiceItem ii
              WHERE ii.ProgramJobId = j.JobId
            ) AS ii
            WHERE j.JobNumber = @JobNumber
            ORDER BY j.JobName";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddVarChar("@JobNumber", 20, projectJobNumber);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                list.Add(new ProjectProgramsListItem(
                  dr.GetInt("JobId"),
                  dr.GetString("JobNumber"),
                  dr.GetString("JobName"),
                  AlbertProgramStatus.GetProgramStatusByIdOrNull(dr.GetIntOrNull("ProgramStatusId")),
                  dr.GetDateTimeOrNull("AbleProgramStartDateUtc"),
                  dr.GetDecimalOrNull("TotalRevenueChecksum"),
                  dr.GetDecimalOrNull("TotalInvoiced") ?? 0,
                  dr.GetDecimalOrNull("TotalXeroInvoiced") ?? 0
                ));
              }
            }
          }
          return list;
        }
      }

      public class StatusSearchOpts {

        public List<AlbertProgramStatus.ProgramStatusInfo> IncludeStatuses { get; private set; }
        public List<AlbertProgramStatus.ProgramStatusInfo> ExcludeStatuses { get; private set; }

        public StatusSearchOpts() {
          IncludeStatuses = new List<AlbertProgramStatus.ProgramStatusInfo>();
          ExcludeStatuses = new List<AlbertProgramStatus.ProgramStatusInfo>();
        }
        public StatusSearchOpts AddIncludeStatuses(params AlbertProgramStatus.ProgramStatusInfo[] status) {
          if (status != null) IncludeStatuses.AddRange(status);
          return this;
        }
        public StatusSearchOpts AddExcludeStatuses(params AlbertProgramStatus.ProgramStatusInfo[] status) {
          if (status != null) ExcludeStatuses.AddRange(status);
          return this;
        }
      }

      public static AbleProgramList GetMainProgramList(
        AppHelper.ParamEnum.ProjectAccessLevel programAccessLevel,
        AbleUser.UserIdentity forUser,
        int? forCompanyId,
        string searchTerm,
        StatusSearchOpts searchOpts,
        int? offsetRows = null,
        int? fetchRows = null) {

        if (programAccessLevel == AppHelper.ParamEnum.ProjectAccessLevel.None) {
          return new AbleProgramList();
        }

        var whereConditions = new List<string>();
        var whereParams = NewSqlParameterList();

        if (!searchTerm.IsNullOrEmpty()) {
          whereConditions.Add(@"
          (
            j.JobNumber LIKE '%' + @term + '%'
            OR j.JobName LIKE '%' + @term + '%'
            OR c.CompanyName LIKE '%' + @term + '%'
            OR prj.ProjectName LIKE '%' + @term + '%'
          ) ");
          whereParams.Add(NewSqlParameter("@term", searchTerm.LimitLengthTo(50), 50));
        }

        if (searchOpts != null) {

          if (searchOpts.IncludeStatuses != null && searchOpts.IncludeStatuses.Count > 0) {
            if (searchOpts.IncludeStatuses.Count == 1) {
              whereConditions.Add("j.ProgramStatusId = @ProgramStatusId");
              whereParams.Add(NewSqlParameter("@ProgramStatusId", searchOpts.IncludeStatuses[0].ProgramStatusId));
            } else {
              whereConditions.Add($"j.ProgramStatusId IN({searchOpts.IncludeStatuses.Join(",", ps => ps.ProgramStatusId.ToString())})");
            }
          }

          if (searchOpts.ExcludeStatuses != null && searchOpts.ExcludeStatuses.Count > 0) {
            if (searchOpts.ExcludeStatuses.Count == 1) {
              whereConditions.Add("j.ProgramStatusId <> @ProgramStatusId");
              whereParams.Add(NewSqlParameter("@ProgramStatusId", searchOpts.ExcludeStatuses[0].ProgramStatusId));
            } else {
              whereConditions.Add($"j.ProgramStatusId NOT IN({searchOpts.ExcludeStatuses.Join(",", ps => ps.ProgramStatusId.ToString())})");
            }
          }
        }

        var whereRelatedUserIs = WhereRelatedUserIs.Tenant_PLC_PC; // safety default

        if (programAccessLevel == AppHelper.ParamEnum.ProjectAccessLevel.All) {
          whereRelatedUserIs = WhereRelatedUserIs.NoCheck;

        } else if (programAccessLevel == AppHelper.ParamEnum.ProjectAccessLevel.TenantOrg) {
          whereRelatedUserIs = WhereRelatedUserIs.Tenant;

        } else if (programAccessLevel == AppHelper.ParamEnum.ProjectAccessLevel.RelatedOrInvited) {
          whereRelatedUserIs = WhereRelatedUserIs.Tenant_AnyRelated;
        }

        if (forCompanyId != null) {
          whereConditions.Add("prj.SvCompanyId = @CompanyId");
          whereParams.Add(NewSqlParameter("@CompanyId", forCompanyId));
        }

        return GetProgramInfoList(
          topOrNullForAll: null,
          whereRelatedUserIs: whereRelatedUserIs,
          relatedUser: forUser,
          extraJoins: null,
          whereConditionsSQL: whereConditions.Join(" AND "),
          sqlWhereParams: whereParams,
          sqlOrderBy: "j.AbleProgramEndDateUtc DESC",
          offsetRows: offsetRows,
          fetchRows: fetchRows);
      }

      public static AbleProgramList GetProgramsToFlagActive() {
        // Programs are flagged "Active" when program status is "Setup" and one or more participants is "On-Boarding" or "Active"

        return GetProgramInfoList(
          topOrNullForAll: null,
          whereRelatedUserIs: WhereRelatedUserIs.NoCheck,
          relatedUser: null,
          extraJoins: $@"
            CROSS APPLY (
              SELECT
                SUM(IIF(ac.ProgramStatusId = {CoacheeProgramStatus.Ids.Onboarding}, 1, 0)) AS CountOnboarding,
                SUM(IIF(ac.ProgramStatusId = {CoacheeProgramStatus.Ids.Active}, 1, 0)) AS CountActive
              FROM al_Coachees ac
              WHERE ac.ProgramJobId = j.JobId
                AND ac.DeletedUtc IS NULL
            ) AS cps",
          whereConditionsSQL: $"j.ProgramStatusId = {AlbertProgramStatus.Ids.Setup} AND (cps.CountOnboarding > 0 OR cps.CountActive > 0)",
          sqlWhereParams: null,
          sqlOrderBy: null);
      }

      public static AbleProgramList GetProgramsToFlagComplete() {

        return GetProgramInfoList(
          topOrNullForAll: null,
          whereRelatedUserIs: WhereRelatedUserIs.NoCheck,
          relatedUser: null,
          extraJoins: $@"
            CROSS APPLY (
              SELECT COUNT(ac.CoacheeId) AS CountAll, SUM(IIF(ac.ProgramStatusId = {CoacheeProgramStatus.Ids.EndProgram}, 1, 0)) AS CountEnd
              FROM al_Coachees ac
              WHERE ac.ProgramJobId = j.JobId
                AND ac.DeletedUtc IS NULL
            ) AS ep
            OUTER APPLY (
              SELECT ISNULL(SUM(IIF(we.StartDate IS NULL OR we.StartDate > GETUTCDATE(), 1, 0)), 0) AS UpcomingWorkshopCount
              FROM ev_WorkshopEvent we
              WHERE we.ProgramJobId = j.JobId
            ) AS weUp
            OUTER APPLY (
              SELECT ISNULL(SUM(IIF(ci.CompletionDateUtc IS NULL OR ci.CompletionDateUtc > GETUTCDATE(), 1, 0)), 0) AS UpcomingConsultingCount
              FROM al_ConsultingItems ci
              WHERE ci.ProgramJobId = j.JobId
            ) AS ciUp",
          whereConditionsSQL: $@"
            WHERE ISNULL(j.ProgramStatusId, 0) = {AlbertProgramStatus.Ids.Active}
              AND ep.CountEnd = ep.CountAll
              AND ISNULL(weUp.UpcomingWorkshopCount, 0) = 0
              AND ISNULL(ciUp.UpcomingConsultingCount, 0) = 0",
          sqlWhereParams: null,
          sqlOrderBy: null);
      }

      private static AbleProgramList GetProgramInfoList(
        int? topOrNullForAll,
        WhereRelatedUserIs whereRelatedUserIs,
        AbleUser.UserIdentity relatedUser,
        string extraJoins,
        string whereConditionsSQL,
        List<SqlParameter> sqlWhereParams,
        string sqlOrderBy,
        int? offsetRows = null,
        int? fetchRows = null) {

        if (whereRelatedUserIs != WhereRelatedUserIs.NoCheck && relatedUser == null) { // user required.
          throw new ApplicationException($"{nameof(relatedUser)} required when {nameof(whereRelatedUserIs)} is set.");
        }

        var programJobList = new AbleProgramList();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);

        var whereConditions = new List<string>();
        whereConditions.AddIfNotNullOrEmpty(whereConditionsSQL);

        string relatedUserConditions = GetRelatedUserConditions(whereRelatedUserIs, relatedUser);

        if (!relatedUserConditions.IsNullOrEmpty()) whereConditions.Add(relatedUserConditions);

        string sql = $@"

          SELECT {sqlTop}

            COUNT(*) OVER() AS TotalRows,

            j.JobId, j.JobUID, j.JobGUID, j.JobNumber, j.JobName, j.CompanyId,
            j.LeadConsultantUserId,
            lcu.FirstName AS LeadConsultantFirstName, lcu.LastName AS LeadConsultantLastName,
            j.ProjectCoordinatorUserId,
            -- j.ContactFirstName, j.ContactLastName, j.ContactEmail, j.ContactPhone,
            j.AbleProgramStartDateUtc, j.AbleProgramEndDateUtc,
            j.ProgramStatusId,
            j.CoachingText, j.ToolText, j.BookingPageInstructions, j.ParticipantFormEmailSentUtc,
            j.Partner_UserId, j.Partner_DeliveryPercentage, j.Partner_SalesDeliveryPercentage, j.Partner_PLCPercentage,
            j.TotalRevenueChecksum, j.GSTPercent,
            pst.DisplaySortOrder AS ProgramStatusDisplayOrder,
            prj.ProjectId, prj.ParentOrgId, prj.ProjectName, prj.CreatedByUserId AS ProjectCreatedByUserId,
            org.OrgOwnerUserId,
            c.CompanyName,
            clu.UserId AS ClientLead_UserId, clu.FirstName AS ClientLead_FirstName, clu.LastName AS ClientLead_LastName,
            pax.ParticipantCount,
            ac.HasCoachingCount, ac.HasCoachingCompletedCount, ac.HasCoachCount,
            ac.OwnCoacheeCount, ac.OwnCoacheeCompletedCount,
            we.WorkshopCount, we.AssignedWorkshopCount, we.OwnWorkshopCount, we.CoFacWorkshopCount,
            we.WorkshopsCompleted, we.OwnWorkshopsCompleted,
            ci.ConsultingCount, ci.AssignedConsultingCount, ci.OwnConsultingCount,
            pplc.ProjPLCCount,
            pcids.ProgramCoachIds,
            IIF (EXISTS (SELECT 1 FROM al_Component cmp WHERE cmp.LockedDateUtc IS NOT NULL AND cmp.ProgramJobId = j.JobId), 1, 0 ) AS HasLockedComponents,
            rev.TotalRevenueAmt, rev.CompletedRevenueAmt,

            {ProgramSummary.GetSelectColumns()}

          FROM id_Job j
          INNER JOIN al_ProgramStatus pst ON pst.ProgramStatusId = j.ProgramStatusId
          INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
          INNER JOIN sv_Organisation org ON org.OrgId = prj.ParentOrgId
          INNER JOIN sv_SurveyCompany c ON c.SvCompanyId = prj.SvCompanyId
          LEFT OUTER JOIN sv_User clu ON clu.UserId = c.ClientLeadUserId
          LEFT OUTER JOIN al_ProgramSummary ps ON ps.ProgramJobId = j.JobId
          LEFT OUTER JOIN sv_User lcu ON lcu.UserId = j.LeadConsultantUserId

          CROSS APPLY (
            SELECT
              COUNT(*) AS ParticipantCount
            FROM al_Coachees ac
            WHERE ac.ProgramJobId = j.JobId
              AND ac.DeletedUtc IS NULL
          ) AS pax

          CROSS APPLY (
            SELECT
              COUNT(*) AS HasCoachingCount,
              COUNT(IIF(ac.ProgramStatusId >= @ProgramStatusId_Complete, 1, NULL)) AS HasCoachingCompletedCount,
              COUNT(IIF(ac.CoachUserId <> @UserId_Unassigned, 1, NULL)) AS HasCoachCount,
              COUNT(IIF(ac.CoachUserId = {Param_OwnUser_UserId}, 1, NULL)) AS OwnCoacheeCount,
              COUNT(IIF(ac.CoachUserId = {Param_OwnUser_UserId} AND ac.ProgramStatusId >= @ProgramStatusId_Complete, 1, NULL)) AS OwnCoacheeCompletedCount
            FROM al_Coachees ac
            WHERE ac.ProgramJobId = j.JobId
              AND ac.CoachingTypeId <> @CoachingTypeId_NoCoaching
              AND ac.DeletedUtc IS NULL
          ) AS ac

          OUTER APPLY (
            SELECT
              ISNULL(SUM(IIF(we.WorkshopStatusId NOT IN (@WorkshopStatusId_NotPlanned, @WorkshopStatusId_Cancelled), 1, 0)), 0) AS WorkshopCount,
              ISNULL(SUM(IIF(we.WorkshopStatusId = @WorkshopStatusId_Confirmed AND we.EndDateUtc < GETUTCDATE(), 1, 0)), 0) AS WorkshopsCompleted,
              ISNULL(COUNT(we.KeyFacilitatorUserId), 0) AS AssignedWorkshopCount,
              ISNULL(SUM(IIF(we.KeyFacilitatorUserId = {Param_OwnUser_UserId}, 1, 0)), 0) AS OwnWorkshopCount,
              ISNULL(SUM(IIF(we.KeyFacilitatorUserId = {Param_OwnUser_UserId} AND we.EndDateUtc < GETUTCDATE(), 1, 0)), 0) AS OwnWorkshopsCompleted,
              ISNULL(SUM(IIF(we.CoFacilitatorUserId = {Param_OwnUser_UserId}, 1, 0)), 0) AS CoFacWorkshopCount
            FROM ev_WorkshopEvent we
            WHERE we.ProgramJobId = j.JobId
          ) AS we

          OUTER APPLY (
            SELECT
              ISNULL(COUNT(ci.ConsultingItemId), 0) AS ConsultingCount,
              ISNULL(COUNT(ci.ConsultantUserId), 0) AS AssignedConsultingCount,
              ISNULL(SUM(IIF(ci.ConsultantUserId = {Param_OwnUser_UserId}, 1, 0)), 0) AS OwnConsultingCount
            FROM al_ConsultingItems ci
            WHERE ci.ProgramJobId = j.JobId
          ) AS ci

          /* Count Programs in Project where user is PLC */
          CROSS APPLY (
            SELECT COUNT(*) AS ProjPLCCount
            FROM id_Job jplc
            WHERE jplc.JobNumber = j.JobNumber
            AND jplc.LeadConsultantUserId = {Param_OwnUser_UserId}
            AND {Param_OwnUser_UserId} IS NOT NULL
          ) AS pplc

          /* List of CoachUserIds in Program */
          CROSS APPLY (
            SELECT (STUFF((
              SELECT DISTINCT ',' + CAST(ac2.CoachUserId AS VARCHAR(20))
              FROM al_Coachees ac2
              WHERE ac2.ProgramJobId = j.JobId
                AND ac2.CoachUserId IS NOT NULL
                AND ac2.DeletedUtc IS NULL
              FOR XML PATH (''), TYPE, ROOT).value('root[1]', 'nvarchar(max)'), 1, 1, '')) as ProgramCoachIds
          ) AS pcids

          /* Revenue TODO remove, replace with al_ProramSummary */
          CROSS APPLY (
            SELECT
              ISNULL(SUM(cmp.ComponentPrice), 0) AS TotalRevenueAmt,
              ISNULL(SUM(IIF(cmp.CompletedDateUtc < GETUTCDATE() AND w.WorkshopStatusId NOT IN (@WorkshopStatusId_NotPlanned, @WorkshopStatusId_Cancelled), cmp.ComponentPrice, 0)),0) AS CompletedRevenueAmt
            FROM al_Component cmp
            LEFT JOIN ev_WorkshopEvent w ON w.WorkshopEventId = cmp.WorkshopEventId
            WHERE cmp.ProgramJobId = j.JobId
          ) AS rev

          {extraJoins}
          {whereConditions.Join(" AND ").EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        if (sqlTop.IsNullOrEmpty() && !sqlOrderBy.IsNullOrEmpty() && offsetRows >= 0 && fetchRows > 0) {
          programJobList.OffsetRows = offsetRows;
          programJobList.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        if (sqlWhereParams == null) sqlWhereParams = new List<SqlParameter>();
        sqlWhereParams.Add(DbExtensions.IfKeyExists.Skip, null,
          NewSqlParameter(Param_OwnUser_UserId, relatedUser?.UserId),
          NewSqlParameter(Param_OwnUser_OrgId, relatedUser?.OrgId),
          NewSqlParameter("CoachingTypeId_NoCoaching", AlbertCoachingTypes.GetType_NoCoaching().CoachingTypeId),
          NewSqlParameter("ProgramStatusId_Complete", AlbertProgramStatus.Ids.Complete),
          NewSqlParameter("UserId_Unassigned", ConfigHelper.UserId.Unassigned),
          NewSqlParameter("WorkshopStatusId_NotPlanned", WorkshopStatus.WorkshopStatus_NotPlanned.WorkshopStatusId),
          NewSqlParameter("WorkshopStatusId_Cancelled", WorkshopStatus.WorkshopStatus_Cancelled.WorkshopStatusId),
          NewSqlParameter("WorkshopStatusId_Confirmed", WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId)
        );

        Query(sql, sqlWhereParams, dr => {

          if (programJobList.TotalRows == 0) programJobList.TotalRows = dr.GetInt("TotalRows");

          var newProgram = new AbleProgramInfo(

            programJobId: dr.GetInt("JobId"),
            programJobUID: dr.GetString("JobUID"),
            programJobGUID: dr.GetGuidOrNull("JobGUID"),
            programJobNumber: dr.GetString("JobNumber"),
            programJobName: dr.GetString("JobName"),

            tenantOrgId: dr.GetInt("ParentOrgId"),
            tenantOrgOwnerUserId: dr.GetInt("OrgOwnerUserId"),

            projectId: dr.GetInt("ProjectId"),
            projectName: dr.GetString("ProjectName"),

            programStartDateUtc: dr.GetDateTimeOrNull("AbleProgramStartDateUtc"),
            programEndDateUtc: dr.GetDateTimeOrNull("AbleProgramEndDateUtc"),
            programExpectedRevenue: dr.GetDecimalOrNull("TotalRevenueChecksum"),
            gstPercent: dr.GetDecimalOrNull("GSTPercent"),

            programStatusId: dr.GetInt("ProgramStatusId"),
            programStatusDisplayOrder: dr.GetInt("ProgramStatusDisplayOrder"),

            projectCreatedByUserId: dr.GetIntOrNull("ProjectCreatedByUserId"),
            projectCoordinatorUserId: dr.GetIntOrNull("ProjectCoordinatorUserId"),

            leadConsultantUserId: dr.GetIntOrNull("LeadConsultantUserId"),
            leadConsultantFirstName: dr.GetString("LeadConsultantFirstName"),
            leadConsultantLastName: dr.GetString("LeadConsultantLastName"),

            salesPartnerUserId: dr.GetIntOrNull("Partner_UserId"),
            partner_DeliveryPercentage: dr.GetDecimalOrNull("Partner_DeliveryPercentage"),
            partner_SalesDeliveryPercentage: dr.GetDecimalOrNull("Partner_SalesDeliveryPercentage"),
            partner_PLCPercentage: dr.GetDecimalOrNull("Partner_PLCPercentage"),

            companyId: dr.GetIntOrNull("CompanyId"),
            companyName: dr.GetString("CompanyName"),

            clientLead_UserId: dr.GetIntOrNull("ClientLead_UserId"),
            clientLead_FirstName: dr.GetString("ClientLead_FirstName"),
            clientLead_LastName: dr.GetString("ClientLead_LastName"),

            programNotes: dr.GetString("CoachingText"),
            toolNotes: dr.GetString("ToolText"),
            bookingPageInstructions: dr.GetString("BookingPageInstructions"),

            participantFormEmailSentUtc: dr.GetDateTimeOrNull("ParticipantFormEmailSentUtc"),

            participantCount: dr.GetInt("ParticipantCount"),
            hasCoachingCount: dr.GetInt("HasCoachingCount"),
            hasCoachingCompletedCount: dr.GetInt("HasCoachingCompletedCount"),
            hasCoachCount: dr.GetInt("HasCoachCount"),

            workshopCount: dr.GetInt("WorkshopCount"),
            workshopsCompleted: dr.GetInt("WorkshopsCompleted"),
            assignedWorkshopCount: dr.GetInt("AssignedWorkshopCount"),

            consultingCount: dr.GetInt("ConsultingCount"),
            assignedConsultingCount: dr.GetInt("AssignedConsultingCount"),

            ownCountUserId: relatedUser?.UserId,
            ownCoacheeCount: dr.GetInt("OwnCoacheeCount"),
            ownCoacheeCompletedCount: dr.GetInt("OwnCoacheeCompletedCount"),
            ownWorkshopCount: dr.GetInt("OwnWorkshopCount"),
            ownWorkshopsCompleted: dr.GetInt("OwnWorkshopsCompleted"),
            coFacWorkshopCount: dr.GetInt("CoFacWorkshopCount"),
            ownConsultingCount: dr.GetInt("OwnConsultingCount"),

            assignedPLCCount: dr.GetInt("ProjPLCCount"),
            programCoachIds: dr.GetString("ProgramCoachIds").ToIntList(),
            hasLockedComponents: dr.GetBoolFromInt("HasLockedComponents"),
            totalRevenueAmt: dr.GetDecimal("TotalRevenueAmt"),
            completedRevenueAmt: dr.GetDecimal("CompletedRevenueAmt")
          ) {
            Summary = new ProgramSummary.SummaryData(dr)
          };

          if (ConfigHelper.IsDevServer) newProgram.SqlText = sql;
          programJobList.ProgramInfoList.Add(newProgram);
        });

        if (ConfigHelper.IsDevServer) programJobList.SqlText = LogHelper.GetLatestSqlQueryText();

        return programJobList;
      }

      private static string GetRelatedUserConditions(WhereRelatedUserIs whereRelatedUserIs, AbleUser.UserIdentity userToFindInProgram) {

        if (whereRelatedUserIs == WhereRelatedUserIs.NoCheck) return string.Empty;

        if (userToFindInProgram == null) throw new ArgumentException($"{nameof(userToFindInProgram)} cannot be null when {nameof(whereRelatedUserIs)} is set.");

        string userInProgramConditions = "(";

        if (whereRelatedUserIs == WhereRelatedUserIs.Tenant
          || whereRelatedUserIs == WhereRelatedUserIs.Tenant_PLC_PC
          || whereRelatedUserIs == WhereRelatedUserIs.Tenant_AnyRelated) {

          // Include Tenant owner and admin.
          userInProgramConditions += $@" org.OrgOwnerUserId = {Param_OwnUser_UserId}";
          if (userToFindInProgram.IsTenantOrgAdmin) {
            userInProgramConditions += $@" OR org.OrgId = {Param_OwnUser_OrgId}";
          }
        }

        if (whereRelatedUserIs == WhereRelatedUserIs.Tenant_PLC_PC
          || whereRelatedUserIs == WhereRelatedUserIs.Tenant_AnyRelated) {

          // Include PLC & PC
          userInProgramConditions += $@"
            OR j.LeadConsultantUserId = {Param_OwnUser_UserId}
            OR j.ProjectCoordinatorUserId = {Param_OwnUser_UserId}";
        }

        if (whereRelatedUserIs == WhereRelatedUserIs.Tenant_AnyRelated) {

          // Include other relationships in program.
          userInProgramConditions += $@"
            OR EXISTS (
              SELECT 1 FROM al_Coachees ac
              WHERE ac.ProgramJobId = j.JobId
                AND ac.CoachUserId = {Param_OwnUser_UserId}
                AND ac.DeletedUtc IS NULL
            )
            OR EXISTS (
              SELECT 1 FROM ev_WorkshopEvent we
              WHERE we.ProgramJobId = j.JobId
                AND ( we.KeyFacilitatorUserId = {Param_OwnUser_UserId}
                      OR we.CoFacilitatorUserId = {Param_OwnUser_UserId} )
            )
            OR EXISTS (
              SELECT 1 FROM al_ConsultingItems ci
              WHERE ci.ProgramJobId = j.JobId
                AND ci.ConsultantUserId = {Param_OwnUser_UserId}
            )
            OR EXISTS (
              SELECT 1 FROM al_UserProjectAccess prjac
              WHERE prjac.ProgramJobId = j.JobId
                AND prjac.UserId = {Param_OwnUser_UserId}
            )";
        }

        userInProgramConditions += ")";

        return userInProgramConditions;
      }

      public static bool UserHasAnyPrograms(AbleUser.UserIdentity user, ConfigHelper.UserRole userRole) {

        if (user == null) {
          throw new Exception("userInfo required");
        }

        if (userRole == ConfigHelper.UserRole.Admin) return true;

        return GetScalarQueryInt($@"
          IF EXISTS (
            SELECT 1
            FROM id_Job j
            INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
            INNER JOIN sv_Organisation org ON org.OrgId = prj.ParentOrgId
            WHERE {GetRelatedUserConditions(WhereRelatedUserIs.Tenant_AnyRelated, user)}
          )
          BEGIN
            SELECT 1
          END
          ELSE
          BEGIN
            SELECT 0
          END",
          NewSqlParameter(Param_OwnUser_UserId, user?.UserId),
          NewSqlParameter(Param_OwnUser_OrgId, user?.OrgId)
        ) > 0;
      }


      public static List<int> GetCoacheesInProgram(int programJobId) {

        var programCoachees = new List<int>();

        Query(@"
          SELECT CoacheeId
          FROM al_Coachees
          WHERE ProgramJobId = @ProgramJobId
            AND DeletedUtc IS NULL",
          dr => {
            programCoachees.Add(dr.GetInt("CoacheeId"));
          },
          NewSqlParameter("@ProgramJobId", programJobId)
        );

        return programCoachees;
      }

      public static List<int> GetCoachesInProgram(int programJobId) {

        var programCoaches = new List<int>();

        Query(@"
          SELECT DISTINCT CoachUserId
          FROM al_Coachees
          WHERE ProgramJobId = @ProgramJobId
            AND CoachUserId IS NOT NULL
            AND DeletedUtc IS NULL",
          dr => {
            programCoaches.Add(dr.GetInt("CoacheeId"));
          },
          NewSqlParameter("@ProgramJobId", programJobId)
        );

        return programCoaches;
      }

      public static AbleProgramInfo CreateProgram(
        SqlTransaction trans,
        int? companyId,
        string programJobNumber, string programName,
        DateTime? programStartDateUtc, DateTime? programEndDateUtc,
        string programNotes) {

        var newProgramInfo = new AbleProgramInfo() {
          ProgramJobNumber = programJobNumber,
          ProgramJobName = programName,
          ProgramStartDateUtc = programStartDateUtc,
          ProgramEndDateUtc = programEndDateUtc,
          CompanyId = companyId,
          ProgramNotes = programNotes
        };

        CreateProgram(trans, newProgramInfo);

        return newProgramInfo;
      }

      public static void CreateProgram(AbleProgramInfo newProgramInfo) {
        CreateProgram(null, newProgramInfo);
      }

      public static void CreateProgram(SqlTransaction trans, AbleProgramInfo newProgramInfo) {

        string newJobUID = "";
        int tries;

        string sql = @"
          INSERT INTO id_Job (
            JobNumber, JobUID, JobName, TotalRevenueChecksum, GSTPercent,
            AbleProgramStartDateUtc, AbleProgramEndDateUtc, ProgramStatusId,
            ProjectCoordinatorUserId, LeadConsultantUserId,
            Partner_UserId, Partner_DeliveryPercentage, Partner_SalesDeliveryPercentage, Partner_PLCPercentage,
            CompanyId,
            IsAbleProgram, CoachingText, ToolText, BookingPageInstructions)
          OUTPUT INSERTED.JobId, INSERTED.JobGUID
          VALUES (
            @JobNumber, @JobUID, @JobName, @TotalRevenueChecksum, @GSTPercent,
            @AbleProgramStartDateUtc, @AbleProgramEndDateUtc, @ProgramStatusId,
            @ProjectCoordinatorUserId, @LeadConsultantUserId,
            @Partner_UserId, @Partner_DeliveryPercentage, @Partner_SalesDeliveryPercentage, @Partner_PLCPercentage,
            @CompanyId,
            @IsAbleProgram, @CoachingText, @ToolText, @BookingPageInstructions)";

        for (tries = 0; tries < UniqueId_Assign_MaxTries; tries++) {

          newJobUID = GetRandomUniqueId();

          try {
            Query(trans, sql,
              dr => {
                // Success. Return new db values in given object.
                newProgramInfo.ProgramJobUID = newJobUID;
                newProgramInfo.ProgramJobId = dr.GetInt("JobId");
                newProgramInfo.ProgramJobGUID = dr.GetGuid("JobGUID");
              },
              NewSqlParameter("@JobNumber", newProgramInfo.ProgramJobNumber.LimitLengthTo(20)),
              NewSqlParameter("@JobName", newProgramInfo.ProgramJobName.LimitLengthTo(100)),
              NewSqlParameter("@TotalRevenueChecksum", newProgramInfo.ProgramExpectedRevenue),
              NewSqlParameter("@GSTPercent", newProgramInfo.GSTPercent),
              NewSqlParameter("@AbleProgramStartDateUtc", newProgramInfo.ProgramStartDateUtc),
              NewSqlParameter("@AbleProgramEndDateUtc", newProgramInfo.ProgramEndDateUtc),
              NewSqlParameter("@ProgramStatusId", newProgramInfo.ProgramStatusId),
              NewSqlParameter("@ProjectCoordinatorUserId", newProgramInfo.ProjectCoordinatorUserId),
              NewSqlParameter("@LeadConsultantUserId", newProgramInfo.LeadConsultantUserId),
              NewSqlParameter("@Partner_UserId", newProgramInfo.SalesPartnerUserId),
              NewSqlParameter("@Partner_DeliveryPercentage", newProgramInfo.Partner_DeliveryPercentage),
              NewSqlParameter("@Partner_SalesDeliveryPercentage", newProgramInfo.Partner_SalesDeliveryPercentage),
              NewSqlParameter("@Partner_PLCPercentage", newProgramInfo.Partner_PLCPercentage),
              NewSqlParameter("@CompanyId", newProgramInfo.CompanyId),
              NewSqlParameter("@IsAbleProgram", true),
              NewSqlParameter("@CoachingText", newProgramInfo.ProgramNotes),
              NewSqlParameter("@ToolText", newProgramInfo.ToolNotes),
              NewSqlParameter("@BookingPageInstructions", newProgramInfo.BookingPageInstructions),
              NewSqlParameter("@JobUID", newJobUID)  // Added the missing parameter
            );
            return;

          } catch (Exception e) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(e)
              .WithOperation(nameof(CreateProgram))
              .WithOperationContext("GenerateUniqueJobUID")
              .WithProperty(DalApplicationInsightsConstants.ProgramJobNumber, newProgramInfo?.ProgramJobNumber)
              .WithProperty(DalApplicationInsightsConstants.ProgramJobName, newProgramInfo?.ProgramJobName)
              .WithProperty(DalApplicationInsightsConstants.CompanyId, newProgramInfo?.CompanyId)
              .WithProperty("db.ProjectCoordinatorUserId", newProgramInfo?.ProjectCoordinatorUserId)
              .WithProperty("db.LeadConsultantUserId", newProgramInfo?.LeadConsultantUserId)
              .WithProperty("db.SalesPartnerUserId", newProgramInfo?.SalesPartnerUserId)
              .WithProperty(DalApplicationInsightsConstants.AttemptNumber, tries)
              .WithProperty(DalApplicationInsightsConstants.IsUniqueKeyError, e.Message.ToLower().Contains("unique"))
              .Track();

            if (!e.Message.ToLower().Contains("unique")) {
              // Not a "violation of unique key" error, must be something serious.
              throw;
            }
            // Continue the loop to try again with a new UID
          }
        }

        // Shouldn't normally arrive here. If so, we may need to increase UID generation attempts, or use a different method of creating UID strings.
        throw new ApplicationException("CreateProgram(): Maxed out UniqueId attempts.");
      }

      public static void UpdateProgram(AbleProgramInfo programInfo) {

        if (programInfo == null) throw new ArgumentException("programInfo is null.");

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          string sql = @"
            UPDATE id_Job SET
              JobNumber = @JobNumber,
              JobName = @JobName,
              AbleProgramStartDateUtc = @AbleProgramStartDateUtc,
              AbleProgramEndDateUtc = @AbleProgramEndDateUtc,
              TotalRevenueChecksum = @TotalRevenueChecksum,
              GSTPercent = @GSTPercent,
              ProgramStatusId = @ProgramStatusId,
              ProjectCoordinatorUserId = @ProjectCoordinatorUserId,
              LeadConsultantUserId = @LeadConsultantUserId,
              Partner_UserId = @Partner_UserId,
              Partner_DeliveryPercentage = @Partner_DeliveryPercentage,
              Partner_SalesDeliveryPercentage = @Partner_SalesDeliveryPercentage,
              Partner_PLCPercentage = @Partner_PLCPercentage,
              CompanyId = @CompanyId,
              -- ContactFirstName = @ContactFirstName,
              -- ContactLastName = @ContactLastName,
              -- ContactEmail = @ContactEmail,
              -- ContactPhone = @ContactPhone,
              CoachingText = @CoachingText,
              ToolText = @ToolText,
              BookingPageInstructions = @BookingPageInstructions
            WHERE JobId = @JobId";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@JobId", programInfo.ProgramJobId);
            cmd.Parameters.AddVarChar("@JobNumber", 100, programInfo.ProgramJobNumber.LimitLengthTo(20));
            cmd.Parameters.AddVarChar("@JobName", 100, programInfo.ProgramJobName.LimitLengthTo(100));
            cmd.Parameters.AddDateTime("@AbleProgramStartDateUtc", programInfo.ProgramStartDateUtc);
            cmd.Parameters.AddDateTime("@AbleProgramEndDateUtc", programInfo.ProgramEndDateUtc);
            cmd.Parameters.AddDecimal("@TotalRevenueChecksum", programInfo.ProgramExpectedRevenue);
            cmd.Parameters.AddInt("@ProgramStatusId", programInfo.ProgramStatusId);
            cmd.Parameters.AddDecimal("@GSTPercent", programInfo.GSTPercent);
            cmd.Parameters.AddInt("@ProjectCoordinatorUserId", programInfo.ProjectCoordinatorUserId);
            cmd.Parameters.AddInt("@LeadConsultantUserId", programInfo.LeadConsultantUserId);
            cmd.Parameters.AddInt("@Partner_UserId", programInfo.SalesPartnerUserId);
            cmd.Parameters.AddDecimal("@Partner_DeliveryPercentage", programInfo.Partner_DeliveryPercentage);
            cmd.Parameters.AddDecimal("@Partner_SalesDeliveryPercentage", programInfo.Partner_SalesDeliveryPercentage);
            cmd.Parameters.AddDecimal("@Partner_PLCPercentage", programInfo.Partner_PLCPercentage);
            cmd.Parameters.AddInt("@CompanyId", programInfo.CompanyId);
            // cmd.Parameters.AddVarChar("@ContactFirstName", 100, programInfo.ContactFirstName.LimitLengthTo(100));
            // cmd.Parameters.AddVarChar("@ContactLastName", 100, programInfo.ContactLastName.LimitLengthTo(100));
            // cmd.Parameters.AddVarChar("@ContactEmail", 100, programInfo.ContactEmail.LimitLengthTo(100));
            // cmd.Parameters.AddVarChar("@ContactPhone", 100, programInfo.ContactPhone.LimitLengthTo(100));
            cmd.Parameters.AddVarCharMax("@CoachingText", programInfo.ProgramNotes);
            cmd.Parameters.AddVarCharMax("@ToolText", programInfo.ToolNotes);
            cmd.Parameters.AddVarCharMax("@BookingPageInstructions", programInfo.BookingPageInstructions);
            conn.Open();
            cmd.ExecuteNonQuery();
          }
        }
      }

      public static bool UpdateProgramStatus(int programJobId, AlbertProgramStatus.ProgramStatusInfo programStatus) {

        return GetNonQueryInt(@"

          UPDATE id_Job
          SET ProgramStatusId = @ProgramStatusId,
              LastUpdatedUtc = SYSUTCDATETIME()
          WHERE JobId = @JobId",

          NewSqlParameter("JobId", programJobId),
          NewSqlParameter("ProgramStatusId", programStatus.ProgramStatusId)

        ) > 0;
      }

      // Ensure all Able programs have same company ID as the parent Project.
      // This should be run after every Project update or Program update,
      // or can be run any time to fix any mismatches.
      public static void MaintainCompanyIds() {

        GetNonQueryInt(@"
          UPDATE j
          SET j.CompanyId = p.SvCompanyId
          FROM id_Job j
          INNER JOIN al_Project p ON j.JobNumber = p.JobNumber
          WHERE j.JobNumber <> ''
            AND j.IsAbleProgram = 1
            AND j.CompanyId <> p.SvCompanyId");
      }

      public static bool UpdateProgramComplete(int programJobId, bool setEndDate) {

        return GetNonQueryInt($@"

          UPDATE id_Job
          SET ProgramStatusId = @ProgramStatusId,
              {(setEndDate ? "AbleProgramEndDateUtc = @AbleProgramEndDateUtc," : string.Empty)}
              LastUpdatedUtc = SYSUTCDATETIME()
          WHERE JobId = @JobId",

          NewSqlParameter("JobId", programJobId),
          NewSqlParameter("ProgramStatusId", AlbertProgramStatus.Ids.Complete),
          NewSqlParameter("AbleProgramEndDateUtc", DateTime.UtcNow)

        ) > 0;
      }

      public static void UpdateParticipantFormEmailSent(int programJobId, DateTime? participantFormEmailSentUtc) {

        GetNonQueryInt(@"

          UPDATE id_Job
          SET ParticipantFormEmailSentUtc = @ParticipantFormEmailSentUtc
          WHERE JobId = @JobId",

          NewSqlParameter("JobId", programJobId),
          NewSqlParameter("@ParticipantFormEmailSentUtc", participantFormEmailSentUtc)

        );
      }

      public static bool UpdateProgramStartedEmailSent(SqlTransaction trans, int programJobId) {

        return GetNonQueryInt(trans, $@"

          UPDATE id_Job
          SET ProgramStartedEmailSentUtc = @ProgramStartedEmailSentDate
          WHERE JobId = @JobId",

         NewSqlParameter("JobId", programJobId),
         NewSqlParameter("ProgramStartedEmailSentDate", DateTime.UtcNow)
        ) == 1;
      }

      // FIX: Called
      public static void DeleteProgram(int programJobId) {

        string sql = @"

          BEGIN TRANSACTION

          -- Only delete if no locked components (or no components at all) exist.
          IF NOT EXISTS (SELECT 1 FROM al_Component WHERE ProgramJobId = @JobId AND LockedDateUtc IS NOT NULL)
          AND EXISTS(SELECT 1 FROM id_Job WHERE JobId = @JobId)
          BEGIN

            UPDATE sv_360_Participants
            SET CoachJobId = NULL,
                AbleProgramJobId = NULL
            WHERE CoachJobId = @JobId
               OR AbleProgramJobId = @JobId;

            DELETE FROM sv_JoinJobsAndIntakes WHERE JobId = @JobId;
            DELETE FROM al_Coachees WHERE ProgramJobId = @JobId AND DeletedUtc IS NOT NULL;
            DELETE FROM al_UserProjectAccess WHERE ProgramJobId = @JobId;
            DELETE FROM al_ProgramSummary WHERE ProgramJobId = @JobId;
            DELETE FROM id_Job WHERE JobId = @JobId;

          END;

          COMMIT TRANSACTION";

        GetNonQueryInt(sql,
          NewSqlParameter("JobId", programJobId)
        );
      }

      public static void JoinProgramToIntake(int programJobId, int intakeCodeId) {
        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          conn.Open();
          JoinProgramToIntake(conn, null, programJobId, intakeCodeId);
        }
      }

      public static void JoinProgramToIntake(SqlConnection conn, SqlTransaction trans, int programJobId, int intakeCodeId) {

        string sql = @"
          MERGE sv_JoinJobsAndIntakes AS ji
          USING (
            SELECT @NewJobId AS NewJobId, @NewDateCodeId AS NewDateCodeId
          ) AS j
          ON  ji.JobId = j.NewJobId
          AND ji.DateCodeId = j.NewDateCodeId
          WHEN NOT MATCHED THEN
            INSERT (JobId, DateCodeId)
            VALUES (j.NewJobId, j.NewDateCodeId);";

        using (var cmd = new SqlCommand(sql, conn, trans)) {
          cmd.Parameters.AddInt("@NewJobId", programJobId);
          cmd.Parameters.AddInt("@NewDateCodeId", intakeCodeId);
          cmd.ExecuteNonQuery();
        }
      }

      public static string GetValidUniqueId(string UIDtoCheck) {
        // Return input if it is a valid UID otherwise return empty string.
        UIDtoCheck = UIDtoCheck.EmptyIfNull().TrimWhitespace();
        if (IsValidUniqueId(UIDtoCheck)) return UIDtoCheck;
        else return "";
      }

      private static bool IsValidUniqueId(string UIDToCheck) {
        var isMatch = Regex.IsMatch(UIDToCheck, "^" + UniqueIdValidRegex + "$");
        return isMatch;
      }

      private static string GetRandomUniqueId() {
        // Note always use on use of Random() - as of Apr 2019, multiple raters can be created at once,
        // in a loop, sometimes leading to new Random() obtainnig the same seed more than once in a row.
        // So from now always use AppHelper.GetRandomInstance() to ensure different seeds each time.
        var rnd = AppHelper.GetRandomInstance();
        string uniqueId = "";
        int length = rnd.Next(UniqueIdMinLength, UniqueIdMaxLength);
        for (int i = 0; i < length; i++) {
          int charPos = rnd.Next(0, UniqueIdValidChars.Length - 1);
          uniqueId += UniqueIdValidChars.Substring(charPos, 1);
        }
        return uniqueId;
      }

      public static List<ProgramTeamMember> GetProgramTeamMembers(int programId) {

        var users = new List<ProgramTeamMember>();

        const string role_PLC = "plc";
        const string role_PC = "pc";
        const string role_Coach = "coach";
        const string role_Facilitator = "facilitator";

        Query(@"
          WITH role
          AS
          (
            SELECT ij.LeadConsultantUserId AS UserId, ij.JobId AS ProgramJobId, @Role_PLC AS Role
            FROM id_Job ij
            UNION
            SELECT ij.ProjectCoordinatorUserId AS UserId, ij.JobId AS ProgramJobId, @Role_PC AS Role
            FROM id_Job ij
            UNION
            SELECT ac.CoachUserId AS UserId, ProgramJobId, @Role_Coach AS Role
            FROM al_Coachees ac
            UNION
            SELECT we.KeyFacilitatorUserId AS UserId, ProgramJobId, @Role_Facilitator AS Role
            FROM ev_WorkshopEvent we
            UNION
            SELECT we.CoFacilitatorUserId AS UserId, ProgramJobId, @Role_Facilitator AS Role
            FROM ev_WorkshopEvent we
          )
          SELECT
            su.UserId, su.FirstName, su.LastName, su.Email, su.Mobile, su.State, su.Country,
            role.Role
          FROM sv_User su
          INNER JOIN role ON role.UserId = su.UserId
          WHERE role.ProgramJobId = @ProgramId
            AND role.UserId <> @UnassignedUserId
            AND role.UserId <> @PandLUserId
            AND role.UserId <> @LostUserId
          ORDER BY su.FirstName, su.UserId, role.Role;
          ",
          dr => {
            int userId = dr.GetInt("UserId");
            var user = users.Find(u => u.UserId == userId);
            if (user == null) {
              user = new ProgramTeamMember(
                userId: dr.GetInt("UserId"),
                firstName: dr.GetString("FirstName"),
                lastName: dr.GetString("LastName"),
                email: dr.GetString("Email"),
                mobileNumber: dr.GetString("Mobile"),
                state: dr.GetString("State"),
                country: dr.GetString("Country")
              );
              users.Add(user);
            }
            switch (dr.GetString("Role")) {
              case role_PLC: user.IsLeadConsultant = true; break;
              case role_PC: user.IsProjectCoordinator = true; break;
              case role_Coach: user.IsCoach = true; break;
              case role_Facilitator: user.IsFacilitator = true; break;
            }
          },
          NewSqlParameter("@ProgramId", programId),
          NewSqlParameter("@UnassignedUserId", ConfigHelper.UserId.Unassigned),
          NewSqlParameter("@PandLUserId", ConfigHelper.UserId.RevenuePandL),
          NewSqlParameter("@LostUserId", ConfigHelper.UserId.RevenueLost),
          NewSqlParameter("@Role_PLC", role_PLC),
          NewSqlParameter("@Role_PC", role_PC),
          NewSqlParameter("@Role_Coach", role_Coach),
          NewSqlParameter("@Role_Facilitator", role_Facilitator)
        );
        return users;
      }

      public static ProgramOverviewEvalScores GetProgramOverviewEvalScores(int programJobId) {

        var scores = new ProgramOverviewEvalScores();

        var coachingCondition = Reports.EvalViewer.GetSQLConditionsForReport(ConfigHelper.EvalType.Coaching);
        var workshopCondition = Reports.EvalViewer.GetSQLConditionsForReport(ConfigHelper.EvalType.Workshop);
        var programCondition = Reports.EvalViewer.GetSQLConditionsForReport(ConfigHelper.EvalType.PostProgram);

        Query($@"
          SELECT
            SUM(sc.ValueScore) AS ScoreSum_Overall,
            COUNT(sc.ValueScore) AS ScoreCount_Overall,
            COUNT(DISTINCT sp.PartId) AS PartCount_Overall,

            SUM(IIF(sv.WorkshopEvalFirstOrLast = 1, sc.ValueScore, NULL)) AS ScoreSum_FirstEval,
            COUNT(IIF(sv.WorkshopEvalFirstOrLast = 1, sc.ValueScore, NULL)) AS ScoreCount_FirstEval,
            COUNT(DISTINCT IIF(sv.WorkshopEvalFirstOrLast = 1, sp.PartId, NULL)) AS PartCount_FirstEval,

            SUM(IIF({coachingCondition.SurveyAndHeadingConditionSQL}, sc.ValueScore, NULL)) AS ScoreSum_Coaching,
            COUNT(IIF({coachingCondition.SurveyAndHeadingConditionSQL}, sc.ValueScore, NULL)) AS ScoreCount_Coaching,
            COUNT(DISTINCT IIF({coachingCondition.SurveyConditionSQL}, sp.PartId, NULL)) AS PartCount_Coaching,

            SUM(IIF({workshopCondition.SurveyAndHeadingConditionSQL}, sc.ValueScore, NULL)) AS ScoreSum_Workshops,
            COUNT(IIF({workshopCondition.SurveyAndHeadingConditionSQL}, sc.ValueScore, NULL)) AS ScoreCount_Workshops,
            COUNT(DISTINCT IIF({workshopCondition.SurveyConditionSQL}, sp.PartId, NULL)) AS PartCount_Workshops,

            SUM(IIF({programCondition.SurveyAndHeadingConditionSQL}, sc.ValueScore, NULL)) AS ScoreSum_EndProgram,
            COUNT(IIF({programCondition.SurveyAndHeadingConditionSQL}, sc.ValueScore, NULL)) AS ScoreCount_EndProgram,
            COUNT(DISTINCT IIF({programCondition.SurveyConditionSQL}, sp.PartId, NULL)) AS PartCount_EndProgram

          FROM sv_Survey sv
          INNER JOIN sv_360_Participants sp ON sp.SurveyId = sv.sv_id
          INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
          INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = sq.GblQuestionId
          WHERE sp.AbleProgramJobId = @ProgramJobId
            AND sp.Completed IS NOT NULL
            AND sq.GblAnswerTypeId = @EvalGblAnswerTypeId",
          dr => {
            scores.Overall = (dr.GetDecimalOrDefault("ScoreSum_Overall", 0) / Math.Max(dr.GetDecimalOrDefault("ScoreCount_Overall", 0), 1)).Round(1, MidpointRounding.AwayFromZero);
            scores.OverallCount = dr.GetIntOrDefault("PartCount_Overall", 0);
            scores.FirstEval = (dr.GetDecimalOrDefault("ScoreSum_FirstEval", 0) / Math.Max(dr.GetDecimalOrDefault("ScoreCount_FirstEval", 0), 1)).Round(1, MidpointRounding.AwayFromZero);
            scores.FirstEvalCount = dr.GetIntOrDefault("PartCount_FirstEval", 0);
            scores.EndProgram = (dr.GetDecimalOrDefault("ScoreSum_EndProgram", 0) / Math.Max(dr.GetDecimalOrDefault("ScoreCount_EndProgram", 0), 1)).Round(1, MidpointRounding.AwayFromZero);
            scores.EndProgramCount = dr.GetIntOrDefault("PartCount_EndProgram", 0);
            scores.Workshops = (dr.GetDecimalOrDefault("ScoreSum_Workshops", 0) / Math.Max(dr.GetDecimalOrDefault("ScoreCount_Workshops", 0), 1)).Round(1, MidpointRounding.AwayFromZero);
            scores.WorkshopsCount = dr.GetIntOrDefault("PartCount_Workshops", 0);
            scores.Coaching = (dr.GetDecimalOrDefault("ScoreSum_Coaching", 0) / Math.Max(dr.GetDecimalOrDefault("ScoreCount_Coaching", 0), 1)).Round(1, MidpointRounding.AwayFromZero);
            scores.CoachingCount = dr.GetIntOrDefault("PartCount_Coaching", 0);
          },
          NewSqlParameter("ProgramJobId", programJobId),
          NewSqlParameter("EvalGblAnswerTypeId", ConfigHelper.GblAnsTypeId_Eval)
        );
        return scores;
      }

      // Note that this overview pre-post result incldues both self AND raters in the result (i.e. no sp.isself check)
      public static ProgramOverviewPrePostScores GetProgramOverviewPrePostScores(int programJobId) {

        var result = new ProgramOverviewPrePostScores();

        Query($@"
          SELECT
            sp.PrePost360ForCoachee,
            SUM(sc.ValueScore) AS ScoreSum,
            COUNT(sc.ValueScore) AS ScoreCount
          FROM sv_360_Participants sp
          INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
          INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
          WHERE sp.AbleProgramJobId = @ProgramJobId
            AND {Reports.SkillsViewer.SQLWhereConditions.Completed360Skills("sv", "sp", "sq")}
            AND sp.PrePost360ForCoachee > 0
          GROUP BY sp.PrePost360ForCoachee;",
          dr => {
            decimal? scoreCount = dr.GetIntOrNull("ScoreCount");
            decimal? scoreSum = dr.GetIntOrNull("ScoreSum");
            if (scoreCount > 0 && scoreSum != null) {
              decimal score = (scoreSum.Value / scoreCount.Value).Round(4, MidpointRounding.AwayFromZero);
              int prePost360ForCoachee = dr.GetInt("PrePost360ForCoachee");
              if (prePost360ForCoachee == Participants.PrePostFlags.IsPreSurvey) {
                result.SurveyScorePre = score;
              } else if (prePost360ForCoachee == Participants.PrePostFlags.IsPostSurvey) {
                result.SurveyScorePost = score;
              }
            }
          },
          NewSqlParameter("ProgramJobId", programJobId),
          NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_SkillsViewer)
        );
        return result;
      }

      public static PrePostSurveyState GetPrePostSurveyState(int programJobId) {

        var state = new PrePostSurveyState();

        Query($@"
          SELECT sp.PrePost360ForCoachee,
            MAX(sc.ScheduledStartDateUtc) AS ScheduledStartDateUtc,
            MAX(sc.IntakeCloseDate) AS CloseDateWST,
            COUNT(*) AS SelfCount,
            COUNT(sp.Completed) AS CompletedCount
          FROM sv_360_Participants sp
          INNER JOIN sv_360_Codes sc ON sc.CodeId = sp.IntakeCodeId
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          WHERE sp.AbleProgramJobId = @ProgramJobId
            AND {AlbertSurveys.SQLWhereConditions.Standard360Surveys("sv")}
            AND sp.IsSelf = 1
            AND sp.PrePost360ForCoachee > 0
          GROUP BY sp.PrePost360ForCoachee
          ORDER BY sp.PrePost360ForCoachee",
          dr => {
            if (dr.GetInt("PrePost360ForCoachee") == Participants.PrePostFlags.IsPreSurvey) {
              state.PreSurveyScheduledUtc = dr.GetDateTimeOrNull("ScheduledStartDateUtc");
              state.PreSurveyCloseWST = dr.GetDateTimeOrNull("CloseDateWST");
              state.PreSurveyPartCount = dr.GetInt("SelfCount");
              state.PreSurveyPartCompleted = dr.GetInt("CompletedCount");
            } else if (dr.GetInt("PrePost360ForCoachee") == Participants.PrePostFlags.IsPostSurvey) {
              state.PostSurveyScheduledUtc = dr.GetDateTimeOrNull("ScheduledStartDateUtc");
              state.PostSurveyCloseWST = dr.GetDateTimeOrNull("CloseDateWST");
              state.PostSurveyPartCount = dr.GetInt("SelfCount");
              state.PostSurveyPartCompleted = dr.GetInt("CompletedCount");
            }
          },
          NewSqlParameter("ProgramJobId", programJobId)
        );

        return state;
      }

      public class PrePostSurveyState {

        public DateTime? PreSurveyScheduledUtc = null;
        public DateTime? PreSurveyCloseWST;
        public int PreSurveyPartCount;
        public int PreSurveyPartCompleted;
        public DateTime? PostSurveyScheduledUtc = null;
        public DateTime? PostSurveyCloseWST;
        public int PostSurveyPartCount;
        public int PostSurveyPartCompleted;

        public bool PreProgramSurveyExists => PreSurveyScheduledUtc != null || PreSurveyCloseWST != null;
        public bool PostProgramSurveyExists => PostSurveyScheduledUtc != null || PostSurveyCloseWST != null;

        public bool PreProgramSurveyComplete => PreSurveyCloseWST != null && PreSurveyCloseWST < DateTime.UtcNow && PreSurveyPartCompleted > 0;
        public bool PostProgramSurveyComplete => PostSurveyCloseWST != null && PostSurveyCloseWST < DateTime.UtcNow && PostSurveyPartCompleted > 0;
      }

      public class ProgramOverviewPrePostScores {
        public decimal? SurveyScorePre = null, SurveyScorePost = null;
      }

      public class ProgramOverviewEvalScores {
        public decimal Overall = 0, FirstEval = 0, EndProgram = 0, Workshops = 0, Coaching = 0;
        public int OverallCount = 0, FirstEvalCount = 0, EndProgramCount = 0, WorkshopsCount = 0, CoachingCount = 0;
      }

      public static ProgramNotesInfo GetProgramNotes(int programId) {

        var programNotes = new ProgramNotesInfo();

        Query($@"
          SELECT
            j.JobId, j.CoachingText as ProgramNotes, j.ProgramAISummary, prj.ProgramContext, prj.ProjectIntent
          FROM id_Job j
          LEFT OUTER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
          WHERE j.JobId = @ProgramJobId",
          dr => {
            programNotes.JobId = dr.GetInt("JobId");
            programNotes.ProgramNotes = dr.GetString("ProgramNotes");
            programNotes.ProgramAISummary = dr.GetString("ProgramAISummary");
            programNotes.ProgramContext = dr.GetString("ProgramContext");
            programNotes.ProjectIntent = dr.GetString("ProjectIntent");
          },
          NewSqlParameter("ProgramJobId", programId)
        );

        return programNotes;
      }

      public static List<ClientProgramSummary> GetClientProgramSummary(int clientUserId, int maxRows, out int totalRows) {

        var result = new List<ClientProgramSummary>();
        int _totalRows = 0;

        Query($@"

          SELECT TOP {maxRows}
            COUNT (*) OVER () AS TotalRows,
            ap.ProjectId, ap.JobNumber, ap.ProjectName,
            ij.JobId, ij.JobName, ij.ProgramStatusId, ij.AbleProgramStartDateUtc, ij.AbleProgramEndDateUtc,
            ac.Participants, ac.Coachees, ac.SessionsAllocated, ac.SessionsBooked, ac.SessionsCompleted, ac.SessionsUpcoming, ac.SessionsNextUpcomingUtc,
            we.Workshops, we.WorkshopsEarliestLocal, we.WorkshopsLatestLocal, we.WorkshopsNextUpcomingLocal, we.WorkshopsCompleted, we.WorkshopsUpcoming,
            eval.EvalScoreAvg,
            rev.TotalRevenueAmt, rev.CompletedRevenueAmt

          FROM al_UserProjectAccess upa
          INNER JOIN id_Job ij ON ij.JobId = upa.ProgramJobId
          INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber

          OUTER APPLY ( -- order by latest component
            SELECT MAX(COALESCE(cmp.LockedDateUtc, cmp.CompletedDateUtc, cmp.RowUpdatedUtc, cmp.RowCreatedUtc)) AS LatestUtc
            FROM al_Component cmp
            WHERE cmp.ProgramJobId = upa.ProgramJobId
          ) AS cmp

          OUTER APPLY ( -- total pax/coachees/sessions
            SELECT
              COUNT(*) AS Participants,
              COUNT(IIF(ac.SessionsAllocated > 0, 1, NULL)) AS Coachees,
              SUM(ac.SessionsAllocated) AS SessionsAllocated,
              SUM(ac.SessionsBooked) AS SessionsBooked,
              SUM(ac.SessionsCompleted) AS SessionsCompleted,
              SUM(ac.SessionsUpcoming) AS SessionsUpcoming,
              MIN(IIF(ac.NextApptDateUTC > GETUTCDATE(), ac.NextApptDateUTC, NULL)) AS SessionsNextUpcomingUtc
            FROM al_Coachees ac
            WHERE ac.ProgramJobId = upa.ProgramJobId
              AND ac.DeletedUtc IS NULL
              AND ac.ProgramStatusId IN (@CoacheeProgramStatus_Onboarding, @CoacheeProgramStatus_Active, @CoacheeProgramStatus_Paused)
          ) AS ac

          OUTER APPLY ( -- workshops
            SELECT
              COUNT(*) AS Workshops,
              MIN(we.StartDateLocal) AS WorkshopsEarliestLocal,
              MAX(we.EndDateLocal) AS WorkshopsLatestLocal,
              COUNT(IIF(we.StartDateUtc < GETUTCDATE(), 1, NULL)) AS WorkshopsCompleted,
              COUNT(IIF(we.StartDateUtc > GETUTCDATE(), 1, NULL)) AS WorkshopsUpcoming,
              MIN(IIF(we.StartDateLocal > GETDATE(), we.StartDateLocal, NULL)) AS WorkshopsNextUpcomingLocal
              FROM ev_WorkshopEvent we
            WHERE we.ProgramJobId = upa.ProgramJobId
              AND we.WorkshopStatusId IN (@WorkshopStatus_Confirmed, @WorkshopStatus_Estimated)
          ) AS we

          OUTER APPLY ( -- Overall eval score.
            SELECT
              AVG(sc.ValueScore) AS EvalScoreAvg
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
            INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
            INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
            INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
            WHERE sp.AbleProgramJobId = upa.ProgramJobId
              AND sp.Completed IS NOT NULL
              AND {AlbertSurveys.SQLWhereConditions.EvalSurveys("sv")}
              AND sq.GblAnswerTypeId = @GblAnswerTypeId_Eval
          ) AS eval

          CROSS APPLY ( -- Revenue
            SELECT
              ISNULL(SUM(cmp.ComponentPrice), 0) AS TotalRevenueAmt,
              ISNULL(SUM(IIF(cmp.CompletedDateUtc < GETUTCDATE(), cmp.ComponentPrice, 0)),0) AS CompletedRevenueAmt
            FROM al_Component cmp
            WHERE cmp.ProgramJobId = upa.ProgramJobId
          ) AS rev

          WHERE upa.UserId = @UserId

          ORDER BY
            IIF(ij.ProgramStatusId = @ProgramStatus_Active, 0, 1),
            cmp.LatestUtc DESC,
            ij.JobId DESC;",

          dr => {

            _totalRows = dr.GetInt("TotalRows");

            var newItem = new ClientProgramSummary(
              projectId: dr.GetInt("ProjectId"),
              jobNumber: dr.GetString("JobNumber"),
              projectName: dr.GetString("ProjectName"),
              programJobId: dr.GetInt("JobId"),
              programName: dr.GetString("JobName"),
              programStatusId: dr.GetInt("ProgramStatusId"),
              startDateUtc: dr.GetDateTimeOrNull("AbleProgramStartDateUtc"),
              endDateUtc: dr.GetDateTimeOrNull("AbleProgramEndDateUtc"),
              participants: dr.GetIntOrNull("Participants") ?? 0,
              coachees: dr.GetIntOrNull("Coachees") ?? 0,
              sessionsAllocated: dr.GetIntOrNull("SessionsAllocated") ?? 0,
              sessionsBooked: dr.GetIntOrNull("SessionsBooked") ?? 0,
              sessionsCompleted: dr.GetIntOrNull("SessionsCompleted") ?? 0,
              sessionsUpcoming: dr.GetIntOrNull("SessionsUpcoming") ?? 0,
              sessionsNextUpcomingUtc: dr.GetDateTimeOrNull("SessionsNextUpcomingUtc"),
              workshops: dr.GetIntOrNull("Workshops") ?? 0,
              workshopsEarliestLocal: dr.GetDateTimeOffsetOrNull("WorkshopsEarliestLocal").ToDateTimeOrNull(),
              workshopsLatestLocal: dr.GetDateTimeOffsetOrNull("WorkshopsLatestLocal").ToDateTimeOrNull(),
              workshopsNextUpcomingLocal: dr.GetDateTimeOffsetOrNull("WorkshopsNextUpcomingLocal").ToDateTimeOrNull(),
              workshopsCompleted: dr.GetIntOrNull("WorkshopsCompleted") ?? 0,
              workshopsUpcoming: dr.GetIntOrNull("WorkshopsUpcoming") ?? 0,
              evalScoreAvg: dr.GetDecimalOrNull("EvalScoreAvg"),
              totalRevenueAmt: dr.GetDecimalOrNull("TotalRevenueAmt") ?? 0,
              completedRevenueAmt: dr.GetDecimalOrNull("CompletedRevenueAmt") ?? 0
            );
            result.Add(newItem);
          },
          NewSqlParameter("UserId", clientUserId),
          NewSqlParameter("ProgramStatus_Active", AlbertProgramStatus.Ids.Active),
          NewSqlParameter("WorkshopStatus_Confirmed", WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId),
          NewSqlParameter("WorkshopStatus_Estimated", WorkshopStatus.WorkshopStatus_Estimated.WorkshopStatusId),
          NewSqlParameter("CoacheeProgramStatus_Onboarding", CoacheeProgramStatus.Ids.Onboarding),
          NewSqlParameter("CoacheeProgramStatus_Active", CoacheeProgramStatus.Ids.Active),
          NewSqlParameter("CoacheeProgramStatus_Paused", CoacheeProgramStatus.Ids.Paused),
          NewSqlParameter("GblAnswerTypeId_Eval", ConfigHelper.GblAnsTypeId_Eval)
        );

        totalRows = _totalRows;

        return result;
      }

      public class ProgramNotesInfo {
        public int JobId { get; set; }
        public string ProgramNotes { get; set; }
        public string ProgramAISummary { get; set; }
        public string ProgramContext { get; set; }
        public string ProjectIntent { get; set; }
        public ProgramNotesInfo() { }
        public ProgramNotesInfo(int jobId, string programNotes, string programAISummary, string programContext, string projectIntent) {
          JobId = jobId;
          ProgramNotes = programNotes;
          ProgramAISummary = programAISummary;
          ProgramContext = programContext;
          ProjectIntent = projectIntent;
        }
      }

      public class ProgramTeamMember {

        public int UserId { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }
        public string MobileNumber { get; private set; }
        public string State { get; private set; }
        public string Country { get; private set; }
        public bool IsLeadConsultant { get; internal set; }
        public bool IsProjectCoordinator { get; internal set; }
        public bool IsCoach { get; internal set; }
        public bool IsFacilitator { get; internal set; }

        public ProgramTeamMember(
          int userId,
          string firstName,
          string lastName,
          string email,
          string mobileNumber,
          string state,
          string country
        ) {
          UserId = userId;
          FirstName = firstName;
          LastName = lastName;
          Email = email;
          MobileNumber = mobileNumber;
          State = state;
          Country = country;
        }
      }

      public class AbleProgramList {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public string SqlText { get; internal set; }
        public List<AbleProgramInfo> ProgramInfoList { get; internal set; }
        public AbleProgramList() {
          OffsetRows = null;
          FetchRows = null;
          TotalRows = 0;
          ProgramInfoList = new List<AbleProgramInfo>();
        }
      }

      public class AbleProgramInfo {

        public string SqlText { get; internal set; }

        public int ProgramJobId { get; internal set; }
        public string ProgramJobUID { get; internal set; }
        public Guid? ProgramJobGUID { get; internal set; }

        public string ProgramJobNumber { get; set; }
        public string ProgramJobName { get; set; }

        public int TenantOrgId { get; internal set; }
        public int TenantOrgOwnerUserId { get; internal set; }

        public int ProjectId { get; internal set; }
        public string ProjectName { get; set; }

        public DateTime? ProgramStartDateUtc { get; set; }
        public DateTime? ProgramEndDateUtc { get; set; }
        public decimal? ProgramExpectedRevenue { get; set; }
        public decimal? GSTPercent { get; set; }

        public int ProgramStatusId { get; set; } = AlbertProgramStatus.Ids.Setup;
        public int ProgramStatusDisplayOrder { get; set; }

        public int? ProjectCreatedByUserId { get; private set; }
        public int? ProjectCoordinatorUserId { get; set; }

        public int? LeadConsultantUserId { get; set; }
        public string LeadConsultantFirstName { get; set; }
        public string LeadConsultantLastName { get; set; }

        public int? SalesPartnerUserId { get; set; }
        public decimal? Partner_DeliveryPercentage { get; set; }      // Note all percentages are stored as 0.00 (0%) to 1.00 (100%).
        public decimal? Partner_SalesDeliveryPercentage { get; set; }
        public decimal? Partner_PLCPercentage { get; set; }

        public int? CompanyId { get; set; }
        public string CompanyName { get; internal set; }

        public int? ClientLead_UserId { get; internal set; }
        public string ClientLead_FirstName { get; internal set; }
        public string ClientLead_LastName { get; internal set; }

        public string ProgramNotes { get; set; }
        public string ToolNotes { get; set; }
        public string BookingPageInstructions { get; set; }

        public DateTime? ParticipantFormEmailSentUtc { get; set; }

        public int? OwnCountUserId { get; internal set; } // UserId context for the "Own..." values.
        public int WorkshopCount { get; internal set; }
        public int WorkshopsCompleted { get; internal set; }
        public int AssignedWorkshopCount { get; internal set; }
        public int OwnWorkshopCount { get; internal set; }
        public int OwnWorkshopsCompleted { get; internal set; }
        public int CoFacWorkshopCount { get; internal set; }
        public int ConsultingCount { get; internal set; }
        public int AssignedConsultingCount { get; internal set; }
        public int OwnConsultingCount { get; internal set; }
        public int ParticipantCount { get; internal set; }
        public int HasCoachingCount { get; internal set; }
        public int HasCoachingCompletedCount { get; internal set; }
        public int HasCoachCount { get; internal set; }
        public int OwnCoacheeCount { get; internal set; }
        public int OwnCoacheeCompletedCount { get; internal set; }
        public int AssignedPLCCount { get; set; }
        public List<int> ProgramCoachIds { get; private set; } = new List<int>();
        public bool HasLockedComponents { get; internal set; } = false;
        public decimal TotalRevenueAmt { get; internal set; }
        public decimal CompletedRevenueAmt { get; internal set; }

        public ProgramSummary.SummaryData Summary { get; init; }

        public AbleProgramInfo() {
          this.GSTPercent = ConfigHelper.Financial.DefaultGSTPercent;
        }

        public AbleProgramInfo(

          int programJobId,
          string programJobUID,
          Guid? programJobGUID,

          string programJobNumber,
          string programJobName,

          int tenantOrgId,
          int tenantOrgOwnerUserId,

          int projectId,
          string projectName,

          DateTime? programStartDateUtc,
          DateTime? programEndDateUtc,
          decimal? programExpectedRevenue,
          decimal? gstPercent,

          int programStatusId,
          int programStatusDisplayOrder,

          int? projectCreatedByUserId,
          int? projectCoordinatorUserId,

          int? leadConsultantUserId,
          string leadConsultantFirstName,
          string leadConsultantLastName,

          int? salesPartnerUserId,
          decimal? partner_DeliveryPercentage,
          decimal? partner_SalesDeliveryPercentage,
          decimal? partner_PLCPercentage,

          int? companyId,
          string companyName,

          int? clientLead_UserId,
          string clientLead_FirstName,
          string clientLead_LastName,

          string programNotes,
          string toolNotes,
          string bookingPageInstructions,

          DateTime? participantFormEmailSentUtc,

          int participantCount,
          int hasCoachingCount,
          int hasCoachingCompletedCount,
          int hasCoachCount,

          int workshopCount,
          int workshopsCompleted,
          int assignedWorkshopCount,

          int consultingCount,
          int assignedConsultingCount,

          int? ownCountUserId,
          int ownCoacheeCount,
          int ownCoacheeCompletedCount,
          int ownWorkshopCount,
          int ownWorkshopsCompleted,
          int coFacWorkshopCount,
          int ownConsultingCount,

          int assignedPLCCount,
          List<int> programCoachIds,
          bool hasLockedComponents,
          decimal totalRevenueAmt,
          decimal completedRevenueAmt
        ) {

          this.ProgramJobId = programJobId;
          this.ProgramJobUID = programJobUID;
          this.ProgramJobGUID = programJobGUID;

          this.ProgramJobNumber = programJobNumber;
          this.ProgramJobName = programJobName;

          this.TenantOrgId = tenantOrgId;
          this.TenantOrgOwnerUserId = tenantOrgOwnerUserId;

          this.ProjectId = projectId;
          this.ProjectName = projectName;

          this.ProjectCreatedByUserId = projectCreatedByUserId;
          this.ProgramStartDateUtc = programStartDateUtc;
          this.ProgramEndDateUtc = programEndDateUtc;
          this.ProgramExpectedRevenue = programExpectedRevenue;
          this.GSTPercent = gstPercent;

          this.ProgramStatusId = programStatusId;
          this.ProgramStatusDisplayOrder = programStatusDisplayOrder;

          this.ProjectCoordinatorUserId = projectCoordinatorUserId;

          this.LeadConsultantUserId = leadConsultantUserId;
          this.LeadConsultantFirstName = leadConsultantFirstName;
          this.LeadConsultantLastName = leadConsultantLastName;

          this.SalesPartnerUserId = salesPartnerUserId;
          this.Partner_DeliveryPercentage = partner_DeliveryPercentage;
          this.Partner_SalesDeliveryPercentage = partner_SalesDeliveryPercentage;
          this.Partner_PLCPercentage = partner_PLCPercentage;

          this.CompanyId = companyId;
          this.CompanyName = companyName;

          this.ClientLead_UserId = clientLead_UserId;
          this.ClientLead_FirstName = clientLead_FirstName;
          this.ClientLead_LastName = clientLead_LastName;

          this.ProgramNotes = programNotes;
          this.ToolNotes = toolNotes;
          this.BookingPageInstructions = bookingPageInstructions;

          this.ParticipantFormEmailSentUtc = participantFormEmailSentUtc;
          this.OwnCountUserId = ownCountUserId;

          this.ParticipantCount = participantCount;   // All participants in the program.
          this.HasCoachingCount = hasCoachingCount;   // All participants who have coaching (coaching type not "no")
          this.HasCoachingCompletedCount = hasCoachingCompletedCount;
          this.HasCoachCount = hasCoachCount;         // All participants assigned to a) any coach (admin); b) viewing coach (partner).
          this.OwnCoacheeCount = ownCoacheeCount;     // All participants whose coach is the viewing user.
          this.OwnCoacheeCompletedCount = ownCoacheeCompletedCount;

          this.WorkshopCount = workshopCount;                   // All workshops.
          this.WorkshopsCompleted = workshopsCompleted;
          this.AssignedWorkshopCount = assignedWorkshopCount;   // Workshops with facilitator assigned to a user.
          this.OwnWorkshopCount = ownWorkshopCount;             // Workshops with facilitator assigned to the viewing user.
          this.OwnWorkshopsCompleted = ownWorkshopsCompleted;
          this.CoFacWorkshopCount = coFacWorkshopCount;         // Workshops with co-facilitator assigned to the viewing user.

          this.ConsultingCount = consultingCount;                 // All consulting items.
          this.AssignedConsultingCount = assignedConsultingCount; // Consulting items assigned to a user.
          this.OwnConsultingCount = ownConsultingCount;           // Consulting items assigned to the viewing user.

          this.AssignedPLCCount = assignedPLCCount; // Number of programs in the project where user is assigned PLC.
          this.ProgramCoachIds = programCoachIds;
          this.HasLockedComponents = hasLockedComponents;
          this.TotalRevenueAmt = totalRevenueAmt;
          this.CompletedRevenueAmt = completedRevenueAmt;
        }
      }

      public class ClientProgramSummary {

        public int ProjectId { get; private set; }
        public string JobNumber { get; private set; }
        public string ProjectName { get; private set; }
        public int ProgramJobId { get; private set; }
        public string ProgramName { get; private set; }
        public int ProgramStatusId { get; private set; }
        public DateTime? StartDateUtc { get; private set; }
        public DateTime? EndDateUtc { get; private set; }
        public int Participants { get; private set; }
        public int Coachees { get; private set; }
        public int SessionsAllocated { get; private set; }
        public int SessionsBooked { get; private set; }
        public int SessionsCompleted { get; private set; }
        public int SessionsUpcoming { get; private set; }
        public DateTime? SessionNextUpcomingUtc { get; private set; }
        public int Workshops { get; private set; }
        public DateTime? WorkshopsEarliestLocal { get; private set; }
        public DateTime? WorkshopsLatestLocal { get; private set; }
        public DateTime? WorkshopsNextUpcomingLocal { get; private set; }
        public int WorkshopsCompleted { get; private set; }
        public int WorkshopsUpcoming { get; private set; }
        public decimal? EvalScoreAvg { get; private set; }
        public decimal TotalRevenueAmt { get; internal set; }
        public decimal CompletedRevenueAmt { get; internal set; }

        public ClientProgramSummary(
          int projectId,
          string jobNumber,
          string projectName,
          int programJobId,
          string programName,
          int programStatusId,
          DateTime? startDateUtc,
          DateTime? endDateUtc,
          int participants,
          int coachees,
          int sessionsAllocated,
          int sessionsBooked,
          int sessionsCompleted,
          int sessionsUpcoming,
          DateTime? sessionsNextUpcomingUtc,
          int workshops,
          DateTime? workshopsEarliestLocal,
          DateTime? workshopsLatestLocal,
          DateTime? workshopsNextUpcomingLocal,
          int workshopsCompleted,
          int workshopsUpcoming,
          decimal? evalScoreAvg,
          decimal totalRevenueAmt,
          decimal completedRevenueAmt
        ) {
          ProjectId = projectId;
          JobNumber = jobNumber;
          ProjectName = projectName;
          ProgramJobId = programJobId;
          ProgramName = programName;
          ProgramStatusId = programStatusId;
          StartDateUtc = startDateUtc;
          EndDateUtc = endDateUtc;
          Participants = participants;
          Coachees = coachees;
          SessionsAllocated = sessionsAllocated;
          SessionsBooked = sessionsBooked;
          SessionsCompleted = sessionsCompleted;
          SessionsUpcoming = sessionsUpcoming;
          SessionNextUpcomingUtc = sessionsNextUpcomingUtc;
          Workshops = workshops;
          WorkshopsEarliestLocal = workshopsEarliestLocal;
          WorkshopsLatestLocal = workshopsLatestLocal;
          WorkshopsNextUpcomingLocal = workshopsNextUpcomingLocal;
          WorkshopsCompleted = workshopsCompleted;
          WorkshopsUpcoming = workshopsUpcoming;
          EvalScoreAvg = evalScoreAvg;
          TotalRevenueAmt = totalRevenueAmt;
          CompletedRevenueAmt = completedRevenueAmt;
        }

      }
    }
  }
}
