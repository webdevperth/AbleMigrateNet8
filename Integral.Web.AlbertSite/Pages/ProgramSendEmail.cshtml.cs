using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ProgramSendEmail : AppCode.PageBaseClasses.ProgramPageBase {

    public const string FirstName_MergedField = "{{FirstName}}";

    // Server location of attachment uploads (temporary just until sent).
    readonly string uploadBasePath = PathHelper.ServerPaths.UploadTemp.EnsureEndsWith("\\", StringExt.Ensure.IfNotBlank);

    public int FormContentCols = 8;
    public int MaxAttachedFiles = 4;
    public string EmailFromAddress, EmailFromName;

    public List<DbHelper.AlbertCoachees.CoacheesForProgramEmail> CoacheeList;
    public List<DbHelper.AblePrograms.ProgramTeamMember> TeamMemberList;
    public int CoacheeCount, TeamMemberCount;
    public int CoacheeEndProgramCount;
    public bool ShowForm, CanSendContentEmail;
    public List<DbHelper.Content.ProgramContentInfo> ContentList;

    public class AjaxAction {
      public const string Test = "test";
      public const string Send = "send";
      public const string Upload = "upload";
      public const string Delete = "delete";
    }

    public class FormFields {
      public const string EmailSubject = "EmailSubject";
      public const string EmailBodyHTML = "EmailBodyHTML";
      public const string FileNameList = "FileNameList";
      public const string FileNameListDelimiter = "|";
      public const string ExcludeEndProgram = "ExcludeEndProgram";
      public const string SendToOption = "SendToOption";
      public const string ParticipantFilter = "ParticipantFilter";
      public const string IncludeBookingLink = "IncludeBookingLink";
      public const string ContentId = "ContentId";
      public const string IncludeContentLink = "IncludeContentLink";
    }

    class FormValues {
      public bool IsTest;
      public string EmailSubject;
      public string EmailBodyHTML;
      public string FileNameList;
      public bool ExcludeEndProgram;
      public SendToEnum SendingTo;
      public List<FileInfo> FilesToAttach;
      public ParticipantFilterEnum ParticipantFilter;
      public bool IncludeBookingLink;
      public bool IncludeContentLink;
      public int? ContentId;
      public DbHelper.Content.ProgramContentInfo ProgramContentInfo;
    }

    public class DataAttrs {
      public const string PaxFilter = "paxfilter";
      public const string SendCount = "send-count";
    }

    public enum SendToEnum { Participants, TeamMembers }
    public enum ParticipantFilterEnum { All, HasCoachingNotBookedAhead, HasCoachingNotBookedAhead30Plus, SurveyNotCompletedSelf }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Send Program Email";
      PageSubtitle = ProgramInfo.ProgramJobNumber + ": " + ProgramInfo.ProgramJobName;

      CoacheeList = DbHelper.AlbertCoachees.GetCoacheesForProgramEmail(ProgramInfo.ProgramJobId);

      CoacheeCount = CoacheeList?.Count ?? 0;
      CoacheeEndProgramCount = CoacheeList.Count(x => x.ProgramStatusId == DbHelper.CoacheeProgramStatus.GetStatus_EndProgram().ProgramStatusId);

      TeamMemberList = DbHelper.AblePrograms.GetProgramTeamMembers(ProgramInfo.ProgramJobId);
      TeamMemberCount = TeamMemberList?.Count ?? 0;

      EmailFromAddress = userInfo.ComputedSenderEmailAddress;
      EmailFromName = userInfo.ComputedSenderEmailName;

      ContentList = DbHelper.Content.GetProgramContentListForEmail(ProgramInfo, SessionHelper.UserInfo);
      CanSendContentEmail = SessionHelper.AppAccess.Content.CanSendContentEmail(ProgramInfo) && !ContentList.IsNullOrEmpty();

      if (SystemWeb.IsHttpPost) {
        DoPost();
        return new EmptyResult();
      }

      ShowForm = CoacheeCount > 0 || TeamMemberCount > 0;

      return Page();
    }

    void DoPost() {

      string contentType = Request.ContentType ?? "";

      if (PageAjaxAction == AjaxAction.Upload && contentType.ToLower().StartsWith("multipart")) {

        UploadFile();

      } else if (PageAjaxAction == AjaxAction.Delete) {

        DeleteFile();

      } else {

        // Form submission.
        AjaxSubmitHelper.Process(ajax => {
          SendEmail(ajax);
        });
      }
    }

    void UploadFile() {

      var files = Request.Form.Files;
      string path = GetUploadPath();
      string output = string.Empty;

      Directory.CreateDirectory(path);

      for (int fn = 0; fn < files.Count; fn++) {
        var file = files[fn];
        using (var fileStream = System.IO.File.Create(path + file.FileName)) {
          using (var inputStream = file.OpenReadStream()) {
            inputStream.CopyTo(fileStream);
          }
        }
        output += file.FileName + "\n"; // FilePond uses this for verification of upload.
      }

      WebHelper.WriteAndEnd(output);
    }

    string GetUploadPath() {
      // User Guid is used as the upload subfolder name.
      return uploadBasePath + SessionHelper.GetUserInfoOrNull().UserGuid.ToStringNoBraces() + "\\";
    }

    void DeleteFile() {

      string fileName;

      using (var reader = new StreamReader(SystemWeb.RequestInputStream)) {
        fileName = reader.ReadToEnd();
        reader.Close();
      }

      string path = GetUploadPath();

      try {
        System.IO.File.Delete(path + fileName);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("DeleteUpload_DeleteFile")
          .FromSession()
          .WithProperty("FileName", fileName)
          .WithProperty("JobNumber", ProjectInfo?.JobNumber)
          .Track();
      } // ignore

      WebHelper.WriteAndEnd("deleted " + path + fileName);
    }

    void SendEmail(AjaxSubmitHelper ajax) {

      // Form validation.
      var formValues = new FormValues();

      formValues.IsTest = ajax.Action != AjaxAction.Send;

      formValues.EmailSubject = ajax.CheckFieldRegex(FormFields.EmailSubject, "Email Subject", AppHelper.Regex.GeneralText.Replace("]", @"}{\|\*]"), true, "Email Subject is required.");
      formValues.EmailBodyHTML = ajax.CheckFieldRegex(FormFields.EmailBodyHTML, "Email Body", AppHelper.Regex.HTML.Replace("]", @"}{\|\*]"), true, "Invalid Characters in Email Body.");
      formValues.FileNameList = ajax.CheckFieldRegex(FormFields.FileNameList, "Email Subject", AppHelper.Regex.FileName.Replace(FormFields.FileNameListDelimiter, ""), false, "Invalid character in file list.");
      formValues.ExcludeEndProgram = ajax.CheckFieldBool(FormFields.ExcludeEndProgram, "1");
      formValues.SendingTo = ajax.CheckFieldEnum<SendToEnum>(FormFields.SendToOption, "Send To");
      formValues.ParticipantFilter = ajax.CheckFieldEnum<ParticipantFilterEnum>(FormFields.ParticipantFilter, "Participant Filter");
      formValues.IncludeBookingLink = ajax.CheckFieldBool(FormFields.IncludeBookingLink, "1");
      formValues.IncludeContentLink = ajax.CheckFieldBool(FormFields.IncludeContentLink, "1");

      if (formValues.IncludeContentLink && formValues.SendingTo == SendToEnum.Participants && CanSendContentEmail) {
        formValues.ContentId = ajax.CheckFieldIDOrNull(FormFields.ContentId, "Microlearning", true, "Please select a Microlearning to attach to the email.");

        DbHelper.Content.ProgramContentInfo programContent = null;

        if (formValues.ContentId != null) {
          programContent = ContentList.FirstOrDefault(x => x.ContentInfo.ContentId == formValues.ContentId);
        }

        if (programContent == null) {
          ajax.AddBadField(FormFields.ContentId, "Select a valid Microlearning to attach to the email.");
          return;
        } else {
          formValues.ProgramContentInfo = programContent;
        }
      }

      if (ajax.BadFieldCount > 0) return;

      // Check all files were uploaded correctly.
      if (!CheckFilesExist(ajax, formValues)) return;

      bool success;
      string errorMessage = "";
      int sentCount = 0;

      if (formValues.IsTest) {

        string emailBodyHtml = GetEmailBodyHtml(formValues, null, false);

        success = SendSingleEmail(
          userInfo.FirstName, userInfo.GetFullName(), userInfo.EmailAddress,
          formValues.EmailSubject, emailBodyHtml, true,
          formValues.FilesToAttach);
      } else if (formValues.SendingTo == SendToEnum.TeamMembers) {
        success = SendEmailToTemMembers(ajax, formValues, out sentCount, out errorMessage);
      } else {
        success = SendEmailToParticipants(ajax, formValues, out sentCount, out errorMessage);
      }

      if (!errorMessage.IsNullOrEmpty()) {
        ajax.AddDialogMessage(errorMessage);
        return;
      }

      if (success) {
        if (formValues.IsTest) {
          ajax.AddSuccessToast("Test email sent.");
        } else if (formValues.SendingTo == SendToEnum.TeamMembers) {
          ajax.AddSuccessDialog($"Email sent to {sentCount} Team {"Member".ToPlural(sentCount)}.");
        } else {
          ajax.AddSuccessDialog($"Email sent to {sentCount} {"Participant".ToPlural(sentCount)}.");
        }
        return;
      }

      // success = false but no errorMessage - shouldn't arrive here. Show placeholder message just in case.
      ajax.AddDialogMessage("Failed to send email to some recipients.");
    }

    bool CheckFilesExist(AjaxSubmitHelper ajax, FormValues formValues) {

      string path = GetUploadPath();
      var fileNameArray = formValues.FileNameList.Split(FormFields.FileNameListDelimiter.ToCharArray());

      if (!formValues.FileNameList.IsNullOrEmpty()) {
        formValues.FilesToAttach = new List<FileInfo>();
        foreach (var fileName in fileNameArray) {
          if (!System.IO.File.Exists(path + fileName)) {
            ajax.AddDialogMessage("Can't find file:<br/>" + fileName + "<br/>Please try removing it and uploading it again.");
            return false;
          }
          formValues.FilesToAttach.Add(new FileInfo(path + fileName));
        }
      }
      return true;
    }

    bool SendEmailToTemMembers(AjaxSubmitHelper ajax, FormValues formValues, out int sentCount, out string errorMessage) {

      errorMessage = "";
      sentCount = 0;

      if (TeamMemberList == null || TeamMemberList.Count == 0) {
        errorMessage = "No Team Members to send to.";
        return false;
      }

      foreach (var teamUser in TeamMemberList) {
        bool sent = false;
        try {
          sent = SendSingleEmail(
            teamUser.FirstName, teamUser.FirstName + " " + teamUser.LastName, teamUser.Email,
            formValues.EmailSubject, formValues.EmailBodyHTML, true,
            formValues.FilesToAttach);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("SendEmailToTeamMembers_SendEmail")
            .FromSession()
            .WithProperty("RecipientEmail", teamUser.Email)
            .WithProperty("JobNumber", ProjectInfo?.JobNumber)
            .Track();
          // ignore exception
        }
        if (sent) sentCount++;
      }

      if (sentCount < TeamMemberList.Count) {
        int failCount = TeamMemberList.Count - sentCount;
        errorMessage = $"Failed to send email to {failCount} team {"member".ToPlural(failCount)}.";
        return false;
      }

      return true;
    }

    private string GetEmailBodyHtml(FormValues formValues, DbHelper.AlbertCoachees.CoacheesForProgramEmail coacheeInfo, bool isCopy) {
      string emailBodyHtml = formValues.EmailBodyHTML;

      if (formValues.SendingTo == SendToEnum.Participants) {
        if (formValues.IncludeBookingLink) {
          if (formValues.IsTest || isCopy) {
            emailBodyHtml += "<br/>" + AlbertEmails.GetEmailLinkButton("", "Book Session"); // Moching item, just so user can see the design on test.
            emailBodyHtml += $"<br/> This button is just a representation of a link that the participants receive{(isCopy ? "d" : "s")}.";

          } else if (coacheeInfo != null && SessionHelper.AppAccess.Participants.CanAttachBookingLinkInEmail(coacheeInfo)) {
            emailBodyHtml += "<br/>" + AlbertEmails.GetEmailLinkButton(PathHelper.Pages.CoacheeSessionBooking(coacheeInfo.CoacheeUID, true), "Book Session");
          }
        } else if (formValues.IncludeContentLink && formValues.ProgramContentInfo != null) {
          emailBodyHtml += "<br/>" + AlbertEmails.GetCustomEmailContentItemHtml(formValues.ProgramContentInfo.ContentInfo);
        }
      }

      return emailBodyHtml;
    }

    bool SendEmailToParticipants(AjaxSubmitHelper ajax, FormValues formValues, out int sentCount, out string errorMessage) {

      errorMessage = "";
      sentCount = 0;

      // Remove EndProgram pax if requested.
      if (CoacheeList != null && formValues.ExcludeEndProgram) {
        CoacheeList.RemoveAll(c => c.ProgramStatusId == DbHelper.CoacheeProgramStatus.Ids.EndProgram);
        CoacheeCount = CoacheeList.Count;
      }

      if (CoacheeList == null || CoacheeList.Count == 0) {
        errorMessage = "No participants to send to.";
        return false;
      }

      // Define the list of coachees based on the filter selected.
      var coacheeListToEmail = new List<DbHelper.AlbertCoachees.CoacheesForProgramEmail>();
      if (formValues.ParticipantFilter == ParticipantFilterEnum.All) {
        coacheeListToEmail = CoacheeList;
      } else if (formValues.ParticipantFilter == ParticipantFilterEnum.HasCoachingNotBookedAhead) {
        coacheeListToEmail = CoacheeList.Where(c => c.HasCoaching && c.SessionsAllocated > c.SessionsBooked).ToList();
      } else if (formValues.ParticipantFilter == ParticipantFilterEnum.HasCoachingNotBookedAhead30Plus) {
        coacheeListToEmail = CoacheeList.Where(c => c.HasCoaching && c.SessionsAllocated > c.SessionsBooked && c.DaysSinceBooking > 30).ToList();
      } else if (formValues.ParticipantFilter == ParticipantFilterEnum.SurveyNotCompletedSelf) {
        coacheeListToEmail = CoacheeList.Where(c => c.HasUncompletedSelfSurvey).ToList();
      }

      var errors = new List<string>();
      foreach (var coachee in coacheeListToEmail) {

        string emailBody = GetEmailBodyHtml(formValues, coachee, false);

        bool emailSent = false;
        try {
          emailSent = SendSingleEmail(
              coachee.FirstName, coachee.GetFullName(), coachee.EmailAddress,
              formValues.EmailSubject, emailBody, true,
              formValues.FilesToAttach);

          if (!emailSent) {
            errors.Add(coachee.EmailAddress + ": Not sent, unknown reason.");
          }
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("SendEmailToParticipants_SendEmail")
            .FromSession()
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coachee.CoacheeUID))
            .WithProperty("RecipientEmail", coachee.EmailAddress)
            .WithProperty("JobNumber", ProjectInfo?.JobNumber)
            .Track();
          errors.Add(coachee.EmailAddress + ": " + ex.Message);
        }

        if (emailSent) {
          sentCount++;
          // Record email sent for this recipient's personal history.
          try {
            DbHelper.EmailHistory.AddEmail(null, coachee.CoacheeId, ProgramInfo.ProgramJobId, true, "", userInfo.GetFullName(), coachee.EmailAddress, formValues.EmailSubject, null);
          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("SendEmailToParticipants_AddEmailHistory")
              .FromSession()
              .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coachee.CoacheeUID))
              .WithProperty("RecipientEmail", coachee.EmailAddress)
              .WithProperty("JobNumber", ProjectInfo?.JobNumber)
              .Track();
            errors.Add(coachee.EmailAddress + ": " + ex.Message);
          }
        }
      }

      if (ConfigHelper.IsLiveServer) {
        SendCopies(formValues);
      }

      if (sentCount < coacheeListToEmail.Count) {
        int failCount = coacheeListToEmail.Count - sentCount;
        errorMessage = $"Failed to send email to {failCount} {"participant".ToPlural(failCount)}.";
        if (ConfigHelper.IsDevServer) errorMessage += "<br/>" + errors.Join("<br/>", s => s.HTMLEncode());
        return false;
      }

      return true;
    }

    // Notify others with a single copy of the email.
    void SendCopies(FormValues formValues) {

      // Add a notice that the merge field will be replaced for each participant (avoids confusion).
      string bodyHTML =
        "<div>"
        + "This is a copy of the email sent to all Program participants.<br/>"
        + FirstName_MergedField.HTMLEncode() + " is replaced with Participant name."
        + "</div>";

      bodyHTML += GetEmailBodyHtml(formValues, null, true);

      // Send a copy to the user.
      SendSingleEmail(
        userInfo.FirstName, userInfo.GetFullName(), userInfo.EmailAddress,
        "(Partner Copy) " + formValues.EmailSubject, bodyHTML,
        false, // Don't replace merge fields.
        formValues.FilesToAttach);

      // Send a copy to program coordinator.
      if (ProgramInfo.ProjectCoordinatorUserId != null) {
        var pcUser = DbHelper.AlbertCoaches.GetCoachInfo((int)ProgramInfo.ProjectCoordinatorUserId);
        SendSingleEmail(
          pcUser.FirstName, pcUser.GetFullName(), pcUser.EmailAddress,
          "(Coordinator Copy) " + formValues.EmailSubject, bodyHTML,
          false, // Don't replace merge fields.
          formValues.FilesToAttach);
      }

      // Send a copy to Lead consultand.
      if (ProgramInfo.LeadConsultantUserId != null) {
        var plcUser = DbHelper.AlbertCoaches.GetCoachInfo((int)ProgramInfo.LeadConsultantUserId);
        SendSingleEmail(
          plcUser.FirstName, plcUser.GetFullName(), plcUser.EmailAddress,
          "(PLC Copy) " + formValues.EmailSubject, bodyHTML,
          false, // Don't replace merge fields.
          formValues.FilesToAttach);
      }
    }

    bool SendSingleEmail(
      string toFirstName, string toFullName, string toEmailAddress,
      string subject, string bodyHTML, bool replaceMergeFields,
      List<FileInfo> filesToAttach) {

      // Replace "{{...}}" merge fields in the submitted subject line and body html.
      if (replaceMergeFields) {
        subject = subject.Replace(FirstName_MergedField, toFirstName);
        bodyHTML = bodyHTML.Replace(FirstName_MergedField, toFirstName);
      }

      return AlbertEmails.SendGenericEmail(
        ProjectInfo,
        subject, "", bodyHTML,
        false,
        EmailFromName, EmailFromAddress,
        filesToAttach, out _,
        new System.Net.Mail.MailAddress(toEmailAddress, toFullName)
      );
    }

    class CoacheeSent {
      public int CoacheeId;
      public string emailAddress;
    }

    public string GetSendingOptions() {
      string html = "";
      if (CoacheeCount > 0) {
        html += $"<option selected value=\"{SendToEnum.Participants}\" data-{DataAttrs.SendCount}=\"{CoacheeCount}\" data-text-singular=\"Participant\" data-text-plural=\"Participants\">Send to Participants</option>";
      }
      if (TeamMemberCount > 0) {
        html += "<option ";
        if (CoacheeCount == 0) html += "selected ";
        html += $" value=\"{SendToEnum.TeamMembers}\" data-{DataAttrs.SendCount}=\"{TeamMemberCount}\" data-text-singular=\"Team Member\" data-text-plural=\"Team Members\">Send to Team Members</option>";
      }
      return html;
    }

    public string GetParticipantFilterOptions() {
      string html = "";
      if (CoacheeCount > 0) {

        int hasCoachingNotBookedAhead = 0, hasCoachingNotBookedAhead30Plus = 0, surveyNotCompletedSelf = 0;
        foreach (var coachee in CoacheeList) {
          if (coachee.HasCoaching && coachee.SessionsAllocated > coachee.SessionsBooked) hasCoachingNotBookedAhead++;
          if (coachee.HasCoaching && coachee.SessionsAllocated > coachee.SessionsBooked && coachee.DaysSinceBooking > 30) hasCoachingNotBookedAhead30Plus++;
          if (coachee.HasUncompletedSelfSurvey) surveyNotCompletedSelf++;
        }

        html += $"<option selected value=\"{ParticipantFilterEnum.All}\" data-{DataAttrs.SendCount}=\"{CoacheeCount}\">All Participants</option>";
        html += $"<option value=\"{ParticipantFilterEnum.HasCoachingNotBookedAhead}\" data-{DataAttrs.SendCount}=\"{hasCoachingNotBookedAhead}\">Has coaching: Not booked ahead</option>";
        html += $"<option value=\"{ParticipantFilterEnum.HasCoachingNotBookedAhead30Plus}\" data-{DataAttrs.SendCount}=\"{hasCoachingNotBookedAhead30Plus}\">Has coaching: Not booked ahead > 30 days</option>";
        html += $"<option value=\"{ParticipantFilterEnum.SurveyNotCompletedSelf}\" data-{DataAttrs.SendCount}=\"{surveyNotCompletedSelf}\">Survey: Not Completed Self</option>";
      }
      return html;
    }
  }
}
