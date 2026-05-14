using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ProjectCustomise : AppCode.PageBaseClasses.ProjectPageBase {

    public class TabName {
      public const string General = "General";
      public const string WelcomeEmail = "WelcomeEmail";
      public const string MeetCoachEmail = "MeetCoachEmail";
      public const string BookNextSession = "BookNextSession";
      public const string EmailCadence = "EmailCadence";
      public const string SendTests = "SendTests";
      public const string Surveys = "Surveys";
    }

    public class AjaxAction {
      public const string SendTests = "SendTests";
      public const string Update = "update";
      public const string ProjectLogo = "ProjectLogo";
    }

    public class FormFields {
      public const string ProjectFriendlyTitle = "ProjectFriendlyTitle";
      public const string ProjectHeaderImage = "ProjectHeaderImage";
      public const string WelcomeEmailHTML = "WelcomeEmailHTML";
      public const string MeetCoachEmailHTML = "MeetCoachEmailHTML";
      public const string BookNextSessionEmailHTML = "BookNextSessionEmailHTML";
      public const string TestCoacheeId = "TestCoacheeId";
      public const string SurveyReminderCadenceId = "SurveyReminderCadenceId";
      public const string SurveyReminderCadenceId_Raters = "SurveyReminderCadenceId_Raters";
      public const string DisablePaxRegReminders = "DisablePaxRegReminders";
      public const string BookingReminderCadenceDays = "BookingReminderCadenceDays";
      public const string BrandingOrgId = "BrandingOrgId";
      public const string IntakeSurveyTemplateId = "IntakeSurveyTemplateId";
      public const string PulseSurveyTemplateId = "PulseSurveyTemplateId";
      public const string CoachingSessionEvalSurveyTemplateId = "CoachingSessionEvalSurveyTemplateId";
      public const string CoachingProgramEvalSurveyTemplateId = "CoachingProgramEvalSurveyTemplateId";
      public const string WorkshopSessionEvalSurveyTemplateId = "WorkshopSessionEvalSurveyTemplateId";
      public const string GenericProgramEvalSurveyTemplateId = "GenericProgramEvalSurveyTemplateId";
      public const string WorkshopAndProgramEvalSurveyTemplateId = "WorkshopAndProgramEvalSurveyTemplateId";
      public const string DevelopmentPlanTemplateId = "DevelopmentPlanTemplateId";
      public const string IntakeSurveyDisable = "IntakeSurveyDisable";
      public const string SendIntakeSurveyToAllParticipants = "SendIntakeSurveyToAllParticipants";
      public const string PulseSurveyDisable = "PulseSurveyDisable";
      public const string CoachingSessionEvalSurveyDisabled = "CoachingSessionEvalSurveyDisabled";
      public const string CoachingProgramEvalSurveyDisabled = "CoachingProgramEvalSurveyDisabled";
      public const string WorkshopSessionEvalSurveyDisabled = "WorkshopSessionEvalSurveyDisabled";
      public const string GenericProgramEvalSurveyDisabled = "GenericProgramEvalSurveyDisabled";
      public const string WorkshopAndProgramEvalSurveyDisabled = "WorkshopAndProgramEvalSurveyDisabled";
      public const string WelcomeEmail_ProgramSummaryDisabled = "WelcomeEmail_ProgramSummaryDisabled";
      public const string OverrideSenderEmailName = "OverrideSenderEmailName";
      public const string OverrideSenderEmailAddress = "OverrideSenderEmailAddress";
      public const string AllowLoggedOutCoachBooking = "AllowLoggedOutCoachBooking";
      public const string NotifySelfWhen180RaterCompleted = "NotifySelfWhen180RaterCompleted";
      public const string CanSelfSelectCoach = "CanSelfSelectCoach";
    }

    public class FormValues {
      public string ProjectFriendlyTitle;
      public string ProjectHeaderImage;
      public string WelcomeEmailHTML;
      public string MeetCoachEmailHTML;
      public string BookNextSessionEmailHTML;
      public Guid? TestCoacheeGuid;
      public int? SurveyReminderCadenceId;
      public int? SurveyReminderCadenceId_Raters;
      public List<int> BookingReminderCadenceDays;
      public bool DisablePaxRegReminders;
      public int? BrandingOrgId;
      public int? IntakeSurveyTemplateId;
      public int? PulseSurveyTemplateId;
      public int? CoachingSessionEvalSurveyTemplateId;
      public int? CoachingProgramEvalSurveyTemplateId;
      public int? WorkshopSessionEvalSurveyTemplateId;
      public int? GenericProgramEvalSurveyTemplateId;
      public int? WorkshopAndProgramEvalSurveyTemplateId;
      public int? DevelopmentPlanTemplateId;
      public bool IntakeSurveyDisable;
      public bool SendIntakeSurveyToAllParticipants;
      public bool PulseSurveyDisable;
      public bool CoachingSessionEvalSurveyDisabled;
      public bool CoachingProgramEvalSurveyDisabled;
      public bool WorkshopSessionEvalSurveyDisabled;
      public bool GenericProgramEvalSurveyDisabled;
      public bool WorkshopAndProgramEvalSurveyDisabled;
      public bool WelcomeEmail_ProgramSummaryDisabled;
      public string OverrideSenderEmailName;
      public string OverrideSenderEmailAddress;
      public bool AllowLoggedOutCoachBooking;
      public bool NotifySelfWhen180RaterCompleted;
      public bool CanSelfSelectCoach;
    }

    public List<DbHelper.AlbertCoachees.AlbertCoacheeInfo> CoacheesInProject;
    public List<DbHelper.TenantOrg.TenantOrgInfo> BrandingOrgList;
    private DbHelper.AlbertSurveys.TemplateList TemplateList;

    public string CoacheesInProjectOptionHTML;
    public bool CanDisablePaxRegistrationReminders;
    public WebHelper.Form.ImageWithUpload CompanyLogoControl;

    protected void Page_Load(object sender, EventArgs e) {

      CanDisablePaxRegistrationReminders = SessionHelper.AppAccess.Projects.CanDisablePaxRegistrationReminders(ProjectInfo);

      CoacheesInProject = DbHelper.AlbertCoachees.GetCoacheesInProject(ProjectInfo.JobNumber);
      BrandingOrgList = DbHelper.TenantOrg.GetQuoteBrandingOrgs(SessionHelper.UserInfo, ProjectInfo.BrandingOrgId);
      TemplateList = DbHelper.AlbertSurveys.GetTemplateList();

      PageTitle = "Project Customisation";

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (ajax.Action == AjaxAction.SendTests) {
            SendTests(ajax);
          } else if (ajax.Action == AjaxAction.Update) {
            SubmitForm(ajax);
          } else if (ajax.Action == AjaxAction.ProjectLogo) {
            SaveProjectLogo(ajax);
          }
        });
        return;
      }

      CoacheesInProjectOptionHTML = CoacheesInProject.Join("", coachee => {
        return "<option"
        + " value=\"" + coachee.CoacheeUID + "\">" + coachee.GetFullName().HTMLEncode()
        + (coachee.CoachUserId == ConfigHelper.UserId.Unassigned ? " (no coach)" : "")
        + "</option>";
      });

      CompanyLogoControl = new WebHelper.Form.ImageWithUpload(
        GetLogoUrl(),
        WebHelper.Form.ImageType.CompanyLogo,
        AjaxAction.ProjectLogo,
        true) {
        MessageUnderButton = "Note: If logo is changed, it won't appear in emails until changes are saved."
      };
    }

    bool TryGetFormValues(AjaxSubmitHelper ajax, out FormValues formValues) {

      formValues = new FormValues();

      formValues.ProjectFriendlyTitle = ajax.CheckFieldRegex(FormFields.ProjectFriendlyTitle, "Project Friendly Title", AppHelper.Regex.GeneralText, false, "Use plain characters for Project Friendly Title.");
      formValues.CanSelfSelectCoach = ajax.CheckFieldBool(FormFields.CanSelfSelectCoach, "1");
      formValues.WelcomeEmailHTML = ajax.CheckFieldRegex(FormFields.WelcomeEmailHTML, "Welcome Email Text", AppHelper.Regex.HTML, false, "");
      formValues.MeetCoachEmailHTML = ajax.CheckFieldRegex(FormFields.MeetCoachEmailHTML, "Meet Coach Email Text", AppHelper.Regex.HTML, false, "");
      formValues.BookNextSessionEmailHTML = ajax.CheckFieldRegex(FormFields.BookNextSessionEmailHTML, "Book Session Email Text", AppHelper.Regex.HTML, false, "");
      formValues.TestCoacheeGuid = ajax.CheckGuid(FormFields.TestCoacheeId, "Test As Participant", false, "Invalid Test Participant");
      formValues.SurveyReminderCadenceId = ajax.CheckFieldIntOrNull(FormFields.SurveyReminderCadenceId);
      formValues.SurveyReminderCadenceId_Raters = ajax.CheckFieldIntOrNull(FormFields.SurveyReminderCadenceId_Raters);
      formValues.BookingReminderCadenceDays = ajax.CheckFieldIntList(FormFields.BookingReminderCadenceDays);
      formValues.DisablePaxRegReminders = ajax.CheckFieldBool(FormFields.DisablePaxRegReminders, "1");
      formValues.BrandingOrgId = ajax.CheckFieldIntOrNull(FormFields.BrandingOrgId);
      formValues.WelcomeEmail_ProgramSummaryDisabled = ajax.CheckFieldBool(FormFields.WelcomeEmail_ProgramSummaryDisabled, "1");
      formValues.OverrideSenderEmailName = ajax.CheckFieldRegex(FormFields.OverrideSenderEmailName, "Override Sender Email Name", AppHelper.Regex.GeneralText, false, "Use plain text characters.");
      formValues.OverrideSenderEmailAddress = ajax.CheckFieldRegex(FormFields.OverrideSenderEmailAddress, "Override Sender Email Address", AppHelper.Regex.Email, false, "Use valid email characters.");
      formValues.AllowLoggedOutCoachBooking = ajax.CheckFieldBool(FormFields.AllowLoggedOutCoachBooking, "1");
      formValues.NotifySelfWhen180RaterCompleted = ajax.CheckFieldBool(FormFields.NotifySelfWhen180RaterCompleted, "1");

      // Survey tab
      formValues.IntakeSurveyDisable = ajax.CheckFieldBool(FormFields.IntakeSurveyDisable, "1");
      formValues.SendIntakeSurveyToAllParticipants = ajax.CheckFieldBool(FormFields.SendIntakeSurveyToAllParticipants, "1");
      formValues.PulseSurveyDisable = ajax.CheckFieldBool(FormFields.PulseSurveyDisable, "1");
      formValues.CoachingSessionEvalSurveyDisabled = ajax.CheckFieldBool(FormFields.CoachingSessionEvalSurveyDisabled, "1");
      formValues.CoachingProgramEvalSurveyDisabled = ajax.CheckFieldBool(FormFields.CoachingProgramEvalSurveyDisabled, "1");
      formValues.WorkshopSessionEvalSurveyDisabled = ajax.CheckFieldBool(FormFields.WorkshopSessionEvalSurveyDisabled, "1");
      formValues.GenericProgramEvalSurveyDisabled = ajax.CheckFieldBool(FormFields.GenericProgramEvalSurveyDisabled, "1");
      formValues.WorkshopAndProgramEvalSurveyDisabled = ajax.CheckFieldBool(FormFields.WorkshopAndProgramEvalSurveyDisabled, "1");

      if (!formValues.IntakeSurveyDisable) formValues.IntakeSurveyTemplateId = ajax.CheckFieldIntOrNull(FormFields.IntakeSurveyTemplateId);
      if (!formValues.PulseSurveyDisable) formValues.PulseSurveyTemplateId = ajax.CheckFieldIntOrNull(FormFields.PulseSurveyTemplateId);
      if (!formValues.CoachingSessionEvalSurveyDisabled) formValues.CoachingSessionEvalSurveyTemplateId = ajax.CheckFieldIntOrNull(FormFields.CoachingSessionEvalSurveyTemplateId);
      if (!formValues.CoachingProgramEvalSurveyDisabled) formValues.CoachingProgramEvalSurveyTemplateId = ajax.CheckFieldIntOrNull(FormFields.CoachingProgramEvalSurveyTemplateId);
      if (!formValues.WorkshopSessionEvalSurveyDisabled) formValues.WorkshopSessionEvalSurveyTemplateId = ajax.CheckFieldIntOrNull(FormFields.WorkshopSessionEvalSurveyTemplateId);
      if (!formValues.GenericProgramEvalSurveyDisabled) formValues.GenericProgramEvalSurveyTemplateId = ajax.CheckFieldIntOrNull(FormFields.GenericProgramEvalSurveyTemplateId);
      if (!formValues.WorkshopAndProgramEvalSurveyDisabled) formValues.WorkshopAndProgramEvalSurveyTemplateId = ajax.CheckFieldIntOrNull(FormFields.WorkshopAndProgramEvalSurveyTemplateId);
      formValues.DevelopmentPlanTemplateId = ajax.CheckFieldIntOrNull(FormFields.DevelopmentPlanTemplateId);

      if (ajax.BadFieldCount > 0) return false;

      if (formValues.BookingReminderCadenceDays != null && formValues.BookingReminderCadenceDays.Count > 0) {

        // Cadence numbers must be > 0 and in ascending order.
        int lastDay = 0;
        foreach (int i in formValues.BookingReminderCadenceDays) {
          if (i < 1) {
            ajax.AddBadField(FormFields.BookingReminderCadenceDays, "Booking reminder cadence day cannot be < 1.");
            return false;
          } else if (i > 50) {
            ajax.AddBadField(FormFields.BookingReminderCadenceDays, "Booking reminder cadence day cannot be > 50.");
            return false;
          } else if (i <= lastDay) {
            ajax.AddBadField(FormFields.BookingReminderCadenceDays, "Booking reminder cadence days must be in ascending order.");
            return false;
          }
          lastDay = i;
        }

        if (formValues.BrandingOrgId != null) { // Validate branding company ONLY if one is selected
          int brandingOrgId = formValues.BrandingOrgId.Value;
          if (BrandingOrgList.Find(c => c.OrgId == brandingOrgId) == null) {
            ajax.AddBadField(FormFields.BrandingOrgId, "Please select a Valid Branding Company");
            return false;
          }
        }

        if (formValues.BookingReminderCadenceDays.ToStringList().Length > 100) {
          ajax.AddDialogMessage("Booking reminder cadence days too long - max 100 characters.");
          return false;
        }
      }
      return true;
    }

    void SendTests(AjaxSubmitHelper ajax) {

      DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo = null;

      FormValues formValues;
      if (!TryGetFormValues(ajax, out formValues)) return;

      if (formValues.TestCoacheeGuid == null) {
        ajax.AddDialogMessage("Test-As Participant is required.");
        return;
      }

      var coacheeInfo = CoacheesInProject.Find(c => c.CoacheeUID == formValues.TestCoacheeGuid);
      if (formValues.TestCoacheeGuid == null) {
        ajax.AddDialogMessage("Invalid Participant for this Project - please try again.");
        return;
      }

      if (!coacheeInfo.CanReceiveWelcomeEmail) {
        ajax.AddDialogMessage("Can't send emails - It's required to have upcoming coaching or workshops or AI coaching or customised in project.");
        return;
      }

      if (coacheeInfo.CoachUserId != ConfigHelper.UserId.Unassigned) {
        coachInfo = DbHelper.AlbertCoaches.GetCoachInfo(coacheeInfo.CoachUserId);
      }

      // Change coachee email address so the current user receives the emails instead of the coachee.
      coacheeInfo.EmailAddress = userInfo.EmailAddress;

      // Send test emails.
      string extraMessages = "";
      int emailsSent = 0;
      try {
        if (AlbertEmails.ParticipantWelcome.Send(ProjectInfo, coacheeInfo, coachInfo, ProjectInfo, out _, AlbertEmails.ParticipantWelcome.SetSendDates.No, true)) {
          emailsSent++;
        }
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("SendTests_WelcomeEmail")
          .FromSession()
          .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo.UserGuid))
          .WithProperty("JobNumber", ProjectInfo.JobNumber)
          .Track();
        extraMessages += "<br/>Could not send Welcome email: " + ex.Message;
      }

      if (coachInfo != null) {
        try {
          if (AlbertEmails.SendMeetCoachEmail(null, coacheeInfo, ProjectInfo, coachInfo, !ConfigHelper.IsLiveServer, false)) {
            emailsSent++;
          }
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("SendTests_MeetCoachEmail")
            .FromSession()
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo.UserGuid))
            .AddExternalUserId(ExternalUserKind.Coach, ConfigHelper.UserRole.Coach.ToExternalUserId(coachInfo?.UserGuid))
            .WithProperty("JobNumber", ProjectInfo.JobNumber)
            .Track();
          extraMessages += "<br/>Could not send Meet Coach email: " + ex.Message;
        }
        try {
          if (AlbertEmails.SendSessionBookingReminder(ProjectInfo, coacheeInfo, null, 5, 1, false, false)) {
            emailsSent++;
          }
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("SendTests_BookingReminderEmail")
            .FromSession()
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo.UserGuid))
            .WithProperty("JobNumber", ProjectInfo.JobNumber)
            .Track();
          extraMessages += "<br/>Could not send Booking Reminder email: " + ex.Message;
        }
      }

      if (emailsSent == 0) {
        ajax.AddDialogMessage("No emails sent, please resolve the following:" + extraMessages);
      } else if (!extraMessages.IsNullOrEmpty()) {
        ajax.AddSuccessDialog("Some emails not sent - please resolve the following:" + extraMessages);
      } else {
        ajax.AddSuccessDialog("All emails sent. Please check your inbox.<br/><br/>Remember to save your changes when you're done editing.");
      }

    }

    void SubmitForm(AjaxSubmitHelper ajax) {

      FormValues formValues;
      if (!TryGetFormValues(ajax, out formValues)) return;

      ProjectInfo.FriendlyProjectTitle = formValues.ProjectFriendlyTitle.ValueIfHTMLEmptyTags(null);
      ProjectInfo.CanSelfSelectCoach = formValues.CanSelfSelectCoach;
      ProjectInfo.WelcomeEmailCustomHTML = formValues.WelcomeEmailHTML.ValueIfHTMLEmptyTags(null);
      ProjectInfo.MeetCoachEmailCustomHTML = formValues.MeetCoachEmailHTML.ValueIfHTMLEmptyTags(null);
      ProjectInfo.BookSessionEmailCustomHTML = formValues.BookNextSessionEmailHTML.ValueIfHTMLEmptyTags(null);

      ProjectInfo.SurveyReminderCadenceId = DbHelper.SurveyReminderCadence.IsValidId(formValues.SurveyReminderCadenceId) ? formValues.SurveyReminderCadenceId : null;
      ProjectInfo.SurveyReminderCadenceId_Raters = DbHelper.SurveyReminderCadence.IsValidId(formValues.SurveyReminderCadenceId_Raters) ? formValues.SurveyReminderCadenceId_Raters : null;
      ProjectInfo.BookingReminderCadenceDays = formValues.BookingReminderCadenceDays == null ? "" : formValues.BookingReminderCadenceDays.ToStringList();
      if (CanDisablePaxRegistrationReminders) {
        ProjectInfo.DisablePaxRegReminders = formValues.DisablePaxRegReminders;
      }

      ProjectInfo.BrandingOrgId = formValues.BrandingOrgId.HasValue ? formValues.BrandingOrgId : null;
      ProjectInfo.IntakeSurveyDisabled = formValues.IntakeSurveyDisable;
      ProjectInfo.SendIntakeSurveyToAllParticipants = formValues.SendIntakeSurveyToAllParticipants;
      ProjectInfo.PulseSurveyDisabled = formValues.PulseSurveyDisable;
      ProjectInfo.CoachingSessionEvalSurveyDisabled = formValues.CoachingSessionEvalSurveyDisabled;
      ProjectInfo.CoachingProgramEvalSurveyDisabled = formValues.CoachingProgramEvalSurveyDisabled;
      ProjectInfo.WorkshopSessionEvalSurveyDisabled = formValues.WorkshopSessionEvalSurveyDisabled;
      ProjectInfo.GenericProgramEvalSurveyDisabled = formValues.GenericProgramEvalSurveyDisabled;
      ProjectInfo.WorkshopAndProgramEvalSurveyDisabled = formValues.WorkshopAndProgramEvalSurveyDisabled;
      ProjectInfo.WelcomeEmail_ProgramSummaryDisabled = formValues.WelcomeEmail_ProgramSummaryDisabled;
      ProjectInfo.OverrideSenderEmailName = formValues.OverrideSenderEmailName;
      ProjectInfo.OverrideSenderEmailAddress = formValues.OverrideSenderEmailAddress;
      ProjectInfo.AllowLoggedOutCoachBooking = formValues.AllowLoggedOutCoachBooking;
      ProjectInfo.NotifySelfWhen180RaterCompleted = formValues.NotifySelfWhen180RaterCompleted;

      // If the survey is disabled, set the new template id value.
      if (!ProjectInfo.IntakeSurveyDisabled) ProjectInfo.IntakeSurveyTemplateId = formValues.IntakeSurveyTemplateId;
      if (!ProjectInfo.PulseSurveyDisabled) ProjectInfo.PulseSurveyTemplateId = formValues.PulseSurveyTemplateId;
      if (!ProjectInfo.CoachingSessionEvalSurveyDisabled) ProjectInfo.CoachingSessionEvalSurveyTemplateId = formValues.CoachingSessionEvalSurveyTemplateId;
      if (!ProjectInfo.CoachingProgramEvalSurveyDisabled) ProjectInfo.CoachingProgramEvalSurveyTemplateId = formValues.CoachingProgramEvalSurveyTemplateId;
      if (!ProjectInfo.WorkshopSessionEvalSurveyDisabled) ProjectInfo.WorkshopSessionEvalSurveyTemplateId = formValues.WorkshopSessionEvalSurveyTemplateId;
      if (!ProjectInfo.GenericProgramEvalSurveyDisabled) ProjectInfo.GenericProgramEvalSurveyTemplateId = formValues.GenericProgramEvalSurveyTemplateId;
      if (!ProjectInfo.WorkshopAndProgramEvalSurveyDisabled) ProjectInfo.WorkshopAndProgramEvalSurveyTemplateId = formValues.WorkshopAndProgramEvalSurveyTemplateId;
      ProjectInfo.DevelopmentPlanTemplateId = formValues.DevelopmentPlanTemplateId;

      bool updated = DbHelper.Projects.UpdateCustomisations(ProjectInfo);

      if (updated) {
        ajax.AddSuccessToast("Project customisations saved.");
      } else {
        ajax.AddDialogMessage("There was an error updating, please try again.");
      }
    }

    void SaveProjectLogo(AjaxSubmitHelper ajax) {
      if (Request.Files == null || Request.Files.Count != 1) return;

      string logoPath = PathHelper.Images.ProjectLogoServerPath(ProjectInfo.JobNumber);

      // Check if the file already exists and delete it
      if (System.IO.File.Exists(logoPath)) {
        System.IO.File.Delete(logoPath);
      }

      using (var fileStream = System.IO.File.Create(logoPath)) {
        Request.Files[0].InputStream.CopyTo(fileStream);
      }
    }

    void MoveCompanyToTop(int orgId) {
      var cmpIndex = BrandingOrgList.FindIndex(cmp => cmp.OrgId == orgId);
      if (cmpIndex >= 0) {
        var cmp = BrandingOrgList[cmpIndex];
        BrandingOrgList.RemoveAt(cmpIndex);
        BrandingOrgList.Insert(0, cmp);
      }
    }

    public string GetBrandingOrgOptions() {

      MoveCompanyToTop(SessionHelper.UserInfo.OrgId); // Move user's company top top of list.

      string html = "<option ";
      if (ProjectInfo.BrandingOrgId == null) html += "selected ";
      html += " data-url=\"" + PathHelper.Images.ProjectLogo(ProjectInfo, true) + "\"";
      html += "value=\"\">[Select Custom Image]</option>";

      foreach (var org in BrandingOrgList) {
        html += "<option";
        if (ProjectInfo.BrandingOrgId != null) {
          if (org.OrgId == ProjectInfo.BrandingOrgId) html += " selected";
        }
        html += " data-url=\"" + PathHelper.Images.TenantOrgLogo(org, true) + "\"";
        html += " value=\"" + org.OrgId + "\">" + org.OrgName.HTMLEncode() + "</option>";
      }
      return html;
    }

    public string GetSurveyTemplateOptions(int? selectedSurveyId, string onlySurveyTypeCode = null) {

      var html = new System.Text.StringBuilder();

      html.Append("<option ");
      if (selectedSurveyId == null) html.Append("selected ");
      html.Append("value=\"\">[Select Survey Template]</option>");

      foreach (var template in TemplateList) {

        if (!onlySurveyTypeCode.IsNullOrEmpty() && template.SurveyTypeCode != onlySurveyTypeCode) continue;

        bool noRaters = template.FeedbackOption == DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters ? true : false;

        html.Append("<option "
          + (template.SurveyId == selectedSurveyId ? "selected " : "")
          + "data-ratersonly=\"" + (template.AlbertRatersOnly ? "1" : "0") + "\" "
          + "data-noraters=\"" + (noRaters ? "1" : "0") + "\" "
          + "value=\"" + template.SurveyId + "\">"
          + (template.IsAlbertSurvey ? "Able: " : "Jarvis: ")
          + SystemWeb.HtmlEncode(template.SurveyName)
          + "</option>");
      }

      return html.ToString();
    }

    public string GetLogoUrl() {
      return PathHelper.Images.ProjectLogo(ProjectInfo, true) + $"?t={DateTime.Now.Ticks}";
    }
  }
}
