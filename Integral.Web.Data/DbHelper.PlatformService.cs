using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class PlatformService {

      public const int PlatformBaseFeeId = 1;

      private const string TblPfx = "pls";
      private const string LinkTblPfx = "qpls";

      public class PlatformServiceFeeIds {
        public const int PlatformBaseFee = 1;
        public const int PartnerNetwork = 2;
        public const int IntegralClientOwner = 3;
        public const int RTO_Project = 4;
        public const int ProposalDesigner = 5;
        public const int DB_Support = 6;
        public const int CoordinationSupport = 7;
        public const int InternalUseOnly = 8;
      }

      public static ServiceInfo GetServiceInfoOrNull(int serviceId) {
        var services = GetServiceListPaged(1, "",
          $"{TblPfx}.PlatformServiceId = @PlatformServiceId", "", null, null,
          Common.NewSqlParameter("PlatformServiceId", serviceId));
        if (services.InfoList.Count == 0) return null;
        return services.InfoList[0];
      }

      public static List<ServiceInfo> GetAllServices() {
        var serviceList = GetServiceListPaged(null, "",
          $"{TblPfx}.ServiceDisabledUtc IS NULL",
          $"{TblPfx}.UISortOrder",
          null, null
        );
        if (serviceList == null || serviceList.InfoList == null || serviceList.InfoList.Count == 0) return null;
        return serviceList.InfoList;
      }

      public static List<ServiceInfo> GetServicesForQuote(int quoteId) {
        var serviceList = GetServiceListPaged(null,
          $"INNER JOIN al_QuotePlatformServices {LinkTblPfx} ON {LinkTblPfx}.PlatformServiceId = {TblPfx}.PlatformServiceId",
          $"{LinkTblPfx}.QuoteId = @QuoteId",
          $"{TblPfx}.UISortOrder",
          null, null,
          Common.NewSqlParameter("QuoteId", quoteId)
        );
        if (serviceList == null || serviceList.InfoList == null || serviceList.InfoList.Count == 0) return null;
        return serviceList.InfoList;
      }

      private static ServiceListPaged GetServiceListPaged(
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        int? offsetRows, int? fetchRows,
        params SqlParameter[] sqlWhereParams) {

        var infoPaged = new ServiceListPaged();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);
        string sql = $@"
          SELECT {sqlTop}
            COUNT(*) OVER() AS TotalRows,
            {TblPfx}.PlatformServiceId, {TblPfx}.ServiceDescription, {TblPfx}.ServiceFeePercent, {TblPfx}.ServiceDisabledUtc,
            {TblPfx}.RequiredServiceIds, {TblPfx}.UISortOrder, {TblPfx}.AlwaysRequired, {TblPfx}.IsHidden, {TblPfx}.TooltipText
          FROM al_PlatformService {TblPfx}
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        if (sqlTop.IsNullOrEmpty() && !sqlOrderBy.IsNullOrEmpty() && offsetRows >= 0 && fetchRows > 0) {
          infoPaged.OffsetRows = offsetRows;
          infoPaged.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        if (ConfigHelper.IsDevServer) infoPaged.SqlText = sql;

        Common.Query(sql, dr => {
          if (infoPaged.TotalRows == 0) infoPaged.TotalRows = dr.GetInt("TotalRows");
          infoPaged.InfoList.Add(new ServiceInfo(
            dr.GetInt("PlatformServiceId"),
            dr.GetString("ServiceDescription"),
            dr.GetDecimal("ServiceFeePercent"),
            dr.GetDateTimeOrNull("ServiceDisabledUtc"),
            dr.GetString("RequiredServiceIds"),
            dr.GetInt("UISortOrder"),
            dr.GetBoolFromInt("AlwaysRequired"),
            dr.GetBoolFromInt("IsHidden", false),
            dr.GetString("TooltipText")
          ));
        }, sqlWhereParams);

        return infoPaged;
      }

      public static void UpdateServicesForQuote(SqlTransaction trans, int quoteId, List<int> serviceIds) {

        Common.UsingTransaction(trans2 => {

          Common.GetNonQueryInt(trans ?? trans2, @"
            DELETE FROM al_QuotePlatformServices
            WHERE QuoteId = @QuoteId",
            Common.NewSqlParameter("QuoteId", quoteId));

          foreach (int serviceId in serviceIds) {
            Common.GetNonQueryInt(trans ?? trans2, @"
              INSERT INTO al_QuotePlatformServices (QuoteId, PlatformServiceId)
              VALUES (@QuoteId, @PlatformServiceId)",
              Common.NewSqlParameter("QuoteId", quoteId),
              Common.NewSqlParameter("PlatformServiceId", serviceId));
          }
          return true;
        });
      }

      public class ServiceListPaged : InfoListPaged<ServiceInfo> { }

      public class ServiceInfo {

        public int PlatformServiceId { get; private set; }
        public string ServiceDescription { get; private set; }
        public decimal ServiceFeePercent { get; private set; }
        public DateTime? ServiceDisabledUtc { get; private set; }
        public string RequiredServiceIds { get; private set; }
        public List<int> RequiredServiceIdsList { get; private set; }
        public int UISortOrder { get; private set; }
        public bool AlwaysRequired { get; private set; }
        public bool IsHidden { get; private set; }
        public string TooltipText { get; private set; }

        public ServiceInfo(
          int platformServiceId,
          string serviceDescription,
          decimal serviceFeePercent,
          DateTime? serviceDisabledUtc,
          string requiredServiceIds,
          int uiSortOrder,
          bool alwaysRequired,
          bool isHidden,
          string tooltipText
        ) {
          this.PlatformServiceId = platformServiceId;
          this.ServiceDescription = serviceDescription;
          this.ServiceFeePercent = serviceFeePercent;
          this.ServiceDisabledUtc = serviceDisabledUtc;
          this.RequiredServiceIds = requiredServiceIds;
          this.RequiredServiceIdsList = requiredServiceIds.ToIntList();
          this.UISortOrder = uiSortOrder;
          this.AlwaysRequired = alwaysRequired;
          this.IsHidden = isHidden;
          this.TooltipText = tooltipText;
        }

      }

    }
  }
}
