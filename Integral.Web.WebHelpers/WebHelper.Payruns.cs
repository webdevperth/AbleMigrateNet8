using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Integral.Web {

  public partial class WebHelper {

    public class Payruns {

      public static string GetPayRunSelectOptions(DbHelper.AbleUser.AbleUserBasicInfo userBasicInfo, int selectedPayRunId) {

        // Negative selectedPayRunId indicates an Upcoming period:
        // -1 = upcoming period 1
        // -2 = upcoming period 2, etc. up to ConfigHelper.PayRun.UpcomingPeriods

        var options = new List<SelectOption>();

        // Add upcoming options.
        if (userBasicInfo.IncludeInPayRuns && ConfigHelper.PayRun.MaxUpcomingPeriods > 0) {
          for (byte upn = 1; upn <= ConfigHelper.PayRun.MaxUpcomingPeriods; upn++) {
            // Get upcoming period end date.
            DbHelper.PayRuns.GetUpcomingPayRunPeriodDates(upn, out _, out DateTime thisPeriodEndUtc, out _, out _);
            // Insert each one above the other, i.e. later periods are above the earlier ones.
            options.Insert(0,
              new SelectOption($"-{upn}", // Note the value is negative to differentiate upcoming payrun number (upn) from existing pay run IDs.
              $"Upcoming to {DisplayDate(SessionHelper.UtcToUserTime(thisPeriodEndUtc)).HTMLEncode()}",
              selectedPayRunId == -upn || selectedPayRunId == 0 && upn == 1)); // selectedPayRunId == 0 means no selection, default selection is first item (the most recent upcoming).
          }
        }

        // Add existing pay runs below the upcoming items. Option value is the pay run ID.
        var payRunList = DbHelper.PayRuns.GetExistingPayRunSelectItems(userBasicInfo.UserId);
        foreach (var payRun in payRunList) {
          options.Add(new SelectOption(payRun.PayRunId.ToString(), DisplayDate(SessionHelper.UtcToUserTime(payRun.PeriodEndUtc)), selectedPayRunId == payRun.PayRunId));
        }

        return GetSelectOptionListHtml(options);
      }

      // Get pay run info based on UI selection.
      // payRunIdSelected < 0 is an upcoming pay run period number (from select options above)
      public static string GetPartnerPayRunInfo(DbHelper.AbleUser.AbleUserBasicInfo userBasicInfo, int? payRunIdSelected, out bool hasItems) {

        if ((payRunIdSelected ?? 0) == 0) {
          hasItems = false;
          return "";
        }

        if (payRunIdSelected < 0) {
          // Upcoming pay run period number selected. Pass as a positive number 1, 2, etc. up to ConfigHelper.PayRun.MaxUpcomingPeriods
          var upcomingPeriodNumber = (byte)Math.Min(Math.Abs(payRunIdSelected.Value), ConfigHelper.PayRun.MaxUpcomingPeriods);
          return GetUpcomingPayRunInfo(userBasicInfo, upcomingPeriodNumber, out hasItems); // Default to first upcoming.
        } else {
          // Existing pay run ID selected.
          return GetExistingPayRunInfo(userBasicInfo, payRunIdSelected.Value, out hasItems);
        }
      }

      // Get first upcoming pay run info by default (upcomingPayRunPeriod 1).
      public static string GetUpcomingPayRunInfo(DbHelper.AbleUser.AbleUserBasicInfo userBasicInfo, out bool hasItems) {
        return GetUpcomingPayRunInfo(userBasicInfo, 1, out hasItems);
      }

      // Get upcoming pay run information.
      // First Upcoming pay run period number is 1, up to ConfigHelper.PayRun.MaxUpcomingPeriods
      private static string GetUpcomingPayRunInfo(DbHelper.AbleUser.AbleUserBasicInfo userBasicInfo, byte upcomingPayRunPeriod, out bool hasItems) {

        hasItems = false;

        if (upcomingPayRunPeriod < 1 || upcomingPayRunPeriod > ConfigHelper.PayRun.MaxUpcomingPeriods) {
          // Invalid period given.
          return "No Upcoming items at this time.";
        }

        DbHelper.PayRuns.GetUpcomingPayRunPeriodDates(
          upcomingPayRunPeriod,
          out DateTime previousPeriodEndUtc, out DateTime thisPeriodEndUtc,
          out DateTime dataPeriodStartUtc, out DateTime dataPeriodEndUtc);

        var payRunItems = DbHelper.PayRuns.GetUpcomingPayRunItems(userBasicInfo, dataPeriodStartUtc, dataPeriodEndUtc);

        if (payRunItems.IsNullOrEmpty()) {
          return "No Upcoming items at this time.";
        }

        hasItems = true;

        return GetPayRunInfo(payRunItems, previousPeriodEndUtc, thisPeriodEndUtc);
      }

      // Get existing pay run information.
      private static string GetExistingPayRunInfo(DbHelper.AbleUser.AbleUserBasicInfo userBasicInfo, int payRunId, out bool hasItems) {

        hasItems = false;

        var payRunItems = DbHelper.PayRuns.GetExistingPayRunItems(userBasicInfo.UserId, payRunId);

        if (payRunItems.IsNullOrEmpty()) {
          return "No Pay Run items found.";
        }

        hasItems = true;

        var payRunInfo = DbHelper.PayRuns.GetPayRunInfo(payRunId);

        return GetPayRunInfo(payRunItems, payRunInfo.PreviousPeriodEndUtc, payRunInfo.PeriodEndUtc);
      }

      // Get pay run information, each pay run item type in a table.
      private static string GetPayRunInfo(List<DbHelper.PayRuns.PayRunItemDetail> payRunItems, DateTime? previousPeriodEndUtc, DateTime thisPeriodEndUtc) {

        var sbHtml = new StringBuilder();

        AppendTotalRevenueBox(sbHtml, payRunItems, previousPeriodEndUtc, thisPeriodEndUtc);
        AppendResultsTableHtml(sbHtml, ConfigHelper.PayRunItemTypeId.Delivery_Coaching, "tblSessions", "Coaching Sessions", "Date & Time", payRunItems, "Sessions Total:", true);
        AppendResultsTableHtml(sbHtml, ConfigHelper.PayRunItemTypeId.Delivery_Workshop, "tblWorkshops", "Workshops", "Date & Time", payRunItems, "Workshops Total:", true);
        AppendResultsTableHtml(sbHtml, ConfigHelper.PayRunItemTypeId.Delivery_Consulting, "tblConsulting", "Consulting Items", "Completion", payRunItems, "Consulting Total:", true);
        AppendResultsTableHtml(sbHtml, ConfigHelper.PayRunItemTypeId.PLC, "tblPLC", "PLC Payout", "Date", payRunItems, "PLC Payout Total:", false);
        AppendResultsTableHtml(sbHtml, ConfigHelper.PayRunItemTypeId.Sales_Opp, "tblSales", "Sales Payout", "Date", payRunItems, "Sales Payout Total:", false);

        return sbHtml.ToString();
      }

      private static string GetDescriptionText(DbHelper.PayRuns.PayRunItemDetail payRunItem) {

        switch (payRunItem.ComponentType) {
          case DbHelper.ProgramComponents.ComponentTypeEnum.CoachingSession:
            return payRunItem.CoacheeFullName;
          case DbHelper.ProgramComponents.ComponentTypeEnum.Workshop:
            return payRunItem.WorkshopTitle;
          case DbHelper.ProgramComponents.ComponentTypeEnum.ConsultingItem:
            return payRunItem.ConsultingItemTitle;
          default:
            return "";
        }
      }

      private static string GetItemDateText(DbHelper.PayRuns.PayRunItemDetail payRunItem) {

        if (payRunItem.ComponentType == DbHelper.ProgramComponents.ComponentTypeEnum.CoachingSession) {
          return TimeHelper.UtcToTimeZoneId(payRunItem.CoachingSessionDateUtc, payRunItem.CoacheeTimeZoneIANA).ToStringOrDefaultIfNull("d MMM yyyy, h:mm tt", "");
        } else if (payRunItem.ComponentType == DbHelper.ProgramComponents.ComponentTypeEnum.Workshop) {
          return TimeHelper.UtcToTimeZoneId(payRunItem.WorkshopStartDateUtc, payRunItem.WorkshopIANATimeZone).ToStringOrDefaultIfNull("d MMM yyyy, h:mm tt", "");
        }
        return DisplayDate(payRunItem.ComponentCompletedDateUtc);
      }

      private static string GetRowLinkUrl(DbHelper.PayRuns.PayRunItemDetail payRunItem) {

        switch (payRunItem.ComponentType) {
          case DbHelper.ProgramComponents.ComponentTypeEnum.CoachingSession:
            return PathHelper.Pages.CoacheeEdit(payRunItem.CoacheeId, PathHelper.CoacheeTabEnum.coaching);
          case DbHelper.ProgramComponents.ComponentTypeEnum.Workshop:
            return PathHelper.Pages.Workshops_Edit(payRunItem.ProgramJobId, payRunItem.WorkshopEventId);
          case DbHelper.ProgramComponents.ComponentTypeEnum.ConsultingItem:
            return PathHelper.Pages.Consulting_Edit(payRunItem.ProgramJobId, payRunItem.ConsultingItemId);
          default:
            return "";
        }
      }

      private static void AppendTotalRevenueBox(StringBuilder sbHtml, List<DbHelper.PayRuns.PayRunItemDetail> payRunItems, DateTime? previousPeriodEndUtc, DateTime thisPeriodEndUtc) {

        decimal? totalRevenue = payRunItems.Sum(i => i.PartnerRevenue);

        sbHtml.Append($@"
        <div class=""table-responsive"">
          <table class=""tblGrandTotal table table-bordered"">
            <tfoot>
              <tr>
                <td class=""""><h4>Total Revenue This Payout</h4>{GetPeriodDateRangeText(previousPeriodEndUtc, thisPeriodEndUtc).HTMLEncode()}</td>
                <td class=""align-right pr20 w150"">{GetAmountCurrencyFormat(totalRevenue).HTMLEncode()}</td>
              </tr>
            </tfoot>
          </table>
        </div>");
      }

      private static string GetPeriodDateRangeText(DateTime? previousPeriodEndUtc, DateTime thisPeriodEndUtc) {

        if (previousPeriodEndUtc == null) {

          // No start date, just show end date.
          return $"To {DisplayDate(SessionHelper.UtcToUserTime(thisPeriodEndUtc))}";

        } else {

          // Show date range. Add 1 day to start date just so it doesn't display the same as the previous end date.
          return $"{DisplayDate(SessionHelper.UtcToUserTime(previousPeriodEndUtc.Value.AddDays(1)))} to {DisplayDate(SessionHelper.UtcToUserTime(thisPeriodEndUtc))}";
        }
      }

      private static void AppendResultsTableHtml(
        StringBuilder sbHtml,
        int payRunItemTypeId,
        string tableCSSClass, string tableHeaderText, string dateColHeaderText,
        List<DbHelper.PayRuns.PayRunItemDetail> payRunItems,
        string footerText,
        bool addDataRowLink) {

        sbHtml.Append($@"
          <div class=""table-title"">{tableHeaderText.HTMLEncode()}</div>
          <div class=""table-responsive"">
          <table class=""{tableCSSClass.HTMLEncode()} table table-bordered {(addDataRowLink ? "table-hover table-rowlink" : "")}"" {(addDataRowLink ? "data-rowlink-url=\"\"" : "")} >
            <thead>
              <tr>
                <th class=""type-datetime"">{dateColHeaderText.HTMLEncode()}</th>
                <th class=""type-description"">Job / Program</th>
                <th class=""type-description"">Description</th>
                <th class=""type-money"">Amount</th>
              </tr>
            </thead>
            <tbody>");

        AppendPayRunTableRows(sbHtml, payRunItems, payRunItemTypeId, out decimal rowsTotalRevenue);

        sbHtml.Append($@"
            </tbody>
            <tfoot>
              <tr tabindex=""-1"">
                <td></td>
                <td></td>
                <td class=""align-right"">{footerText.HTMLEncode()}</td>
                <td class=""align-right pr20 tfoot-total"">{GetAmountCurrencyFormat(rowsTotalRevenue)}</td>
              </tr>
            </tfoot>
          </table>
        </div>");
      }

      private static void AppendPayRunTableRows(StringBuilder sbHtml, List<DbHelper.PayRuns.PayRunItemDetail> payRunItems, int payRunItemTypeId, out decimal totalRevenue) {

        totalRevenue = 0;

        foreach (var item in payRunItems) {

          if (item.PayRunItemTypeId != payRunItemTypeId || item.PartnerRevenue == null) continue;

          totalRevenue += item.PartnerRevenue.Value;

          string itemDateText = GetItemDateText(item);
          string titleText = $"{item.JobNumber}: {item.ProgramName}";
          string descriptionText = GetDescriptionText(item);
          string rowLinkUrl = GetRowLinkUrl(item);
          string linkDataAttr = rowLinkUrl.IsNullOrEmpty() ? "" : $"data-rowlink-id=\"{rowLinkUrl.HTMLEncode()}\"";

          sbHtml.Append($@"
            <tr tabindex=""0"" class=""rowData"" {linkDataAttr}>
              <td class=""type-datetime"">{itemDateText.HTMLEncode()}</td>
              <td class=""type-description"">{titleText.HTMLEncode()}</td>
              <td class=""type-description"">{descriptionText.HTMLEncode()}</td>
              <td class=""type-money"">{GetAmountCurrencyFormat(item.PartnerRevenue.Value)}</td>
            </tr>");
        }
      }

    }
  }
}
