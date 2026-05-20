using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class CoacheeInfoBase : LoggedInPageBase {

    public int UrlCoacheeId { get; protected set; }
    public bool IsNewCoachee { get; protected set; }
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo { get; protected set; } = null;
    public bool CanAddCoachee { get; private set; } = false;
    public bool CanChangeProgramStatus, CanChangeCoach, CanChangeMeetCoachDate, CanApplyCoachingToProgram, CanDeleteParticipant, LimitedEdit;
    public bool CanUpdateCoaching, CanViewParticipantProfile;

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);

      // Mirror onto LayoutModel for future ViewComponent consumers. See LayoutModel.cs.
      var layout = LayoutModel.GetCurrent();

      // Change Fallback to Coachees if user is admin or coach.
      if (SessionHelper.IsUserRoleAdmin || SessionHelper.IsUserRoleCoach) FallbackUrl = PathHelper.Pages.Coachees();

      UrlCoacheeId = (int)WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.CoacheeId, 0);
      IsNewCoachee = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeId) == PathHelper.AbleUrlValues.IdNew;
      layout.IsNewCoachee = IsNewCoachee;

      if (IsNewCoachee) {

        int? addToProgramJobId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.ProgramJobId, null);

        if (PathHelper.IsCurrentPage(PathHelper.Pages.CoacheeEdit())) {
          SetRedirect(PathHelper.Pages.ProgramParticipants(addToProgramJobId));
          return;
        }

        if (addToProgramJobId != null) {
          // Adding from the Program Participants list, so we should know what program this is for.

          FallbackUrl = PathHelper.Pages.ProgramParticipants(addToProgramJobId);
          ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull((int)addToProgramJobId);
          ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(ProgramInfo.ProgramJobNumber);
          layout.ProgramInfo = ProgramInfo;
          layout.ProjectInfo = ProjectInfo;

          if (ProgramInfo == null) {
            SetRedirect(PathHelper.Pages.Projects_List(), "Related Program was not found."); // Program doesn't exist, go back to Projects list.
            return;
          }

          CanAddCoachee = SessionHelper.AppAccess.Programs.CanAddProgramParticipant(ProgramInfo); // Can add from program participants list.

        } else {
          // Adding from the Participant admin master list.

          ProgramInfo = null;
          layout.ProgramInfo = null;
          CanAddCoachee = SessionHelper.AppAccess.Participants.CanAdd();
        }

        if (!CanAddCoachee) {

          SetFallbackRedirect("Adding Coachee not allowed.");
          return;

        } else {

          CoacheeInfo = null;
          layout.CoacheeInfo = null;
          CanChangeProgramStatus = true;
          CanChangeCoach = true;
          CanChangeMeetCoachDate = true;
          CanApplyCoachingToProgram = true;
          CanDeleteParticipant = true;
          CanUpdateCoaching = true;
        }

      } else {

        if (UrlCoacheeId == 0) {
          SetFallbackRedirect("Coachee ID required in request.");
          return;
        }

        CoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(UrlCoacheeId);
        layout.CoacheeInfo = CoacheeInfo;

        if (CoacheeInfo == null) {
          SetFallbackRedirect("Requested Coachee not found, it may have been deleted.");
          return;
        }

        // Although CoacheeInfo.ProgramJobId is nullable in the db and model, this should change because coachees are never without a Program.
        // Since CoacheeInfo.ProgramJobId can never be null, ProgramInfo can also never be null.
        // Similarly, ProjectInfo can never be null.
        // TODO: In db and model, set ProgramJobId to be not nullable, to align with the business rules.
        // TODO: Program and Project should be related by ProjectId (the rowid) not JobNumber - change in db and models.
        // TODO: ProgramJobNumber/JobNumber should be called ProjectUID or something, unique per Org (tenant), and only stored in the project table.
        if (CoacheeInfo.ProgramJobId != null) {
          ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(CoacheeInfo.ProgramJobId.Value);
          ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(ProgramInfo.ProgramJobNumber);
          layout.ProgramInfo = ProgramInfo;
          layout.ProjectInfo = ProjectInfo;
        }

        // If user can view Participants in Program, change Fallback to Program Participants list instead of main Coachees list.
        if (ProgramInfo != null && SessionHelper.AppAccess.Programs.CanViewProgramParticipants(ProgramInfo)) {
          FallbackUrl = PathHelper.Pages.ProgramParticipants(ProgramInfo.ProgramJobId);
        }

        CanViewParticipantProfile = SessionHelper.AppAccess.Participants.CanEdit(CoacheeInfo)
          || SessionHelper.AppAccess.Participants.LimitedEdit(CoacheeInfo)
          || SessionHelper.AppAccess.Programs.CanViewProgramParticipants(ProgramInfo);

        GetSharedSurveyInfo();

        if (!IsLeaderViewingValidSurveyOrReport() && !IsParticipantSlideoutPanel()) {

          // Can current user edit or view (read only) this coachee?
          if (!CanViewParticipantProfile) {
            SetFallbackRedirect("Access to this Coachee not allowed.");
            return;
          }

          // If coachee doesn't have a program, redirect to profile page to set a program.
          if (CoacheeInfo.ProgramJobId == null) {
            if (!PathHelper.IsCurrentPage(PathHelper.Pages.CoacheeEdit())) {
              WebHelper.SetNextPageMessageText("Please select a Program for this Coachee.");
              SetRedirect(PathHelper.Pages.CoacheeEdit(true));
              return;
            }
          } else {
            SessionHelper.AppState.Coachees.ResetIfNotFilteredProgram(CoacheeInfo.ProgramJobId);
          }

          CanChangeProgramStatus = SessionHelper.AppAccess.Participants.CanChangeProgramStatus(CoacheeInfo);
          CanChangeCoach = SessionHelper.AppAccess.Participants.CanChangeCoach(CoacheeInfo);
          CanChangeMeetCoachDate = SessionHelper.AppAccess.Participants.CanChangeMeetCoachDate(CoacheeInfo);
          CanApplyCoachingToProgram = SessionHelper.AppAccess.Participants.CanApplyCoachingToProgram(CoacheeInfo);
          LimitedEdit = SessionHelper.AppAccess.Participants.LimitedEdit(CoacheeInfo);
          CanUpdateCoaching = SessionHelper.AppAccess.Participants.CanUpdateCoaching(CoacheeInfo);
          CanDeleteParticipant = SessionHelper.AppAccess.Participants.CanSoftDelete(CoacheeInfo);
        }

        if (ConfigHelper.ChangeCoachFilterWhenAdminViewsCoachee) {
          // When viewing a user as admin, update the list filter to be the coachee's coach.
          if (SessionHelper.IsUserRoleAdmin && CoacheeInfo != null && CoacheeInfo.CoachUserId != ConfigHelper.UserId.Unassigned) {
            if (SessionHelper.AppState.Coachees.GetFilterIdFromSession(SessionHelper.AppState.Coachees.FilterScope.Coach) != CoacheeInfo.CoachUserId) {
              var coachInfo = DbHelper.AlbertCoaches.GetCoachInfo(CoacheeInfo.CoachUserId);
              if (coachInfo == null)
                SessionHelper.AppState.Coachees.SetFilterScope(SessionHelper.AppState.Coachees.FilterScope.Coach, userInfo.UserId, userInfo.GetFullName()); // revert to user.
              else
                SessionHelper.AppState.Coachees.SetFilterScope(SessionHelper.AppState.Coachees.FilterScope.Coach, coachInfo.UserId, coachInfo.GetFullName());
            }
          }
        }

        // Check user access to Project pages.
        if (!CheckUserPageAccess()) {
          SetFallbackRedirect("Access to this page not allowed.");
          return;
        }
      }
    }

    internal bool IsLeaderViewingValidSurveyOrReport() {

      if (SessionHelper.IsUserRoleClient) {
        if (PathHelper.IsCurrentPage(PathHelper.Partials.CoacheeSurveyDetailsModal())) {
          return CoacheeInfo.CompanyId == userInfo.ClientCompanyId;
        }
        return false;
      }

      if (CoacheeInfo == null) return false;
      if (!SessionHelper.IsUserRoleLeader) return false;

      if (PathHelper.IsCurrentPage(PathHelper.Reports.CoacheeSurvey())
        || PathHelper.IsCurrentPage(PathHelper.Partials.CoacheeSurveyDetailsModal())
        || PathHelper.IsCurrentPage(PathHelper.Pages.CoacheeSurveyEmbed())) { // Viewing a survey page.

        return CoacheeInfo.UserId == userInfo.UserId || IsViewingSharedSurvey; // Allowed to view this survey.
      }

      return false;
    }

    internal bool IsParticipantSlideoutPanel() {
      return PathHelper.IsCurrentPage(PathHelper.Partials.ParticipantSlideoutPanel(null));
    }

    internal bool CheckUserPageAccess() {

      if (PathHelper.IsCurrentPage(PathHelper.Pages.CoacheeEdit())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessCoacheeEdit()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Partials.CoacheeSendSurveyModal(0))) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessCoacheeSendSurvey()) return false;

      }

      return true;
    }
  }
}
