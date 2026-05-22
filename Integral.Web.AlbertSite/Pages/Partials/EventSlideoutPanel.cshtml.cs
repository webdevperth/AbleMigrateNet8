using System;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class EventSlideoutPanel : AppCode.PageBaseClasses.LoggedInPageModel {

    private DbHelper.WorkshopEvents.WorkshopEventInfo WorkshopEventInfo = null;
    private DbHelper.CoachingSessions.AbleSessionInfo CoachingSessionInfo = null;
    private enum EventProfileType { Coach, KeyFacilitator, CoFacilitator }
    private enum SessionType { Workshop, CoachingSession, None }
    private SessionType CurrentSessionType;

    public IActionResult OnGet() {

      PageTitle = "Event Details";

      int? urlWorkshopEventId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.WorkshopId);
      int? urlCoachingSessionId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.CoachingSessionId);

      if (urlWorkshopEventId != null) {
        WorkshopEventInfo = DbHelper.WorkshopEvents.GetWorkshopInfo((int)urlWorkshopEventId);
        if (WorkshopEventInfo != null) CurrentSessionType = SessionType.Workshop;

      } else if (urlCoachingSessionId != null) {
        CoachingSessionInfo = DbHelper.CoachingSessions.GetSessionInfoOrNull((int)urlCoachingSessionId);
        if (CoachingSessionInfo != null) CurrentSessionType = SessionType.CoachingSession;
      }

      if (CoachingSessionInfo == null && WorkshopEventInfo == null) CurrentSessionType = SessionType.None;

      return Page();
    }

    public string GetEventPanelInfo() {

      string titleText = "", contentHtml = "";
      if (CurrentSessionType == SessionType.Workshop) {
        titleText = "Workshop";
        contentHtml = GetWorkshopHtml();
      } else if (CurrentSessionType == SessionType.CoachingSession) {
        titleText = "Coaching Session";
        contentHtml = GetCoachingSessionHtml();
      }

      return $@"
        <h4 class=""align-center"">{titleText}</h4>
        <ul class=""details-list mt20"">{contentHtml}</ul>";
    }

    private string GetWorkshopHtml() {

      if (WorkshopEventInfo == null) return string.Empty;

      string htmlDetailList = "";

      htmlDetailList += $"<li><label>Title:</label><span>{WorkshopEventInfo.WorkshopTitle.HTMLEncode()}</span></li>";
      htmlDetailList += $"<li><label>Program:</label><span>{WorkshopEventInfo.ProgramJobName}</span></li>";
      htmlDetailList += "<hr/>";

      htmlDetailList += GetProfileImageForEvent(EventProfileType.KeyFacilitator, "Key Facilitator", WorkshopEventInfo.KeyFacilitatorFirstName + " " + WorkshopEventInfo.KeyFacilitatorLastName, WorkshopEventInfo.KeyFacilitatorUserId);
      htmlDetailList += GetProfileImageForEvent(EventProfileType.CoFacilitator, "Co-Facilitator", WorkshopEventInfo.CoFacilitatorFirstName + " " + WorkshopEventInfo.CoFacilitatorLastName, WorkshopEventInfo.CoFacilitatorUserId);
      htmlDetailList += $"<li><label>Delivery:</label><span class=\"w150\">{WebHelper.GetDeliveryBadge(!WorkshopEventInfo.IsVirtual)}</span></li>";

      string location = WebHelper.GetEventVenueHtml(WorkshopEventInfo.Location, WorkshopEventInfo.WhenStartUtc);
      if (!location.IsNullOrEmptyOrWhitespace()) {
        htmlDetailList += $"<li><label>Location:</label><span>{location}</span></li>";
      }

      htmlDetailList += $"<li><label>Start Date:</label><span>{GetEventDateTime(WorkshopEventInfo.WhenStartLocal)}</span></li>";
      htmlDetailList += $"<li><label>End Date:</label><span>{GetEventDateTime(WorkshopEventInfo.WhenEndLocal)}</span></li>";

      if (!WorkshopEventInfo.ParticipantAdditionalInfo.IsNullOrEmptyOrWhitespace()) {
        htmlDetailList += "<hr/>";
        htmlDetailList += $"<li><label>Information:</label><div class=\"overflow-y-auto\">{WorkshopEventInfo.ParticipantAdditionalInfo}<div></li>";
      }

      return htmlDetailList;
    }

    private string GetEventDateTime(DateTime? eventDateTimeLocal) {
      if (eventDateTimeLocal == null) return string.Empty;
      return WebHelper.DisplayDateTime((DateTime)eventDateTimeLocal, WebHelper.TimeDisplayMinutes.Yes);
    }

    private string GetCoachingSessionHtml() {

      if (CoachingSessionInfo == null) return string.Empty;

      string htmlDetailList = "";

      htmlDetailList += $"<li><label>Title:</label><span>{CoachingSessionInfo.EventSessionTypeDisplayName.HTMLEncode()}</span></li>";
      htmlDetailList += $"<li><label>Program:</label><span>{CoachingSessionInfo.ProgramJobName}</span></li>";
      htmlDetailList += "<hr/>";

      htmlDetailList += GetProfileImageForEvent(EventProfileType.Coach, "Coach", CoachingSessionInfo.CoachFirstName + " " + CoachingSessionInfo.CoachLastName, CoachingSessionInfo.CoachUserId);
      htmlDetailList += $"<li><label>Delivery:</label><span class=\"w150\">{WebHelper.GetDeliveryBadge(CoachingSessionInfo.SessionTypeInPerson)}</span></li>";

      string venue = WebHelper.GetEventVenueHtml(CoachingSessionInfo.ApptVenue, CoachingSessionInfo.ApptDateUTC);
      string venueAddress = WebHelper.GetEventVenueHtml(CoachingSessionInfo.ApptVenueAddress, CoachingSessionInfo.ApptDateUTC);
      if (!venue.IsNullOrEmptyOrWhitespace()) {
        htmlDetailList += $"<li><label>Location:</label><span>{venue}</span></li>";
      }
      if (!venueAddress.IsNullOrEmptyOrWhitespace()) {
        htmlDetailList += $"<li><label>{(venue.IsNullOrEmptyOrWhitespace() ? "Location" : "Address")}:</label><span>{venueAddress}</span></li>";
      }

      if (CoachingSessionInfo.DurationMins > 0) {
        htmlDetailList += $"<li><label>Duration:</label><span>{CoachingSessionInfo.DurationMins} mins.</span></li>";
      }

      htmlDetailList += $"<li><label>Date:</label><span>{WebHelper.DisplayDate(CoachingSessionInfo.ApptDateUTC)}</span></li>";

      if (!CoachingSessionInfo.SessionNotes.IsNullOrEmptyOrWhitespace()) {
        htmlDetailList += " <hr/>";
        htmlDetailList += $"<li><label>Notes:</label><div class=\"overflow-y-auto\">{CoachingSessionInfo.SessionNotes}<div></li>";
      }

      return htmlDetailList;
    }

    private string GetProfileImageForEvent(EventProfileType profileType, string label, string name, int? userId) {

      if (userId == null || userId == ConfigHelper.UserId.Unassigned) {

        if (profileType == EventProfileType.CoFacilitator) return string.Empty;

        return $"<li><label>{label}:</label><span>{ConfigHelper.UnassignedUser_FullName}</span></li>";
      }

      string userPhoto = "";

      if (profileType == EventProfileType.Coach) {
        userPhoto = PathHelper.Images.UserPhoto(CoachingSessionInfo, PathHelper.Images.UserPhotoSize.Large, true);
      } else {
        bool isCoFacilitator = profileType == EventProfileType.CoFacilitator;
        userPhoto = PathHelper.Images.UserPhoto(WorkshopEventInfo, PathHelper.Images.UserPhotoSize.Large, isCoFacilitator, true, false);
      }

      return $"<li><label>{label}:</label><span class=\"user-avatar-horizontal\">{WebHelper.GetAvatarForTable_User(userPhoto, name, userId)}</span></li>";
    }

  }
}
