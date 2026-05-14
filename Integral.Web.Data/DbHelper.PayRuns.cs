using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class PayRuns {

      public static List<PayRunSelectItem> GetExistingPayRunSelectItems(int userId) {

        var result = new List<PayRunSelectItem>();

        Common.Query(@"
          SELECT
            pr.PayRunId, pr.AdjustmentsEndDateUtc,
            LEAD(pr.AdjustmentsEndDateUtc, 1) OVER (ORDER BY pr.ProcessedDateUtc DESC) AS PreviousAdjustmentsEndDateUtc
          FROM id_PayRun pr
          INNER JOIN id_PayRunItems pri ON pr.PayRunId = pri.PayRunId
          WHERE pri.ForUserId = @CoachUserId
          GROUP BY pr.PayRunId, pr.StartDateUtc, pr.AdjustmentsEndDateUtc, pr.ProcessedDateUtc, pr.TotalAmount
          ORDER BY pr.ProcessedDateUtc DESC",
          dr => {
            result.Add(new PayRunSelectItem(
              payRunId: dr.GetInt("PayRunId"),
              adjustmentsEndDateUtc: dr.GetDateTime("AdjustmentsEndDateUtc"),
              previousAdjustmentsEndDateUtc: dr.GetDateTimeOrNull("PreviousAdjustmentsEndDateUtc")
            ));
          }, Common.NewSqlParameter("CoachUserId", userId)
        );

        return result;
      }

      public static PayRunInfo GetPayRunInfo(int payRunId) {

        PayRunInfo result = null;

        // Note, StartDate in the db is not the actual period start, so the previous
        // pay run's AdjustmentsEndDateUtc is used instead (regardless of userid).
        Common.Query(@"
          SELECT
            pr.PayRunId, pr.StartDateUtc, pr.AdjustmentsEndDateUtc, pr.ProcessedDateUtc, pr.TotalAmount,
            ( SELECT MAX(ppr.AdjustmentsEndDateUtc)
              FROM id_PayRun ppr
              WHERE ppr.AdjustmentsEndDateUtc < pr.AdjustmentsEndDateUtc
            ) AS PreviousAdjustmentsEndDateUtc
          FROM id_PayRun pr
          WHERE pr.PayRunId = @PayRunId",
          dr => {
            result = new PayRunInfo(
              payRunId: dr.GetInt("PayRunId"),
              startDateUtc: dr.GetDateTime("StartDateUtc"),
              adjustmentsEndDateUtc: dr.GetDateTime("AdjustmentsEndDateUtc"),
              previousAdjustmentsEndDateUtc: dr.GetDateTimeOrNull("PreviousAdjustmentsEndDateUtc"),
              processedDateUtc: dr.GetDateTimeOrNull("ProcessedDateUtc"),
              totalAmount: dr.GetDecimalOrNull("TotalAmount")
            );
          },
          Common.NewSqlParameter("PayRunId", payRunId),
          Common.NewSqlParameter("PayRunPeriodDays", ConfigHelper.PayRun.FixedPeriodDays));

        return result;
      }

      public static DateTime? GetLatestProcessedDateUtc(int userId) {

        DateTime? result = null;

        Common.Query(@"
          SELECT MAX(pr.ProcessedDateUtc) AS LatestProcessedDateUtc
          FROM id_PayRunItems pri
          INNER JOIN id_PayRun pr ON pri.PayRunId = pr.PayRunId
          WHERE pri.ForUserId = @UserId",
          dr => {
            result = dr.GetDateTimeOrNull("LatestProcessedDateUtc");
          },
          Common.NewSqlParameter("UserId", userId)
        );

        return result;
      }

      // Note that pay runs occur on the same dates for all partners, so this does not depend on userid.
      public static DateTime GetLatestPayRunPeriodEndUtc() {

        // AdjustmentsEndDate is used as the pay run period end date.
        return (DateTime)Common.GetScalarQuery(@"SELECT MAX(AdjustmentsEndDateUtc) FROM id_PayRun");
      }

      // Get dates for an upcoming pay run period.
      // "upcomingPeriodNumber" is in the range 1 (next) to ConfigHelper.PayRun.MaxUpcomingPeriods.
      // Initial date of reference is the latest pay run's adjustment end date.
      // Note "dataPeriodStart/End" are separate outputs because the date range for querying upcoming components may differ a bit.
      public static void GetUpcomingPayRunPeriodDates(
        byte upcomingPeriodNumber,
        out DateTime previousPeriodEndUtc, out DateTime thisPeriodEndUtc,
        out DateTime dataPeriodStartUtc, out DateTime dataPeriodEndUtc) {

        // Limit upcomingPeriodNumber to valid range.
        if (upcomingPeriodNumber < 1) upcomingPeriodNumber = 1;
        if (upcomingPeriodNumber > ConfigHelper.PayRun.MaxUpcomingPeriods) upcomingPeriodNumber = ConfigHelper.PayRun.MaxUpcomingPeriods;

        // Get latest AdjustmentsEndDate in the system - upcoming dates are based on this.
        // Note pay runs occur at the same time for all partners, so the latest adjustment date is regardless of userid.
        DateTime latestAdjustmentsEndDateUtc = GetLatestPayRunPeriodEndUtc();

        // Get desired upcoming period based on above, in steps of the pay run FixedPeriodDays.
        previousPeriodEndUtc = latestAdjustmentsEndDateUtc.AddDays((upcomingPeriodNumber - 1) * ConfigHelper.PayRun.FixedPeriodDays);
        thisPeriodEndUtc = previousPeriodEndUtc.AddDays(ConfigHelper.PayRun.FixedPeriodDays);

        // Period to get components from the db is usually the same...
        dataPeriodStartUtc = previousPeriodEndUtc;
        dataPeriodEndUtc = thisPeriodEndUtc;

        // ... except for the first upcoming period, in which case include a lag before the start.
        if (upcomingPeriodNumber == 1) {
          dataPeriodStartUtc = latestAdjustmentsEndDateUtc.AddDays(-ConfigHelper.PayRun.UpcomingPeriod1_StartDateLagDays);
        }
      }

      public static List<UpcomingPLCSalesInfo> GetUpcomingSalesAndPLC(int partnerUserId, DateTime afterDateUtc) {

        var results = new List<UpcomingPLCSalesInfo>();

        Common.Query(@"
          WITH component
          AS (
            SELECT 1 AS GroupSort,
              ac.ProgramJobId,
              ac.CoachUserId AS ItemUserId,
              ac.FirstName + ' ' + ac.LastName AS ItemName,
              cs.CoachingSessionId, NULL AS WorkshopEventId, NULL AS ConsultingItemId,
              cs.ApptDateUTC AS ItemDateUtc,
              IIF(ac.SessionsAllocated = 0, 0, cp.CoachingRevenue / ac.SessionsAllocated) AS ItemRevenue
            FROM id_CoachingSession cs
            INNER JOIN al_Coachees ac ON cs.AbleCoacheeId = ac.CoacheeId
            OUTER APPLY (SELECT SUM(cp.ComponentPrice) AS CoachingRevenue FROM al_Component cp WHERE cp.CoacheeId = ac.CoacheeId) AS cp
            WHERE cs.ApptDateUTC > @AfterDateUtc
              AND ac.SessionsAllocated > 0
              AND cp.CoachingRevenue > 0
              AND ac.DeletedUtc IS NULL
            UNION
            SELECT 2 AS GroupSort,
              we.ProgramJobId,
              we.KeyFacilitatorUserId AS ItemUserId,
              we.WorkshopTitle AS ItemName,
              NULL, we.WorkshopEventId, NULL,
              we.StartDate AS ItemDateUtc,
              we.WorkshopRevenue AS ItemRevenue
            FROM ev_WorkshopEvent we
            WHERE we.StartDate > @AfterDateLocal
              AND we.WorkshopRevenue > 0
            UNION
            SELECT 3 AS GroupSort,
              ci.ProgramJobId,
              ci.ConsultantUserId AS ItemUserId,
              ci.ItemTitle AS ItemName,
              NULL, NULL, ci.ConsultingItemId,
              ci.CompletionDateUtc AS ItemDateUtc,
              ci.ItemAmount AS ItemRevenue
            FROM al_ConsultingItems ci
            WHERE ci.CompletionDateUtc > @AfterDateUtc
              AND ci.ItemAmount > 0
          )
          SELECT
            cmp.GroupSort, cmp.ProgramJobId, cmp.ItemUserId,
            cmp.CoachingSessionId, cmp.WorkshopEventId, cmp.ConsultingItemId,
            cmp.ItemDateUtc,
            j.JobId, j.JobNumber, j.JobName, cmp.ItemName,
            j.Partner_UserId AS SalesUserId,
            IIF(j.Partner_UserId = @UserId, cmp.ItemRevenue * j.Partner_SalesDeliveryPercentage, 0) AS SalesRevenue,
            j.LeadConsultantUserId AS PLCUserId,
            IIF(j.LeadConsultantUserId = @UserId, cmp.ItemRevenue * j.Partner_PLCPercentage, 0) AS PLCRevenue,
            sc.SvCompanyId, sc.CompanyName, prj.ProjectName
          FROM component cmp
          INNER JOIN id_Job j ON cmp.ProgramJobId = j.JobId
          LEFT OUTER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
          LEFT OUTER JOIN sv_SurveyCompany sc ON sc.SvCompanyId = j.CompanyId
          WHERE ((j.Partner_UserId = @UserId AND j.Partner_SalesDeliveryPercentage <> 0)
                  OR (j.LeadConsultantUserId = @UserId AND j.Partner_PLCPercentage <> 0))
          ORDER BY cmp.GroupSort, cmp.ItemDateUtc;",
          dr => {
            var item = new UpcomingPLCSalesInfo() {
              GroupSort = dr.GetInt("GroupSort"),
              ProgramJobId = dr.GetInt("JobId"),
              JobNumber = dr.GetString("JobNumber"),
              JobName = dr.GetString("JobName"),
              ProjectName = dr.GetString("ProjectName"),
              CompanyId = dr.GetIntOrNull("SvCompanyId"),
              CompanyName = dr.GetString("CompanyName"),
              ItemUserId = dr.GetInt("ItemUserId"),
              ItemName = dr.GetString("ItemName"),
              ItemDateUtc = dr.GetDateTime("ItemDateUtc"),
              SalesUserId = dr.GetIntOrNull("SalesUserId"),
              SalesRevenue = dr.GetDecimalOrDefault("SalesRevenue", 0),
              PLCUserId = dr.GetIntOrNull("PLCUserId"),
              PLCRevenue = dr.GetDecimalOrDefault("PLCRevenue", 0),
              CoachingSessionId = dr.GetIntOrNull("CoachingSessionId"),
              WorkshopEventId = dr.GetIntOrNull("WorkshopEventId"),
              ConsultingItemId = dr.GetIntOrNull("ConsultingItemId")
            };
            if (dr.GetIntOrNull("WorkshopEventId") != null) item.ItemDateUtc = item.ItemDateUtc.ToUniversalTime(null); // Workshop date is WST.
            results.Add(item);
          },
          Common.NewSqlParameter("UserId", partnerUserId),
          Common.NewSqlParameter("AfterDateUtc", afterDateUtc),
          Common.NewSqlParameter("AfterDateLocal", afterDateUtc.UtcToTZ(null))
        );
        return results;
      }

      public static List<PayRunItemDetail> GetUpcomingPayRunItems(AbleUser.AbleUserBasicInfo userBasicInfo, DateTime periodStartUtc, DateTime periodEndUtc) {

        // Only get upcoming data if user is included in pay runs.
        if (!userBasicInfo.IncludeInPayRuns) return null;

        var results = new List<PayRunItemDetail>();

        Common.Query($@"

          SELECT

            cmp.ComponentId, cmp.ProgramJobId, cmp.CompletedDateUtc, cmp.ComponentPrice, cmp.CoacheeId,
            cmp.CoachingSessionId, cmp.WorkshopEventId, cmp.ConsultingItemId, cmp.ProgramCostItemId,
            j.JobNumber, j.JobName AS ProgramName,
            ac.FirstName + ' ' + ac.LastName AS CoacheeFullName,
            cs.ApptDateUTC, cs.CoacheeTimeZoneIANA,
            we.WorkshopTitle, we.StartDateUtc, we.IANATimeZone,
            ci.ItemTitle AS ConsultingItemTitle,
            pci.Description AS CostItemTitle,

            NULL AS PayRunItemTypeId,
            NULL AS PayRunItemId,

            CASE WHEN cmp.PartnerUserId = @UserId
            THEN cmp.ComponentPrice * j.Partner_DeliveryPercentage
            ELSE NULL
            END AS PartnerRevenue,

            CASE WHEN j.LeadConsultantUserId = @UserId
            THEN cmp.ComponentPrice * j.Partner_PLCPercentage
            ELSE NULL
            END AS PLCRevenue,

            CASE WHEN j.Partner_UserId = @UserId
            THEN cmp.ComponentPrice * j.Partner_SalesDeliveryPercentage
            ELSE NULL
            END AS SalesRevenue

          FROM al_Component cmp
          INNER JOIN id_Job j ON j.JobId = cmp.ProgramJobId
          INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber

          LEFT JOIN id_CoachingSession cs ON cs.CoachingSessionId = cmp.CoachingSessionId
          LEFT JOIN al_Coachees ac ON ac.CoacheeId = cmp.CoacheeId
          LEFT JOIN ev_WorkshopEvent we ON we.WorkshopEventId = cmp.WorkshopEventId
          LEFT JOIN al_ConsultingItems ci ON ci.ConsultingItemId = cmp.ConsultingItemId
          LEFT JOIN al_ProgramCostItems pci ON pci.ProgramCostItemId = cmp.ProgramCostItemId

          WHERE cmp.CompletedDateUtc BETWEEN @PeriodStartUtc AND @PeriodEndUtc
            AND NOT EXISTS(SELECT NULL FROM id_PayRunItems pri WHERE pri.ComponentId = cmp.ComponentId) -- Components not yet in a pay run.
            AND cmp.QuoteItemId IS NOT NULL
            AND prj.InvoiceInstructionTypeId <> @InvoiceInstructionTypeId_NoTransaction
            AND (we.WorkshopStatusId IN (@WorkshopStatusId_Confirmed, @WorkshopStatusId_Cancelled) OR cmp.WorkshopEventId IS NULL)
            AND cmp.PartnerUserId NOT IN (@UserId_Unassigned, @UserId_Revenue_PandL, @UserId_Revenue_Lost)
            AND (
                 (cmp.PartnerUserId = @UserId      AND j.Partner_DeliveryPercentage > 0)
              OR (j.LeadConsultantUserId = @UserId AND j.Partner_PLCPercentage > 0)
              OR (j.Partner_UserId = @UserId       AND j.Partner_SalesDeliveryPercentage > 0)
            )

          ORDER BY cmp.CompletedDateUtc, cs.ApptDateUTC, we.StartDateUtc",

          dr => {
            // Add separate pay run items as needed, based on component and additional revenue types.

            // Component revenue allocated to user.
            if (dr.GetDecimalOrNull("PartnerRevenue") > 0) results.Add(new PayRunItemDetail(dr));

            // Additional revenue allocated to user, with the specific PayRunItemTypeId for each.
            if (dr.GetDecimalOrNull("PLCRevenue") > 0) results.Add(new PayRunItemDetail(dr, ConfigHelper.PayRunItemTypeId.PLC, dr.GetDecimal("PLCRevenue")));
            if (dr.GetDecimalOrNull("SalesRevenue") > 0) results.Add(new PayRunItemDetail(dr, ConfigHelper.PayRunItemTypeId.Sales_Opp, dr.GetDecimal("SalesRevenue")));
          },

          Common.NewSqlParameter("UserId", userBasicInfo.UserId),
          Common.NewSqlParameter("UserId_Unassigned", ConfigHelper.UserId.Unassigned),
          Common.NewSqlParameter("UserId_Revenue_PandL", ConfigHelper.UserId.RevenuePandL),
          Common.NewSqlParameter("UserId_Revenue_Lost", ConfigHelper.UserId.RevenueLost),
          Common.NewSqlParameter("WorkshopStatusId_Confirmed", WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId),
          Common.NewSqlParameter("WorkshopStatusId_Cancelled", WorkshopStatus.WorkshopStatus_Cancelled.WorkshopStatusId),
          Common.NewSqlParameter("InvoiceInstructionTypeId_NoTransaction", ConfigHelper.InvoiceInstructionTypeId_NoTransaction),
          Common.NewSqlParameter("PeriodStartUtc", periodStartUtc),
          Common.NewSqlParameter("PeriodEndUtc", periodEndUtc)
        );

        return results;
      }

      public static List<PayRunItemDetail> GetExistingPayRunItems(int userId, int payRunId) {

        if (userId == 0) return null;

        var results = new List<PayRunItemDetail>();

        Common.Query($@"

          SELECT

            cmp.ComponentId, cmp.ProgramJobId, cmp.CompletedDateUtc, cmp.ComponentPrice, cmp.CoacheeId,
            cmp.CoachingSessionId, cmp.WorkshopEventId, cmp.ConsultingItemId, cmp.ProgramCostItemId,
            j.JobNumber, j.JobName AS ProgramName, j.Partner_DeliveryPercentage, j.Partner_PLCPercentage, j.Partner_SalesDeliveryPercentage,
            ac.FirstName + ' ' + ac.LastName AS CoacheeFullName, cs.ApptDateUTC, cs.CoacheeTimeZoneIANA,
            w.WorkshopTitle, w.StartDateUtc, w.IANATimeZone,
            ci.ItemTitle AS ConsultingItemTitle,
            pci.Description AS CostItemTitle,

            prit.PayRunItemTypeId,
            pri.PayRunItemId,
            pri.PartnerRevenue

          FROM id_PayRunItems pri
          INNER JOIN id_PayRunItemType prit ON pri.PayRunItemTypeId = prit.PayRunItemTypeId
          INNER JOIN id_PayRun pr ON pr.PayRunId = pri.PayRunId
          INNER JOIN al_Component cmp ON cmp.ComponentId = pri.ComponentId
          INNER JOIN id_Job j ON j.JobId = cmp.ProgramJobId
          INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber

          LEFT JOIN id_CoachingSession cs ON cs.CoachingSessionId = cmp.CoachingSessionId
          LEFT JOIN al_Coachees ac ON ac.CoacheeId = cmp.CoacheeId
          LEFT JOIN ev_WorkshopEvent w ON w.WorkshopEventId = cmp.WorkshopEventId
          LEFT JOIN al_ConsultingItems ci ON ci.ConsultingItemId = cmp.ConsultingItemId
          LEFT JOIN al_ProgramCostItems pci ON pci.ProgramCostItemId = cmp.ProgramCostItemId

          WHERE pri.PayRunId = @PayRunId
            AND pri.ForUserId = @UserId

          ORDER BY cmp.CompletedDateUtc, cs.ApptDateUTC, w.StartDateUtc;",

          dr => {
            results.Add(new PayRunItemDetail(dr));
          },
          Common.NewSqlParameter("UserId", userId),
          Common.NewSqlParameter("PayrunId", payRunId)
        );
        return results;
      }

      public class PayRunSelectItem {

        public readonly int PayRunId;
        public readonly DateTime AdjustmentsEndDateUtc;
        public readonly DateTime? PreviousAdjustmentsEndDateUtc;

        // AdjustmentsEndDate is used as the pay run period end date.
        public DateTime PeriodEndUtc => AdjustmentsEndDateUtc;
        public DateTime? PreviousPeriodEndUtc => PreviousAdjustmentsEndDateUtc;

        public PayRunSelectItem(
          int payRunId,
          DateTime adjustmentsEndDateUtc,
          DateTime? previousAdjustmentsEndDateUtc
        ) {
          PayRunId = payRunId;
          AdjustmentsEndDateUtc = adjustmentsEndDateUtc;
          PreviousAdjustmentsEndDateUtc = previousAdjustmentsEndDateUtc;
        }
      }

      public class PayRunItemDetail {

        public int ComponentId { get; private set; }
        public DbHelper.ProgramComponents.ComponentTypeEnum ComponentType { get; private set; }
        public DateTime? ComponentCompletedDateUtc { get; private set; }
        public decimal? ComponentPrice { get; private set; }
        public int? CoachingSessionId { get; private set; }
        public DateTime? CoachingSessionDateUtc { get; private set; }
        public string CoacheeTimeZoneIANA { get; private set; }
        public DateTime? WorkshopStartDateUtc { get; private set; }
        public string WorkshopIANATimeZone { get; private set; }
        public int? WorkshopEventId { get; private set; }
        public int? ConsultingItemId { get; private set; }
        public int? ProgramCostItemId { get; private set; }
        public int ProgramJobId { get; private set; }
        public string ProgramName { get; private set; }
        public string JobNumber { get; private set; }
        public int? CoacheeId { get; private set; }
        public string CoacheeFullName { get; private set; }
        public string WorkshopTitle { get; private set; }
        public string ConsultingItemTitle { get; private set; }
        public string CostItemTitle { get; private set; }
        public decimal? PartnerRevenue { get; private set; }
        public decimal? SalesRevenue { get; private set; }
        public decimal? PLCRevenue { get; private set; }

        // Only set for existing pay runs. Upcoming is by components without PRIs.
        public int? PayRunItemId { get; private set; }
        public int? PayRunItemTypeId { get; private set; }

        public PayRunItemDetail(SqlDataReader dr) {

          Init(dr);

          // If PayRunItemTypeId not in resultset (i.e. for upcoming revenue), infer from component type.
          if (this.PayRunItemTypeId == null) {
            switch (this.ComponentType) {
              case ProgramComponents.ComponentTypeEnum.CoachingSession:
                this.PayRunItemTypeId = ConfigHelper.PayRunItemTypeId.Delivery_Coaching;
                break;
              case ProgramComponents.ComponentTypeEnum.Workshop:
                this.PayRunItemTypeId = ConfigHelper.PayRunItemTypeId.Delivery_Workshop;
                break;
              case ProgramComponents.ComponentTypeEnum.ConsultingItem:
                this.PayRunItemTypeId = ConfigHelper.PayRunItemTypeId.Delivery_Consulting;
                break;
              default:
                throw new ArgumentException("Only revenue component types allowed (i.e. CostItem component should not be present in the resultset).");
            }
          }
        }

        public PayRunItemDetail(SqlDataReader dr, int nonComponentPayRunItemTypeId, decimal nonComponentPartnerRevenue) {

          Init(dr);

          // PayRunItemTypeId & revenue given for upcoming non-component revenue (e.g. PLC, Sales).
          this.PayRunItemTypeId = nonComponentPayRunItemTypeId;
          this.PartnerRevenue = nonComponentPartnerRevenue;
        }

        private void Init(SqlDataReader dr) {

          this.ComponentId = dr.GetInt("ComponentId");

          this.CoachingSessionId = dr.GetIntOrNull("CoachingSessionId");
          this.WorkshopEventId = dr.GetIntOrNull("WorkshopEventId");
          this.ConsultingItemId = dr.GetIntOrNull("ConsultingItemId");
          this.ProgramCostItemId = dr.GetIntOrNull("ProgramCostItemId");
          this.ComponentType = ProgramComponents.GetComponentTypeFromIds(this.CoachingSessionId, this.WorkshopEventId, this.ConsultingItemId, this.ProgramCostItemId);

          this.ComponentCompletedDateUtc = dr.GetDateTimeOrNull("CompletedDateUtc");
          this.ComponentPrice = dr.GetDecimalOrNull("ComponentPrice");
          this.CoachingSessionDateUtc = dr.GetDateTimeOrNull("ApptDateUTC");
          this.CoacheeTimeZoneIANA = dr.GetString("CoacheeTimeZoneIANA");
          this.ProgramJobId = dr.GetInt("ProgramJobId");
          this.ProgramName = dr.GetString("ProgramName");
          this.JobNumber = dr.GetString("JobNumber");
          this.CoacheeId = dr.GetIntOrNull("CoacheeId");
          this.CoacheeFullName = dr.GetString("CoacheeFullName");
          this.WorkshopTitle = dr.GetString("WorkshopTitle");
          this.WorkshopStartDateUtc = dr.GetDateTimeOrNull("StartDateUtc");
          this.WorkshopIANATimeZone = dr.GetString("IANATimeZone");
          this.ConsultingItemTitle = dr.GetString("ConsultingItemTitle");
          this.CostItemTitle = dr.GetString("CostItemTitle");
          this.PartnerRevenue = dr.GetDecimalOrNull("PartnerRevenue");
          this.PayRunItemId = dr.GetIntOrNull("PayRunItemId");
          this.PayRunItemTypeId = dr.GetIntOrNull("PayRunItemTypeId");
        }
      }

      public class UpcomingPLCSalesInfo {

        public int GroupSort { get; internal set; }
        public int ProgramJobId { get; internal set; }
        public string JobNumber { get; internal set; }
        public string JobName { get; internal set; }
        public string ProjectName { get; internal set; }
        public int? CompanyId { get; internal set; }
        public string CompanyName { get; set; }
        public int ItemUserId { get; internal set; }
        public string ItemName { get; set; }
        public DateTime ItemDateUtc { get; internal set; }
        public int? CoachingSessionId { get; internal set; }
        public int? WorkshopEventId { get; internal set; }
        public int? ConsultingItemId { get; internal set; }
        public int? SalesUserId { get; internal set; }
        public decimal SalesRevenue { get; internal set; }
        public int? PLCUserId { get; internal set; }
        public decimal PLCRevenue { get; internal set; }

      }

      public class PayRunInfo {

        public int PayRunId { get; private set; }
        public DateTime StartDateUtc { get; private set; }
        public DateTime AdjustmentsEndDateUtc { get; private set; }
        public DateTime? PreviousAdjustmentsEndDateUtc { get; private set; }
        public DateTime? ProcessedDateUtc { get; private set; }
        public decimal? TotalAmount { get; private set; }

        // AdjustmentsEndDate is used as the pay run period end date.
        public DateTime PeriodEndUtc => AdjustmentsEndDateUtc;
        public DateTime? PreviousPeriodEndUtc => PreviousAdjustmentsEndDateUtc;

        public PayRunInfo(
          int payRunId,
          DateTime startDateUtc,
          DateTime adjustmentsEndDateUtc,
          DateTime? previousAdjustmentsEndDateUtc,
          DateTime? processedDateUtc,
          decimal? totalAmount
        ) {
          this.PayRunId = payRunId;
          this.StartDateUtc = startDateUtc;
          this.AdjustmentsEndDateUtc = adjustmentsEndDateUtc;
          this.PreviousAdjustmentsEndDateUtc = previousAdjustmentsEndDateUtc;
          this.ProcessedDateUtc = processedDateUtc;
          this.TotalAmount = totalAmount;
        }
      }

      public class PayRunItemInfo {

        public int PayRunItemId { get; private set; }
        public int PayRunId { get; private set; }
        public int ForUserId { get; private set; }
        public int? CoachingSessionId { get; private set; }
        public int? WorkshopEventId { get; private set; }
        public int? ConsultingItemId { get; private set; }
        public decimal TotalRevenue { get; private set; }
        public decimal PartnerRevenue { get; private set; }
        public DateTime PayRunStartDateUtc { get; private set; }
        public decimal? PayRunTotalAmount { get; private set; }

        public PayRunItemInfo(
          int payRunItemId,
          int payRunId,
          int forUserId,
          int? coachingSessionId,
          int? workshopEventId,
          int? consultingItemId,
          decimal totalRevenue,
          decimal partnerRevenue,
          DateTime payRunStartDateUtc,
          decimal? payRunTotalAmount
        ) {
          this.PayRunItemId = payRunItemId;
          this.PayRunId = payRunId;
          this.ForUserId = forUserId;
          this.CoachingSessionId = coachingSessionId;
          this.WorkshopEventId = workshopEventId;
          this.ConsultingItemId = consultingItemId;
          this.TotalRevenue = totalRevenue;
          this.PartnerRevenue = partnerRevenue;
          this.PayRunStartDateUtc = payRunStartDateUtc;
          this.PayRunTotalAmount = payRunTotalAmount;
        }
      }

    }
  }
}
