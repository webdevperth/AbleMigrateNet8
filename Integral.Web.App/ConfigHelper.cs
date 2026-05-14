using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Integral.Web.Services;

// Note that a bit of code here relies on the project path down to web root being the following:
// (..anything..)\Integral\Albert\WebApplication\AbleWebApp\Integral.Web.AlbertSite\

namespace Integral.Web {

  public static partial class ConfigHelper {

    // When on dev or staging, ALL recipients are filtered using these regex tests.
    // This is the final-word check to prevent sending emails to live users during development & testing.
    // Only allow developer or tester email addresses here.
    public static readonly string[] DevEmailRecipient_AllowedRegexList = {
      @"^(?:webdevperth|jvdalen)(?:\+[a-z0-9]+)?\@gmail\.com$", // certain gmail addresses, including their "plus aliases"
      @"^[^@]*\@integral\.global$",      // anything on @integral.global
      @"^[^@]*\@helloable\.co$"          // anything on helloable.co
    };

    const string ConnectionStringName_IntegralDb = "IntegralDb";
    const string ConnectionStringName_IntegralDb_EF = "IntegralDb_EF";

    public const string DefaultTimeZoneIdIana = "Australia/Perth";
    public const string DefaultTimeZoneAbbrev = "WST";
    public const int DefaultTimeZoneUTCOffsetHours = 8;
    public static readonly TimeZoneInfo DefaultTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(TimeHelper.IANAToWindowsTimeZoneId(DefaultTimeZoneIdIana));

    public const int DefaultAnonymousSessionTimeoutDays = 2;
    public const int DefaultLoginSessionTimeoutDays = 90;
    public const int UserDeletionRequestDelayDays = 10;
    public const string DefaultNewProgramName = "Program 1";

    public class CookieNames {
      public const string AbleLoginSession = "AbleLoginSession";
    }

    public class RequestItems {
      // Doesn't matter if these strings change but they must be unique.
      public const string HelperLog_Prefix = "HelperLog_";
      public const string LogHelper_ResponseLog = "LogHelper_ResponseLog";
      public const string SessionHelper_SessionInfo = "SessionHelper_SessionUserInfo";
      public const string Email_RecipientOverrideName = "Email_RecipientOverrideName";
      public const string Email_RecipientOverrideAddress = "Email_RecipientOverrideAddress";
      public const string Email_MaxToSend = "Email_MaxToSend";
      public const string Email_DisableMaxCheck = "Email_DisableMaxCheck";
      public const string Email_SentCount = "Email_SentCount";
      public const string AjaxHelper_Object = "AjaxHelper_Object";
      public const string PageTitle = "PageTitle";
      public const string IsLoggedInPage = "IsLoggedInPage";
    }

    public const string UnassignedUser_FullName = "[Unassigned]"; // Must be same as in User table.

    public class PayRun {
      // Unsigned types to ensure positive numbers.
      public const byte FixedPeriodDays = 14; // Do not change! The billing period of each pay run.
      public const ushort UpcomingPeriod1_StartDateLagDays = 180; // Number of days before latest pay run to include in upcoming period 1.
      public const byte MaxUpcomingPeriods = 2; // Number of upcoming periods user can select.
    }

    public class PayRunItemTypeId {
      public const int Delivery_Coaching = 1;
      public const int Delivery_Workshop = 2;
      public const int Delivery_Consulting = 3;
      public const int PLC = 4;
      public const int Sales_Opp = 5;
    }

    public static class UserId {
      public const int Automation = 2;
      public const int Unassigned = 233;
      public const int CanDeleteInvoices = 253; // Only Jeroen's user can remove invoices
      public const int RevenuePandL = 252;
      public const int RevenueLost = 943;
    }

    public class UserContractTypeId {
      public const int PartnerCasual = 4;
      public const int PartnerContract = 5;
      public const int SalariedConsultant = 6;
      public const int SalariedOps = 7;
    }

    public const int InvoiceInstructionTypeId_NoTransaction = 6;

    // Markup Cost Items
    public static readonly List<int> CanUpdateDefaultCostItemMarkupPercent = new List<int> { 220, 244 }; // Only these user IDs can update the default cost item markup percent.
    public static readonly List<int> CanAllowCostItemPriceOverwrite = new List<int> { 220, 244, 210, 539 }; // Only these user IDs can set 'Allow Cost Item Manual Overwrite'.
    public const decimal DefaultCostItemMarkupPercent = 0.20M; // 20% markup on cost items by default.

    public enum XeroTaxType { Input, Output, ExemptExpenses, ExemptOutput }
    public static readonly Dictionary<XeroTaxType, string> XeroTaxTypeValue = new Dictionary<XeroTaxType, string>() {
      { XeroTaxType.Input , "INPUT" },
      { XeroTaxType.Output, "OUTPUT" },
      { XeroTaxType.ExemptExpenses , "EXEMPTEXPENSES" },
      { XeroTaxType.ExemptOutput,  "EXEMPTOUTPUT" }
    };

    public class Participants {
      public const int DefaultCoachingProductId = 236;
      public const string DefaultCoachingProductDescription = "Auto-Added Coaching for Participants";
    }

    public class SelfCreatedUserDefaults {

      public class QuoteData {
        public const int DealOwnerUserId = 220;
        public static readonly string XeroTaxType = XeroTaxTypeValue[ConfigHelper.XeroTaxType.Output];
        public const bool CustomInvoicing = false;
        public const bool AddToFreshSales = false;
        public const bool ExcludeFromSalesIncentive = true;
        public const decimal OPPPercentage = 0;
        public const decimal PLCPercentage = 0;
        public const decimal DeliveryPercentage = 0;
        public const decimal PlatformPercentage = 0;
        public const decimal ProposalDesignerPercentage = 0;
        public const int QuoteItems_Quantity = 1;
        public const string QuoteItems_QuantityDescription = "";
        public const int QuoteDealSource_Undefined = 7;
      }

      public class ProjectData {
        public const string ProjectName = "Individual Project for";
      }

      public class ProgramData {
        public const string ProgramName = "Program 1";
        public const int PLC = 220;
      }
    }

    public class RTO_ManagerInfo {
      public const int UserId = 683098;
      public const string FirstName = "Randy";
      public const string LastName = "Salmond";
    }

    public class AbleCoachInfo {
      public const int UserId = 1068;
      public const string FirstName = "Able";
      public const string LastName = "Coach";
      public const string Email = "coach@helloable.co";
    }

    public class WorkshopQualityScoreTargetAvg {
      public const int TargetAverage_Min = 8;
      public const int TargetAverage_Max = 9;
    }

    // Specific partner tags.
    public class PartnerTagId {
      public const int Orientation_TagId = 14;
      public const int TechnicalOnboarding_TagId = 119;
      public const int ICF_MCC_TagId = 10;
      public const int ICF_ACC_TagId = 38;
      public const int ICF_PCC_TagId = 39;
      public const int ICF_AccreditedCoachTraining_TagId = 114;
      public const int Integral_ILP_LMP_LDP_PDP_EIPSL_IHELP_TagId = 102;
      public const int IntegralApprovedCoach = 130;
      public static readonly List<int> RequiredTagsForCoachAssignment = new List<int> { ICF_MCC_TagId, ICF_ACC_TagId, ICF_PCC_TagId, ICF_AccreditedCoachTraining_TagId, IntegralApprovedCoach };
      public const int CoachingStandardPool = 149;
      public const int CoachingExecutivePool = 150;
      public static readonly List<int> RequiredTagsForCoachSelfSelectionByParticipant = new List<int> { CoachingStandardPool, CoachingExecutivePool };
    }

    // Settings for specific companies (hacky, to be remedied).
    public const int SvCompanyId_Deloitte = 3265;
    public const int GblAnsTypeId_Deloitte360 = 3135;

    // Specific global answer types.
    public const int GblAnsTypeId_Standard360 = 2801;
    public const int GblAnsTypeId_Eval = 3050;
    public const int GblAnsTypeId_360Pulse = 3114;
    public const int GblAnsTypeId_Intake = 3132;
    public const int GblAnsTypeId_IOS = 2706;

    // Global answer type preferred descriptions.
    public static readonly Dictionary<int, string> GblAnsTypeDescrById = new Dictionary<int, string>() {
      { GblAnsTypeId_Standard360, "Standard 360 10-point Scale" }
    };

    // Specific question master groups.
    public const int RptQnGroupId_SkillsViewer = 1; // "Leadership Capabilities" group, required for Skills Viewer.
    public const int RptQnGroupId_EvalViewer = 3;   // "Participant Experience" group, required for Eval Viewer.

    // Specific question heading excluded from Evals.
    public const int EvalExclude_RptQnHeadingId = 17;

    public class ReportTypes {
      public const string Eval = "eval";
      public const string Able360 = "able360";
      public const string IOS = "ios";
    }

    // AI Chat
    public const string AbleAIChatName = "Able";

    // Universal login password, only works on dev/local server.
    public const string DevServerTestPassword = "able";

    // Overview Upcoming
    public const int MaxRowsPerTable_OverviewUpcoming = 6;
    public const int CompanyActiveLearnerWindowDays = 90;

    // Coach bios max length
    public const int Coach_ShortBio_MaxLength = 400;

    // Specific survey templates.
    public class TemplateSurveyIds {
      public const int CoachingSessionEval = 4798; // Eval survey to send after a coaching session (not final session).
      public const int WorkshopSessionEval = 4796; // Eval survey to send after a workshop session (not final workshop).
      public const int WorkshopAndProgramEval = 4793; // Eval survey to send after a workshop session (final workshop).
      public const int PostProgramEval_FinalCoachingSession = 4407; // Post-Program Eval on final coaching session.
      public const int PostProgramEval_Old = 4412;
      public const int PostProjectEval = 12424; // Eval survey to send after a project is closed.
      public const int Pulse360 = 11585;
      public const int Intake = 6668;
      public const int CoachingIntake = 11896;
      public const int DevelopmentPlan = 14379;
      public const int NewPreProgramSurvey = 6637;
      public const int NewPostProgramSurvey = 13352;
      public const int IOS_IOIOnly = 23048; // IOS with only the first 32 IOI questions.
    }

    public const int DefaultSurveyIntakeNumber = 1;

    // Do not change ids. Should eventually create a real table.
    public enum SurveyReminderCadenceIds {
      All = 1,            // Send all invites and reminders.
      None = 2,    // Send first invite but no reminders.
      FirstOnly = 3,      // Send first invite
      FirstAndFinal = 4
    }

    public enum EvalType { All, Coaching, Workshop, PostProgram, PostProject }

    public class ReportHeadingIds {
      public const int NPS = 16;
      public const int Outcomes = 17;
      public const int Practitioner = 18;
      public const int GroupSession = 19;
      public const int Coaching = 20;
      public const int Quality = 52;
      public const int OpenQuestions = 25;
    }

    public class EvalScoreTemplateSurveyIds {
      public static readonly int[] Coaching = { 4407, 4798 };
      public static readonly int[] Workshops = { 4796, 13752, 4793, 13753 };
      public static readonly int[] EndProgram = { 4793, 13753, 4407 };
    }

    public class EvalScoreReportHeadingIds {
      public static readonly int[] Coaching = { ReportHeadingIds.NPS, ReportHeadingIds.Outcomes, ReportHeadingIds.Practitioner, ReportHeadingIds.Coaching };
      public static readonly int[] Workshops = { ReportHeadingIds.NPS, ReportHeadingIds.Practitioner, ReportHeadingIds.Quality };
      public static readonly int[] Program = { ReportHeadingIds.NPS, ReportHeadingIds.Outcomes };
    }

    // Specifc global question IDs.
    public class GlobalQuestionIds {
      public const int DevelopmentPlan_ProfessionalVision = 4179;
    }

    // These are the only valid (non-zero/null) values
    // of the column EvalFirstOrLast in workshop or survey.
    // 1 = Workshop / Eval Survey is the first eval in the program.
    // 2 = Workshop / Eval Survey is the last eval in the program.
    public enum EvalFirstOrLastEnum {
      IsFirstEval = 1,
      IsLastEval = 2
    }

    // These are the separate role "modes" a user can switch to in the UI.
    // "Tenant Admin" role isn't currently sepatate, it is the Coach role + user.IsTenantOrgAdmin == true.
    public enum UserRole { Unset, Admin, TenantOrgAdmin, Coach, Client, Leader, OrgReportViewer }

    // Maps certain ConfigHelper.UserRole enum names to different display names.
    public static Dictionary<UserRole, string> UserRoleDisplayNames = new Dictionary<UserRole, string>() {
      { UserRole.Admin, "Admin" },
      { UserRole.TenantOrgAdmin, "Account Admin" },
      { UserRole.Client, "Client" },
      { UserRole.Coach, "Practitioner" },
      { UserRole.Leader, "Leader" },
      { UserRole.OrgReportViewer, "Org Reports" }
    };

    // User Role Id from al_UserRole
    // Note
    public static Dictionary<UserRole, int> UserRoleId = new Dictionary<UserRole, int>() {
      { UserRole.Admin, 1 },
      { UserRole.Client, 2 },
      { UserRole.Coach, 3 },
      { UserRole.Leader, 4 },
      { UserRole.TenantOrgAdmin, 5 },
      { UserRole.OrgReportViewer, 6 }
    };

    public class SubscriptionId {
      public const int Legacy = 1;
      public const int FoundationFree = 2;
      public const int Embedding = 3;
      public const int AI_Coaching = 4;
      public const int AnalyticsPro = 5;
    }

    public class QuoteSalesContentTypeId {
      public const int UrlList = 1;
      public const int PDF = 2;
      public const int WebPageUrl = 3;
      public const int Qwilr = 4;
    }

    // For now, these must reflect the values in lookup table al_SurveyType.
    // TODO: Once SurveyTypeCode is being properly maintained, add flag columns to al_SurveyType indicating business rules (e.g. IsPulse, Is360, HideOnPaxSurveyList.
    public class SurveyTypeCodes {
      public const string Able360 = "360";
      public const string Pulse360 = "Pulse";
      public const string DevPlan = "DevPlan";
      public const string Eval = "Eval";
      public const string Intake = "Intake";
      public const string IOS = "IOS";
    }

    public class InputTypes {
      public const string Option = "o";
      public const string Scale = "scale";
      public const string Dropdown = "d";
      public const string Ranked = "ranked";
      public const string Distributed = "dist";
      public const string Text = "t";
      public const string OpenText = "a";
    }

    public class ExternalUrls {
      public const string PartnerAction_OnboardingNotionUrl = "https://integral.notion.site/Onboarding-with-Integral-8b6680639c114d09be580542acac52ad?pvs=4";
      public const string PartnerAction_OrientationNotionUrl = "https://integral.notion.site/Orientation-360-Accreditation-2217157f38d64c4fbdf888d39bc06faf?pvs=4";
      public const string Qwilr_NonPublicDomain = "app.qwilr.com";
      public const string AbleTermsAndConditionsUrl = "https://www.integral.global/terms-and-conditions";
      public const string AbleWebinarsUrl = "https://www.integral.global/webinars";
      public const string CalendlySupportBooking = "https://calendly.com/jonah-cacioppe/able-support";
    }

    public class HelpUrls {
      public const string ContactUs = "https://help.helloable.co/en/articles/11967899-contact-us";
      public const string Help = "https://help.helloable.co/en/";
    }

    public class YoutubeUrls {
      public const string LongFormat = "www.youtube.com";
      public const string ShortFormat = "youtu.be";
      public const string LongPathQueryVideoId = "v";
    }

    // These settings are stop-gap, for temporary enabling of features in
    // special circumstances, until they are implemented in a standard way.
    // Keep this class even if empty, it will remain the standard place to put these temp settings.
    public static class TemporaryFeatureFlags { }

    // Note the "api key" is the public one, used in javascript, so it's ok to be here and in the repo.
    public const string AmplitudeApiKey = "385c8b3c58d6c4dafd655ff15e37e2a4";
    public const string AmplitudeEventName_Login = "Login";
    public const string AmplitudeEventName_Registration = "User Registration";
    public const string AmplitudeEventName_PageViewed = "Page Viewed";
    public const int AmplitudeCacheSize = 128; // LRU cache size for tracking user property changes per session

    private static readonly IConfigSource _configSource;

    public static string AppRootPath { get; private set; }
    public static string UploadsFolderName { get; private set; }
    public static string UploadsFolderPath { get; private set; }
    public static string UnitTestProjectPath { get; private set; }
    public static string UnitTestResourcesPath { get; private set; }
    public static string LogsFolderPath { get; private set; }

    // Image dimensions
    public const int UserProfilePhotoHeight_Large = 500;
    public const int UserProfilePhotoHeight_Thumb = 80;
    public const int ContentCoverImageHeight_Card = 150;
    public const int ContentCoverImageHeight_DetailPage = 300;
    public const int ContentCoverImageHeight_Thumb = 80;

    public static bool IsDevServer { get; private set; }
    public static bool IsStagingServer { get; private set; }
    public static bool IsLiveServer { get; private set; }

    /// <summary>
    /// Returns the environment type as a string: "dev", "staging", or "prod"
    /// </summary>
    public static string EnvironmentType {
      get {
        if (IsLiveServer) return "prod";
        if (IsStagingServer) return "staging";
        return "dev";
      }
    }

    public static int ServerMaxUploadFileSizeMB { get; private set; }

    public static int JarvisOrgId { get; private set; } // Org Id of Jarvis user accounts.
    public static int AbleOrgId { get; private set; }   // Default Org Id of Able user accounts.
    public static int IntegralTenantOrgId { get; private set; } // For checking if user is Integral
    public static int AbleSurveyOrgId { get; private set; }   // Default Org Id of Able user accounts.
    public static int CoacheeDefaultOrgId { get; private set; }   // Default Org Id of Able user accounts.

    // General settings.

    public static int AnonymousSessionTimeoutDays { get; private set; }
    public static int LoginSessionTimeoutDays { get; private set; }
    public static string IntegralDbConnectionString { get; private set; }
    public static string SiteDomain_Jarvis { get; private set; }
    public static string[] TestUserAccounts { get; private set; }
    public static string[] EmailTesterAccounts { get; private set; }
    public static string EmailRecipientOverrideAddress { get; private set; }
    public static string TestBounceRecipientEmail { get; private set; }
    public static int MinimumSelfsForGroupReport { get; private set; }
    public static int MinimumRatersFor360Report { get; private set; }
    public static int InvitedRatersMinimumRequired { get; private set; }
    public static int InvitedRatersMaximumSuggested { get; private set; }
    public static string EmailMasterTemplateFile { get; private set; }
    public static string EmailMasterTemplate { get; private set; }
    public static string SurveyInviteEmailSignoffFile { get; private set; }
    public static string SurveyInviteEmailSignoff { get; private set; }

    public static string SMTPServer { get; private set; }
    public static int SMTPPort { get; private set; }
    public static bool SMTPUseSSL { get; private set; }
    public static string SMTPAuthUsername { get; private set; }
    public static string SMTPAuthPassword { get; private set; }

    // Test mail disposable-address domain.
    public static string EmailTestRedirectionDomain { get; private set; }
    public static string EmailTestRedirectionIDAccountDomain { get; private set; }

    // Use the on-screen console.log output (usually only when running locally)
    public static bool ShowScreenLog { get; private set; }

    // Albert email template settings.
    public static string AlbertEmailMasterTemplateFile { get; private set; }
    public static string AlbertEmailMasterTemplate { get; private set; }
    public static string AlbertEmailMasterFooterFile { get; private set; }
    public static string AlbertEmailMasterFooter { get; private set; }
    public static string AlbertSurveyInviteEmailSubject_Self { get; private set; }
    public static string AlbertSurveyInviteEmailSubject_Rater { get; private set; }
    public static string AlbertSurveyInviteEmailSubject_PulseRater { get; private set; }
    public static string AlbertSurveyInviteEmailBodyFile_Self { get; private set; }
    public static string AlbertSurveyInviteEmailBodyFile_Rater { get; private set; }
    public static string AlbertSurveyInviteEmailBodyFile_PulseRater { get; private set; }
    public static string AlbertSurveyInviteEmailBody_Self { get; private set; }
    public static string AlbertSurveyInviteEmailBody_Rater { get; private set; }
    public static string AlbertSurveyInviteEmailBody_PulseRater { get; private set; }

    public static string JarvisSurveyInviteEmailSubject_Self { get; private set; }
    public static string JarvisSurveyInviteEmailSubject_Rater { get; private set; }
    public static string JarvisSurveyInviteEmailBodyFile_Self { get; private set; }
    public static string JarvisSurveyInviteEmailBodyFile_Rater { get; private set; }
    public static string JarvisSurveyInviteEmailBody_Self { get; private set; }
    public static string JarvisSurveyInviteEmailBody_Rater { get; private set; }

    public static string SurveyInviteEmailSubject_Self { get; private set; }
    public static string SurveyInviteEmailTemplateFile_Self { get; private set; }
    public static string SurveyInviteEmailTemplate_Self { get; private set; }
    public static string SurveyInviteEmailSubject_Rater { get; private set; }
    public static string SurveyInviteEmailTemplateFile_Rater { get; private set; }
    public static string SurveyInviteEmailTemplate_Rater { get; private set; }
    public static string AlbertEmailReplyToTemplate { get; private set; }

    public static string EventGridEndpoint { get; private set; }
    public static string EventGridEndpoint_Accounting { get; private set; }
    public static string EventGridEndpoint_WorkshopSession { get; private set; }
    public static string EventGridAccessKey { get; private set; }
    public static string EventGridAccessKey_Accounting { get; private set; }
    public static string EventGridAccessKey_Quotes { get; private set; }

    public static string MandrillAPIKey { get; private set; }

    public static string MandrillTemplate_CoacheeBookingReminder { get; private set; }
    public static string MandrillTemplate_Welcome { get; private set; }
    public static string MandrillTemplate_Welcome_WIP { get; private set; }
    public static string MandrillTemplate_MeetYourCoach { get; private set; }
    public static string MandrillTemplate_MeetYourCoach_Subject { get; private set; }
    public static string MandrillTemplate_Generic { get; private set; }
    public static string MandrillTemplate_Nudge { get; private set; }
    public static string MandrillTemplate_Module { get; private set; }
    public static string MandrillTemplate_SurveyInvitation { get; private set; }

    public static string Email_Welcome_From_Name { get; private set; }
    public static string Email_Welcome_From_Address { get; private set; }
    public static int Email_Welcome_SurveyOpenDays { get; private set; }

    public static string Email_Coordinator_FullName { get; private set; }
    public static string Email_Coordinator_Address { get; private set; }

    public static string AICoach_FromEmail { get; private set; }

    public static string UserInviteCodeAllowedCharacters { get; private set; }
    public static string UserInviteCodeRegex { get; private set; }
    public static int UserInviteCodeMaxLength { get; private set; }

    public static string PartnerReferralCodeAllowedCharacters { get; private set; }
    public static string PartnerReferralCodeRegex { get; private set; }
    public static int PartnerReferralCodeMaxLength { get; private set; }

    // Registration Invitation Settings.
    public static int RegistrationInvitationMaxReminders { get; private set; } = 4;
    public static int RegistrationInvitationGapDays { get; private set; } = 6;

    // Survey Settings.
    public static int AbleSurveyDefaultOpenDays_Self { get; private set; }
    public static int AbleSurveyDefaultOpenDays_Rater { get; private set; }
    public static int DevelopmentPlanDefaultOpenDays_Self { get; private set; }
    public static int PulseSurveyGapDays { get; private set; } = 30;
    public static int PulseSurveyDurationDays { get; private set; } = 15;

    // End of Project Survey Email Settings.
    public static int EndProgramEmailWindowDays { get; private set; } = 30;
    public static int MonthsToCreateNewSurveyIntake { get; private set; } = 3;

    // Survey Reminders
    public static int MinimumHoursBetweenReminders { get; private set; } = 45; // 2 days between reminders (less 3hrs to be sure it passes, hence 45 not 48).

    // Users allowed to view Admin Tools page.
    public static readonly List<string> AdminTools_AllowedUsers;

    // Coaching Settings.
    public static List<int> BookingReminderCadenceDays { get; private set; } = new List<int>() { 1, 4, 7, 10, 13, 16, 19, 24, 29, 34, 39, 44, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330, 360 };
    public static int BookingReminderMaxWeeklyReminderMonth { get; private set; } = 6;
    public static int Max_Coaching_Sessions { get; private set; } = 50;

    // Program Overview Coachee Alert.
    public static int ProgramOverviewCoacheeAlert_DaysSinceMeetCoachEmail { get; private set; } = 30;
    public static int ProgramOverviewCoacheeAlert_DaysSinceLastCoachingSession { get; private set; } = 30;

    // Calendly settings.
    public static string CalendlyAPIToken { get; private set; }
    public static int Calendly_QuestionNo_SessionProjectName { get; private set; } = 1;
    public static int Calendly_QuestionNo_GroupProjectName { get; private set; } = 1;
    public static int Calendly_QuestionNo_GroupWorkshopGuid { get; private set; } = 2;
    public static string NoInvalidSessionTypeAlertForEmailDomain { get; private set; } = "integral.global";

    public static int MaxParticipantEvents { get; private set; } = 4;
    public static int MaxParticipantActions_AICoach { get; private set; } = 2;
    public static int MaxParticipantActions_Upcoming { get; private set; } = 3;

    // Reporting settings.
    public const int OrgWebReport_DefaultMinResponsesToShow = 6;
    public const int Reports_FocusTab_RowsPerTable = 5;
    public const int Reports_SkillsViewer_MinSelfResponses = 4;
    public const int Reports_PaxAllowViewFeedbackAfterDays = 28;
    public const int Reports_FocusMaxDifference = 5;

    // Money-related
    public static class Financial {
      public static decimal DefaultGSTPercent { get; internal set; }
      public static decimal DefaultQuoteDeliveryPercentage { get; internal set; }
      public static int QuoteAmountMaxFor100PercentInvoicing { get; internal set; }
    }

    // Misc behaviour settings.
    public static bool ChangeCoachFilterWhenAdminViewsCoachee { get; private set; } = false;
    public static int OverviewPage_HistoricItemCutoffDays { get; private set; } = 1;

    public const int Min_Raters_For_PulseSurvey = 1;

    const string TEST_ASSEMBLY_NAME = "Microsoft.VisualStudio.QualityTools.UnitTestFramework";
    private static bool? _IsUnitTestRunning = null;

    // ParticipantAIChat endpoint.
    public static Uri ParticipantAIChatEndpoint { get; private set; }

    public static class WebConfigValues {
      public static int ServerMaxRequestLength { get; internal set; }
      public static int ExecutionTimeoutSeconds { get; internal set; }
      public static int MaxUrlLength { get; internal set; }
    }

    public static class UIValues {
      public const string SideMenuLaunchClass = "sidebar-submenu-launch";
      public const string SideMenuLaunchClassHighlight = "highlight";
    }

    public static class DaysBetweenEvents {
      // Defaults, overridden by optional config values if present.
      public static int WelcomeEmail_to_MeetCoachEmail { get; private set; }
      public static int CompletedSurvey_to_MeetCoachEmail { get; private set; }
      public static int MeetCoachEmail_to_FirstBookingTargetDate { get; private set; }
      public static int LatestBooking_to_NextBookingTargetDate { get; private set; }
      public static int BookingReminder_To_BookingTargetDate { get; private set; } // Days *before* booking target date to begin reminders.
      public static int Send360Report_To_FirstSessionDate { get; private set; } // Days *before* first session date to send report.
      static DaysBetweenEvents() {
        WelcomeEmail_to_MeetCoachEmail = int.Parse(GetAppSettingOrDefault("WelcomeEmail_to_MeetCoachEmail_Days", "9"));
        CompletedSurvey_to_MeetCoachEmail = int.Parse(GetAppSettingOrDefault("CompletedSurvey_to_MeetCoachEmail", "1"));
        MeetCoachEmail_to_FirstBookingTargetDate = int.Parse(GetAppSettingOrDefault("MeetCoachEmail_to_FirstBookingTargetDate_Days", "7"));
        LatestBooking_to_NextBookingTargetDate = int.Parse(GetAppSettingOrDefault("LatestBooking_to_NextBookingTargetDate_Days", "30"));
        BookingReminder_To_BookingTargetDate = int.Parse(GetAppSettingOrDefault("BookingReminder_To_BookingTargetDate_Days", "30"));
        Send360Report_To_FirstSessionDate = int.Parse(GetAppSettingOrDefault("Send360Report_To_FirstSessionDate_Days", "2"));
      }
    }

    public class Stripe {
      public static string ClientId { get; internal set; }
      public static string ApiKey { get; internal set; }
    }

    public class Intercom {

      // this is the key from the "messenger" interation - used to sign the JWT that encodes the user information
      // that is sent per-page.
      //
      // prod & non-prod: https://app.intercom.com/a/apps/yunr84s6/settings/channels/messenger/security?section=messenger-security&tab=web
      public static string FrontendApiKey { get; internal set; }

      // this is the "application integration" key, that is used to secure sending events from the backend of the server (oauth key)
      // non-prod: https://app.intercom.com/a/apps/yunr84s6/developer-hub/app-packages/153182/oauth
      // prod: https://app.intercom.com/a/apps/yunr84s6/developer-hub/app-packages/153585/oauth
      public static string BackendAccessToken { get; internal set; }

      /// <summary>
      /// How long in minutes the intercom JWT should be valid for, default is 7 days as per the intercom standard.
      /// </summary>
      public static int ExpirationMinutes { get; internal set; }

      /// <summary>
      /// Number of background worker threads for processing Intercom events (default: 2)
      /// </summary>
      public static int WorkerThreadCount { get; internal set; }
    }

    public class AppInsights {
      public static string ConnectionString { get; internal set; }
    }


    public class Amplitude {
      public static string ApiKey { get; internal set; }
      public static int SessionCacheSize { get; internal set; }
      public static int SessionCacheTtlSeconds { get; internal set; }
      public static int SessionCacheMaxAgeSeconds { get; internal set; }
      public static int WorkerThreadCount { get; internal set; }
      public static string EventApiUrl { get; internal set; }
      public static string IdentifyApiUrl { get; internal set; }
      public static int HttpTimeoutSeconds { get; internal set; }
      public static string AnalyticsSampleRate { get; internal set; }
      public static string SessionReplaySampleRate { get; internal set; }
      public static string SessionReplayAllowedRoles { get; internal set; }
    }

    static ConfigHelper() {

      if (IsUnitTestRunning) {
        // When running unit tests in the IDE, we must set location of web.config manually.

        AppRootPath = Regex.Match(AppDomain.CurrentDomain.BaseDirectory, @"^.*Integral\\").Value + @"Albert\WebApplication\AbleWebApp\Integral.Web.AlbertSite\"; // Kind-of-not-hacky-ish as long as project paths are consistent under "\Integral\".
        UnitTestProjectPath = Regex.Match(AppDomain.CurrentDomain.BaseDirectory, @"^.*AbleWebApp\\").Value + @"UnitTests\";
        UnitTestResourcesPath = UnitTestProjectPath + @"resources\";

        var fileMap = new ExeConfigurationFileMap { ExeConfigFilename = AppRootPath + "web.config" };
        var testConfiguration = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
        _configSource = new ConfigSource_Framework(testConfiguration);

      } else {
        // Normal running in IIS/Express.

        _configSource = new ConfigSource_Framework();
        AppRootPath = SystemWeb.ServerMapPath("/").EnsureEndsWith("\\", StringExt.Ensure.IfNotBlank);
      }

      IsLiveServer = IsUnitTestRunning ? false : bool.Parse(GetAppSettingRequired("IsLive"));
      IsStagingServer = IsUnitTestRunning ? false : bool.Parse(GetAppSettingRequired("IsStaging"));
      IsDevServer = IsUnitTestRunning || (!IsLiveServer && !IsStagingServer);

      AnonymousSessionTimeoutDays = GetAppSettingOrDefault("LoginSessionTimeoutDays", "").ToIntOrDefault(DefaultAnonymousSessionTimeoutDays);
      LoginSessionTimeoutDays = GetAppSettingOrDefault("LoginSessionTimeoutDays", "").ToIntOrDefault(DefaultLoginSessionTimeoutDays);

      // UploadsFolder is where all files are stored that must be retained between app deployments. e.g. image uploads.
      // If set, it refers to the mounted file storage path in the Azure App Service.
      // Setting can be omitted on local dev machine, which defaults to "Storage" folder in web root.
      UploadsFolderName = GetAppSettingRequired("UploadsFolderName");
      UploadsFolderPath = GetAppSettingOrDefault("UploadsFolderPath", AppRootPath + UploadsFolderName, true).EnsureEndsWith("\\", StringExt.Ensure.IfNotBlank);

      // Location of app-generated log files. Defaults to "/logs" under the web root (which must be made writeable by IIS)
      LogsFolderPath = GetAppSettingOrDefault("LogsFolderPath", AppRootPath + "logs", true).EnsureEndsWith("\\", StringExt.Ensure.IfNotBlank);

      SetWebConfigValues();

      if (!TryDbConnectionStrings()) return;

      ShowScreenLog = IsDevServer;

      SiteDomain_Jarvis = GetAppSettingRequired("SiteDomain_Jarvis");

      SMTPServer = GetAppSettingRequired("SMTPServer");
      SMTPPort = int.Parse(GetAppSettingRequired("SMTPPort"));
      SMTPUseSSL = GetAppSettingRequired("SMTPUseSSL").ToLower() == "true";
      SMTPAuthUsername = GetAppSettingRequired("SMTPAuthUsername");
      SMTPAuthPassword = GetAppSettingRequired("SMTPAuthPassword");

      ServerMaxUploadFileSizeMB = 20; // This must match what is set in IIS / Azure web app.
      // try {
      //   HttpRuntimeSection section = ConfigurationManager.GetSection("system.web/httpRuntime") as HttpRuntimeSection;
      //   if (section != null) ServerMaxUploadFileSizeMB = section.MaxRequestLength / 1024;
      // } catch (Exception) { } // ignore

      // Settings for email during development.
      EmailRecipientOverrideAddress = GetAppSettingOrDefault("OverrideRecipientEmail", ""); // Send ALL emails to this addr. Omit or blank on prod for normal operation.
      TestUserAccounts = GetAppSettingOrDefault("TestUserAccounts", "").Split(','); // Optional.
      EmailTesterAccounts = GetAppSettingOrDefault("EmailTesterAccounts", "").Split(','); // Optional.
      TestBounceRecipientEmail = GetAppSettingRequired("TestBounceRecipientEmail");

      // Special domain that redirects Integral email addresses.
      EmailTestRedirectionDomain = GetAppSettingOrDefault("EmailTestRedirectionDomain", "idtestmail.com");
      EmailTestRedirectionIDAccountDomain = GetAppSettingOrDefault("EmailTestRedirectionIDAccountDomain", "integral.global"); // ID address from which an account name is determined.

      MinimumSelfsForGroupReport = int.Parse(GetAppSettingOrDefault("MinimumSelfsForGroupReport", "3"));
      MinimumRatersFor360Report = int.Parse(GetAppSettingRequired("MinimumRatersFor360Report"));
      InvitedRatersMinimumRequired = int.Parse(GetAppSettingRequired("InvitedRatersMinimumRequired"));
      InvitedRatersMaximumSuggested = int.Parse(GetAppSettingRequired("InvitedRatersMaximumSuggested"));

      JarvisOrgId = int.Parse(GetAppSettingRequired("IntegralOrgId"));
      AbleOrgId = int.Parse(GetAppSettingRequired("AbleOrgId"));
      IntegralTenantOrgId = AbleOrgId;
      AbleSurveyOrgId = AbleOrgId;
      CoacheeDefaultOrgId = AbleOrgId;

      AbleSurveyDefaultOpenDays_Self = int.Parse(GetAppSettingRequired("AbleSurveyDefaultOpenDays_Self"));
      AbleSurveyDefaultOpenDays_Rater = int.Parse(GetAppSettingRequired("AbleSurveyDefaultOpenDays_Rater"));
      DevelopmentPlanDefaultOpenDays_Self = int.Parse(GetAppSettingOrDefault("DevelopmentPlanDefaultOpenDays_Self", "0"));

      CalendlyAPIToken = GetAppSettingOrDefault("CalendlyAPIToken", "");

      EventGridEndpoint = GetAppSettingOrDefault("EventGridEndpoint", "https://program.australiaeast-1.eventgrid.azure.net/api/events");
      EventGridEndpoint_Accounting = GetAppSettingOrDefault("EventGridEndpoint_Accounting", "https://accounting.australiaeast-1.eventgrid.azure.net/api/events");
      EventGridEndpoint_WorkshopSession = GetAppSettingOrDefault("EventGridEndpoint_WorkshopSession", "https://workshopsession.australiaeast-1.eventgrid.azure.net/api/events");

      EventGridAccessKey = GetAppSettingRequired("EventGridAccessKey");
      EventGridAccessKey_Accounting = GetAppSettingRequired("EventGridAccessKey_Accounting");
      EventGridAccessKey_Quotes = GetAppSettingRequired("EventGridAccessKey_Quotes");

      MandrillAPIKey = GetAppSettingRequired("MandrillAPIKey");
      MandrillTemplate_CoacheeBookingReminder = GetAppSettingRequired("MandrillTemplate_CoacheeBookingReminder");
      MandrillTemplate_Welcome = GetAppSettingRequired("MandrillTemplate_Welcome");
      MandrillTemplate_Welcome_WIP = MandrillTemplate_Welcome + "-wip";
      MandrillTemplate_MeetYourCoach = GetAppSettingRequired("MandrillTemplate_MeetYourCoach");
      MandrillTemplate_MeetYourCoach_Subject = GetAppSettingRequired("MandrillTemplate_MeetYourCoach_Subject");

      MandrillTemplate_Generic = GetAppSettingOrDefault("MandrillTemplate_Generic", "generic-email");
      MandrillTemplate_Nudge = GetAppSettingRequired("MandrillTemplate_Nudge");
      MandrillTemplate_Module = GetAppSettingRequired("MandrillTemplate_Module");
      MandrillTemplate_SurveyInvitation = GetAppSettingRequired("MandrillTemplate_SurveyInvitation");

      // Note "StripeClientId" and "StripeApiKey" are default environment vars used by the Stripe .NET API.
      // For local dev, set them in localsettings/appSettings.config with everything else,
      // but on servers dev/staging/prod, Stripe will automatically get them from the app service environment vars.
      // Be sure to use the "_test_" keys on local, dev & staging. See: https://dashboard.stripe.com/test/apikeys
      Stripe.ClientId = GetAppSettingRequired("StripeClientId");
      Stripe.ApiKey = GetAppSettingRequired("StripeApiKey");

      Intercom.BackendAccessToken = GetAppSettingOrDefault("IntercomBackendAccessToken", "");
      Intercom.FrontendApiKey = GetAppSettingOrDefault("IntercomFrontendApiKey", "");
      if (!int.TryParse(GetAppSettingOrDefault("IntercomExpirationMinutes", ""), out var intercomExpirationMinutes)) {
        LogHelper.DebugWrite("Invalid or empty value for IntercomExpirationMinutes provided, using default.");
        intercomExpirationMinutes = 10080;
      }
      Intercom.ExpirationMinutes = intercomExpirationMinutes;

      if (!int.TryParse(GetAppSettingOrDefault("IntercomWorkerThreadCount", ""), out var intercomWorkerThreadCount) || intercomWorkerThreadCount < 1) {
        LogHelper.DebugWrite("Invalid or empty value for IntercomWorkerThreadCount provided, using default of 2.");
        intercomWorkerThreadCount = 2;
      }
      Intercom.WorkerThreadCount = intercomWorkerThreadCount;

      AppInsights.ConnectionString = GetAppSettingOrDefault("ApplicationInsightsConnectionString", "");
      Amplitude.ApiKey = GetAppSettingOrDefault("AmplitudeApiKey", AmplitudeApiKey);
      Amplitude.SessionCacheSize = GetAppSettingInt("AmplitudeCacheSize", 1024);
      Amplitude.SessionCacheTtlSeconds = GetAppSettingInt("AmplitudeCacheTtlSeconds", 3600); // Default: 1 hour
      Amplitude.SessionCacheMaxAgeSeconds = GetAppSettingInt("AmplitudeCacheMaxAgeSeconds", 3600); // Default: 1 hour
      Amplitude.WorkerThreadCount = GetAppSettingInt("AmplitudeWorkerThreadCount", 2);
      Amplitude.EventApiUrl = GetAppSettingOrDefault("AmplitudeEventApiUrl", "https://api2.amplitude.com/2/httpapi");
      Amplitude.IdentifyApiUrl = GetAppSettingOrDefault("AmplitudeIdentifyApiUrl", "https://api2.amplitude.com/identify");
      Amplitude.HttpTimeoutSeconds = GetAppSettingInt("AmplitudeHttpTimeoutSeconds", 10);
      Amplitude.AnalyticsSampleRate = GetAppSettingOrDefault("AmplitudeSampleRate", "0.7");
      Amplitude.SessionReplaySampleRate = GetAppSettingOrDefault("AmplitudeSessionReplaySampleRate", "0.7");
      Amplitude.SessionReplayAllowedRoles = GetAppSettingOrDefault("AmplitudeSessionReplayAllowedRoles", "Coach,Client");

      Email_Welcome_From_Name = GetAppSettingRequired("Email_Welcome_From_Name");
      Email_Welcome_From_Address = GetAppSettingRequired("Email_Welcome_From_Address");
      Email_Welcome_SurveyOpenDays = int.Parse(GetAppSettingOrDefault("Email_Welcome_SurveyOpenDays", "21"));

      Email_Coordinator_FullName = GetAppSettingOrDefault("AlbertEmailCoordinatorFullName", "Coordination @ Able, by Integral");
      Email_Coordinator_Address = GetAppSettingOrDefault("AlbertEmailCoordinatorAddress", "coordination@integral.global");

      // AI Coach Settings
      AICoach_FromEmail = GetAppSettingOrDefault("AICoach_FromEmail", "coach@helloable.co");

      UserInviteCodeAllowedCharacters = GetAppSettingOrDefault("UserInviteCodeAllowedCharacters", "2346789ABCDEFGHJKLMNPQRTUVWXYZ");
      UserInviteCodeRegex = GetAppSettingOrDefault("UserInviteCodeRegex", @"[a-zA-Z0-9\-]+");
      UserInviteCodeMaxLength = GetAppSettingInt("UserInviteCodeMaxLength", 10);

      PartnerReferralCodeAllowedCharacters = GetAppSettingOrDefault("PartnerReferralCodeAllowedCharacters", "2346789ABCDEFGHJKLMNPQRTUVWXYZ");
      PartnerReferralCodeRegex = GetAppSettingOrDefault("PartnerReferralCodeRegex", @"[a-zA-Z0-9\-]+");
      PartnerReferralCodeMaxLength = GetAppSettingInt("PartnerReferralCodeMaxLength", 5);

      ParticipantAIChatEndpoint = new Uri(GetAppSettingRequired("ParticipantAIChatEndpoint"), UriKind.Absolute);

      GetEmailTemplates();

      // Accounting, currency, taxes etc.
      Financial.DefaultGSTPercent = GetAppSettingPercent("Default_GST_Percent", 0.1M); // 10% GST is the default. All percentages are stored as fractions of 1.
      Financial.DefaultQuoteDeliveryPercentage = GetAppSettingPercent("DefaultQuoteDeliveryPercent", 0.45M); // 45% is the default.
      Financial.QuoteAmountMaxFor100PercentInvoicing = GetAppSettingInt("QuoteAmountMaxFor100PercentInvoicing", 10000); // 100% invoicing up to this amount.

      AdminTools_AllowedUsers = GetAppSettingOrDefault("AdminTools_AllowedUsers", "").Split(',').ToList();
    }

    private static string _siteDomain = null;

    public static string SiteDomain {
      get {

        if (!string.IsNullOrEmpty(_siteDomain)) return _siteDomain;

        string siteDomain = null;
        string sourceDescr = null;

        // Get site domain in the following order of precedence:

        if (System.Diagnostics.Debugger.IsAttached) {
          // Running in IDE. Try getting site domain from project properties "StartExternalURL" setting.
          // Need to manually read the XML file <projectname>.csproj.user.
          sourceDescr = "StartExternalURL project setting";
          siteDomain = null;
          string projectPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName;
          string projectName = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Name;
          string projectUserFile = Path.Combine(projectPath, $"{projectName}.csproj.user");
          if (File.Exists(projectUserFile)) {
            var doc = XDocument.Load(projectUserFile);
            siteDomain = doc.Descendants(doc.Root.Name.Namespace + "StartExternalURL").FirstOrDefault()?.Value;
          }
          //          if (siteDomain.IsNullOrEmpty()) throw new ApplicationException($"Can't get 'StartExternalURL' from csproj.user file.");
        }

        if (siteDomain.IsNullOrEmpty()) {
          sourceDescr = "AbleAppDomain environment var";
          siteDomain = GetAppSettingOrDefault("AbleAppDomain", null);
        }

        if (siteDomain.IsNullOrEmpty()) {
          sourceDescr = "Request url";
          siteDomain = SystemWeb.RequestUrlLeftPart(UriPartial.Authority);
        }

        if (siteDomain.IsNullOrEmpty()) {
          throw new ApplicationException("Can't determine site domain.");
        }

        // Ensure only the domain name in the string, nothing leading or trailing.
        siteDomain = siteDomain.RegexReplace(@"https?:\/\/", "", RegexOptions.IgnoreCase);
        siteDomain = siteDomain.RegexReplace(@"\/$", "", RegexOptions.IgnoreCase);
        if (!siteDomain.RegexIsMatch(AppHelper.Regex.Domain) && !siteDomain.RegexIsMatch(@"^localhost")) {
          throw new ApplicationException($"Invalid site domain obtained from: {sourceDescr} ({siteDomain}).");
        }

        _siteDomain = "https://" + siteDomain;

        return _siteDomain;
      }
    }

    private static void SetWebConfigValues() {
      WebConfigValues.ServerMaxRequestLength = 20480;
      WebConfigValues.ExecutionTimeoutSeconds = 360;
      WebConfigValues.MaxUrlLength = 10000;
      // if (ConfigurationManager.GetSection("system.web/httpRuntime") is HttpRuntimeSection section) {
      //   WebConfigValues.ServerMaxRequestLength = section.MaxRequestLength;
      //   WebConfigValues.ExecutionTimeoutSeconds = (int)section.ExecutionTimeout.TotalSeconds;
      //   WebConfigValues.MaxUrlLength = section.MaxUrlLength;
      // }
    }

    public static bool IsUnitTestRunning {
      get {
        if (_IsUnitTestRunning == null) {
          _IsUnitTestRunning = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.StartsWith(TEST_ASSEMBLY_NAME))
            || AppDomain.CurrentDomain.GetData("notweb") != null; // Hack to force loading configuration directly not via iis.
        }
        return (bool)_IsUnitTestRunning;
      }
    }

    private static void GetEmailTemplates() {

      EmailMasterTemplateFile = GetAppSetting_File("EmailMasterTemplateFile");
      EmailMasterTemplate = GetConfigFileContent(EmailMasterTemplateFile);

      SurveyInviteEmailTemplateFile_Self = GetAppSettingRequired("SurveyInviteEmailTemplateFile_Self");
      SurveyInviteEmailTemplate_Self = GetConfigFileContent(SurveyInviteEmailTemplateFile_Self);

      SurveyInviteEmailTemplateFile_Rater = GetAppSettingRequired("SurveyInviteEmailTemplateFile_Rater");
      SurveyInviteEmailTemplate_Rater = GetConfigFileContent(SurveyInviteEmailTemplateFile_Rater);

      SurveyInviteEmailSignoffFile = GetAppSettingRequired("SurveyInviteEmailSignoffFile");
      SurveyInviteEmailSignoff = GetConfigFileContent(SurveyInviteEmailSignoffFile);

      SurveyInviteEmailSubject_Self = GetAppSettingRequired("SurveyInviteEmailSubject_Self");
      SurveyInviteEmailSubject_Rater = GetAppSettingRequired("SurveyInviteEmailSubject_Rater");

      // Albert-specific:

      AlbertEmailMasterTemplateFile = GetAppSetting_File("AlbertEmailMasterTemplateFile");
      AlbertEmailMasterTemplate = GetConfigFileContent(AlbertEmailMasterTemplateFile);

      AlbertEmailMasterFooterFile = GetAppSetting_File("AlbertEmailMasterFooterFile");
      AlbertEmailMasterFooter = GetConfigFileContent(AlbertEmailMasterFooterFile);

      AlbertSurveyInviteEmailBodyFile_Self = GetAppSetting_File("AlbertSurveyInviteEmailTemplateFile_Self");
      AlbertSurveyInviteEmailBody_Self = GetConfigFileContent(AlbertSurveyInviteEmailBodyFile_Self);

      AlbertSurveyInviteEmailBodyFile_Rater = GetAppSetting_File("AlbertSurveyInviteEmailTemplateFile_Rater");
      AlbertSurveyInviteEmailBody_Rater = GetConfigFileContent(AlbertSurveyInviteEmailBodyFile_Rater);

      AlbertSurveyInviteEmailBodyFile_PulseRater = GetAppSetting_File("AlbertSurveyInviteEmailTemplateFile_PulseRater");
      AlbertSurveyInviteEmailBody_PulseRater = GetConfigFileContent(AlbertSurveyInviteEmailBodyFile_PulseRater);

      AlbertSurveyInviteEmailSubject_Self = GetAppSettingRequired("AlbertSurveyInviteEmailSubject_Self");
      AlbertSurveyInviteEmailSubject_Rater = GetAppSettingRequired("AlbertSurveyInviteEmailSubject_Rater");
      AlbertSurveyInviteEmailSubject_PulseRater = GetAppSettingRequired("AlbertSurveyInviteEmailSubject_PulseRater");

      // Jarvis-specific:

      JarvisSurveyInviteEmailSubject_Self = GetAppSettingRequired("JarvisSurveyInviteEmailSubject_Self");
      JarvisSurveyInviteEmailBodyFile_Self = GetAppSetting_File("JarvisSurveyInviteEmailTemplateFile_Self");
      JarvisSurveyInviteEmailBody_Self = GetConfigFileContent(JarvisSurveyInviteEmailBodyFile_Self);

      JarvisSurveyInviteEmailSubject_Rater = GetAppSettingRequired("JarvisSurveyInviteEmailSubject_Rater");
      JarvisSurveyInviteEmailBodyFile_Rater = GetAppSetting_File("JarvisSurveyInviteEmailTemplateFile_Rater");
      JarvisSurveyInviteEmailBody_Rater = GetConfigFileContent(JarvisSurveyInviteEmailBodyFile_Rater);

    }

    public static void ReplacePathTags(string imagesPath) {
      // Called from the site's PathHelper constructor.
      // Replace all path tags in config string with given paths.

      var replacements = new Dictionary<string, string>() { { "imagespath", imagesPath } };

      AlbertEmailMasterTemplate = AlbertEmailMasterTemplate.ReplaceTags(replacements);

    }

    private static string GetConfigFileContent(string virtualPath) {
      try {
        return System.IO.File.ReadAllText(AppRootPath + virtualPath.Replace("/", "\\"));
      } catch (Exception e) {
        throw new ApplicationException("Can't find config file: " + virtualPath, e);
      }
    }

    private static string GetAppSettingOrDefault(string key, string defaultValue, bool defaultIfEmpty = false) {
      if (key.IsNullOrEmpty()) throw new ArgumentNullException("key");
      string result = _configSource.GetAppSetting(key) ?? defaultValue;
      if (result.IsNullOrEmpty() && defaultIfEmpty) result = defaultValue;
      return result;
    }

    private static decimal GetAppSettingPercent(string key, decimal defaultValue) {
      if (!decimal.TryParse(GetAppSettingOrDefault(key, ""), out var percentValue)) percentValue = defaultValue;
      if (percentValue < 0 || percentValue > 1) StartupErrorMessage("Invalid AppSetting percent value '" + key + "'. Perentages must be 0 >= x <= 1");
      return percentValue;
    }

    private static int GetAppSettingInt(string key, int defaultValue) {
      if (!int.TryParse(GetAppSettingOrDefault(key, ""), out var intValue)) intValue = defaultValue;
      return intValue;
    }

    private static string GetAppSettingRequired(string key) {
      string value = GetAppSettingOrDefault(key, null);
      if (value == null) StartupErrorMessage("Missing AppSetting '" + key + "'");
      return value;
    }

    private static string GetAppSetting_File(string key) {
      // Ensure the setting starts with "/"
      string appSetting = GetAppSettingRequired(key);
      if (!appSetting.StartsWith("/")) appSetting = appSetting.Insert(0, "/");
      return appSetting;
    }

    private static bool TryDbConnectionStrings() {

      IntegralDbConnectionString = _configSource.GetConnectionString(ConnectionStringName_IntegralDb) ?? "";

      if (IntegralDbConnectionString.IsNullOrEmpty()) {
        StartupErrorMessage("Missing ConnectionString '" + ConnectionStringName_IntegralDb + "'");
        return false;
      }

      // Attempt integral db connection to verify it's working.
      using (var conn = new Microsoft.Data.SqlClient.SqlConnection(IntegralDbConnectionString)) {
        try {
          conn.Open();
          conn.Close();
        } catch (Exception ex) {
          StartupErrorMessage("Can't connect to integral database.", ex);
          return false;
        }
      }

      return true;
    }

    public static string TestEmailExceptionRegex {
      get { return @"webdevperth(?:\+[^@]+)?[@]gmail\.com"; }
    }
    public static string InternalSupportFromEmailAddress {
      get { return Email_Coordinator_Address; }
    }
    public static string InternalSupportRecipientEmailAddress {
      get { return "andrew@integral.global"; }
    }

    private static void StartupErrorMessage(string msg, Exception ex = null) {
      if (IsLiveServer) {
        SendSupportEmail(msg);
      } else {
        throw new ApplicationException(msg, ex);
      }
    }

    private static void SendSupportEmail(string message) {
      try {
        using (var mailMsg = new MailMessage {
          From = new MailAddress(InternalSupportFromEmailAddress),
          Subject = "ERROR: ConfigHelper Error",
          Body = message,
          IsBodyHtml = false
        }) {
          mailMsg.To.Add(InternalSupportRecipientEmailAddress);
          using (var smtp = new SmtpClient()) {
            smtp.Send(mailMsg);
          }
        }
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(nameof(SendSupportEmail))
          .WithProperty(ApplicationInsightsConstants.Message, message)
          .Track();
      }
    }
  }
}
