using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using static Integral.Web.DbHelper.Common;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class TenantOrg {

      public static List<TenantOrgInfo> GetQuoteBrandingOrgs(AbleUser.UserIdentity forUser, int? currentBrandingOrgId = null) {

        string sqlWhere = "(";
        sqlWhere += $"org.OrgId = {ConfigHelper.AbleOrgId} OR org.OrgId = {forUser.OrgId}";
        if (currentBrandingOrgId != null) {
          sqlWhere += $" OR org.OrgId = {currentBrandingOrgId}";
        }
        sqlWhere += ")";

        var orgList = GetTenantOrgListPaged(
          new TenantOrgListPagedOpts() {
            SqlWhereConditions = sqlWhere,
            SqlOrderBy = "org.OrgName"
          });

        if (orgList == null || orgList.InfoList.Count == 0) return null;

        return orgList.InfoList;
      }

      public static List<TenantOrgInfo> GetAllTenantOrgs() {
        return GetTenantOrgListPaged(
          new TenantOrgListPagedOpts() {
            SqlOrderBy = "org.OrgName"
          }
        ).InfoList;
      }

      public static TenantOrgInfo GetTenantOrgById(int tenantOrgId) {
        var orgList = GetTenantOrgListPaged(
          new TenantOrgListPagedOpts() {
            TopOrNullForAll = 1,
            SqlWhereConditions = $@"org.OrgId = @OrgId",
          },
          NewSqlParameter("OrgId", tenantOrgId)
        );

        if (orgList == null || orgList.InfoList.Count == 0) return null;

        return orgList.InfoList[0];
      }

      public static TenantOrgInfo GetTenantOrgByGuid(Guid orgGuid) {

        var orgList = GetTenantOrgListPaged(
          new TenantOrgListPagedOpts() {
            TopOrNullForAll = 1,
            SqlWhereConditions = $@"org.OrgGuid = @OrgGuid",
          },
          NewSqlParameter("OrgGuid", orgGuid)
        );

        if (orgList == null || orgList.InfoList.Count == 0) return null;

        return orgList.InfoList[0];
      }

      public static TenantOrgInfo GetCompanyByName(SqlTransaction trans, string orgName) {

        var orgList = GetTenantOrgListPaged(
          new TenantOrgListPagedOpts() {
            Trans = trans,
            TopOrNullForAll = 1,
            SqlWhereConditions = $@"org.OrgName = @OrgName",
          },
          NewSqlParameter("OrgName", orgName)
        );

        if (orgList == null || orgList.InfoList.Count == 0) return null;

        return orgList.InfoList[0];
      }

      public static bool TenantOrgNameExists(string orgName) {

        return GetScalarQueryInt(@"
          SELECT IIF(EXISTS(
            SELECT 1
            FROM sv_Organisation
            WHERE OrgName = @OrgName
          ), 1, 0)",
          NewSqlParameter("OrgName", orgName)) > 0;
      }

      private struct TenantOrgListPagedOpts {
        public SqlTransaction Trans;
        public int? TopOrNullForAll;
        public string SqlExtraJoins;
        public string SqlWhereConditions;
        public string SqlOrderBy;
        public int? OffsetRows;
        public int? FetchRows;
      }

      private static TenantOrgListPaged GetTenantOrgListPaged(
        TenantOrgListPagedOpts opts,
        params SqlParameter[] sqlWhereParams) {

        var infoPaged = new TenantOrgListPaged();

        string sqlTop = opts.TopOrNullForAll == null ? "" : ("TOP " + opts.TopOrNullForAll);
        string sql = $@"
          SELECT {sqlTop}
            COUNT(*) OVER() AS TotalRows,
            org.OrgId, org.OrgGuid, org.OrgName, org.OrgFriendlyName, org.OrgPhone, org.OrgEmail,
            org.BusinessIDNumber, org.WebSiteURL,
            org.OrgOwnerUserId, org.PlatformFeePercent,
            org.GenericSenderEmailName, org.GenericSenderEmailAddress,
            org.StripeCustomerId{TestColumnSuffix},
            org.StripeSubscriptionId{TestColumnSuffix}
          FROM sv_Organisation org
          {opts.SqlExtraJoins.EmptyIfNull()}
          {opts.SqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {opts.SqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        if (sqlTop.IsNullOrEmpty() && !opts.SqlOrderBy.IsNullOrEmpty() && opts.OffsetRows >= 0 && opts.FetchRows > 0) {
          infoPaged.OffsetRows = opts.OffsetRows;
          infoPaged.FetchRows = opts.FetchRows;
          sql += $" OFFSET {opts.OffsetRows} ROWS FETCH NEXT {opts.FetchRows} ROWS ONLY";
        }

        if (ConfigHelper.IsDevServer) infoPaged.SqlText = sql;

        Query(opts.Trans, sql,
          dr => {
            if (infoPaged.TotalRows == 0) infoPaged.TotalRows = dr.GetInt("TotalRows");
            infoPaged.InfoList.Add(new TenantOrgInfo(
              orgId: dr.GetInt("OrgId"),
              orgGuid: dr.GetGuid("OrgGuid"),
              orgName: dr.GetString("OrgName"),
              friendlyName: dr.GetString("OrgFriendlyName"),
              businessIDNumber: dr.GetString("BusinessIDNumber"),
              email: dr.GetString("OrgEmail"),
              phone: dr.GetString("OrgPhone"),
              webSiteURL: dr.GetString("WebSiteURL"),
              ownerUserId: dr.GetIntOrNull("OrgOwnerUserId"),
              genericSenderEmailName: dr.GetString("GenericSenderEmailName"),
              genericSenderEmailAddress: dr.GetString("GenericSenderEmailAddress"),
              stripeCustomerId: dr.GetString($"StripeCustomerId{TestColumnSuffix}"),
              stripeCustomerSubscriptionId: dr.GetString($"StripeSubscriptionId{TestColumnSuffix}")
            ));
          },
          sqlWhereParams
        );

        return infoPaged;
      }

      public static int CreateTenantOrg(SqlTransaction trans, TenantOrgInfo newOrgInfo) {

        if (newOrgInfo == null) throw new ArgumentException("newOrgInfo is null.");

        newOrgInfo.OrgId = GetScalarQueryInt(trans, $@"
          INSERT INTO sv_Organisation (OrgName, OrgFriendlyName, OrgPhone, OrgEmail, BusinessIDNumber, WebSiteURL, OrgOwnerUserId, GenericSenderEmailName, GenericSenderEmailAddress)
          OUTPUT INSERTED.OrgId
          VALUES (@OrgName, @OrgFriendlyName, @OrgPhone, @OrgEmail, @BusinessIDNumber, @WebSiteURL, @OrgOwnerUserId, @GenericSenderEmailName, @GenericSenderEmailAddress)",
          NewSqlParameter("OrgName", newOrgInfo.OrgName),
          NewSqlParameter("OrgFriendlyName", newOrgInfo.OrgFriendlyName),
          NewSqlParameter("OrgPhone", newOrgInfo.OrgPhone),
          NewSqlParameter("OrgEmail", newOrgInfo.OrgEmail),
          NewSqlParameter("BusinessIDNumber", newOrgInfo.BusinessIDNumber),
          NewSqlParameter("WebSiteURL", newOrgInfo.WebSiteURL),
          NewSqlParameter("OrgOwnerUserId", newOrgInfo.OrgOwnerUserId),
          NewSqlParameter("GenericSenderEmailName", newOrgInfo.GenericSenderEmailName),
          NewSqlParameter("GenericSenderEmailAddress", newOrgInfo.GenericSenderEmailAddress)
        );
        return newOrgInfo.OrgId;
      }

      public static bool UpdateCompany(SqlTransaction trans, TenantOrgInfo orgInfo) {

        if (orgInfo == null) throw new ArgumentException("orgInfo is null.");

        var rows = GetNonQueryInt(trans, @"
          UPDATE sv_Organisation SET
            OrgName = @OrgName,
            OrgFriendlyName = @OrgFriendlyName,
            OrgPhone = @OrgPhone,
            OrgEmail = @OrgEmail,
            BusinessIDNumber = @BusinessIDNumber,
            WebSiteURL = @WebSiteURL,
            OrgOwnerUserId = @OrgOwnerUserId,
            GenericSenderEmailName = @GenericSenderEmailName,
            GenericSenderEmailAddress = @GenericSenderEmailAddress
          WHERE OrgId = @OrgId",

          NewSqlParameter("OrgId", orgInfo.OrgId),

          NewSqlParameter("OrgName", orgInfo.OrgName),
          NewSqlParameter("OrgFriendlyName", orgInfo.OrgFriendlyName),
          NewSqlParameter("OrgPhone", orgInfo.OrgPhone),
          NewSqlParameter("OrgEmail", orgInfo.OrgEmail),
          NewSqlParameter("BusinessIDNumber", orgInfo.BusinessIDNumber),
          NewSqlParameter("WebSiteURL", orgInfo.WebSiteURL),
          NewSqlParameter("OrgOwnerUserId", orgInfo.OrgOwnerUserId),
          NewSqlParameter("GenericSenderEmailName", orgInfo.GenericSenderEmailName),
          NewSqlParameter("GenericSenderEmailAddress", orgInfo.GenericSenderEmailAddress)
        );

        return rows == 1;
      }

      public static bool UpdateStripeCustomerId(SqlTransaction trans, TenantOrgInfo orgInfo, string stripeCustomerId) {

        if (orgInfo == null) throw new ArgumentException("orgInfo is null.");

        bool updated = GetNonQueryInt(trans, $@"
          UPDATE sv_Organisation
          SET StripeCustomerId{TestColumnSuffix}  = @StripeCustomerId
          WHERE OrgId = @OrgId",
          NewSqlParameter("OrgId", orgInfo.OrgId),
          NewSqlParameter("StripeCustomerId", stripeCustomerId)
        ) == 1;

        if (updated) {
          orgInfo.StripeCustomerId = stripeCustomerId; // Update object
        }

        return updated;
      }

      public static bool UpdateStripeSubscriptionId(SqlTransaction trans, TenantOrgInfo orgInfo, string stripeCustomerSubscriptionId) {

        if (orgInfo == null) throw new ArgumentException("orgInfo is null.");

        bool updated = GetNonQueryInt(trans, $@"
          UPDATE sv_Organisation
          SET StripeSubscriptionId{TestColumnSuffix} = @StripeSubscriptionId
          WHERE OrgId = @OrgId",
          NewSqlParameter("OrgId", orgInfo.OrgId),
          NewSqlParameter("StripeSubscriptionId", stripeCustomerSubscriptionId)
        ) == 1;

        if (updated) {
          orgInfo.StripeCustomerSubscriptionId = stripeCustomerSubscriptionId;
        }

        return updated;
      }

      // Obviously direct deletion will fail if there are dependent rows in other tables.
      public static bool DeleteCompany(int tenantOrgId) {
        int rows = GetNonQueryInt(@"
          DELETE FROM sv_Organisation
          WHERE OrgId = @OrgId",
          NewSqlParameter("OrgId", tenantOrgId));
        return rows == 1;
      }

      public class TenantOrgListPaged : InfoListPaged<TenantOrgInfo> { }

      public class TenantOrgInfo {

        // TODO: OwnerUserId should be not null, as technically every Org requires an owner.
        //       There will need to be a data fix for that, to set an owner for every Org, then change it to not null.

        public int OrgId { get; internal set; }
        public Guid OrgGuid { get; internal set; }
        public string OrgName { get; set; }
        public string OrgFriendlyName { get; set; }
        public string BusinessIDNumber { get; set; }
        public string OrgEmail { get; set; }
        public string OrgPhone { get; set; }
        public string WebSiteURL { get; set; }
        public int? OrgOwnerUserId { get; set; }
        public string GenericSenderEmailName { get; set; }
        public string GenericSenderEmailAddress { get; set; }
        public string StripeCustomerId { get; internal set; }
        public string StripeCustomerSubscriptionId { get; internal set; }

        public TenantOrgInfo() { }

        public TenantOrgInfo(
          string orgName,
          int? ownerUserId
        ) {
          this.OrgName = orgName;
          this.OrgOwnerUserId = ownerUserId;
        }

        public TenantOrgInfo(
          int orgId,
          Guid orgGuid,
          string orgName,
          string friendlyName,
          string businessIDNumber,
          string email,
          string phone,
          string webSiteURL,
          int? ownerUserId,
          string genericSenderEmailName,
          string genericSenderEmailAddress,
          string stripeCustomerId,
          string stripeCustomerSubscriptionId
        ) {
          this.OrgId = orgId;
          this.OrgGuid = orgGuid;
          this.OrgName = orgName;

          this.OrgFriendlyName = friendlyName;
          this.BusinessIDNumber = businessIDNumber;
          this.OrgEmail = email;
          this.OrgPhone = phone;
          this.WebSiteURL = webSiteURL;
          this.OrgOwnerUserId = ownerUserId;

          this.GenericSenderEmailName = genericSenderEmailName;
          this.GenericSenderEmailAddress = genericSenderEmailAddress;

          this.StripeCustomerId = stripeCustomerId;
          this.StripeCustomerSubscriptionId = stripeCustomerSubscriptionId;
        }

        public TenantOrgInfo(
          string orgName,
          string friendlyName,
          string businessIDNumber,
          string email,
          string phone,
          string webSiteURL,
          int? ownerUserId,
          string genericSenderEmailName,
          string genericSenderEmailAddress
        ) {
          this.OrgName = orgName;
          this.OrgFriendlyName = friendlyName;
          this.BusinessIDNumber = businessIDNumber;
          this.OrgEmail = email;
          this.OrgPhone = phone;
          this.WebSiteURL = webSiteURL;
          this.OrgOwnerUserId = ownerUserId;
          this.GenericSenderEmailName = genericSenderEmailName;
          this.GenericSenderEmailAddress = genericSenderEmailAddress;
        }
      }

    }
  }
}
