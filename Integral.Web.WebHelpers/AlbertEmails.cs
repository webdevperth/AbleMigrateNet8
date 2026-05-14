using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.Data.SqlClient;
using System.Net.Mail;
using Integral.Web.Services;

namespace Integral.Web {

  // Functions for sending site-specific emails, such as
  // participant & rater invitations & reminders.

  public class AlbertEmails {

    public static readonly string EmailHeaderForPartUID = "X-Able-PartUID";
    public static readonly string EmailHeaderForSurveyUID = "X-Able-SurveyUID";
    private static readonly string EmailHeaderAbleFlag = "X-Able-FromAble"; // Indicates the email was sent from the Able app, not the classic (Jarvis) app.

    private static readonly string ReplaceWithIfCompanyNameBlank = "your company";

    private static readonly string MailChimpMergeTagStart = "*|";
    private static readonly string MailChimpMergeTagEnd = "|*";

    private static readonly int ProjectLogoPixelHeight = 70;

    private static readonly string DefaultButtonStyle = "display:inline-block; border-radius:3px; padding: 10px 15px; background:#634CFF; color:#fff; text-decoration:none; font-weight:bold";

    // All merge tags used in Mandrill templates.
    public class MandrillMergeTags {
      public const string ProjectLogoURL = "ProjectLogoURL";
      public const string ProjectLogoPixelHeight = "ProjectLogoHeight";
      public const string FriendlyProjectTitle = "FriendlyProjectTitle";
      public const string ProgramName = "ProgramName";
      public const string CoacheeFirstName = "CoacheeFirstName";
      public const string CoachFirstName = "CoachFirstName";
      public const string CoachFullName = "CoachFullName";
      public const string SessionsAllocated = "SessionsAllocated";
      public const string TotalWorkshops = "TotalWorkshops";
      public const string TitleText = "TitleText";
      public const string BodyHTML = "BodyHTML";
      public const string SurveyName = "SurveyName";
      public const string SurveyLink = "SurveyLink";
      public const string DeclineLink = "DeclineLink";
      public const string WorkshopName = "WorkshopName";
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string FullName = "FullName";
      public const string SelfFirstName = "SelfFirstName";
      public const string SelfLastName = "SelfLastName";
      public const string CompanyName = "CompanyName";
      public const string CompanyName_s = "CompanyName's";
      public const string HasCustomText = "HasCustomText";
      public const string CustomText = "CustomText";
      public const string TargetDate = "TargetDate";
      public const string CalendlyURL = "CalendlyURL";
      public const string CompletedSessions = "CompletedSessions";
      public const string TotalSessions = "TotalSessions";
      public const string CoachName = "CoachName";
      public const string CoachBioShort = "CoachBioShort";
      public const string CoachBioURL = "CoachBioURL";
      public const string CoordinatorFullName = "CoordinatorFullName";
      public const string Session1Duration = "Session1Duration";
      public const string CadenceSummary = "CadenceSummary";
      public const string HasSurvey = "HasSurvey";
      public const string HasSurveyYesNo = "HasSurveyYesNo";
      public const string SUBJECT = "SUBJECT";
      public const string EXTRACSS = "EXTRACSS";
      public const string ProgramType = "ProgramType";
      public const string HasCoaching = "HasCoaching";
      public const string SessionDuration = "SessionDuration";
      public const string HasWorkshops = "HasWorkshops";
      public const string WorkshopsList = "WorkshopsList";
      public const string AblePlatformButton = "AblePlatformButton";
      public const string ScheduledSurveyType = "ScheduledSurveyType";
      public const string ScheduledSurveyTitle = "ScheduledSurveyTitle";
      public const string NudgeTitle = "NudgeTitle";
      public const string NudgeBodyText = "NudgeBodyText";
      public const string SessionsCompleted = "SessionsCompleted";
      public const string ModuleSubject = "ModuleSubject";
      public const string ModuleBodyText = "ModuleBodyText";
      public const string ModuleURL = "ModuleURL";
      public const string CloseDate_ClosingDate = "CloseDate|ClosingDate";
      public const string SelfName_SelfFullName = "SelfName|SelfFullName";
      public const string RaterFirstName = "RaterFirstName";
      public const string RaterLastName = "RaterLastName";
      public const string RaterName_RaterFullName = "RaterName|RaterFullName";
      public const string MeetCoach_HtmlAboveBookButton = "HtmlAboveBookButton";
      public const string MeetCoach_HtmlBelowBookButton = "HtmlBelowBookButton";
    }

    private static readonly string SurveyInvitationMergeField_ExtraCSS = "EXTRACSS";
    private static readonly string SurveyInvitationMergeField_SurveyName = "SurveyName";
    private static readonly string SurveyInvitationMergeField_RecipientFirstName = "RecipientFirstName";
    private static readonly string SurveyInvitationMergeField_SurveyEmailContent = "SurveyInvitationContent";

    private static readonly string FallbackWelcomeEmailTitle = "Leadership Starts Here";

    private static readonly string StagingTemplateSuffix = "-staging";

    public static string GetMergeTag(string tag) {
      return MailChimpMergeTagStart + tag + MailChimpMergeTagEnd;
    }

    private static string GetEmailTopLogoHtml(string logoUrl) => $@"
      <center style=""padding: 0; margin: 0 0 20px"">
        <img src=""{logoUrl}"" style=""height:50px; max-height:50px; width:auto"" />
      </center>";

    // ***
    // *** Generic blank email, provide title and content.
    // ***
    public static bool SendGenericEmail(
      DbHelper.Projects.ProjectInfo projectInfo,
      string subject, string bodyHtml,
      bool allowProjectOverrideSender,
      params MailAddress[] tos
    ) {
      return SendGenericEmail(projectInfo, subject, "", bodyHtml, allowProjectOverrideSender, "", "", null, out _, tos);
    }

    // Main signature which also outputs sendResult with mandrill error details if not sent.
    public static bool SendGenericEmail(
      DbHelper.Projects.ProjectInfoBrief projectInfoBrief,
      string subject, string titleText, string bodyHtml,
      bool allowProjectOverrideSender,
      string preferredSenderEmailName, string preferredSenderEmailAddress,
      List<System.IO.FileInfo> filesToAttach,
      out EmailHelper.MandrillSentResult sendResult,
      params MailAddress[] tos
    ) {

      if (tos == null || tos.Length == 0) throw new ArgumentException("No recipients provided.");

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.ProjectLogo(projectInfoBrief, false, true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.TitleText, titleText },
        { MandrillMergeTags.BodyHTML, bodyHtml }
      };

      EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>(tos), null, null,
        preferredSenderEmailName, preferredSenderEmailAddress,
        projectInfoBrief, allowProjectOverrideSender,
        subject,
        ConfigHelper.MandrillTemplate_Generic,
        mergePairs,
        out sendResult,
        filesToAttach);

      return sendResult.Sent;
    }

    public class UserInviteResult {
      public bool IsSuccessful { get; }
      public string Message { get; }

      public UserInviteResult(bool isSuccessful, string message) {
        IsSuccessful = isSuccessful;
        Message = message;
      }
    }

    public static UserInviteResult TrySendUserInvite(
      DbHelper.AbleUser.AbleUserBasicInfo inviteeUserInfo,
      DbHelper.AbleUser.AbleUserInfo invitedByUserInfo,
      bool isAutoReminder = false,
      bool updateSentDate = true) {

      if (inviteeUserInfo == null) throw new ArgumentNullException($"{nameof(inviteeUserInfo)} is required.");

      string returnMessage = "";

      // Do not continue if user is registered.
      if (inviteeUserInfo.IsRegistered) {
        returnMessage = isAutoReminder
          ? $"User {inviteeUserInfo.EmailAddress} is already registered, not sending the invite."
          : string.Empty;
        return new UserInviteResult(true, returnMessage);
      }

      // Do not proceed to send invite if it was already sent today, return true and log info.
      if (inviteeUserInfo.LastInviteReminderSentUtc?.Date == DateTime.UtcNow.Date) {
        returnMessage = isAutoReminder
          ? $" An invite was sent to {inviteeUserInfo.EmailAddress} today already, skipping."
          : $"Invite was sent to {inviteeUserInfo.GetFullName()} at {inviteeUserInfo.EmailAddress}.";
        return new UserInviteResult(true, returnMessage);
      }

      int invitedByUserId = DbHelper.AbleUser.GetInvitedByUserId(inviteeUserInfo);

      // Validate invitedBy information was received not null.
      if (invitedByUserInfo == null || inviteeUserInfo?.InvitedByUserId != invitedByUserId) {
        // If userInfo.InvitedByUserId is null, use "Unassigned" userId as default to prevent having coachInfo null and not sending invite.
        invitedByUserInfo = DbHelper.AbleUser.GetUserByIdOrNull(inviteeUserInfo.InvitedByUserId ?? ConfigHelper.UserId.Unassigned, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
        if (invitedByUserInfo == null) throw new Exception("User invited by was not found.");
      }

      // Update invitation fields for user and get invite code.
      DbHelper.AbleUser.UpdateInviteDetails(inviteeUserInfo, invitedByUserInfo.UserId);

      bool emailSent = false, updatedSentDate = false;

      // Send invite email.
      emailSent = SendUserInvite(inviteeUserInfo, invitedByUserInfo);

      // If email was sent, update last invite date.
      if (emailSent && updateSentDate) {
        updatedSentDate = DbHelper.AbleUser.UpdateLastInviteReminderSent(inviteeUserInfo, DateTime.UtcNow);
      }

      if (emailSent && (updatedSentDate || !updateSentDate)) {

        // If operation was successful, display log accordingly if it's auto reminder or ux.
        returnMessage = isAutoReminder
          ? $" - Sent."
          : $"An invitation email to join the platform has been sent to {inviteeUserInfo.GetFullName()} at {inviteeUserInfo.EmailAddress}.";
        return new UserInviteResult(true, returnMessage);

      } else if (emailSent) {

        returnMessage = isAutoReminder
          ? " - Email sent, problem updating LastInvite field."
          : $"Sent invite to {inviteeUserInfo.GetFullName()}, email: {inviteeUserInfo.EmailAddress}";
        return new UserInviteResult(true, returnMessage);

      } else {

        returnMessage = isAutoReminder
          ? " - Failed to send email."
          : $"Failed to send invite to {inviteeUserInfo.GetFullName()}, email: {inviteeUserInfo.EmailAddress}";
        return new UserInviteResult(false, returnMessage);
      }

    }

    private static bool SendUserInvite(
      DbHelper.AbleUser.AbleUserBasicInfo inviteeUserInfo,
      DbHelper.AbleUser.AbleUserInfo invitedByUserInfo) {

      if (inviteeUserInfo == null) throw new ArgumentException("inviteeUserInfo is null.");
      if (inviteeUserInfo.InvitedByUserId == null) throw new ArgumentException("inviteeUserInfo.InvitedByUserId is null.");
      if (inviteeUserInfo.FirstName.IsNullOrEmpty()) throw new ArgumentException("inviteeUserInfo.FirstName is required.");
      if (inviteeUserInfo.LastName.IsNullOrEmpty()) throw new ArgumentException("inviteeUserInfo.LastName is required.");
      if (inviteeUserInfo.EmailAddress.IsNullOrEmpty()) throw new ArgumentException("inviteeUserInfo.EmailAddress is required.");

      var emailTo = new MailAddress(inviteeUserInfo.EmailAddress, inviteeUserInfo.GetFullName());

      string computedSenderEmailName = "";
      // If the userId is unassigned, use the coordinator's name. Otherwise use Coach name.
      if (invitedByUserInfo.UserId == ConfigHelper.UserId.Unassigned) {
        invitedByUserInfo.FirstName = ConfigHelper.Email_Coordinator_FullName;
        computedSenderEmailName = ConfigHelper.Email_Coordinator_FullName;
      } else {
        computedSenderEmailName = invitedByUserInfo.ComputedSenderEmailName;
      }

      string bodyTextForUserType = "";

      if (inviteeUserInfo.IsAbleAdmin || inviteeUserInfo.IsAbleCoach) {

        bodyTextForUserType = "We give you tools, technology and access to a network of experienced coaches, trainers and facilitators, so you can easily provide the best leadership and organisation development programs for your clients.";

      } else if (inviteeUserInfo.IsAbleClient) {

        bodyTextForUserType = "Able allows your to view your quotes, track program progress, add and view participants, review quality and impact information, contact your delivery team and much more.";

      } else if (invitedByUserInfo.IsParticipant) {

        string latestCoacheeInfoText = "";

        if (inviteeUserInfo.LatestCoacheeInfo != null) {
          var latestCoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(inviteeUserInfo.LatestCoacheeInfo.CoacheeId);
          if (latestCoacheeInfo != null) {
            latestCoacheeInfoText = $@" and the <b>""{latestCoacheeInfo.FriendlyProjectTitle}""</b> with <b>{latestCoacheeInfo.CompanyName}</b>";
          }
        }

        bodyTextForUserType = $@"
          Able is an essential part of your leadership development journey {latestCoacheeInfoText}.
          <br/><br/>
          You need to create an Able account to view the program content, meet your coach, book your coaching, complete your 360º or development plan and get help advice from your AI-Coach.";
      }

      string fromOrgName = (invitedByUserInfo != null && invitedByUserInfo.UserId != ConfigHelper.UserId.Unassigned) ? $"from {invitedByUserInfo.OrgName}" : "";

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.SpacerGif(true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, "" },
        { MandrillMergeTags.TitleText, invitedByUserInfo.GetFullName() + " has invited you to the join the Able platform." },
        { MandrillMergeTags.EXTRACSS, @"

          .templateContainer{ max-width:700px !important; }
          table[width=""600""], td[width=""600""] { width: 700px !important; }
          #templateHeader { display: none !important; }
          #templateBody { background-color: #F5F8FA !important; }
          #templateBody td { background-color: transparent !important; }
          #templateBody * { box-sizing: border-box; font-size: 14px; line-height: 1.25; font-weight: 400; color: #6a7283; letter-spacing: .2px;
                            font-family: Open Sans,-apple-system,system-ui,BlinkMacSystemFont,Helvetica,Arial,Segoe UI,Roboto,sans-serif; }
          #templateBody ul, #templateBody li, #templateBody p { margin: 14px 0; }
          .border-box * { box-sizing: border-box; }

        " },
        { MandrillMergeTags.BodyHTML, $@"
          <div style=""background-color: #FFF; border-radius: 10px; padding: 25px;"">
            {GetEmailTopLogoHtml(PathHelper.Images.AbleEmailLogo(true))}
            {GetReferralEmailHeaderHtml(invitedByUserInfo, true)}
            <div style=""margin-top: 15px;"">
              <p>Hi {inviteeUserInfo.FirstName},</p>
              <p>{invitedByUserInfo.GetFullName()} {fromOrgName} has invited you to join them on the Able platform!</p>
              <p>{bodyTextForUserType}</p>
              <p><a href=""{PathHelper.Pages.RegisterInvited(inviteeUserInfo.InviteCode, true)}"" style=""display: block; margin-top: 20px; background-color: #513ED5; color: #FFF; font-size: 14px; border-radius: 5px; text-align: center; padding: 10px 20px; text-decoration: none;"">Claim Your Invite</a></p>
            </div>
          </div>
        "}
      };

      return EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>() { emailTo }, null, null,
        computedSenderEmailName, invitedByUserInfo.ComputedSenderEmailAddress,
        null, false,
        $"{invitedByUserInfo.GetFullName()} {fromOrgName} has invited you to the join Able.",
        ConfigHelper.MandrillTemplate_Generic,
        mergePairs,
        out _,
        filesToAttach: null);
    }

    public static bool SendEndOfProjectSurveyEmail(
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      DbHelper.ProjectUserAccess.ProjectAccessInfo projectUserAccessInfo,
      DbHelper.Projects.ProjectInfoBrief projectInfoBrief,
      string selfPartUID,
      Addressee addrSelf) {

      string emailSubject = $"Thank you {projectUserAccessInfo.FullName}! How was your project?";

      string surveyUrl = PathHelper.Pages.Survey(surveyInfo, selfPartUID, true);
      string declineUrl = PathHelper.Pages.RaterDecline(surveyInfo.SurveyUID, selfPartUID, true);
      string projectLogo = PathHelper.Images.ProjectLogo(surveyInfo.ProgramJobNumber, surveyInfo.BrandingOrgGuid, surveyInfo.OrgGuid, false, true);

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, projectLogo },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, surveyInfo.FriendlyProjectTitle.ValueIfNullOrEmpty("") },
        { SurveyInvitationMergeField_ExtraCSS, " p { margin: 0 !important } " },
        { SurveyInvitationMergeField_SurveyName, surveyInfo.SurveyName },
        { SurveyInvitationMergeField_RecipientFirstName, projectUserAccessInfo.FirstName },
        { MandrillMergeTags.DeclineLink, "<a href=\"" + declineUrl + "\">I do not wish to participate</a>" }
      };

      mergePairs.Add(SurveyInvitationMergeField_SurveyEmailContent, $@"
          <div style=""background-color: #FFF; border-radius: 10px;"">
            <p>Your project {projectInfoBrief.ProjectName.HTMLEncode()} has now finished and we want to thank you for the opportunity to work with you and your organisation.</p>
            <br/>
            <p>To help us improve, we would love to hear your feedback. Please take a couple of minutes to fill out this short evaluation survey:</p>
            <p><a href=""{surveyUrl}"" style=""display: block; margin-top: 20px; background-color: #513ED5; color: #FFF; font-size: 14px; border-radius: 5px; text-align: center; padding: 10px 20px; text-decoration: none;"">Begin Your Survey</a></p>
            <br/>
            <p>Thank you again for your support.</p>
            <p style=""margin-top: 20px;"">Best regards,</p>
            <p>Integral</p>
          </div>
        ");

      return EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>() { new MailAddress(addrSelf.Email, addrSelf.GetFullName()) }, null, null,
        null, null,
        projectInfoBrief, false,
        emailSubject,
        ConfigHelper.MandrillTemplate_SurveyInvitation,
        mergePairs,
        out _);
    }

    public static bool SendQuoteRequestEmail(
      DbHelper.AbleUser.AbleUserInfo clientUserInfo,
      DbHelper.ClientCompanies.AlbertCompanyInfo clientCompanyInfo,
      DbHelper.Projects.ProjectInfo projectInfo,
      string requestText) {

      if (clientUserInfo == null) throw new ArgumentException("client userInfo is required.");

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.TitleText, "Quote Request" },
        { SurveyInvitationMergeField_ExtraCSS, " p { margin: 0 !important } " }
      };

      string requestInfoLine = "", contactInfo = "", clientLeadName, clientLeadEmail;
      string linkColorStyle = "style=\"color: #513ED5;\"";
      string quotesPath = projectInfo != null ? PathHelper.Pages.ProjectQuotes(projectInfo.JobNumber) : PathHelper.Pages.QuoteList();
      string emailSubject = $"A quote has been requested by client.";

      // RequestInfoLine will display the general information on top, depending if it was requested from a project or not.
      requestInfoLine += $@"<p>Client: <a {linkColorStyle} href=""{PathHelper.Pages.CoachEdit(clientUserInfo.UserId)}"">{clientUserInfo.GetFullName()}</a></p>";

      if (projectInfo != null) {
        requestInfoLine += $@"<p>Project: <a {linkColorStyle} href=""{quotesPath}"">{projectInfo.FriendlyProjectTitle.ValueIfNullOrEmpty(projectInfo.ProjectName)}</a></p>";
        requestInfoLine += $@"<p>Company: <a {linkColorStyle} href=""{PathHelper.Pages.OrganisationOverview(projectInfo.CompanyId)}"">{projectInfo.ClientCompanyName}</a></p>";

        mergePairs.Add(MandrillMergeTags.ProjectLogoURL, PathHelper.Images.ProjectLogo(projectInfo, false));
        mergePairs.Add(MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString());
        mergePairs.Add(MandrillMergeTags.FriendlyProjectTitle, projectInfo.FriendlyProjectTitle.ValueIfNullOrEmpty(projectInfo.ProjectName));

      } else if (clientCompanyInfo != null) {
        requestInfoLine += $@"<p>Company: <a {linkColorStyle} href=""{PathHelper.Pages.OrganisationOverview(clientCompanyInfo.CompanyId)}"">{clientCompanyInfo.CompanyName}</a></p>";
      }

      // If Client Lead of the company is set, send the email to them, otherwise to coordination.
      if (clientCompanyInfo != null && clientCompanyInfo.ClientLeadEmail != null) {
        clientLeadName = $"{clientCompanyInfo.ClientLeadFirstName} {clientCompanyInfo.ClientLeadLastName}";
        clientLeadEmail = clientCompanyInfo.ClientLeadEmail;
        mergePairs.Add(SurveyInvitationMergeField_RecipientFirstName, clientCompanyInfo.ClientLeadFirstName);
      } else {
        clientLeadName = ConfigHelper.Email_Coordinator_FullName;
        clientLeadEmail = ConfigHelper.Email_Coordinator_Address;
        mergePairs.Add(SurveyInvitationMergeField_RecipientFirstName, ConfigHelper.Email_Coordinator_FullName);
      }

      contactInfo = $"<p style=\"margin-bottom: 10px;\">You can contact the client via email at <a {linkColorStyle} target=\"_blank\" href=\"mailto:{clientUserInfo.EmailAddress}\">{clientUserInfo.EmailAddress}</a>" + (clientUserInfo.MobileNumber.IsNullOrEmpty() ? "." : $" or by phone at {clientUserInfo.MobileNumber}.</p>");

      mergePairs.Add(MandrillMergeTags.BodyHTML, $@"
          <div style=""background-color: #FFF; border-radius: 10px;"">
            {requestInfoLine}
            <br/>
            <p>{clientUserInfo.GetFullName()} has requested a quote, this is the specification they gave:</p>
            <div style=""margin-top: 10px; margin-bottom: 10px; font-style: italic; color: #3424a1; text-align: justify;"">
              {requestText}
            </div>
            {contactInfo}
            <p><a href=""{quotesPath}"" style=""display: block; margin-top: 20px; background-color: #513ED5; color: #FFF; font-size: 14px; border-radius: 5px; text-align: center; padding: 10px 20px; text-decoration: none;"">Create a Quote</a></p>
            <p style=""margin-top: 20px;"">Best regards,</p>
            <p>Integral</p>
          </div>
        ");

      return EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>() { new MailAddress(clientLeadEmail, clientLeadName) }, null, null,
        ConfigHelper.Email_Coordinator_FullName, ConfigHelper.Email_Coordinator_Address,
        null, false,
        emailSubject,
        ConfigHelper.MandrillTemplate_Generic,
        mergePairs,
        out _);
    }

    public static bool SendClientInviteToAddParticipants(
      DbHelper.AbleUser.AbleUserBasicInfo inviteeUserInfo,
      DbHelper.AblePrograms.AbleProgramInfo programInfo) {

      // Invite code must be generated already for the client.
      if (inviteeUserInfo.InviteCode.IsNullOrEmpty()) throw new ArgumentException("inviteeInfo.InviteCode required.");
      if (inviteeUserInfo.InvitedByUserId == null) throw new ArgumentException("inviteeInfo.InvitedByUserId required.");

      var invitedByUserInfo = DbHelper.AbleUser.GetBasicInfoById(inviteeUserInfo.InvitedByUserId.Value, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);

      var emailTo = new MailAddress(inviteeUserInfo.EmailAddress, inviteeUserInfo.GetFullName());
      bool isClientRegistered = inviteeUserInfo.IsRegistered;
      string subjectText = "";

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.SpacerGif(true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, "" },
        { MandrillMergeTags.TitleText, "You have been invited to add participants to your Program." },
        { MandrillMergeTags.EXTRACSS, @"

          .templateContainer{ max-width:700px !important; }
          table[width=""600""], td[width=""600""] { width: 700px !important; }
          #templateHeader { display: none !important; }
          #templateBody { background-color: #F5F8FA !important; }
          #templateBody td { background-color: transparent !important; }
          #templateBody * { box-sizing: border-box; font-size: 14px; line-height: 1.25; font-weight: 400; color: #6a7283; letter-spacing: .2px;
                            font-family: Open Sans,-apple-system,system-ui,BlinkMacSystemFont,Helvetica,Arial,Segoe UI,Roboto,sans-serif; }
          #templateBody ul, #templateBody li, #templateBody p { margin: 14px 0; }
          .border-box * { box-sizing: border-box; }

        " }
      };

      if (isClientRegistered) {

        mergePairs.Add(MandrillMergeTags.BodyHTML, $@"
          <div style=""background-color: #FFF; border-radius: 10px; padding: 25px;"">
            {GetEmailTopLogoHtml(PathHelper.Images.AbleEmailLogo(true))}
            <div>
              <p>Hi {inviteeUserInfo.FirstName},</p>
              <p>You have been invited to add participants to your program.</p>
              <p style=""margin-top: 30px;""><a href=""{PathHelper.Pages.ProgramParticipants(programInfo.ProgramJobId, true)}"" style=""display: block; margin-top: 20px; background-color: #513ED5; color: #FFF; font-size: 14px; border-radius: 5px; text-align: center; padding: 10px 20px; text-decoration: none;"">Start Adding Participants Now</a></p>
            </div>
          </div>
        ");

        subjectText = " has invited you to add Program Participants.";

      } else {

        mergePairs.Add(MandrillMergeTags.BodyHTML, $@"
          <div style=""background-color: #FFF; border-radius: 10px; padding: 25px;"">
            {GetEmailTopLogoHtml(PathHelper.Images.AbleEmailLogo(true))}
            <div>
              <p>Hi {inviteeUserInfo.FirstName},</p>
              <p>You have been invited to Able - Integral’s leadership development platform. By accepting this invitation, you will have full access to, and overview of your project/s.</p>
              <p>You can view project information, such as participants, group sessions, project team and program insights and evaluations. You will also have access to view signed and outstanding quotes and the full partner network. .</p>
              {GetReferralEmailHeaderHtml(invitedByUserInfo)}
              <p><a href=""{PathHelper.Pages.RegisterInvited(inviteeUserInfo.InviteCode, programInfo.ProgramJobId, true)}"" style=""display: block; margin-top: 20px; background-color: #513ED5; color: #FFF; font-size: 14px; border-radius: 5px; text-align: center; padding: 10px 20px; text-decoration: none;"">Claim Your Invite</a></p>
              <p style=""margin-top: 30px;""><b>How to sign up:</b></p>
              <p>1. Click the button above to claim your invite.</p>
              <p>2. Review your details.</p>
              <p>3. Create a password.</p>
              <p>4. Click the register button.</p>
              <p>5. Once registered, click the Sign In button.</p>
              <p>6. Once you log in, you will be taken to your project participants page.</p>
              <p><a href=""{PathHelper.Pages.ProgramParticipants(programInfo.ProgramJobId, true)}"" style=""display: block; margin-top: 20px; background-color: #513ED5; color: #FFF; font-size: 14px; border-radius: 5px; text-align: center; padding: 10px 20px; text-decoration: none;"">Start Adding Participants Now</a></p>
            </div>
          </div>
        ");

        subjectText = " has invited you to join Able - the leadership development platform.";
      }

      return EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>() { emailTo }, null, null,
        invitedByUserInfo.ComputedSenderEmailName, invitedByUserInfo.ComputedSenderEmailAddress,
        null, false,
        invitedByUserInfo.GetFullName() + subjectText,
        ConfigHelper.MandrillTemplate_Generic,
        mergePairs,
        out _);
    }

    public static bool SendQuoteAcceptanceEmail(
      DbHelper.AbleUser.AbleUserInfo quoteContactUserInfo,
      Guid quoteGuid) {

      // Get quote info with updated data from acceptance.
      var quoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(quoteGuid);

      if (quoteContactUserInfo == null) throw new ArgumentException("QuoteContactUserInfo required.");
      if (quoteInfo == null) throw new ArgumentException("QuoteInfo required.");
      if (quoteInfo.IsAccepted == false) throw new ArgumentException("QuoteInfo must be accepted.");

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.SpacerGif(true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, "" },
        { MandrillMergeTags.TitleText, quoteInfo.QuoteTitle },
        { MandrillMergeTags.BodyHTML, $@"
          <div style=""background-color: #FFF; border-radius: 10px; padding: 25px;"">
            {GetEmailTopLogoHtml(PathHelper.Images.AbleEmailLogo(true))}
            <div>
              <p>Hi {quoteContactUserInfo.FirstName.HTMLEncode()},</p>
              <p>Thank you for signing your quote.</p>
              <p><b>Details</b></p>
              <ul>
                <li><b>Company:</b> {quoteInfo.CompanyInfo.CompanyName.HTMLEncode()}</li>
                <li><b>Project:</b> {quoteInfo.ProjectName.HTMLEncode()}</li>
                <li><b>Deal Accepted Amount:</b> ${quoteInfo.ClientAcceptedAmount?.ToString("#,##0.00")}</li>
                <li><b>Accepted Date:</b> {quoteInfo.ClientAcceptedUtc.ToString("d MMM yyyy")}</li>
                <li><b>Payment details:</b> {quoteInfo.AccPayFirstName.HTMLEncode()} {quoteInfo.AccPayLastName.HTMLEncode()} ({quoteInfo.AccPayEmailAddress.HTMLEncode()})</li>
              </ul>
              <p>{(quoteInfo.ClientAcceptedAmount.GetValueOrDefault(0) > ConfigHelper.Financial.QuoteAmountMaxFor100PercentInvoicing
                ? "Your contract will automatically be invoiced 50% on signing and 50% upon delivering 30% of your program unless otherwise agreed."
                : "Your contract will automatically be invoiced 100% on signing unless otherwise agreed.")}</p>
              <p>For more details, please log into Able.</p>
              <p style=""margin-top: 30px;""><a href=""{PathHelper.Pages.QuotePublicView(quoteInfo.PublicGuid, true)}"" style=""display: block; margin-top: 20px; background-color: #513ED5; color: #FFF; font-size: 14px; border-radius: 5px; text-align: center; padding: 10px 20px; text-decoration: none;"">Go to Able</a></p>
            </div>
          </div>"}
      };

      return EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>() { new MailAddress(quoteContactUserInfo.EmailAddress, quoteContactUserInfo.GetFullName()) }, null, null,
        quoteInfo.OwnerFullName, quoteInfo.OwnerEmail,
        null, false,
        $"Thank you for signing your quote: {quoteInfo.QuoteTitle.HTMLEncode()}",
        ConfigHelper.MandrillTemplate_Generic,
        mergePairs,
        out _);
    }

    public static int SendProgramStartedEmail(DbHelper.AblePrograms.AbleProgramInfo programInfo) {

      var projectAccessUserList = DbHelper.ProjectUserAccess.GetProjectAccessUsers(programInfo.ProgramJobNumber);
      var PLCUserInfo = DbHelper.AbleUser.GetBasicInfoById(programInfo.LeadConsultantUserId.Value, DbHelper.AbleUser.RegisteredFilter.Any);
      int emailsSent = 0;

      foreach (var projectAccessInfo in projectAccessUserList) {
        bool emailSent = SendProgramStartedEmailForUser(programInfo, projectAccessInfo, PLCUserInfo);
        if (emailSent) emailsSent++;
      }

      if (PLCUserInfo != null) {
        bool emailSent = SendProgramStartedEmailForUser(programInfo, null, PLCUserInfo);
        if (emailSent) emailsSent++;
      }

      return emailsSent;
    }

    private static bool SendProgramStartedEmailForUser(
      DbHelper.AblePrograms.AbleProgramInfo programInfo,
      DbHelper.ProjectUserAccess.ProjectAccessInfo projectAccessInfo,
      DbHelper.AbleUser.AbleUserBasicInfo plcUserInfo) {

      MailAddress emailTo = null;

      string receiverFirstName = "";
      string preferredSenderEmailName = null, preferredSenderEmailAddress = null;

      if (projectAccessInfo == null) {

        emailTo = new MailAddress(plcUserInfo.EmailAddress, plcUserInfo.GetFullName());
        receiverFirstName = plcUserInfo.FirstName;

      } else {

        emailTo = new MailAddress(projectAccessInfo.Email, projectAccessInfo.FullName);
        receiverFirstName = projectAccessInfo.FirstName;

        if (plcUserInfo != null && plcUserInfo.UserId != ConfigHelper.UserId.Unassigned) {
          preferredSenderEmailName = plcUserInfo.GetFullName();
          preferredSenderEmailAddress = plcUserInfo.EmailAddress;
        }
      }

      string subjectText = $"{receiverFirstName}, the first component of your Able Program has been delivered!";

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.SpacerGif(true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, "" },
        { MandrillMergeTags.TitleText, subjectText },
        { MandrillMergeTags.BodyHTML, $@"
          <div style=""background-color: #FFF; border-radius: 10px; padding: 25px;"">
            {GetEmailTopLogoHtml(PathHelper.Images.AbleEmailLogo(true))}
            <div>
              <p>Hi {receiverFirstName.HTMLEncode()},</p>
              <p>We're excited to inform you that your leadership program is now active!</p>
              <p>This means the first participants are onboarding.</p>
              <p><b>Details</b></p>
              <ul>
                <li><b>Company:</b> {programInfo.CompanyName.HTMLEncode()}</li>
                <li><b>Project:</b> {programInfo.ProjectName.HTMLEncode()}</li>
                <li><b>Program:</b> {programInfo.ProgramJobName.HTMLEncode()}</li>
              </ul>
              <p>Colleagues added to Project Access have also been informed.</p>
              <p>See your Able Program Overview page to find out more.</p>
              <p style=""margin-top: 30px;""><a href=""{PathHelper.Pages.ProgramOverview(programInfo.ProgramJobId, true)}"" style=""display: block; margin-top: 20px; background-color: #513ED5; color: #FFF; font-size: 14px; border-radius: 5px; text-align: center; padding: 10px 20px; text-decoration: none;"">Go to Able</a></p>
            </div>
          </div>"}
      };

      return EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>() { emailTo }, null, null,
        preferredSenderEmailName, preferredSenderEmailAddress,
        null, false,
        subjectText,
        ConfigHelper.MandrillTemplate_Generic,
        mergePairs,
        out _);
    }

    public static string GetReferralEmailHeaderHtml(DbHelper.AbleUser.AbleUserBasicInfo invitedByUserInfo, bool includeOrganisationName = false) {

      // Do not include img or table column if user is coordination.
      string referralPictureHtml = "", styleWhenCoordinator = "", fromOrgName = "";

      if (invitedByUserInfo.UserId == ConfigHelper.UserId.Unassigned) {

        invitedByUserInfo.FirstName = ConfigHelper.Email_Coordinator_FullName;
        styleWhenCoordinator = "padding-left: 36px;";

      } else {

        if (includeOrganisationName) fromOrgName = $" from {invitedByUserInfo.OrgName}";

        referralPictureHtml = $@"
          <td width=""140"">
            <img width=""110"" height=""110"" src=""{PathHelper.Images.UserPhoto(invitedByUserInfo, PathHelper.Images.UserPhotoSize.Large, placeholderIfNotFound: true, includeDomain: true)}"" style=""border: 14px solid #EBEEF1; border-radius: 40px; width: 110px; height: 110px;""/>
          </td>
        ";
      }

      return $@"
        <div style=""background-color: #F5F8FA; border-radius: 28px; padding: 14px; margin-top: 10px; {styleWhenCoordinator}"">
          <table cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
            <tr>
              {referralPictureHtml}
              <td>
                <div style=""font-weight: 700; color: #1a1b25; font-size: 21px; margin-bottom: 10px; line-height: 1.2;"">{invitedByUserInfo.GetFullName().HTMLEncode()} {fromOrgName.HTMLEncode()} has invited you to join the Able platform.</div>
                <div style=""font-weight: 700; color: #6a7283; font-size: 14px;"">Join the platform by registering for a free account.</div>
              </td>
            </tr>
          </table>
        </div>
      ";
    }

    public static string GetReminderEmailItemHtml(string mainTitle, string description, string optionalExtraLineText, string buttonContentHtml, string linkUrl) {

      string optionalExtraLineHtml = "";

      if (!optionalExtraLineText.IsNullOrEmpty()) {
        optionalExtraLineHtml = $@"
          <div style=""font-weight: 500; color: #1a1b25; font-size: 16px; margin-bottom: 5px; line-height: 1.2;"">
            {optionalExtraLineText.HTMLEncode()}.
          </div>";
      }

      return $@"
        <div style=""background-color: #F5F8FA; border-radius: 28px; padding: 24px; margin-top: 15px; margin-bottom: 10px; "">
          <table cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
            <tr>
              <td>
                <div style=""font-weight: 700; color: #1a1b25; font-size: 17px; margin-bottom: 5px; line-height: 1.2;"">
                  {mainTitle.HTMLEncode()}.
                </div>
                <div style=""font-weight: 500; color: #1a1b25; font-size: 17px; margin-bottom: 5px; line-height: 1.2;"">
                  {description.HTMLEncode()}.
                </div>
                {optionalExtraLineHtml}
                <div style=""float: right; margin-left: 20px; margin-top: 15px;"">
                  <div>
                    <center>
                      <a href=""{linkUrl.HTMLEncode()}"" style=""display:block;width:130px;height: 35px;border-radius:3px;background:#0b1736;color:#fff;text-decoration:none;font-weight:bold;line-height:35px;"" target=""_blank"">
                        <span style=""font-size:13px"">
                          <span style=""font-family:arial,helvetica neue,helvetica,sans-serif"">
                            <span style=""color:#FFFFFF"">{buttonContentHtml}</span>
                          </span>
                        </span>
                      </a>
                    </center>
                  </div>
                </div>
              </td>
            </tr>
          </table>
        </div>
      ";
    }

    public static string GetReminderEmailBodyHtml(string firstName, List<string> selfEmailItems, List<string> addMoreRaterEmailItems, List<string> raterEmailItems) {
      string selfEmailItemsHtml = "", raterEmailItemsHtml = "", addMoreRaterEmailItemsHtml = "";

      if (selfEmailItems.Count > 0) {
        selfEmailItemsHtml = $@"
          <div style=""margin-top: 20px;"">
            <div style=""font-weight: 700; color: #1a1b25; font-size: 18px; margin-bottom: 10px; line-height: 1.2;"">Complete your survey{(selfEmailItems.Count > 1 ? "s" : "")}:</div>
            {string.Join("", selfEmailItems)}
          </div>
        ";
      }

      if (addMoreRaterEmailItems.Count > 0) {
        addMoreRaterEmailItemsHtml = $@"
          <div style=""margin-top: 20px;"">
            <div style=""font-weight: 700; color: #1a1b25; font-size: 18px; margin-bottom: 10px; line-height: 1.2;"">Invite more raters:</div>
            {string.Join("", addMoreRaterEmailItems)}
          </div>
        ";
      }

      if (raterEmailItems.Count > 0) {
        raterEmailItemsHtml = $@"
          <div style=""margin-top: 20px;"">
            <div style=""font-weight: 700; color: #1a1b25; font-size: 18px; margin-bottom: 10px; line-height: 1.2;"">Seeking feedback:</div>
            {string.Join("", raterEmailItems)}
          </div>
        ";
      }

      return $@"
        <div style=""background-color: #FFF; border-radius: 10px; padding: 25px;"">
          {GetEmailTopLogoHtml(PathHelper.Images.AbleEmailLogo(true))}
          <div>
            <p>Hi {firstName},</p>
            <p>Just a friendly reminder to complete the following pending assessment{(selfEmailItems.Count + raterEmailItems.Count > 1 ? "s" : "")} listed below to continue your leadership journey.</p>
            {selfEmailItemsHtml}
            {addMoreRaterEmailItemsHtml}
            {raterEmailItemsHtml}
          </div>
        </div>
      ";
    }

    public static bool SendSelfInvitationEmail(
      DbHelper.Projects.ProjectInfoBrief projectInfoBrief,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      string workshopName,
      int selfPartId, string selfPartUID,
      int? coacheeId, int? programJobId,
      string preferredSenderEmailName, string preferredSenderEmailAddress,
      Addressee addrSelf,
      Addressee addrCoach,
      string companyName,
      bool isReminder,
      bool updateSentDate = true) {

      string emailSubject = GetSelfInvitationEmailSubject(surveyInfo, workshopName, addrSelf, isReminder);

      string emailHTML = GetSelfInvitationEmailBody(surveyInfo, workshopName, selfPartUID, addrSelf, addrCoach, companyName);

      var headers = new NameValueCollection {
        { EmailHeaderForSurveyUID, surveyInfo.SurveyUID },
        { EmailHeaderForPartUID, selfPartUID },
        { EmailHeaderAbleFlag, "true" }
      };

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.ProjectLogo(surveyInfo.ProgramJobNumber, surveyInfo.BrandingOrgGuid, projectInfoBrief.TenantOrgGuid, false, true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, surveyInfo.FriendlyProjectTitle.ValueIfNullOrEmpty("") },
        { SurveyInvitationMergeField_ExtraCSS, " p { margin: 0 !important } " },
        { SurveyInvitationMergeField_SurveyName, surveyInfo.SurveyName },
        { SurveyInvitationMergeField_RecipientFirstName, addrSelf.FirstName },
        { SurveyInvitationMergeField_SurveyEmailContent, emailHTML }
      };

      bool emailSent = EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>() { new MailAddress(addrSelf.Email, addrSelf.GetFullName()) }, null, null,
        preferredSenderEmailName, preferredSenderEmailAddress,
        projectInfoBrief, true,
        emailSubject,
        ConfigHelper.MandrillTemplate_SurveyInvitation,
        mergePairs,
        out _);

      // If email successful, update time of last email sent.
      if (emailSent && updateSentDate) {
        if (!isReminder) DbHelper.Participants.UpdateFirstInvitationSent(selfPartId, DateTime.UtcNow);
        else DbHelper.Participants.UpdateLastInvitationSent(selfPartId, DateTime.UtcNow);
        if (coacheeId != null && programJobId != null) {
          DbHelper.EmailHistory.AddEmail(null, (int)coacheeId, (int)programJobId, false, "", ConfigHelper.Email_Coordinator_Address, addrSelf.Email, emailSubject, null);
        }
      }
      return emailSent;
    }

    public static bool SendRaterInvitationEmail(
      DbHelper.Projects.ProjectInfoBrief projectInfoBrief,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      int raterPartId, string raterPartUID,
      string preferredSenderEmailName, string preferredSenderEmailAddress,
      Addressee addrRater,
      Addressee addrSelf,
      Addressee addrCoach,
      string companyName,
      bool isReminder,
      bool updateLastInvitationSent = true) {

      string emailSubject = GetRaterInvitationEmailSubject(surveyInfo, addrSelf, addrRater, raterPartUID, isReminder);

      string emailHTML = GetRaterInvitationEmailBody(surveyInfo, raterPartUID, addrSelf, addrRater, addrCoach, companyName);

      var headers = new NameValueCollection {
        { EmailHeaderForSurveyUID, surveyInfo.SurveyUID },
        { EmailHeaderForPartUID, raterPartUID },
        { EmailHeaderAbleFlag, "true" }
      };

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.ProjectLogo(surveyInfo.ProgramJobNumber, surveyInfo.BrandingOrgGuid, projectInfoBrief.TenantOrgGuid, false, true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, surveyInfo.FriendlyProjectTitle.ValueIfNullOrEmpty("") },
        { SurveyInvitationMergeField_ExtraCSS, " p { margin: 0 !important } " },
        { SurveyInvitationMergeField_SurveyName, surveyInfo.SurveyName },
        { SurveyInvitationMergeField_RecipientFirstName, addrRater.FirstName },
        { SurveyInvitationMergeField_SurveyEmailContent, emailHTML }
      };

      bool emailSent = EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>() { new MailAddress(addrRater.Email, addrRater.GetFullName()) }, null, null,
        preferredSenderEmailName, preferredSenderEmailAddress,
        projectInfoBrief, true,
        emailSubject,
        ConfigHelper.MandrillTemplate_SurveyInvitation,
        mergePairs,
        out _);

      // If email successful, update time of last email sent.
      if (emailSent && updateLastInvitationSent) DbHelper.Participants.UpdateLastInvitationSent(raterPartId, DateTime.UtcNow);

      return emailSent;
    }

    // Alternate sig for the above, but gets rater data from the db.
    public static bool SendRaterInvitationEmail(
      DbHelper.Projects.ProjectInfo projectInfo,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      int raterPartId,
      string preferredSenderEmailName, string preferredSenderEmailAddress) {

      DbHelper.AlbertCoachees.RaterEmailInfo raterInfo = DbHelper.AlbertCoachees.GetRaterEmailInfo(surveyInfo.SurveyId, raterPartId);

      return SendRaterInvitationEmail(
        projectInfo, surveyInfo,
        raterInfo.RaterPartId, raterInfo.RaterPartUID,
        preferredSenderEmailName, preferredSenderEmailAddress,
        new Addressee(raterInfo.RaterFirstName, raterInfo.RaterLastName, raterInfo.RaterEmail),
        new Addressee(raterInfo.CoacheeFirstName, raterInfo.CoacheeLastName, raterInfo.CoacheeEmail),
        new Addressee(raterInfo.CoachFirstName, raterInfo.CoachLastName, raterInfo.CoachEmail),
        raterInfo.CompanyName, true);
    }

    private static string GetSelfInvitationEmailSubject(
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      string workshopName,
      Addressee addrSelf,
      bool isReminder) {

      // All invite email subjects same, independent of survey.
      string emailSubject;
      if (!surveyInfo.InvitationEmailSubject_Self.IsNullOrEmpty()) {
        emailSubject = surveyInfo.InvitationEmailSubject_Self;
      } else if (surveyInfo.OrgId == ConfigHelper.JarvisOrgId) {
        emailSubject = ConfigHelper.JarvisSurveyInviteEmailSubject_Self; // Jarvis default subject
      } else {
        emailSubject = ConfigHelper.AlbertSurveyInviteEmailSubject_Self; // Able default subject
      }
      // Replace merge fields.
      emailSubject = ReplaceInvitationEmailSubjectMergeFields(emailSubject, surveyInfo.SurveyName, workshopName, addrSelf);
      // Reminder prefix.
      if (isReminder) emailSubject = "Reminder: " + emailSubject;

      return emailSubject;
    }

    private static string GetRaterInvitationEmailSubject(
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      Addressee addrSelf,
      Addressee addrRater,
      string raterPartUID,
      bool isReminder) {

      // All invite email subjects same, independent of survey.
      string emailSubject;
      if (!surveyInfo.InvitationEmailSubject_Rater.IsNullOrEmpty()) {
        emailSubject = surveyInfo.InvitationEmailSubject_Rater;
      } else if (surveyInfo.OrgId == ConfigHelper.JarvisOrgId) {
        emailSubject = ConfigHelper.JarvisSurveyInviteEmailSubject_Rater;
      } else {
        // Different email subject if survey is pulse or not.
        emailSubject = surveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Pulse360
          ? ConfigHelper.AlbertSurveyInviteEmailSubject_PulseRater
          : ConfigHelper.AlbertSurveyInviteEmailSubject_Rater;
      }

      // Replace merge fields.
      emailSubject = ReplaceInvitationEmailSubjectMergeFields(emailSubject, surveyInfo.SurveyName, "", addrSelf, addrRater);
      // Reminder prefix.
      if (isReminder) emailSubject = "Reminder: " + emailSubject;

      return emailSubject;
    }

    private static string GetSelfInvitationEmailBody(
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      string workshopName,
      string selfPartUID,
      Addressee addrSelf,
      Addressee addrCoach,
      string companyName) {

      // Email body containing merge fields. Same for all surveys.
      string emailBodyTemplate;
      if (!surveyInfo.InvitationEmailBodyTemplate_Self.IsNullOrEmpty()) {
        emailBodyTemplate = surveyInfo.InvitationEmailBodyTemplate_Self;
      } else if (surveyInfo.OrgId == ConfigHelper.JarvisOrgId) {
        emailBodyTemplate = ConfigHelper.JarvisSurveyInviteEmailBody_Self; // Default Jarvis body
      } else {
        emailBodyTemplate = ConfigHelper.AlbertSurveyInviteEmailBody_Self; // Default Able body
      }

      string emailHTML = ReplaceInvitationEmailBodyMergeFields(
        emailBodyTemplate,
        surveyInfo,
        workshopName,
        selfPartUID,
        addrSelf, addrSelf, addrCoach,
        companyName,
        surveyInfo.CloseDateSelfLocal);

      return emailHTML;
    }

    private static string GetRaterInvitationEmailBody(
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      string raterPartUID,
      Addressee addrSelf,
      Addressee addrRater,
      Addressee addrCoach,
      string companyName) {

      // Email body template containing merge fields.
      string emailBodyTemplate;
      if (!surveyInfo.InvitationEmailBodyTemplate_Rater.IsNullOrEmpty()) {
        emailBodyTemplate = surveyInfo.InvitationEmailBodyTemplate_Rater;
      } else if (surveyInfo.OrgId == ConfigHelper.JarvisOrgId) {
        emailBodyTemplate = ConfigHelper.JarvisSurveyInviteEmailBody_Rater;
      } else {
        // Different email body if survey is pulse or not.
        emailBodyTemplate = surveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Pulse360
          ? ConfigHelper.AlbertSurveyInviteEmailBody_PulseRater
          : ConfigHelper.AlbertSurveyInviteEmailBody_Rater;
      }

      emailBodyTemplate = ReplaceInvitationEmailBodyMergeFields(
        emailBodyTemplate,
        surveyInfo, "",
        raterPartUID,
        addrRater, addrSelf, addrCoach,
        companyName,
        surveyInfo.CloseDateRatersLocal);

      return emailBodyTemplate;
    }

    // Replace merge fields in the email Subject.
    // Used for sending to both self or rater.
    private static string ReplaceInvitationEmailSubjectMergeFields(
      string emailSubject,
      string surveyName,
      string workshopName,
      Addressee addrSelf = null,
      Addressee addrRater = null) {

      return emailSubject.ReplaceTags(
        new Dictionary<string, string>() {
          { MandrillMergeTags.SurveyName, surveyName },
          { MandrillMergeTags.WorkshopName, workshopName },
          { MandrillMergeTags.SelfName_SelfFullName, addrSelf == null ? "" : addrSelf.GetFullName() },
          { MandrillMergeTags.SelfFirstName, addrSelf == null ? "" : addrSelf.FirstName },
          { MandrillMergeTags.SelfLastName, addrSelf == null ? "" : addrSelf.LastName },
          { MandrillMergeTags.RaterFirstName, addrRater == null ? "" : addrRater.FirstName },
          { MandrillMergeTags.RaterLastName, addrRater == null ? "" : addrRater.LastName },
          { MandrillMergeTags.RaterName_RaterFullName, addrRater == null ? "" : addrRater.GetFullName() }
        });
    }

    // Close date is taken as in the survey local time, i.e. the literal date displayed in the email.
    private static string ReplaceInvitationEmailBodyMergeFields(
      string emailBodyTemplate,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      string workshopName,
      string recipientPartUID,
      Addressee addrRecipient,
      Addressee addrSelf, // same as recipient if sending to self participant.
      Addressee addrCoach,
      string companyName,
      DateTime closeDate) {

      string surveyUrl = PathHelper.Pages.Survey(surveyInfo, recipientPartUID, true);
      string declineUrl = PathHelper.Pages.RaterDecline(surveyInfo.SurveyUID, recipientPartUID, true);
      string adjustedCompanyName = companyName.IsNullOrEmpty() ? ReplaceWithIfCompanyNameBlank : companyName;

      string rtn = emailBodyTemplate.ReplaceTags(
        new Dictionary<string, string>() {
          { MandrillMergeTags.SurveyName, surveyInfo.SurveyName },
          { MandrillMergeTags.WorkshopName, workshopName },
          { MandrillMergeTags.CloseDate_ClosingDate, closeDate.ToString("d MMM yyyy") },
          { MandrillMergeTags.SurveyLink, GetSurveyButton(surveyUrl, "Begin Your Survey") },
          { MandrillMergeTags.DeclineLink, "<a href=\"" + declineUrl + "\">I do not wish to participate</a>" },
          { MandrillMergeTags.FullName, addrRecipient == null ? "" : addrRecipient.GetFullName() },
          { MandrillMergeTags.FirstName, addrRecipient == null ? "" : addrRecipient.FirstName },
          { MandrillMergeTags.LastName, addrRecipient == null ? "" : addrRecipient.LastName },
          { MandrillMergeTags.SelfName_SelfFullName, addrSelf == null ? "your colleague" : addrSelf.GetFullName() },
          { MandrillMergeTags.SelfFirstName, addrSelf == null ? "" : addrSelf.FirstName },
          { MandrillMergeTags.SelfLastName, addrSelf == null ? "" : addrSelf.LastName },
          { MandrillMergeTags.CompanyName, adjustedCompanyName },
          { MandrillMergeTags.CompanyName_s, adjustedCompanyName.AddPosessive() }
        }
      );

      return rtn;
    }

    private static string GetSurveyButton(string url, string text) {
      return $@"
        <div style=""text-align: center;"">
          <a href=""{url.HTMLEncode()}"" style=""{DefaultButtonStyle}"" target=""_blank"">{text.HTMLEncode()}</a>
        </div>";
    }

    public static string GetEmailLinkButton(string linkUrl, string buttonText) {
      return $@"
        <div style=""margin-top: 20px; margin-bottom:15px;"">
          <center>
            <a href=""{linkUrl.HTMLEncode()}"" style=""display:block;width:200px;height: 45px;border-radius:3px;background:#0b1736;color:#fff;text-decoration:none;font-weight:bold;line-height:45px;"" target=""_blank"">
              <span style=""font-size:16px"">
                <span style=""font-family:arial,helvetica neue,helvetica,sans-serif"">
                  <span style=""color:#FFFFFF"">{buttonText.HTMLEncode()}</span>
                </span>
              </span>
            </a>
          </center>
        </div>";
    }

    // This is sent to the self participant when the rater has completed their survey.
    // Sent only once upon rater survey completion, no reminders.
    // There are 2 versions of the content:
    //   a) Self is not yet complete, let them know the rater is complete and link to their survey.
    //   b) Self is complete, let them know the rater is complete and link to the report.
    // Note the wording "your manager" - this is specific to Multiplex at the moment,
    // but later this message should vary depending on whether the rater is actually a manager or not.
    public static bool Send180RaterCompleted(
      DbHelper.Projects.ProjectInfoBrief projectInfoBrief,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
      DbHelper.AlbertSurveys.SurveyInfo selfSurveyInfo,
      DbHelper.Participants.ParticipantInfo raterPartInfo) {

      if (projectInfoBrief == null) throw new NullReferenceException($"{nameof(projectInfoBrief)}");
      if (selfSurveyInfo == null) throw new NullReferenceException($"{nameof(selfSurveyInfo)}");
      if (coacheeInfo == null) throw new NullReferenceException($"{nameof(coacheeInfo)}");
      if (raterPartInfo == null) throw new NullReferenceException($"{nameof(raterPartInfo)}");

      string subjectText = $@"{coacheeInfo.FirstName}, your manager {raterPartInfo.FullName} has now completed the assessment.";

      string bodyHtml = $@"
        Hi {coacheeInfo.FirstName},<br/>
        <br/>
        Your manager, {raterPartInfo.FullName.HTMLEncode()}, has now completed the assessment.<br/>
        <br/>";

      if (selfSurveyInfo.FoundParticipantBrief.CompletedUtc == null) {
        // Self not complete, link to their survey.

        string surveyUrl = PathHelper.Pages.Survey(selfSurveyInfo, selfSurveyInfo.FoundParticipantBrief.PartUniqueId, true);

        bodyHtml += $@"
          To view your report, please complete your assessment as well.<br/>
          <br/>
  `       {GetEmailLinkButton(surveyUrl, "Complete Assessment")}";

      } else {
        // Self complete, link to their report.

        string reportUrl = PathHelper.Reports.CoacheeSurvey(coacheeInfo, selfSurveyInfo, true);

        bodyHtml += $@"
          To view your report, please go to the Able platform.<br/>
          <br/>
  `       {GetEmailLinkButton(reportUrl, "View Report")}";
      }

      var headers = new NameValueCollection {
        { EmailHeaderForSurveyUID, selfSurveyInfo.SurveyUID },
        { EmailHeaderForPartUID, selfSurveyInfo.FoundParticipantBrief.PartUniqueId },
        { EmailHeaderAbleFlag, "true" }
      };

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.ProjectLogo(projectInfoBrief.JobNumber, projectInfoBrief.BrandingOrgGuid, projectInfoBrief.TenantOrgGuid, false, true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, projectInfoBrief.FriendlyProjectTitle },
        { MandrillMergeTags.TitleText, $"{raterPartInfo.FullName} has completed the assessment." },
        { MandrillMergeTags.BodyHTML, bodyHtml }
      };

      bool emailSent = EmailHelper.SendSingleWithMergeTemplate(
        new List<MailAddress>() { new MailAddress(coacheeInfo.EmailAddress, coacheeInfo.GetFullName()) }, null, null,
        null, null,
        projectInfoBrief, true,
        subjectText,
        ConfigHelper.MandrillTemplate_Generic,
        mergePairs,
        out _);

      return emailSent;
    }

    public static bool SendNudgeEmail(
      DbHelper.Projects.ProjectInfoBrief projectInfoBrief,
      DbHelper.AlbertCoacheeComms.NudgeStatus nudgeStatus,
      bool updateSentDate = true) {

      return SendNudgeEmail(projectInfoBrief, nudgeStatus.Coachee, nudgeStatus.Coach, nudgeStatus.NextNudge, updateSentDate);
    }

    public static bool SendNudgeEmail(
      DbHelper.Projects.ProjectInfoBrief projectInfoBrief,
      DbHelper.AlbertCoacheeComms.CoacheeInfo coacheeInfo,
      DbHelper.AlbertCoacheeComms.CoachInfo coachInfo,
      DbHelper.AlbertCoacheeComms.NudgeComms nudgeInfo,
      bool updateSentDate = true
    ) {

      string coacheeFullName = (coacheeInfo.FirstName + " " + coacheeInfo.LastName).Trim();
      string coachFullName = (coachInfo.FirstName + " " + coachInfo.LastName).Trim();
      string nudgeBodyHTML = EmailHelper.EnsureHTMLLineBreaks(nudgeInfo.NudgeContentBodyText);

      // Attach the "Log in to Able" button to the end of the email.
      nudgeBodyHTML += GetEmailLinkButton(PathHelper.WebRoot, "Log in to Able");

      var emailTo = new List<MailAddress>() { new MailAddress(coacheeInfo.EmailAddress, coacheeFullName) };

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.EXTRACSS, " p {  } " },
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.ProjectLogo(coacheeInfo.ProgramJobNumber, coacheeInfo.BrandingOrgGuid, projectInfoBrief.TenantOrgGuid, false, true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, coacheeInfo.FriendlyProjectTitle.ValueIfNullOrEmpty("") },
        { MandrillMergeTags.CoacheeFirstName, coacheeInfo.FirstName },
        { MandrillMergeTags.NudgeTitle, nudgeInfo.NudgeContentTitle},
        { MandrillMergeTags.NudgeBodyText, nudgeBodyHTML },
        { MandrillMergeTags.CoachFirstName, coachInfo.FirstName },
        { MandrillMergeTags.CoachFullName, (coachInfo.FirstName + " " + coachInfo.LastName).Trim() },
        { MandrillMergeTags.SessionsAllocated, coacheeInfo.SessionsAllocated.ToString() },
        { MandrillMergeTags.SessionsCompleted, coacheeInfo.SessionsCompleted.ToString() }
      };

      string subject = coacheeInfo.FirstName + ", " + nudgeInfo.NudgeContentTitle;

      bool emailSent = EmailHelper.SendSingleWithMergeTemplate(
        emailTo, null, null,
        coachInfo.ComputedSenderEmailName, coachInfo.ComputedSenderEmailAddress,
        null, false,
        subject,
        ConfigHelper.MandrillTemplate_Nudge + (ConfigHelper.IsStagingServer ? StagingTemplateSuffix : ""),
        mergePairs,
        out _);

      // Record nudge comms sent.
      if (emailSent && updateSentDate) {
        try {
          DbHelper.AlbertCoacheeComms.AddNudgeComms(coacheeInfo.CoacheeId, nudgeInfo.NudgeContentId);
          DbHelper.EmailHistory.AddEmail(null, coacheeInfo.CoacheeId, coacheeInfo.ProgramJobId, false, "", coachInfo.EmailAddress, coacheeInfo.EmailAddress, subject, null);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(SendNudgeEmail))
            .WithOperationContext("RecordComms")
            .WithProperty(ApplicationInsightsConstants.CoacheeId, (object)coacheeInfo?.CoacheeId)
            .WithProperty(ApplicationInsightsConstants.NudgeContentId, (object)nudgeInfo?.NudgeContentId)
            .WithProperty(ApplicationInsightsConstants.ProgramJobId, (object)coacheeInfo?.ProgramJobId)
            .Track();
          // ignore
        }
      }

      return emailSent;
    }

    // ***
    // *** Send Content Email
    // ***

    public static string GetCustomEmailContentItemHtml(
      DbHelper.Content.ContentInfo contentInfo) {

      if (contentInfo == null) {
        return string.Empty;
      }

      return GetEmailContentItemHtml(contentInfo) + GetEmailLinkButton(PathHelper.Pages.ContentDetails(contentInfo.ContentGuid, true, PathHelper.PageViewAsRole.Participant), "Read More");
    }

    private static string GetEmailContentItemHtml(DbHelper.Content.ContentInfo contentInfo) {
      return $@"
        <div style=""background-color: #F5F8FA; border-radius: 28px; padding: 24px; margin-top: 15px; margin-bottom: 10px; "">
          <table cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
            <tr>
              <td>
                <div style=""font-weight: 700; color: #1a1b25; font-size: 17px; margin-bottom: 5px; line-height: 1.2;"">
                  {contentInfo.ContentTitle.HTMLEncode()}
                </div>
                <div style=""font-weight: 500; color: #1a1b25; font-size: 17px; margin-bottom: 5px; line-height: 1.2;"">
                  {contentInfo.ContentSummary.HTMLEncode()}
                </div>
                <div style=""font-weight: 500; color: #1a1b25; font-size: .85rem; margin-bottom: 5px; margin-top: 5px;"">
                  <b>Type: </b> {contentInfo.ContentTypeName.HTMLEncode()}
                </div>
              </td>
            </tr>
          </table>
        </div>";
    }

    public static bool SendScheduledProgramContent(
      DbHelper.Content.ScheduledContentInfo scheduledContentInfo,
      bool updateEmailHistory) {

      if (scheduledContentInfo == null || scheduledContentInfo.CoacheeId == null || scheduledContentInfo.ContentInfo == null) {
        return false;
      }

      string coacheeFullName = (scheduledContentInfo.CoacheeFirstName + " " + scheduledContentInfo.CoacheeLastName).Trim();
      string moduleBodyHTML = $@"
        <p style=""margin-bottom:20px;"">Here's a microlearning post which aligns with your current leadership development program.</p>
        {GetEmailContentItemHtml(scheduledContentInfo.ContentInfo)}";

      string coachSignatureHtml = $@"
        <p style=""margin-top:20px;"">Hope you enjoy it, <br/>{ConfigHelper.AbleCoachInfo.FirstName + " " + ConfigHelper.AbleCoachInfo.LastName}.</p>";

      var emailTo = new List<MailAddress>() { new MailAddress(scheduledContentInfo.CoacheeEmail, coacheeFullName) };

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.EXTRACSS, " p {  } " },
        { MandrillMergeTags.ProjectLogoURL, "" },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, scheduledContentInfo.ContentInfo.ContentTitle },
        { MandrillMergeTags.CoacheeFirstName, scheduledContentInfo.CoacheeFirstName },
        { MandrillMergeTags.ModuleSubject, scheduledContentInfo.ContentInfo.ContentTitle },
        { MandrillMergeTags.ModuleBodyText, moduleBodyHTML },
        { MandrillMergeTags.ModuleURL, PathHelper.Pages.ContentDetails(scheduledContentInfo.ContentInfo.ContentGuid, true) },
        { MandrillMergeTags.CoachFullName, coachSignatureHtml }
      };

      bool emailSent = EmailHelper.SendSingleWithMergeTemplate(
        emailTo, null, null,
        ConfigHelper.AbleCoachInfo.FirstName + " " + ConfigHelper.AbleCoachInfo.LastName, ConfigHelper.AbleCoachInfo.Email,
        null, false,
        scheduledContentInfo.ContentInfo.ContentTitle,
        ConfigHelper.MandrillTemplate_Module + (ConfigHelper.IsStagingServer ? StagingTemplateSuffix : ""),
        mergePairs,
        out _);

      // Record module comms sent.
      if (emailSent && updateEmailHistory) {
        try {
          DbHelper.Content.UpdateScheduledContentSent(scheduledContentInfo.ProgramContentId);
          DbHelper.EmailHistory.AddEmail(null, scheduledContentInfo.CoacheeId.Value, scheduledContentInfo.ProgramJobId, false, "", ConfigHelper.AbleCoachInfo.Email, scheduledContentInfo.CoacheeEmail, scheduledContentInfo.ContentInfo.ContentTitle, null);

        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(SendScheduledProgramContent))
            .WithOperationContext("RecordComms")
            .WithProperty(ApplicationInsightsConstants.ProgramContentId, (object)scheduledContentInfo?.ProgramContentId)
            .WithProperty(ApplicationInsightsConstants.CoacheeId, (object)scheduledContentInfo?.CoacheeId)
            .WithProperty(ApplicationInsightsConstants.ProgramJobId, (object)scheduledContentInfo?.ProgramJobId)
            .WithProperty(ApplicationInsightsConstants.ContentTitle, scheduledContentInfo?.ContentInfo?.ContentTitle)
            .Track();
          // ignore
        }
      }

      return emailSent;
    }

    // ***
    // *** Share Survey Email
    // ***

    public static bool SendShareSurveyEmail(
      DbHelper.AbleUser.AbleUserInfo userInfoSharedBy,
      DbHelper.AbleUser.AbleUserBasicInfo userInfoSharedWith,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      string customMsg = "") {

      if (userInfoSharedBy == null || userInfoSharedWith == null || surveyInfo == null) {
        return false;
      }

      string registrationHtml = "";
      if (!userInfoSharedWith.IsRegistered) {
        registrationHtml = " An account has been created for you, you just have to complete registration by clicking in the link below, it only takes a minute.";
      }

      string subject = "A survey has been shared with you on able!";
      string customMgHtml = "";
      if (!customMsg.IsNullOrEmpty()) {
        customMgHtml = $@"<p style=""font-style: italic;"">{customMsg}</p>";
      }

      string pathLink = ""; // Depends on the survey type
      if (surveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) {
        pathLink = PathHelper.Pages.DevelopmentPlan(true);
      } else {
        pathLink = PathHelper.Pages.ParticipantSurveys(PathHelper.ParticipantSurveysTabEnum.SharedWithUser, true);
      }

      // Check if the computed email that will be the sender is the the email address of the user sharing the survey.
      bool isComputedSenderUserEmail = userInfoSharedBy.EmailAddress == userInfoSharedBy.ComputedSenderEmailAddress;

      string bodyHtml = $@"
        Hi {userInfoSharedWith.FirstName},
        <br/><br/>
        {(isComputedSenderUserEmail ? $"I have shared my survey " : $"{userInfoSharedBy.GetFullName()} has shared the survey ")} <b>'{surveyInfo.SurveyName}'</b> with you.
        {customMgHtml.EnsureStartsWith("<br/><br/>", true)}
        <br/><br/>
        To view it in detail, please log into able.{registrationHtml}
        {GetEmailLinkButton(pathLink, "See on Able")}";

      return SendGenericEmail(
        null,
        subject, "", bodyHtml,
        false,
        userInfoSharedBy.ComputedSenderEmailName, userInfoSharedBy.ComputedSenderEmailAddress,
        null, out _,
        new System.Net.Mail.MailAddress(userInfoSharedWith.EmailAddress, userInfoSharedWith.GetFullName())
      );
    }


    // ***
    // *** Participant Welcome Email
    // ***

    public class ParticipantWelcome {

      private static class ProgramTypes {
        public const string MixedProgram = "mixed";
        public const string CoachingProgram = "coaching";
        public const string WorkshopProgram = "workshop";
        public const string AllPaxOrHasAI = "AllPaxOrHasAI";
      }

      private static class ScheduledSurveyTypes {
        public const string ILP180 = "ILP180";
        public const string ILP360 = "ILP360";
        public const string LMP = "LMP";
        public const string LDP = "LDP";
        public const string EIPSL = "EIPSL";
        public const string None = "None";
      }

      private class MergeKeys {
        public const string SessionsAllocated = "sessionsAllocated";
        public const string WorkshopsAllocated = "workshopsAllocated";
        public const string SessionPluralChar = "sessionPluralChar";
        public const string WorkshopPluralChar = "workshopPluralChar";
        public const string ContinueOrEndOfLine = "continueOrEndOfLine";
        public const string WorkshopsList = "workshopsList";
        public const string CadenceSummary = "cadenceSummary";
        public const string ScheduledSurveyType = "scheduledSurveyType";
        public const string ScheduledSurveyTitle = "scheduledSurveyTitle";
        public const string FirstName = "firstName";
        public const string ProgramName = "programName";
        public const string ProgramDescription = "programDescription";
        public const string ProgramExplanationParagraph = "programExplanationParagraph";
        public const string IntakeSurveyLink = "intakeSurveyLink";
        public const string AblePlatformBtnUrl = "ablePlatformBtnUrl";
        public const string AblePlatformBtnUrlText = "ablePlatformUrlText";
        public const string ProgramDetails = "programDetails";
        public const string SpecificProgramType = "specificProgramType";
        public const string ProgramDetailList = "programDetailList";
        public const string ScheduledSurveyTypeInfo = "scheduledSurveyTypeInfo";
        public const string SurveyTypeInfo = "surveyTypeInfo";
        public const string DefaultWelcomeLine = "defaultWelcomeLine";
      }

      public enum SetSendDates { No, Yes }

      public static bool Send(
        DbHelper.Projects.ProjectInfoBrief projectInfoBrief,
        DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
        DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo,
        DbHelper.Projects.ProjectInfo projectInfo,
        out EmailHelper.MandrillSentResult sendResult,
        SetSendDates setSendDates,
        bool isTest = false) {

        DbHelper.AlbertSurveys.SurveyInfo intakeSurveyInfo = null;
        if (coacheeInfo == null) throw new ArgumentException("coacheeInfo is null");
        if (projectInfo == null) throw new ArgumentException("projectInfo is null");

        sendResult = new EmailHelper.MandrillSentResult(false);

        // Create email subject.
        string emailSubject = $"{coacheeInfo.FirstName}, your {coacheeInfo.FriendlyProjectTitle} program starts today";

        if (!projectInfo.IntakeSurveyDisabled && !isTest) {
          if (coacheeInfo.HasCoaching || coacheeInfo.SendIntakeSurveyToAllParticipants || coacheeInfo.HasWorkshops || coacheeInfo.UserHasAICoach) {
            // Create onboarding survey.
            try {
              // Get the latest intake survey for this coachee if existing.
              intakeSurveyInfo = DbHelper.AlbertSurveys.GetLatestIntakeSurveyForUserId(coacheeInfo.UserId);
              if (intakeSurveyInfo == null) {
                // Create a new intake survey for this coachee.
                intakeSurveyInfo = DbHelper.AlbertSurveys.CreateCoacheeIntakeSurvey(coacheeInfo, projectInfo);
              }
            } catch (Exception ex) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(ex)
                .WithOperation(nameof(Send))
                .WithOperationContext("CreateIntakeSurvey")
                .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
                .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo?.UserGuid))
                .WithProperty(ApplicationInsightsConstants.ProjectId, (object)projectInfo?.ProjectId)
                .Track();
            }
          }
        }

        if (!EmailHelper.IsEmailAllowed()) {
          // Don't email, pretend successful, skip creation of onboarding survey.
          if (setSendDates == SetSendDates.Yes) SetWelcomeEmailDatesAndHistory(coacheeInfo, intakeSurveyInfo, emailSubject);
          return true;
        }

        var emailTo = new List<MailAddress>() { new MailAddress(coacheeInfo.EmailAddress, coacheeInfo.GetFullName()) };
        var firstSessionType = coacheeInfo.GetFirstSessionType();
        int firstSessionDurationMins = firstSessionType == null ? 0 : firstSessionType.DurationMins;

        string programType = "";
        if (coacheeInfo.HasCoaching && coacheeInfo.HasWorkshops) {
          programType = ProgramTypes.MixedProgram;
        } else if (coacheeInfo.HasCoaching) {
          programType = ProgramTypes.CoachingProgram;
        } else if (coacheeInfo.HasWorkshops) {
          programType = ProgramTypes.WorkshopProgram;
        } else if (projectInfo.SendIntakeSurveyToAllParticipants || coacheeInfo.UserHasAICoach) {
          programType = ProgramTypes.AllPaxOrHasAI;
        } else {
          return false;
        }

        string workshopsList = "";
        if (coacheeInfo.HasWorkshops) {
          workshopsList += "<ul>";
          try {
            var workshops = DbHelper.WorkshopEvents.GetWorkshopsInProgram((int)coacheeInfo.ProgramJobId);
            foreach (var ws in workshops) workshopsList += "<li>" + ws.WorkshopTitle.HTMLEncode() + "</li>";
          } catch {
            // ignore for now
          }
          workshopsList += "</ul>";
        }

        // Get first upcoming scheduled survey (excluding evals), if any.
        string upcomingSurveyType = "None";
        string upcomingSurveyTitle = "";
        bool hasSurvey = false;
        if (!isTest) {
          try {
            var upcomingSurvey = DbHelper.AlbertSurveys.GetFirstUpcomingNonEvalSurveyOrNull(coacheeInfo.CoacheeId);
            if (upcomingSurvey != null) {
              var reportType = DbHelper.ReportTypes.GetReportTypeByDbValue(upcomingSurvey.ReportType);
              upcomingSurveyType = !upcomingSurvey.ReportSubType.IsNullOrEmpty() ? upcomingSurvey.ReportSubType : reportType.ReportTypeName;
              upcomingSurveyTitle = upcomingSurvey.SurveyTitle;
              hasSurvey = true;
            }
          } catch {
            // ignore for now
          }
        }

        var mergePairs = new Dictionary<string, string>() {
          { MandrillMergeTags.SUBJECT, emailSubject },
          { MandrillMergeTags.EXTRACSS, " p {  } " },
          { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.ProjectLogo(projectInfo.JobNumber, projectInfo.BrandingOrgGuid, projectInfoBrief.TenantOrgGuid, false, true) },
          { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
          { MandrillMergeTags.FriendlyProjectTitle, projectInfo.FriendlyProjectTitle.ValueIfNullOrEmpty(FallbackWelcomeEmailTitle) },
          { MandrillMergeTags.HasCustomText, projectInfo.WelcomeEmailCustomHTML.IsNullOrEmpty() ? "" : "True" },
          { MandrillMergeTags.CustomText, projectInfo.WelcomeEmailCustomHTML.EmptyIfNull() },
          { MandrillMergeTags.ProgramName, coacheeInfo.ProgramName },
          { MandrillMergeTags.ProgramType, programType },
          { MandrillMergeTags.CoacheeFirstName, coacheeInfo.FirstName },
          { MandrillMergeTags.CoordinatorFullName, ConfigHelper.Email_Welcome_From_Name },
          { MandrillMergeTags.HasCoaching, coacheeInfo.HasCoaching ? "yes" : "" },
          { MandrillMergeTags.SessionsAllocated, coacheeInfo.UserActivity?.SessionsAllocated.ToString() },
          { MandrillMergeTags.SessionDuration, firstSessionDurationMins.ToString() },
          { MandrillMergeTags.CadenceSummary, GetSessionDurations(coacheeInfo) },
          { MandrillMergeTags.HasWorkshops, coacheeInfo.HasWorkshops ? "yes" : "" },
          { MandrillMergeTags.TotalWorkshops, coacheeInfo.UserActivity?.WorkshopsAllocated.ToString() },
          { MandrillMergeTags.WorkshopsList, workshopsList },
          { MandrillMergeTags.AblePlatformButton, GetAblePlatformButton(coacheeInfo) },
          { MandrillMergeTags.HasSurvey, hasSurvey ? "yes" : "" },
          { MandrillMergeTags.HasSurveyYesNo, hasSurvey ? "Yes" : "No" },
          { MandrillMergeTags.ScheduledSurveyType, upcomingSurveyType.ToUpper() },
          { MandrillMergeTags.ScheduledSurveyTitle, upcomingSurveyTitle }
        };

        if (coachInfo != null) {
          mergePairs.Add(MandrillMergeTags.CoachFirstName, coachInfo.FirstName);
          mergePairs.Add(MandrillMergeTags.CoachFullName, coachInfo.GetFullName());
        }

        if (projectInfo.WelcomeEmailCustomHTML.EmptyIfNull().Contains(MailChimpMergeTagStart)) {
          // Apply above merge fields to custom content.
          mergePairs[MandrillMergeTags.CustomText] = projectInfo.WelcomeEmailCustomHTML.ReplaceTags(MailChimpMergeTagStart, MailChimpMergeTagEnd, mergePairs);
        }

        bool emailSent = SendGenericEmail(projectInfo, emailSubject,
          projectInfo.FriendlyProjectTitle.ValueIfNullOrEmpty(FallbackWelcomeEmailTitle),
          GetEmailBodyHtml(mergePairs, projectInfo),
          true,
          null, null, null,
          out sendResult,
          emailTo.ToArray());

        if (emailSent && coacheeInfo.ProgramStatusId == DbHelper.CoacheeProgramStatus.GetStatus_WaitingLaunch().ProgramStatusId) {
          DbHelper.AlbertCoachees.UpdateCoacheeStatus(null, coacheeInfo, DbHelper.CoacheeProgramStatus.GetStatus_Onboarding());
        }

        if (sendResult.Sent && setSendDates == SetSendDates.Yes) {
          SetWelcomeEmailDatesAndHistory(coacheeInfo, intakeSurveyInfo, emailSubject);
          if (coacheeInfo.UserId != null) DbHelper.AbleUser.UpdateLastInviteReminderSentByUserId(coacheeInfo.UserId.Value, DateTime.UtcNow);
        }

        return emailSent;
      }

      private static string GetEmailBodyHtml(Dictionary<string, string> mergePairs, DbHelper.Projects.ProjectInfo projectInfo) {

        string programType = mergePairs[MandrillMergeTags.ProgramType];
        string programName = mergePairs[MandrillMergeTags.ProgramName];
        int sessionsAllocated = mergePairs[MandrillMergeTags.SessionsAllocated].ToIntOrDefault(0);
        int workshopsAllocated = mergePairs[MandrillMergeTags.TotalWorkshops].ToIntOrDefault(0);
        string ablePlatformUrl = mergePairs[MandrillMergeTags.AblePlatformButton];
        string coacheeFirstName = mergePairs[MandrillMergeTags.CoacheeFirstName];
        string workshopsList = mergePairs[MandrillMergeTags.WorkshopsList];
        string cadenceSummary = mergePairs[MandrillMergeTags.CadenceSummary];
        string scheduledSurveyType = mergePairs[MandrillMergeTags.ScheduledSurveyType];
        string scheduledSurveyTitle = mergePairs[MandrillMergeTags.ScheduledSurveyTitle];

        string specificProgramType = programType == ProgramTypes.CoachingProgram ? programType : string.Empty;
        string programDescription = "";
        string programExplanationParagraph = HtmlParts.ProgramExplanationFirstLine;
        bool hasCustomDescription = !mergePairs[MandrillMergeTags.CustomText].IsNullOrEmpty();

        // Define and add to dictionary the base stablished keys.
        var mergeValues = new Dictionary<string, string>() {
          { MergeKeys.SessionsAllocated, sessionsAllocated.ToString() },
          { MergeKeys.WorkshopsAllocated, workshopsAllocated.ToString() },
          { MergeKeys.SessionPluralChar, sessionsAllocated != 1 ? "s" : string.Empty },
          { MergeKeys.WorkshopPluralChar, workshopsAllocated != 1 ? "s" : string.Empty },
          { MergeKeys.ContinueOrEndOfLine, programType == ProgramTypes.MixedProgram ? " and " : "" },
          { MergeKeys.WorkshopsList, workshopsList },
          { MergeKeys.CadenceSummary, cadenceSummary },
          { MergeKeys.ScheduledSurveyType, scheduledSurveyType },
          { MergeKeys.ScheduledSurveyTitle, scheduledSurveyTitle },
          { MergeKeys.ProgramName, programName },
          { MergeKeys.FirstName, coacheeFirstName },
          { MergeKeys.SpecificProgramType, specificProgramType },
          { MergeKeys.ProgramDescription, programDescription },
          { MergeKeys.AblePlatformBtnUrl, ablePlatformUrl },
          { MergeKeys.ProgramDetails, "" }
        };

        // Depending on the program type, the text is obtained from constants and tags are replaced. Dictionary needs to constantly be updated.
        // Sometimes a secondary tag value needs to be obtained before the main as tags need to be replaced when consulting.
        if (programType == ProgramTypes.CoachingProgram) {

          if (!hasCustomDescription) {
            programDescription = HtmlParts.CoachingProgramDescriptionHtml(mergeValues);
            programExplanationParagraph += HtmlParts.CoachingProgramExplanationHtml(mergeValues);
          }

          mergeValues.AddOrUpdate(MergeKeys.ProgramDetailList,
            GetScheduledSurveyTypeHtml(scheduledSurveyType, mergeValues) +
            HtmlParts.ProgramDetailListInfo_Coaching);

        } else if (programType == ProgramTypes.WorkshopProgram) {

          if (!hasCustomDescription) {
            programDescription = HtmlParts.WorkshopProgramDescriptionHtml(mergeValues);
            programExplanationParagraph += HtmlParts.WorkshopProgramExplanationHtml(mergeValues);
          }

          mergeValues.AddOrUpdate(MergeKeys.ProgramDetailList,
            HtmlParts.ProgramDetailList_Workshop(mergeValues) +
            HtmlParts.ProgramDetailListInfo_Workshop);

        } else if (programType == ProgramTypes.MixedProgram) {

          if (!hasCustomDescription) {
            programDescription = HtmlParts.CoachingProgramDescriptionHtml(mergeValues)
            + HtmlParts.WorkshopProgramDescriptionHtml(mergeValues);
            programExplanationParagraph += HtmlParts.CoachingProgramExplanationHtml(mergeValues)
              + HtmlParts.WorkshopProgramExplanationHtml(mergeValues);
          }

          mergeValues.AddOrUpdate(MergeKeys.ProgramDetailList,
            HtmlParts.ProgramDetailList_Workshop(mergeValues) + GetScheduledSurveyTypeHtml(scheduledSurveyType, mergeValues) + HtmlParts.ProgramDetailListInfo_Mixed);

        } else if (!hasCustomDescription) {

          // If Program Type is AllParticipants, is because of having the SendIntakeForAllParticipants flag.
          programDescription = string.Empty;
          programExplanationParagraph += HtmlParts.CoachingProgramExplanationHtml(mergeValues) + HtmlParts.WorkshopProgramExplanationHtml(mergeValues);
          mergeValues.AddOrUpdate(MergeKeys.ProgramDetailList, HtmlParts.ProgramDetailListInfo_Workshop + HtmlParts.ProgramDetailListInfo_Mixed);

        }

        // Add all program data obtained to dictionary.

        if (hasCustomDescription) {

          // If MergeTags contains a CustomText just use that as DefaultWelcomeLine and empty explanation paragraph.
          mergeValues.AddOrUpdate(MergeKeys.DefaultWelcomeLine, "<div>" + mergePairs[MandrillMergeTags.CustomText] + "</div>");
          mergeValues.AddOrUpdate(MergeKeys.ProgramExplanationParagraph, string.Empty);

        } else {

          // Use DefaultWelcomeLine key and replace tags if no custom text is set.
          mergeValues.AddOrUpdate(MergeKeys.ProgramDescription, programDescription);
          mergeValues.AddOrUpdate(MergeKeys.ProgramExplanationParagraph, programExplanationParagraph);
          mergeValues.AddOrUpdate(MergeKeys.DefaultWelcomeLine, HtmlParts.DefaultWelcomeLine(mergeValues));

        }

        // Get Program Details based on Detail Lists obtained above. Adding it here to not repeat in each sentence.
        mergeValues.AddOrUpdate(MergeKeys.ProgramDetails, projectInfo.WelcomeEmail_ProgramSummaryDisabled ? string.Empty : HtmlParts.ProgramDetails(mergeValues));

        // Generate the Intake Survey Url Html if it is not empty.
        //mergeValues.AddOrUpdate(MergeKeys.IntakeSurveyLink, GetIntakeSurveyLinkHtml(intakeSurveyUrl)); // Add Empty if html is not generated so tag is replaced.

        return HtmlParts.DefaultWelcomeEmailBodyHtml(mergeValues);
      }

      private static string GetAblePlatformButton(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

        DbHelper.AbleUser.AbleUserBasicInfo coacheeUserInfo = null;

        coacheeUserInfo = DbHelper.AbleUser.GetUserByEmailOrNull(coacheeInfo.EmailAddress, DbHelper.AbleUser.RegisteredFilter.Any);
        int invitedByUserId = DbHelper.AbleUser.GetInvitedByUserId(coacheeUserInfo);

        string platformUrl = "", platformUrlText = "";

        if (coacheeUserInfo.IsRegistered) {
          // If coachee's user is registered, send to overview page.
          platformUrl = PathHelper.Pages.ParticipantUpcoming(true);
          platformUrlText = "Start Development";
        } else {
          // If coachee's user is not registered, send registry invite.
          // If Invite Code is empty, create one and update invite details.
          if (coacheeUserInfo.InviteCode.IsNullOrEmpty() || coacheeUserInfo.InvitedByUserId != invitedByUserId) {
            DbHelper.AbleUser.UpdateInviteDetails(coacheeUserInfo, invitedByUserId);
          }
          platformUrl = PathHelper.Pages.RegisterInvited(coacheeUserInfo.InviteCode, true);
          platformUrlText = "Complete Registration";
        }

        return HtmlParts.AblePlatformButton(new Dictionary<string, string>() {
          { MergeKeys.AblePlatformBtnUrl, platformUrl },
          { MergeKeys.AblePlatformBtnUrlText, platformUrlText }
        });
      }

      private static string GetScheduledSurveyTypeHtml(string scheduledSurveyType, Dictionary<string, string> mergeValues) {

        if (scheduledSurveyType.ToLower() == ScheduledSurveyTypes.None.ToLower()) {

          mergeValues.AddOrUpdate(MergeKeys.ScheduledSurveyTypeInfo, HtmlParts.ScheduledSurveyTypeInfo_None);

        } else {

          if (scheduledSurveyType.ToLower() == ScheduledSurveyTypes.ILP180.ToLower()) {
            mergeValues.AddOrUpdate(MergeKeys.SurveyTypeInfo, HtmlParts.SurveyTypeInfo_ILP180);
          } else if (scheduledSurveyType.ToLower() == ScheduledSurveyTypes.EIPSL.ToLower()) {
            mergeValues.AddOrUpdate(MergeKeys.SurveyTypeInfo, HtmlParts.SurveyTypeInfo_EIPSL);
          } else {
            mergeValues.AddOrUpdate(MergeKeys.SurveyTypeInfo, string.Empty);
          }

          mergeValues.AddOrUpdate(MergeKeys.ScheduledSurveyTypeInfo, HtmlParts.ScheduledSurveyTypeInfo_Default(mergeValues));
        }

        return HtmlParts.ProgramDetailList_Coaching(mergeValues);
      }

      private static void SetWelcomeEmailDatesAndHistory(
        DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
        DbHelper.AlbertSurveys.SurveyInfo welcomeSurveyInfo,
        string emailSuject) {

        // Set relevant dates.
        try {
          DbHelper.AlbertCoachees.SetWelcomeSent(coacheeInfo.CoacheeId, DateTime.UtcNow);
          if (welcomeSurveyInfo != null) {
            DbHelper.Participants.UpdateFirstInvitationSent(welcomeSurveyInfo.FoundParticipantBrief.PartId, DateTime.UtcNow);
          }
          DbHelper.EmailHistory.AddEmail(null,
            coacheeInfo.CoacheeId, (int)coacheeInfo.ProgramJobId, false, "",
            ConfigHelper.Email_Welcome_From_Address, coacheeInfo.EmailAddress, emailSuject, null);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(SetWelcomeEmailDatesAndHistory))
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo?.UserGuid))
            .WithProperty(ApplicationInsightsConstants.ProgramJobId, (object)coacheeInfo?.ProgramJobId)
            .WithProperty(ApplicationInsightsConstants.ParticipantId, (object)welcomeSurveyInfo?.FoundParticipantBrief?.PartId)
            .Track();
          // ignore for now
        }
      }

      private class HtmlParts {

        internal static string DefaultWelcomeEmailBodyHtml(Dictionary<string, string> mergePairs) {
          return $@"
          <div>
            <p>
              Hi {mergePairs[MergeKeys.FirstName]},
            </p>
            <p>
              {mergePairs[MergeKeys.DefaultWelcomeLine]}
            <p>
            <p class=""mt15"">
              {mergePairs[MergeKeys.ProgramExplanationParagraph]}
            </p>
            {mergePairs[MergeKeys.AblePlatformBtnUrl]}
            {mergePairs[MergeKeys.ProgramDetails]}
          </div>";
        }

        internal static string DefaultWelcomeLine(Dictionary<string, string> mergePairs) {
          return $@"
          Welcome to your {mergePairs[MergeKeys.SpecificProgramType]} program, <strong>{mergePairs[MergeKeys.ProgramName]}</strong>, which includes {mergePairs[MergeKeys.ProgramDescription]}.";
        }

        internal static string CoachingProgramDescriptionHtml(Dictionary<string, string> mergePairs) {
          return $@"<strong>{mergePairs[MergeKeys.SessionsAllocated]}</strong> individual coaching session{mergePairs[MergeKeys.SessionPluralChar]}{mergePairs[MergeKeys.ContinueOrEndOfLine]}";
        }

        internal static string WorkshopProgramDescriptionHtml(Dictionary<string, string> mergePairs) {
          return $@"
          <strong>{mergePairs[MergeKeys.WorkshopsAllocated]}</strong> workshop{mergePairs[MergeKeys.WorkshopPluralChar]} or group session{mergePairs[MergeKeys.WorkshopPluralChar]}";
        }

        internal static readonly string ProgramExplanationFirstLine = "We're eager to get you started ";

        internal static string CoachingProgramExplanationHtml(Dictionary<string, string> mergePairs) {
          return $@"
          with your individual coaching, but first, we would like you to fill out
          a form to better understand your professional goals. Your answers will help us and your coach to tailor
          the session{mergePairs[MergeKeys.SessionPluralChar]} to your needs. In a couple of days, you will be introduced to your coach.";
        }

        internal static string WorkshopProgramExplanationHtml(Dictionary<string, string> mergePairs) {
          return $@"
          <br/>
          We'll soon be in touch again if your program includes pre-work, such as reading material or surveys.";
        }

        internal static string AblePlatformButton(Dictionary<string, string> mergePairs) {
          return $@"
          <div class=""mt20"">
            <center>
              <a href=""{mergePairs[MergeKeys.AblePlatformBtnUrl]}"" style=""display:block;width:200px;height: 45px;border-radius:3px;background:#0b1736;color:#fff;text-decoration:none;font-weight:bold;line-height:45px;"" target=""_blank"">
                <span style=""font-size:16px"">
                  <span style=""font-family:arial,helvetica neue,helvetica,sans-serif"">
                    <span style=""color:#FFFFFF"">{mergePairs[MergeKeys.AblePlatformBtnUrlText]}</span>
                  </span>
                </span>
              </a>
            </center>
          </div>";
        }

        internal static string ProgramDetails(Dictionary<string, string> mergePairs) {
          return $@"
          <br/>
          <hr/>
          <div style=""margin-top: 20px;"">
            <h2 class=""mc-toc-title"">
              <span style=""color:#424242"">
                <span style=""font-size:22px"">
                  <span style=""font-family:lato,helvetica neue,helvetica,arial,sans-serif"">
                    Your Program: {mergePairs[MergeKeys.ProgramName]}
                  </span>
                </span>
              </span>
            </h2>
            <ul>
              {mergePairs[MergeKeys.ProgramDetailList]}
            </ul>
          </div>";
        }

        internal static string ProgramDetailList_Workshop(Dictionary<string, string> mergePairs) {
          return $@"
          <li>
            <b>{mergePairs[MergeKeys.WorkshopsAllocated]} Workshop{mergePairs[MergeKeys.WorkshopPluralChar]} or Group Session{mergePairs[MergeKeys.WorkshopPluralChar]}:</b><br />
            These are:<br />
            <b>{mergePairs[MergeKeys.WorkshopsList]}</b>
          </li>";
        }

        internal static string ProgramDetailList_Coaching(Dictionary<string, string> mergePairs) {
          return $@"
          <li>
            <b>{mergePairs[MergeKeys.SessionsAllocated]} Individual Coaching Session{mergePairs[MergeKeys.SessionPluralChar]}:</b>
            Online or face-to-face, depending on your program. Ideally once a month.
            <ul>
              <li><b>Session Duration:</b> {mergePairs[MergeKeys.CadenceSummary]}</li>
            </ul>
          </li>
          <li>
            <b>Dedicated Coach:</b> You'll be paired with a coach who best suits your goals and needs.
          </li>
          {mergePairs[MergeKeys.ScheduledSurveyTypeInfo]}";
        }

        internal static string ProgramDetailListInfo_Workshop = @"
        <li>
          <b>Learning Material:</b> After each coaching session, your coach
          might assign you learning material aligned with your goals.
        </li>
        <li>
          <b>Evaluations:</b> Throughout the program we may send you
          evaluation surveys to complete, which takes 2-5 minutes.
        </li>";

        internal static readonly string ProgramDetailListInfo_Coaching = @"
        <li>
          <b>Development Plan:</b> Your coach will work on your plan together
          with you, focussing on the key areas you wish to improve or strengthen.
        </li>
        <li>
          <b>Message-Based Coaching:</b> Reply to your coach anytime.
        </li>"
          + ProgramDetailListInfo_Workshop;

        internal static readonly string ProgramDetailListInfo_Mixed = ProgramDetailListInfo_Coaching;

        internal const string ScheduledSurveyTypeInfo_None = @"
        <li>
          <b>Survey:</b> This time, your program does not include a survey.
        </li>";

        internal static string ScheduledSurveyTypeInfo_Default(Dictionary<string, string> mergePairs) {
          return $@"
          <li>
            <b>Survey:</b> During this program you will complete the <b>{mergePairs[MergeKeys.ScheduledSurveyTitle]}</b>.
            This survey measures the major roles, skills and personal attributes
            associated with effective leadership and management behaviour.<br />
            {mergePairs[MergeKeys.SurveyTypeInfo]}<br />
            You will receive a separate email with a survey invite.
          </li>";
        }

        internal const string SurveyTypeInfo_ILP180 = @"
        This information, when collated with all other respondents on the program, is used by Integral to show
        the impact this leadership program has had on the leadership behaviour of all participants. You will
        receive a separate email with a survey invite.";

        internal const string SurveyTypeInfo_EIPSL = @"
        It provides you with feedback about how others perceive your school leadership skills based on the Integral
        School Empowerment Model and the Professional Practices for School Leaders identified in the AITSL framework.";
      }
    }

    // ***
    // *** Meet Coach Email
    // ***

    public static bool SendMeetCoachEmail(
      SqlTransaction trans,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
      DbHelper.Projects.ProjectInfo projectInfo,
      DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo,
      bool useStagingTemplate = false,
      bool setSendDates = true) {

      if (coacheeInfo == null) throw new ArgumentException("coacheeInfo is null");
      if (coachInfo == null) throw new ArgumentException("coachInfo is null");
      if (!coacheeInfo.HasCoaching) throw new ArgumentException("HasCoaching is false");

      if (!EmailHelper.IsEmailAllowed()) {
        // Don't email, pretend successful.
        if (setSendDates) SetMeetCoachEmailDatesAndHistory(trans, coacheeInfo);
        return true;
      }

      var emailTo = new List<MailAddress>() { new MailAddress(coacheeInfo.EmailAddress, coacheeInfo.GetFullName()) };
      var firstSessionType = coacheeInfo.GetFirstSessionType();
      int firstSessionDurationMins = firstSessionType == null ? 0 : firstSessionType.DurationMins;
      string customText = projectInfo.MeetCoachEmailCustomHTML.EmptyIfNull().EnsureStartsWith("<br/><br/>", true);

      string htmlAboveBookButton = $@"
        Hi {coacheeInfo.FirstName},<br/><br/>
        We'd like to introduce you to your coach <b>{coachInfo.GetFullName()}</b><br/><br/>
        <div style=""width:100% margin-bottom:20px; margin-top: 20px;"">
          <center>
            <a title=""View on Able"" href=""{PathHelper.Pages.ParticipantCoaching(coacheeInfo.CoacheeUID, coacheeInfo.UserId, true)}"">
              <img style=""border-radius: 50%; height: 180px; width: 180px;"" src=""{PathHelper.Images.UserPhoto(coachInfo, PathHelper.Images.UserPhotoSize.Large, placeholderIfNotFound: true, includeDomain: true)}"" alt=""View on Able"">
            </a>
          </center>
        </div>
        {customText}
        <br/><br/>";

      string htmlBelowBookButton = $@"
        <b>About Your Coach</b><br/>
        {coachInfo.BioShort}<br/><br/>
        If you don't think your coach is a good match after your first session, you can change at any time. Just contact our <a href=""mailto:coordination@helloable.co?subject=Support%3A%20"" target=""_blank"">coordination team</a> and we will help you.";

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.SUBJECT, ConfigHelper.MandrillTemplate_MeetYourCoach_Subject },
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.ProjectLogo(projectInfo, false, true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, projectInfo.FriendlyProjectTitle.ValueIfNullOrEmpty("Meet Your Coach: " + coachInfo.GetFullName()) },
        { MandrillMergeTags.HasCustomText, projectInfo.MeetCoachEmailCustomHTML.IsNullOrEmpty() ? "" : "True" },
        { MandrillMergeTags.CustomText, projectInfo.MeetCoachEmailCustomHTML.EmptyIfNull() },
        { MandrillMergeTags.ProgramName, coacheeInfo.ProgramName },
        { MandrillMergeTags.CoacheeFirstName, coacheeInfo.FirstName },
        { MandrillMergeTags.CoachFirstName, coachInfo.FirstName },
        { MandrillMergeTags.CoachFullName, coachInfo.GetFullName() },
        { MandrillMergeTags.SessionsAllocated, coacheeInfo.UserActivity?.SessionsAllocated.ToString() },
        { MandrillMergeTags.CoachBioShort, coachInfo.BioShort },
        { MandrillMergeTags.CoachBioURL, coachInfo.WebProfileUrl },
        { MandrillMergeTags.CoordinatorFullName, ConfigHelper.Email_Welcome_From_Name },
        { MandrillMergeTags.Session1Duration, firstSessionDurationMins.ToString() },
        { MandrillMergeTags.CalendlyURL, PathHelper.Pages.CoacheeSessionBooking(coacheeInfo.CoacheeUID, true) },
        { MandrillMergeTags.CadenceSummary, GetSessionDurations(coacheeInfo) },
        { MandrillMergeTags.MeetCoach_HtmlAboveBookButton, htmlAboveBookButton },
        { MandrillMergeTags.MeetCoach_HtmlBelowBookButton, htmlBelowBookButton }
      };

      if (!projectInfo.MeetCoachEmailCustomHTML.IsNullOrEmpty() && projectInfo.MeetCoachEmailCustomHTML.Contains(MailChimpMergeTagStart)) {
        // Apply above merge fields to custom content.
        mergePairs[MandrillMergeTags.CustomText] = projectInfo.MeetCoachEmailCustomHTML.ReplaceTags(MailChimpMergeTagStart, MailChimpMergeTagEnd, mergePairs);
      }

      bool emailSent = EmailHelper.SendSingleWithMergeTemplate(
        emailTo, null, null,
        null, null,
        projectInfo, true,
        ConfigHelper.MandrillTemplate_MeetYourCoach_Subject,
        ConfigHelper.MandrillTemplate_MeetYourCoach,// + (useStagingTemplate ? StagingTemplateSuffix : ""),
        mergePairs,
        out _);

      if (emailSent && setSendDates) SetMeetCoachEmailDatesAndHistory(trans, coacheeInfo);

      return emailSent;
    }

    private static void SetMeetCoachEmailDatesAndHistory(SqlTransaction trans, DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      DbHelper.AlbertCoachees.SetMeetCoachSent(trans, coacheeInfo.CoacheeId, DateTime.UtcNow);
      DbHelper.EmailHistory.AddEmail(trans,
        coacheeInfo.CoacheeId, (int)coacheeInfo.ProgramJobId, false, "",
        ConfigHelper.Email_Welcome_From_Address, coacheeInfo.EmailAddress, ConfigHelper.MandrillTemplate_MeetYourCoach_Subject, null);
    }

    public static string GetSessionDurations(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      var coachingType = coacheeInfo.GetCoachingType();
      if (coachingType != null && coacheeInfo.HasCoaching) {
        if (coacheeInfo.UserActivity?.SessionsAllocated < 1) throw new ApplicationException("SessionsAllocated < 1");
        return String.Join<int>(" min, ", coachingType.GetDurations(coacheeInfo.UserActivity.SessionsAllocated)) + " min";
      }
      return "";
    }

    // ***
    // *** Coachee Booking Reminders
    // ***

    public static bool SendSessionBookingReminder(
      DbHelper.Projects.ProjectInfo projectInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
      DateTime? latestBookingUtc,
      int daysPassed,
      int bookingReminderCount,
      bool setSentDate,
      bool addToHistory
    ) {

      if (coacheeInfo.ProgramJobId == null) throw new ArgumentException("Coachee not assigned a Job Id");
      if (coacheeInfo.NextBookingTargetDateUtc == null) throw new ArgumentException("Coachee NextBookingTargetDateUtc is null");

      var bookingReminder = new DbHelper.CoachingSessions.SessionBookingReminderInfo(

        tenantOrgId: projectInfo.TenantOrgId,
        tenantOrgGuid: projectInfo.TenantOrgGuid,
        tenantOrgOwnerUserId: projectInfo.TenantOrgOwnerUserId,

        projectJobNumber: projectInfo.JobNumber,
        friendlyProjectTitle: projectInfo.FriendlyProjectTitle,
        brandingOrgGuid: projectInfo.BrandingOrgGuid,
        coacheeId: coacheeInfo.CoacheeId,
        coacheeGuid: coacheeInfo.CoacheeUID,
        coacheeFirstName: coacheeInfo.FirstName,
        coacheeLastName: coacheeInfo.LastName,
        coacheeEmailAddress: coacheeInfo.EmailAddress,
        programJobId: (int)coacheeInfo.ProgramJobId,
        companyId: coacheeInfo.CompanyId.Value,
        programStatusId: coacheeInfo.ProgramStatusId,
        coachingTypeId: coacheeInfo.CoachingTypeId.GetValueOrDefault(DbHelper.AlbertCoachingTypes.ReservedType_Hybrid.CoachingTypeId),
        sessionsAllocated: coacheeInfo.UserActivity.SessionsAllocated,
        sessionsBooked: coacheeInfo.UserActivity.SessionsBooked,
        welcomeEmailUtc: coacheeInfo.WelcomeEmailUtc,
        meetCoachEmailUtc: coacheeInfo.MeetCoachEmailUtc,
        meetCoachEmailSentUtc: coacheeInfo.MeetCoachEmailSentUtc,
        latestBookingUtc: latestBookingUtc,
        bookingReminderLastSentUtc: coacheeInfo.BookingReminderLastSentUtc,
        daysPassed: daysPassed,
        nextBookingTargetDateUtc: (DateTime)coacheeInfo.NextBookingTargetDateUtc,
        bookingReminderCount: bookingReminderCount,
        coachFirstName: coacheeInfo.UserActivity.CoachFirstName,
        coachLastName: coacheeInfo.UserActivity.CoachLastName,
        coachEmailAddress: coacheeInfo.UserActivity.CoachEmail,
        emailSenderName: coacheeInfo.UserActivity.CoachComputedSenderEmailName,
        emailSenderAddress: coacheeInfo.UserActivity.CoachComputedSenderEmailAddress,
        bookSessionEmailCustomHTML: projectInfo.BookSessionEmailCustomHTML,
        bookingReminderCadenceDays: projectInfo.BookingReminderCadenceDays);

      var reminderInfo = new SessionBookingReminderEmailInfo(
        projectJobNumber: bookingReminder.ProjectJobNumber,
        friendlyProjectTitle: bookingReminder.FriendlyProjectTitle,
        tenantOrgGuid: bookingReminder.TenantOrgGuid,
        brandingOrgGuid: bookingReminder.BrandingOrgGuid,
        coacheeId: bookingReminder.CoacheeId,
        coacheeGuid: bookingReminder.CoacheeGuid,
        coacheeFirstName: bookingReminder.CoacheeFirstName,
        coacheeFullName: bookingReminder.CoacheeFirstName + " " + bookingReminder.CoacheeLastName,
        coacheeEmail: bookingReminder.CoacheeEmailAddress,
        programJobId: bookingReminder.ProgramJobId,
        targetDateToDisplay: bookingReminder.NextBookingTargetDateUtc.UtcToTZ(),
        sessionsAllocated: bookingReminder.SessionsAllocated,
        sessionsBooked: bookingReminder.SessionsBooked,
        coachFullName: bookingReminder.CoachFirstName + " " + bookingReminder.CoachLastName,
        coachEmail: bookingReminder.CoachEmailAddress,
        emailSenderName: bookingReminder.EmailSenderName,
        emailSenderAddress: bookingReminder.EmailSenderAddress,
        bookSessionEmailCustomHTML: bookingReminder.BookSessionEmailCustomHTML);

      return SendSessionBookingReminder(reminderInfo, setSentDate, addToHistory);
    }

    public static bool SendSessionBookingReminder(
      SessionBookingReminderEmailInfo reminderInfo,
      bool setSentDate,
      bool addToHistory,
      List<MailAddress> additionalTos = null,
      List<MailAddress> additionalCCs = null,
      List<MailAddress> additionalBCCs = null
    ) {

      string emailSubject = "Coaching Reminder: Time to Book Your Session";

      var emailTo = new List<MailAddress>() { new MailAddress(reminderInfo.CoacheeEmailAddress, reminderInfo.CoacheeFullName) };
      if (additionalTos != null) foreach (var addrTo in additionalTos) emailTo.Add(addrTo);

      var mergePairs = new Dictionary<string, string>() {
        { MandrillMergeTags.ProjectLogoURL, PathHelper.Images.ProjectLogo(reminderInfo.ProjectJobNumber, reminderInfo.BrandingOrgGuid, reminderInfo.TenantOrgGuid, false, true) },
        { MandrillMergeTags.ProjectLogoPixelHeight, ProjectLogoPixelHeight.ToString() },
        { MandrillMergeTags.FriendlyProjectTitle, reminderInfo.FriendlyProjectTitle.ValueIfNullOrEmpty("") },
        { MandrillMergeTags.HasCustomText, reminderInfo.BookSessionEmailCustomHTML.IsNullOrEmpty() ? "" : "True" },
        { MandrillMergeTags.CustomText, reminderInfo.BookSessionEmailCustomHTML.EmptyIfNull() },
        { MandrillMergeTags.CoacheeFirstName, reminderInfo.CoacheeFirstName },
        { MandrillMergeTags.TargetDate, reminderInfo.TargetDateToDisplay.ToString("MMMM d") },
        { MandrillMergeTags.CalendlyURL, PathHelper.Pages.CoacheeSessionBooking(reminderInfo.CoacheeGuid, true) },
        { MandrillMergeTags.CompletedSessions, reminderInfo.SessionsBooked.ToString() },
        { MandrillMergeTags.SessionsAllocated, reminderInfo.SessionsAllocated.ToString() },
        { MandrillMergeTags.TotalSessions, reminderInfo.SessionsAllocated.ToString() },
        { MandrillMergeTags.CoachFullName, reminderInfo.CoachFullName },
        { MandrillMergeTags.CoachName, reminderInfo.CoachFullName }
      };

      if (!reminderInfo.BookSessionEmailCustomHTML.IsNullOrEmpty() && reminderInfo.BookSessionEmailCustomHTML.Contains(MailChimpMergeTagStart)) {
        // Apply above merge fields to custom content.
        mergePairs[MandrillMergeTags.CustomText] = reminderInfo.BookSessionEmailCustomHTML.ReplaceTags(MailChimpMergeTagStart, MailChimpMergeTagEnd, mergePairs);
      }

      bool emailSent = EmailHelper.SendSingleWithMergeTemplate(
        emailTo, additionalCCs, additionalBCCs,
        reminderInfo.EmailSenderName, reminderInfo.EmailSenderAddress,
        null, false,
        emailSubject,
        ConfigHelper.MandrillTemplate_CoacheeBookingReminder + (ConfigHelper.IsStagingServer ? StagingTemplateSuffix : ""),
        mergePairs,
        out _);

      if (emailSent) {
        if (setSentDate) {
          DbHelper.AlbertCoachees.SetBookingReminderSent(reminderInfo.CoacheeId, DateTime.UtcNow);
        }
        if (addToHistory) {
          DbHelper.EmailHistory.AddEmail(null,
            reminderInfo.CoacheeId, reminderInfo.ProgramJobId, false, "",
            reminderInfo.CoachEmailAddress, reminderInfo.CoacheeEmailAddress, emailSubject, null);
        }
      }

      return emailSent;
    }

    public static string WrapEmailBodyWithMasterTemplate(string EmailBody) {

      return ConfigHelper.AlbertEmailMasterTemplate.ReplaceTags(
        new Dictionary<string, string>() {
          { "EmailBody", EmailBody },
          { "EmailFooter", ConfigHelper.AlbertEmailMasterFooter }
        });
    }

    public class SessionBookingReminderEmailInfo {

      public string ProjectJobNumber { get; set; }
      public string FriendlyProjectTitle { get; set; }
      public Guid TenantOrgGuid { get; set; }
      public Guid? BrandingOrgGuid { get; set; }
      public int CoacheeId { get; set; }
      public Guid CoacheeGuid { get; set; }
      public string CoacheeFirstName { get; set; }
      public string CoacheeFullName { get; set; }
      public string CoacheeEmailAddress { get; set; }
      public int ProgramJobId { get; set; }
      public DateTime TargetDateToDisplay { get; set; }
      public int SessionsAllocated { get; set; }
      public int SessionsBooked { get; set; }
      public string CoachFullName { get; set; }
      public string CoachEmailAddress { get; set; }
      public string EmailSenderName { get; set; }
      public string EmailSenderAddress { get; set; }
      public string BookSessionEmailCustomHTML { get; set; }

      public SessionBookingReminderEmailInfo(
        string projectJobNumber,
        string friendlyProjectTitle,
        Guid tenantOrgGuid,
        Guid? brandingOrgGuid,
        int coacheeId,
        Guid coacheeGuid,
        string coacheeFirstName,
        string coacheeFullName,
        string coacheeEmail,
        int programJobId,
        DateTime targetDateToDisplay,
        int sessionsAllocated,
        int sessionsBooked,
        string coachFullName,
        string coachEmail,
        string emailSenderName,
        string emailSenderAddress,
        string bookSessionEmailCustomHTML
      ) {
        this.ProjectJobNumber = projectJobNumber;
        this.FriendlyProjectTitle = friendlyProjectTitle;
        this.TenantOrgGuid = tenantOrgGuid;
        this.BrandingOrgGuid = brandingOrgGuid;
        this.CoacheeId = coacheeId;
        this.CoacheeGuid = coacheeGuid;
        this.CoacheeFirstName = coacheeFirstName;
        this.CoacheeFullName = coacheeFullName;
        this.CoacheeEmailAddress = coacheeEmail;
        this.ProgramJobId = programJobId;
        this.TargetDateToDisplay = targetDateToDisplay;
        this.SessionsAllocated = sessionsAllocated;
        this.SessionsBooked = sessionsBooked;
        this.CoachFullName = coachFullName;
        this.CoachEmailAddress = coachEmail;
        this.EmailSenderName = emailSenderName;
        this.EmailSenderAddress = emailSenderAddress;
        this.BookSessionEmailCustomHTML = bookSessionEmailCustomHTML;
      }
    }

    public class Addressee {

      public string FirstName { get; private set; }
      public string LastName { get; private set; }
      public string Email { get; private set; }

      public Addressee(string firstName, string lastName, string email) {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
      }
      public Addressee(DbHelper.AbleUser.AbleUserInfo userInfo) {
        FirstName = userInfo.FirstName;
        LastName = userInfo.LastName;
        Email = userInfo.EmailAddress;
      }
      public Addressee(DbHelper.Participants.ParticipantInfo partInfo) {
        FirstName = partInfo.FirstName;
        LastName = partInfo.LastName;
        Email = partInfo.Email;
      }
      public Addressee(DbHelper.Participants.AddParticipantToSurveyInfo partInfo) {
        FirstName = partInfo.FirstName;
        LastName = partInfo.LastName;
        Email = partInfo.EmailAddr;
      }
      public string GetFullName() {
        return FirstName + " " + LastName;
      }
    }

  }
}
