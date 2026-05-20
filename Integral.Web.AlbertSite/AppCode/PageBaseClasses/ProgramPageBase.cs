using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class ProgramPageBase : LoggedInPageBase {

    public bool IsNewProgram { get; protected set; }
    public string RevenueTextDisplay { get; protected set; }
    public DbHelper.Projects.ProjectInfo AddToProjectInfo { get; protected set; } = null;
    internal string ProjectJobNumber;
    internal bool isProgramSettingsPage;
    public bool CanViewHiddenPartners { get; protected set; } = false;
    public bool CanViewInactivePartners { get; protected set; } = false;

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);
      MenuThirdLayerActive_Programs = ProjectMenuIsActive = true;
      IsNewProgram = false;
      ProgramInfo = null;
      PageSubtitleIsHtml = false;

      // Mirror onto LayoutModel for future ViewComponent consumers. See LayoutModel.cs.
      var layout = LayoutModel.GetCurrent();
      layout.MenuThirdLayerActive_Programs = MenuThirdLayerActive_Programs;
      layout.ProjectMenuIsActive = ProjectMenuIsActive;
      layout.ProgramInfo = ProgramInfo;

      // Set the revenue text to "Price" if the user is a client, or else "Revenue".
      RevenueTextDisplay = SessionHelper.IsUserRoleClient ? "Price" : "Revenue";

      if (SessionHelper.IsUserRoleAdmin || SessionHelper.IsUserRoleCoach) FallbackUrl = PathHelper.Pages.ProjectPrograms();

      // Is there a job id or "new" in the querystring?
      int programJobId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.ProgramJobId) ?? 0;
      IsNewProgram = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProgramJobId) == PathHelper.AbleUrlValues.IdNew;

      ProjectJobNumber = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber);
      isProgramSettingsPage = PathHelper.IsCurrentPage(PathHelper.Pages.ProgramSettings());
      layout.IsNewProgram = IsNewProgram;

      // Get program data if an id is given, or new Program object if adding a new one.
      if (IsNewProgram) {

        if (!isProgramSettingsPage) {
          SetFallbackRedirect(); // New only allowed on settings page.
          return;
        }

        ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber));
        layout.ProjectInfo = ProjectInfo;

        if (ProjectInfo == null) {
          SetRedirect(PathHelper.Pages.Projects_List());
          return;
        }

        ProgramInfo = new DbHelper.AblePrograms.AbleProgramInfo();
        layout.ProgramInfo = ProgramInfo;

        SessionHelper.AppState.Coachees.ResetFilter();

      } else {

        ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(programJobId,
          SessionHelper.AppAccess.Programs.CanViewAllProjectPrograms()
            ? DbHelper.AblePrograms.WhereRelatedUserIs.NoCheck
            : DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_AnyRelated,
          SessionHelper.UserInfo);
        layout.ProgramInfo = ProgramInfo;

        if (ProgramInfo == null) {
          SetFallbackRedirect();
          return;
        }
        ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(ProgramInfo.ProgramJobNumber);
        layout.ProjectInfo = ProjectInfo;
        if (ProjectInfo == null) {
          SetRedirect(PathHelper.Pages.Projects_List());
          return;
        }
        SessionHelper.AppState.Coachees.SetFilterScope(SessionHelper.AppState.Coachees.FilterScope.Program, ProgramInfo.ProgramJobId, ProgramInfo.ProgramJobNumber + ": " + ProgramInfo.ProgramJobName);
        if (SessionHelper.AppAccess.Projects.CanViewProject(ProgramInfo)) {
          PageSubtitle = $"<a href=\"{PathHelper.Pages.ProjectInvoicing(ProgramInfo.ProgramJobNumber)}\">{ProgramInfo.ProgramJobNumber.HTMLEncode()}</a>";
          PageSubtitleIsHtml = true;
        } else {
          PageSubtitle = ProgramInfo.ProgramJobNumber;
        }
        PageSubtitle += ": " + ProgramInfo.CompanyName + " - " + ProgramInfo.ProgramJobName;
      }

      // Check user access to Program pages.
      if (!CheckUserPageAccess()) {
        SetFallbackRedirect();
        return;
      }

      CanViewHiddenPartners = SessionHelper.AppAccess.Coaches.CanViewHiddenPartners();
      CanViewInactivePartners = SessionHelper.AppAccess.Coaches.CanViewInactivePartners();
    }

    internal bool CheckUserPageAccess() {

      bool canListCostItems = SessionHelper.AppAccess.Programs.CanListCostItems(ProgramInfo);

      if (isProgramSettingsPage) {

        if (IsNewProgram) {
          if (!SessionHelper.AppAccess.Programs.CanCreateProgram(ProjectInfo)) return false;
        } else {
          if (!SessionHelper.AppAccess.Programs.CanViewProgramSettings(ProgramInfo)) return false;
        }

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProgramOverview())) {

        if (SessionHelper.AppAccess.Programs.CanViewProgramOverview(ProgramInfo)) {
          return true;
        } else {
          if (SessionHelper.AppAccess.Programs.Workshops.CanViewWorkshops(ProgramInfo)) {
            FallbackUrl = PathHelper.Pages.Workshops_List(ProgramInfo.ProgramJobId);
          }
          return false;
        }

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProgramSurveyStatus())) {

        if (!SessionHelper.AppAccess.PageAccess.CanAccessProgramSurveyStatus(ProgramInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProgramSendSurvey())) {

        if (!SessionHelper.AppAccess.PageAccess.CanAccessProgramSendSurvey()) return false;
        // Check access to viewing/send program survey status.
        if (!SessionHelper.AppAccess.Programs.CanSendSurveys(ProgramInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProgramSendEmail())) {
        // Check access to program send email page.
        if (!SessionHelper.AppAccess.PageAccess.CanAccessProgramSendEmail()) return false;
        // Check access to send email page.
        if (!SessionHelper.AppAccess.Programs.CanSendProgramEmail(ProgramInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProgramCostItems_List())
        || PathHelper.IsCurrentPage(PathHelper.Pages.ProgramCostItems_Edit(0))
        || PathHelper.IsCurrentPage(PathHelper.Pages.ProgramCostItems_Add(0))) {
        // Check access to program cost items.
        if (!SessionHelper.AppAccess.PageAccess.CanAccessProgramCostItems()) return false;
        // Check access to cost item list and editing.
        if (!SessionHelper.AppAccess.Programs.CanListCostItems(ProgramInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProgramInformation())) {

        if (!SessionHelper.AppAccess.PageAccess.CanAccessProgramInformation()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.Consulting_List())) {

        if (!SessionHelper.AppAccess.PageAccess.CanAccessProgramConsultingItems() || !SessionHelper.AppAccess.Programs.Consulting.CanListConsultingItems(ProgramInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.Workshops_List())) {

        if (!SessionHelper.AppAccess.Programs.Workshops.CanViewWorkshops(ProgramInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProgramParticipants(null))) {

        if (!SessionHelper.AppAccess.PageAccess.CanAccessProgramParticipants()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProgramContent(null))) {

        if (!SessionHelper.AppAccess.PageAccess.CanAccessProgramContent()) return false;
      }

      return true;
    }

    public string GetWorkshopStartDateTimeHtml(DbHelper.WorkshopEvents.WorkshopEventInfo workshopRowInfo) {

      if (workshopRowInfo.WhenStartLocal == null || workshopRowInfo.WorkshopStatusId == DbHelper.WorkshopStatus.WorkshopStatus_NotPlanned.WorkshopStatusId) return "-";

      string startLocal = workshopRowInfo.WhenStartLocal.ToString("d MMM yyyy, h:mm tt");

      if (workshopRowInfo.TimeZoneIdIana == userInfo.TimeZoneIdIana) return startLocal.Replace(", ", "<br>");

      // Time zone differs from viewing user, so show user time and both in a tooltip.
      DateTime timeInUserTZ = TimeHelper.UtcToTimeZoneId(workshopRowInfo.WhenStartUtc, userInfo.TimeZoneIdIana).Value.DateTime;
      string startForUser = timeInUserTZ.ToString("d MMM yyyy, h:mm tt");

      return startForUser.Replace(", ", "<br>")
        + " <a class=\"far fa-info-circle\" title=\""
        + startLocal + " " + workshopRowInfo.TimeZoneIdIana.HTMLEncode() + " Time<br>"
        + startForUser + " " + userInfo.TimeZoneIdIana.HTMLEncode() + " Time"
        + "\"></a>";
    }
  }
}
