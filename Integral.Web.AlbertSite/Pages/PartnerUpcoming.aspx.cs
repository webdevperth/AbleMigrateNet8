using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class PartnerUpcoming : AppCode.PageBaseClasses.CoachInfoBase {

    public decimal workshopTotalRevenue = 0;
    public decimal consultingTotalRevenue = 0;
    public decimal payRunTotalRevenue = 0;
    public decimal salesTotalRevenue = 0;
    public decimal plcTotalRevenue = 0;

    public List<DbHelper.CoachingSessions.AbleSessionInfo> SessionList;
    public List<DbHelper.WorkshopEvents.WorkshopEventInfo> WorkshopList;
    public List<DbHelper.ConsultingItems.ConsultingItemInfo> ConsultingItemList;
    public List<DbHelper.PayRuns.UpcomingPLCSalesInfo> SalesItemList;
    public List<DbHelper.PayRuns.UpcomingPLCSalesInfo> PLCItemList;

    protected void Page_Load(object sender, EventArgs e) {

      if (!SessionHelper.AppAccess.Coaches.CanViewPayRuns(userInfo, CoachInfo.UserId)) {
        WebHelper.Redirect(FallbackUrl);
        return;
      }

      PageTitle = "Upcoming";

      var latestPayRunDateUtc = DbHelper.PayRuns.GetLatestProcessedDateUtc(CoachInfo.UserId).GetValueOrDefault(DateTime.MinValue);
      var overviewItemEarliestDate = DateTime.UtcNow.AddDays(-ConfigHelper.OverviewPage_HistoricItemCutoffDays);

      SessionList = DbHelper.CoachingSessions.GetUpcomingSessionsForCoach(CoachInfo.UserId, overviewItemEarliestDate);

      WorkshopList = DbHelper.WorkshopEvents.GetUpcomingWorkshopsForCoach(CoachInfo.UserId, overviewItemEarliestDate);
      workshopTotalRevenue = 0;
      if (WorkshopList != null) foreach (var item in WorkshopList) workshopTotalRevenue += item.WorkshopRevenue.GetValueOrDefault(0);

      ConsultingItemList = DbHelper.ConsultingItems.GetUpcomingConsultingForCoach(CoachInfo.UserId, overviewItemEarliestDate);
      consultingTotalRevenue = 0;
      if (ConsultingItemList != null) foreach (var item in ConsultingItemList) consultingTotalRevenue += item.ItemAmount;

      var salesAndPLC = DbHelper.PayRuns.GetUpcomingSalesAndPLC(CoachInfo.UserId, latestPayRunDateUtc);

      // Tweak descriptions a bit.
      salesAndPLC.ForEach(i => {
        if (i.CoachingSessionId != null) i.ItemName = "Coaching: " + i.ItemName;
        else if (i.WorkshopEventId != null) i.ItemName = "Workshop: " + i.ItemName;
        else if (i.ConsultingItemId != null) i.ItemName = "Consulting: " + i.ItemName;
      });

      // Separate sales and plc items to their own separate lists.
      SalesItemList = (from i in salesAndPLC where i.SalesUserId == CoachInfo.UserId && i.SalesRevenue > 0 select i).ToList();
      PLCItemList = (from i in salesAndPLC where i.PLCUserId == CoachInfo.UserId && i.PLCRevenue > 0 select i).ToList();

      payRunTotalRevenue = workshopTotalRevenue + consultingTotalRevenue;
    }

    public string GetSessionTime(DbHelper.CoachingSessions.AbleSessionInfo coachingSession) {
      return TimeHelper.UtcToTimeZoneId(coachingSession.ApptDateUTC, coachingSession.CoachTimeZoneIdIANA).ToStringOrDefaultIfNull("d MMM yyyy, h:mm tt", "");
    }

    public string GetWorkshopTime(DbHelper.WorkshopEvents.WorkshopEventInfo workshopRowInfo) {
      if (workshopRowInfo.WhenStartLocal == null) return "-";
      string startLocal = workshopRowInfo.WhenStartLocal.ToString("d MMM yyyy, h:mm tt");
      if (workshopRowInfo.TimeZoneIdIana == userInfo.TimeZoneIdIana) return startLocal;
      // Time zone differs from viewing user, so show user time and both in a tooltip.
      DateTime timeInUserTZ = TimeHelper.UtcToTimeZoneId(workshopRowInfo.WhenStartUtc, userInfo.TimeZoneIdIana).Value.DateTime;
      string startForUser = timeInUserTZ.ToString("d MMM yyyy, h:mm tt");
      return startForUser
        + " <i class=\"far fa-info-circle\" title=\""
        + startLocal + " " + workshopRowInfo.TimeZoneIdIana.HTMLEncode() + " Time<br>"
        + startForUser + " " + userInfo.TimeZoneIdIana.HTMLEncode() + " Time"
        + "\"></i>";
    }

    public decimal GetCoacheeDeliveryAmount(DbHelper.CoachingSessions.AbleSessionInfo coachingSession) {
      if (coachingSession.ComponentPrice == null || coachingSession.ProgramDeliveryPercentage == null) return 0;
      return (decimal)coachingSession.ComponentPrice * (decimal)coachingSession.ProgramDeliveryPercentage;
    }

    public decimal GetWorkshopDeliveryAmount(DbHelper.WorkshopEvents.WorkshopEventInfo workshopRowInfo) {
      if (workshopRowInfo.WorkshopRevenue == null || workshopRowInfo.ProgramDeliveryPercentage == null) return 0;
      return (decimal)workshopRowInfo.WorkshopRevenue * (decimal)workshopRowInfo.ProgramDeliveryPercentage;
    }

    public decimal GetConsultingDeliveryAmount(DbHelper.ConsultingItems.ConsultingItemInfo consultingItem) {
      if (consultingItem.ProgramDeliveryPercentage == null) return 0;
      return consultingItem.ItemAmount * (decimal)consultingItem.ProgramDeliveryPercentage;
    }

  }
}
