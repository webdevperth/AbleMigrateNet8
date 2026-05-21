using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using Integral.Integrations.Intercom;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static Integral.Web.DbHelper.Common;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class AlbertAutoReminders : PageModel {

    // If testing, send to address set up in appSettings tag overrideRecipientEmail and processed in ConfigHelper, otherwise use default.
    readonly string TestRecipientAddress = ConfigHelper.IsDevServer ? ConfigHelper.EmailRecipientOverrideAddress : "andrew@integral.global";
    const int TestMaxEmailsPerProcess = 1; // if testing, don't send more than this number of emails **per process** (per ProcessEnum function).
    const int CATCHUP_WINDOW_DAYS = 10; // "Catch-up" window for queries in case this process fails to run for a few days.
    const int WelcomeEmail_LagWindowDays = 14; // Send welcome within this number of days from welcome email date.
    const int DeleteTempFilesOlderThanDays = 1; // Send welcome within this number of days from welcome email date.
    const int WorkshopEvalsCompletedNotice_MinCompleted = 4;

    const int POSTSESSION_EVAL_SESSION_NUMBER = 1;
    const int POSTSESSION_EVAL_MIN_SESSIONS = 2;
    const int POSTPROGRAM_EVAL_MIN_SESSIONS = 1;
    const int POSTPROGRAM_EVAL_DURATION_DAYS = 7; // Days from now to close date.
    const int WORKSHOP_EVAL_DURATION_DAYS = 7; // Days from now to close date.
    const int POSTPROJECT_EVAL_DURATION_DAYS = 7; // Days from now to close date.

    // Process-wide last-run timestamp (was: Application["AutoReminders_LastRun"]).
    static DateTime? _autoRemindersLastRunUtc;

    class UrlParams {
      public const string TestMode = "test";
      public const string ForceRun = "forcerun";
      public const string NoEmails = "noemails";
      public const string NoLogEmail = "nologemail";
      public const string RunOnly = "runonly";
      public const string SendOnlyToEmail = "sendonlytoemail";
      public const string UpdateSentDates = "sentdates";
      public const string PatchCompanyId = "patchcmpid";
      public const string PatchJobId = "patchjobid";
      public const string PatchSurveyId = "patchsvid";
      public const string OnlyJobNumber = PathHelper.AbleUrlKeys.ProjectJobNumber;
      public const string OnlyCompanyId = PathHelper.AbleUrlKeys.CompanyId;
      public const string TestMaxEmailsPerProcess = "emailspp";
    }

    enum ProcessEnum {
      None,
      General,
      UpdateSurveyInfo,
      UpdateCompanyInfo,
      WelcomeEmails,
      MeetCoachEmails,
      NudgeEmails,
      WorkshopEvals,
      SessionEvals,
      ProgramEvals_FinalCoachingSession,
      BookingReminders,
      SetCoacheesToEndProgram,
      RegistrationInvites,
      ClientInvitesForAcceptedQuotes,
      ParticipantPulseSurveys,
      DeleteOldTempFiles,
      EndOfProjectEmails,
      SetPartnerActiveStatus,
      WorkshopEvalCompletedEmails,
      CreateOrLinkCoacheeToUserId,
      DeleteOldUserSessions,
      SurveyFirstInvites,
      SurveyUserReminders,
      SendScheduledContent,
      RedactUsers,
      UpdateProgramSummaries
    }

    enum IsCriticalEnum { No, Yes }

    ProcessEnum currentRunningProcess = ProcessEnum.None;
    string currentRunningProcessTitle = null;

    class LoggedError {

      public ProcessEnum CurrentRunningProcess = ProcessEnum.None;
      public string Message;
      public string LatestSQLText;
      public Exception Exception;

      public LoggedError(AlbertAutoReminders page, Exception exception = null) {
        Init(page, null, null, exception);
      }

      public LoggedError(AlbertAutoReminders page, string message, Exception exception = null) {
        Init(page, message, null, exception);
      }

      public LoggedError(AlbertAutoReminders page, string message, string latestSQLText, Exception exception = null) {
        Init(page, message, latestSQLText, exception);
      }

      private void Init(AlbertAutoReminders page, string message, string latestSQLText, Exception exception) {
        CurrentRunningProcess = page.currentRunningProcess;
        Message = $"Process: {page.currentRunningProcess}: {message.RegexReplace("^[ -]", "")}";
        Exception = exception;
        LatestSQLText = latestSQLText;
      }
    }

    class RunFlags {
      public bool TestMode = false;
      public bool CanSendSurveyRemindersToday = false; // Whether to send survey reminders today.
      public bool UpdateSentDates = true;
      public bool NoEmails = false;
      public bool ForceRun = false;
      public List<ProcessEnum> RunOnlyProcesses = new List<ProcessEnum>(); // Selected processes to run otherwise all.
      public string SendOnlyToEmail = null;
      public bool CanSendSessionBookingRemindersToday = false;
      public int PatchJobId = 0;
      public int PatchSurveyId = 0;
      public int PatchCompanyId = 0;
      public bool NoLogEmail = false;
      public string OnlyJobNumber = null;
      public int? OnlyCompanyId = null;
      public int TestMaxEmailsPerProcess = AlbertAutoReminders.TestMaxEmailsPerProcess;
    }

    RunFlags runFlags = new RunFlags();
    StringBuilder sbLog = new StringBuilder(); // For log messages.

    DateTime currentTime_appZone;
    DayOfWeek[] weekdaysToRemind = new DayOfWeek[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
    bool todayIsWeekend;

    List<LoggedError> loggedErrors = new List<LoggedError>();

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      // Top level try/catch to ensure we're alerted if Main() fails before activity log is sent.
      try {
        Main();
      } catch (Exception ex) {
        if (!ex.Message.ContainsIgnoreCase("Thread was being aborted")) {
          // Manual send of email alert and the current log state.
          EmailHelper.SendInternalSupportEmail(ex, "AutoReminders Critical Fail", sbLog.ToString());
        }
      }
      return new EmptyResult();
    }

    void Main() {

      SystemWeb.SetContentType(WebHelper.GetContentTypeString(WebHelper.HttpContentType.Text));

      DateTime serverStartTime = DateTime.Now;

      currentTime_appZone = (DateTime)TimeHelper.UtcToAppDefaultTimeZone(DateTime.UtcNow); // Current time in app default zone.
      todayIsWeekend = currentTime_appZone.DayOfWeek == DayOfWeek.Saturday || currentTime_appZone.DayOfWeek == DayOfWeek.Sunday;

      runFlags.TestMode = ConfigHelper.IsDevServer ? true : !("" + WebHelper.GetQueryStringValue(UrlParams.TestMode)).IsNullOrEmpty(); // Test mode won't sent out emails.
      runFlags.ForceRun = runFlags.TestMode ? true : !("" + WebHelper.GetQueryStringValue(UrlParams.ForceRun)).IsNullOrEmpty(); // Force run regardless of time of day etc.
      runFlags.NoEmails = !("" + WebHelper.GetQueryStringValue(UrlParams.NoEmails)).IsNullOrEmpty();
      runFlags.SendOnlyToEmail = WebHelper.GetQueryStringValue(UrlParams.SendOnlyToEmail)?.Replace(" ", "+"); // Saves encoding the "+" manually.
      runFlags.PatchJobId = WebHelper.GetQueryStringValue(UrlParams.PatchJobId).ToIntOrDefault(0);
      runFlags.PatchSurveyId = WebHelper.GetQueryStringValue(UrlParams.PatchSurveyId).ToIntOrDefault(0);
      runFlags.PatchCompanyId = WebHelper.GetQueryStringValue(UrlParams.PatchCompanyId).ToIntOrDefault(0);
      runFlags.NoLogEmail = !("" + WebHelper.GetQueryStringValue(UrlParams.NoLogEmail)).IsNullOrEmpty();
      runFlags.OnlyJobNumber = WebHelper.GetQueryStringValue(UrlParams.OnlyJobNumber);
      runFlags.OnlyCompanyId = WebHelper.GetQueryStringValue(UrlParams.OnlyCompanyId).ToIntOrNull();
      runFlags.TestMaxEmailsPerProcess = WebHelper.GetQueryStringValue(UrlParams.TestMaxEmailsPerProcess).ToIntOrNull() ?? TestMaxEmailsPerProcess;

      // Check for selected processes.
      if (!string.IsNullOrEmpty(WebHelper.GetQueryStringValue(UrlParams.RunOnly))) {
        foreach (string processName in WebHelper.GetQueryStringValue(UrlParams.RunOnly).Split(',')) {
          if (Enum.TryParse(processName, true, out ProcessEnum process)) {
            runFlags.RunOnlyProcesses.Add(process);
          } else {
            LogMessage($"Aborting - \"{processName}\" is not a valid value for \"{UrlParams.RunOnly}\".", true);
            SystemWeb.ResponseWriteLine(sbLog.ToString());
            return;
          }
        }
      }

      LogMessage("Able AutoReminders for " + currentTime_appZone.ToString("d MMM yyyy, h:m tt"));
      LogMessage("Called by: " + SystemWeb.RequestUserAgent);
      LogMessage("QueryString: " + SystemWeb.RequestQueryString);
      LogMessage("Day of the week is " + currentTime_appZone.DayOfWeek.ToString());
      LogMessage("Processes to run: " + (runFlags.RunOnlyProcesses.IsNullOrEmpty() ? "All" : runFlags.RunOnlyProcesses.Join(", ", p => p.ToString())));
      LogMessage("");

      // Catch unhandled process errors here to ensure the activity log is still completed and delivered.
      try {
        RunDailyProcesses();
      } catch (Exception ex) {
        LogMessage("Uncaught process error.", ex);
      }

      LogMessage("");
      LogMessage("Done.");
      LogMessage("Total Time: " + (DateTime.Now - serverStartTime).TotalSeconds.ToString("0") + " seconds.");

      SystemWeb.ResponseWriteLine(GetLoggedErrorsText());

      SystemWeb.ResponseWriteLine(sbLog.ToString());

      // Clear request-level max-emails restriction in case it was set above, to ensure the activity email is sent.
      EmailHelper.ClearMaxEmailsToSendForRequest();

      // Send activity email.
      var recipientsOfActivitySummaryEmail = new List<MailAddress>() {
        new MailAddress("andrew@helloable.co", "Andrew Hollander")
      };
      if (ConfigHelper.IsLiveServer && !runFlags.TestMode && !runFlags.NoEmails) {
        recipientsOfActivitySummaryEmail.Add(new MailAddress("jeroen@integral.global", "Jeroen"));
        recipientsOfActivitySummaryEmail.Add(new MailAddress("cj@integral.global", "CJ"));
      }

      if (!runFlags.NoEmails && !runFlags.NoLogEmail) {
        SendActivitySummaryEmail(recipientsOfActivitySummaryEmail);
      }

    }

    void RunDailyProcesses() {

      if (!ConfigHelper.IsDevServer && _autoRemindersLastRunUtc != null && (DateTime.UtcNow - _autoRemindersLastRunUtc.Value).TotalSeconds < 30) {
        LogMessage("Aborting - last run less than 30 seconds ago.");
        return;
      }

      _autoRemindersLastRunUtc = DateTime.UtcNow;

      if (runFlags.TestMode || !ConfigHelper.IsLiveServer) {
        // Defaults for test mode.
        EmailHelper.SetRecipientOverrideAddressForRequest("AutoReminder Test Person", TestRecipientAddress); // All emails during this request go to the test address.
      }

      if (runFlags.TestMode) {
        runFlags.CanSendSurveyRemindersToday = true;
        runFlags.CanSendSessionBookingRemindersToday = true;
        runFlags.UpdateSentDates = WebHelper.GetQueryStringValue(UrlParams.UpdateSentDates) == "1"; // Test mode - false unless set to true.
        LogMessage("*** Test Mode ***");
        LogMessage("TestRecipientAddress: " + TestRecipientAddress);
        LogMessage("MaxEmailsPerProcess: " + runFlags.TestMaxEmailsPerProcess);
      } else {
        runFlags.CanSendSurveyRemindersToday = Array.IndexOf(weekdaysToRemind, currentTime_appZone.DayOfWeek) >= 0;
        runFlags.CanSendSessionBookingRemindersToday = !todayIsWeekend;
        runFlags.UpdateSentDates = WebHelper.GetQueryStringValue(UrlParams.UpdateSentDates) != "0"; // Live - true unless set to false.
      }

      LogMessage("Update Sent Times = " + runFlags.UpdateSentDates);
      LogMessage("Send survey reminders today = " + runFlags.CanSendSurveyRemindersToday);

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        LogMessage($"Sending ONLY to email: {runFlags.SendOnlyToEmail}");
      }

      if (runFlags.NoEmails) {
        EmailHelper.SetMaxEmailsToSendForRequest(0);
        LogMessage("NoEmails - SetMaxEmailsToSendForRequest: 0");
      }

      // Only run at 7am-ish WST Perth time.
      if (currentTime_appZone.Hour != 7 && !runFlags.ForceRun) {
        LogMessage("This script should run automatically at 7am-ish " + ConfigHelper.DefaultTimeZoneAbbrev + ".", true);
        return;
      }

      // Set Programs to status "Active".
      if (!RunProcess(SetProgramsToActive, ProcessEnum.General, IsCriticalEnum.Yes, "Setting Programs to Active")) {
        return; // Can't continue if this fails.
      }

      // Update session stats.
      if (!RunProcess(UpdateSessionStats, ProcessEnum.General, IsCriticalEnum.Yes, "Updating Session Stats")) {
        return; // Can't continue if this fails.
      }

      // Update survey norms.
      RunProcess(PatchSurveyInfo, ProcessEnum.UpdateSurveyInfo, IsCriticalEnum.Yes, "Updating Survey Info");

      // Update company info.
      RunProcess(UpdateCompanyInfo, ProcessEnum.UpdateCompanyInfo, IsCriticalEnum.Yes, "Updating Company Info");

      // Send Welcome emails.
      RunProcess(SendWelcomeEmails, ProcessEnum.WelcomeEmails, IsCriticalEnum.No, "Sending Welcome Emails");

      // Send Meet Coach Emails.
      RunProcess(SendMeetCoachEmails, ProcessEnum.MeetCoachEmails, IsCriticalEnum.No, "Sending Meet Coach Emails");

      // Send Nudge Content Emails.
      RunProcess(SendNudgeEmails, ProcessEnum.NudgeEmails, IsCriticalEnum.No, "Sending Nudge Content Emails");

      // Send Post-Session Evals.
      RunProcess(SendPostFirstSessionEvals, ProcessEnum.SessionEvals, IsCriticalEnum.No, "Sending Post-Session Evals");

      // Send Session Booking Reminders.
      RunProcess(SendSessionBookingReminders, ProcessEnum.BookingReminders, IsCriticalEnum.No, "Sending Session Booking Reminders");

      // Set coachees to end-program. Must occur after Post Session Evals.
      if (!RunProcess(SetCoacheesToEndProgram, ProcessEnum.SetCoacheesToEndProgram, IsCriticalEnum.Yes, "Setting Coachees to End-Program")) {
        return; // Can't continue if this fails.
      }

      // Send Workshop Evals. Must occur after SetCoacheesToEndProgram().
      RunProcess(SendWorkshopEvals, ProcessEnum.WorkshopEvals, IsCriticalEnum.No, "Sending Workshop Evals");

      // Set Programs to status "Complete". Must occur after SetCoacheesToEndProgram().
      if (!RunProcess(SetProgramsToComplete, ProcessEnum.General, IsCriticalEnum.Yes, "Setting Programs to Complete")) {
        return; // Can't continue if this fails.
      }

      // Send post-program evals. Must occur after SetProgramsToComplete().
      RunProcess(SendPostProgramEvals_FinalCoachingSession, ProcessEnum.ProgramEvals_FinalCoachingSession, IsCriticalEnum.No, "Sending Post-Program Evals");

      RunProcess(SendSurveyFirstInvites, ProcessEnum.SurveyFirstInvites, IsCriticalEnum.No, "Sending Survey First Invites");

      RunProcess(SendSurveyUserReminders, ProcessEnum.SurveyUserReminders, IsCriticalEnum.No, "Sending Survey User Reminders");

      // Send User Registration Invites (repeats same for reminders).
      RunProcess(SendRegistrationInvites, ProcessEnum.RegistrationInvites, IsCriticalEnum.No, "Sending User Registration Invites");

      // Send Registration Invites for Clients with Quotes Accepted Yesterday.
      RunProcess(SendClientInvitesForAcceptedQuotes, ProcessEnum.ClientInvitesForAcceptedQuotes, IsCriticalEnum.No, "Sending Client Invites for Quotes Accepted Yesterday");

      // Participants' Pulse Surveys
      RunProcess(SendParticipantPulseSurveys, ProcessEnum.ParticipantPulseSurveys, IsCriticalEnum.No, "Sending Participant Pulse Surveys");

      // Delete old Temp files.
      RunProcess(DeleteOldTempFiles, ProcessEnum.DeleteOldTempFiles, IsCriticalEnum.No, "Deleting Old Temp Files");

      // Send End of Project Emails to Clients
      RunProcess(SendEndOfProjectEmails, ProcessEnum.EndOfProjectEmails, IsCriticalEnum.No, "Sending End of Project Emails");

      // Set Partners status to inactive
      RunProcess(SetPartnerActiveStatus, ProcessEnum.SetPartnerActiveStatus, IsCriticalEnum.No, "Setting Partners to Inactive");

      // Send Workshop Eval Completed Emails
      RunProcess(SendWorkshopEvalCompletedEmails, ProcessEnum.WorkshopEvalCompletedEmails, IsCriticalEnum.No, "Sending Workshop Eval Completed Emails");

      // Create or Link Coachees to User
      RunProcess(CreateOrLinkCoacheeToUserId, ProcessEnum.CreateOrLinkCoacheeToUserId, IsCriticalEnum.No, "Creating or Linking Coachees to User");

      // Create or Link Coachees to User
      RunProcess(DeleteOldUserLoginSessions, ProcessEnum.DeleteOldUserSessions, IsCriticalEnum.No, "Deleting Old User Sessions");

      // Send Program Scheduled Content Emails
      RunProcess(SendScheduledContent, ProcessEnum.SendScheduledContent, IsCriticalEnum.No, "Sending Program Scheduled Content Emails");

      // Send Program Scheduled Content Emails
      RunProcess(RedactUserData, ProcessEnum.RedactUsers, IsCriticalEnum.No, "Redacting Users");

      // Send Program Scheduled Content Emails
      RunProcess(UpdateProgramSummaries, ProcessEnum.UpdateProgramSummaries, IsCriticalEnum.No, "Updateing Program Summaries");

    } // main

    void DeleteOldTempFiles() {

      var dirInfo = new System.IO.DirectoryInfo(PathHelper.ServerPaths.UploadTemp);

      // Delete old folders.
      DeleteOldTempObjects(dirInfo.GetDirectories());
      // Delete old files.
      DeleteOldTempObjects(dirInfo.GetFiles());

      // Handles both file list and folder list.
      void DeleteOldTempObjects(System.IO.FileSystemInfo[] fsObjects) {

        string objType;
        if (fsObjects == null) {
          return;
        } else if (fsObjects is DirectoryInfo[]) {
          objType = "Directory";
        } else if (fsObjects is FileInfo[]) {
          objType = "File";
        } else {
          return;
        }

        if (fsObjects.Length == 0) {
          LogMessage($" - No temp {objType.ToPlural(0)} found.");
          return;
        }

        LogMessage($" - {fsObjects.Length} temp {objType.ToPlural(fsObjects.Length)} found.");

        int tryCount = 0;
        int successCount = 0;
        foreach (var dirObj in fsObjects) {
          if (dirObj.Name.EqualsIgnoreCase(".gitignore")) continue;
          if ((DateTime.UtcNow - dirObj.CreationTimeUtc).TotalDays > DeleteTempFilesOlderThanDays) {
            try {
              tryCount++;
              Directory.Delete(dirObj.FullName, true);
              successCount++;
              LogMessage($" - Deleted folder {dirObj.FullName}");
            } catch (Exception ex) {
              LogMessage($" - Error deleting folder {dirObj.FullName}", ex);
            }
          }
        }

        LogMessage($" - {successCount} of {tryCount} {objType.ToPlural(fsObjects.Length)} deleted.");
      }
    }

    private bool RunProcess(Action processAction, ProcessEnum process, IsCriticalEnum isCritical, string processTitle) {

      currentRunningProcess = process;
      currentRunningProcessTitle = processTitle;

      if (runFlags.TestMode && !runFlags.NoEmails) {
        // Reset max emails to send for this process.
        EmailHelper.SetMaxEmailsToSendForRequest(runFlags.TestMaxEmailsPerProcess);
        EmailHelper.SetEmailsSentThisRequest(0);
      }

      if (isCritical == IsCriticalEnum.No
        && !runFlags.RunOnlyProcesses.IsNullOrEmpty()
        && !runFlags.RunOnlyProcesses.Contains(process)) {
        // RunOnlyProcesses option is set, and this is _not_ one of the selected processes, so we don't run this one.
        return false;
      }

      LogMessage("");
      LogMessage($"{processTitle}...");

      var stopwatch = new Stopwatch();
      stopwatch.Start();

      try {
        processAction();
        LogMessage($"Elapsed Time for {processTitle}: " + stopwatch.Elapsed.TotalSeconds.ToString("0.00") + " seconds.");
      } catch (Exception ex) {
        if (ex.Message.ToLower() != "thread was being aborted.") {
          LogMessage($"ERROR: Process '{processTitle}' did not run:", ex);
          try {
            LogErrorToDb(ex);
          } catch (Exception ex2) {
            LogMessage($"ERROR!!!: Could not save error ErrorLog table.", ex2);
          }
        }
        return false;
      }
      return true;
    }

    void LogErrorToDb(Exception ex) {

      var errorLogInfo = new DbHelper.ErrorLog.ErrorLogInfo(
        occurredUtc: DateTime.UtcNow,
        errorMessage: ex.Message,
        stackTraceText: ex.ToString(),
        isJSError: false,
        jsErrorUrl: null,
        jsErrorLine: null,
        jsErrorColumn: null,
        browserUserAgent: null,
        httpMethod: "GET",
        requestUrl: SystemWeb.RequestRawUrl,
        queryUrlDecoded: SystemWeb.UrlDecode(SystemWeb.RequestQueryString),
        referrerUrl: null,
        requestHeaders: null,
        formOriginal: null,
        formUrlDecoded: null,
        loggedInUserId: null,
        loggedInUserEmail: null,
        userRole: null,
        sessionGuid: null,
        latestSql: LogHelper.GetLatestSqlQueryText()
      );

      DbHelper.ErrorLog.Add(errorLogInfo);
    }

    void LogMessage(string message, Exception ex) {
      LogMessage(sbLog, message, ex);
    }

    void LogMessage(List<string> messages, Exception ex = null) {
      if (messages.IsNullOrEmpty()) return;
      // Log all messages. If there is an exception, log it only with the final message.
      int msgCount = 0;
      foreach (var message in messages) {
        msgCount++;
        if (msgCount == messages.Count) {
          LogMessage(message, ex);
        } else {
          LogMessage(message);
        }
      }
    }

    void LogMessage(string message, bool logError = false) {
      LogMessage(sbLog, message, null);
      if (logError) loggedErrors.Add(new LoggedError(this, message));
    }

    void LogMessage(StringBuilder sbLogTemp, string message, Exception ex) {
      string errorMessage = "";
      string latestSQLText = "";
      if (ex != null) {
        if (ex is SqlException) {
          latestSQLText = "\n--- Latest SQL ---\n" + LogHelper.GetLatestSqlQueryText() + "\n------------------\n";
        }
        loggedErrors.Add(new LoggedError(this, message, latestSQLText, ex));
        errorMessage += ex.Message;
        if (ex.InnerException != null) errorMessage += " " + ex.InnerException.Message;
      }
      sbLogTemp.AppendLine(message
        + errorMessage.SurroundWith(" (", ")")
      );
    }

    void LogMessageWithAlertEmail(string message, string extraContextForEmail, Exception ex = null) {
      LogMessage(sbLog, message, ex);
      EmailHelper.SendInternalSupportEmail(ex, "AutoReminders Error", extraContextForEmail.EnsureEndsWith("<br/><br/>", StringExt.Ensure.IfNotBlank) + message);
    }

    void UpdateSessionStats() {

      int updated = GetNonQueryInt(@"
        UPDATE ac
        SET ac.SessionsBooked = cs.SessionsBooked,
            ac.SessionsUpcoming = cs.SessionsUpcoming,
            ac.SessionsCompleted = cs.SessionsBooked - cs.SessionsUpcoming,
            ac.NextApptDateUTC = cs.NextApptDateUTC
        FROM al_Coachees ac
        CROSS APPLY (
          SELECT COUNT(cs.ApptDateUTC) AS SessionsBooked,
            COUNT(IIF(cs.ApptCancelledUTC IS NULL AND cs.ApptDateUTC > GETUTCDATE(), 1, NULL)) AS SessionsUpcoming,
            MIN(IIF(cs.ApptCancelledUTC IS NULL AND cs.ApptDateUTC > GETUTCDATE(), cs.ApptDateUTC, NULL)) AS NextApptDateUtc
          FROM id_CoachingSession cs
          WHERE cs.AbleCoacheeId = ac.CoacheeId
        ) AS cs
        WHERE ac.SessionsBooked <> cs.SessionsBooked
          OR ac.SessionsCompleted <> cs.SessionsBooked - cs.SessionsUpcoming
          OR ac.SessionsUpcoming <> cs.SessionsUpcoming
          OR ISNULL(ac.NextApptDateUTC, '2000-01-01') <> ISNULL(cs.NextApptDateUTC, '2000-01-01')"
      );

      LogMessage($" - {updated} Session Stats updated.");

      updated = GetNonQueryInt($@"
        UPDATE al_Coachees SET
          NextBookingTargetDateUtc =
            CASE
              WHEN NextApptDateUTC IS NOT NULL THEN
                DATEADD(DAY, @LatestBooking_to_NextBookingTargetDate_Days, NextApptDateUTC)
              WHEN MeetCoachEmailUtc IS NOT NULL THEN
                DATEADD(DAY, @MeetCoachEmail_to_FirstBookingTargetDate_Days, MeetCoachEmailUtc)
            END
        WHERE ProgramStatusId < @CoacheeProgramStatusId_EndProgram
          AND CoachUserId <> @UnassignedUserId
          AND SessionsBooked < SessionsAllocated
          AND (NextApptDateUTC IS NOT NULL OR MeetCoachEmailUtc IS NOT NULL)
          AND (ISNULL(NextBookingTargetDateUtc, '2000-01-01') < NextApptDateUTC
            OR ISNULL(NextBookingTargetDateUtc, '2000-01-01') < MeetCoachEmailUtc)",
        NewSqlParameter("LatestBooking_to_NextBookingTargetDate_Days", ConfigHelper.DaysBetweenEvents.LatestBooking_to_NextBookingTargetDate),
        NewSqlParameter("MeetCoachEmail_to_FirstBookingTargetDate_Days", ConfigHelper.DaysBetweenEvents.MeetCoachEmail_to_FirstBookingTargetDate),
        NewSqlParameter("CoacheeProgramStatusId_EndProgram", DbHelper.CoacheeProgramStatus.Ids.EndProgram),
        NewSqlParameter("UnassignedUserId", ConfigHelper.UserId.Unassigned)
      );

      LogMessage($" - {updated} Booking Target Dates updated.");

      updated = GetNonQueryInt(@"
        UPDATE al_Coachees
        SET NextBookingSendReminderEmailUtc = DATEADD(DAY, -@BookingReminder_To_BookingTargetDate_Days, NextBookingTargetDateUtc)
        WHERE ProgramStatusId < @CoacheeProgramStatusId_EndProgram
          AND CoachUserId <> @UnassignedUserId
          AND SessionsUpcoming > 0
          AND NextBookingTargetDateUtc IS NOT NULL
          AND NextBookingSendReminderEmailUtc <> DATEADD(DAY, -@BookingReminder_To_BookingTargetDate_Days, NextBookingTargetDateUtc);",
        NewSqlParameter("BookingReminder_To_BookingTargetDate_Days", ConfigHelper.DaysBetweenEvents.BookingReminder_To_BookingTargetDate),
        NewSqlParameter("CoacheeProgramStatusId_EndProgram", DbHelper.CoacheeProgramStatus.Ids.EndProgram),
        NewSqlParameter("UnassignedUserId", ConfigHelper.UserId.Unassigned)
      );

      LogMessage($" - {updated} Next Booking Reminder Dates updated.");

      updated = GetNonQueryInt($@"
        UPDATE ac SET
          ac.Send360ReportUtc = DATEADD(DAY, -@Send360Report_To_FirstSessionDate_Days, cs.FirstApptDateUtc)
        FROM al_Coachees ac
        CROSS APPLY (
          SELECT MIN(cs.ApptDateUtc) AS FirstApptDateUtc
          FROM id_CoachingSession cs
          WHERE cs.AbleCoacheeId = ac.CoacheeId
        ) AS cs
        WHERE ac.ProgramStatusId < @CoacheeProgramStatusId_EndProgram
          AND CoachUserId <> @UnassignedUserId
          AND ac.SessionsBooked > 0
          AND ac.SessionsCompleted = 0
          AND ac.Send360ReportUtc IS NULL",
        NewSqlParameter("Send360Report_To_FirstSessionDate_Days", ConfigHelper.DaysBetweenEvents.Send360Report_To_FirstSessionDate),
        NewSqlParameter("CoacheeProgramStatusId_EndProgram", DbHelper.CoacheeProgramStatus.Ids.EndProgram),
        NewSqlParameter("UnassignedUserId", ConfigHelper.UserId.Unassigned)
      );

      LogMessage($" - {updated} Send360Report Dates updated.");
    }

    void PatchSurveyInfo() {

      var patchLog = new List<string>();

      try {
        if (runFlags.PatchSurveyId > 0) {
          LogMessage($" - For SurveyId {runFlags.PatchSurveyId}.");
          DbHelper.AlbertSurveys.PatchSingleSurvey(runFlags.PatchSurveyId, patchLog);
        } else if (runFlags.PatchJobId > 0) {
          LogMessage($" - For ProgramJobId {runFlags.PatchJobId}.");
          DbHelper.AlbertSurveys.PatchSurveysInProgram(runFlags.PatchJobId, patchLog);
        } else if (runFlags.PatchCompanyId > 0) {
          LogMessage($" - For CompanyId {runFlags.PatchCompanyId}.");
          DbHelper.AlbertSurveys.PatchSurveysInCompany(runFlags.PatchCompanyId, patchLog);
        } else {
          DbHelper.AlbertSurveys.PatchAllSurveys(patchLog);
        }
        LogMessage(patchLog);
      } catch (Exception ex) {
        LogMessage(patchLog, ex);
      }
    }

    void UpdateCompanyInfo() {

      try {
        DbHelper.ClientCompanies.UpdateActiveLearnerCounts(DateTime.UtcNow.AddDays(-ConfigHelper.CompanyActiveLearnerWindowDays));
        LogMessage($" - Updated company info.");
      } catch (Exception ex) {
        LogMessage($" - Error updating company info.", ex);
      }
    }

    void SendSurveyFirstInvites() {

      var surveyInfoCache = new DbHelper.AlbertSurveys.SurveyInfoCache();
      var workshopInfoCache = new DbHelper.WorkshopEvents.WorkshopInfoCache();
      var projectInfoCache = new DbHelper.Projects.ProjectInfoCache();

      Query(@"
        SELECT
          cmp.SvCompanyId, cmp.CompanyName,
          ac.CoacheeId, ac.ProgramJobId,
          s.sv_id, s.IsAlbertSurvey, s.AlbertRatersOnly, s.sv_title, s.sv_FeedbackOption,
          dc.Code AS IntakeNumber, dc.Value AS IntakeName,
          dc.IntakeCloseDate, dc.IntakeCloseDateSelf, dc.ScheduledStartDateUtc,
          p.PartId, p.UniqueId AS PartUID,
          p.Name, p.FirstName, p.LastName, p.Email, p.Completed,
          p.FirstInvitationSent, p.LastInvitationSent,
          p.IsSelf, p.Self_PartId,
          pr.RaterCount,
          pself.Name AS SelfName, pself.FirstName AS SelfFirstName, pself.LastName AS SelfLastName, pself.Email AS SelfEmail,
          u.FirstName AS CoachFirstName, u.LastName AS CoachLastName, u.Email AS CoachEmail,
          j.JobNumber
        FROM sv_360_Participants p
        INNER JOIN sv_Survey s ON p.SurveyId = s.sv_id
        INNER JOIN sv_360_AnswerTypes dt ON s.sv_id = dt.SurveyId AND dt.AnswerTypeDescr = 'date'
        INNER JOIN sv_360_Codes dc ON dt.AnswerTypeId = dc.AnswerTypeId AND dc.Code = p.DateGroupCode
        OUTER APPLY (
          SELECT TOP 1 lc.CoacheeId -- Latest coachee
          FROM al_Coachees lc
          INNER JOIN id_Job lj ON lj.JobId = lc.ProgramJobId
          WHERE lc.UserId = p.UserId
            AND GETUTCDATE() BETWEEN lj.AbleProgramStartDateUtc AND lj.AbleProgramEndDateUtc
          ORDER BY lc.RowCreatedUtc DESC
        ) AS lc
        LEFT OUTER JOIN al_Coachees ac ON ac.CoacheeId = ISNULL(p.AbleCoacheeId, lc.CoacheeId)
        LEFT OUTER JOIN sv_User u ON u.UserId = ac.CoachUserId
        LEFT OUTER JOIN sv_SurveyCompany cmp ON ac.CompanyId = cmp.SvCompanyId
        LEFT OUTER JOIN id_Job j ON j.JobId = ac.ProgramJobId
        LEFT OUTER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
        CROSS APPLY (
          SELECT COUNT(pr.PartId) AS RaterCount
          FROM sv_360_Participants pr
          WHERE s.AlbertRatersOnly = 0
            AND pr.Self_PartId = p.PartId
            AND pr.Declined IS NULL
        ) AS pr
        LEFT OUTER JOIN sv_360_Participants pself ON pself.PartId = p.Self_PartId
        WHERE s.IsAlbertSurvey = 1
          AND dc.IntakeCloseDate > DATEADD(HOUR, 12, GETUTCDATE()) -- not too near the close date
          AND ( dc.ScheduledStartDateUtc IS NULL OR dc.ScheduledStartDateUtc < GETUTCDATE() )
          AND p.FirstInvitationSent IS NULL
          AND p.Completed IS NULL
        ORDER BY cmp.SvCompanyId, s.sv_id DESC, dc.Code, ISNULL(p.Self_PartId, p.PartId), p.IsSelf DESC, p.Name;",

        dr => {

          if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
            if (!dr.GetString("Email").EqualsIgnoreCase(runFlags.SendOnlyToEmail)) return;
          }

          int surveyId = dr.GetInt("sv_id");
          int intakeNumber = dr.GetInt("IntakeNumber");
          string jobNumber = dr.GetString("JobNumber");
          int? companyId = dr.GetIntOrNull("SvCompanyId");

          if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
            if (!jobNumber.EqualsIgnoreCase(runFlags.OnlyJobNumber)) return;
          }

          if (runFlags.OnlyCompanyId != null) {
            if (companyId != runFlags.OnlyCompanyId) return;
          }

          var projectInfo = projectInfoCache.GetProjectInfoOrNull(jobNumber);

          var surveyInfo = surveyInfoCache.GetSurveyInfoOrNull(surveyId, intakeNumber);
          if (surveyInfo == null) {
            LogMessage($" - ERROR - Can't find SurveyId {surveyId}", true);
            return;
          }

          // WorkshopEventId may be null.
          DbHelper.WorkshopEvents.WorkshopEventInfo workshopEventInfo = null;
          if (surveyInfo.WorkshopEventId != null) {
            workshopEventInfo = workshopInfoCache.GetWorkshopInfoOrNull(surveyInfo.WorkshopEventId.Value);
          }

          SendSurveyFirstInvite(dr, projectInfo, surveyInfo, workshopEventInfo);

        }
      );
    }

    void SendSurveyFirstInvite(SqlDataReader dr,
      DbHelper.Projects.ProjectInfo projectInfo,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      DbHelper.WorkshopEvents.WorkshopEventInfo workshopEventInfo) {

      bool isSelf = dr.GetBoolFromInt("IsSelf");
      DateTime closeDateRatersLocal = dr.GetDateTime("IntakeCloseDate");
      DateTime closeDateSelfLocal = dr.GetDateTimeOrNull("IntakeCloseDateSelf") ?? closeDateRatersLocal;
      DateTime? firstInvitationSentUtc = dr.GetDateTimeOrNull("FirstInvitationSent");
      DateTime? latestReminderSentUtc = dr.GetDateTimeOrNull("LastInvitationSent");
      bool isRatersOnly = dr.GetBoolFromInt("AlbertRatersOnly"); // i.e. a "pulse" survey, only sent to raters.
      bool noRaters = dr.GetIntOrDefault("sv_FeedbackOption", 0) == (int)DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters;
      int? coacheeId = dr.GetIntOrNull("CoacheeId");
      int? programJobId = dr.GetIntOrNull("ProgramJobId");
      int partId = dr.GetInt("PartId");
      string partUID = dr.GetString("PartUID");
      string partFullName = dr.GetString("Name");
      string partFirstName = dr.GetString("FirstName");
      string partLastName = dr.GetString("LastName");
      string partEmail = dr.GetString("Email");
      string companyName = dr.GetString("CompanyName");
      string selfPartFullName = dr.GetString("SelfName");
      string selfPartFirstName = dr.GetString("SelfFirstName");
      string selfPartLastName = dr.GetString("SelfLastName");
      string selfPartEmail = dr.GetString("SelfEmail");
      string coachFirstName = dr.GetString("CoachFirstName");
      string coachLastName = dr.GetString("CoachLastName");
      string coachEmail = dr.GetString("CoachEmail");

      // This is a good place to check if any participant or rater emails are invalid.
      try {
        _ = new System.Net.Mail.MailAddress(partEmail);
      } catch (Exception) {
        LogMessage($"PartId {partId}: Invalid email address: {partEmail}", true);
        return; // Continue to the next one. TODO: Should we email an alert to someone?
      }

      // Fix names if necessary.
      if (partFirstName.IsNullOrEmpty() && !partFullName.IsNullOrEmpty()) {
        partFirstName = partFullName.Split(' ')[0];
        partLastName = partFullName.Substring(partFirstName.Length).Trim();
      }
      if (selfPartFirstName.IsNullOrEmpty() && !selfPartFullName.IsNullOrEmpty()) {
        selfPartFirstName = selfPartFullName.Split(' ')[0];
        selfPartLastName = selfPartFullName.Substring(selfPartFirstName.Length).Trim();
      }

      if (projectInfo != null) {
        var reminderCadence = isSelf ? projectInfo.SurveyReminderCadence : projectInfo.SurveyReminderCadence_Raters;
        if (reminderCadence != null && !reminderCadence.CanSendInvite(firstInvitationSentUtc, latestReminderSentUtc, isSelf ? closeDateSelfLocal : closeDateRatersLocal)) {
          return; // Cadence rules prevent sending email at this time.
        }
      }

      if (isSelf) {

        if (isRatersOnly) return;

        LogMessage($" - {(!EmailHelper.IsEmailAllowed() ? "(no email) " : "")}SvId {surveyInfo.SurveyId}, Intake {surveyInfo.IntakeNumber}: Sending self invite to {partEmail}");

        string workshopEventName = workshopEventInfo == null ? "" : workshopEventInfo.WorkshopTitle; // If a workshop is related to this survey.

        AlbertEmails.SendSelfInvitationEmail(
          projectInfo, surveyInfo, workshopEventName,
          partId, partUID, coacheeId, programJobId,
          null, null,
          new AlbertEmails.Addressee(partFirstName, partLastName, partEmail),
          new AlbertEmails.Addressee(coachFirstName, coachLastName, coachEmail),
          companyName,
          isReminder: false,
          updateSentDate: runFlags.UpdateSentDates);

      } else { // Rater

        if (noRaters) return;

        LogMessage($" - {(!EmailHelper.IsEmailAllowed() ? "(no email) " : "")}SvId {surveyInfo.SurveyId}, Intake {surveyInfo.IntakeNumber}: Sending rater invite to {partEmail} for self {selfPartEmail}");

        AlbertEmails.SendRaterInvitationEmail(
          projectInfo, surveyInfo, partId, partUID,
          null, null,
          new AlbertEmails.Addressee(partFirstName, partLastName, partEmail),
          new AlbertEmails.Addressee(selfPartFirstName, selfPartLastName, selfPartEmail),
          new AlbertEmails.Addressee(coachFirstName, coachLastName, coachEmail),
          companyName,
          isReminder: false,
          updateLastInvitationSent: runFlags.UpdateSentDates);

      }
    }

    private class UserSurveyReminder {
      public string UserPartEmail;
      public List<DbHelper.AlbertSurveys.SurveyUserReminderInfo> SurveyInfoList;
      public UserSurveyReminder(string userPartEmail, List<DbHelper.AlbertSurveys.SurveyUserReminderInfo> surveyInfoList) {
        UserPartEmail = userPartEmail;
        SurveyInfoList = surveyInfoList;
      }
    }

    void SendSurveyUserReminders() {

      if (!runFlags.CanSendSurveyRemindersToday) {
        LogMessage(" - Not a day to send Survey Reminders. Nothing sent.");
        return;
      }

      var userSurveyToRemind = DbHelper.AlbertSurveys.GetSurveysForUserReminders();

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        userSurveyToRemind.RemoveAll(s => !s.PartEmail.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
      }

      if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
        userSurveyToRemind.RemoveAll(s => !s.ProgramJobNumber.EqualsIgnoreCase(runFlags.OnlyJobNumber));
      }

      if (runFlags.OnlyCompanyId != null) {
        userSurveyToRemind.RemoveAll(s => s.CompanyId != runFlags.OnlyCompanyId);
      }

      if (userSurveyToRemind.IsNullOrEmpty()) {
        LogMessage(" - No reminders pending to send.");
        return;
      }

      var userSurveyReminders = new List<UserSurveyReminder>();
      var distinctUsers = userSurveyToRemind.Select(survey => survey.PartEmail).Distinct().ToList();

      foreach (var userPartEmail in distinctUsers) {
        if (userPartEmail == null || userPartEmail.IsNullOrEmpty()) continue;
        userSurveyReminders.Add(new UserSurveyReminder(userPartEmail, userSurveyToRemind.Where(x => x.PartEmail == userPartEmail).ToList()));
      }

      if (userSurveyReminders == null || userSurveyReminders.Count == 0) {
        LogMessage(" - No reminders pending to send.");
        return;
      }

      int sentEmailsCount = 0;
      List<string> surveysIncludedInEmail;

      var projectInfoCache = new DbHelper.Projects.ProjectInfoCache();

      foreach (var userSurveyReminder in userSurveyReminders) {

        surveysIncludedInEmail = new List<string>();
        int itemsSentOnEmail = 0;

        try {
          itemsSentOnEmail = SendSurveyUserReminder(
            projectInfoCache,
            userSurveyReminder.SurveyInfoList.Distinct().ToList(),
            out surveysIncludedInEmail,
            runFlags.UpdateSentDates);
        } catch (Exception ex) {
          LogMessage("   - ERROR sending reminder.", ex);
        }

        if (itemsSentOnEmail > 0) {

          sentEmailsCount++;

          LogMessage($"   - {(!EmailHelper.IsEmailAllowed() ? "(no email) " : "")}Sent email with {itemsSentOnEmail} item{(itemsSentOnEmail > 1 ? "s" : "")} to user: {userSurveyReminder.UserPartEmail}");
          foreach (var surveyIncluded in surveysIncludedInEmail) {
            LogMessage($"     -  {surveyIncluded}");
          }
        }
      }

      if (sentEmailsCount == 0) {
        LogMessage($"   - No reminders sent.");
      } else {
        LogMessage($"   - Sent reminders to {sentEmailsCount} user{(sentEmailsCount != 1 ? "s" : "")} in total.");
      }
    }

    private int SendSurveyUserReminder(
      DbHelper.Projects.ProjectInfoCache projectInfoCache,
      List<DbHelper.AlbertSurveys.SurveyUserReminderInfo> surveyUserReminders,
      out List<string> surveysIncludedInEmail,
      bool updateSentTimes) {

      surveysIncludedInEmail = new List<string>();

      var selfEmailItems = new List<string>();
      var addMoreRaterEmailItems = new List<string>();
      var raterEmailItems = new List<string>();
      var SurveyPartId_UpdateLastInvitationSent = new List<DbHelper.AlbertSurveys.SurveyUserReminderInfo>();
      string emailBodyHtml = "", emailSubject = "", userEmail = "", userFirstName = "", userLastName = "";

      foreach (var userSurvey in surveyUserReminders) {

        var reminderCadence = userSurvey.IsSelf ? userSurvey.SurveyReminderCadence : userSurvey.SurveyReminderCadence_Raters;
        if (!reminderCadence.CanSendInvite(userSurvey.FirstInvitationSentUtc, userSurvey.LatestReminderSentUtc,
                                           userSurvey.IsSelf ? userSurvey.CloseDateSelfLocal : userSurvey.CloseDateRatersLocal)) {
          continue;
        }

        string itemMainTitle = userSurvey.SurveyName;
        string itemDrescription = $"{userSurvey.CompanyName} - {userSurvey.FriendlyProjectTitle}";
        string surveyUrl = "";
        string closingDateDisplay = "Closing date: ";

        if (userSurvey.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) {
          surveyUrl = PathHelper.Pages.DevelopmentPlanForm(userSurvey.SurveyUID, userSurvey.PartUniqueId, true);
        } else {
          surveyUrl = PathHelper.Pages.ParticipantSurvey(userSurvey.SurveyUID, userSurvey.PartUniqueId, true);
        }

        userFirstName = userSurvey.PartFirstName;
        userLastName = userSurvey.PartLastName;
        userEmail = userSurvey.PartEmail;

        bool noRaters = userSurvey.FeedbackOption == (int)DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters;

        if (userSurvey.IsSelf) { // It's self item

          if (!userSurvey.IsRatersOnly // Ignore self if this is a pulse survey (self does not participate).
              && (userSurvey.CloseDateSelfLocal == null || userSurvey.CloseDateSelfLocal > DateTime.UtcNow)) {

            closingDateDisplay += WebHelper.DisplayDate(userSurvey.CloseDateSelfLocal);

            if (!userSurvey.IsCompleted) {

              // Get email item
              selfEmailItems.Add(AlbertEmails.GetReminderEmailItemHtml(itemMainTitle, itemDrescription, userSurvey.WorkshopTitle.EnsureEndsWith("<br/>", StringExt.Ensure.IfNotBlank) + closingDateDisplay, "Complete now", surveyUrl));
              SurveyPartId_UpdateLastInvitationSent.Add(userSurvey);
              surveysIncludedInEmail.Add(itemMainTitle);

            } else {

              // Survey is completed, check if more raters are required.
              if (userSurvey.RatersInvited < userSurvey.MinimumRatersRequiredForReport) {
                // Send reminder to add more raters.
                addMoreRaterEmailItems.Add(AlbertEmails.GetReminderEmailItemHtml(itemMainTitle, itemDrescription, closingDateDisplay, "Add now", surveyUrl));
                SurveyPartId_UpdateLastInvitationSent.Add(userSurvey);
                surveysIncludedInEmail.Add(itemMainTitle);
              }
            }
          }

        } else if (!noRaters) { // It's rater item

          string raterFor = $"For: {userSurvey.SelfFirstName} {userSurvey.SelfLastName}";

          closingDateDisplay += WebHelper.DisplayDate(userSurvey.CloseDateRatersLocal);
          raterEmailItems.Add(AlbertEmails.GetReminderEmailItemHtml(itemMainTitle, itemDrescription, raterFor, "Give feedback".EnsureEndsWith("<br/>", StringExt.Ensure.IfNotBlank) + closingDateDisplay, surveyUrl));
          SurveyPartId_UpdateLastInvitationSent.Add(userSurvey);
          surveysIncludedInEmail.Add(itemMainTitle);
        }
      }

      // If there are no self or rater items, then there's nothing to send.
      if (selfEmailItems.IsNullOrEmpty() && raterEmailItems.IsNullOrEmpty() && addMoreRaterEmailItems.IsNullOrEmpty()) return 0; // No emails to send.

      var toAddr = new MailAddress(userEmail, $"{userFirstName} {userLastName}");

      emailBodyHtml = AlbertEmails.GetReminderEmailBodyHtml(userFirstName, selfEmailItems, addMoreRaterEmailItems, raterEmailItems);
      emailSubject = $"Reminder: Complete your pending assessment{(selfEmailItems.Count + raterEmailItems.Count + raterEmailItems.Count > 1 ? "s" : "")}";

      // If all items are in the same project, get the project info.
      DbHelper.Projects.ProjectInfo projectInfo = null;
      var distinctJobNumbers = surveyUserReminders.Where(r => !r.ProgramJobNumber.IsNullOrEmpty()).Select(r => r.ProgramJobNumber).Distinct().ToList();
      if (distinctJobNumbers.Count == 1) {
        projectInfo = projectInfoCache.GetProjectInfoOrNull(distinctJobNumbers[0]);
      }

      bool emailSent = AlbertEmails.SendGenericEmail(projectInfo, emailSubject, emailBodyHtml, true, toAddr);

      if (emailSent && updateSentTimes) {

        int? coacheeId = null; // To update email history and make it display on each user's email history if applies.

        // Update last invitation sent date for each of the surveys that were attached to the email.
        foreach (var userSurvey in SurveyPartId_UpdateLastInvitationSent) {

          DbHelper.Participants.UpdateLastInvitationSent(userSurvey.PartId, DateTime.UtcNow);

          // Add to user's email history.
          if ((coacheeId == null && userSurvey.CoacheeId != null) || (coacheeId != userSurvey.CoacheeId && userSurvey.CoacheeId != null)) {
            coacheeId = userSurvey.CoacheeId;
            int programJobId = userSurvey.ProgramJobId != null ? userSurvey.ProgramJobId.Value : 0;
            DbHelper.EmailHistory.AddEmail(null, coacheeId.Value, programJobId, false, "", ConfigHelper.Email_Coordinator_Address, userEmail, emailSubject, null);
          }
        }
      }

      // Return the number of items sent. If email wasn't sent return 0.
      return !emailSent ? 0 : selfEmailItems.Count + raterEmailItems.Count + addMoreRaterEmailItems.Count; ;
    }

    void SetCoacheesToEndProgram() {

      // Set end-program for coachees only if:
      // a) Final workshop done AND no future coaching sessions, or
      // b) Final coaching session AND no future workshops.

      int setCount = 0;

      string sql = $@"

        DECLARE @Today12amWST_UTC DATETIME2(0) = {GetSQL_Today12amWST_UTC()};

        SELECT
          ac.CoacheeId, ac.EmailAddress, ac.SessionsAllocated, ac.SessionsBooked,
          cps.DisplayTitle, cs.LastApptDateUtc
        FROM al_Coachees ac
        INNER JOIN al_CoacheeProgramStatus cps ON ac.ProgramStatusId = cps.ProgramStatusId
        INNER JOIN id_Job j ON j.JobId = ac.ProgramJobId
        OUTER APPLY (
          SELECT MAX(cs.ApptDateUTC) AS LastApptDateUtc
          FROM id_CoachingSession cs
          WHERE cs.AbleCoacheeId = ac.CoacheeId
        ) AS cs
        OUTER APPLY (
          SELECT MAX(we.StartDateUtc) AS LastWorkshopStartUtc
          FROM ev_WorkshopEvent we
          WHERE we.ProgramJobId = ac.ProgramJobId
        ) AS we
        OUTER APPLY (
          SELECT TOP 1 us.SubscriptionId, s.HasAICoaching
          FROM al_UserSubscription us
          INNER JOIN al_Subscription s ON s.SubscriptionId = us.SubscriptionId
          WHERE us.UserId = ac.UserId
            AND us.SubscriptionEndUtc > @Today12amWST_UTC
          ORDER BY us.CreatedUTC DESC
        ) AS sub
        WHERE cps.ProgramStatusId = @ActiveStatusId
          AND
          (
            ( ac.SessionsAllocated = 0
              AND cs.LastApptDateUtc IS NULL
              AND we.LastWorkshopStartUtc IS NULL
              AND j.AbleProgramEndDateUtc < @Today12amWST_UTC
            ) OR (
              ac.SessionsAllocated > 0
              AND ac.SessionsCompleted >= ac.SessionsAllocated
              AND cs.LastApptDateUtc < @Today12amWST_UTC
              AND we.LastWorkshopStartUtc IS NULL
            ) OR (
              ac.SessionsAllocated = 0
              AND cs.LastApptDateUtc IS NULL
              AND we.LastWorkshopStartUtc < @Today12amWST_UTC
            ) OR (
              ac.SessionsAllocated > 0
              AND ac.SessionsCompleted >= ac.SessionsAllocated
              AND cs.LastApptDateUtc < @Today12amWST_UTC
              AND we.LastWorkshopStartUtc < @Today12amWST_UTC
            )
          )
          AND (sub.HasAICoaching <> 1 OR sub.HasAICoaching IS NULL)
          AND ac.DeletedUtc IS NULL
        ORDER BY cs.LastApptDateUtc DESC";

      Query(sql,
        dr => {
          setCount++;
          int coacheeId = dr.GetInt("CoacheeId");
          var coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(coacheeId);
          if (coacheeInfo == null) {
            LogMessage(" - ERROR: Can't find Coachee ID: " + coacheeId, true);
          } else {
            try {
              coacheeInfo.ProgramStatusId = DbHelper.CoacheeProgramStatus.GetStatus_EndProgram().ProgramStatusId;
              DbHelper.AlbertCoachees.UpdateCoachee(coacheeInfo);
              LogMessage(" - Set to End-Program: " + coacheeInfo.GetFullName() + " (" + coacheeInfo.EmailAddress + ")");
            } catch (Exception ex) {
              LogMessage($" - ERROR: Setting Coachee ID {coacheeInfo.CoacheeId} to End-Program.", ex);
            }
          }
        },
        NewSqlParameter("@ActiveStatusId", DbHelper.CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId)
      );

      if (setCount == 0) LogMessage(" - Nothing to do.");
    }

    void SetProgramsToActive() {

      var programs = DbHelper.AblePrograms.GetProgramsToFlagActive();
      if (programs == null || programs.ProgramInfoList == null || programs.ProgramInfoList.Count == 0) {
        LogMessage(" - Nothing to do.");
        return;
      }

      int setCount = 0;
      var statusActive = DbHelper.AlbertProgramStatus.Statuses.Active;

      foreach (var programInfo in programs.ProgramInfoList) {

        setCount++;

        try {
          programInfo.ProgramStatusId = statusActive.ProgramStatusId;
          bool setProgramStatus = DbHelper.AblePrograms.UpdateProgramStatus(programInfo.ProgramJobId, statusActive);

          if (setProgramStatus) {
            int emailsSent = AlbertEmails.SendProgramStartedEmail(programInfo);

            if (emailsSent > 0) {
              DbHelper.AblePrograms.UpdateProgramStartedEmailSent(null, programInfo.ProgramJobId);
            }

            LogMessage($@" - Set to Active: {programInfo.ProgramJobNumber}: {programInfo.ProgramJobName}. {emailsSent} people were informed by email.");
          }
        } catch (Exception ex) {
          LogMessage($" - ERROR: Setting Program ID {programInfo.ProgramJobId} to Active.", ex);
        }
      }
    }

    void SetProgramsToComplete() {

      var programs = DbHelper.AblePrograms.GetProgramsToFlagComplete();
      if (programs == null || programs.ProgramInfoList == null || programs.ProgramInfoList.Count == 0) {
        LogMessage(" - Nothing to do.");
        return;
      }

      int setCount = 0;
      var statusComplete = DbHelper.AlbertProgramStatus.Statuses.Complete;

      foreach (var programInfo in programs.ProgramInfoList) {

        setCount++;

        try {
          programInfo.ProgramStatusId = statusComplete.ProgramStatusId;
          DbHelper.AblePrograms.UpdateProgramComplete(programInfo.ProgramJobId, false);
          LogMessage(" - Set to Complete: " + programInfo.ProgramJobNumber + ": " + programInfo.ProgramJobName);
        } catch (Exception ex) {
          LogMessage($" - ERROR: Setting Program {programInfo.ProgramJobId} to Complete.", ex);
        }
      }
    }

    void SendPostFirstSessionEvals() {

      // Send to all coachees who have completed their first coaching session.

      int setCount = 0;

      var projectInfoCache = new DbHelper.Projects.ProjectInfoCache();

      string sql = @"
        SELECT ac.CoacheeId, ac.EmailAddress, cs.CoachingSessionId,
          DATEDIFF(DAY, cs.ApptDateUTC, GETUTCDATE()) AS DaysPassed,
          ap.BookingReminderCadenceDays, ap.SvCompanyId,
          j.JobNumber,
          ap.CoachingSessionEvalSurveyDisabled, ap.CoachingSessionEvalSurveyTemplateId
        FROM al_Coachees ac
        INNER JOIN id_Job j ON ac.ProgramJobId = j.JobId
        INNER JOIN al_Project ap ON ap.JobNumber = j.JobNumber
        CROSS APPLY (
          SELECT
            ROW_NUMBER() OVER (PARTITION BY cs.AbleCoacheeId ORDER BY cs.ApptDateUTC) AS SessionNumber,
            -- COUNT(*) OVER (PARTITION BY cs.AbleCoacheeId) AS SessionCount,
            -- MAX(cs.ApptDateUTC) OVER (PARTITION BY cs.AbleCoacheeId) AS FinalSessionDateUtc,
            cs.CoachingSessionId, cs.ApptDateUTC,
            cs.EvalCoacheeFirstSentUTC
          FROM id_CoachingSession cs
          WHERE cs.AbleCoacheeId = ac.CoacheeId
        ) AS cs
        WHERE cs.SessionNumber = @EvalSessionNumber       -- only after this session #.
          AND cs.ApptDateUTC < GETUTCDATE()               -- session has taken place.
          AND ac.SessionsAllocated > @MinSessionsForEval  -- if more sessions allocated.
          AND ac.SessionsAllocated > @EvalSessionNumber   -- not the last session.
          AND DATEDIFF(DAY, cs.ApptDateUTC, GETUTCDATE()) <= @CatchUpWindowDays  -- send within this time frame.
          AND NOT EXISTS (                                -- eval survey doesn't already exist.
            SELECT *
            FROM sv_Survey sv
            INNER JOIN sv_360_Participants sp ON sv.sv_id = sp.SurveyId
            WHERE sp.AbleCoacheeId = ac.CoacheeId
              AND sv.ClonedFromSvId = @CoachingEvalSurveyTemplateId
          )
          AND ac.DeletedUtc IS NULL
        ORDER BY ac.CoacheeId, cs.ApptDateUTC";

      Query(sql,
        dr => {

          if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
            if (!runFlags.SendOnlyToEmail.EqualsIgnoreCase(dr.GetString("EmailAddress"))) return;
          }

          if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
            if (!runFlags.OnlyJobNumber.EqualsIgnoreCase(dr.GetString("JobNumber"))) return;
          }

          if (runFlags.OnlyCompanyId != null) {
            if (runFlags.OnlyCompanyId != dr.GetIntOrNull("SvCompanyId")) return;
          }

          setCount++;

          SendPostFirstSessionEval(
            projectInfoCache.GetProjectInfoOrNull(dr.GetString("JobNumber")),
            dr.GetInt("CoacheeId"),
            dr.GetInt("CoachingSessionId"),
            dr.GetInt("DaysPassed"),
            dr.GetString("BookingReminderCadenceDays"),
            dr.GetString("JobNumber"),
            dr.GetBoolFromInt("CoachingSessionEvalSurveyDisabled"),
            dr.GetIntOrNull("CoachingSessionEvalSurveyTemplateId"));
        },
        NewSqlParameter("CoachingEvalSurveyTemplateId", ConfigHelper.TemplateSurveyIds.CoachingSessionEval),
        NewSqlParameter("CatchUpWindowDays", CATCHUP_WINDOW_DAYS),
        NewSqlParameter("EvalSessionNumber", POSTSESSION_EVAL_SESSION_NUMBER),
        NewSqlParameter("MinSessionsForEval", POSTSESSION_EVAL_MIN_SESSIONS)
      );

      if (setCount == 0) LogMessage(" - Nothing to do.");
    }

    void SendPostFirstSessionEval(
      DbHelper.Projects.ProjectInfo projectInfo,
      int coacheeId, int coachingSessionId, int daysPassed, string customCadence, string jobNumber,
      bool coachingSessionEvalSurveyDisabled, int? coachingSessionEvalSurveyTemplateId) {

      string logMessage = "";

      if (coachingSessionEvalSurveyDisabled) {
        LogMessage($" - Not sending Program Eval Survey as it's disabled for this Project's Program {jobNumber}");
        return;
      }

      var coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(coacheeId);
      if (coacheeInfo == null) {
        LogMessage($" - ERROR: Can't find Coachee ID: {coacheeId}", true);
        return;
      }

      logMessage += $" Session Eval for {coacheeInfo.CoacheeId} ({coacheeInfo.EmailAddress}): ";

      var bookingReminderCadenceDays = ConfigHelper.BookingReminderCadenceDays;
      if (!customCadence.IsNullOrEmpty()) {
        try {
          bookingReminderCadenceDays = customCadence.ToIntList();
        } catch (Exception) {
          LogMessage(logMessage + " ERROR: Invalid custom cadence: " + customCadence, true);
          return;
        }
      }

      int maxCadenceDay = bookingReminderCadenceDays[bookingReminderCadenceDays.Count - 1];
      if (daysPassed > maxCadenceDay) {
        LogMessage(logMessage + " Passed max cadence day (" + maxCadenceDay + ").");
        return;
      }

      DbHelper.AlbertSurveys.SurveyInfo evalSurvey = null;
      int evalSurveyId = 0;
      int templateId = coachingSessionEvalSurveyTemplateId ?? ConfigHelper.TemplateSurveyIds.CoachingSessionEval;
      bool emailSent = false;
      Exception tryEx = null;

      if (runFlags.TestMode && !EmailHelper.IsEmailAllowed()) {
        emailSent = true; // Testing without email, so pretend it was sent.
        logMessage += "(no email) ";
      } else {
        try {
          evalSurvey = CreateAndSendSurvey(projectInfo, coacheeInfo, templateId, CATCHUP_WINDOW_DAYS, out emailSent);
        } catch (Exception ex) {
          tryEx = ex;
          logMessage += $" ERROR: Failed sending survey template ID {templateId} to Coachee ID  {coacheeInfo.CoacheeId}";
        }
        if (evalSurvey != null) evalSurveyId = evalSurvey.SurveyId;
      }

      logMessage += (!emailSent ? "NOT " : "") + "Sent, " + PathHelper.AbleUrlKeys.SurveyId + "=" + evalSurveyId;

      if (tryEx == null && emailSent && runFlags.UpdateSentDates) {
        try {
          DbHelper.CoachingSessions.UpdatePostSessionEvalSent(coachingSessionId, DateTime.UtcNow);
        } catch (Exception ex) {
          tryEx = ex;
          logMessage += $" ERROR: UpdatePostSessionEvalSent() for coachingSessionId {coachingSessionId}";
        }
      }

      LogMessage(logMessage, tryEx);
    }

    void SendPostProgramEvals_FinalCoachingSession() {

      // For all coachees who
      //   a) have status "End-Program", and
      //   b) not been sent a post-program eval survey,
      // send them a post-program eval survey.

      int setCount = 0;

      var projectInfoCache = new DbHelper.Projects.ProjectInfoCache();

      Query(@"
        SET @TodayDateWST = CAST(@TodayDateWST AS DATE); -- Ensure date only, no time.
        SELECT
          ac.CoacheeId, ac.EmailAddress, cs.SessionCount,
          ap.BookingReminderCadenceDays, ap.SvCompanyId,
          cs.LastApptDateWST,
          DATEDIFF(DAY, cs.LastApptDateWST, @TodayDateWST) AS DaysPassed,
          j.JobNumber,
          GenericProgramEvalSurveyDisabled, GenericProgramEvalSurveyTemplateId
        FROM al_Coachees ac
        INNER JOIN al_CoacheeProgramStatus aps ON ac.ProgramStatusId = aps.ProgramStatusId
        INNER JOIN id_Job j ON ac.ProgramJobId = j.JobId
        INNER JOIN al_Project ap ON ap.JobNumber = j.JobNumber
        CROSS APPLY (
          SELECT
            MAX(CAST(DATEADD(HOUR, 8, cs.ApptDateUTC) AS DATE)) AS LastApptDateWST,
            SUM(IIF(cs.ApptCancelledUTC IS NULL, 1, 0)) AS SessionCount
          FROM id_CoachingSession cs
          WHERE cs.AbleCoacheeId = ac.CoacheeId
        ) AS cs
        WHERE ac.SessionsCompleted >= ac.SessionsAllocated
          AND ac.SessionsAllocated >= @MinSessions
          AND DATEDIFF(DAY, cs.LastApptDateWST, @TodayDateWST) BETWEEN 0 AND @CatchUpWindowDays
          AND NOT EXISTS (
            SELECT 1
            FROM sv_Survey sv
            INNER JOIN sv_360_Participants sp ON sv.sv_id = sp.SurveyId
            WHERE sp.AbleCoacheeId = ac.CoacheeId
              AND sv.ClonedFromSvId = @PostProgramEvalTemplateId
          )
          AND ac.DeletedUtc IS NULL
        ORDER BY cs.LastApptDateWST DESC",
        dr => {

          if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
            if (!runFlags.SendOnlyToEmail.EqualsIgnoreCase(dr.GetString("EmailAddress"))) return;
          }

          if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
            if (!runFlags.OnlyJobNumber.EqualsIgnoreCase(dr.GetString("JobNumber"))) return;
          }

          if (runFlags.OnlyCompanyId != null) {
            if (runFlags.OnlyCompanyId != dr.GetIntOrNull("SvCompanyId")) return;
          }

          setCount++;

          SendPostProgramEval_FinalCoachingSession(
            projectInfoCache.GetProjectInfoOrNull(dr.GetString("JobNumber")),
            dr.GetInt("CoacheeId"),
            dr.GetInt("DaysPassed"),
            dr.GetString("BookingReminderCadenceDays"),
            dr.GetString("JobNumber"),
            dr.GetBoolFromInt("GenericProgramEvalSurveyDisabled"),
            dr.GetIntOrNull("GenericProgramEvalSurveyTemplateId"));
        },
        NewSqlParameter("EndProgramStatusId", DbHelper.CoacheeProgramStatus.GetStatus_EndProgram().ProgramStatusId),
        NewSqlParameter("PostProgramEvalTemplateId", ConfigHelper.TemplateSurveyIds.PostProgramEval_FinalCoachingSession),
        NewSqlParameter("CatchUpWindowDays", CATCHUP_WINDOW_DAYS),
        NewSqlParameter("MinSessions", POSTPROGRAM_EVAL_MIN_SESSIONS),
        NewSqlParameter("TodayDateWST", TimeHelper.UtcNowToAppDefaultTimeZone().Date) // Note date only, no time.
      );

      if (setCount == 0) LogMessage(" - Nothing to do.");
    }

    void SendPostProgramEval_FinalCoachingSession(
      DbHelper.Projects.ProjectInfo projectInfo,
      int coacheeId, int daysPassed, string customCadence, string jobNumber,
      bool genericProgramEvalSurveyDisabled, int? genericProgramEvalSurveyTemplateId) {

      if (genericProgramEvalSurveyDisabled) {
        LogMessage($" - Not sending Program Eval Survey as it's disabled for this Project's Program {jobNumber}");
        return;
      }

      var coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(coacheeId);
      if (coacheeInfo == null) {
        LogMessage(" - ERROR: Can't find Coachee ID: " + coacheeId, true);
        return;
      }

      if (daysPassed != 0) {
        var bookingReminderCadenceDays = ConfigHelper.BookingReminderCadenceDays;
        if (!customCadence.IsNullOrEmpty()) {
          try {
            bookingReminderCadenceDays = customCadence.ToIntList();
          } catch (Exception) {
            LogMessage(" - ERROR: Invalid custom cadence: " + customCadence, true);
            return;
          }
        }
        if (daysPassed > bookingReminderCadenceDays[bookingReminderCadenceDays.Count - 1]) {
          LogMessage(" - " + coacheeInfo.GetFullName() + "(" + coacheeInfo.EmailAddress + ") - Passed max cadence day.");
          return;
        }
      }

      DbHelper.AlbertSurveys.SurveyInfo evalSurvey = null;
      int evalSurveyId = 0;
      int templateId = genericProgramEvalSurveyTemplateId ?? ConfigHelper.TemplateSurveyIds.PostProgramEval_FinalCoachingSession;
      bool emailSent = false;

      string logMessage = " - " + coacheeInfo.GetFullName() + "(" + coacheeInfo.EmailAddress + ")";
      Exception sendException = null;

      if (!EmailHelper.IsEmailAllowed()) {
        emailSent = true; // Testing without email, so pretend it was sent.
        logMessage += " (no email)";
      } else {
        try {
          evalSurvey = CreateAndSendSurvey(projectInfo, coacheeInfo, templateId, POSTPROGRAM_EVAL_DURATION_DAYS, out emailSent);
          logMessage += " - Sent Post-Program Eval" + PathHelper.AbleUrlKeys.SurveyId + "=" + evalSurveyId;
        } catch (Exception ex) {
          sendException = ex;
          logMessage += " - CreateAndSendEvalSurvey() Error.";
        }
        if (evalSurvey != null) evalSurveyId = evalSurvey.SurveyId;
      }

      LogMessage(logMessage, sendException);
    }

    void SendWorkshopEvals() {

      // Workshop evals are ideally sent on the same morning as the workshop.
      // However a leeway of 3 days from StartDate is allowed in case of hiccups.
      // At the same time, all Coachees' Program Statuses are changed to Active Program on the first workshop date.

      int setCount = 0;

      Query(@"
        SELECT
          j.JobId, j.JobNumber, j.JobName, j.AbleProgramStartDateUtc,
          we.WorkshopEventId, we.WorkshopTitle, we.StartDate, we.EndDate, we.FriendlyWorkshopId,
          IIF(we2.FirstStartDate = we.StartDate, 1, 0) AS IsFirstWorkshop,
          IIF(we2.LastStartDate = we.StartDate, 1, 0) AS IsFinalWorkshop,
          prj.WorkshopSessionEvalSurveyDisabled, prj.WorkshopSessionEvalSurveyTemplateId, prj.SvCompanyId
        FROM ev_WorkshopEvent we
        INNER JOIN id_Job j ON we.ProgramJobId = j.JobId
        INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
        CROSS APPLY (
          SELECT MIN(we2.StartDate) AS FirstStartDate, MAX(we2.StartDate) AS LastStartDate
          FROM ev_WorkshopEvent we2
          WHERE we2.ProgramJobId = we.ProgramJobId
        ) AS we2
        WHERE DATEDIFF(DAY, CAST(we.StartDate AS DATE), @TodayDateAppTZ) BETWEEN 0 AND @CatchUpWindowDays
          AND we.WorkshopStatusId = @WorkshopConfirmedStatusId
          AND NOT EXISTS (SELECT 1 FROM sv_Survey sv WHERE sv.WorkshopEventId = we.WorkshopEventId)
          AND we.DisableEvals = 0
          AND EXISTS (
            SELECT 1 FROM al_Coachees ac
            WHERE ac.ProgramJobId = we.ProgramJobId
              AND ac.DeletedUtc IS NULL
              AND ac.ProgramStatusId <> @CoacheeStatusId_EndProgram
          )
        ORDER BY j.AbleProgramEndDateUtc DESC, j.JobId, we.StartDate DESC, we.WorkshopEventId",

        dr => {

          if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
            if (!runFlags.OnlyJobNumber.EqualsIgnoreCase(dr.GetString("JobNumber"))) return;
          }

          if (runFlags.OnlyCompanyId != null) {
            if (runFlags.OnlyCompanyId != dr.GetIntOrNull("SvCompanyId")) return;
          }

          setCount++;
          WorkshopEvalInfo workshopEvalInfo;

          try {
            workshopEvalInfo = new WorkshopEvalInfo(
              programJobId: dr.GetInt("JobId"),
              jobNumber: dr.GetString("JobNumber"),
              jobName: dr.GetString("JobName"),
              workshopEventId: dr.GetInt("WorkshopEventId"),
              workshopTitle: dr.GetString("WorkshopTitle"),
              startDateLocal: dr.GetDateTime("StartDate"),
              isFirstWorkshop: dr.GetBoolFromInt("IsFirstWorkshop"),
              isFinalWorkshop: dr.GetBoolFromInt("IsFinalWorkshop"),
              friendlyWorkshopId: dr.GetString("FriendlyWorkshopId"),
              workshopSessionEvalSurveyDisabled: dr.GetBoolFromInt("WorkshopSessionEvalSurveyDisabled"),
              workshopSessionEvalSurveyTemplateId: dr.GetIntOrNull("WorkshopSessionEvalSurveyTemplateId")
            );
          } catch (Exception ex) {
            LogMessage(" - ERROR: new WorkshopEvalInfo()", ex);
            return;
          }

          if (workshopEvalInfo.WorkshopSessionEvalSurveyDisabled) {
            LogMessage($" - Not sending Eval Survey as it's disabled for this Project's Program {workshopEvalInfo.JobNumber}");
            return;
          }

          LogMessage(" - Sending eval for " + workshopEvalInfo.FriendlyWorkshopId
            + " (" + workshopEvalInfo.WorkshopEventId + ") \"" + workshopEvalInfo.WorkshopTitle + "\""
            + (workshopEvalInfo.IsFirstWorkshop ? " (First Workshop)" : "")
            + (workshopEvalInfo.IsFinalWorkshop ? " (Final Workshop)" : ""));

          var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(workshopEvalInfo.JobNumber);
          var coachees = DbHelper.AlbertCoachees.GetCoacheesInProgram(workshopEvalInfo.ProgramJobId);

          if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
            coachees.RemoveAll(c => !c.EmailAddress.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
          }

          if (coachees.Count == 0) {
            LogMessage(" - Nothing to send.");
            return;
          }

          int surveyTemplateId = workshopEvalInfo.WorkshopSessionEvalSurveyTemplateId.GetValueOrDefault(0);
          string surveyTitle;

          if (workshopEvalInfo.IsFinalWorkshop) {
            surveyTemplateId = projectInfo.WorkshopAndProgramEvalSurveyTemplateId.GetValueOrDefault(0);
            if (surveyTemplateId == 0) surveyTemplateId = ConfigHelper.TemplateSurveyIds.WorkshopAndProgramEval;
            surveyTitle = "Program Evaluation for " + workshopEvalInfo.JobName;
          } else {
            if (surveyTemplateId == 0) surveyTemplateId = ConfigHelper.TemplateSurveyIds.WorkshopSessionEval;
            surveyTitle = "Workshop Evaluation for " + workshopEvalInfo.WorkshopTitle;
          }

          if (EmailHelper.IsEmailAllowed()) { // Only create survey if not testing (or testing and emails are allowed).

            var templateSurvey = DbHelper.AlbertSurveys.GetTemplateInfo(surveyTemplateId);
            if (templateSurvey == null) {
              LogMessage($"ERROR: Cannot create survey, template ID {surveyTemplateId} not found.", true);
              return;
            }

            DbHelper.AlbertSurveys.SurveyInfo newSurveyInfo = null;
            List<NewSurveyParticipant> newSurveyParticipants = null;

            bool success = UsingTransaction(trans => {
              return CreateWorkshopEvalSurvey(trans,
                workshopEvalInfo, projectInfo, coachees, templateSurvey.SurveyId, surveyTitle,
                out newSurveyInfo, out newSurveyParticipants);
            });
            if (success) {
              SendWorkshopEvalEmails(projectInfo, workshopEvalInfo, newSurveyInfo, newSurveyParticipants);
            }
          }
        },
        NewSqlParameter("@TodayDateAppTZ", currentTime_appZone.Date),
        NewSqlParameter("@WorkshopConfirmedStatusId", DbHelper.WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId),
        NewSqlParameter("@CoacheeStatusId_EndProgram", DbHelper.CoacheeProgramStatus.Ids.EndProgram),
        NewSqlParameter("@CatchUpWindowDays", CATCHUP_WINDOW_DAYS)
      );
      if (setCount == 0) LogMessage(" - Nothing to do.");
    }

    private void SendWorkshopEvalEmails(
      DbHelper.Projects.ProjectInfo projectInfo,
      WorkshopEvalInfo workshopEvalInfo,
      DbHelper.AlbertSurveys.SurveyInfo newSurveyInfo,
      List<NewSurveyParticipant> newSurveyParticipants) {

      if (newSurveyInfo == null || newSurveyParticipants.IsNullOrEmpty()) return;

      // Send invitations to coachee(s).

      foreach (var newPart in newSurveyParticipants) {

        if (newSurveyInfo != null && newPart != null) {
          try {
            AlbertEmails.SendSelfInvitationEmail(
              projectInfo, newSurveyInfo, workshopEvalInfo.WorkshopTitle,
              newPart.NewPartInfo.NewPartId, newPart.NewPartInfo.NewPartUID,
              newPart.NewPartInfo.CoacheeId, newPart.NewPartInfo.ProgramJobId,
              null, null,
              new AlbertEmails.Addressee(newPart.NewPartInfo), new AlbertEmails.Addressee(newPart.NewPartInfo),
              projectInfo.ClientCompanyName, isReminder: false,
              updateSentDate: runFlags.UpdateSentDates);
          } catch (Exception ex) {
            LogMessage($"   - ERROR sending eval survey invitation to {newPart.NewPartInfo.EmailAddr}", ex);
            continue;
          }
        }

        string logMessage = "   - ";
        if (!EmailHelper.IsEmailAllowed()) logMessage += "(no email) ";
        logMessage += $"Sent eval to: {newPart.NewPartInfo.EmailAddr}";
        if (newPart.NewCoacheeStatus != null) {
          if (!newPart.SetStatusError.IsNullOrEmpty()) {
            logMessage += $" - {newPart.SetStatusError}";
          } else {
            logMessage += $" - set Status to: {newPart.NewCoacheeStatus.IntercomFieldValue}";
          }
        }
        LogMessage(logMessage);

        if (runFlags.UpdateSentDates) {
          try {
            DbHelper.WorkshopEvents.UpdateLastEvalSent(null, workshopEvalInfo.WorkshopEventId, DateTime.UtcNow);
          } catch (Exception ex) {
            LogMessage("   - ERROR: UpdateLastEvalSent()", ex);
          }
        }
      }
    }

    class NewSurveyParticipant {

      public DbHelper.Participants.AddParticipantToSurveyInfo NewPartInfo { get; private set; }
      public DbHelper.CoacheeProgramStatus.ProgramStatusInfo NewCoacheeStatus { get; private set; }
      public string SetStatusError { get; private set; }

      public NewSurveyParticipant(DbHelper.Participants.AddParticipantToSurveyInfo newPartInfo, DbHelper.CoacheeProgramStatus.ProgramStatusInfo newCoacheeStatus, string setStatusError) {
        NewPartInfo = newPartInfo;
        NewCoacheeStatus = newCoacheeStatus; // Remains null if status is not changed.
        SetStatusError = setStatusError; // Null unless an error occurs.
      }
    }

    // Create one survey intake for this workshop and invite all participants.
    // Return true if all operations successful.
    bool CreateWorkshopEvalSurvey(
      SqlTransaction trans,
      WorkshopEvalInfo workshopEvalInfo,
      DbHelper.Projects.ProjectInfo projectInfo,
      List<DbHelper.AlbertCoachees.CoacheesInProgram> coachees,
      int templateSurveyId,
      string surveyTitle,
      out DbHelper.AlbertSurveys.SurveyInfo newSurveyInfo,
      out List<NewSurveyParticipant> newSurveyParticipants) {

      newSurveyInfo = null;
      DbHelper.AnswerTypes.IntakeInfo newIntakeInfo = null;
      newSurveyParticipants = new List<NewSurveyParticipant>();

      try {
        newSurveyInfo = CreateSurveyAndDefaultIntake(
          trans: trans,
          companyId: projectInfo.CompanyId,
          programJobId: workshopEvalInfo.ProgramJobId,
          templateSurveyId: templateSurveyId,
          surveyDurationDays: WORKSHOP_EVAL_DURATION_DAYS,
          newIntakeInfo: out newIntakeInfo,
          replacementSurveyTitle: surveyTitle);
      } catch (Exception ex) {
        LogMessage($" - ERROR: Create survey failed, template ID {templateSurveyId}, program ID {workshopEvalInfo.ProgramJobId}", ex);
        return false;
      }

      if (newSurveyInfo == null || newIntakeInfo == null) {
        LogMessage($" - ERROR: Create survey failed, template ID {templateSurveyId}, program ID {workshopEvalInfo.ProgramJobId}", true);
        return false;
      }

      try {
        DbHelper.AlbertSurveys.UpdateWorkshopEventId(trans, newSurveyInfo, workshopEvalInfo.WorkshopEventId);
      } catch (Exception ex) {
        LogMessage($" - ERROR: UpdateWorkshopEventId({workshopEvalInfo.WorkshopEventId})", ex);
        return false;
      }

      LogMessage($" - Created workshop eval survey id={newSurveyInfo.SurveyId}.");

      foreach (var coacheeId in coachees.Select(c => c.CoacheeId).ToList()) {

        DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo = null;
        try {
          coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(trans, coacheeId);
        } catch (Exception ex) {
          LogMessage("   - ERROR: Can't get Coachee Id " + coacheeId, ex);
          continue;
        }
        if (coacheeInfo == null) {
          LogMessage("   - ERROR: Can't find Coachee Id: " + coacheeId, true);
          continue;
        }

        // Set Coachee status to Active if first workshop.
        DbHelper.CoacheeProgramStatus.ProgramStatusInfo newCoacheeStatus = null;
        string setStatusError = null;

        if (workshopEvalInfo.IsFirstWorkshop) newCoacheeStatus = DbHelper.CoacheeProgramStatus.GetStatus_ActiveProgram();

        if (newCoacheeStatus != null && coacheeInfo.ProgramStatusId != newCoacheeStatus.ProgramStatusId) {
          bool statusUpdated = false;
          if (!EmailHelper.IsEmailAllowed()) {
            statusUpdated = true; // Testing, so pretend we have updated the coachee status.
          } else {
            try {
              statusUpdated = DbHelper.AlbertCoachees.UpdateCoacheeStatus(trans, coacheeInfo, newCoacheeStatus);
              if (statusUpdated) coacheeInfo.ProgramStatusId = newCoacheeStatus.ProgramStatusId;
            } catch (Exception ex) {
              setStatusError = ": " + ex.Message;
            }
          }
          if (!statusUpdated) setStatusError = $"ERROR: Failed to update coachee {coacheeInfo.CoacheeId} with Programstatus '{newCoacheeStatus.IntercomFieldValue}'{setStatusError}";
        }

        string newPartEmailAddr = coacheeInfo.EmailAddress;

        if (newSurveyInfo != null && newIntakeInfo != null) {
          var newPartInfo = AddParticipantToSurvey(trans, newSurveyInfo, newIntakeInfo, coacheeInfo, null);
          if (newPartInfo == null) {
            LogMessage($"   - ERROR: Couldn't add coachee ID {coacheeInfo.CoacheeId} to survey ID {newSurveyInfo.SurveyId}.", true);
            continue;
          } else {
            newSurveyParticipants.Add(new NewSurveyParticipant(newPartInfo, newCoacheeStatus, setStatusError));
          }
        }
      }

      return true;
    }

    class WorkshopEvalInfo {

      public int ProgramJobId { get; private set; }
      public string JobNumber { get; private set; }
      public string JobName { get; private set; }
      public int WorkshopEventId { get; private set; }
      public string WorkshopTitle { get; private set; }
      public DateTime StartDateLocal { get; private set; }
      public bool IsFirstWorkshop { get; private set; }
      public bool IsFinalWorkshop { get; private set; }
      public string FriendlyWorkshopId { get; private set; }
      public bool WorkshopSessionEvalSurveyDisabled { get; private set; }
      public int? WorkshopSessionEvalSurveyTemplateId { get; private set; }

      public WorkshopEvalInfo(int programJobId, string jobNumber, string jobName, int workshopEventId, string workshopTitle,
        DateTime startDateLocal, bool isFirstWorkshop, bool isFinalWorkshop, string friendlyWorkshopId,
        bool workshopSessionEvalSurveyDisabled, int? workshopSessionEvalSurveyTemplateId) {

        ProgramJobId = programJobId;
        JobNumber = jobNumber;
        JobName = jobName;
        WorkshopEventId = workshopEventId;
        WorkshopTitle = workshopTitle;
        StartDateLocal = startDateLocal;
        IsFirstWorkshop = isFirstWorkshop;
        IsFinalWorkshop = isFinalWorkshop;
        FriendlyWorkshopId = friendlyWorkshopId;
        WorkshopSessionEvalSurveyDisabled = workshopSessionEvalSurveyDisabled;
        WorkshopSessionEvalSurveyTemplateId = workshopSessionEvalSurveyTemplateId;
      }
    }

    DbHelper.AlbertSurveys.SurveyInfo CreateAndSendSurvey(
      DbHelper.Projects.ProjectInfo projectInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
      int templateSurveyId,
      int surveyDurationDays,
      out bool emailSent) {

      if (projectInfo == null) throw new ArgumentNullException(nameof(projectInfo));

      emailSent = false;

      // Create the new survey.

      DbHelper.AlbertSurveys.SurveyInfo newSurveyInfo = null;
      DbHelper.Participants.AddParticipantToSurveyInfo newPartInfo = null;

      bool surveyCreated = UsingTransaction(trans => {

        newSurveyInfo = CreateSurveyAndDefaultIntake(
          trans: trans,
          companyId: projectInfo?.CompanyId,
          programJobId: coacheeInfo.ProgramJobId,
          templateSurveyId: templateSurveyId,
          surveyDurationDays: surveyDurationDays,
          newIntakeInfo: out var newIntakeInfo);

        if (newSurveyInfo == null || newIntakeInfo == null) return false;

        newPartInfo = AddParticipantToSurvey(trans, newSurveyInfo, newIntakeInfo, coacheeInfo, null);

        if (newPartInfo == null) return false;

        return true;
      });

      // Send invitations to coachee(s).
      try {
        emailSent = AlbertEmails.SendSelfInvitationEmail(
          projectInfo, newSurveyInfo, "",
          newPartInfo.NewPartId, newPartInfo.NewPartUID,
          coacheeInfo.CoacheeId, coacheeInfo.ProgramJobId,
          null, null,
          new AlbertEmails.Addressee(newPartInfo), new AlbertEmails.Addressee(newPartInfo),
          coacheeInfo.CompanyName, isReminder: false, updateSentDate: runFlags.UpdateSentDates);
      } catch (Exception ex) {
        LogMessage(" - Error sending survey invitation.", ex);
      }

      // Send Intercom event for automated survey creation
      if (surveyCreated && newSurveyInfo != null) {
        try {
          var intercom = ServiceLocator.Instance.GetRequiredService<IIntercomEventService>();
          intercom
            .SurveyCreated()
            .WithUser(ConfigHelper.UserId.Automation, ConfigHelper.UserRole.Unset.ToExternalUserId(Guid.Empty).Value)
            .WithEmail("automated-process@able.co")
            .WithSurvey(newSurveyInfo.SurveyId, newSurveyInfo.InternalTitle ?? "Automated Survey")
            .WithProgram(coacheeInfo.ProgramJobId, coacheeInfo.ProgramJobNumber)
            .WithParticipantCount(1)
            .Send();
        } catch (Exception intercomEx) {
          LogMessage($"   WARNING: Intercom survey creation event failed for survey {newSurveyInfo.SurveyId}: {intercomEx.Message}");
        }
      }

      return newSurveyInfo;
    }

    // Create a self-only survey - single close date. Use only for welcome & evals.
    DbHelper.AlbertSurveys.SurveyInfo CreateSurveyAndDefaultIntake(
      SqlTransaction trans,
      int? companyId,
      int? programJobId,
      int templateSurveyId,
      int surveyDurationDays,
      out DbHelper.AnswerTypes.IntakeInfo newIntakeInfo,
      string replacementSurveyTitle = null) {

      if (trans == null) throw new ArgumentNullException(nameof(trans), "Transaction required.");

      DbHelper.AlbertSurveys.NewSurveyIdInfo newSurveyIdInfo = null;
      DbHelper.AlbertSurveys.SurveyInfo newSurveyInfo = null;
      newIntakeInfo = null;

      DateTime closeDateLocal = TimeHelper.UtcNowToAppDefaultTimeZone().Date.AddDays(surveyDurationDays);

      var templateSurvey = DbHelper.AlbertSurveys.GetTemplateInfo(templateSurveyId);
      if (templateSurvey == null) {
        LogMessage($"ERROR: Cannot create survey, template ID {templateSurveyId} not found.", true);
        return null;
      }

      if (!replacementSurveyTitle.IsNullOrEmpty()) {
        templateSurvey.SetInternalTitle(replacementSurveyTitle);
        templateSurvey.SetSurveyName(replacementSurveyTitle);
        templateSurvey.SetReportTitle(replacementSurveyTitle);
      }

      try {
        newSurveyIdInfo = DbHelper.AlbertSurveys.AddSurveyStub(
          trans: trans,
          createdByUserId: DbHelper.AbleUser.GetAutomationUser().UserId,
          templateSurvey: templateSurvey,
          companyId: companyId,
          programJobId: programJobId,
          isProgramSurvey: false,
          firstIntakeName: "Intake 1",
          closeDateInCoacheeLocalTime_Self: closeDateLocal,
          closeDateInCoacheeLocalTime_Rater: closeDateLocal,
          scheduledStartDateUTC: null);
      } catch (Exception ex) {
        LogMessage("Error creating eval survey.", ex);
        return null;
      }

      try {
        newSurveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(trans, newSurveyIdInfo.SurveyId, newSurveyIdInfo.IntakeCode);
      } catch (Exception ex) {
        LogMessage("Failed to get new survey info.", ex);
        return null;
      }

      // Get intake info.
      try {
        newIntakeInfo = DbHelper.AnswerTypes.GetIntake(trans, newSurveyIdInfo.SurveyId, newSurveyIdInfo.IntakeCode);
      } catch (Exception ex) {
        LogMessage("Failed to get new intake info.", ex);
        return null;
      }

      return newSurveyInfo;
    }

    private DbHelper.Participants.AddParticipantToSurveyInfo AddParticipantToSurvey(
      SqlTransaction trans,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      DbHelper.AnswerTypes.IntakeInfo intakeInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
      DbHelper.ProjectUserAccess.ProjectAccessInfo projectAccessInfo) {

      // Add participant.
      DbHelper.Participants.AddParticipantToSurveyInfo newPartInfo;
      try {
        if (projectAccessInfo != null) {
          newPartInfo = new DbHelper.Participants.AddParticipantToSurveyInfo(surveyInfo, intakeInfo, projectAccessInfo, coacheeInfo?.ProgramJobId);
        } else {
          newPartInfo = new DbHelper.Participants.AddParticipantToSurveyInfo(surveyInfo, intakeInfo, coacheeInfo);
        }
        DbHelper.Participants.AddParticipantToSurvey(trans, newPartInfo);
      } catch (Exception ex) {
        LogMessage(" - ERROR adding participant to survey.", ex);
        return null;
      }
      if (newPartInfo == null) {
        LogMessage(" - ERROR null return adding participant to survey.");
        return null;
      }

      return newPartInfo;
    }

    void SendWelcomeEmails() {

      int sendCount = 0;

      var welcomeCoachees = DbHelper.AlbertCoachees.GetCoacheesToWelcome(WelcomeEmail_LagWindowDays);
      var projectInfoCache = new DbHelper.Projects.ProjectInfoCache();
      var coachInfoCache = new DbHelper.AlbertCoaches.CoachInfoCache();

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        welcomeCoachees.RemoveAll(c => !c.EmailAddress.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
      }

      if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
        welcomeCoachees.RemoveAll(c => !c.ProgramJobNumber.EqualsIgnoreCase(runFlags.OnlyJobNumber));
      }

      if (runFlags.OnlyCompanyId != null) {
        welcomeCoachees.RemoveAll(c => c.CompanyId != runFlags.OnlyCompanyId);
      }

      foreach (var welcomeCoachee in welcomeCoachees) {

        sendCount++;
        string logMsg = " - " + welcomeCoachee.GetFullName() + " - Sending... ";

        var projectInfo = projectInfoCache.GetProjectInfoOrNull(welcomeCoachee.ProgramJobNumber);
        var coachInfo = coachInfoCache.GetCoachInfoOrNull(welcomeCoachee.CoachUserId);

        // First set coachee status to onboarding if it's status is prior to that.
        DbHelper.AlbertCoachees.UpdateStatusWaitingToOnboarding(welcomeCoachee);

        if (!EmailHelper.IsEmailAllowed()) {
          LogMessage(logMsg + "Sent (no email)."); // Email off during testing - prevent creation of intake survey and pretend email was sent.
          continue;
        }

        bool sent = AlbertEmails.ParticipantWelcome.Send(projectInfo, welcomeCoachee, coachInfo, projectInfo, out var sendResult, AlbertEmails.ParticipantWelcome.SetSendDates.Yes); // Also creates intake survey.

        if (sent) {
          logMsg += "Sent.";

          // Send Intercom event for coachee invitation (automated welcome email)
          try {
            var intercom = ServiceLocator.Instance.GetRequiredService<IIntercomEventService>();
            intercom
              .CoacheeInvited()
              .WithUser(ConfigHelper.UserId.Automation, ConfigHelper.UserRole.Unset.ToExternalUserId(Guid.Empty).Value)
              .WithEmail("automated-process@able.co")
              .WithCoacheeEmailAddress(welcomeCoachee.EmailAddress)
              .WithOrganisation(welcomeCoachee.TenantOrgId, welcomeCoachee.OrgName)
              .Send();
          } catch (Exception intercomEx) {
            // Log but don't fail the process
            LogMessage($"   WARNING: Intercom event failed for {welcomeCoachee.EmailAddress}: {intercomEx.Message}");
          }
        } else {
          logMsg += "ERROR: Email Not Sent: " + sendResult.StatusAndReason;
        }

        LogMessage(logMsg);
      }

      if (sendCount == 0) LogMessage(" - Nothing to be done.");
    }

    void SendMeetCoachEmails() {

      int lineCount = 0;

      var meetCoachees = DbHelper.AlbertCoachees.GetCoacheesForMeetCoachEmail();
      var projectInfoCache = new DbHelper.Projects.ProjectInfoCache();
      var coachInfoCache = new DbHelper.AlbertCoaches.CoachInfoCache();

      meetCoachees = meetCoachees.GroupBy(x => x.EmailAddress).Select(y => y.First()).ToList();

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        meetCoachees.RemoveAll(c => !c.EmailAddress.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
      }

      if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
        meetCoachees.RemoveAll(c => !c.ProgramJobNumber.EqualsIgnoreCase(runFlags.OnlyJobNumber));
      }

      if (runFlags.OnlyCompanyId != null) {
        meetCoachees.RemoveAll(c => c.CompanyId != runFlags.OnlyCompanyId);
      }

      foreach (var meetCoachee in meetCoachees) {

        lineCount++;
        string logMessage = " - " + meetCoachee.EmailAddress + " - Sending... ";
        if (!EmailHelper.IsEmailAllowed()) logMessage += "(no email) ";

        var projectInfo = projectInfoCache.GetProjectInfoOrNull(meetCoachee.ProgramJobNumber);
        var coachInfo = coachInfoCache.GetCoachInfoOrNull(meetCoachee.CoachUserId);

        bool emailSent = false;
        Exception ex = null;
        try {
          emailSent = AlbertEmails.SendMeetCoachEmail(null, meetCoachee, projectInfo, coachInfo);
        } catch (Exception e) {
          ex = e;
        }

        if (!emailSent) {
          LogMessage(logMessage + $"ERROR: Meet Coach email not sent to Coachee ID {meetCoachee.CoacheeId}.", ex);
        } else {
          LogMessage(logMessage + "Sent.");
          DbHelper.AlbertCoachees.SetMeetCoachSent(null, meetCoachee.CoacheeId, DateTime.UtcNow);
        }
      }

      if (lineCount == 0) LogMessage(" - Nothing to be done.");
    }

    void SendNudgeEmails() {

      if (currentTime_appZone.DayOfWeek != DayOfWeek.Friday) {
        LogMessage(" - Not a Friday, skipping.");
        return;
      }

      var nudgeStatusList = DbHelper.AlbertCoacheeComms.GetNudgeStatusList(DbHelper.CoacheeProgramStatus.GetStatus_ActiveProgram(), false);

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        nudgeStatusList.RemoveAll(s => !s.Coachee.EmailAddress.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
      }

      if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
        nudgeStatusList.RemoveAll(s => s.Coachee.ProgramJobNumber.EqualsIgnoreCase(runFlags.OnlyJobNumber));
      }

      if (runFlags.OnlyCompanyId != null) {
        nudgeStatusList.RemoveAll(c => c.Coachee.CompanyId != runFlags.OnlyCompanyId);
      }

      if (nudgeStatusList.Count == 0) {
        LogMessage(" - Nothing to be done.");
        return;
      }

      var projectInfoBriefCache = new DbHelper.Projects.ProjectInfoBriefCache();

      foreach (var nudgeStatus in nudgeStatusList) {

        if (nudgeStatus.UserSubscription == null) {
          LogMessage($" - Not sending Nudge Email to {nudgeStatus.Coachee.EmailAddress} as they don't have an active subscription.");
          continue;
        }

        if (!nudgeStatus.UserSubscription.HasNudges) {
          LogMessage($" - Not sending Nudge Email to {nudgeStatus.Coachee.EmailAddress} as their subscription doesn't include nudges.");
          continue;
        }

        if (nudgeStatus.UserSubscription.HasNudges && nudgeStatus.UserSubscription.HasAICoaching) {
          LogMessage($" - Not sending Nudge Email to {nudgeStatus.Coachee.EmailAddress} as they have AI Coaching, sending from script.");
          continue;
        }

        var projectInfoBrief = projectInfoBriefCache.GetProjectInfoBrief(nudgeStatus.Coachee.ProgramJobNumber);

        string logMessage = $" - ";
        if (!EmailHelper.IsEmailAllowed()) logMessage += "(no email) ";
        logMessage += $"Sending NudgeContentId {nudgeStatus.NextNudge.NudgeContentId} to {nudgeStatus.Coachee.EmailAddress}";

        bool emailSent = false;
        try {
          emailSent = AlbertEmails.SendNudgeEmail(projectInfoBrief, nudgeStatus, runFlags.UpdateSentDates);
          logMessage += "..." + (emailSent ? "" : " NOT") + " Sent.";
          LogMessage(logMessage);
        } catch (Exception ex) {
          LogMessage(nudgeStatus.Coachee.EmailAddress + " - Error", ex);
        }
      }
    }

    void SendSessionBookingReminders() {

      if (!runFlags.CanSendSessionBookingRemindersToday) {
        LogMessage(" - No sending on weekend.");
        return;
      }

      var bookingReminders = DbHelper.CoachingSessions.GetBookingRemindersOrderedByDaysPassed();

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        bookingReminders.RemoveAll(b => !b.CoacheeEmailAddress.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
      }

      if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
        bookingReminders.RemoveAll(b => !b.ProjectJobNumber.EqualsIgnoreCase(runFlags.OnlyJobNumber));
      }

      if (runFlags.OnlyCompanyId != null) {
        bookingReminders.RemoveAll(b => b.CompanyId != runFlags.OnlyCompanyId);
      }

      if (bookingReminders.Count == 0) {
        LogMessage(" - Nothing due to send.");
        return;
      }

      foreach (var bookingReminder in bookingReminders) {
        SendSessionBookingReminder(bookingReminder);
      }
      LogMessage(" - Done.");
    }

    void SendSessionBookingReminder(DbHelper.CoachingSessions.SessionBookingReminderInfo bookingReminder) {

      string logMessage = " - " + bookingReminder.CoacheeEmailAddress + ": ";

      if (bookingReminder.SessionsAllocated == 0) {
        logMessage += "ERROR: SessionsAllocated = 0.";
        LogMessage(logMessage);
        return;
      }

      if (bookingReminder.SessionsBooked >= bookingReminder.SessionsAllocated) {
        logMessage += "ERROR: All sessions booked.";
        LogMessage(logMessage);
        return;
      }

      DbHelper.CoacheeProgramStatus.ProgramStatusInfo programStatus;
      try {
        programStatus = DbHelper.CoacheeProgramStatus.GetProgramStatusById(bookingReminder.ProgramStatusId);
      } catch (Exception ex) {
        logMessage += "ERROR: GetProgramStatusById(" + bookingReminder.ProgramStatusId + "): " + ex.Message;
        LogMessage(logMessage, ex);
        return;
      }

      if (programStatus == null) {
        logMessage += "ERROR: programStatus == null";
        LogMessage(logMessage);
        return;
      }

      logMessage += programStatus.IntercomFieldValue;

      DbHelper.AlbertCoachingTypes.CoachingTypeInfo coachingType;
      try {
        coachingType = DbHelper.AlbertCoachingTypes.GetCoachingTypeById(bookingReminder.CoachingTypeId);
      } catch (Exception ex) {
        logMessage += " - ERROR: GetCoachingTypeById(" + bookingReminder.CoachingTypeId + "): " + ex.Message;
        LogMessage(logMessage, ex);
        return;
      }

      if (coachingType == null) {
        logMessage += " - ERROR: coachingType == null";
        LogMessage(logMessage);
        return;
      }

      logMessage += " - " + coachingType.IntercomFieldValue;

      if (bookingReminder.DaysPassed < 1) {
        logMessage += " - ERROR: DaysPassed < 1.";
        LogMessage(logMessage);
        return;
      }

      logMessage +=
        " - Session " + (bookingReminder.SessionsBooked + 1) + " of " + bookingReminder.SessionsAllocated
        + ", DaysPassed: " + bookingReminder.DaysPassed;

      // Note the checks above must all precede the checks below.
      // Note that there are 2 "levels" of reminders:
      // 1. Daily up to the end of the cadence array.
      // 2. Weekly, on Mondays, until passed BookingReminderMaxWeeklyReminderMonth months.

      var bookingReminderCadenceDays = ConfigHelper.BookingReminderCadenceDays;
      if (!bookingReminder.BookingReminderCadenceDays.IsNullOrEmpty()) {
        try {
          bookingReminderCadenceDays = bookingReminder.BookingReminderCadenceDays.ToIntList();
          logMessage += ", custom cadence: " + bookingReminder.BookingReminderCadenceDays;
        } catch (Exception) {
          // Log error but continue using the config value.
          logMessage += " - ERROR: Invalid cadence string in Project: " + bookingReminder.BookingReminderCadenceDays;
        }
      }

      int lastCadenceDay = bookingReminderCadenceDays[bookingReminderCadenceDays.Count - 1];
      if (bookingReminder.DaysPassed > lastCadenceDay) {
        logMessage += " - beyond cadence limit (" + lastCadenceDay + " days).";
        LogMessage(logMessage);
        return;
      }

      if (!bookingReminderCadenceDays.Contains(bookingReminder.DaysPassed)) {
        // Not a cadence day, but if this is Monday, check if cadence occurred over the weekend and allow it today.
        if (currentTime_appZone.DayOfWeek == DayOfWeek.Monday
          && (bookingReminderCadenceDays.Contains(bookingReminder.DaysPassed - 1) || bookingReminderCadenceDays.Contains(bookingReminder.DaysPassed - 2))) {
          logMessage += ", cadence on weekend";
        } else {
          logMessage += " - not a cadence day.";
          LogMessage(logMessage);
          return;
        }
      }

      DbHelper.AlbertCoachingTypes.SessionTypeInfo sessionType;
      try {
        sessionType = coachingType.GetSessionType(bookingReminder.SessionsBooked + 1);
      } catch (Exception ex) {
        logMessage += " - ERROR: GetSessionType(" + (bookingReminder.SessionsBooked + 1) + "): " + ex.Message;
        LogMessage(logMessage, ex);
        return;
      }

      if (sessionType == null) {
        logMessage += " - ERROR: sessionInfo not found.";
        LogMessage(logMessage);
        return;
      }

      if (sessionType.DurationMins < 1) {
        logMessage += " - ERROR: sessionInfo.DurationMins not set.";
        LogMessage(logMessage);
        return;
      }

      logMessage
        += ", " + (sessionType.InPerson ? "In-Person" : "Remote")
        + ", " + sessionType.DurationMins + "mins";

      // Adjust "target date for next booking" so it is always a Monday as follows:
      // 1. If date is a weekday, make it the Monday of that week.
      // 2. If not a weekday, make it the following Monday.
      // 3. If the Monday is under 2 days from today, make it next Monday.
      DateTime targetDateAdjusted_appZone = (DateTime)TimeHelper.UtcToAppDefaultTimeZone(bookingReminder.NextBookingTargetDateUtc); // Measure in reference to app time zone.
      if (targetDateAdjusted_appZone.DayOfWeek == DayOfWeek.Saturday)
        targetDateAdjusted_appZone.AddDays(2);
      else if (targetDateAdjusted_appZone.DayOfWeek == DayOfWeek.Sunday)
        targetDateAdjusted_appZone.AddDays(1);
      else
        targetDateAdjusted_appZone.AddDays(DayOfWeek.Monday - targetDateAdjusted_appZone.DayOfWeek);
      if ((currentTime_appZone.Date - targetDateAdjusted_appZone.Date).TotalDays < 2)
        targetDateAdjusted_appZone.AddDays(7);

      logMessage
        += ", Target: " + targetDateAdjusted_appZone.ToString("ddd d MMM")
        + ", Reminder #" + (bookingReminder.BookingReminderCount + 1);

      var reminderInfo = new AlbertEmails.SessionBookingReminderEmailInfo(
          bookingReminder.ProjectJobNumber,
          bookingReminder.FriendlyProjectTitle,
          bookingReminder.TenantOrgGuid,
          bookingReminder.BrandingOrgGuid,
          bookingReminder.CoacheeId,
          bookingReminder.CoacheeGuid,
          bookingReminder.CoacheeFirstName,
          bookingReminder.CoacheeFirstName + " " + bookingReminder.CoacheeLastName,
          bookingReminder.CoacheeEmailAddress,
          bookingReminder.ProgramJobId,
          targetDateAdjusted_appZone,
          bookingReminder.SessionsAllocated,
          bookingReminder.SessionsBooked,
          bookingReminder.CoachFirstName + " " + bookingReminder.CoachLastName,
          bookingReminder.CoachEmailAddress,
          bookingReminder.EmailSenderName,
          bookingReminder.EmailSenderAddress,
          bookingReminder.BookSessionEmailCustomHTML);

      bool emailSent = false;
      bool setSendDate = !runFlags.TestMode;
      bool addToHistory = !runFlags.TestMode;

      try {
        emailSent = AlbertEmails.SendSessionBookingReminder(reminderInfo, setSendDate, addToHistory);
      } catch (Exception ex) {
        Exception ex2 = ex;
        while (ex2.InnerException != null) ex2 = ex2.InnerException;
        logMessage += " - ERROR: SendSessionBookingReminder(): " + ex2.Message;
        LogMessage(logMessage);
        return;
      }

      if (!EmailHelper.IsEmailAllowed()) logMessage += "(no email) ";
      logMessage += " - " + (emailSent ? "sent" : "not sent") + ".";
      LogMessage(logMessage);
    }

    void SendActivitySummaryEmail(List<MailAddress> recipientsOfActivitySummaryEmail) {

      EmailHelper.SendInternalEmail(
        "Able AutoReminder Activity" + (loggedErrors.IsNullOrEmpty() ? "" : " !!HAS ERRORS!! See inside and in ErrorLog table."),
        "<pre>"
        + GetLoggedErrorsText()
        + sbLog.ToString()
        + "</pre>",
        recipientsOfActivitySummaryEmail.ToArray());
    }

    void SendRegistrationInvites() {

      if (!ConfigHelper.IsDevServer && currentTime_appZone.DayOfWeek != DayOfWeek.Monday) {
        LogMessage(" - Not a Monday, skipping.");
        return;
      }

      var usersToSendReminders = DbHelper.AbleUser.GetUsersForRegistrationInviteEmails();

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        usersToSendReminders.RemoveAll(u => !u.EmailAddress.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
        if (usersToSendReminders.IsNullOrEmpty()) {
          LogMessage(" - Nothing to send.");
          return;
        }
      }

      SendUserInviteEmails(usersToSendReminders);
    }

    void SendClientInvitesForAcceptedQuotes() {

      var clientsToSendInvites = DbHelper.AbleUser.GetClientsToInviteForQuotesAcceptedPreviousDay();

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        clientsToSendInvites.RemoveAll(c => !c.EmailAddress.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
      }

      SendUserInviteEmails(clientsToSendInvites);
    }

    void SendUserInviteEmails(List<DbHelper.AbleUser.AbleUserBasicInfo> usersToInvite) {

      if (usersToInvite.Count == 0) {
        LogMessage(" - Nothing due to send.");
        return;
      }

      var projectInfoCache = new DbHelper.Projects.ProjectInfoCache();

      foreach (var inviteeUserInfo in usersToInvite) {

        Exception sendException = null;
        AlbertEmails.UserInviteResult sendInviteEmail = null;

        string inviteeJobNumber = inviteeUserInfo.LatestCoacheeInfo?.JobNumber;
        bool isSkipped = false;

        if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
          if (!runFlags.OnlyJobNumber.EqualsIgnoreCase(inviteeJobNumber)) continue;
        }

        if (runFlags.OnlyCompanyId != null) {
          if (runFlags.OnlyCompanyId != inviteeUserInfo.LatestCoacheeInfo?.CompanyId) continue;
        }

        // Only for pax users, skip reminder if their project has DisablePaxRegReminders = true.
        if (!inviteeJobNumber.IsNullOrEmpty() && inviteeUserInfo.IsParticipant && !inviteeUserInfo.IsAbleClient && !inviteeUserInfo.IsAbleCoach) {

          var inviteeProject = projectInfoCache.GetProjectInfoOrNull(inviteeJobNumber);

          if (inviteeProject.DisablePaxRegReminders) isSkipped = true;
        }

        string logMessage = " - Sending to " + inviteeUserInfo.EmailAddress + inviteeJobNumber.SurroundWith(" (", ")");

        if (isSkipped) {
          logMessage += $" - Skipped, DisablePaxRegReminders = true";
          LogMessage(logMessage);
          continue;
        }

        try {
          sendInviteEmail = AlbertEmails.TrySendUserInvite(inviteeUserInfo, null, true, runFlags.UpdateSentDates);
        } catch (Exception ex) {
          sendException = ex;
        }

        if (sendInviteEmail == null || !sendInviteEmail.IsSuccessful) {
          LogMessage($" - {logMessage} - ERROR: Email NOT sent. {sendInviteEmail.Message ?? ""} - {sendException}");
          continue;
        } else {
          if (!EmailHelper.IsEmailAllowed()) logMessage += "(no email) ";
          logMessage += sendInviteEmail.Message;
          LogMessage(logMessage);
        }
      }
    }

    void SendParticipantPulseSurveys() {

      var projectInfoCache = new DbHelper.Projects.ProjectInfoCache();

      var sendToCoachees = DbHelper.AlbertCoachees.GetCoacheesToSendPulseSurvey();

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        sendToCoachees.RemoveAll(c => !c.EmailAddress.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
      }

      if (sendToCoachees.Count == 0) {
        LogMessage(" - Nothing due to send.");
        return;
      }

      LogMessage(" - Intending to send to: " + sendToCoachees.Count.ToString());

      int surveysSent = 0;

      foreach (var sendItem in sendToCoachees) {

        var coachee = DbHelper.AlbertCoachees.GetCoacheeInfo(sendItem.CoacheeId);

        if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
          if (!runFlags.OnlyJobNumber.EqualsIgnoreCase(coachee.ProgramJobNumber)) continue;
        }

        if (runFlags.OnlyCompanyId != null) {
          if (runFlags.OnlyCompanyId != coachee.CompanyId) continue;
        }

        string logMessage = $" - Sending to {sendItem.EmailAddress.HTMLEncode()} - ";
        int surveyId;

        if (!EmailHelper.IsEmailAllowed()) { // Only create survey if we are not testing.

          logMessage += "(no email) ";

        } else {

          var projectInfo = projectInfoCache.GetProjectInfoOrNull(coachee.ProgramJobNumber);

          // If Participant doesn't have a PulseSurveyId, create Survey.
          if (sendItem.PulseSurveyId == null) {

            if (!CreateAndSendPulseSurvey(projectInfo, coachee, out surveyId, out string errorMsg, out Exception exception)) {
              LogMessage(logMessage + errorMsg, exception);
              continue;
            }
            logMessage += "Sent new survey.";

          } else {

            if (!CreateAndSendPulseIntake(sendItem.PulseSurveyId.Value, sendItem.PulseSurveyUID, projectInfo, coachee, out string errorMsg, out Exception exception)) {
              // Send email alert for this, as it shouldn't happen - something odd is going on.
              LogMessageWithAlertEmail(logMessage + errorMsg, "CreateAndSendPulseIntake failed.", exception);
              continue;
            }

            logMessage += "Sent new intake.";
          }

        }

        surveysSent++;
        LogMessage(logMessage);
      }

      LogMessage(" - Pulse surveys sent: " + surveysSent.ToString());
    }

    bool CreateAndSendPulseSurvey(
      DbHelper.Projects.ProjectInfo projectInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
      out int surveyId, out string errorMsg, out Exception exception) {

      DbHelper.AlbertSurveys.SurveyInfo surveyInfo = null;
      surveyId = 0;
      errorMsg = "";
      exception = null;

      int PulseSurveyTemplateId = coacheeInfo.PulseSurveyTemplateId ?? ConfigHelper.TemplateSurveyIds.Pulse360;

      try {
        surveyInfo = CreateAndSendSurvey(projectInfo, coacheeInfo, PulseSurveyTemplateId, ConfigHelper.PulseSurveyGapDays, out bool emailSent);
      } catch (Exception ex) {
        errorMsg = "ERROR: Can't create pulse survey: " + ex.Message;
        exception = ex;
        return false;
      }

      if (surveyInfo == null) {
        errorMsg = "ERROR: Can't create pulse survey.";
        return false;
      }

      surveyId = surveyInfo.SurveyId;
      return true;
    }

    // If Participant has a PulseSurveyId, create a new intake.
    bool CreateAndSendPulseIntake(int existingPulseSurveyId, string existingPulseSurveyUID,
      DbHelper.Projects.ProjectInfo projectInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee,
      out string errorMsg, out Exception exception) {

      DbHelper.AnswerTypes.IntakeInfo newIntakeInfo = null;
      DbHelper.Participants.AddParticipantToSurveyInfo newPartInfo = null;

      errorMsg = "";
      exception = null;

      var closingTime = DateTime.UtcNow.AddDays(ConfigHelper.PulseSurveyGapDays);

      try {

        UsingTransaction(trans => {

          // Create New Intake
          newIntakeInfo = DbHelper.AlbertSurveys.AddIntake(trans.Connection, trans, existingPulseSurveyId, coachee.ProgramJobId, "Intake for " + DateTime.UtcNow.ToString("d MMM yyyy"), closingTime, closingTime, null);

          // Add participant.
          newPartInfo = new DbHelper.Participants.AddParticipantToSurveyInfo(existingPulseSurveyId, existingPulseSurveyUID, newIntakeInfo, coachee);
          DbHelper.Participants.AddParticipantToSurvey(trans.Connection, trans, newPartInfo);

          return true;
        });

      } catch (Exception ex) {
        errorMsg = "ERROR: Can't create new intake: " + ex.Message;
        exception = ex;
        return false;
      }

      if (newIntakeInfo == null) {
        errorMsg = "ERROR: Can't create new intake.";
        return false;
      }

      var surveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(newPartInfo.SurveyUID, newPartInfo.NewPartUID);

      // Send survey invite for new Pulse Survey intake.
      bool emailSent = false;
      try {
        emailSent = AlbertEmails.SendSelfInvitationEmail(
          projectInfo, surveyInfo, "",
          newPartInfo.NewPartId, newPartInfo.NewPartUID,
          coachee.CoacheeId, coachee.ProgramJobId,
          null, null,
          new AlbertEmails.Addressee(newPartInfo), new AlbertEmails.Addressee(newPartInfo),
          coachee.CompanyName, isReminder: false, updateSentDate: runFlags.UpdateSentDates);
      } catch (Exception ex) {
        errorMsg = "ERROR: Failed to send survey invitation: " + ex.Message;
        exception = ex;
        return false;
      }

      return emailSent;
    }

    class NewEndProjectSurveyParticipant {

      public DbHelper.ProjectUserAccess.ProjectAccessInfo ProjectAccessUser { get; private set; }
      public DbHelper.Participants.AddParticipantToSurveyInfo NewPartInfo { get; private set; }

      public NewEndProjectSurveyParticipant(
        DbHelper.ProjectUserAccess.ProjectAccessInfo projectAccessUser,
        DbHelper.Participants.AddParticipantToSurveyInfo newParticipant) {

        ProjectAccessUser = projectAccessUser;
        NewPartInfo = newParticipant;
      }
    }

    void SendEndOfProjectEmails() {

      var projectsForEndProjectSurvey = DbHelper.Projects.GetProjectsForEndProjectSurvey();

      if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
        projectsForEndProjectSurvey.RemoveAll(p => !p.JobNumber.EqualsIgnoreCase(runFlags.OnlyJobNumber));
      }

      if (runFlags.OnlyCompanyId != null) {
        projectsForEndProjectSurvey.RemoveAll(p => p.CompanyId != runFlags.OnlyCompanyId);
      }

      if (projectsForEndProjectSurvey == null || projectsForEndProjectSurvey.Count == 0) {
        LogMessage(" - Nothing due to send.");
        return;
      }

      LogMessage($" - {projectsForEndProjectSurvey.Count} projects have been closed in the past {ConfigHelper.EndProgramEmailWindowDays} days.");
      LogMessage(string.Empty);

      foreach (var projectInfoBrief in projectsForEndProjectSurvey) {

        // Check if there are any clients to email and go to next project if not.
        var projectAccessUsers = DbHelper.ProjectUserAccess.GetProjectAccessUsers(projectInfoBrief.JobNumber);

        if (projectAccessUsers.IsNullOrEmpty()) {
          LogMessage($"{projectInfoBrief.JobNumber} has no clients to email.");
          continue;
        }

        if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
          projectAccessUsers.RemoveAll(u => !u.Email.EqualsIgnoreCase(runFlags.SendOnlyToEmail));
          if (projectAccessUsers.IsNullOrEmpty()) {
            LogMessage($"{projectInfoBrief.JobNumber} has no Access users with email {runFlags.SendOnlyToEmail}");
            continue;
          }
        }

        LogMessage($"{projectInfoBrief.JobNumber} has {projectAccessUsers.Count} clients in project access.");

        // Keep count of how many emails were sent to log later.
        int emailsSentCount = 0;

        DbHelper.AlbertSurveys.SurveyInfo newSurveyInfo = null;
        DbHelper.AnswerTypes.IntakeInfo newIntakeInfo = null;
        List<NewEndProjectSurveyParticipant> newParts = new List<NewEndProjectSurveyParticipant>();
        bool emailSent = false;

        bool surveyCreated = UsingTransaction(trans => {

          // Create the Survey for this Project.
          try {
            newSurveyInfo = CreateSurveyAndDefaultIntake(
              trans: trans,
              companyId: projectInfoBrief.CompanyId,
              programJobId: null,
              templateSurveyId: ConfigHelper.TemplateSurveyIds.PostProjectEval,
              surveyDurationDays: POSTPROJECT_EVAL_DURATION_DAYS,
              newIntakeInfo: out newIntakeInfo,
              replacementSurveyTitle: "Project Evaluation for " + projectInfoBrief.ProjectName);
          } catch (Exception ex) {
            LogMessage($" - ERROR: Can't create project eval survey for {projectInfoBrief.JobNumber}.", ex);
            return false;
          }
          if (newSurveyInfo == null || newIntakeInfo == null) {
            LogMessage($" - ERROR: Can't create project eval survey for {projectInfoBrief.JobNumber}.", true);
            return false;
          }

          // Send email with survey eval to each client.
          foreach (var prjUserInfo in projectAccessUsers) {

            DbHelper.Participants.AddParticipantToSurveyInfo newPartInfo = null;
            try {
              newPartInfo = AddParticipantToSurvey(trans, newSurveyInfo, newIntakeInfo, null, prjUserInfo);
            } catch (Exception ex) {
              LogMessage($"   - ERROR adding client's user ID {prjUserInfo.UserId} to survey {newSurveyInfo.SurveyId}.", ex);
              return false;
            }
            if (newPartInfo == null) {
              LogMessage($"   - ERROR adding client's user ID {prjUserInfo.UserId} to survey {newSurveyInfo.SurveyId}.", true);
              return false;
            }

            newParts.Add(new NewEndProjectSurveyParticipant(prjUserInfo, newPartInfo));
          }

          if (runFlags.UpdateSentDates) {
            try {
              DbHelper.Projects.UpdateEndProjectSurveySent(trans, projectInfoBrief.ProjectId, newSurveyInfo.SurveyId);
            } catch (Exception ex) {
              LogMessage($" - ERROR: UpdateEndProjectSurveySent({projectInfoBrief.ProjectId}, {newSurveyInfo.SurveyId})", ex);
            }
          }

          return true;
        });

        if (!surveyCreated) continue;

        // Send email with survey eval to each client.
        foreach (var newPart in newParts) {

          // Send email to client.
          try {
            emailSent = AlbertEmails.SendEndOfProjectSurveyEmail(
              newSurveyInfo, newPart.ProjectAccessUser, projectInfoBrief, newPart.NewPartInfo.NewPartUID,
              new AlbertEmails.Addressee(newPart.NewPartInfo));
          } catch (Exception ex) {
            LogMessage($"   - ERROR: sending project {projectInfoBrief.JobNumber} eval invitation to user {newPart.NewPartInfo.UserId}", ex);
            continue;
          }

          if (emailSent && runFlags.UpdateSentDates) {
            try {
              DbHelper.Participants.UpdateFirstInvitationSent(newPart.NewPartInfo.NewPartId, DateTime.UtcNow);
            } catch (Exception) { } // ignore for now
          }

          if (!emailSent) {
            LogMessage($"   - ERROR: sending project {projectInfoBrief.JobNumber} eval invitation to user {newPart.ProjectAccessUser.UserId}", true);
            continue;
          }

          emailsSentCount++;
          LogMessage($"   {(!EmailHelper.IsEmailAllowed() ? "(no email) " : "")}Sent survey to {newPart.ProjectAccessUser.Email}.");
        }

        LogMessage($"   Sent {emailsSentCount} End-Project evals for project {projectInfoBrief.JobNumber}.");
        LogMessage(string.Empty);
      }
    }

    void SendWorkshopEvalCompletedEmails() {

      List<DbHelper.EvalSurveys.WorkshopEvalCompletedNotification> evals = null;

      try {
        evals = DbHelper.EvalSurveys.GetWorkshopEvalCompletedNotifications(DateTime.UtcNow.AddDays(-10), WorkshopEvalsCompletedNotice_MinCompleted);
      } catch (Exception ex) {
        LogMessage($" - ERROR: GetWorkshopEvalCompletedNotifications()", ex);
        return;
      }

      if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
        evals.RemoveAll(e => !e.JobNumber.EqualsIgnoreCase(runFlags.OnlyJobNumber));
      }

      if (runFlags.OnlyCompanyId != null) {
        evals.RemoveAll(e => e.CompanyId != runFlags.OnlyCompanyId);
      }

      if (evals.IsNullOrEmpty()) {
        LogMessage(" - Nothing due to send.");
        return;
      }

      DbHelper.Projects.ProjectInfo project = null;

      foreach (var eval in evals) {

        // Only consult the project info if the object is null or if it's different to previous iteration.
        if (project == null || project.JobNumber != eval.JobNumber) {
          try {
            project = DbHelper.Projects.GetProjectInfoOrNull(eval.JobNumber);
            if (project == null) {
              LogMessage($" - ERROR: Can't get project info for {eval.JobNumber}.", true);
              continue;
            }
          } catch (Exception ex) {
            LogMessage($" - ERROR: Can't get project info for {eval.JobNumber}.", ex);
            continue;
          }
        }

        LogMessage($" - Eval completed for {eval.JobNumber} Job:{eval.ProgramJobId} WsId:{eval.WorkshopEventId}.");
        foreach (var user in eval.NotifyUsers) {
          SendWorkshopEvalCompletedEmail(project, eval, user);
        }
      }
    }

    void SendWorkshopEvalCompletedEmail(
      DbHelper.Projects.ProjectInfo project,
      DbHelper.EvalSurveys.WorkshopEvalCompletedNotification eval,
      DbHelper.EvalSurveys.WorkshopEvalCompletedNotification.NotifyUser user) {

      if (!runFlags.SendOnlyToEmail.IsNullOrEmpty()) {
        if (!user.Email.EqualsIgnoreCase(runFlags.SendOnlyToEmail)) return;
      }

      bool emailSent = false;
      string logMessage = $"Sending notice to: {user.Email}";
      if (user.UserId == eval.LeadConsultantUserId && eval.LeadConsultantUserId == eval.KeyFacilitatorUserId) {
        logMessage += " (PLC & Facilitator)";
      } else if (user.UserId == eval.LeadConsultantUserId) {
        logMessage += " (PLC)";
      } else if (user.UserId == eval.KeyFacilitatorUserId) {
        logMessage += " (Facilitator)";
      }

      string inviteCodeForQuery = user.InviteCode.IsNullOrEmpty() ? "" : $"&{PathHelper.AbleUrlKeys.UserInviteCode}={user.InviteCode.HTMLEncode()}";
      Exception tryEx = null;

      if (!EmailHelper.IsEmailAllowed()) {

        emailSent = true; // Testing without email, so pretend it was sent.
        logMessage += " (no email)";

      } else {

        try {

          emailSent = AlbertEmails.SendGenericEmail(project, "Workshop Evaluation Completed", $@"
            <p>Hi {user.FirstName.HTMLEncode()},</p>
            <p>A Workshop Evaluation has been completed.</p>
            <p>Organisation: <b>{eval.CompanyName.HTMLEncode()}</b></p>
            <p>Program: <b>{eval.JobName.HTMLEncode()}</b></p>
            <p>Workshop: <b>{eval.StartDate.ToString("d MMM yyyy")}: {eval.WorkshopTitle.HTMLEncode()}</b></p>
            {GetWorkshopAvgScoreInfo(eval.LeadConsultantUserId, user.UserId, eval.EvalScoreAvg, eval.WorkshopFacilitatorInfo)}
            <p><a style=""display: block; background-color: #513ED5; color: #FFF; font-size: 14px; border-radius: 5px; text-align: center; padding: 10px 20px; text-decoration: none;"" "
              + $@"href=""{PathHelper.Pages.ProgramOverview(eval.ProgramJobId, true)}{inviteCodeForQuery}"">View Evaluations</a></p>",
            false,
            new MailAddress(user.Email, user.FirstName + " " + user.LastName));

        } catch (Exception ex) {
          tryEx = ex;
          logMessage += " Error: " + ex.Message;
        }
      }

      if (emailSent && runFlags.UpdateSentDates) {
        try {
          DbHelper.EvalSurveys.UpdateWorkshopEvalNotificationSent(eval.WorkshopEventId, DateTime.UtcNow);
        } catch (Exception ex) {
          tryEx = ex;
          logMessage += " UpdatePostSessionEvalSent() Error: " + ex.Message;
        }
      }

      LogMessage("   - " + logMessage, tryEx);
    }

    private string GetWorkshopAvgScoreInfo(int? LeadConsultandUserId, int? currentUserId, decimal? qualityScoreAvg,
      DbHelper.EvalSurveys.WorkshopEvalCompletedNotification.NotifyUser workshopFacilitatorInfo) {
      // Not include this info unless the user is the lead consultant.
      if (LeadConsultandUserId == null || currentUserId == null || LeadConsultandUserId != currentUserId) return "";

      string scoreInfo = "";
      if (qualityScoreAvg == null || qualityScoreAvg == 0) {
        scoreInfo = "no evaluations have been submitted yet.";
      } else if (qualityScoreAvg < ConfigHelper.WorkshopQualityScoreTargetAvg.TargetAverage_Min) {
        scoreInfo = "this is below the target. Please check in with the facilitator to see what can be learned and if any changes need to be made.";
      } else if (qualityScoreAvg >= ConfigHelper.WorkshopQualityScoreTargetAvg.TargetAverage_Min && qualityScoreAvg <= ConfigHelper.WorkshopQualityScoreTargetAvg.TargetAverage_Max) {
        scoreInfo = "this is on target and within the normal range.";
      } else if (qualityScoreAvg > ConfigHelper.WorkshopQualityScoreTargetAvg.TargetAverage_Max) {
        scoreInfo = "this is above target, please congratulate the facilitator.";
      }

      string facilitatorName = workshopFacilitatorInfo != null ? $"{workshopFacilitatorInfo.FirstName} {workshopFacilitatorInfo.LastName}" : null;

      return $@"
        <hr/>
        <p style=""margin-top: 20px; margin-bottom: 5px; "">Facilitator {(facilitatorName)} delivered this workshop.</p>
        <p margin-bottom: 5px; "">The average quality score is <b>{qualityScoreAvg.GetValueOrDefault(0).ToString("0.0")}</b>, {scoreInfo}</p>
        <p style=""margin-bottom: 20px; "">Please check the details of the feedback in able by clicking below.</p>";
    }

    void SetPartnerActiveStatus() {

      var partnersList = DbHelper.AlbertCoaches.GetCoachInfoList(false, DbHelper.AbleUser.RegisteredFilter.Any);

      if (partnersList.Count == 0) {
        LogMessage(" - No partners to update.");
        return;
      }

      LogMessage($" - {partnersList.Count} partners to analyze.");

      int activeCount = 0, inactiveCount = 0;
      foreach (var thisPartner in partnersList) {
        if (!thisPartner.IsAbleCoach) continue;

        bool wasActive = thisPartner.IsPartnerActive;
        DbHelper.AlbertCoaches.UpdateIsPartnerActive(null, thisPartner);
        if (wasActive != thisPartner.IsPartnerActive) {
          if (thisPartner.IsPartnerActive) {
            activeCount++;
          } else {
            inactiveCount++;
          }
        }
      }

      int totalUpdated = activeCount + inactiveCount;
      if (totalUpdated > 0) {
        LogMessage($" - {activeCount} partners were set to Active and {inactiveCount} partners were set to Inactive.");
        LogMessage($" - {totalUpdated} partners status were updated in total.");
      } else {
        LogMessage(" - No partners to update.");
      }
    }

    void CreateOrLinkCoacheeToUserId() {

      var coacheeList = DbHelper.AlbertCoachees.GetCoacheesWithoutUserId();

      if (coacheeList.Count == 0) {
        LogMessage(" - No coachees pending to link.");
        return;
      }

      LogMessage($" - {coacheeList.Count} coachees do not have a UserId linked.");

      int updatedCount = 0;
      foreach (var coacheeInfo in coacheeList) {

        var userId = DbHelper.AbleUser.GetUserByEmailOrNull(coacheeInfo.EmailAddress, DbHelper.AbleUser.RegisteredFilter.Any)?.UserId;

        // If it's not found, create a new user.
        if (userId == null) {
          userId = DbHelper.AbleUser.CreateUserFromCoachee(null, coacheeInfo);
        }

        if (userId != null) {
          bool coacheeUpdated = DbHelper.AlbertCoachees.UpdateCoacheeUserId(coacheeInfo.EmailAddress, userId);
          if (coacheeUpdated) updatedCount++;
        }

      }

      if (updatedCount == 0) {
        LogMessage(" - No coachees were linked.");
      } else {
        LogMessage($" - {updatedCount} coachees were updated.");
      }

    }

    void DeleteOldUserLoginSessions() {

      int deleted = DbHelper.UserLoginSession.DeleteOldSessions(ConfigHelper.AnonymousSessionTimeoutDays, ConfigHelper.LoginSessionTimeoutDays);

      LogMessage($" - {deleted} old login sessions deleted.");
    }

    string GetLoggedErrorsText() {

      if (loggedErrors.IsNullOrEmpty()) return "\nNo errors this run.\n";

      var sb = new StringBuilder();

      sb.AppendLine("\n!!! Errors logged during this run: !!!");
      foreach (var e in loggedErrors) {
        string stackTrace = "";
        if (e.Exception != null) {
          stackTrace = e.Exception.ToString()
            .RegexReplace(@"^ +at +System\.[^\r\n]+[\r\n]+", "", System.Text.RegularExpressions.RegexOptions.Multiline)
            .RegexReplace(@"Integral\.Web.", "")
            .RegexReplace(@"\([^)]+\)", "()")
            .RegexReplace(@":line (\d+)", " [$1]")
            .RegexReplace(@" in [A-Z]:\\[^\r\n]+\\WebApplication\\", " -> ");
        }
        sb.AppendLine($"-------------------------------------------------");
        sb.AppendLine($"{e.Message ?? e.Exception.Message}");
        sb.AppendLine($"{stackTrace}");
      }
      sb.AppendLine($"-------------------------------------------------\n");

      return sb.ToString();
    }

    void SendScheduledContent() {

      var scheduledContent = DbHelper.Content.GetScheduledContentToSend();

      if (!runFlags.OnlyJobNumber.IsNullOrEmpty()) {
        scheduledContent.RemoveAll(c => !c.ProgramJobNumber.EqualsIgnoreCase(runFlags.OnlyJobNumber));
      }

      if (runFlags.OnlyCompanyId != null) {
        scheduledContent.RemoveAll(c => c.CompanyId != runFlags.OnlyCompanyId);
      }

      if (scheduledContent.Count == 0) {
        LogMessage(" - No scheduled content to send.");
        return;
      }

      LogMessage($" - {scheduledContent.Count} participants scheduled content to send.");

      int emailsSentCounter = 0;

      foreach (var content in scheduledContent) {

        try {

          bool emailSent = AlbertEmails.SendScheduledProgramContent(content, runFlags.UpdateSentDates);

          if (emailSent) {
            emailsSentCounter++;
            LogMessage($" - {content.CoacheeEmail}, '{content.ContentInfo.ContentTitle}'.");
          }
        } catch (Exception ex) {
          LogMessage($" - Error sending scheduled content: {ex.Message}");
        }
      }

      if (emailsSentCounter == 0) {
        LogMessage($" - No emails were sent.");
      } else {
        LogMessage($" - {(!EmailHelper.IsEmailAllowed() ? "(no email) " : "")}Emails were sent to {emailsSentCounter} participants.");
      }
    }

    // Calling this "redacting" at the moment, as it does not actually delete rows, just redacts certain columns.
    // Triggered by setting user's DeletionRequestedUtc.
    // Action is performed after that date + ConfigHelper.UserDeletionRequestDelayDays.
    // Data is then redacted and User is set to "Deleted".
    void RedactUserData() {

      var users = DbHelper.AbleUser.GetUsersForDeletion();

      if (users.IsNullOrEmpty()) {
        LogMessage(" - Nothing to do.");
        return;
      }

      foreach (var user in users) {

        LogMessage($" - Redacting User {user.EmailAddress}");

        DbHelper.AbleUser.RedactUserData(user);

      }
    }

    void UpdateProgramSummaries() {

      int rowsUpdated = DbHelper.ProgramSummary.UpdateProgramSummaries(runFlags.PatchJobId.NullIf(0));
      LogMessage($" - {rowsUpdated} Program Summaries Updated.");
    }

  }
}
