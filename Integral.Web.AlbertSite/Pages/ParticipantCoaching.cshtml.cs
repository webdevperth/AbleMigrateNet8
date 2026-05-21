using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ParticipantCoaching : AppCode.PageBaseClasses.LoggedInPageModel {

    public DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo;
    public DbHelper.AlbertCoaches.AlbertCoachInfo CoachInfo;
    public bool CanBookSession, CanViewEventSessions, HasBookedAllSessions, UserIsNotInAProgram, CanViewCoachInfo, TriggerCoachSlideout;
    public int FirstFreeSessionNumber;
    private Guid urlCoacheeGuid = Guid.Empty;
    public List<DbHelper.AlbertCoaches.AlbertCoachInfo> CoachesList = null;

    public enum PageModeEnum { BookSession, SelfCoachSelection }
    public PageModeEnum PageMode { get; set; }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Coaching";

      bool canSelectCoach = SessionHelper.AppAccess.Participants.CanSelfSelectCoach(userInfo);
      if (canSelectCoach) {

        PageMode = PageModeEnum.SelfCoachSelection;
        CoachesList = DbHelper.AlbertCoaches.GetCoachInfoList_SelfAssignment();

      } else {

        PageMode = PageModeEnum.BookSession;
        int UrlUserId = (int)WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.UserId, 0);
        TriggerCoachSlideout = (UrlUserId != 0 && UrlUserId == userInfo.UserId);

        // If url contains a specific coachee Guid, set that as the user's "latest" coachee used on this page.
        if (WebHelper.TryGetQueryStringGuid(PathHelper.AbleUrlKeys.CoacheeGuid, out urlCoacheeGuid)) {
          if (userInfo.LatestCoachingInfo?.CoacheeGuid != urlCoacheeGuid) {
            userInfo.SetLatestCoachingInfo(urlCoacheeGuid);
          }
        }

        if (!SessionHelper.AppAccess.PageAccess.CanAccessParticipantCoaching()) {
          WebHelper.Redirect(FallbackUrl);
          return new EmptyResult();
        }

        GetBookingInfo();
      }

      return Page();
    }

    private void GetBookingInfo() {

      if (userInfo.LatestCoachingInfo == null) {
        UserIsNotInAProgram = true;
        return;
      }

      CoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(userInfo.LatestCoachingInfo.CoacheeId);

      if (CoacheeInfo == null) {
        UserIsNotInAProgram = true;
        return;
      }

      UserIsNotInAProgram = false;
      CoachInfo = new DbHelper.AlbertCoaches.AlbertCoachInfo();

      ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(CoacheeInfo.ProgramJobId.Value);
      ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(ProgramInfo.ProgramJobNumber);
      FirstFreeSessionNumber = DbHelper.ProgramComponents.GetFirstFreeSessionNumber(CoacheeInfo.CoacheeId) ?? 0;
      HasBookedAllSessions = CoacheeInfo.UserActivity?.SessionsBooked >= CoacheeInfo.UserActivity?.SessionsAllocated || FirstFreeSessionNumber == 0;
      CanBookSession = SessionHelper.AppAccess.Participants.CanBookSession(ProgramInfo, CoacheeInfo);

      if (CoacheeInfo.CoachUserId != ConfigHelper.UserId.Unassigned) {
        CoachInfo = DbHelper.AlbertCoaches.GetCoachInfo(CoacheeInfo.CoachUserId);

        if (CoachInfo == null) CanBookSession = false;

      }

      if (CoacheeInfo.UserActivity?.SessionsBooked > 0) CanViewEventSessions = true;
      CanViewCoachInfo = CoacheeInfo.HasCoaching && CoacheeInfo.IsCoachAssigned && CoachInfo != null;
    }

    public string GetBookedSessionsHtml(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      // If there aren't any sessions booked, then return the default event item and prevent the db consult.
      if (coacheeInfo.UserActivity?.SessionsBooked == 0) return "";

      // If no sessions were found, then return the default event item.
      var bookedSessions = DbHelper.CoachingSessions.GetSessionsForCoacheeId(coacheeInfo.CoacheeId);

      if (bookedSessions == null) return "";

      int totalBookedSessions = bookedSessions.Count;
      int maxEvents = ConfigHelper.MaxParticipantEvents;
      string infoHtml = "";

      if (totalBookedSessions > maxEvents) {
        // Keep only the top 5 events and remove the rest
        bookedSessions = bookedSessions.Take(maxEvents).ToList();
        // If there are more sessions booked than the allowed to display in ux, then show a message with the total booked sessions.
        infoHtml = HasBookedAllSessions ? "" : $"<p>{maxEvents} / {totalBookedSessions}  Booked Sessions</p>";
      }

      // Separate the sessions in upcoming and past, sort accordingly.
      var upcomingSessions = bookedSessions.Where(x => x.ApptDateUTC >= DateTime.UtcNow).OrderBy(item => item.ApptDateUTC).ToList();
      var pastSessions = bookedSessions.Where(x => x.ApptDateUTC < DateTime.UtcNow).OrderByDescending(item => item.ApptDateUTC).ToList();

      string html = "";
      // For each session found, create a x event.

      if (!upcomingSessions.IsNullOrEmpty()) {
        html += "<h4>Upcoming</h4>";
        html += GetSessionHtml(upcomingSessions);
      }

      if (!pastSessions.IsNullOrEmpty()) {
        html += $"<h4>Past Sessions</h4>";
        html += GetSessionHtml(pastSessions);
      }

      return html + infoHtml;
    }

    private string GetSessionHtml(List<DbHelper.CoachingSessions.AbleSessionInfo> sessions) {
      string html = "";
      foreach (var s in sessions) {
        html += WebHelper.GetParticipantEventItem(s.ApptDateUTC, s.EventSessionTypeDisplayName, s.ApptVenue, s.ApptVenueAddress, WebHelper.ParticipantEventType.CoachingSession,
           PathHelper.Partials.EventSlideoutPanel_CoachingSession(s.SessionId), userInfo.TimeZoneIdIana);
      }
      return html;
    }

    public string GetBookingMessageHtml() {
      StringBuilder msgHtml = new StringBuilder();

      if (CoacheeInfo != null && CoacheeInfo.HasCoaching && CanBookSession && !UserIsNotInAProgram && HasBookedAllSessions) {

        msgHtml.Append(@"All your sessions are booked.<br />Please check your confirmation emails if you need to reschedule or cancel.");

        if (CanViewCoachInfo) {
          msgHtml.Append($@"<br/><br/>
              If you have any queries or think this is incorrect, please contact your coach:<br/>
              {CoachInfo.GetFullName().HTMLEncode()} at <a href=""mailto:{CoachInfo.EmailAddress.HTMLEncode()}"">{CoachInfo.EmailAddress.HTMLEncode()}</a>");
        }

      } else if (UserIsNotInAProgram || (CoacheeInfo != null && !CoacheeInfo.HasCoaching)) {

        msgHtml.Append($@"Please contact the sales team on <a href=""mailto:coordination@integral.global"">coordination@integral.global</a> to get information about our coaching programs.<br/>");

      } else if (!CanBookSession && !CanViewCoachInfo && !CanViewEventSessions) {

        msgHtml.Append($@"Unable to book a session at this time.<br/>");

        if (CanViewCoachInfo) {

          msgHtml.Append($@"Either your Program is not currently active, or has not yet started.<br/>
          If you think this is incorrect, please contact your coach:<br/>
          {CoachInfo.GetFullName().HTMLEncode()} at <a href=""mailto:{CoachInfo.EmailAddress.HTMLEncode()}"">{CoachInfo.EmailAddress.HTMLEncode()}</a>");

        } else {

          msgHtml.Append($@"Either your Program is not currently active, or you are yet to be assigned a Coach.<br/>
          If you think this is incorrect, please contact us on <a href=""mailto:{ConfigHelper.Email_Coordinator_Address}"">{ConfigHelper.Email_Coordinator_Address}</a>.");
        }

      } else {

        if (UserIsNotInAProgram || (CoacheeInfo != null && !CoacheeInfo.HasCoaching)) {
          msgHtml.Append($@"Please contact the sales team on <a href=""mailto:{ConfigHelper.Email_Coordinator_Address}"">{ConfigHelper.Email_Coordinator_Address}</a> to get information about our coaching programs.");
        }
      }

      if (msgHtml != null && !msgHtml.ToString().IsNullOrEmptyOrWhitespace()) {
        return $@"
          <div class=""booking-message"">
            Hi {userInfo.FirstName}, <br/><br/>
            {msgHtml}<br/><br/>
            All the best,<br/>
            The team at Able.
          </div>";
      }

      return msgHtml.ToString();
    }

    public string GetTeamCardsHtml() {
      var html = new StringBuilder();

      if (CoachesList.IsNullOrEmpty()) return "";

      foreach (var coachInfo in CoachesList) {
        html.Append(WebHelper.GetCoachInfoCard(coachInfo));
      }

      return html.ToString();
    }
  }
}
