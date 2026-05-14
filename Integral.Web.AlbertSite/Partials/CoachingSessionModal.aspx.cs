using System;
using System.Collections.Generic;
using System.Globalization;
using Integral.Database.CoachingSessions;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class CoachingSessionModal : AppCode.PageBaseClasses.CoacheeInfoBase {

    public bool IsNewSession = false;
    public DbHelper.CoachingSessions.AbleSessionInfo SessionInfo = null;
    public DbHelper.AlbertCoaches.AlbertCoachInfo CoachInfo;
    public bool CanAddSession, CanEdit, CanChangeDate, CanDelete, IsReadOnly, CanEditNotes, CanViewCoachingNotes;
    public bool UpcomingSessionsExist = false;

    public class FormFields {
      public const string ApptDateUtc = "apptdate";
      public const string SessionTime = "appttime";
      public const string DurationMins = "durationmins";
      public const string StatusName = "StatusName";
      public const string CoachNotes = "CoachNotes";
    }

    public class FormValues {
      public DateTime? ApptDateUtc;
      public int DurationMins;
      public string StatusName;
      public string CoachNotes;
    }

    public class AjaxAction {
      public const string Update = "Update";
      public const string Delete = "Delete";
    }

    protected void Page_Load(object sender, EventArgs e) {

      IsNewSession = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoachingSessionId) == PathHelper.AbleUrlValues.IdNew;
      CoachInfo = DbHelper.AlbertCoaches.GetCoachInfo(CoacheeInfo.CoachUserId);
      CanAddSession = SessionHelper.AppAccess.Sessions.CanAdd(CoacheeInfo);
      CanViewCoachingNotes = SessionHelper.AppAccess.Participants.CanViewCoachingNotes(CoacheeInfo);

      if (IsNewSession) { //New sesion.

        if (!CanAddSession) { // No access to adding new session.
          RespondNoAccessOrRedirect();
          return;
        }
        if (CoacheeInfo.UserActivity?.SessionsBooked >= CoacheeInfo.UserActivity?.SessionsAllocated) {
          RespondNoAccessOrRedirect("No more sessions can be added to this coachee.");
          return;
        }

        ProcessAddition();
        return;

      } else {

        int? sessionId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.CoachingSessionId);

        if (sessionId == null) {
          RespondNoAccessOrRedirect("Can't find Session ID.");
          return;
        }

        ProcessUpdate(sessionId.Value);
        return;
      }
    }

    void ProcessAddition() {

      SessionInfo = new DbHelper.CoachingSessions.AbleSessionInfo(CoacheeInfo.CoachUserId, CoacheeInfo.CoacheeId, CoacheeInfo.GetNextSessionType());

      IsReadOnly = false;
      CanChangeDate = true;
      CanEditNotes = true;
      CanDelete = false;

      PageTitle = "Add New Session";

      if (!SystemWeb.IsHttpPost) return;

      // Submitted new session form.

      AjaxSubmitHelper.Process(ajax => {

        if (ajax.Action == AjaxAction.Update) {
          if (UpdateSession(ajax)) {
            UpdateSessionStats(ajax);
            ajax.SetRedirectUrl(PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.coaching));
          }
        }

      });
      WebHelper.EndRequest();
      return;
    }

    void ProcessUpdate(int sessionId) {

      PageTitle = "Update Session";

      try {
        SessionInfo = DbHelper.CoachingSessions.GetSessionInfoOrNull(CoacheeInfo.CoacheeId, sessionId);
      } catch (Exception) { }

      if (SessionInfo == null) { // Session not found.
        RespondNoAccessOrRedirect("Can't find Session.");
        return;
      }

      IsReadOnly = SessionHelper.AppAccess.Sessions.ReadOnly(SessionInfo);
      CanEditNotes = SessionHelper.AppAccess.Sessions.CanEditNotes(CoacheeInfo);
      CanDelete = SessionHelper.AppAccess.Sessions.CanDelete(CoacheeInfo, SessionInfo);

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          bool success = false;

          switch (PageAjaxAction) {

            case AjaxAction.Update:
              success = UpdateSession(ajax);
              break;

            case AjaxAction.Delete:
              if (!CanDelete) {
                ajax.RespondNoAccessToFunction();
                return;
              } else {
                success = DeleteSession(ajax);
              }
              break;
          }

          if (success) {
            UpdateSessionStats(ajax);
            ajax.SetRedirectUrl(PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.coaching));
          }
        });
      }
    }

    void RespondNoAccessOrRedirect(string message = null) {

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.RespondNoAccessToFunction(message);
        WebHelper.EndRequest();
      } else {
        WebHelper.Redirect(PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.coaching));
      }
    }

    public string GetStatusOptions(string labelText) {
      string selectedValue = "";
      if (SessionInfo == null) return selectedValue;
      if (SessionInfo.ApptCancelledLate) selectedValue = StatusText(SessionStatusEnum.Cancelled_Late);
      else if (SessionInfo.ApptNoShow) selectedValue = StatusText(SessionStatusEnum.No_Show);
      else if (SessionInfo.ApptCancelledUtc != null) selectedValue = StatusText(SessionStatusEnum.Cancelled);
      return WebHelper.GetButtonGroup(labelText, FormFields.StatusName,
        new List<WebHelper.ButtonGroupButton> {
          ButtonOption(SessionStatusEnum.Normal),
          ButtonOption(SessionStatusEnum.Cancelled_Late),
          ButtonOption(SessionStatusEnum.No_Show)
        }, selectedValue, IsReadOnly);
    }

    WebHelper.ButtonGroupButton ButtonOption(SessionStatusEnum status) {
      return new WebHelper.ButtonGroupButton(StatusText(status).Replace("_", " "), StatusText(status));
    }

    string StatusText(SessionStatusEnum status) {
      return status.ToString();
    }

    FormValues GetFormValues(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();

      if (CanEditNotes && CanViewCoachingNotes) {
        formValues.CoachNotes = WebHelper.GetFormValue(FormFields.CoachNotes);
      }

      if (IsReadOnly) return formValues; // Only notes can still be edited.

      // Get date and time.
      string ApptDateUtcString = WebHelper.GetFormValue(FormFields.ApptDateUtc);
      string sessionTimeString = WebHelper.GetFormTimePicker(FormFields.SessionTime);
      if (sessionTimeString.IsNullOrEmptyOrWhitespace()) {
        ajax.AddBadField(FormFields.SessionTime, "Time is Required.");
        return null;
      }
      if (!DateTime.TryParse(ApptDateUtcString + " " + sessionTimeString, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime ApptDateUtcTimeLocal)) {
        ajax.AddBadField(FormFields.ApptDateUtc, "Date and time Required.");
        return null;
      }

      formValues.ApptDateUtc = TimeHelper.TimeZoneIdToUtc(ApptDateUtcTimeLocal, CoachInfo.TimeZoneIdIana);
      formValues.DurationMins = ajax.CheckFieldInt(FormFields.DurationMins, "Duration", 10, 600, true, "Duration required.");
      if (formValues.DurationMins < 10) ajax.AddBadField(FormFields.DurationMins, "Enter a valid number of minutes.");

      if (ajax.BadFieldCount > 0) return null;

      // Ensure session status is a valid value or default to "normal".
      string sessionStatus = WebHelper.GetFormValue(FormFields.StatusName);
      formValues.StatusName = "";
      foreach (var validStatus in SessionStatusEnum.GetValues(typeof(SessionStatusEnum))) {
        if (sessionStatus == validStatus.ToString()) { formValues.StatusName = sessionStatus; break; }
      }
      if (formValues.StatusName == "") {
        ajax.AddBadField(FormFields.StatusName, "Please select the session status.");
        return null;
      }

      return formValues;
    }

    void CopyFormToDTO(FormValues formValues) {

      if (CanEditNotes) {
        SessionInfo.CoachNotes = formValues.CoachNotes; // Can always edit notes.
      }

      if (!IsReadOnly) {

        SessionStatusEnum sessionStatus;
        if (!Enum.TryParse(formValues.StatusName, true, out sessionStatus)) sessionStatus = SessionStatusEnum.Normal;

        if (SessionInfo.ApptCancelledUtc == null) {
          if (sessionStatus == SessionStatusEnum.Cancelled || sessionStatus == SessionStatusEnum.Cancelled_Late) SessionInfo.ApptCancelledUtc = DateTime.UtcNow;
        } else {
          if (sessionStatus != SessionStatusEnum.Cancelled && sessionStatus != SessionStatusEnum.Cancelled_Late) SessionInfo.ApptCancelledUtc = null;
        }

        if (IsNewSession) SessionInfo.CoacheeId = CoacheeInfo.CoacheeId;
        SessionInfo.ApptDateUTC = (DateTime)formValues.ApptDateUtc;
        SessionInfo.DurationMins = formValues.DurationMins;
        SessionInfo.ApptCancelledLate = sessionStatus == SessionStatusEnum.Cancelled_Late;
        SessionInfo.ApptNoShow = sessionStatus == SessionStatusEnum.No_Show;
      }
    }

    bool UpdateSession(AjaxSubmitHelper ajax) {

      var formValues = GetFormValues(ajax);

      if (ajax.BadFieldCount > 0 || ajax.MessagesExist()) return false;

      CopyFormToDTO(formValues);

      try {

        if (IsNewSession) {

          int? newSessionId = DbHelper.CoachingSessions.CreateSessionInFreeComponent
            (null, CoacheeInfo, SessionInfo.ApptDateUTC, SessionInfo.DurationMins, "",
            SessionInfo.ApptCancelledUtc, SessionInfo.ApptCancelledLate, SessionInfo.ApptNoShow,
            SessionInfo.CoachNotes, null, SessionInfo.SessionNotes);

          if (newSessionId == null) {
            ajax.AddDialogMessage("Unable to add a new session to this coachee.");
            return false;
          }

          // Send Intercom event for manual session creation
          var coach = DbHelper.AbleUser.GetUserByIdOrNull(CoacheeInfo.CoachUserId, DbHelper.AbleUser.RegisteredFilter.Any);
          var coachExternalId = ConfigHelper.UserRole.Coach.ToExternalUserId(coach?.UserGuid ?? Guid.Empty);

          SendEvent(
            intercom => intercom.CoachingSessionCreated()
              .FromSession()
              .WithSessionId(newSessionId.Value)
              .WithCoach(coachExternalId)
              .WithSessionStartUtc(new DateTimeOffset(SessionInfo.ApptDateUTC, TimeSpan.Zero))
              .WithDurationMins(SessionInfo.DurationMins)
              .WithSource("manual"),
            operationName: "CoachingSessionModal_CoachingSessionCreated",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["SessionId"] = newSessionId.Value,
              ["CoachUserId"] = CoacheeInfo.CoachUserId,
              ["CoacheeId"] = CoacheeInfo.CoacheeId,
              ["Source"] = "manual"
            }
          );

        } else {

          if (SessionInfo.ComponentLocked) {
            DbHelper.CoachingSessions.UpdateSessionLocked(null,
              SessionInfo.SessionId, SessionInfo.CoachNotes, SessionInfo.SessionNotes);
          } else {
            DbHelper.CoachingSessions.UpdateSessionUnlocked(null, CoacheeInfo,
              SessionInfo.SessionId, SessionInfo.ApptDateUTC, SessionInfo.DurationMins, SessionInfo.CoachNotes,
              SessionInfo.ApptCancelledUtc, SessionInfo.ApptCancelledLate, SessionInfo.ApptNoShow, SessionInfo.SessionNotes);
          }

          // Send Intercom event for manual session update
          SendEvent(
            intercom => intercom.CoachingSessionUpdated()
              .FromSession()
              .WithSessionId(SessionInfo.SessionId)
              .WithSource("manual_update"),
            operationName: "CoachingSessionModal_CoachingSessionUpdated",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["SessionId"] = SessionInfo.SessionId,
              ["CoacheeId"] = CoacheeInfo.CoacheeId,
              ["Source"] = "manual_update"
            }
          );

        }

      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(IsNewSession ? "CreateCoachingSession" : "UpdateCoachingSession")
          .FromSession()
          .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo?.UserGuid))
          .WithProperty("SessionId", SessionInfo?.SessionId.ToString())
          .WithProperty(ApplicationInsightsConstants.IsNewSession, IsNewSession)
          .Track();

        ajax.AddDialogMessage("Failed to update database. Please try again later.<br/>" + (ConfigHelper.IsDevServer ? ex.Message : ""));
        return false;
      }

      return true;
    }

    bool DeleteSession(AjaxSubmitHelper ajax) {

      if (SessionInfo.ComponentLocked) {
        ajax.AddDialogMessage("Can't delete this session, as the component is locked.");
        return false;
      }

      try {
        DbHelper.CoachingSessions.DeleteSessionUnlocked(null, SessionInfo.SessionId, CoacheeInfo.CoacheeId);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .AddExternalUserId()
          .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo?.UserGuid))
          .WithOperation("DeleteCoachingSession")
          .WithProperty("SessionId", SessionInfo?.SessionId.ToString())
          .Track();

        ajax.AddDialogMessage("Failed to delete the session. Please try again later.", ex);
        return false;
      }

      // Send Intercom event for manual session deletion
      SendEvent(
        intercom => intercom.CoachingSessionDeleted()
          .FromSession()
          .WithSessionId(SessionInfo.SessionId)
          .WithSource("manual_deletion"),
        operationName: "CoachingSessionModal_CoachingSessionDeleted",
        requestRawUrl: SystemWeb.RequestRawUrl,
        telemetryProperties: new Dictionary<string, object> {
          ["SessionId"] = SessionInfo.SessionId,
          ["CoacheeId"] = CoacheeInfo.CoacheeId,
          ["Source"] = "manual_deletion"
        }
      );

      return true;
    }

    void UpdateSessionStats(AjaxSubmitHelper ajax) {

      // Update session stats for this coachee in Able.
      DbHelper.CoachingSessions.SessionStats sessionStats;
      try {
        sessionStats = DbHelper.CoachingSessions.GetSessionStats(null, CoacheeInfo.CoacheeId);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("GetSessionStatsForUpdate")
          .FromSession()
          .Track();

        ajax.AddDialogMessage("Error retrieving session stats. Please try again later.");
        return;
      }
      if (sessionStats == null) {
        ajax.AddDialogMessage("Failed to retrieve session stats. Please try again later.");
        return;
      }
      try {
        DbHelper.AlbertCoachees.UpdateSessionStatsAndTargetDates(null, CoacheeInfo, sessionStats);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UpdateSessionStatsAfterChange")
          .FromSession()
          .Track();

        ajax.AddDialogMessage("Error updating session stats. Please try again later.");
        return;
      }

    }
  }
}
