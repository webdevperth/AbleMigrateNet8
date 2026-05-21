using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class BookSession : PageModel {

    public DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo;
    public string CoacheeFullName;
    public bool CanBookSession, HasBookedAllSessions;
    public DbHelper.AlbertCoaches.AlbertCoachInfo CoachInfo;
    public DbHelper.AblePrograms.AbleProgramInfo ProgramInfo;
    public DbHelper.Projects.ProjectInfo ProjectInfo;
    public int FirstFreeSessionNumber;

    public IActionResult OnGet() {

      CanBookSession = false;
      CoachInfo = null;
      CoacheeFullName = WebHelper.GetQueryStringValue("name");

      // Get coachee first by guid then by id, depending on what is provided.
      // On production, only Guid is allowed, not ID.
      if (Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeGuid), out Guid requestCoacheeGuid)) {
        CoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(requestCoacheeGuid);
      } else if (!ConfigHelper.IsLiveServer && int.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeId), out int requestCoacheeId)) {
        CoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(requestCoacheeId);
      }

      if (CoacheeInfo == null) {
        WebHelper.Redirect(PathHelper.Pages.Home());
        return new EmptyResult();
      }

      ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(CoacheeInfo.ProgramJobId.Value);
      ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(ProgramInfo.ProgramJobNumber);

      if (RedirectToPortalBookingPage()) {
        SessionHelper.PublicPageUserRedirect(CoacheeInfo.UserId.Value, PathHelper.Pages.ParticipantCoaching(CoacheeInfo.CoacheeUID));
        return new EmptyResult();
      }

      FirstFreeSessionNumber = DbHelper.ProgramComponents.GetFirstFreeSessionNumber(CoacheeInfo.CoacheeId) ?? 0;
      HasBookedAllSessions = CoacheeInfo.UserActivity?.SessionsBooked >= CoacheeInfo.UserActivity?.SessionsAllocated || FirstFreeSessionNumber == 0;
      CanBookSession = SessionHelper.AppAccess.Participants.CanBookSession(ProgramInfo, CoacheeInfo);

      CoacheeFullName = CoacheeInfo.GetFullName();

      if (CoacheeInfo.CoachUserId != ConfigHelper.UserId.Unassigned) {
        CoachInfo = DbHelper.AlbertCoaches.GetCoachInfo(CoacheeInfo.CoachUserId);
      }

      return Page();
    }

    bool RedirectToPortalBookingPage() {

      var userInfo = SessionHelper.GetUserInfoOrNull();

      if (userInfo != null) { // User logged in

        // (#86cugdmqj)
        // Don't ever redirect to portal booking page if user is admin
        if (SessionHelper.IsUserRoleAdmin) return false;
        // ... or if user is the coachee's coach.
        if (SessionHelper.IsUserRoleCoach && userInfo.UserId == CoacheeInfo.CoachUserId) return false;

        // Redirect to portal booking page if the logged in user is the coachee.
        return userInfo.UserId == CoacheeInfo.UserId && userInfo.IsParticipant;

      } else { // Not logged in

        // (#86cvz2h67) If not logged in, this flag determines whether to redirect to portal page.
        return !ProjectInfo.AllowLoggedOutCoachBooking;

      }
    }

    public class CoachingSession {

      public string ProjectJobNumber { get; private set; }
      public string ProjectName { get; private set; }
      public string ProjectFriendlyName { get; private set; }
      public string JobName { get; private set; }
      public int CoachingTypeId { get; private set; }
      public string CoachName { get; private set; }
      public string CoachEmail { get; private set; }
      public string CoachCalendlyUrlName { get; private set; }
      public int SessionNumber { get; private set; }
      public DbHelper.AlbertCoachingTypes.SessionTypeInfo SessionType { get; private set; }
      public DateTime? ApptDateUtc { get; private set; }
      public bool IsLocked { get; private set; }

      public CoachingSession(
        string projectJobNumber, string projectName, string projectFriendlyName, string jobName,
        int coachingTypeId, string coachName, string coachEmail, string coachCalendlyUrlName,
        int sessionNumber, DbHelper.AlbertCoachingTypes.SessionTypeInfo sessionType,
        DateTime? apptDateUtc, bool isLocked
      ) {
        ProjectJobNumber = projectJobNumber;
        ProjectName = projectName;
        ProjectFriendlyName = projectFriendlyName;
        JobName = jobName;
        CoachingTypeId = coachingTypeId;
        CoachName = coachName;
        CoachEmail = coachEmail;
        CoachCalendlyUrlName = coachCalendlyUrlName;
        SessionNumber = sessionNumber;
        SessionType = sessionType;
        ApptDateUtc = apptDateUtc;
        IsLocked = isLocked;
      }
    }
  }
}
