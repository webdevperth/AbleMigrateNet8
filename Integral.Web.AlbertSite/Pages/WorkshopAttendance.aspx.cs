using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class WorkshopAttendance : AppCode.PageBaseClasses.ProgramPageBase {

    public string WorkShopTitle = "";
    public List<DbHelper.WorkshopEvents.AttendanceInfo> AttendanceList;
    public DbHelper.WorkshopEvents.WorkshopEventInfo WorkshopEventInfo;

    public class FormFields {
      public const string WorkshopAttendanceIds = "WorkshopAttendanceIds";
    }

    protected void Page_Load(object sender, EventArgs e) {

      int urlWorkshopEventId = (int)WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.WorkshopId, 0);

      // Check workshops exists.
      WorkshopEventInfo = DbHelper.WorkshopEvents.GetWorkshopInfo(urlWorkshopEventId);

      // Go back to list if user has no access to workshop.
      if (!SessionHelper.AppAccess.Programs.Workshops.CanView(ProgramInfo, WorkshopEventInfo)) {
        RespondNoAccessOrRedirect();
        return;
      }

      WorkShopTitle = WorkshopEventInfo.WorkshopTitle;
      PageTitle = $"Update Workshop Attendance - {WorkShopTitle}";

      AttendanceList = DbHelper.WorkshopEvents.GetAttendanceList(WorkshopEventInfo);

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {
          UpdateAttendance(ajax);
        });
      }
    }

    void UpdateAttendance(AjaxSubmitHelper ajax) {

      var selectedCoacheeIds = ajax.CheckFieldIntList(FormFields.WorkshopAttendanceIds);

      // Remove any coachee IDs which are not part of this program (i.e. don't trust browsers!)
      var coacheeIdsInProgram = DbHelper.AblePrograms.GetCoacheesInProgram(ProgramInfo.ProgramJobId);
      selectedCoacheeIds.RemoveAll(coacheeId => !coacheeIdsInProgram.Contains(coacheeId));

      DbHelper.WorkshopEvents.UpdateAttendance(WorkshopEventInfo, selectedCoacheeIds, SessionHelper.UserInfo);
    }

    // If POST, return fail status, otherwise redirect back to the list.
    void RespondNoAccessOrRedirect() {
      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.RespondNoAccessToFunction();
        return;
      } else {
        WebHelper.Redirect(PathHelper.Pages.Workshops_List()); // Not allowed to add new item.
        return;
      }
    }
  }
}
