using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using static Integral.Web.DbHelper.Common;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AbleQuotes {

      // Values for al_QuoteItem IsOptional column. Not making a lookup table for it yet.
      // Possible values are
      // 0 - item is not optional.
      // 1 - item is optional, default is selected.
      // 2 - item is optional, default is not selected.
      public class OptionalEnum {
        public int Id { get; private set; }
        public string Text { get; private set; }
        public bool IsOptional { get; private set; }
        public bool DefaultSelected { get; private set; }
        public static List<OptionalEnum> Options { get; private set; } = new List<OptionalEnum>();
        private static Dictionary<int, OptionalEnum> OptionsById = new Dictionary<int, OptionalEnum>();
        private OptionalEnum(int id, string text, bool isOptional, bool defaultSelected) {
          this.Id = id; this.Text = text; this.IsOptional = isOptional; this.DefaultSelected = defaultSelected;
          Options.Add(this);
          OptionsById.Add(id, this);
        }
        public static OptionalEnum GetOptionById(int? id, OptionalEnum defaultValue) {
          if (id != null && OptionsById.ContainsKey((int)id)) return OptionsById[(int)id];
          return defaultValue;
        }
        public static OptionalEnum No = new OptionalEnum(0, "No", false, false);
        public static OptionalEnum Yes_Selected = new OptionalEnum(1, "Yes, selected", true, true);
        public static OptionalEnum Yes_NotSelected = new OptionalEnum(2, "Yes, not selected", true, false);
      }

      public class SqlForUserColumns {
        public const string IsQuoteOwner = "isqo.ForUser_IsQuoteOwner";
        public const string IsQuoteContact = "isqc.ForUser_IsQuoteContact";
        public const string IsQuoteTeamMember = "istm.ForUser_IsQuoteTeamMember";
        public const string IsPCOrPLC = "ispc.ForUser_IsPCOrPLC";
        public const string IsInProjectAccess = "ispa.ForUser_IsInProjectAccess";
        public const string IsDeliveryInProject = "isdip.ForUser_IsDeliveryInProject";
        public const string IsCoachInProgram = "isco.ForUser_IsCoachInProgram";
      }

      private const string ForUserIdParam = "ForUserId";

      public static QuoteInfo GetEmptyQuoteInfo() {
        return new QuoteInfo();
      }

      public static QuoteInfo GetQuoteInfoOrNull(int quoteId, AbleUser.AbleUserBasicInfo forUser = null) {
        var quoteList = GetQuoteListPaged(forUser, 1, "",
          $"quo.QuoteId = @QuoteId", IncludeDeletedEnum.No, "", null, null,
          NewSqlParameter("QuoteId", quoteId));
        if (quoteList.InfoList.Count == 0) return null;
        return quoteList.InfoList[0];
      }

      public static List<QuoteInfo> GetQuotesByCompanyId(int companyId, AbleUser.AbleUserBasicInfo forUser = null) {
        var quoteList = GetQuoteListPaged(
          forUser,
          "cmp.SvCompanyId = @CompanyId", IncludeDeletedEnum.No,
          $"quo.CreatedUtc DESC", null, null,
          NewSqlParameter("CompanyId", companyId));
        return quoteList.InfoList;
      }

      public static List<QuoteInfo> GetQuotesByOwner(int ownerUserId, AbleUser.AbleUserBasicInfo forUser = null) {
        var quoteList = GetQuoteListPaged(
          forUser,
          $"quo.OwnerUserId = @OwnerId", IncludeDeletedEnum.No, "", null, null,
          NewSqlParameter("OwnerId", ownerUserId));
        return quoteList.InfoList;
      }

      public static List<QuoteInfo> GetQuotesByContact(int contactUserId, AbleUser.AbleUserBasicInfo forUser = null) {
        var quoteList = GetQuoteListPaged(
          forUser,
          $"quo.QuoteUserId = @ContactUserId", IncludeDeletedEnum.No, "", null, null,
          NewSqlParameter("ContactUserId", contactUserId));
        return quoteList.InfoList;
      }

      public static List<QuoteInfo> GetQuotesByProjectAccess(int accessUserId, AbleUser.AbleUserBasicInfo forUser = null) {
        var quoteList = GetQuoteListPaged(
          forUser, null,
          "INNER JOIN al_UserProjectAccess upa ON upa.ProjectId = prj.ProjectId",
          "upa.UserId = @AccessUserId", IncludeDeletedEnum.No, "", null, null,
          NewSqlParameter("AccessUserId", accessUserId));
        return quoteList.InfoList;
      }

      public static QuoteInfo GetQuoteInfoOrNull(string jobNumber, AbleUser.AbleUserBasicInfo forUser = null) {
        var quoteList = GetQuoteListPaged(forUser, 1, "",
          $"quo.JobNumber = @JobNumber", IncludeDeletedEnum.No, "", null, null,
          NewSqlParameter("JobNumber", jobNumber));
        if (quoteList.InfoList.Count == 0) return null;
        return quoteList.InfoList[0];
      }

      public static QuoteInfo GetQuoteInfoOrNull(Guid quoteGuid, AbleUser.AbleUserBasicInfo forUser = null) {
        var quoteList = GetQuoteListPaged(forUser, 1, "",
          $"quo.QuotePublicGuid = @QuotePublicGuid", IncludeDeletedEnum.No, "", null, null,
          NewSqlParameter("QuotePublicGuid", quoteGuid));
        if (quoteList.InfoList.Count == 0) return null;
        return quoteList.InfoList[0];
      }

      public enum QuoteListMode { Active, Completed, Any }

      public static QuoteListPaged GetQuotesForLists(AbleUser.AbleUserInfo forUser, Projects.ProjectInfo forProject) {
        return GetQuotesForLists(forUser, forProject, QuoteListMode.Any, null, 0, 0);
      }

      public static QuoteListPaged GetQuotesForLists(AbleUser.AbleUserInfo forUser, QuoteListMode listMode, string searchTerm, int offsetRows, int fetchRows) {
        return GetQuotesForLists(forUser, null, listMode, searchTerm, offsetRows, fetchRows);
      }

      // Applies rules for what quotes can be viewed.
      // This function should be used for all lists/dropdowns.
      // The SQL rules are meant to replicate the view permissions in AppAccess.Quotes.CanViewQuoteInfo() (details in clickup #860rdfm18)
      private static QuoteListPaged GetQuotesForLists(
        AbleUser.AbleUserInfo forUser,
        Projects.ProjectInfo forProject,
        QuoteListMode listMode,
        string searchTerm,
        int offsetRows, int fetchRows) {

        if (forUser == null) throw new ArgumentNullException("forUser is required. Usually provide the logged-in user.");

        var whereClauses = new List<string>();
        var whereParams = new List<SqlParameter>();
        const string sqlParam_StatusId_Lost = "QuoteStatusId_Lost";

        // Required params.
        whereParams.Add(NewSqlParameter(sqlParam_StatusId_Lost, AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.lost).QuoteStatusId));

        if (forProject != null) {
          // For specific project, add job number.
          whereClauses.Add($"quo.JobNumber = @JobNumber");
          whereParams.Add(NewSqlParameter("JobNumber", forProject.JobNumber));
        }

        if (listMode == QuoteListMode.Active) {
          // "Active" quotes are not Accepted and not Lost.
          whereClauses.Add($"quo.ClientAcceptedUtc IS NULL AND quo.QuoteStatusId <> @{sqlParam_StatusId_Lost}");
        } else if (listMode == QuoteListMode.Completed) {
          // "Completed" quotes are either Accepted or Lost.
          whereClauses.Add($"(quo.ClientAcceptedUtc IS NOT NULL OR quo.QuoteStatusId = @{sqlParam_StatusId_Lost})");
        }

        // Search term.
        if (!searchTerm.IsNullOrEmpty()) {
          whereClauses.Add($@"
            (
              quo.JobNumber LIKE '%' + @SearchTerm + '%' OR
              quo.ProjectName LIKE '%' + @SearchTerm + '%' OR
              prj.ProjectName LIKE '%' + @SearchTerm + '%' OR
              cmp.CompanyName LIKE '%' + @SearchTerm + '%' OR
              u.FirstName LIKE '%' + @SearchTerm + '%' OR
              u.LastName LIKE '%' + @SearchTerm + '%' OR
              u.FirstName + ' '  + u.LastName LIKE '%' + @SearchTerm + '%' OR
              u.Email LIKE '%' + @SearchTerm + '%'
            )
          ");
          whereParams.Add(NewSqlParameter("SearchTerm", searchTerm));
        }

        if (!forUser.IsAbleAdmin) {
          string whereCompletedCase = "";
          // If status is Accepted, also allow Coaches in Program to see the quotes.
          if (listMode == QuoteListMode.Completed) {
            whereCompletedCase = $" OR ({SqlForUserColumns.IsCoachInProgram} = 1)";
          }

          // Only get quotes that match the rules in AppAccess.CanViewQuoteInfo()
          whereClauses.Add($@"
            ( {SqlForUserColumns.IsQuoteOwner} = 1
              {whereCompletedCase}
              OR
              ( quo.QuoteStatusId <> @{sqlParam_StatusId_Lost}
                AND
                ( {SqlForUserColumns.IsQuoteContact} = 1
                  OR
                  {SqlForUserColumns.IsPCOrPLC} = 1
                  OR
                  {SqlForUserColumns.IsQuoteTeamMember} = 1
                )
              )
            )");
        }

        var sqlOrderBy = $"quo.CreatedUtc DESC";
        if (listMode == QuoteListMode.Any) sqlOrderBy = $"quo.QuoteStatusId, {sqlOrderBy}";

        return GetQuoteListPaged(
          forUser,
          whereClauses.Join(" AND "),
          IncludeDeletedEnum.No,
          sqlOrderBy,
          offsetRows, fetchRows, whereParams.ToArray()
        );
      }

      public static QuoteListPaged GetQuotesToSignForClient(AbleUser.AbleUserBasicInfo clientUser) {

        if (clientUser == null) throw new ArgumentException("User object required.");
        if (!clientUser.IsAbleClient) throw new ArgumentException("User must be a Client.");

        string sqlWhere = "";

        // Where Quote Status is Client-Signing and doesn't have Accepted date,
        // and Client is the Quote Contact or has Project Access.
        sqlWhere = $@"
          quo.QuoteStatusId = @QuoteStatusId_ClientSigning
          AND quo.ClientAcceptedUtc IS NULL
          AND ({SqlForUserColumns.IsQuoteContact} = 1 OR {SqlForUserColumns.IsInProjectAccess} = 1)";

        return GetQuoteListPaged(
          clientUser,
          sqlWhere,
          IncludeDeletedEnum.No,
          $"quo.CreatedUtc DESC",
          null, null,
          NewSqlParameter("QuoteStatusId_ClientSigning", AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.client).QuoteStatusId)
        );
      }

      public static bool IsQuoteCompleted(QuoteInfo quoteInfo) {
        if (quoteInfo == null) return false;
        return quoteInfo.QuoteStatusId == AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusId ||
               quoteInfo.QuoteStatusId == AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.lost).QuoteStatusId ||
               quoteInfo.QuoteStatusId == AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.client).QuoteStatusId;
      }

      public enum IncludeDeletedEnum { No, Yes }

      private static QuoteListPaged GetQuoteListPaged(
        AbleUser.AbleUserBasicInfo forUser,
        string sqlWhereConditions,
        IncludeDeletedEnum includeDeleted,
        string sqlOrderBy,
        int? offsetRows, int? fetchRows,
        params SqlParameter[] sqlWhereParams) {

        return GetQuoteListPaged(forUser, null, "", sqlWhereConditions, includeDeleted, sqlOrderBy, offsetRows, fetchRows, sqlWhereParams);
      }

      private static QuoteListPaged GetQuoteListPaged(
        AbleUser.AbleUserBasicInfo forUser,
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        IncludeDeletedEnum includeDeleted,
        string sqlOrderBy,
        int? offsetRows, int? fetchRows,
        params SqlParameter[] sqlWhereParams) {

        var infoPaged = new QuoteListPaged();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);

        if (sqlOrderBy.IsNullOrEmpty()) sqlOrderBy = $"quo.CreatedUtc DESC";

        if (includeDeleted == IncludeDeletedEnum.No) {
          if (!sqlWhereConditions.IsNullOrEmpty()) sqlWhereConditions = "(" + sqlWhereConditions + ") AND ";
          sqlWhereConditions += $"quo.DeletedUtc IS NULL";
        }

        if (!sqlWhereParams.ContainsParamName(ForUserIdParam)) {
          // Ensure this param exists, default to 0 to avoid accidentally selecting nulls.
          Array.Resize(ref sqlWhereParams, sqlWhereParams.Length + 1);
          sqlWhereParams[sqlWhereParams.Length - 1] = NewSqlParameter(ForUserIdParam, forUser?.UserId ?? 0);
        }

        string sql = $@"
          {Projects.GetSQLDeliveryInProjectCTE(ForUserIdParam)}
          SELECT {sqlTop}
            COUNT(*) OVER() AS TotalRows,
            quo.ProjectName AS QuoteTitle,
            quo.QuoteId, quo.CreatedUtc, quo.DeletedUtc, quo.JobNumber, quo.OwnerUserId, quo.LeadConsultantUserId,
            quo.ProposalDesignerUserId, quo.QuoteUserId, quo.QuoteStatusId,
            quo.QuotePublicGuid, quo.XeroTaxType, quo.CustomInvoicing, quo.AddToFreshSales, quo.QwilrUrl, quo.QwilrPDFUrl,
            quo.BrandingOrgId, quo.EstimatedStartDateUtc,
            quo.OPPPercentage, quo.PLCPercentage, quo.DeliveryPercentage, quo.PlatformPercentage, quo.ProposalDesignerPercentage,
            quo.CoverLetterHtml,
            quo.ClientAcceptedUtc, quo.ClientAcceptedAmount,
            quo.ClientFirstName, quo.ClientLastName, quo.ClientEmailAddress,
            quo.AccPayFirstName, quo.AccPayLastName, quo.AccPayEmailAddress,
            quo.QuoteNotes, quo.ExcludeFromSalesIncentive, quo.QuoteDealSourceId,
            quo.QuoteSalesContentTypeId, quo.QuoteSalesContentUrlId,
            quo.QuoteSalesContentPDFFileName, quo.QuoteSalesContentWebPageUrl,
            cmp.SvCompanyId, cmp.OrgId, cmp.CompanyGUID, cmp.CompanyName, cmp.ClientLeadUserId,
            prj.ProjectId, prj.ProjectName, prj.ParentOrgId, prj.PurchaseOrderRequired, prj.InvoiceNumber,
            o_prj.OrgGuid AS ParentOrgGuid, o_prj.OrgOwnerUserId,
            qs.QuoteStatusText,
            qtu.TeamUserJSON,
            qi.ItemCount, qi.ItemTotalAmt,
            o.OrgGuid AS BrandingOrgGuid, o.OrgName as BrandingOrgName,
            u.FirstName as OwnerFirstName, u.LastName as OwnerLastName, u.FirstName + ' ' + u.LastName as OwnerFullName, u.Email as OwnerEmail,
            {SqlForUserColumns.IsQuoteOwner},
            {SqlForUserColumns.IsQuoteContact},
            {SqlForUserColumns.IsQuoteTeamMember},
            {SqlForUserColumns.IsPCOrPLC},
            {SqlForUserColumns.IsInProjectAccess},
            {SqlForUserColumns.IsDeliveryInProject},
            {SqlForUserColumns.IsCoachInProgram}

          FROM al_Quote quo WITH (NOLOCK)
          INNER JOIN al_Project prj ON prj.JobNumber = quo.JobNumber
          INNER JOIN sv_Organisation o_prj ON o_prj.OrgId = prj.ParentOrgId
          INNER JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = prj.SvCompanyId
          INNER JOIN al_QuoteStatus qs ON qs.QuoteStatusId = quo.QuoteStatusId
          INNER JOIN sv_User u ON u.UserId = quo.OwnerUserId
          LEFT OUTER JOIN sv_Organisation o ON o.OrgId = quo.BrandingOrgId
          LEFT OUTER JOIN {Projects.DeliveryInProjectCTEName} dipCTE ON dipCTE.JobNumber = quo.JobNumber

          CROSS APPLY ( SELECT IIF(@{ForUserIdParam} > 0 AND quo.OwnerUserId = @{ForUserIdParam}, 1, 0) AS ForUser_IsQuoteOwner ) AS isqo
          CROSS APPLY ( SELECT IIF(@{ForUserIdParam} > 0 AND quo.QuoteUserId = @{ForUserIdParam}, 1, 0) AS ForUser_IsQuoteContact ) AS isqc
          CROSS APPLY ( SELECT ISNULL(dipCTE.IsDeliveryInProject, 0) AS ForUser_IsDeliveryInProject ) AS isdip

          CROSS APPLY (
            SELECT IIF(EXISTS
              ( SELECT 1 FROM id_Job pcj WITH (NOLOCK)
                WHERE pcj.JobNumber = quo.JobNumber
                  AND (@{ForUserIdParam} = pcj.ProjectCoordinatorUserId
                    OR @{ForUserIdParam} = pcj.LeadConsultantUserId)
              ), 1, 0) AS ForUser_IsPCOrPLC
          ) AS ispc

          CROSS APPLY (
            SELECT IIF(EXISTS
              ( SELECT 1 FROM al_UserProjectAccess upa WITH (NOLOCK)
                WHERE upa.ProjectId = prj.ProjectId
                  AND upa.UserId = @{ForUserIdParam}
              ), 1, 0) AS ForUser_IsInProjectAccess
          ) AS ispa

          CROSS APPLY (
            SELECT IIF(EXISTS
            ( SELECT 1 FROM al_QuoteTeamUser qtu WITH (NOLOCK)
              WHERE qtu.QuoteId = quo.QuoteId
                AND qtu.UserId = @{ForUserIdParam}
            ), 1, 0) AS ForUser_IsQuoteTeamMember
          ) AS istm

          CROSS APPLY (
            SELECT IIF(EXISTS (
              SELECT 1
              FROM al_Coachees cu
              LEFT OUTER JOIN id_Job cj ON cj.JobId = cu.ProgramJobId
              WHERE cj.JobNumber = quo.JobNumber AND cu.CoachUserId = @{ForUserIdParam}
            ), 1, 0) AS ForUser_IsCoachInProgram
          ) AS isco

          CROSS APPLY ( -- Item total
            SELECT COUNT(*) AS ItemCount, SUM(qi.UnitPrice * qi.Quantity) AS ItemTotalAmt
            FROM al_QuoteItem qi WITH (NOLOCK)
            WHERE qi.QuoteId = quo.QuoteId
          ) AS qi

          OUTER APPLY ( -- Get team user info
            SELECT (
              SELECT u.UserId, u.UserGuid, u.FirstName, u.LastName, u.Email
              FROM al_QuoteTeamUser qtu WITH (NOLOCK)
              INNER JOIN sv_User u ON u.UserId = qtu.UserId
              WHERE qtu.QuoteId = quo.QuoteId
              ORDER BY qtu.UserId
              FOR JSON AUTO
            ) AS TeamUserJSON
          ) AS qtu

          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        if (sqlTop.IsNullOrEmpty() && offsetRows >= 0 && fetchRows > 0) {
          infoPaged.OffsetRows = offsetRows;
          infoPaged.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        if (ConfigHelper.IsDevServer) infoPaged.SqlText = sql;

        Query(sql,
          dr => {

            if (infoPaged.TotalRows == 0) {
              infoPaged.TotalRows = dr.GetInt("TotalRows");
            }

            var companyId = dr.GetIntOrNull("SvCompanyId");

            var quoteInfo = new QuoteInfo(

              tenantOrgId: dr.GetInt("ParentOrgId"),
              tenantOrgGuid: dr.GetGuid("ParentOrgGuid"),
              tenantOrgOwnerUserId: dr.GetInt("OrgOwnerUserId"),

              quoteId: dr.GetInt("QuoteId"),
              createdUtc: dr.GetDateTime("CreatedUtc"),
              publicGuid: dr.GetGuid("QuotePublicGuid"),
              deletedUtc: dr.GetDateTimeOrNull("DeletedUtc"),
              jobNumber: dr.GetString("JobNumber"),
              ownerUserId: dr.GetInt("OwnerUserId"),
              leadConsultantUserId: dr.GetIntOrNull("LeadConsultantUserId"),
              proposalDesignerUserId: dr.GetIntOrNull("ProposalDesignerUserId"),
              contactUserId: dr.GetInt("QuoteUserId"),
              projectId: dr.GetInt("ProjectId"),
              projectName: dr.GetString("ProjectName"),
              purchaseOrderRequired: dr.GetBoolFromInt("PurchaseOrderRequired"),
              purchaseOrderNumber: dr.GetString("InvoiceNumber"),
              quoteTitle: dr.GetString("QuoteTitle"),
              brandingOrgId: dr.GetIntOrNull("BrandingOrgId"),
              brandingOrgGuid: dr.GetGuidOrNull("BrandingOrgGuid"),
              brandingOrgName: dr.GetString("BrandingOrgName"),
              quoteStatusId: dr.GetInt("QuoteStatusId"),
              quoteStatusText: dr.GetString("QuoteStatusText"),
              estimatedStartDateUtc: dr.GetDateTimeOrNull("EstimatedStartDateUtc"),
              xeroTaxType: dr.GetString("XeroTaxType"),
              customInvoicing: dr.GetBoolFromInt("CustomInvoicing"),
              addToFreshSales: dr.GetBoolFromInt("AddToFreshSales"),
              qwilrUrl: dr.GetString("QwilrUrl"),
              qwilrPDFUrl: dr.GetString("QwilrPDFUrl"),
              oppPercentage: dr.GetDecimal("OPPPercentage"),
              plcPercentage: dr.GetDecimal("PLCPercentage"),
              deliveryPercentage: dr.GetDecimal("DeliveryPercentage"),
              platformPercentage: dr.GetDecimal("PlatformPercentage"),
              proposalDesignerPercentage: dr.GetDecimal("ProposalDesignerPercentage"),
              coverLetterHtml: dr.GetString("CoverLetterHtml"),
              clientAcceptedUtc: dr.GetDateTimeOrNull("ClientAcceptedUtc"),
              clientAcceptedAmount: dr.GetDecimalOrNull("ClientAcceptedAmount"),
              clientFirstName: dr.GetString("ClientFirstName"),
              clientLastName: dr.GetString("ClientLastName"),
              clientEmailAddress: dr.GetString("ClientEmailAddress"),
              accPayFirstName: dr.GetString("AccPayFirstName"),
              accPayLastName: dr.GetString("AccPayLastName"),
              accPayEmailAddress: dr.GetString("AccPayEmailAddress"),
              quoteNotes: dr.GetString("QuoteNotes"),
              excludeFromSalesIncentive: dr.GetBoolFromInt("ExcludeFromSalesIncentive"),
              quoteDealSourceId: dr.GetIntOrNull("QuoteDealSourceId"),
              quoteSalesContentTypeId: dr.GetIntOrNull("QuoteSalesContentTypeId"),
              quoteSalesContentUrlId: dr.GetIntOrNull("QuoteSalesContentUrlId"),
              quoteSalesContentPDFFileName: dr.GetString("QuoteSalesContentPDFFileName"),
              quoteSalesContentWebPageUrl: dr.GetString("QuoteSalesContentWebPageUrl"),
              quoteItemCount: dr.GetInt("ItemCount"),
              quoteItemTotalAmount: dr.GetDecimal("ItemTotalAmt"),
              companyInfo: companyId == null
                ? null
                : new ClientCompanies.BriefCompanyInfo((int)companyId, dr.GetGuid("CompanyGUID"), dr.GetInt("OrgId"), dr.GetString("CompanyName"), dr.GetIntOrNull("ClientLeadUserId")),
              ownerFirstName: dr.GetString("OwnerFirstName"),
              ownerLastName: dr.GetString("OwnerLastName"),
              ownerFullName: dr.GetString("OwnerFullName"),
              ownerEmail: dr.GetString("OwnerEmail"),
              forUser_UserId: forUser?.UserId,
              forUser_IsQuoteOwner: dr.GetBoolFromInt("ForUser_IsQuoteOwner"),
              forUser_IsQuoteContact: dr.GetBoolFromInt("ForUser_IsQuoteContact"),
              forUser_IsQuoteTeamMember: dr.GetBoolFromInt("ForUser_IsQuoteTeamMember"),
              forUser_IsPCOrPLC: dr.GetBoolFromInt("ForUser_IsPCOrPLC"),
              forUser_IsInProjectAccess: dr.GetBoolFromInt("ForUser_IsInProjectAccess"),
              forUser_IsDeliveryInProject: dr.GetBoolFromInt("ForUser_IsDeliveryInProject"),
              forUser_IsCoachInProgram: dr.GetBoolFromInt("ForUser_IsCoachInProgram")
            );
            string teamUserJSON = dr.GetString("TeamUserJSON");
            if (!teamUserJSON.IsNullOrEmpty()) quoteInfo.QuoteTeamUsers = JsonConvert.DeserializeObject<List<QuoteTeamUser>>(teamUserJSON);
            infoPaged.InfoList.Add(quoteInfo);
          },
          sqlWhereParams
        );
        // Populate Quote Items only if we wanted a single quote and it was found.
        if (topOrNullForAll == 1 && infoPaged.InfoList.Count > 0 && infoPaged.InfoList[0] != null) {
          infoPaged.InfoList[0].QuoteItems = GetQuoteItems(infoPaged.InfoList[0].JobNumber, infoPaged.InfoList[0].QuoteId); // Get items if returning a single result.
        }
        return infoPaged;
      }

      // If any of the accepted quotes for a company has an admin as an owner,
      // then we count this as an "existing client" for the purposes of the PlatformServices list.
      // Q: Integral-only?
      public static bool IsCompanyExistingClient(int companyId) {
        var result = GetScalarQueryInt(@"
          IF EXISTS (
            SELECT NULL
            FROM al_Quote q
            INNER JOIN al_Project p ON q.JobNumber = p.JobNumber
            INNER JOIN sv_User u ON u.UserId = q.OwnerUserId
            WHERE p.SvCompanyId = @CompanyId
              AND q.ClientAcceptedUtc IS NOT NULL
              AND u.IsAlbertAdmin = 1
          )
            SELECT 1
          ELSE
            SELECT 0",
          NewSqlParameter("CompanyId", companyId));
        return result == 1;
      }

      public static List<QuoteInfo.QuoteItemInfo> GetQuoteItems(string jobNumber, int? quoteIdOrNullForAll, bool onlyAccepted = false) {
        var items = new List<QuoteInfo.QuoteItemInfo>();
        Query($@"
          SELECT
            qi.QuoteItemId, qi.ProductId, qi.ItemDescription,
            qi.UnitPrice, qi.Quantity, qi.QuantityDescr, qi.IsOptional, qi.QuantitySelectable, qi.IsAccepted,
            pc.CategoryName, p.ProductCategoryId, p.SubCategory, p.ProductTitle, p.QuoteComponentWarningMessage,
            p.SubscriptionId as DefaultSubscriptionId, p.RequiresSubscription, p.IsQuantityPerPerson, p.MinAllowedQuotePrice,
            ISNULL(cmp.HasLockedComponents, 0) AS HasLockedComponents,
            ISNULL(cmp.HasUnallocatedComponentsToInvoice, 0) AS HasUnallocatedComponentsToInvoice
          FROM al_QuoteItem qi
          INNER JOIN al_Quote q ON q.QuoteId = qi.QuoteId
          LEFT OUTER JOIN al_Product p ON qi.ProductId = p.ProductId
          LEFT OUTER JOIN al_ProductCategory pc ON p.ProductCategoryId = pc.ProductCategoryId
          LEFT OUTER JOIN
          (
            SELECT
              QuoteItemId,
              MAX(CASE WHEN LockedDateUtc IS NOT NULL THEN 1 ELSE 0 END) AS HasLockedComponents,
              MAX(CASE WHEN InvoiceItemId IS NULL THEN 1 ELSE 0 END) AS HasUnallocatedComponentsToInvoice
            FROM
              al_Component
            GROUP BY
              QuoteItemId
          ) cmp ON cmp.QuoteItemId = qi.QuoteItemId
          WHERE q.JobNumber = @JobNumber
            {(quoteIdOrNullForAll == null ? "" : "AND qi.QuoteId = @QuoteId")}
            {(onlyAccepted ? "AND qi.IsAccepted = 1" : "")}
          ORDER BY qi.QuoteId, qi.DisplayOrder",
          new List<SqlParameter>() {
            NewSqlParameter("JobNumber", jobNumber),
            NewSqlParameter("QuoteId", quoteIdOrNullForAll)
          },
          dr => {
            items.Add(new QuoteInfo.QuoteItemInfo(
              quoteItemId: dr.GetInt("QuoteItemId"),
              productId: dr.GetIntOrNull("ProductId"),
              categoryName: dr.GetString("CategoryName"),
              productCategoryId: dr.GetIntOrNull("ProductCategoryId"),
              productName: string.Join(" - ", dr.GetString("SubCategory"), dr.GetString("ProductTitle")),
              itemDescriptionHtml: dr.GetString("ItemDescription"),
              quoteComponentWarningMessage: dr.GetString("QuoteComponentWarningMessage"),
              unitPrice: dr.GetDecimalOrNull("UnitPrice"),
              quantity: dr.GetDecimalOrNull("Quantity"),
              quantityDescr: dr.GetString("QuantityDescr"),
              optionalId: dr.GetInt("IsOptional"),
              qtySelectable: dr.GetBoolFromInt("QuantitySelectable"),
              isAccepted: dr.GetBoolFromIntOrNull("IsAccepted"),
              hasLockedComponents: dr.GetBoolFromInt("HasLockedComponents"),
              hasUnallocatedComponentsToInvoice: dr.GetBoolFromInt("HasUnallocatedComponentsToInvoice"),
              subscriptionId: dr.GetIntOrNull("DefaultSubscriptionId"),
              requiresSubscription: dr.GetBoolFromInt("RequiresSubscription"),
              isQuantityPerPerson: dr.GetBoolFromInt("IsQuantityPerPerson"),
              minAllowedQuotePrice: dr.GetDecimalOrNull("MinAllowedQuotePrice")
            ));
          }
        );
        return items;
      }

      public class QuoteItemFundsInfo {

        public int QuoteItemId { get; private set; }
        public string ProductName { get; internal set; }
        public decimal TotalFunds { get; private set; }
        public decimal TotAllocated { get; private set; }

        public QuoteItemFundsInfo(int quoteItemId, string productName, decimal totalFunds, decimal totAllocated) {
          QuoteItemId = quoteItemId;
          ProductName = productName;
          TotalFunds = totalFunds;
          TotAllocated = totAllocated;
        }
      }

      public static QuoteItemFundsInfo GetQuoteItemFundsByType(SqlTransaction trans, int quoteItemId,
        ProgramComponents.KeyColumnEnum ignoreKeyColumn, int ignoreKeyId) {
        return GetQuoteItemFunds(trans, quoteItemId, ignoreKeyColumn, ignoreKeyId, null);
      }

      public static QuoteItemFundsInfo GetQuoteItemFundsByProgram(SqlTransaction trans, int quoteItemId, int ignoreProgramJobId) {
        return GetQuoteItemFunds(trans, quoteItemId, null, null, ignoreProgramJobId);
      }

      private static QuoteItemFundsInfo GetQuoteItemFunds(SqlTransaction trans, int quoteItemId,
        ProgramComponents.KeyColumnEnum? ignoreKeyColumn, int? ignoreKeyId, int? ignoreProgramJobId) {

        QuoteItemFundsInfo af = null;

        Query(trans, $@"
          SELECT pr.ProductTitle, qi.UnitPrice * qi.Quantity AS TotalFunds, cmp.TotalRevenue
          FROM al_QuoteItem qi
          LEFT OUTER JOIN al_Product pr ON qi.ProductId = pr.ProductId
          CROSS APPLY (
            SELECT SUM(cmp.ComponentPrice) AS TotalRevenue
            FROM al_Component cmp
            WHERE cmp.QuoteItemId = qi.QuoteItemId
              {(ignoreKeyColumn == null || ignoreKeyId == null ? "" : $@"AND cmp.{ignoreKeyColumn} <> @IgnoreKeyId")}
              {(ignoreProgramJobId == null ? "" : $@"AND cmp.ProgramJobId <> @IgnoreProgramJobId")}
          ) AS cmp
          WHERE qi.QuoteItemId = @QuoteItemId",
          dr => {
            af = new QuoteItemFundsInfo(quoteItemId, dr.GetString("ProductTitle"), dr.GetDecimalOrDefault("TotalFunds", 0), dr.GetDecimalOrDefault("TotalRevenue", 0));
          },
          NewSqlParameter("QuoteItemId", quoteItemId),
          NewSqlParameter("IgnoreKeyId", ignoreKeyId),
          NewSqlParameter("IgnoreProgramJobId", ignoreProgramJobId)
        );

        return af;
      }

      public class QuoteItemForList {

        public int QuoteItemId { get; private set; }
        public int QuoteId { get; private set; }
        public int DisplayOrder { get; private set; }
        public string Description { get; private set; }
        public string CategoryName { get; private set; }
        public string ProductTitle { get; private set; }
        public decimal TotalFunds { get; private set; }
        public decimal TotalRevenue { get; private set; }
        public decimal TotalRevenueIgnoredComponent { get; private set; }
        public decimal TotalRevenueIgnoredProgram { get; private set; }

        public QuoteItemForList(
          int quoteItemId,
          int quoteId,
          int displayOrder,
          string description,
          string categoryName,
          string productTitle,
          decimal totalFunds,
          decimal totalRevenue,
          decimal totalRevenueIgnoredComponent,
          decimal totalRevenueIgnoredProgram
        ) {
          this.QuoteItemId = quoteItemId;
          this.QuoteId = quoteId;
          this.DisplayOrder = displayOrder;
          this.Description = description;
          this.CategoryName = categoryName;
          this.ProductTitle = productTitle;
          this.TotalFunds = totalFunds;
          this.TotalRevenue = totalRevenue;
          this.TotalRevenueIgnoredComponent = totalRevenueIgnoredComponent;
          this.TotalRevenueIgnoredProgram = totalRevenueIgnoredProgram;
        }
      }

      public static List<QuoteItemForList> GetQuoteItemsForList(
        string jobNumber, bool coachingItemsOnly,
        ProgramComponents.KeyColumnEnum ignoreKeyColumn, int? ignoreKeyId, int ignoreProgramJobId) {

        var result = new List<QuoteItemForList>();

        Query($@"
          SELECT
            q.QuoteId,
            qi.QuoteItemId, qi.ItemDescription, qi.DisplayOrder, qi.UnitPrice * qi.Quantity AS TotalFunds,
            pc.CategoryName, prd.ProductTitle,
            cmp.TotalRevenue, cmp.TotalRevenueIgnoredComponent, cmp.TotalRevenueIgnoredProgram
          FROM al_QuoteItem qi
          INNER JOIN al_Quote q ON qi.QuoteId = q.QuoteId
          INNER JOIN al_Product prd ON qi.ProductId = prd.ProductId
          INNER JOIN al_ProductCategory pc ON prd.ProductCategoryId = pc.ProductCategoryId
          OUTER APPLY (
            SELECT
              SUM(cmp.ComponentPrice) AS TotalRevenue,
              SUM(IIF(cmp.{ignoreKeyColumn} = @IgnoreKeyId, 0, cmp.ComponentPrice)) AS TotalRevenueIgnoredComponent,
              SUM(IIF(cmp.{ignoreKeyColumn} > 0 AND cmp.ProgramJobId = @IgnoreProgramJobId, 0, cmp.ComponentPrice)) AS TotalRevenueIgnoredProgram
            FROM al_Component cmp
            WHERE cmp.QuoteItemId = qi.QuoteItemId
          ) AS cmp
          WHERE q.JobNumber = @JobNumber
            AND q.DeletedUtc IS NULL
            AND qi.IsAccepted = 1
            AND (@CoachingOnly = 0 OR pc.IsCoaching = 1)
          ORDER BY q.QuoteId, qi.DisplayOrder",
          dr => {
            result.Add(new QuoteItemForList(
              quoteItemId: dr.GetInt("QuoteItemId"),
              quoteId: dr.GetInt("QuoteId"),
              displayOrder: dr.GetInt("DisplayOrder"),
              description: dr.GetString("ItemDescription"),
              categoryName: dr.GetString("CategoryName"),
              productTitle: dr.GetString("ProductTitle"),
              totalFunds: dr.GetDecimalOrDefault("TotalFunds", 0),
              totalRevenue: dr.GetDecimalOrDefault("TotalRevenue", 0),
              totalRevenueIgnoredComponent: dr.GetDecimalOrDefault("TotalRevenueIgnoredComponent", 0),
              totalRevenueIgnoredProgram: dr.GetDecimalOrDefault("TotalRevenueIgnoredProgram", 0)
            ));
          },
          NewSqlParameter("JobNumber", jobNumber),
          NewSqlParameter("CoachingOnly", coachingItemsOnly ? 1 : 0),
          NewSqlParameter("IgnoreKeyId", ignoreKeyId ?? 0),
          NewSqlParameter("IgnoreProgramJobId", ignoreProgramJobId)
        );

        return result;
      }

      // Check if Quote Items in Quote have Coachees or Components pointing to them.
      public static bool QuoteHasDependents(int quoteId) {
        // Returns 1 for true, 0 for false.
        int result = GetScalarQueryInt(@"
          IF EXISTS (
            SELECT 1
            FROM al_Quote q
            INNER JOIN al_QuoteItem qi ON q.QuoteId = qi.QuoteId
            LEFT OUTER JOIN id_CoachingSession cs ON qi.QuoteItemId = cs.QuoteItemId
            LEFT OUTER JOIN al_Component cp ON qi.QuoteItemId = cp.QuoteItemId
            WHERE q.QuoteId = @QuoteId
              AND (cs.CoachingSessionId IS NOT NULL OR cp.ComponentId IS NOT NULL)
          )
          SELECT 1
          ELSE
          SELECT 0",
          NewSqlParameter("QuoteId", quoteId));
        return result == 1;
      }

      public static int CreateQuote(NewQuoteInfo newQuoteInfo) {
        return CreateQuote(null, newQuoteInfo);
      }

      public static int CreateQuote(SqlTransaction trans, NewQuoteInfo newQuoteInfo) {

        if (newQuoteInfo == null) throw new ArgumentException("newQuoteInfo is null.");

        return GetScalarQueryInt(trans, @"
          INSERT INTO al_Quote (
            JobNumber, OwnerUserId, QuoteUserId, ProjectName, EstimatedStartDateUtc, XeroTaxType, CustomInvoicing, AddToFreshSales,
            BrandingOrgId, QuoteStatusId, OPPPercentage, PLCPercentage, DeliveryPercentage, PlatformPercentage, ProposalDesignerPercentage, CoverLetterHtml,
            LeadConsultantUserId, ProposalDesignerUserId, ExcludeFromSalesIncentive, QuoteDealSourceId)
          OUTPUT INSERTED.QuoteId
          VALUES (
            @JobNumber, @OwnerUserId, @ContactUserId, @ProjectName, @EstimatedStartDateUtc, @XeroTaxType, @CustomInvoicing, @AddToFreshSales,
            @BrandingOrgId, @QuoteStatusId, @OPPPercentage, @PLCPercentage, @DeliveryPercentage, @PlatformPercentage, @ProposalDesignerPercentage, @CoverLetterHtml,
            @LeadConsultantUserId, @ProposalDesignerUserId, @ExcludeFromSalesIncentive, @QuoteDealSourceId)",
          NewSqlParameter("JobNumber", newQuoteInfo.JobNumber),
          NewSqlParameter("OwnerUserId", newQuoteInfo.OwnerUserId),
          NewSqlParameter("ContactUserId", newQuoteInfo.ContactUserId),
          NewSqlParameter("ProjectName", newQuoteInfo.QuoteTitle),
          NewSqlParameter("BrandingOrgId", newQuoteInfo.BrandingOrgId),
          NewSqlParameter("EstimatedStartDateUtc", newQuoteInfo.EstimatedStartDateUtc),
          NewSqlParameter("XeroTaxType", newQuoteInfo.XeroTaxType),
          NewSqlParameter("CustomInvoicing", newQuoteInfo.CustomInvoicing),
          NewSqlParameter("AddToFreshSales", newQuoteInfo.AddToFreshSales),
          NewSqlParameter("QuoteStatusId", newQuoteInfo.QuoteStatusId),
          NewSqlParameter("OPPPercentage", newQuoteInfo.OPPPercentage),
          NewSqlParameter("PLCPercentage", newQuoteInfo.PLCPercentage),
          NewSqlParameter("DeliveryPercentage", newQuoteInfo.DeliveryPercentage),
          NewSqlParameter("PlatformPercentage", newQuoteInfo.PlatformPercentage),
          NewSqlParameter("ProposalDesignerPercentage", newQuoteInfo.ProposalDesignerPercentage),
          NewSqlParameter("CoverLetterHtml", newQuoteInfo.CoverLetterHtml),
          NewSqlParameter("LeadConsultantUserId", newQuoteInfo.LeadConsultantUserId),
          NewSqlParameter("ProposalDesignerUserId", newQuoteInfo.ProposalDesignerUserId),
          NewSqlParameter("ExcludeFromSalesIncentive", newQuoteInfo.ExcludeFromSalesIncentive),
          NewSqlParameter("QuoteDealSourceId", newQuoteInfo.QuoteDealSourceId)
        );
      }

      public static bool UpdateQuote(QuoteInfo quoteInfo) => UpdateQuote(null, quoteInfo);

      public static bool UpdateQuote(SqlTransaction trans, QuoteInfo quoteInfo) {

        if (quoteInfo == null) throw new ArgumentException("quoteInfo is null.");

        var rows = GetNonQueryInt(trans, @"

          UPDATE al_Quote SET

            ProjectName = @QuoteTitle,
            JobNumber = @JobNumber,
            OwnerUserId = @OwnerUserId,
            QuoteUserId = @ContactUserId,
            BrandingOrgId = @BrandingOrgId,
            EstimatedStartDateUtc = @EstimatedStartDateUtc,
            XeroTaxType = @XeroTaxType,
            CustomInvoicing = @CustomInvoicing,
            AddToFreshSales = @AddToFreshSales,
            ExcludeFromSalesIncentive = @ExcludeFromSalesIncentive,
            QuoteStatusId = @QuoteStatusId,
            OPPPercentage = @OPPPercentage,
            PLCPercentage = @PLCPercentage,
            DeliveryPercentage = @DeliveryPercentage,
            PlatformPercentage = @PlatformPercentage,
            ProposalDesignerPercentage = @ProposalDesignerPercentage,
            CoverLetterHtml = @CoverLetterHtml,
            QuoteNotes = @QuoteNotes,
            LeadConsultantUserId = @LeadConsultantUserId,
            ProposalDesignerUserId = @ProposalDesignerUserId,
            QuoteDealSourceId = @QuoteDealSourceId,

            QuoteSalesContentTypeId = @QuoteSalesContentTypeId,
            QuoteSalesContentUrlId = @QuoteSalesContentUrlId,
            QuoteSalesContentPDFFileName = @QuoteSalesContentPDFFileName,
            QuoteSalesContentWebPageUrl = @QuoteSalesContentWebPageUrl,
            QwilrUrl = @QwilrUrl,
            QwilrPDFUrl = @QwilrPDFUrl

          WHERE QuoteId = @QuoteId",

          NewSqlParameter("QuoteId", quoteInfo.QuoteId),

          NewSqlParameter("QuoteTitle", quoteInfo.QuoteTitle),
          NewSqlParameter("JobNumber", quoteInfo.JobNumber),
          NewSqlParameter("OwnerUserId", quoteInfo.OwnerUserId),
          NewSqlParameter("ContactUserId", quoteInfo.ContactUserId),
          NewSqlParameter("BrandingOrgId", quoteInfo.BrandingOrgId),
          NewSqlParameter("EstimatedStartDateUtc", quoteInfo.EstimatedStartDateUtc),
          NewSqlParameter("XeroTaxType", quoteInfo.XeroTaxType),
          NewSqlParameter("CustomInvoicing", quoteInfo.CustomInvoicing),
          NewSqlParameter("AddToFreshSales", quoteInfo.AddToFreshSales),
          NewSqlParameter("ExcludeFromSalesIncentive", quoteInfo.ExcludeFromSalesIncentive),
          NewSqlParameter("QuoteStatusId", quoteInfo.QuoteStatusId),
          NewSqlParameter("OPPPercentage", quoteInfo.OPPPercentage),
          NewSqlParameter("PLCPercentage", quoteInfo.PLCPercentage),
          NewSqlParameter("DeliveryPercentage", quoteInfo.DeliveryPercentage),
          NewSqlParameter("PlatformPercentage", quoteInfo.PlatformPercentage),
          NewSqlParameter("ProposalDesignerPercentage", quoteInfo.ProposalDesignerPercentage),
          NewSqlParameter("CoverLetterHtml", quoteInfo.CoverLetterHtml),
          NewSqlParameter("QuoteNotes", quoteInfo.QuoteNotes),
          NewSqlParameter("LeadConsultantUserId", quoteInfo.LeadConsultantUserId),
          NewSqlParameter("ProposalDesignerUserId", quoteInfo.ProposalDesignerUserId),
          NewSqlParameter("QuoteDealSourceId", quoteInfo.QuoteDealSourceId),
          NewSqlParameter("QuoteSalesContentTypeId", quoteInfo.QuoteSalesContentTypeId),
          NewSqlParameter("QuoteSalesContentUrlId", quoteInfo.QuoteSalesContentUrlId),
          NewSqlParameter("QuoteSalesContentPDFFileName", quoteInfo.QuoteSalesContentPDFFileName),
          NewSqlParameter("QuoteSalesContentWebPageUrl", quoteInfo.QuoteSalesContentWebPageUrl),
          NewSqlParameter("QwilrUrl", quoteInfo.QwilrUrl),
          NewSqlParameter("QwilrPDFUrl", quoteInfo.QwilrPDFUrl)
        );

        return rows == 1;
      }

      public static int CopyQuoteAndItems(SqlTransaction trans, int quoteId, string newQuoteTitle) {

        return GetScalarQueryInt(trans, $@"

          DECLARE @cols NVARCHAR(4000);
          DECLARE @sql NVARCHAR(4000);
          DECLARE @newId INT;

          {(trans == null ? "BEGIN TRANSACTION;" : "")};

          SET @cols = STUFF((SELECT N',' + ac.name
            FROM sys.all_columns AS ac
            WHERE ac.object_id = OBJECT_ID(N'al_Quote', N'U')
              AND ac.name NOT IN ('QuoteId','QuotePublicGuid','CreatedUtc','QuoteStatusId','BrandingOrgId','DeletedUtc','InvoiceInstructionTypeId')
              AND ac.name NOT LIKE 'Client%'
              AND ac.name NOT LIKE 'AccPay%'
            ORDER BY ac.column_id
            FOR XML PATH ('')), 1, 1, N'');

          SET @sql =
            'DECLARE @OutId TABLE ( Id INT ); ' +
            'INSERT INTO al_Quote (QuoteStatusId,' + @cols + ') ' +
            'OUTPUT INSERTED.QuoteId AS Id INTO @OutId ' +
            'SELECT {(int)AbleQuoteStatus.AppTagEnum.draft},' + @cols + ' ' +
            'FROM al_Quote ' +
            'WHERE QuoteId = @QuoteId; ' +
            'SET @newId = (SELECT TOP 1 Id FROM @OutId);';
          EXECUTE sp_executesql @sql, N'@QuoteId INT, @newId INT OUTPUT', @QuoteId = @QuoteId, @newId = @newId OUTPUT;

          UPDATE al_Quote SET ProjectName = @QuoteTitle WHERE QuoteId = @newId

          SET @cols = STUFF((
            SELECT N',' + ac.name
            FROM sys.all_columns AS ac
            WHERE ac.object_id = OBJECT_ID(N'al_QuoteItem', N'U')
              AND ac.name NOT IN ('QuoteItemId','QuoteId','IsAccepted')
            ORDER BY ac.column_id
            FOR XML PATH ('')), 1, 1, N'');

          SET @sql =
            'INSERT INTO al_QuoteItem (QuoteId,' + @cols + ') ' +
            'SELECT @newId,' + @cols + ' ' +
            'FROM al_QuoteItem ' +
            'WHERE QuoteId = @QuoteId';
          EXECUTE sp_executesql @sql, N'@QuoteId INT, @newId INT', @QuoteId = @QuoteId, @newId = @newId;

          SET @cols = STUFF((
            SELECT N',' + ac.name
            FROM sys.all_columns AS ac
            WHERE ac.object_id = OBJECT_ID(N'al_QuoteTeamUser', N'U')
              AND ac.name NOT IN ('QuoteTeamUserId','QuoteId')
            ORDER BY ac.column_id
            FOR XML PATH ('')), 1, 1, N'');

          SET @sql =
            'INSERT INTO al_QuoteTeamUser (QuoteId,' + @cols + ') ' +
            'SELECT @newId,' + @cols + ' ' +
            'FROM al_QuoteTeamUser ' +
            'WHERE QuoteId = @QuoteId';
          EXECUTE sp_executesql @sql, N'@QuoteId INT, @newId INT', @QuoteId = @QuoteId, @newId = @newId;

          {(trans == null ? "COMMIT TRANSACTION;" : "")};

          SELECT @newId;",

          NewSqlParameter("QuoteId", quoteId),
          NewSqlParameter("QuoteTitle", newQuoteTitle));
      }

      public static bool DeleteQuote(int quoteId) {
        int rows = GetNonQueryInt(@"
          UPDATE al_Quote SET DeletedUtc = GETUTCDATE()
          WHERE QuoteId = @QuoteId",
          NewSqlParameter("QuoteId", quoteId));
        return rows == 1;
      }

      public static void UpdateQuoteItemAccepted(SqlTransaction trans, int quoteItemId, bool isAccepted) {

        GetNonQueryInt(trans, @"
          UPDATE al_QuoteItem SET IsAccepted = @IsAccepted
          WHERE QuoteItemId = @QuoteItemId",
          NewSqlParameter("QuoteItemId", quoteItemId),
          NewSqlParameter("IsAccepted", isAccepted)
        );
      }

      public static int CreateQuoteItem(SqlTransaction trans, int quoteId, int? productId, string itemDescription, int isOptionalId, decimal? unitPrice, decimal? quantity, string quantityDescr, bool isAccepted = false) {

        return GetScalarQueryInt(trans, @"

          -- Auto compute DisplayOrder
          DECLARE @NextDisplayOrder INT;
          SELECT @NextDisplayOrder = ISNULL(MAX(DisplayOrder), 0) + 1 FROM al_QuoteItem WHERE QuoteId = @QuoteId;

          INSERT INTO al_QuoteItem (QuoteId, ProductId, ItemDescription, IsOptional, UnitPrice, Quantity, QuantityDescr, DisplayOrder, IsAccepted)
          OUTPUT INSERTED.QuoteItemId
          VALUES (@QuoteId, @ProductId, @ItemDescription, @IsOptional, @UnitPrice, @Quantity, @QuantityDescr, @NextDisplayOrder, @IsAccepted)",

          NewSqlParameter("QuoteId", quoteId),
          NewSqlParameter("ProductId", productId),
          NewSqlParameter("ItemDescription", itemDescription),
          NewSqlParameter("IsOptional", isOptionalId),
          NewSqlParameter("UnitPrice", unitPrice),
          NewSqlParameter("Quantity", quantity),
          NewSqlParameter("QuantityDescr", quantityDescr),
          NewSqlParameter("IsAccepted", isAccepted)
        );
      }

      public static int AddQuoteTeamUser(SqlTransaction trans, int quoteId, int userId) {

        return GetNonQueryInt(trans, @"
          INSERT INTO al_QuoteTeamUser (QuoteId, UserId)
          VALUES (@QuoteId, @UserId)",
          NewSqlParameter("QuoteId", quoteId),
          NewSqlParameter("UserId", userId)
        );
      }

      public static void UpdateQuoteAccepted(SqlTransaction trans, int quoteId, decimal quoteTotalExGST, Interfaces.IQuoteSignoffInfo signoffInfo) {

        GetNonQueryInt(trans, @"
          UPDATE al_Quote SET
            ClientAcceptedUtc = GETUTCDATE(),
            ClientAcceptedAmount = @QuoteTotalExGST,
            ClientFirstName = @ClientFirstName,
            ClientLastName = @ClientLastName,
            ClientEmailAddress = @ClientEmailAddress,
            AccPayFirstName = @AccPayFirstName,
            AccPayLastName = @AccPayLastName,
            AccPayEmailAddress = @AccPayEmailAddress,
            QuoteStatusId = @QuoteStatusId
          WHERE QuoteId = @QuoteId",
          NewSqlParameter("QuoteId", quoteId),
          NewSqlParameter("QuoteTotalExGST", quoteTotalExGST),
          NewSqlParameter("ClientFirstName", signoffInfo.ClientFirstName.LimitLengthTo(50)),
          NewSqlParameter("ClientLastName", signoffInfo.ClientLastName.LimitLengthTo(50)),
          NewSqlParameter("ClientEmailAddress", signoffInfo.ClientEmail.LimitLengthTo(100)),
          NewSqlParameter("AccPayFirstName", signoffInfo.AccFirstName.LimitLengthTo(50)),
          NewSqlParameter("AccPayLastName", signoffInfo.AccLastName.LimitLengthTo(50)),
          NewSqlParameter("AccPayEmailAddress", signoffInfo.AccEmail.LimitLengthTo(100)),
          NewSqlParameter("QuoteStatusId", AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusId)
        );
      }

      public static List<Actions_QuotesProgramSetup> GetActions_QuotesProgramSetup(int userId) {

        var list = new List<Actions_QuotesProgramSetup>();

        Query(@"
          SELECT
            q.QuotePublicGuid, prj.ProjectName, j.JobName as ProgramName
          FROM id_Job j
          INNER JOIN al_Quote q on q.JobNumber = j.JobNumber
          INNER JOIN al_Project prj on prj.JobNumber = j.JobNumber
          CROSS APPLY (
            SELECT qi.QuoteItemId, qi.UnitPrice * qi.Quantity AS QuoteItemTotal
            FROM al_QuoteItem qi
            WHERE qi.QuoteId = q.QuoteId
          ) AS qi
          CROSS APPLY (
            SELECT SUM(cmp.ComponentPrice) AS ComponentTotalPrice
            FROM al_Component cmp
            WHERE cmp.QuoteItemId = qi.QuoteItemId
          ) AS cmp
          WHERE qi.QuoteItemTotal <> cmp.ComponentTotalPrice
            AND j.ProgramStatusId = @ProgramStatus
            AND (j.ProjectCoordinatorUserId = @UserId OR j.LeadConsultantUserId = @UserId)
          GROUP BY q.QuotePublicGuid, prj.ProjectName, j.JobName",
          dr => {
            list.Add(new Actions_QuotesProgramSetup(
              dr.GetGuid("QuotePublicGuid"),
              dr.GetString("ProjectName"),
              dr.GetString("ProgramName")
            ));
          },
          NewSqlParameter("@ProgramStatus", AlbertProgramStatus.Ids.Setup),
          NewSqlParameter("@UserId", userId)
        );

        return list;
      }

      public static List<QuoteDealSource> GetQuoteDealSources() {

        var list = new List<QuoteDealSource>();

        Query(@"
          SELECT
            qds.QuoteDealSourceId, qds.DealSourceName
          FROM al_QuoteDealSource qds
          ORDER BY qds.DealSourceName",
          dr => {
            list.Add(new QuoteDealSource(
              dr.GetInt("QuoteDealSourceId"),
              dr.GetString("DealSourceName")
            ));
          }
        );

        return list;
      }

      public static List<QuoteSalesContentType> GetSalesContentTypes() {

        var list = new List<QuoteSalesContentType>();

        Query(@"
          SELECT qsc.QuoteSalesContentTypeId, qsc.ListItemText, qsc.ListSortOrder
          FROM al_QuoteSalesContentType qsc
          ORDER BY qsc.ListSortOrder",
          dr => {
            list.Add(new QuoteSalesContentType(
              quoteSalesContentTypeId: dr.GetInt("QuoteSalesContentTypeId"),
              listItemText: dr.GetString("ListItemText"),
              listSortOrder: dr.GetInt("ListSortOrder")
            ));
          }
        );

        return list;
      }

      public static List<QuoteSalesContentUrl> GetSalesContentUrls(int orgId) {

        var list = new List<QuoteSalesContentUrl>();

        Query(@"
          SELECT qsc.QuoteSalesContentUrlId, qsc.OrgId, qsc.ListItemText, qsc.Url
          FROM al_QuoteSalesContentUrl qsc
          WHERE OrgId = @OrgId
          ORDER BY qsc.ListItemText",
          dr => {
            list.Add(new QuoteSalesContentUrl(
              quoteSalesContentUrlId: dr.GetInt("QuoteSalesContentUrlId"),
              orgId: dr.GetInt("OrgId"),
              listItemText: dr.GetString("ListItemText"),
              url: dr.GetString("Url")
            ));
          },
          NewSqlParameter("OrgId", orgId)
        );

        return list;
      }

      public static QuoteItemForSubscription GetQuoteItemsForSubscriptions(SqlTransaction trans, int quoteItemId) {
        var result = GetQuoteItemsForSubscriptions(
          trans: trans,
          whereClause: "qi.QuoteItemId = @QuoteItemId",
          sqlParameter: NewSqlParameter("QuoteItemId", quoteItemId));

        if (result == null) return null;

        return result[0];
      }

      public static List<QuoteItemForSubscription> GetQuoteItemsForSubscriptions(string jobNumber) {
        return GetQuoteItemsForSubscriptions(
          trans: null,
          whereClause: "q.JobNumber = @JobNumber",
          sqlParameter: NewSqlParameter("JobNumber", jobNumber));
      }

      private static List<QuoteItemForSubscription> GetQuoteItemsForSubscriptions(SqlTransaction trans, string whereClause, SqlParameter sqlParameter) {

        var result = new List<QuoteItemForSubscription>();

        Query(trans, $@"
          SELECT
            qi.QuoteItemId, qi.ItemDescription, qi.DisplayOrder, qi.UnitPrice,
            p.SubscriptionId, p.ProductTitle,
            CAST(ISNULL(qi.Quantity, 0) AS INT) AS AllocatedSubscriptions,
            CAST(ISNULL(ci.AssignedSubscriptions, 0) AS INT) AS AssignedSubscriptions
          FROM al_Quote q
          INNER JOIN al_QuoteItem qi ON q.QuoteId = qi.QuoteId
          INNER JOIN al_Product p ON qi.ProductId = p.ProductId

          CROSS APPLY (
            SELECT COUNT(*) AS AssignedSubscriptions
            FROM al_Component c
            WHERE c.QuoteItemId = qi.QuoteItemId and c.UserSubscriptionId IS NOT NULL
          ) AS ci
          WHERE
              q.ClientAcceptedUtc IS NOT NULL AND p.ProductCategoryId = @ProductCategoryId {whereClause.EnsureStartsWith(" AND ", true)}

          ORDER BY q.CreatedUtc DESC, qi.DisplayOrder",
          dr => {
            result.Add(new QuoteItemForSubscription(
              quoteItemId: dr.GetInt("QuoteItemId"),
              itemDescriptionHtml: dr.GetString("ItemDescription"),
              displayOrder: dr.GetInt("DisplayOrder"),
              unitPrice: dr.GetDecimalOrNull("UnitPrice") ?? 0,
              subscriptionId: dr.GetInt("SubscriptionId"),
              productTitle: dr.GetString("ProductTitle"),
              allocatedSubscriptions: dr.GetInt("AllocatedSubscriptions"),
              assignedSubscriptions: dr.GetInt("AssignedSubscriptions")
            ));
          },
          sqlParameter,
          NewSqlParameter("ProductCategoryId", (int)DbHelper.Products.ProductCategory.Subscription)
        );

        return result;
      }

      public class QuoteItemForSubscription {
        public int QuoteItemId { get; private set; }
        public string ItemDescriptionHtml { get; private set; }
        public int DisplayOrder { get; private set; }
        public decimal UnitPrice { get; private set; }
        public int SubscriptionId { get; private set; }
        public string ProductTitle { get; private set; }
        public int AvailableSubscriptions { get; private set; }

        public QuoteItemForSubscription(
          int quoteItemId,
          string itemDescriptionHtml,
          int displayOrder,
          decimal unitPrice,
          int subscriptionId,
          string productTitle,
          int allocatedSubscriptions,
          int assignedSubscriptions
        ) {
          this.QuoteItemId = quoteItemId;
          this.ItemDescriptionHtml = itemDescriptionHtml;
          this.DisplayOrder = displayOrder;
          this.UnitPrice = unitPrice;
          this.SubscriptionId = subscriptionId;
          this.ProductTitle = productTitle;
          this.AvailableSubscriptions = allocatedSubscriptions - assignedSubscriptions;
        }

      }

      public class QuoteDealSource {

        public int QuoteDealSourceId { get; }
        public string DealSourceName { get; }

        public QuoteDealSource(int quoteDealSourceId, string dealSourceName) {
          QuoteDealSourceId = quoteDealSourceId;
          DealSourceName = dealSourceName;
        }
      }

      public class QuoteSalesContentType {

        public int QuoteSalesContentTypeId { get; }
        public string ListItemText { get; }
        public int ListSortOrder { get; }

        public QuoteSalesContentType(int quoteSalesContentTypeId, string listItemText, int listSortOrder) {
          QuoteSalesContentTypeId = quoteSalesContentTypeId;
          ListItemText = listItemText;
          ListSortOrder = listSortOrder;
        }
      }

      public class QuoteSalesContentUrl {

        public int QuoteSalesContentUrlId { get; }
        public int OrgId { get; }
        public string ListItemText { get; }
        public string Url { get; }

        public QuoteSalesContentUrl(int quoteSalesContentUrlId, int orgId, string listItemText, string url) {
          QuoteSalesContentUrlId = quoteSalesContentUrlId;
          OrgId = orgId;
          ListItemText = listItemText;
          Url = url;
        }
      }

      public class Actions_QuotesProgramSetup {

        public Guid QuotePublicGuid { get; internal set; }
        public string ProjectName { get; internal set; }
        public string ProgramName { get; internal set; }

        public Actions_QuotesProgramSetup(
          Guid quotePublicGuid,
          string projectName,
          string programName
        ) {
          QuotePublicGuid = quotePublicGuid;
          ProjectName = projectName;
          ProgramName = programName;
        }
      }

      // Note can't use "internal" setters because Newtonsoft.JSON has to access these.
      public class QuoteTeamUser {
        public int UserId { get; set; }
        public Guid UserGuid { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
      }

      public class NewQuoteInfo {

        public string JobNumber { get; set; }
        public int OwnerUserId { get; set; }
        public int? LeadConsultantUserId { get; set; }
        public int? ProposalDesignerUserId { get; set; }
        public int ContactUserId { get; set; }
        public string QuoteTitle { get; set; }
        public int? BrandingOrgId { get; set; }
        public int QuoteStatusId { get; set; }
        public string QuoteStatusText { get; set; }
        public DateTime? EstimatedStartDateUtc { get; set; }
        public string XeroTaxType { get; set; }
        public bool CustomInvoicing { get; set; }
        public bool AddToFreshSales { get; set; }
        public bool ExcludeFromSalesIncentive { get; set; }
        public int? QuoteDealSourceId { get; set; }
        public decimal OPPPercentage { get; set; }
        public decimal PLCPercentage { get; set; }
        public decimal DeliveryPercentage { get; set; }
        public decimal PlatformPercentage { get; set; }
        public decimal ProposalDesignerPercentage { get; set; }
        public string CoverLetterHtml { get; set; }
        public string QuoteNotes { get; set; }

        internal NewQuoteInfo() { }

        public NewQuoteInfo(
          string jobNumber,
          int ownerUserId,
          int? leadConsultantUserId,
          int? proposalDesignerUserId,
          int contactUserId,
          string quoteTitle,
          int? brandingOrgId,
          int quoteStatusId,
          DateTime? estimatedStartDateUtc,
          string xeroTaxType,
          bool customInvoicing,
          bool addToFreshSales,
          bool excludeFromSalesIncentive,
          int? quoteDealSourceId,
          decimal oppPercentage,
          decimal plcPercentage,
          decimal deliveryPercentage,
          decimal platformPercentage,
          decimal proposalDesignerPercentage,
          string coverLetterHtml
        ) {
          JobNumber = jobNumber;
          OwnerUserId = ownerUserId;
          LeadConsultantUserId = leadConsultantUserId;
          ProposalDesignerUserId = proposalDesignerUserId;
          ContactUserId = contactUserId;
          QuoteTitle = quoteTitle;
          BrandingOrgId = brandingOrgId;
          QuoteStatusId = quoteStatusId;
          EstimatedStartDateUtc = estimatedStartDateUtc;
          XeroTaxType = xeroTaxType;
          CustomInvoicing = customInvoicing;
          AddToFreshSales = addToFreshSales;
          ExcludeFromSalesIncentive = excludeFromSalesIncentive;
          QuoteDealSourceId = quoteDealSourceId;
          OPPPercentage = oppPercentage;
          PLCPercentage = plcPercentage;
          DeliveryPercentage = deliveryPercentage;
          PlatformPercentage = platformPercentage;
          ProposalDesignerPercentage = proposalDesignerPercentage;
          CoverLetterHtml = coverLetterHtml;
        }
      }

      public class QuoteListPaged : InfoListPaged<QuoteInfo> { }

      public class QuoteInfo : NewQuoteInfo {

        public int TenantOrgId { get; internal set; }
        public Guid TenantOrgGuid { get; private set; }
        public int TenantOrgOwnerUserId { get; internal set; }

        public int QuoteId { get; internal set; }
        public DateTime CreatedUtc { get; private set; }
        public DateTime? DeletedUtc { get; set; }
        public Guid PublicGuid { get; private set; }
        public int ProjectId { get; private set; }
        public string ProjectName { get; private set; }
        public bool PurchaseOrderRequired { get; private set; }
        public string PurchaseOrderNumber { get; set; }
        public Guid? BrandingOrgGuid { get; private set; }
        public string BrandingOrgName { get; private set; }
        public int? QuoteSalesContentTypeId { get; set; }
        public int? QuoteSalesContentUrlId { get; set; }
        public string QuoteSalesContentPDFFileName { get; set; }
        public string QuoteSalesContentWebPageUrl { get; set; }
        public string QwilrUrl { get; set; }
        public string QwilrPDFUrl { get; set; }
        public DateTime? ClientAcceptedUtc { get; private set; }
        public decimal? ClientAcceptedAmount { get; private set; }
        public string ClientFirstName { get; private set; }
        public string ClientLastName { get; private set; }
        public string ClientEmailAddress { get; private set; }
        public string AccPayFirstName { get; private set; }
        public string AccPayLastName { get; private set; }
        public string AccPayEmailAddress { get; private set; }
        public int QuoteItemCount { get; private set; }
        public decimal QuoteItemTotalAmount { get; private set; }
        public ClientCompanies.BriefCompanyInfo CompanyInfo { get; private set; }
        public List<QuoteItemInfo> QuoteItems { get; internal set; }
        public List<QuoteTeamUser> QuoteTeamUsers { get; internal set; }
        public string OwnerFirstName { get; private set; }
        public string OwnerLastName { get; private set; }
        public string OwnerFullName { get; private set; }
        public string OwnerEmail { get; private set; }
        public int? ForUser_UserId { get; private set; }
        public bool ForUser_IsQuoteOwner { get; private set; }
        public bool ForUser_IsQuoteContact { get; private set; }
        public bool ForUser_IsQuoteTeamMember { get; private set; }
        public bool ForUser_IsPCOrPLC { get; private set; }
        public bool ForUser_IsInProjectAccess { get; private set; }
        public bool ForUser_IsDeliveryInProject { get; private set; }
        public bool ForUser_IsCoachInProgram { get; private set; }

        public class QuoteItemInfo {

          public int QuoteItemId { get; set; }
          public int? ProductId { get; set; }
          public string CategoryName { get; set; }
          public int? ProductCategoryId { get; set; }
          public string ProductName { get; set; }
          public string ItemDescriptionHtml { get; set; }
          public string QuoteComponentWarningMessage { get; set; }
          public decimal? UnitPrice { get; set; }
          public decimal? Quantity { get; set; }
          public string QuantityDescr { get; set; }
          public int OptionalId { get; set; }
          public OptionalEnum OptionalInfo { get; private set; }
          public bool QtySelectable { get; set; }
          public bool IsNote { get; private set; }
          public bool? IsAccepted { get; private set; }
          public bool HasLockedComponents { get; private set; }
          public bool HasUnallocatedComponentsToInvoice { get; private set; }
          public int? SubscriptionId { get; private set; }
          public bool RequiresSubscription { get; private set; }
          public bool IsQuantityPerPerson { get; private set; }
          public decimal? MinAllowedQuotePrice { get; private set; }

          public QuoteItemInfo(
            int quoteItemId, int? productId, string categoryName, int? productCategoryId, string productName, string itemDescriptionHtml, string quoteComponentWarningMessage,
            decimal? unitPrice, decimal? quantity, string quantityDescr, int optionalId, bool qtySelectable, bool? isAccepted,
            bool hasLockedComponents, bool hasUnallocatedComponentsToInvoice, int? subscriptionId, bool requiresSubscription, bool isQuantityPerPerson, decimal? minAllowedQuotePrice
          ) {
            this.QuoteItemId = quoteItemId;
            this.ProductId = productId;
            this.CategoryName = categoryName;
            this.ProductCategoryId = productCategoryId;
            this.ProductName = productName;
            this.ItemDescriptionHtml = itemDescriptionHtml;
            this.QuoteComponentWarningMessage = quoteComponentWarningMessage;
            this.UnitPrice = unitPrice;
            this.Quantity = quantity;
            this.QuantityDescr = quantityDescr;
            this.OptionalId = optionalId;
            this.OptionalInfo = OptionalEnum.GetOptionById(optionalId, OptionalEnum.No);
            this.QtySelectable = qtySelectable;
            this.IsNote = productId == null;
            this.IsAccepted = isAccepted;
            this.HasLockedComponents = hasLockedComponents;
            this.HasUnallocatedComponentsToInvoice = hasUnallocatedComponentsToInvoice;
            this.SubscriptionId = subscriptionId;
            this.RequiresSubscription = requiresSubscription;
            this.IsQuantityPerPerson = isQuantityPerPerson;
            this.MinAllowedQuotePrice = minAllowedQuotePrice;
          }

          public bool IsSubscription => this.ProductCategoryId == (int)DbHelper.Products.ProductCategory.Subscription;
          public decimal? PriceXQty(decimal? defaultIfNull) => UnitPrice == null || Quantity == null ? defaultIfNull : (UnitPrice * Quantity);
        }

        internal QuoteInfo() {
          CreatedUtc = DateTime.UtcNow;
          CompanyInfo = new ClientCompanies.BriefCompanyInfo();
          QuoteItems = new List<QuoteItemInfo>();
        }

        internal QuoteInfo(

          int tenantOrgId,
          Guid tenantOrgGuid,
          int tenantOrgOwnerUserId,

          int quoteId,
          DateTime createdUtc,
          Guid publicGuid,
          DateTime? deletedUtc,
          string jobNumber,
          int ownerUserId,
          int? leadConsultantUserId,
          int? proposalDesignerUserId,
          int contactUserId,

          int projectId,
          string projectName,
          bool purchaseOrderRequired,
          string purchaseOrderNumber,
          string quoteTitle,
          int? brandingOrgId,
          Guid? brandingOrgGuid,
          string brandingOrgName,
          int quoteStatusId,
          string quoteStatusText,
          DateTime? estimatedStartDateUtc,
          string xeroTaxType,
          bool customInvoicing,
          bool addToFreshSales,
          decimal oppPercentage,
          decimal plcPercentage,
          decimal deliveryPercentage,
          decimal platformPercentage,
          decimal proposalDesignerPercentage,
          string coverLetterHtml,
          DateTime? clientAcceptedUtc,
          decimal? clientAcceptedAmount,
          string clientFirstName,
          string clientLastName,
          string clientEmailAddress,
          string accPayFirstName,
          string accPayLastName,
          string accPayEmailAddress,
          string quoteNotes,
          bool excludeFromSalesIncentive,
          int? quoteDealSourceId,
          int? quoteSalesContentTypeId,
          int? quoteSalesContentUrlId,
          string quoteSalesContentPDFFileName,
          string quoteSalesContentWebPageUrl,
          string qwilrUrl,
          string qwilrPDFUrl,
          int quoteItemCount,
          decimal quoteItemTotalAmount,
          ClientCompanies.BriefCompanyInfo companyInfo,
          string ownerFirstName,
          string ownerLastName,
          string ownerFullName,
          string ownerEmail,
          int? forUser_UserId,
          bool forUser_IsQuoteOwner,
          bool forUser_IsQuoteContact,
          bool forUser_IsQuoteTeamMember,
          bool forUser_IsPCOrPLC,
          bool forUser_IsInProjectAccess,
          bool forUser_IsDeliveryInProject,
          bool forUser_IsCoachInProgram
        ) {
          TenantOrgId = tenantOrgId;
          TenantOrgGuid = tenantOrgGuid;
          TenantOrgOwnerUserId = tenantOrgOwnerUserId;

          QuoteId = quoteId;
          CreatedUtc = createdUtc;
          DeletedUtc = deletedUtc;
          JobNumber = jobNumber;
          OwnerUserId = ownerUserId;
          LeadConsultantUserId = leadConsultantUserId;
          ProposalDesignerUserId = proposalDesignerUserId;
          ContactUserId = contactUserId;
          ProjectId = projectId;
          ProjectName = projectName;
          PurchaseOrderRequired = purchaseOrderRequired;
          PurchaseOrderNumber = purchaseOrderNumber;
          QuoteTitle = quoteTitle;
          BrandingOrgId = brandingOrgId;
          BrandingOrgGuid = brandingOrgGuid;
          BrandingOrgName = brandingOrgName;
          PublicGuid = publicGuid;
          QuoteStatusId = quoteStatusId;
          QuoteStatusText = quoteStatusText;
          EstimatedStartDateUtc = estimatedStartDateUtc;
          XeroTaxType = xeroTaxType;
          CustomInvoicing = customInvoicing;
          AddToFreshSales = addToFreshSales;
          CompanyInfo = companyInfo;
          OPPPercentage = oppPercentage;
          PLCPercentage = plcPercentage;
          DeliveryPercentage = deliveryPercentage;
          PlatformPercentage = platformPercentage;
          ProposalDesignerPercentage = proposalDesignerPercentage;
          CoverLetterHtml = coverLetterHtml;
          ClientAcceptedUtc = clientAcceptedUtc;
          ClientAcceptedAmount = clientAcceptedAmount;

          ClientFirstName = clientFirstName;
          ClientLastName = clientLastName;
          ClientEmailAddress = clientEmailAddress;
          AccPayFirstName = accPayFirstName;
          AccPayLastName = accPayLastName;
          AccPayEmailAddress = accPayEmailAddress;
          QuoteNotes = quoteNotes;
          ExcludeFromSalesIncentive = excludeFromSalesIncentive;
          QuoteDealSourceId = quoteDealSourceId;

          QuoteSalesContentTypeId = quoteSalesContentTypeId;
          QuoteSalesContentUrlId = quoteSalesContentUrlId;
          QuoteSalesContentPDFFileName = quoteSalesContentPDFFileName;
          QuoteSalesContentWebPageUrl = quoteSalesContentWebPageUrl;
          QwilrUrl = qwilrUrl;
          QwilrPDFUrl = qwilrPDFUrl;

          QuoteItemCount = quoteItemCount;
          QuoteItemTotalAmount = quoteItemTotalAmount;

          QuoteItems = new List<QuoteItemInfo>();
          QuoteTeamUsers = new List<QuoteTeamUser>();
          CompanyInfo = companyInfo ?? new ClientCompanies.BriefCompanyInfo();

          OwnerFirstName = ownerFirstName;
          OwnerLastName = ownerLastName;
          OwnerFullName = ownerFullName;
          OwnerEmail = ownerEmail;

          ForUser_UserId = forUser_UserId;
          ForUser_IsQuoteOwner = forUser_IsQuoteOwner;
          ForUser_IsQuoteContact = forUser_IsQuoteContact;
          ForUser_IsQuoteTeamMember = forUser_IsQuoteTeamMember;
          ForUser_IsPCOrPLC = forUser_IsPCOrPLC;
          ForUser_IsInProjectAccess = forUser_IsInProjectAccess;
          ForUser_IsDeliveryInProject = forUser_IsDeliveryInProject;
          ForUser_IsCoachInProgram = forUser_IsCoachInProgram;
        }

        public bool IsUserTeamMember(int userId) {
          if (QuoteTeamUsers == null || QuoteTeamUsers.Count == 0) return false;
          return QuoteTeamUsers.Exists(x => x.UserId == userId);
        }

        public void SetProject(Projects.ProjectInfo projectInfo) {
          ProjectId = projectInfo?.ProjectId ?? 0;
          ProjectName = projectInfo?.ProjectName;
          JobNumber = projectInfo?.JobNumber;
          QuoteTitle = projectInfo.ProjectName;
        }

        public void SetCompanyInfo(ClientCompanies.BriefCompanyInfo companyInfo) {
          CompanyInfo = companyInfo;
        }

        public void AddQuoteItem(QuoteItemInfo quoteItemInfo) {
          QuoteItems.Add(quoteItemInfo);
        }

        public bool IsAccepted => ClientAcceptedUtc != null || QuoteStatusId == AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusId;
        public bool IsLost => QuoteStatusId == AbleQuoteStatus.GetStatus(AbleQuoteStatus.AppTagEnum.lost).QuoteStatusId;
        public bool HasLockedComponents => QuoteItems != null && QuoteItems.Find(item => item.HasLockedComponents) != null;

        // Delegate properties for Intercom integration
        public int? CompanyId => CompanyInfo?.CompanyId;
        public string CompanyName => CompanyInfo?.CompanyName;
      }

    }
  }
}
