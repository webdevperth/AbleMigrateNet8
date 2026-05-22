using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static Integral.Web.PathHelper.Content;
using static Integral.Web.PathHelper.Images;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class PathHelper : HelperBase<PathHelper> {

    // TODO: Define enums-classes in HelperBase for folders and paths to make them type safe.
    //       See https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/enumeration-classes-over-enum-types

    public const string MenuHighlightPathPrefix = "highlightPath-";
    public static readonly string WebRoot;
    public static readonly string UploadsWebRoot;
    public static readonly string UserPhotoPathTemplate, UserPhotoPathTemplateReplaceName;

    // Script auto-"versioning" using number of seconds since start of current month.
    public static readonly string ScriptVer = (DateTime.Now - new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)).TotalSeconds.ToInt().ToString();

    public enum PageViewAsRole { Default, Participant }

    static PathHelper() {

      WebRoot = GetWebRootPath();

      // Uploads folder.
      // On the prod server, this is a mounted storage path, which persists between app deploys.
      // On a dev machine, it is simply the "uploads" subfolder under the web root.
      // In web.config, the following rule routes requests to "/uploads/..." to a handler script,
      // thus simulating a mapped url path to the mounted storage on the prod server.
      // <rule name="Uploads" patternSyntax="ECMAScript" stopProcessing="true">
      //   <match url="^uploads/(.*)$" ignoreCase="true" />
      //   <action type="Rewrite" url="tools/StorageHandler.aspx" appendQueryString="false" />
      // </rule>
      UploadsWebRoot = WebRoot + ConfigHelper.UploadsFolderName + "/";

      // On first run, ensure folders exist for uploads.
      // Note that on Azure, "Mounted Storage" is used and mapped to a local drive letter.
      // On local dev, these are just local writable folders under the web root.
      EnsureUploadFoldersExist();

      // Ensure path tags in AlbertEmailMasterTemplate are replaced.
      ConfigHelper.ReplacePathTags(UrlPath.Images);

      UserPhotoPathTemplate = UserPhoto("_", "_", UserPhotoSize.Thumbnail, FileNotFoundReturn.NoCheck);
      UserPhotoPathTemplateReplaceName = UserPhotoPathTemplate.RegexMatchStringOrNull(@"(?<=\/)[^\/]*$");
      //if (UserPhotoPathTemplateReplaceName.IsNullOrEmpty()) throw new ApplicationException($"App startup: Error finding UserPhotoPathTemplateReplaceName.");
    }

    static string GetWebRootPath() {
      if (ConfigHelper.IsUnitTestRunning) return "/";
      return SystemWeb.ApplicationVirtualPath;
    }

    private enum UploadSubfolders {
      TenantOrgLogos,
      PartnerProfilePhotos,
      UserProfilePhotos,
      ProjectLogos,
      QuoteOwnerSignatures,
      QuoteSalesPDFs,
      Temp,
      Content,
      SurveyInformation
    }

    public static class UrlPath {
      public static readonly string Partials = WebRoot + "partials/";
      public static readonly string Ajax = WebRoot + "ajax/";
      public static readonly string JS = WebRoot + "scripts/";
      public static readonly string JS_3rdParty = WebRoot + "scripts/3rdparty/";
      public static readonly string CSS = WebRoot + "styles/";
      public static readonly string CSS_3rdparty = WebRoot + "styles/3rdparty/";
      public static readonly string Images = WebRoot + "images/";
      public static readonly string Icons = Images + "icons/";
      public static readonly string Fonts = WebRoot + "fonts/";
      public static readonly string TenantOrgLogos = $"{UploadsWebRoot}{UploadSubfolders.TenantOrgLogos}/";
      public static readonly string PartnerProfilePhotos = $"{UploadsWebRoot}{UploadSubfolders.PartnerProfilePhotos}/";
      public static readonly string UserProfilePhotos = $"{UploadsWebRoot}{UploadSubfolders.UserProfilePhotos}/";
      public static readonly string ProjectLogos = $"{UploadsWebRoot}{UploadSubfolders.ProjectLogos}/";
      public static readonly string QuoteOwnerSignatures = $"{UploadsWebRoot}{UploadSubfolders.QuoteOwnerSignatures}/";
      public static readonly string QuoteSalesPDFs = $"{UploadsWebRoot}{UploadSubfolders.QuoteSalesPDFs}/";
      public static readonly string Settings = "Settings/";
    }

    public static class ServerPaths {
      public static readonly string Pages = @"~\Pages_Albert\";
      public static readonly string UserControls = @"~\UserControls\";
      public static readonly string TenantOrgLogos = $@"{ConfigHelper.UploadsFolderPath}{UploadSubfolders.TenantOrgLogos}\";
      public static readonly string PartnerProfilePhotos = $@"{ConfigHelper.UploadsFolderPath}{UploadSubfolders.PartnerProfilePhotos}\";
      public static readonly string UserProfilePhotos = $@"{ConfigHelper.UploadsFolderPath}{UploadSubfolders.UserProfilePhotos}\";
      public static readonly string ProjectLogos = $@"{ConfigHelper.UploadsFolderPath}{UploadSubfolders.ProjectLogos}\";
      public static readonly string QuoteOwnerSignatures = $@"{ConfigHelper.UploadsFolderPath}{UploadSubfolders.QuoteOwnerSignatures}\";
      public static readonly string QuoteSalesPDFs = $@"{ConfigHelper.UploadsFolderPath}{UploadSubfolders.QuoteSalesPDFs}\";
      public static readonly string UploadTemp = $@"{ConfigHelper.UploadsFolderPath}{UploadSubfolders.Temp}\";
      public static readonly string Content = $@"{ConfigHelper.UploadsFolderPath}{UploadSubfolders.Content}\";
      public static readonly string SurveyInformation = $@"{ConfigHelper.UploadsFolderPath}{UploadSubfolders.SurveyInformation}\";
    }

    public static class AbleUrlKeys {
      public const string Action = "action";
      public const string PasswordResetCode = "code";
      public const string CoachId = "co";
      public const string CoacheeId = "id";
      public const string CoacheeGuid = "cgid";
      public const string CoacheeSearchTerm = "csearch";
      public const string CompanyId = "cpyid";
      public const string ComponentId = "cmpid";
      public const string SurveyReportType = "svrt";
      public const string GblQnHeadingId = "gqh";
      public const string SurveyId = "svid";
      public const string TemplateSurveyId = "tsvid";
      public const string GblAnswerTypeId = "gat";
      public const string SurveyUId = "svuid";
      public const string SurveyUId_LegacyReportLink = "suid";
      public const string SurveyIntakeCodeId = "sviid";
      public const string PreSurveyIntakeId = "preiid";
      public const string PreSurveyPartId = "prepartid";
      public const string SurveyIntakeNumber = "svin";
      public const string WorkshopId = "wid";
      public const string ProjectJobNumber = "pj";
      public const string ProjectSearchTerm = "psearch";
      public const string ProjectStatusToggle = "pstatus";
      public const string ProgramJobId = "jid";
      public const string QuotesSearchTerm = "qsearch";
      public const string QuoteStatusToggle = "qstatus";
      public const string CoachingSessionId = "csid";
      public const string JobSearchTerm = "jsearch";
      public const string PerPage = "perpage";
      public const string GetPage = "getpage";
      public const string CoacheeFilter = "cfilter";
      public const string CoacheeStatusToggle = "cstatustoggle";
      public const string PartId = "partid";
      public const string PartUId = "partuid";
      public const string RaterUId = "rateruid";
      public const string SurveyPage = "pg";
      public const string ConsultingItemId = "ciid";
      public const string ProgramCostItemId = "pciid";
      public const string PayRunId = "prid";
      public const string QuoteGuid = "qgid";
      public const string UserGuid = "ugid";
      public const string QuotePublicPDFPage = "page";
      public const string UserId = "u";
      public const string ReturnUrl = "rtnurl";
      public const string PageTab = "tab";
      public const string UserInviteCode = "invite";
      public const string UserEmailAddress = "ue";
      public const string UserContract = "uc";
      public const string SurveyViewerBenchmark = "svb";
      public const string SurveyViewerFocusPart = "svfp";
      public const string PartnerSearchTerm = "psearch";
      public const string PartnerStatusToggle = "pstatus";
      public const string PartnerTagIds = "ptagids";
      public const string OrganisationGuid = "orguid";
      public const string OrganisationSearchTerm = "osearch";
      public const string OrganisationStatusToggle = "ostatus";
      public const string OrgPeopleSearchTerm = "osearch";
      public const string OrgPeopleStatusToggle = "ostatus";
      public const string OrgIOSReportId = "oirpt";
      public const string EvalType = "evaltype";
      public const string PrePostScope = "pps";
      public const string PartnerSearchMode = "psm";
      public const string SortBy = "sort";
      public const string ContentGuid = "cogid";
      public const string ContentTypeId = "cotid";
      public const string ModuleGuid = "mgid";
      public const string SurveyShareId = "ssid";
      public const string ChartUnits = "units";
      public const string PartialRandomId = "rndid";
      public const string PageViewAsRole = "pvar";
      public const string FileTimestamp = "fts";
      public const string IOSClearCachedResults = "nocache";
      public const string SubscriptionGuid = "subguid";
      public const string SubscriptionQty = "subqty";
      public const string StripeReturnUrlSessionId = "strsid";
      public const string PageMode = "pgmode";
      public const string ParticipantQuickAddTrigger = "pquickadd";
    }

    public static class JSLoggerKeys {
      public const string RequestUrl = "requesturl";
      public const string RequestQuery = "requestquery";
      public const string ReferrerUrl = "referrerurl";
      public const string BrowserUserAgent = "browseruseragent";
      public const string ErrorMessage = "errormessage";
      public const string ErrorUrl = "errorurl";
      public const string ErrorLine = "errorline";
      public const string ErrorColumn = "errorcolumn";
      public const string StackTrace = "StackTrace";
    }

    public static class AbleUrlValues {
      public const string IdNew = "new"; // Used to indicate adding a new record. e.g. "?" + Workshops_URLKey_Id + "=" + IdNew
      public const string CoacheeFilterReset = "reset";
      public const string CoacheeFilterEndProgramToggle = "ept";
      public const string CoacheeStatusToggleValue_Active = "active";
      public const string CoacheeStatusToggleValue_EndProgram = "endProgram";
      public const string ProjectStatusToggleValue_Active = "active";
      public const string ProjectStatusToggleValue_Closed = "closed";
      public const string QuoteStatusToggleValue_Active = "active";
      public const string QuoteStatusToggleValue_Closed = "closed";
      public const string PartnerStatus_Active = "partnerActive";
      public const string PartnerStatus_Inactive = "partnerInactive";
      public const string Organisation_Active = "orgActive";
      public const string Organisation_Inactive = "orgInactive";
      public const string OrganisationPeople_Active = "orgpActive";
      public const string OrganisationPeople_Inactive = "orgpInactive";
      public const string PrePostScope_Coachee = "c";
      public const string PrePostScope_User = "u";
      public const string PageViewAsRole_Participant = "pvarp";
    }

    public static class JarvisUrlKeys {
      public const string SurveyId = "svid";
      public const string SurveyUId = "svid"; // same as Id
      public const string PartId = "partid";
      public const string PartUId = "partid"; // same as id
      public const string Intake = "intake";
      public const string StCode = "stcode";
      public const string JobUID = "jobuid";
      public const string PDFReportPartUId = "partid";
    }

    public static class JarvisUrlValues {
      public const string StCode_360 = "360";
      public const string StCode_Org = "eds";
      public const string StCode_Coaching = "coaching";
    }

    public static class FormKeys {
      public const string AjaxAction = "AjaxAction";
      public const string UserRole = "UserRole";
    }

    public enum QuotePublicPDFPageEnum {
      overview = 1,
      team = 2,
      costing = 3,
      contact = 4
    }

    public enum PrePostScopeEnum {
      None = 0,
      User = 1,
      Coachee = 2
    }

    public enum CoacheeTabEnum { summary, coaching, surveys, email, settings, notes }
    public enum CoachTabEnum { summary, profile, bio, company, partners, partnertags, contract, engageSetting }
    public enum SurveyViewerTabEnum { selection, overview, categories, detailed, focus, heatmap, prepost, comments, aisummary, reportinformationhtml }
    public enum SurveyViewerBenchmarkEnum { Org, Global }
    public enum QuoteTabEnum { project, settings, splits, components, coverLetter, info }
    public enum ParticipantSurveysTabEnum { UsersSurveys, SharedWithUser, SharedByUser }
    public enum ActivityChartUnits { Minutes, Interactions }

    // TODO: Use this in top level functions, like UserPhoto(), instead of "bool placeholderIfNotFound"
    public enum FileNotFoundReturn { EmptyString, UsePlaceholder, NoCheck }

    public static class Pages {

      public static string Home(bool includeDomain = false) => PageUrl("", includeDomain);
      public static string Logout(bool includeDomain = false) => PageUrl("Logout", includeDomain);
      public static string ResetPassword(bool includeDomain = false) => PageUrl("ResetPassword", includeDomain);
      public static string ResetPassword(string resetCode, bool includeDomain = false) => PageUrl($"ResetPassword?{AbleUrlKeys.PasswordResetCode}={resetCode}", includeDomain);
      public static string Register(bool includeDomain = false) => PageUrl($"Register", includeDomain);
      public static string RegisterInvited(string inviteCode, bool includeDomain = false) => PageUrl($"Register?{AbleUrlKeys.UserInviteCode}={inviteCode}", includeDomain);
      public static string RegisterInvited(string inviteCode, int programJobId, bool includeDomain = false) => PageUrl($"Register?{AbleUrlKeys.UserInviteCode}={inviteCode}&{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}", includeDomain);
      public static string Coachees(bool includeDomain = false) => PageUrl($"Coachees", includeDomain);
      public static string CoacheesQuickAdd() => PageUrl($"Coachees?{AbleUrlKeys.ParticipantQuickAddTrigger}");
      public static string CoacheeEdit(bool useIdFromQuery = false) => PageUrlWithQueryId("CoacheeEdit", AbleUrlKeys.CoacheeId, useIdFromQuery);
      public static string CoacheeEdit(int? coacheeId, bool includeDomain = false) => PageUrl($"CoacheeEdit?{AbleUrlKeys.CoacheeId}={coacheeId}", includeDomain);
      public static string CoacheeEdit(int? coacheeId, CoacheeTabEnum? coacheeTabName, bool includeDomain = false) => PageUrl($"CoacheeEdit?{AbleUrlKeys.CoacheeId}={coacheeId}&{AbleUrlKeys.PageTab}={coacheeTabName}", includeDomain);
      public static string CoacheeEdit(CoacheeTabEnum coacheeTabName, int? coacheeId) => PageUrl($"CoacheeEdit?{AbleUrlKeys.PageTab}={coacheeTabName}&{AbleUrlKeys.CoacheeId}={coacheeId}");
      public static string CoacheeEdit(int? coacheeId, CoacheeTabEnum coacheeTabName, string surveyUId, string partUId) => PageUrl($"CoacheeEdit?{AbleUrlKeys.CoacheeId}={coacheeId}&{AbleUrlKeys.PageTab}={coacheeTabName}&{AbleUrlKeys.SurveyUId}={surveyUId}-{partUId}");
      public static string CoacheeSessionBooking(Guid coacheeGuid, bool includeDomain = false) => PageUrl($"BookSession?{AbleUrlKeys.CoacheeGuid}={coacheeGuid.ToStringNoBraces()}", includeDomain);
      public static string CoacheeSurveyEmbed(bool includeDomain = false) => PageUrl("CoacheeSurveyEmbed", includeDomain);
      public static string CoacheeSurveyEmbed(string surveyUId, string partUId, int? surveyShareId = null) => PageUrl($"CoacheeSurveyEmbed?{AbleUrlKeys.SurveyUId}={surveyUId}&{AbleUrlKeys.PartUId}={partUId}{(surveyShareId != null ? $"&{AbleUrlKeys.SurveyShareId}={surveyShareId}" : "")}");
      public static string Organisations() => PageUrl($"Organisations");
      public static string Organisation_Add() => PageUrl($"OrganisationSettings?{AbleUrlKeys.CompanyId}={AbleUrlValues.IdNew}");
      public static string OrganisationSettings(int? companyId) => PageUrl($"OrganisationSettings?{AbleUrlKeys.CompanyId}={companyId}");
      public static string OrganisationSettings(bool useIdFromQuery = false) => PageUrlWithQueryId("OrganisationSettings", AbleUrlKeys.CompanyId, useIdFromQuery);
      public static string OrganisationOverview(int? companyId) => PageUrl($"OrganisationOverview?{AbleUrlKeys.CompanyId}={companyId}");
      public static string OrganisationOverview(bool useIdFromQuery = false) => PageUrlWithQueryId("OrganisationOverview", AbleUrlKeys.CompanyId, useIdFromQuery);
      public static string OrganisationProjects(int? companyId) => PageUrl($"OrganisationProjects?{AbleUrlKeys.CompanyId}={companyId}");
      public static string OrganisationProjects(bool useIdFromQuery = false) => PageUrlWithQueryId("OrganisationProjects", AbleUrlKeys.CompanyId, useIdFromQuery);
      public static string OrganisationDepartments(int? companyId) => PageUrl($"OrganisationDepts?{AbleUrlKeys.CompanyId}={companyId}");
      public static string OrganisationDepartments(bool useIdFromQuery = false) => PageUrlWithQueryId("OrganisationDepts", AbleUrlKeys.CompanyId, useIdFromQuery);
      public static string OrganisationCapabilities() => PageUrl($"OrganisationCapabilities");
      public static string OrganisationCapabilities(int? companyId) => $"{OrganisationCapabilities()}?{AbleUrlKeys.CompanyId}={companyId}";
      public static string OrganisationPeople(bool useIdFromQuery = false) => PageUrlWithQueryId("OrganisationPeople", AbleUrlKeys.CompanyId, useIdFromQuery);
      public static string OrganisationPeople(int? companyId) => PageUrl($"OrganisationPeople?{AbleUrlKeys.CompanyId}={companyId}");
      public static string PeopleDetails() => PageUrl($"PeopleDetails");
      public static string PeopleDetails(int? companyId, Guid? userGuid) => PageUrl($"PeopleDetails?{AbleUrlKeys.CompanyId}={companyId}&{AbleUrlKeys.UserGuid}={userGuid.ToStringNoBracesOrDefault("")}");
      public static string Coaches() => PageUrl($"Coaches");
      public static string CoachEdit(int? coachUserId) => PageUrl($"CoachEdit?{AbleUrlKeys.CoachId}={coachUserId.ToStringOrEmptyIfNull()}");
      public static string CoachEdit(int? coachUserId, CoachTabEnum tabName) => PageUrl($"CoachEdit?{AbleUrlKeys.CoachId}={coachUserId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.PageTab}={tabName}");
      public static string CoachEdit(bool useIdFromQuery = false) => PageUrlWithQueryId("CoachEdit", AbleUrlKeys.CoachId, useIdFromQuery);
      public static string CoachPayRuns(int? coachUserId) => PageUrl($"PartnerPayRuns?{AbleUrlKeys.CoachId}={coachUserId.ToStringOrEmptyIfNull()}");
      public static string CoachPayRuns(int coachUserId, int? payRunId) => PageUrl($"PartnerPayRuns?{AbleUrlKeys.CoachId}={coachUserId}&{AbleUrlKeys.PayRunId}={payRunId.ToStringOrEmptyIfNull()}");
      public static string CoachPayRuns(bool useIdFromQuery = false) { return PageUrlWithQueryId("PartnerPayRuns", AbleUrlKeys.CoachId, useIdFromQuery); }
      public static string CoachUpcoming(bool useIdFromQuery = false) => PageUrlWithQueryId("PartnerUpcoming", AbleUrlKeys.CoachId, useIdFromQuery);
      public static string CoachUpcoming(int? coachUserId) => PageUrl($"PartnerUpcoming?{AbleUrlKeys.CoachId}={coachUserId.ToStringOrEmptyIfNull()}");
      public static string CoachReferrals(bool useIdFromQuery = false) => PageUrlWithQueryId("PartnerReferrals", AbleUrlKeys.CoachId, useIdFromQuery);
      public static string CoachReferrals(int? coachUserId) => PageUrl($"PartnerReferrals?{AbleUrlKeys.CoachId}={coachUserId.ToStringOrEmptyIfNull()}");

      public static string OverviewUpcoming(bool includeDomain = false) => PageUrl("OverviewUpcoming", includeDomain);
      public static string OverviewPayruns() => PageUrl($"OverviewPayruns");
      public static string OverviewPayruns(int? payRunId) => PageUrl($"OverviewPayruns?{AbleUrlKeys.PayRunId}={payRunId.ToStringOrEmptyIfNull()}");

      public static string Programs_Add() => PageUrl($"ProgramSettings?{AbleUrlKeys.ProgramJobId}={AbleUrlValues.IdNew}");
      public static string Programs_Add(string addToProjectJobNumber, string returnUrl) => PageUrl($"ProgramSettings?{AbleUrlKeys.ProgramJobId}={AbleUrlValues.IdNew}&{AbleUrlKeys.ProjectJobNumber}={addToProjectJobNumber}&{AbleUrlKeys.ReturnUrl}={returnUrl.URLEncode()}");
      public static string ProgramOverview() => PageUrlWithQueryId("ProgramOverview", AbleUrlKeys.ProgramJobId, true);
      public static string ProgramOverview(int? programJobId, bool includeDomain = false) => PageUrl($"ProgramOverview?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}", includeDomain);
      public static string ProgramSettings() => PageUrlWithQueryId("ProgramSettings", AbleUrlKeys.ProgramJobId, true);
      public static string ProgramSettings(int? programJobId) => PageUrl($"ProgramSettings?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}");
      public static string ProgramSendEmail() => PageUrlWithQueryId("ProgramSendEmail", AbleUrlKeys.ProgramJobId, true);
      public static string ProgramSendEmail(int? programJobId) => PageUrl($"ProgramSendEmail?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}");
      public static string ProgramSurveyStatus() => PageUrl($"ProgramSurveyStatus");
      public static string ProgramSurveyStatus(bool useIdsFromQuery = false) => PathWithQueryIds("ProgramSurveyStatus", AbleUrlKeys.ProgramJobId, AbleUrlKeys.SurveyUId, useIdsFromQuery);
      public static string ProgramSurveyStatus(int programJobId) => PageUrl($"ProgramSurveyStatus?{AbleUrlKeys.ProgramJobId}={programJobId}");
      public static string ProgramSendSurvey() => PageUrlWithQueryId("ProgramSendSurvey", AbleUrlKeys.ProgramJobId, true);
      public static string ProgramSendSurvey(int? programJobId) => PageUrl($"ProgramSendSurvey?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}");
      public static string ProgramSendSurvey(int programJobId, int templateSurveyId) => PageUrl($"ProgramSendSurvey?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.TemplateSurveyId}={templateSurveyId}");
      public static string ProgramParticipants(int? programJobId, bool includeDomain = false) => PageUrl($"Coachees?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}", includeDomain);
      public static string ProgramInformation() => PageUrlWithQueryId("ProgramInformation", AbleUrlKeys.ProgramJobId, true);
      public static string ProgramInformation(int? programJobId) => PageUrl($"ProgramInformation?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}");
      public static string ProgramInsights(int? programJobId) => PageUrl($"ProgramInsights?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}");

      public static string Content() => PageUrl($"Content");
      public static string ProgramContent(int? programJobId) => PageUrl($"ProgramContent?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}");

      public static string ContentDetails(Guid? quoteGuid, bool includeDomain = false, PageViewAsRole viewAsUserRole = PageViewAsRole.Default) {
        return PageUrl($"ContentDetails?{AbleUrlKeys.ContentGuid}={quoteGuid.ToStringOrEmptyIfNull()}{GetPageViewAsRole(viewAsUserRole)}", includeDomain);
      }
      public static string ProgramContentDetails(int? programJobId, Guid? quoteGuid) {
        return PageUrl($"ContentDetails?{AbleUrlKeys.ContentGuid}={quoteGuid.ToStringOrEmptyIfNull()}&{AbleUrlKeys.ProgramJobId}={programJobId}");
      }
      public static string ContentDetails(Guid? quoteGuid, Guid? moduleGuid) {
        return PageUrl($"ContentDetails?{AbleUrlKeys.ContentGuid}={quoteGuid.ToStringOrEmptyIfNull()}&{AbleUrlKeys.ModuleGuid}={moduleGuid}");
      }
      public static string ContentDetails_AddContent() {
        return PageUrl($"ContentDetails?{AbleUrlKeys.ContentGuid}={AbleUrlValues.IdNew}");
      }
      public static string ContentDetails_AddContent(int programJobId) {
        return PageUrl($"ContentDetails?{AbleUrlKeys.ContentGuid}={AbleUrlValues.IdNew}&{AbleUrlKeys.ProgramJobId}={programJobId}");
      }
      public static string ModuleEdit_AddContent() {
        return PageUrl($"ModuleEdit?{AbleUrlKeys.ModuleGuid}={AbleUrlValues.IdNew}");
      }
      public static string ModuleEdit(Guid? moduleGuid) {
        return PageUrl($"ModuleEdit?{AbleUrlKeys.ModuleGuid}={moduleGuid.ToStringOrEmptyIfNull()}");
      }
      public static string Module(Guid? moduleGuid) {
        return PageUrl($"Module?{AbleUrlKeys.ModuleGuid}={moduleGuid.ToStringOrEmptyIfNull()}");
      }
      public static string Module(Guid? moduleGuid, int programJobId) {
        return PageUrl($"Module?{AbleUrlKeys.ModuleGuid}={moduleGuid.ToStringOrEmptyIfNull()}&{AbleUrlKeys.ProgramJobId}={programJobId}");
      }

      public static string Workshops_List() => PageUrlWithQueryId("Workshops", AbleUrlKeys.ProgramJobId, true);
      public static string Workshops_List(int? programJobId) => PageUrl($"Workshops?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}");
      public static string Workshops_Edit(int? programJobId, int? workshopEventId) => PageUrl($"Workshops?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.WorkshopId}={workshopEventId.ToStringOrEmptyIfNull()}");
      public static string Workshops_Add(int programJobId) => PageUrl($"Workshops?{AbleUrlKeys.ProgramJobId}={programJobId}&{AbleUrlKeys.WorkshopId}={AbleUrlValues.IdNew}");

      public static string WorkshopAttendance(int programJobId, int workshopEventId) => PageUrl($"WorkshopAttendance?{AbleUrlKeys.ProgramJobId}={programJobId}&{AbleUrlKeys.WorkshopId}={workshopEventId}");

      public static string Consulting_List() => PageUrlWithQueryId("Consulting", AbleUrlKeys.ProgramJobId, true);
      public static string Consulting_List(int? programJobId) => PageUrl($"Consulting?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}");
      public static string Consulting_Edit(int? programJobId, int? consultingItemId = null) => PageUrl($"Consulting?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.ConsultingItemId}={consultingItemId.ToStringOrEmptyIfNull()}");
      public static string Consulting_Add(int programJobId) => PageUrl($"Consulting?{AbleUrlKeys.ProgramJobId}={programJobId}&{AbleUrlKeys.ConsultingItemId}={AbleUrlValues.IdNew}");

      public static string ProgramCostItems_List() => PageUrlWithQueryId("ProgramCostItems", AbleUrlKeys.ProgramJobId, true);
      public static string ProgramCostItems_List(int? programJobId) => PageUrl($"ProgramCostItems?{AbleUrlKeys.ProgramJobId}={programJobId.ToStringOrEmptyIfNull()}");
      public static string ProgramCostItems_Edit(int programJobId, int? programCostItemId = null) => PageUrl($"ProgramCostItems?{AbleUrlKeys.ProgramJobId}={programJobId}&{AbleUrlKeys.ProgramCostItemId}={programCostItemId.ToStringOrEmptyIfNull()}");
      public static string ProgramCostItems_Add(int programJobId) => PageUrl($"ProgramCostItems?{AbleUrlKeys.ProgramJobId}={programJobId}&{AbleUrlKeys.ProgramCostItemId}={AbleUrlValues.IdNew}");

      public static string ProgramSkillsViewer(DbHelper.AblePrograms.AbleProgramInfo program) => PageUrl($"ProgramSkillsViewer?{AbleUrlKeys.ProgramJobId}={program.ProgramJobId}");

      public static string Projects(bool includeDomain = false) => PageUrl("Projects", includeDomain);
      public static string Projects_List(bool includeDomain = false) => Projects(includeDomain);
      public static string Projects_Add() => PageUrl($"ProjectSettings?{AbleUrlKeys.ProjectJobNumber}={AbleUrlValues.IdNew}");
      public static string ProjectSettings() => PageUrlWithQueryId("ProjectSettings", AbleUrlKeys.ProjectJobNumber, true);
      public static string ProjectSettings(string projectJobNumber) => PageUrl($"ProjectSettings?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.EmptyIfNull()}");
      public static string ProjectPrograms() => PageUrlWithQueryId("ProjectPrograms", AbleUrlKeys.ProjectJobNumber, true);
      public static string ProjectPrograms(string projectJobNumber) => PageUrl($"ProjectPrograms?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.EmptyIfNull()}");
      public static string ProjectInvoicing() => PageUrlWithQueryId("ProjectInvoicing", AbleUrlKeys.ProjectJobNumber, true);
      public static string ProjectInvoicing(string projectJobNumber) => PageUrl($"ProjectInvoicing?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.EmptyIfNull()}");
      public static string ProjectQuotes() => PageUrlWithQueryId("ProjectQuotes", AbleUrlKeys.ProjectJobNumber, true);
      public static string ProjectQuotes(string projectJobNumber) => PageUrl($"ProjectQuotes?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.EmptyIfNull()}");
      public static string ProjectQuoteCreate(string projectJobNumber) => PageUrl($"QuoteInfo?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber}&{AbleUrlKeys.QuoteGuid}={AbleUrlValues.IdNew}");
      public static string ProjectQuoteDetails(string projectJobNumber, Guid? quoteGuid) => PageUrl($"QuoteInfo?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber}&{AbleUrlKeys.QuoteGuid}={quoteGuid.ToStringNoBracesOrDefault("")}");
      public static string ProjectComponents() => PageUrlWithQueryId("ProjectComponents", AbleUrlKeys.ProjectJobNumber, true);
      public static string ProjectComponents(string projectJobNumber) => PageUrl($"ProjectComponents?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.EmptyIfNull()}");
      public static string ProjectComponents(DbHelper.ProgramComponents.ComponentInfo cmp) {
        if (cmp.CoachingSessionId != null) {
          return CoacheeEdit(CoacheeTabEnum.coaching, cmp.CoacheeId);
        } else if (cmp.WorkshopEventId != null) {
          return Workshops_Edit(cmp.ProgramJobId, cmp.WorkshopEventId);
        } else if (cmp.ConsultingItemId != null) {
          return Consulting_Edit(cmp.ProgramJobId, cmp.ConsultingItemId);
        } else if (cmp.ProgramCostItemId != null) {
          return ProgramCostItems_Edit(cmp.ProgramJobId, cmp.ProgramCostItemId);
        } else {
          return null;
        }
      }
      public static string ProjectCustomise() => PageUrlWithQueryId("ProjectCustomise", AbleUrlKeys.ProjectJobNumber, true);
      public static string ProjectCustomise(string projectJobNumber) => PageUrl($"ProjectCustomise?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.EmptyIfNull()}");

      public static string ProjectAccess_List() => PageUrlWithQueryId("ProjectAccess", AbleUrlKeys.ProjectJobNumber, true);
      public static string ProjectAccess_List(string projectJobNumber) => PageUrl($"ProjectAccess?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.EmptyIfNull()}");
      public static string ProjectAccess_EditUser(string projectJobNumber, int? userId = null) => PageUrl($"ProjectAccess?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber}&{AbleUrlKeys.UserId}={userId.ToStringOrEmptyIfNull()}");
      public static string ProjectAccess_AddUser(string projectJobNumber) => PageUrl($"ProjectAccess?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber}&{AbleUrlKeys.UserId}={AbleUrlValues.IdNew}");

      public static string QuoteList() => PageUrl($"Quotes");
      public static string QuoteCreate() => PageUrl($"QuoteInfo?{AbleUrlKeys.QuoteGuid}={AbleUrlValues.IdNew}");
      public static string QuoteDetails(Guid? quoteGuid) => PageUrl($"QuoteInfo?{AbleUrlKeys.QuoteGuid}={quoteGuid.ToStringNoBracesOrDefault("")}");
      public static string QuoteDetails(Guid? quoteGuid, QuoteTabEnum? quoteTabEnum) => PageUrl($"QuoteInfo?{AbleUrlKeys.QuoteGuid}={quoteGuid.ToStringNoBracesOrDefault("")}&{AbleUrlKeys.PageTab}={quoteTabEnum}");
      public static string QuoteComponents(Guid? quoteGuid) => PageUrl($"QuoteItems?{AbleUrlKeys.QuoteGuid}={quoteGuid.ToStringNoBracesOrDefault("")}");

      public static string QuoteQwilrView(Guid? quoteGuid, bool includeDomain = false) => PageUrl($"QuoteQwilrView?{AbleUrlKeys.QuoteGuid}={quoteGuid.ToStringNoBracesOrDefault("")}", includeDomain);
      public static string QuoteQwilrSignOff(Guid? quoteGuid, bool includeDomain = false) => PageUrl($"QuoteQwilrSignOff?{AbleUrlKeys.QuoteGuid}={quoteGuid.ToStringNoBracesOrDefault("")}", includeDomain);
      public static string QuotePublicView(Guid? quoteGuid, bool includeDomain = false) => PageUrl($"QuotePublicView?{AbleUrlKeys.QuoteGuid}={quoteGuid.ToStringNoBracesOrDefault("")}", includeDomain);
      public static string QuotePublicView(Guid quoteGuid, Guid userGuid, bool includeDomain = false) => PageUrl($"QuotePublicView?{AbleUrlKeys.QuoteGuid}={quoteGuid}&{AbleUrlKeys.UserGuid}={userGuid}", includeDomain);
      public static string QuotePublicPDF(Guid? quoteGuid, bool includeDomain = false) => PageUrl($"QuotePublicPDF?{AbleUrlKeys.QuoteGuid}={quoteGuid.ToStringNoBracesOrDefault("")}", includeDomain);
      public static string QuotePublicPDF(Guid? quoteGuid, QuotePublicPDFPageEnum page, bool includeDomain = false) => PageUrl($"QuotePublicPDF?{AbleUrlKeys.QuoteGuid}={quoteGuid.ToStringNoBracesOrDefault("")}&{AbleUrlKeys.QuotePublicPDFPage}={page.ToString()}", includeDomain);

      public static string DevelopmentPlan(bool includeDomain = false) => PageUrl($"DevelopmentPlan", includeDomain);
      public static string DevelopmentPlanForm(bool includeDomain = false) => PageUrl($"DevelopmentPlanForm", includeDomain);
      public static string DevelopmentPlanForm(string surveyUId, string partUId, bool includeDomain = false) => PageUrl($"DevelopmentPlanForm?{AbleUrlKeys.SurveyUId}={surveyUId}&{AbleUrlKeys.PartUId}={partUId}", includeDomain);
      public static string DevelopmentPlanForm(string surveyUId, string partUId, int surveyShareId, bool includeDomain = false) => PageUrl($"DevelopmentPlanForm?{AbleUrlKeys.SurveyUId}={surveyUId}&{AbleUrlKeys.PartUId}={partUId}&{AbleUrlKeys.SurveyShareId}={surveyShareId}", includeDomain);

      public static string ParticipantSurveys(bool includeDomain = false) => PageUrl($"ParticipantSurveys", includeDomain);
      public static string ParticipantSurveys(ParticipantSurveysTabEnum? surveyTab, bool includeDomain = false) => PageUrl($"ParticipantSurveys?{AbleUrlKeys.PageTab}={surveyTab}", includeDomain);
      public static string ParticipantSurvey(bool includeDomain = false) => PageUrl($"ParticipantSurvey", includeDomain);
      public static string ParticipantSurvey(string surveyUId, string partUId, bool includeDomain = false) => PageUrl($"ParticipantSurvey?{AbleUrlKeys.SurveyUId}={surveyUId}&{AbleUrlKeys.PartUId}={partUId}", includeDomain);
      public static string ParticipantSurvey(string surveyUId, string partUId, int page) => PageUrl($"ParticipantSurvey?{AbleUrlKeys.SurveyUId}={surveyUId}&{AbleUrlKeys.PartUId}={partUId}&{AbleUrlKeys.SurveyPage}={page}");
      public static string ParticipantRaters(string surveyUID, string partUID) => PageUrl($"ParticipantRaters?{AbleUrlKeys.SurveyUId}={surveyUID}&{AbleUrlKeys.PartUId}={partUID}");
      public static string ParticipantCompleted(string surveyUID, string partUID) => PageUrl($"ParticipantCompleted?{AbleUrlKeys.SurveyUId}={surveyUID}&{AbleUrlKeys.PartUId}={partUID}");

      public static string RaterDecline(string surveyUID, string raterUID, bool includeDomain = false) => PageUrl($"RaterDecline?{AbleUrlKeys.SurveyUId}={surveyUID}&{AbleUrlKeys.RaterUId}={raterUID}", includeDomain);

      public static string IOSParticipants(bool includeDomain = false) => PageUrl($"IOSParticipants", includeDomain);

      public static string ParticipantCoaching(bool includeDomain = false) => PageUrl($"ParticipantCoaching", includeDomain);
      public static string ParticipantCoaching(Guid? coacheeGuid, int? userIdToTriggetCoachPanel = null, bool includeDomain = false) => PageUrl($"ParticipantCoaching{(coacheeGuid == null ? "" : $"?{AbleUrlKeys.CoacheeGuid}={coacheeGuid}")}{(userIdToTriggetCoachPanel == null ? "" : $"&{AbleUrlKeys.UserId}={userIdToTriggetCoachPanel}")}", includeDomain);
      public static string ParticipantUpcoming(bool includeDomain = false) => PageUrl($"ParticipantUpcoming", includeDomain);
      public static string ParticipantAICoach(bool includeDomain = false) => PageUrl($"ParticipantAICoach", includeDomain);
      public static string ParticipantDevelopment(bool includeDomain = false) => PageUrl($"ParticipantDevelopment", includeDomain);

      public static string Survey(DbHelper.AlbertSurveys.SurveyInfo surveyInfo, string partUID, bool includeDomain = false) {
        return Survey(surveyInfo, partUID, 1, includeDomain);
      }
      public static string Survey(DbHelper.AlbertSurveys.SurveyInfo surveyInfo, bool includeDomain = false) {
        return Survey(surveyInfo, surveyInfo?.FoundParticipantBrief?.PartUniqueId, 1, includeDomain);
      }

      public static string Survey(DbHelper.AlbertSurveys.SurveyInfo surveyInfo, string partUID, int page, bool includeDomain = false) {

        if (surveyInfo == null || partUID.IsNullOrEmpty()) return "";

        if (surveyInfo.IsAbleSurvey) {
          // Able survey link.

          string url;

          if (surveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) {
            // Dev plan surveys are a separate thing and require being logged in as participant.
            url = DevelopmentPlanForm(surveyInfo.SurveyUID, partUID, includeDomain);
          } else {
            // The normal survey page, which will redirect user to the in-portal survey page if appropriate at the time.
            url = PageUrl($"ParticipantSurvey?{AbleUrlKeys.SurveyUId}={surveyInfo.SurveyUID}&{AbleUrlKeys.PartUId}={partUID}&{AbleUrlKeys.SurveyPage}={page}", includeDomain);
          }
          return url;

        } else {
          // Jarvis survey link. Always add domain as it's a different site to Able.

          return $"{ConfigHelper.SiteDomain_Jarvis}/survey/survey_show360.asp?{JarvisUrlKeys.SurveyUId}={surveyInfo.SurveyUID}&{JarvisUrlKeys.PartUId}={partUID}";
        }
      }

      public static string Surveys(bool includeDomain = false) => PageUrl($"Surveys", includeDomain);
      public static string SurveyQuestions(bool includeDomain = false) => PageUrl($"SurveyQuestions", includeDomain);
      public static string SurveyQuestions(string surveyUID, string partUID, int Page = 1) => PageUrl($"SurveyQuestions?{AbleUrlKeys.SurveyUId}={surveyUID}&{AbleUrlKeys.PartUId}={partUID}&{AbleUrlKeys.SurveyPage}={Page}");
      public static string SurveyRaters(string surveyUID, string partUID, bool includeDomain = false) => PageUrl($"SurveyRaters?{AbleUrlKeys.SurveyUId}={surveyUID}&{AbleUrlKeys.PartUId}={partUID}", includeDomain);
      public static string SurveyCompleted(string surveyUID, string partUID, bool includeDomain = false) => PageUrl($"SurveyCompleted?{AbleUrlKeys.SurveyUId}={surveyUID}&{AbleUrlKeys.PartUId}={partUID}", includeDomain);

      public static string GetSurveyCompletedURL(DbHelper.Participants.ParticipantInfo partInfo, DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
        if (partInfo.IsSelf && surveyInfo.FeedbackOption != DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters) {
          // Show Raters Page
          if (SessionHelper.IsUserLoggedIn) {
            return SurveyRaters(surveyInfo.SurveyUID, partInfo.PartUID);
          } else {
            return ParticipantRaters(partInfo.SurveyUID, partInfo.PartUID);
          }
        } else {
          // Show Completed Page
          if (SessionHelper.IsUserLoggedIn) {
            return ParticipantSurveys();
          } else {
            return ParticipantCompleted(partInfo.SurveyUID, partInfo.PartUID);
          }
        }
      }

      public static string Guide(bool includeDomain = false) => PageUrl($"Guide", includeDomain);
      public static string AdminTools(bool includeDomain = false) => PageUrl($"AdminTools", includeDomain);

      public static void GetCoacheeSurveyUIDs(out string urlSelectedSurveyUId, out string urlSelectedPartUId) {
        urlSelectedSurveyUId = null;
        urlSelectedPartUId = null;
        string svuid = WebHelper.GetQueryStringValue(AbleUrlKeys.SurveyUId) ?? string.Empty;
        if (svuid.IsNullOrEmptyOrWhitespace()) return;
        if (!svuid.Contains("-")) svuid += "-"; // Ensure there are 2 parts if suid is blank or one part is not given.
        var sarr = svuid.Split('-');
        urlSelectedSurveyUId = DbHelper.AlbertSurveys.GetValidUniqueId(sarr[0]); // survey uid.
        urlSelectedPartUId = DbHelper.AlbertSurveys.GetValidUniqueId(sarr[1]); // participant uid.
      }

      public class Settings {

        public static string General(bool includeDomain = false) => PageUrl($"{UrlPath.Settings}General", includeDomain);
        public static string Billings(bool includeDomain = false) => PageUrl($"{UrlPath.Settings}Billings", includeDomain);

        public static string BillingsReturnUrl(Guid selectedSubscriptionGuid, int? selectedQuanity) =>
          PageUrl($"{UrlPath.Settings}Billings"
            + $"?{AbleUrlKeys.SubscriptionGuid}={selectedSubscriptionGuid.ToStringNoBraces()}"
            + $"&{AbleUrlKeys.SubscriptionQty}={selectedQuanity}",
            includeDomain: true);

        public static string Team() => $"{UrlPath.Settings}Team";
        public static string Plans() => $"{UrlPath.Settings}Plans";
        public static string Invoices() => $"{UrlPath.Settings}Invoices";
      }
    }

    public class JarvisPages {

      public static string ParticipantsUrl(int surveyId, int intakeNumber) =>
        $"{ConfigHelper.SiteDomain_Jarvis}/survey/survey_participants360.asp?{JarvisUrlKeys.StCode}={JarvisUrlValues.StCode_360}&{JarvisUrlKeys.SurveyId}={surveyId}&{JarvisUrlKeys.Intake}={intakeNumber}";

      public static string OrgParticipantsUrl(int surveyId, int intakeNumber) =>
        $"{ConfigHelper.SiteDomain_Jarvis}/survey/survey_participantsEDS.asp?{JarvisUrlKeys.StCode}={JarvisUrlValues.StCode_Org}&{JarvisUrlKeys.SurveyId}={surveyId}&{JarvisUrlKeys.Intake}={intakeNumber}";

      public static string DataEntryUrl(int surveyId, int partId) =>
        $"{ConfigHelper.SiteDomain_Jarvis}/survey/survey_entry.asp?{JarvisUrlKeys.StCode}={JarvisUrlValues.StCode_360}&{JarvisUrlKeys.SurveyId}={surveyId}&{JarvisUrlKeys.PartId}={partId}";
    }

    public static class Partials {

      public static string CoacheeSurveyDetailsModal() {
        return $"{UrlPath.Partials}CoacheeSurveyDetailsModal";
      }
      public static string CoacheeSurveyDetailsModal(DbHelper.AlbertCoachees.AlbertCoacheeInfo contextCoacheeInfo, DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
        if (surveyInfo == null) return "";
        if (surveyInfo.FoundParticipantBrief == null) {
          return CoacheeSurveyDetailsModal(contextCoacheeInfo?.CoacheeId ?? 0, $"{surveyInfo.SurveyUID}");
        } else {
          return CoacheeSurveyDetailsModal(contextCoacheeInfo?.CoacheeId ?? surveyInfo.FoundParticipantBrief?.CoacheeId ?? 0, $"{surveyInfo.SurveyUID}-{surveyInfo.FoundParticipantBrief.PartUniqueId}");
        }
      }
      public static string CoacheeSurveyDetailsModal(int contextCoacheeId, string surveyUId = null, int? surveyShareId = null) {
        return $"{UrlPath.Partials}CoacheeSurveyDetailsModal?{AbleUrlKeys.CoacheeId}={contextCoacheeId}&{AbleUrlKeys.SurveyUId}={surveyUId.EmptyIfNull()}{(surveyShareId != null ? $"&{AbleUrlKeys.SurveyShareId}={surveyShareId}" : "")}";
      }
      public static string CoachingSessionModal(int coacheeId, string sessionId = null) {
        return $"{UrlPath.Partials}CoachingSessionModal?{AbleUrlKeys.CoacheeId}={coacheeId}&{AbleUrlKeys.CoachingSessionId}={sessionId.EmptyIfNull()}";
      }
      public static string ComponentModal(string jobNumber, int? componentId) {
        return $"{UrlPath.Partials}ComponentModal?{AbleUrlKeys.ProjectJobNumber}={jobNumber}&{AbleUrlKeys.ComponentId}={componentId}";
      }
      public static string Components_BulkAddInvoiceModal(string jobNumber, Guid? quoteGuid) {
        return $"{UrlPath.Partials}Components_BulkAddInvoiceModal?{AbleUrlKeys.ProjectJobNumber}={jobNumber}&{AbleUrlKeys.QuoteGuid}={quoteGuid}";
      }

      public static string CoacheeReport_Overview(List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark, int? surveyShareId = null) {
        return $"{UrlPath.Partials}CoacheeReport_Overview?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}{(surveyShareId != null ? $"&{AbleUrlKeys.SurveyShareId}={surveyShareId}" : "")}";
      }
      public static string CoacheeReport_Categories(List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark, int? surveyShareId = null) {
        return $"{UrlPath.Partials}CoacheeReport_Categories?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}{(surveyShareId != null ? $"&{AbleUrlKeys.SurveyShareId}={surveyShareId}" : "")}";
      }
      public static string CoacheeReport_Detail(List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark, int? surveyShareId = null) {
        return $"{UrlPath.Partials}CoacheeReport_Detail?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}{(surveyShareId != null ? $"&{AbleUrlKeys.SurveyShareId}={surveyShareId}" : "")}";
      }
      public static string CoacheeReport_Focus(List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark, int? surveyShareId = null) {
        return $"{UrlPath.Partials}CoacheeReport_Focus?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}{(surveyShareId != null ? $"&{AbleUrlKeys.SurveyShareId}={surveyShareId}" : "")}";
      }
      public static string CoacheeReport_PrePost(int preSurvey_partId, int postSurvey_intakeCodeId, SurveyViewerBenchmarkEnum? benchmark, int? surveyShareId = null) {
        return $@"{UrlPath.Partials}CoacheeReport_PrePost?{AbleUrlKeys.PreSurveyPartId}={preSurvey_partId}&{AbleUrlKeys.SurveyIntakeCodeId}={postSurvey_intakeCodeId}
          &{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}{(surveyShareId != null ? $"&{AbleUrlKeys.SurveyShareId}={surveyShareId}" : "")}";
      }
      public static string CoacheeReport_Comments(List<int> intakeCodeIds, int? surveyShareId = null) {
        return $"{UrlPath.Partials}CoacheeReport_Comments?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}{(surveyShareId != null ? $"&{AbleUrlKeys.SurveyShareId}={surveyShareId}" : "")}";
      }

      public static string SurveyViewer_Overview(List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SurveyViewer_Overview?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SurveyViewer_Overview(List<int> intakeCodeIds, int? gblAnswerTypeId, int? templateSurveyId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SurveyViewer_Overview?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.GblAnswerTypeId}={gblAnswerTypeId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.TemplateSurveyId}={templateSurveyId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SurveyViewer_Categories(List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SurveyViewer_Categories?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SurveyViewer_Categories(List<int> intakeCodeIds, int? gblAnswerTypeId, int? templateSurveyId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SurveyViewer_Categories?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.GblAnswerTypeId}={gblAnswerTypeId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.TemplateSurveyId}={templateSurveyId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SurveyViewer_Detail(List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SurveyViewer_Detail?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SurveyViewer_Detail(List<int> intakeCodeIds, int? gblAnswerTypeId, int? templateSurveyId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SurveyViewer_Detail?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.GblAnswerTypeId}={gblAnswerTypeId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.TemplateSurveyId}={templateSurveyId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public enum SurveyViewer_Focus_ShowSection { Highest, Lowest, Both }
      public static string SurveyViewer_Focus(List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark, SurveyViewer_Focus_ShowSection showPart = SurveyViewer_Focus_ShowSection.Both) {
        return $"{UrlPath.Partials}SurveyViewer_Focus?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}"
          + $"&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}"
          + $"&{AbleUrlKeys.SurveyViewerFocusPart}={showPart.ToStringOrEmptyIfNull()}";
      }
      public static string SurveyViewer_PrePost(int preSurvey_partId, int postSurvey_intakeCodeId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SurveyViewer_PrePost?{AbleUrlKeys.PreSurveyPartId}={preSurvey_partId}&{AbleUrlKeys.SurveyIntakeCodeId}={postSurvey_intakeCodeId}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SurveyViewer_Comments(List<int> intakeCodeIds) {
        return $"{UrlPath.Partials}SurveyViewer_Comments?{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}";
      }

      public static string SkillsViewer_Overview(string projectJobNumber, List<int> programJobIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SkillsViewer_Overview?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SkillsViewer_Categories(string projectJobNumber, List<int> programJobIds, PrePostScopeEnum prePostScope, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SkillsViewer_Categories?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}&{AbleUrlKeys.PrePostScope}={prePostScope}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SkillsViewer_Detail(string projectJobNumber, List<int> programJobIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SkillsViewer_Detail?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SkillsViewer_Focus(string projectJobNumber, List<int> programJobIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SkillsViewer_Focus?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SkillsViewer_HeatMap(string projectJobNumber, List<int> programJobIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SkillsViewer_HeatMap?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string SkillsViewer_PrePost(string projectJobNumber, List<int> programJobIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}SkillsViewer_PrePost?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }

      public static string CompanyReport_Overview(int surveyCompanyId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}CompanyReport_Overview?{AbleUrlKeys.CompanyId}={surveyCompanyId}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string CompanyReport_Categories(int surveyCompanyId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}CompanyReport_Categories?{AbleUrlKeys.CompanyId}={surveyCompanyId}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string CompanyReport_Detail(int surveyCompanyId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}CompanyReport_Detail?{AbleUrlKeys.CompanyId}={surveyCompanyId}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string CompanyReport_Focus(int surveyCompanyId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}CompanyReport_Focus?{AbleUrlKeys.CompanyId}={surveyCompanyId}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string CompanyReport_HeatMap(int surveyCompanyId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}CompanyReport_HeatMap?{AbleUrlKeys.CompanyId}={surveyCompanyId}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string CompanyReport_PrePost(int surveyCompanyId, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}CompanyReport_PrePost?{AbleUrlKeys.CompanyId}={surveyCompanyId}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string ActivityChartForCompany(int companyId, ActivityChartUnits? chartUnits = ActivityChartUnits.Minutes) {
        return $"{UrlPath.Partials}ActivityChart?{AbleUrlKeys.CompanyId}={companyId}&{AbleUrlKeys.ChartUnits}={chartUnits.ToStringOrEmptyIfNull()}";
      }
      public static string ActivityChartForProgram(int programJobId, ActivityChartUnits? chartUnits = ActivityChartUnits.Minutes) {
        return $"{UrlPath.Partials}ActivityChart?{AbleUrlKeys.ProgramJobId}={programJobId}&{AbleUrlKeys.ChartUnits}={chartUnits.ToStringOrEmptyIfNull()}";
      }
      public static string EvalViewer_Overview(string projectJobNumber, List<int> programJobIds, List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}EvalViewer_Overview"
          + $"?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}"
          + $"&{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string EvalViewer_Categories(string projectJobNumber, List<int> programJobIds, List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}EvalViewer_Categories"
          + $"?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}"
          + $"&{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string EvalViewer_QuestionDetail(string projectJobNumber, List<int> programJobIds, List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}EvalViewer_QuestionDetail"
          + $"?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}"
          + $"&{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }
      public static string EvalViewer_QuestionPrePost(string projectJobNumber, List<int> programJobIds, List<int> intakeCodeIds, SurveyViewerBenchmarkEnum? benchmark) {
        return $"{UrlPath.Partials}EvalViewer_QuestionPrePost"
          + $"?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber.URLEncode()}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}"
          + $"&{AbleUrlKeys.SurveyIntakeCodeId}={intakeCodeIds.ToStringList()}&{AbleUrlKeys.SurveyViewerBenchmark}={benchmark.ToStringOrEmptyIfNull()}";
      }

      public static string CoacheeSendSurveyModal(int coacheeId) {
        return $"{UrlPath.Partials}CoacheeSendSurveyModal?{AbleUrlKeys.CoacheeId}={coacheeId}";
      }

      public static string PartnerSlideoutPanel(int? coachId) {
        return $"{UrlPath.Partials}PartnerSlideoutPanel?{AbleUrlKeys.CoachId}={coachId}";
      }

      public static string ParticipantSlideoutPanel(int? coacheeId) {
        return $"{UrlPath.Partials}ParticipantSlideoutPanel?{AbleUrlKeys.CoacheeId}={coacheeId}";
      }

      public static string QuoteRequestModal(string projectJobNumber) {
        return $"{UrlPath.Partials}QuoteRequestModal?{AbleUrlKeys.ProjectJobNumber}={projectJobNumber}";
      }

      public static string EventSlideoutPanel_Workshop(int? workshopEventId) {
        return $"{UrlPath.Partials}EventSlideoutPanel?{AbleUrlKeys.WorkshopId}={workshopEventId.ToStringOrEmptyIfNull()}";
      }

      public static string EventSlideoutPanel_CoachingSession(int? coachingSessionId) {
        return $"{UrlPath.Partials}EventSlideoutPanel?{AbleUrlKeys.CoachingSessionId}={coachingSessionId.ToStringOrEmptyIfNull()}";
      }

      public static string AddParticipant_Singular(WebHelper.AddParticipantFrom addParticipantFrom, int addFromId) {
        if (addParticipantFrom == WebHelper.AddParticipantFrom.Company) {
          return $"{UrlPath.Partials}AddParticipant_Singular?{AbleUrlKeys.CompanyId}={addFromId}";
        } else if (addParticipantFrom == WebHelper.AddParticipantFrom.Program) {
          return $"{UrlPath.Partials}AddParticipant_Singular?{AbleUrlKeys.ProgramJobId}={addFromId}";
        }
        return Pages.Home();
      }

      public static string AddParticipant_FromFile(WebHelper.AddParticipantFrom addParticipantFrom, int addFromId) {
        if (addParticipantFrom == WebHelper.AddParticipantFrom.Company) {
          return $"{UrlPath.Partials}AddParticipant_FromFile?{AbleUrlKeys.CompanyId}={addFromId}";
        } else if (addParticipantFrom == WebHelper.AddParticipantFrom.Program) {
          return $"{UrlPath.Partials}AddParticipant_FromFile?{AbleUrlKeys.ProgramJobId}={addFromId}";
        }
        return Pages.Home();
      }

      public static string AddParticipant_QuickAdd_Existing(int programJobId) {
        return $"{UrlPath.Partials}AddParticipant_QuickAdd_Existing?{AbleUrlKeys.ProgramJobId}={programJobId}";
      }

      public static string AddParticipant_QuickAdd() {
        return $"{UrlPath.Partials}AddParticipant_QuickAdd";
      }

      public static string AddParticipantWizard() {
        return $"{UrlPath.Partials}AddParticipantWizard";
      }

      public static string CoacheeSendEmailModal(int coacheeId) {
        return $"{UrlPath.Partials}CoacheeSendEmailModal?{AbleUrlKeys.CoacheeId}={coacheeId}";
      }

      public static string ShareSurveyModal(string surveyUId, string partUId) {
        return $"{UrlPath.Partials}ShareSurveyModal?{AbleUrlKeys.SurveyUId}={surveyUId}&{AbleUrlKeys.PartUId}={partUId}";
      }

      public static string ShareSurveyModal(string surveyUId, string partUId, int invitedUserId) {
        return $"{UrlPath.Partials}ShareSurveyModal?{AbleUrlKeys.SurveyUId}={surveyUId}&{AbleUrlKeys.PartUId}={partUId}&{AbleUrlKeys.UserId}={invitedUserId}";
      }

      public static string PurchaseServices() {
        return $"{UrlPath.Partials}PurchaseServices";
      }

      public static string OrgSubscriptionUpdate(Guid subscriptionGuid) {
        return $"{UrlPath.Partials}OrgSubscriptionUpdate?{AbleUrlKeys.SubscriptionGuid}={subscriptionGuid.ToStringNoBraces()}";
      }

      public static string StripePaymentMethod() {
        return $"{UrlPath.Partials}StripePaymentMethod";
      }
    }

    public static class Reports {

      private const string OrgReport_SurveyUId_URLKey = "id";
      private const string OrgReport_PartUId_Delimiter = "-";

      public static string ParticipantPDFReport(DbHelper.ReportTypes.ReportTypeInfo reportType, int surveyId, string partUID) {

        if (reportType.ReportPathJarvisFolder.IsNullOrEmpty()) return null;

        return ConfigHelper.SiteDomain_Jarvis
          + $"/survey/reports/{reportType.ReportPathJarvisFolder}/?{JarvisUrlKeys.StCode}={JarvisUrlValues.StCode_360}"
          + $"&{AbleUrlKeys.SurveyId}={surveyId}&{JarvisUrlKeys.PDFReportPartUId}={partUID}";
      }

      public static string OldIOSReport(string surveyUniqueId, string partUniqueId = null, bool includeDomain = false) =>
          PageUrl($"OrgReport?{OrgReport_SurveyUId_URLKey}={surveyUniqueId}"
          + (partUniqueId.IsNullOrEmpty() ? "" : (OrgReport_PartUId_Delimiter + partUniqueId)), includeDomain);

      public static string Quality(bool includeDomain = false) => PageUrl($"Quality", includeDomain);

      public static string SurveyViewer(bool includeDomain = false) => PageUrl($"SurveyViewer", includeDomain);
      public static string SurveyViewer(DbHelper.ClientCompanies.SurveyViewerCompanyInfo company, bool includeDomain = false) => PageUrl($"SurveyViewer?{AbleUrlKeys.CompanyId}={(company == null ? "" : company.CompanyId.ToString())}", includeDomain);
      public static string SurveyViewer(int companyId, string reportTypeCode) => PageUrl($"SurveyViewer?{AbleUrlKeys.CompanyId}={companyId}&{AbleUrlKeys.SurveyReportType}={reportTypeCode.EmptyIfNull()}");
      public static string SurveyViewer(int companyId, string reportTypeCode, int surveyId, int intakeNumber) => PageUrl($"SurveyViewer?{AbleUrlKeys.CompanyId}={companyId}&{AbleUrlKeys.SurveyReportType}={reportTypeCode}&{AbleUrlKeys.SurveyId}={surveyId.ToStringOrEmptyIfNull()}&{AbleUrlKeys.SurveyIntakeNumber}={intakeNumber.ToStringOrEmptyIfNull()}");

      public static string EvalViewer(bool includeDomain = false) => PageUrl($"EvalViewer", includeDomain);
      public static string EvalViewer(DbHelper.AblePrograms.AbleProgramInfo program, ConfigHelper.EvalType evalType, bool includeDomain = false) => PageUrl($"EvalViewer?{AbleUrlKeys.ProjectJobNumber}={program.ProgramJobNumber}&{AbleUrlKeys.ProgramJobId}={program.ProgramJobId}&{AbleUrlKeys.EvalType}={evalType}", includeDomain);
      public static string EvalViewer(DbHelper.WorkshopEvents.WorkshopEventInfo workshop, bool includeDomain = false) => PageUrl($"EvalViewer?{AbleUrlKeys.ProjectJobNumber}={workshop.ProgramJobNumber}&{AbleUrlKeys.ProgramJobId}={workshop.ProgramJobId}&{AbleUrlKeys.EvalType}={ConfigHelper.EvalType.Workshop}&{AbleUrlKeys.SurveyIntakeCodeId}={workshop.EvalIntakeCodeId}");

      public static string SkillsViewer(bool includeDomain = false) => PageUrl("SkillsViewer", includeDomain);
      public static string SkillsViewer(DbHelper.Projects.ProjectInfo project, bool includeDomain = false) => PageUrl($"SkillsViewer?{AbleUrlKeys.ProjectJobNumber}={(project == null ? "" : project.JobNumber)}", includeDomain);
      public static string SkillsViewer(DbHelper.Projects.ProjectInfo project, List<int> programJobIds, bool includeDomain = false) => $"{SkillsViewer(project, includeDomain)}&{AbleUrlKeys.ProgramJobId}={programJobIds.ToStringList()}";

      public static string OrganisationIOSReports() => PageUrl($"OrganisationIOSReports");
      public static string OrganisationIOSReports(DbHelper.AlbertSurveys.SurveyInfo survey) => OrganisationIOSReports(survey.CompanyId ?? 0, survey.SurveyUID);
      public static string OrganisationIOSReports(int companyId, string surveyUId = null)
        => $"{OrganisationIOSReports()}?{AbleUrlKeys.CompanyId}={companyId}"
        + (surveyUId.IsNullOrEmpty() ? "" : $"&{AbleUrlKeys.SurveyUId}={surveyUId}");

      public static string InsightsIOSReports() => PageUrl($"InsightsIOSReports");
      public static string InsightsIOSReports(DbHelper.AlbertSurveys.SurveyInfo survey) => InsightsIOSReports(survey.CompanyId ?? 0, survey.SurveyUID);
      public static string InsightsIOSReports(int companyId, string surveyUId = null)
        => PageUrl($"InsightsIOSReports?{AbleUrlKeys.CompanyId}={companyId}"
          + (surveyUId.IsNullOrEmpty() ? "" : $"&{AbleUrlKeys.SurveyUId}={surveyUId}"));

      // See CoacheeReportPartialBase for def of "context coachee".
      public static string CoacheeSurvey(bool includeDomain = false) => PageUrl($"CoacheeSurveySummaryReport", includeDomain);
      public static string CoacheeSurvey(DbHelper.AlbertCoachees.AlbertCoacheeInfo contextCoacheeInfo, string surveyUid, string partUid, bool includeDomain = false) => PageUrl($"CoacheeSurveySummaryReport?{AbleUrlKeys.CoacheeId}={contextCoacheeInfo.CoacheeId}&{AbleUrlKeys.SurveyUId}={surveyUid}-{partUid}", includeDomain);
      public static string CoacheeSurvey(DbHelper.AlbertCoachees.AlbertCoacheeInfo contextCoacheeInfo, DbHelper.AlbertSurveys.SurveyInfo surveyInfo, bool includeDomain = false)
        => $"{CoacheeSurvey(includeDomain)}"
        + $"?{AbleUrlKeys.CoacheeId}={contextCoacheeInfo?.CoacheeId ?? surveyInfo.FoundParticipantBrief.CoacheeId}" // Fallback if coacheeInfo is null.
        + $"&{AbleUrlKeys.SurveyUId}={surveyInfo.SurveyUID}-{surveyInfo.FoundParticipantBrief?.PartUniqueId}";

      public static string CoacheeSurvey(DbHelper.SurveyShare.SharedSurveysInfo sharedSurveysInfo, bool includeDomain = false)
        => $"{CoacheeSurvey(includeDomain)}"
        + $"?{AbleUrlKeys.CoacheeId}={sharedSurveysInfo.SurveyInfo.CoacheeId}"
        + $"&{AbleUrlKeys.SurveyUId}={sharedSurveysInfo.SurveyInfo.SurveyUID}-{sharedSurveysInfo.SurveyInfo.PartUniqueId}"
        + $"&{AbleUrlKeys.SurveyShareId}={sharedSurveysInfo.SurveyShareId}";
    }

    public static class Endpoints {
      public static string JSLogger() => $"{UrlPath.Ajax}jslogger";
      public static string ProjectsForCompanyId(int? companyId) => $"{UrlPath.Ajax}AbleProjects?{AbleUrlKeys.CompanyId}={companyId.ToStringOrEmptyIfNull()}";
      public static string ProgramsForCompanyId(int? companyId) => $"{UrlPath.Ajax}AblePrograms?{AbleUrlKeys.CompanyId}={companyId.ToStringOrEmptyIfNull()}";
      public static string SetUserRole() => $"{UrlPath.Ajax}SetUserRole";
    }

    public static class PublicEndpoints {
      public static string CalendlySubmit() => PageUrl("calendly-submit", true);
    }

    public static bool IsExternalUrl(string url) {
      if (url.IsNullOrEmpty()) return false;
      return url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        && !url.StartsWith(ConfigHelper.SiteDomain, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCurrentPage(string url) {

      // Given url should be either PathHelper.Pages or PathHelper.Partials strings.
      // If we are currently running a partial, but the given url is NOT a partial url,
      // then we assume the caller wants to know what PAGE we are on, not what partial is loading.
      // In that case compare the given url to the REFERRER url (the page that called the partial).
      if (IsCurrentUrlPartial() && !IsUrlPartial(url)) {
        string referrerUrl = WebHelper.GetReferrerUri()?.ToString();
        if (referrerUrl.IsNullOrEmpty()) return false; // No referrer, no match.
        return IsSameUrl(referrerUrl, url);
      } else {
        return IsSameUrl(SystemWeb.RequestRawUrlNoQuery, url);
      }
    }

    public static bool IsCurrentUrlPartial() {
      return IsUrlPartial(SystemWeb.RequestRawUrlNoQuery);
    }

    private static bool IsUrlPartial(string url) {
      return url.ContainsIgnoreCase(UrlPath.Partials);
    }

    public static string CurrentUrl => SystemWeb.RequestRawUrl;

    private static bool IsSameUrl(string url1, string url2) {
      // Compare urls ignoring case and querystring.

      if (url1 == null || url2 == null) return false;

      int endPos1 = url1.IndexOf("?") - 1;
      if (endPos1 < 0) endPos1 = url1.Length - 1;
      if (url1[endPos1] == '/') endPos1--;

      int endPos2 = url2.IndexOf("?") - 1;
      if (endPos2 < 0) endPos2 = url2.Length - 1;
      if (url2[endPos2] == '/') endPos2--;

      if (endPos1 != endPos2) return false;

      return url1.Substring(0, endPos1 + 1).Equals(url2.Substring(0, endPos2 + 1), StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool IsParticipantPage(string url) {
      return IsSameUrl(url, Pages.ParticipantAICoach())
          || IsSameUrl(url, Pages.ParticipantCoaching())
          || IsSameUrl(url, Pages.ParticipantSurveys())
          || IsSameUrl(url, Pages.ParticipantUpcoming())
          || IsSameUrl(url, Pages.DevelopmentPlan())
          || IsSameUrl(url, Pages.SurveyQuestions())
          || IsSameUrl(url, Pages.ParticipantSurvey())
          || IsPageViewAsPartParticipant(url);
    }

    public static bool IsPageViewAsPartParticipant(string url) {
      if (url.IsNullOrEmpty()) return false;
      return WebHelper.GetQueryStringValue(AbleUrlKeys.PageViewAsRole) == AbleUrlValues.PageViewAsRole_Participant;
    }

    // The application context in which a partial is viewed,
    // mostly depends on the page(s) the partial is embedded on.
    public enum PartialViewContext { Unknown, ParticipantView }

    public static PartialViewContext GetPartialViewContext() {
      // Note HTTP request value "referrer" contains the URL of the page the partial was called from.

      if (IsReferrerPageURL(Pages.CoacheeEdit())
        || IsReferrerPageURL(Pages.DevelopmentPlanForm())
        || IsReferrerPageURL(Pages.PeopleDetails())) {

        return PartialViewContext.ParticipantView;
      }

      return PartialViewContext.Unknown;
    }

    public static bool IsCurrentUrlAPage() {
      return SystemWeb.RequestPhysicalPath.ContainsIgnoreCase(ServerPaths.Pages)
        || SystemWeb.RequestPhysicalPath.ContainsIgnoreCase("default.aspx");
    }

    public static bool IsReferrerPageURL(string pageURL) { // where pageURL is one of the PathHelper.Pages strings.
      pageURL = Regex.Replace(pageURL, "[?].*", "");
      var referrerUri = WebHelper.GetReferrerUri();
      if (referrerUri == null) return false;
      return referrerUri.AbsolutePath.Equals(pageURL, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetDefaultPostLoginURL(DbHelper.AbleUser.AbleUserInfo userInfo) {

      if (userInfo == null) {
        return Pages.Home();
      } else if (userInfo.IsIOSClientHR) {
        return Pages.IOSParticipants();
      } else if (userInfo.IsIOSReportViewer) {
        return Reports.OldIOSReport(userInfo.ViewOnlyIOSReportUniqueId);
      } else if (SessionHelper.IsUserRoleLeader && SessionHelper.AppAccess.Coaches.CanViewAIChat()) {
        return Pages.ParticipantAICoach();
      } else if (SessionHelper.AppAccess.PageAccess.CanAccessParticipantUpcoming()) {
        return Pages.ParticipantUpcoming();
      } else {
        return Pages.OverviewUpcoming();
      }
    }

    public static bool IsValidYoutubeUrl(string youtubeVideoUrl) {
      // If it's a valid YouTube URL, it should contain a video ID
      return !GetVideoIdFromYouTubeUrl(youtubeVideoUrl).IsNullOrEmpty();
    }

    public static string GetVideoIdFromYouTubeUrl(string youtubeVideoUrl) {

      if (youtubeVideoUrl.IsNullOrEmptyOrWhitespace()) {
        return "";
      }
      string videoId = "";

      try {

        // Check if the URL is from YouTube
        Uri uri = new Uri(youtubeVideoUrl);

        if (uri.Host.EqualsIgnoreCase(ConfigHelper.YoutubeUrls.LongFormat)) {
          // Extract the video ID from the query string (e.g., ?v=VIDEO_ID)
          var query = SystemWeb.ParseQueryString(uri.Query);
          videoId = query[ConfigHelper.YoutubeUrls.LongPathQueryVideoId];

        } else if (uri.Host.EqualsIgnoreCase(ConfigHelper.YoutubeUrls.ShortFormat)) {
          // Extract the video ID from the URL path (e.g., https://youtu.be/VIDEO_ID)
          videoId = uri.Segments.Last();
        }

      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(nameof(GetVideoIdFromYouTubeUrl))
          .WithProperty(ApplicationInsightsConstants.Url, youtubeVideoUrl)
          .Track();

        return videoId;
      }


      return videoId;
    }

    public class Content {

      public enum ContentFileType { Document, Image, CoverImage }

      public static Dictionary<ContentFileType, string> ContentFileTypeSuffixes = new Dictionary<ContentFileType, string>() {
        { ContentFileType.Document, "_document" },
        { ContentFileType.Image, "_image" },
        { ContentFileType.CoverImage, "_coverimage" },
      };

      public static string GetContentSuffix(ContentFileType fileType) => ContentFileTypeSuffixes[fileType];

      public static string ContentPhotoServerPath(Guid? contentGuid, ContentFileType contentFileType, ContentCoverImageSize photoSize) {
        return ServerPaths.Content + ContentPhotoFileName(contentGuid, contentFileType, photoSize);
      }

      public static string ContentFileServerPathAndName(Guid? contentGuid, ContentFileType contentFileType, string fileExtension) => ServerPaths.Content + ContentFileName(contentGuid, contentFileType) + fileExtension;
      public static string ContentFileName(Guid? contentGuid, ContentFileType contentFileType) => contentGuid.ToStringNoBracesOrDefault("x") + GetContentSuffix(contentFileType);

      internal static string ContentPhotoFileName(Guid? contentGuid, ContentFileType contentFileType, ContentCoverImageSize photoSize) {
        return $"{contentGuid.ToStringNoBracesOrDefault("x")}{GetContentSuffix(contentFileType)}{ContentCoverImageSizes[photoSize].FileSuffix}.{CoverImageFileTypeExt}";
      }

      public static string GetContentFile(Guid? contentGuid, ContentFileType contentFileType) {

        string imageSizeSuffix = "";
        if (contentFileType == ContentFileType.Image) {
          imageSizeSuffix = ContentCoverImageSizes[ContentCoverImageSize.Original].FileSuffix;
        }

        string searchPattern = ContentFileName(contentGuid, contentFileType) + imageSizeSuffix + ".*"; // Match any extension
        string physicalPath = ConfigHelper.UploadsFolderPath + UploadSubfolders.Content.ToString() + "\\";
        string webPath = UploadsWebRoot + UploadSubfolders.Content.ToString() + "/";
        string[] matchedFiles = Directory.GetFiles(physicalPath, searchPattern);
        string fileName = string.Empty;

        string foundResult = matchedFiles.FirstOrDefault();

        if (foundResult == null) {
          // If no file found return empty string
          return "";
        }

        // Construct webPath using the found file name (with extension)
        fileName = Path.GetFileName(foundResult);

        return $"{webPath}{fileName}";
      }

      public static void RemoveContentFiles(Guid? contentGuid, ContentFileType contentFileType) {
        string searchPattern = ContentFileName(contentGuid, contentFileType) + "*"; // Match any extension
        string physicalPath = ConfigHelper.UploadsFolderPath + UploadSubfolders.Content.ToString() + "\\";
        string[] matchedFiles = Directory.GetFiles(physicalPath, searchPattern);

        if (matchedFiles.IsNullOrEmpty()) return;

        foreach (string file in matchedFiles) {
          File.Delete(file);
        }
      }
    }

    private static void GetPhysicalUploadFilePath(UploadSubfolders folder, string fileName, out string physicalPath, out bool fileExists) {
      physicalPath = Path.Combine(ConfigHelper.UploadsFolderPath, folder.ToString(), fileName);
      fileExists = File.Exists(physicalPath);
    }

    private static string GetUploadFilePath(UploadSubfolders folder, string fileName, bool includeDomain, FileNotFoundReturn fileNotFoundReturn, string placeholderFilepath = null) {

      bool fileExists;
      string fileTimestamp = null;

      if (fileNotFoundReturn == FileNotFoundReturn.NoCheck) {
        // No existence check just return the filepath.

        fileExists = true;
        fileTimestamp = null;

      } else {
        // Check if file exists and get timestamp to use as a unique id to avoid caching replaced files.
        // Note this timestamp stuff won't be needed when files are moved to CDN.

        GetPhysicalUploadFilePath(folder, fileName, out string physicalPath, out fileExists);

        if (!fileExists) {
          if (fileNotFoundReturn == FileNotFoundReturn.UsePlaceholder) {
            return placeholderFilepath;
          } else if (fileNotFoundReturn == FileNotFoundReturn.EmptyString) {
            return string.Empty;
          }
        }

        fileTimestamp = File.GetLastWriteTime(physicalPath).ToFileTimeUtc().ToString();
      }

      string webPath = UploadsWebRoot + folder.ToString() + "/" + fileName;

      if (!fileTimestamp.IsNullOrEmpty()) {
        // Add the file's LastWriteTime as a parameter on the end of the path.
        // This makes the url different every time a new image is uploaded, so browsers don't show the old cached image.
        webPath += $"?{AbleUrlKeys.FileTimestamp}={fileTimestamp}";
      }

      return $"{(includeDomain ? ConfigHelper.SiteDomain : "")}{webPath}";
    }

    public class PDF {

      // Names the file after the public GUID of the Quote
      public static string QuoteSalesPDFFileName(DbHelper.AbleQuotes.QuoteInfo quoteItemInfo) {
        return quoteItemInfo.PublicGuid.ToStringNoBraces() + ".pdf";
      }

      // Used to retrieve the PDF URL
      // For Downloads or Displaying
      public static string QuoteSalesPDFUrl(DbHelper.AbleQuotes.QuoteInfo quoteItemInfo, bool includeDomain = false) {
        return GetUploadFilePath(UploadSubfolders.QuoteSalesPDFs, QuoteSalesPDFFileName(quoteItemInfo), includeDomain, FileNotFoundReturn.EmptyString);
      }

      public static void SavePDFToFile(DbHelper.AbleQuotes.QuoteInfo quoteItemInfo, IUploadedFile file) {
        GetPhysicalUploadFilePath(UploadSubfolders.QuoteSalesPDFs, QuoteSalesPDFFileName(quoteItemInfo), out string physicalFilePath, out bool fileExists);
        using (var source = file.OpenReadStream())
        using (var target = File.Create(physicalFilePath)) {
          source.CopyTo(target);
        }
      }
    }

    public class Images {

      private class Filename {
        internal const string AbleHeaderLogo = "able.svg";
        internal const string AbleFavicon = "Able-favicon-V8.png";
        internal const string OrgLogo_Default = "company-logo-default-able.png";
        internal const string OrgLogo_Missing = "company-logo-missing.png";
        internal const string UserLogo_Missing = "partner-profile-photo-missing-round.png";
        internal const string ContentImage_Missing = "content-img-missing.png";
        internal const string EmailFooterLogo = "id-email-logo-50-65.png";
        internal const string AbleEmailLogo = "able-logo-email.png";
        internal const string SpacerGif = "spacer.gif";
        internal const string AjaxLoader = "btn-ajax-loader.gif";
        internal const string BusyLoadSpinner200 = "busy-load-spinner-200.svg";
        internal const string DualRingSpinner100 = "dual-ring-spinner-100.svg";
        internal const string PDF_Icon = "pdf-icon.png";
        internal const string RightArrow = "right-arrow.svg";
        internal const string CompassIcon = "compass.svg";
        internal const string DocumentTextIcon = "document-text.svg";
        internal const string PeopleCircleIcon = "people-circle.svg";
        internal const string SchoolIcon = "school.svg";
        internal const string AIIcon = "sparkles.png";
        internal const string GoalIcon = "goal.svg";
        internal const string PuzzleIcon = "puzzle.svg";
        internal const string MicrolearningIcon = "learning.svg";
        internal const string ModuleIcon = "module.svg";
        internal const string SurveyIcon = "survey.svg";
        internal const string CoordinationTeamAvatar = "CoordinationTeam.png";
        internal const string DocumentPreview = "contentDocImg.png";
        internal const string ShareIcon = "share.svg";
        internal const string OrgReportHeatMapEmptyState = "OrgReportHeatMapEmptyState.png";
        internal const string EmptyStatePageIcon = "empty-state-icon.svg";
        internal const string ActionsArrowForwardIcon = "actions-arrow-forward-circle.svg";
        internal const string ProfileIconAction = "Profile-Icon.svg";
        internal const string CompanyIconAction = "Org-Company-Icon.svg";
        internal const string ProjectIconAction = "Projects-Icon.svg";
        internal const string WorkshopIconAction = "Workshop-Icon.svg";
        internal const string SupportIconAction = "Support-Icon.svg";
        internal const string TeamIconAction = "Team-Icon.svg";
        internal const string QuoteIconAction = "Quote.svg";
        internal const string PulseIcon = "pulse.svg";
        internal const string ContractIconAction = "Contract-Icon.svg";
        internal const string MicrolearningIconAction = "Microlearning.png";
      }

      private static string ImageUrl(string imageFilename, bool includeDomain = false) {
        return $"{includeDomain.ToValue(ConfigHelper.SiteDomain)}{UrlPath.Images}{imageFilename}";
      }

      private static string IconUrl(string imageFilename, bool includeDomain = false) {
        return $"{includeDomain.ToValue(ConfigHelper.SiteDomain)}{UrlPath.Icons}{imageFilename}";
      }

      // Blank pixel gif.
      public const string TransPixelGifData = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==";

      public static string AbleHeaderLogo(bool includeDomain = false) => ImageUrl(Filename.AbleHeaderLogo, includeDomain);
      public static string AbleFavicon(bool includeDomain = false) => ImageUrl(Filename.AbleFavicon, includeDomain);
      public static string AbleEmailLogo(bool includeDomain = false) => ImageUrl(Filename.AbleEmailLogo, includeDomain);
      public static string OrgLogo_Default(bool includeDomain = false) => ImageUrl(Filename.OrgLogo_Default, includeDomain);
      public static string OrgLogo_Missing(bool includeDomain = false) => ImageUrl(Filename.OrgLogo_Missing, includeDomain);
      public static string UserPhoto_Missing(bool includeDomain = false) => ImageUrl(Filename.UserLogo_Missing, includeDomain);
      public static string ContentImage_Missing(bool includeDomain = false) => ImageUrl(Filename.ContentImage_Missing, includeDomain);
      public static string SpacerGif(bool includeDomain = false) => ImageUrl(Filename.SpacerGif, includeDomain);
      public static string AjaxLoader(bool includeDomain = false) => ImageUrl(Filename.AjaxLoader, includeDomain);
      public static string DualRingSpinner100(bool includeDomain = false) => ImageUrl(Filename.DualRingSpinner100, includeDomain);
      public static string BusyLoadSpinner200(bool includeDomain = false) => ImageUrl(Filename.BusyLoadSpinner200, includeDomain);
      public static string CoordinationTeamAvatar(bool includeDomain = false) => ImageUrl(Filename.CoordinationTeamAvatar, includeDomain);
      public static string OrgReportHeatMapEmptyState(bool includeDomain = false) => ImageUrl(Filename.OrgReportHeatMapEmptyState, includeDomain);

      public static string AIIcon(bool includeDomain = false) => IconUrl(Filename.AIIcon, includeDomain);
      public static string CompassIcon(bool includeDomain = false) => IconUrl(Filename.CompassIcon, includeDomain);
      public static string DocumentTextIcon(bool includeDomain = false) => IconUrl(Filename.DocumentTextIcon, includeDomain);
      public static string PeopleCircleIcon(bool includeDomain = false) => IconUrl(Filename.PeopleCircleIcon, includeDomain);
      public static string PulseIcon(bool includeDomain = false) => IconUrl(Filename.PulseIcon, includeDomain);
      public static string SchoolIcon(bool includeDomain = false) => IconUrl(Filename.SchoolIcon, includeDomain);
      public static string GoalIcon(bool includeDomain = false) => IconUrl(Filename.GoalIcon, includeDomain);
      public static string PuzzleIcon(bool includeDomain = false) => IconUrl(Filename.PuzzleIcon, includeDomain);
      public static string MicrolearningIcon(bool includeDomain = false) => IconUrl(Filename.MicrolearningIcon, includeDomain);
      public static string ModuleIcon(bool includeDomain = false) => IconUrl(Filename.ModuleIcon, includeDomain);
      public static string SurveyIcon(bool includeDomain = false) => IconUrl(Filename.SurveyIcon, includeDomain);
      public static string DocumentPreview(bool includeDomain = false) => IconUrl(Filename.DocumentPreview, includeDomain);
      public static string ShareIcon(bool includeDomain = false) => IconUrl(Filename.ShareIcon, includeDomain);
      public static string EmptyStatePageIcon(bool includeDomain = false) => IconUrl(Filename.EmptyStatePageIcon, includeDomain);
      public static string ActionsArrowForwardIcon(bool includeDomain = false) => IconUrl(Filename.ActionsArrowForwardIcon, includeDomain);
      public static string ProfileIconAction(bool includeDomain = false) => IconUrl(Filename.ProfileIconAction, includeDomain);
      public static string CompanyIconAction(bool includeDomain = false) => IconUrl(Filename.CompanyIconAction, includeDomain);
      public static string ProjectIconAction(bool includeDomain = false) => IconUrl(Filename.ProjectIconAction, includeDomain);
      public static string WorkshopIconAction(bool includeDomain = false) => IconUrl(Filename.WorkshopIconAction, includeDomain);
      public static string SupportIconAction(bool includeDomain = false) => IconUrl(Filename.SupportIconAction, includeDomain);
      public static string TeamIconAction(bool includeDomain = false) => IconUrl(Filename.TeamIconAction, includeDomain);
      public static string QuoteIconAction(bool includeDomain = false) => IconUrl(Filename.QuoteIconAction, includeDomain);
      public static string ContractIconAction(bool includeDomain = false) => IconUrl(Filename.ContractIconAction, includeDomain);
      public static string MicrolearningIconAction(bool includeDomain = false) => IconUrl(Filename.MicrolearningIconAction, includeDomain);

      public enum UserPhotoSize { Original, Large, Thumbnail }

      public class UserPhotoSizeInfo {
        public string FileSuffix { get; private set; }
        public int Height { get; private set; }
        public UserPhotoSizeInfo(string fileSuffix, int height) {
          FileSuffix = fileSuffix;
          Height = height;
        }
      }

      private static readonly Dictionary<UserPhotoSize, UserPhotoSizeInfo> UserPhotoSizes = new Dictionary<UserPhotoSize, UserPhotoSizeInfo>() {
        { UserPhotoSize.Original, new UserPhotoSizeInfo($"_original", -1) },
        { UserPhotoSize.Large, new UserPhotoSizeInfo($"_large", ConfigHelper.UserProfilePhotoHeight_Large) },
        { UserPhotoSize.Thumbnail, new UserPhotoSizeInfo($"_thumb", ConfigHelper.UserProfilePhotoHeight_Thumb) }
      };

      private const string UserPhotoFileTypeExt = "jpg";

      public static UserPhotoSizeInfo GetUserPhotoSizeInfo(UserPhotoSize photoSize) => UserPhotoSizes[photoSize];

      public enum ContentCoverImageSize { Original, Card, DetailPage, Thumbnail }

      public class ContentCoverImageSizeInfo {
        public string FileSuffix { get; private set; }
        public int Height { get; private set; }
        public ContentCoverImageSizeInfo(string fileSuffix, int height) {
          FileSuffix = fileSuffix;
          Height = height;
        }
      }

      internal static readonly Dictionary<ContentCoverImageSize, ContentCoverImageSizeInfo> ContentCoverImageSizes = new Dictionary<ContentCoverImageSize, ContentCoverImageSizeInfo>() {
        { ContentCoverImageSize.Original, new ContentCoverImageSizeInfo($"_original", -1) },
        { ContentCoverImageSize.Card, new ContentCoverImageSizeInfo($"_card", ConfigHelper.ContentCoverImageHeight_Card) },
        { ContentCoverImageSize.DetailPage, new ContentCoverImageSizeInfo($"_detailPage", ConfigHelper.ContentCoverImageHeight_DetailPage) },
        { ContentCoverImageSize.Thumbnail, new ContentCoverImageSizeInfo($"_thumb", ConfigHelper.ContentCoverImageHeight_Thumb) }
      };

      public static int GetContentImageHeight(ContentCoverImageSize photoSize) => ContentCoverImageSizes[photoSize].Height;

      public const string CoverImageFileTypeExt = "png";



      public static void SaveStreamToUserPhoto(Stream imageStream, string firstName, string lastName) {

        // Save original uploaded image.
        string originalImagePath = UserPhotoServerPath(firstName, lastName, UserPhotoSize.Original);
        using (var fileStream = File.Create(originalImagePath)) {
          imageStream.CopyTo(fileStream);
        }

        // Save resized images.
        var encoderParamsForJpgQuality = GetEncoderParamsForJpgQuality(95);
        SaveResizedImage(originalImagePath, UserPhotoServerPath(firstName, lastName, UserPhotoSize.Thumbnail), UserPhotoSizes[UserPhotoSize.Thumbnail].Height, encoderParamsForJpgQuality);
        SaveResizedImage(originalImagePath, UserPhotoServerPath(firstName, lastName, UserPhotoSize.Large), UserPhotoSizes[UserPhotoSize.Large].Height, encoderParamsForJpgQuality);
      }

      public static void SaveStreamToContentImage(Stream imageStream, Guid? contentGuid, ContentFileType contentFileType) {

        string contentImagePath = ContentPhotoServerPath(contentGuid, contentFileType, ContentCoverImageSize.Original); // All versions except original of an Image type Content.

        using (var fileStream = File.Create(contentImagePath)) {
          imageStream.CopyTo(fileStream);
        }

        // Save resized images.
        var encoderParamsForJpgQuality = GetEncoderParamsForJpgQuality(95);
        SaveResizedImage(contentImagePath, ContentPhotoServerPath(contentGuid, contentFileType, ContentCoverImageSize.Card), GetContentImageHeight(ContentCoverImageSize.Card), encoderParamsForJpgQuality);
        SaveResizedImage(contentImagePath, ContentPhotoServerPath(contentGuid, contentFileType, ContentCoverImageSize.DetailPage), GetContentImageHeight(ContentCoverImageSize.DetailPage), encoderParamsForJpgQuality);

        if (contentFileType == ContentFileType.Image) {
          SaveResizedImage(contentImagePath, ContentPhotoServerPath(contentGuid, contentFileType, ContentCoverImageSize.Thumbnail), GetContentImageHeight(ContentCoverImageSize.Thumbnail), encoderParamsForJpgQuality);
        }
      }

      private static void SaveResizedImage(string sourceImagePath, string outputImageFileName, int photoSizeHeight, EncoderParameters encoderParamsForJpgQuality) {

        using (Image sourceImage = Image.FromFile(sourceImagePath)) {
          var newHeight = photoSizeHeight;
          var newWidth = GetResizedWidth(sourceImage, newHeight);
          using (var newImage = new Bitmap(newWidth, newHeight))
          using (var graphics = Graphics.FromImage(newImage)) {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(sourceImage, new Rectangle(0, 0, newWidth, newHeight));
            newImage.Save(outputImageFileName, GetImageEncoder(ImageFormat.Jpeg), encoderParamsForJpgQuality);
          }
        }
      }

      // From: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.imaging.encoderparameter?view=netframework-4.8.1
      private static EncoderParameters GetEncoderParamsForJpgQuality(long quality1to100) {
        if (quality1to100 < 1 || quality1to100 > 100) throw new ArgumentException(nameof(quality1to100), "Must be from 1 to 100");
        EncoderParameters encoderParameters = new EncoderParameters(1); // There is only one.
        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality1to100);
        return encoderParameters;
      }

      private static ImageCodecInfo GetImageEncoder(ImageFormat format) {
        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
        foreach (ImageCodecInfo codec in codecs) {
          if (codec.FormatID == format.Guid) {
            return codec;
          }
        }
        return null;
      }

      private static int GetResizedWidth(Image sourceImage, int requiredHeight) {
        return ((double)requiredHeight / sourceImage.Height * sourceImage.Width).RoundAwayFromZero();
      }

      public static string UserPhotoFileName(string firstName, string lastName, UserPhotoSize photoSize) {
        return $"{firstName}{lastName}{UserPhotoSizes[photoSize].FileSuffix}.{UserPhotoFileTypeExt}";
      }

      public static string UserPhotoServerPath(string firstName, string lastName, UserPhotoSize photoSize) {
        return ServerPaths.PartnerProfilePhotos + UserPhotoFileName(firstName, lastName, photoSize);
      }

      public static string UserPhoto(
        string firstName, string lastName, UserPhotoSize photoSize,
        FileNotFoundReturn fileNotFoundReturn, bool includeDomain = false) {

        return GetUploadFilePath(
          UploadSubfolders.PartnerProfilePhotos,
          UserPhotoFileName(firstName, lastName, photoSize),
          includeDomain, fileNotFoundReturn, UserPhoto_Missing(includeDomain));
      }

      public static string UserPhoto(string firstName, string lastName, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(firstName, lastName, photoSize,
          placeholderIfNotFound ? FileNotFoundReturn.UsePlaceholder : FileNotFoundReturn.EmptyString,
          includeDomain);
      }

      // Remove all these and leave one which uses UserIdentity.
      public static string UserPhoto(DbHelper.OrganisationUsers.ProfileInfo profileInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(profileInfo.FirstName, profileInfo.LastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(coachInfo.FirstName, coachInfo.LastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.CoachingSessions.AbleSessionInfo coachingSessionInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(coachingSessionInfo.CoachFirstName, coachingSessionInfo.CoachLastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.AblePrograms.ProgramTeamMember programTeamMember, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(programTeamMember.FirstName, programTeamMember.LastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.AbleUser.AbleUserBasicInfo userInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(userInfo.FirstName, userInfo.LastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(coacheeInfo.UserActivity.CoachFirstName, coacheeInfo.UserActivity.CoachLastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.WorkshopEvents.WorkshopEventInfo workshopFacilitatorInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(workshopFacilitatorInfo.KeyFacilitatorFirstName, workshopFacilitatorInfo.KeyFacilitatorLastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.WorkshopEvents.WorkshopEventInfo workshopFacilitatorInfo, UserPhotoSize photoSize, bool isCoFacilitator, bool placeholderIfNotFound, bool includeDomain = false) {
        if (isCoFacilitator) {
          return UserPhoto(workshopFacilitatorInfo.CoFacilitatorFirstName, workshopFacilitatorInfo.CoFacilitatorLastName, photoSize, placeholderIfNotFound, includeDomain);
        } else {
          return UserPhoto(workshopFacilitatorInfo.KeyFacilitatorFirstName, workshopFacilitatorInfo.KeyFacilitatorLastName, photoSize, placeholderIfNotFound, includeDomain);
        }
      }
      public static string UserPhoto(DbHelper.ConsultingItems.ConsultingItemInfo consultantInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(consultantInfo.ConsultantFirstName, consultantInfo.ConsultantLastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.AlbertCoachees.CoacheeListItem coacheeCoachItemInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(coacheeCoachItemInfo.UserActivity?.CoachFirstName, coacheeCoachItemInfo.UserActivity?.CoachLastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.AbleQuotes.QuoteInfo quoteItemInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(quoteItemInfo.OwnerFirstName, quoteItemInfo.OwnerLastName, photoSize, placeholderIfNotFound, includeDomain);
      }
      public static string UserPhoto(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo, UserPhotoSize photoSize, bool placeholderIfNotFound, bool includeDomain = false) {
        return UserPhoto(companyInfo.ClientLeadFirstName, companyInfo.ClientLeadLastName, photoSize, placeholderIfNotFound, includeDomain);
      }

      private static string UserProfilePhotoFileName(Guid userGuid) => userGuid.ToStringNoBraces() + ".jpg";

      public static string UserPhoto(Guid userGuid, bool placeholderIfNotFound, bool includeDomain = false) {
        return GetUploadFilePath(UploadSubfolders.UserProfilePhotos,
          UserProfilePhotoFileName(userGuid),
          includeDomain,
          placeholderIfNotFound ? FileNotFoundReturn.UsePlaceholder : FileNotFoundReturn.EmptyString,
          UserPhoto_Missing(includeDomain));
      }

      public static string ContentCoverImage(Guid? contentGuid, ContentCoverImageSize contentCoverImageSize, bool placeholderIfNotFound = true, bool includeDomain = false) {
        return GetUploadFilePath(UploadSubfolders.Content,
          ContentPhotoFileName(contentGuid, ContentFileType.CoverImage, contentCoverImageSize),
          includeDomain,
          placeholderIfNotFound ? FileNotFoundReturn.UsePlaceholder : FileNotFoundReturn.EmptyString,
          ContentImage_Missing(includeDomain));
      }

      public static string ContentImageFile(Guid? contentGuid, ContentCoverImageSize contentCoverImage, bool includeDomain = false) {
        string fileName = ContentPhotoFileName(contentGuid, ContentFileType.Image, contentCoverImage);
        return GetUploadFilePath(UploadSubfolders.Content, fileName, includeDomain,
          FileNotFoundReturn.UsePlaceholder,
          ContentImage_Missing(includeDomain));
      }

      private static string TenantOrgLogoFileName(Guid orgGuid) => orgGuid.ToStringNoBraces() + ".png";

      // TODO: Currently this path is also used for ClientCompany (aka SurveyCompany) logos, but they should be in a different folder to TenantOrg logos.
      //       At the momeent it works because they're both Guids so won't clash
      public static string TenantOrgLogoServerPath(Guid orgGuid) => ServerPaths.TenantOrgLogos + TenantOrgLogoFileName(orgGuid);

      public static string TenantOrgLogo(Guid? partnerComapnyGuid, Guid orgGuid, bool placeholderIfNotFound, bool includeDomain = false) {

        string logoFileName = null;
        bool logoFileFound = false;

        // First try partner company logo (for projects this is the "branding" company).
        // Currently, company and tenant logos are in the same folder - todo: separate them.
        if (partnerComapnyGuid != null) {
          logoFileName = TenantOrgLogoFileName(partnerComapnyGuid.Value);
          GetPhysicalUploadFilePath(UploadSubfolders.TenantOrgLogos, logoFileName, out _, out logoFileFound);
        }

        // If not found, then try the tenant org logo.
        if (!logoFileFound) {
          logoFileName = TenantOrgLogoFileName(orgGuid);
          GetPhysicalUploadFilePath(UploadSubfolders.TenantOrgLogos, logoFileName, out _, out logoFileFound);
        }

        if (!logoFileFound || logoFileName.IsNullOrEmpty()) {
          return placeholderIfNotFound ? OrgLogo_Missing(includeDomain) : OrgLogo_Default(includeDomain);
        }

        return GetUploadFilePath(
          UploadSubfolders.TenantOrgLogos,
          logoFileName,
          includeDomain,
          FileNotFoundReturn.UsePlaceholder,
          placeholderIfNotFound ? OrgLogo_Missing(includeDomain) : OrgLogo_Default(includeDomain));
      }

      public static string TenantOrgLogo(DbHelper.AbleUser.AbleUserInfo userInfo, bool placeholderIfNotFound, bool includeDomain = false) {
        return TenantOrgLogo(userInfo.CompanyGuid, userInfo.OrgGuid, placeholderIfNotFound, includeDomain);
      }
      public static string TenantOrgLogo(DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo, bool placeholderIfNotFound, bool includeDomain = false) {
        return TenantOrgLogo(coachInfo.OrgGuid, coachInfo.OrgGuid, placeholderIfNotFound, includeDomain);
      }
      public static string TenantOrgLogo(DbHelper.AbleQuotes.QuoteInfo quoteInfo, bool placeholderIfNotFound, bool includeDomain = false) {
        return TenantOrgLogo(quoteInfo.BrandingOrgGuid, quoteInfo.TenantOrgGuid, placeholderIfNotFound, includeDomain);
      }
      public static string TenantOrgLogo(DbHelper.TenantOrg.TenantOrgInfo orgInfo, bool placeholderIfNotFound, bool includeDomain = false) {
        return TenantOrgLogo(orgInfo.OrgGuid, orgInfo.OrgGuid, placeholderIfNotFound, includeDomain);
      }
      public static string TenantOrgLogo(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo, bool placeholderIfNotFound, bool includeDomain = false) {
        return TenantOrgLogo(companyInfo.CompanyGUID, companyInfo.CompanyGUID, placeholderIfNotFound, includeDomain);
      }

      private static string ProjectLogoFileName(string projectJobNumber) => projectJobNumber + "_logo.png";

      public static string ProjectLogoServerPath(string projectJobNumber) => ServerPaths.ProjectLogos + ProjectLogoFileName(projectJobNumber);

      public static string ProjectLogo(DbHelper.Projects.ProjectInfoBrief projectInfoBrief, bool placeholderIfNotFound, bool includeDomain = false) {
        if (projectInfoBrief == null) return "";
        return ProjectLogo(projectInfoBrief.JobNumber, projectInfoBrief.BrandingOrgGuid, projectInfoBrief.TenantOrgGuid, placeholderIfNotFound, includeDomain);
      }

      public static string ProjectLogo(string projectJobNumber, Guid? brandingOrgGuid, Guid orgGuid, bool placeholderIfNotFound, bool includeDomain = false) {

        if (brandingOrgGuid != null) {
          return TenantOrgLogo(brandingOrgGuid, orgGuid, placeholderIfNotFound, includeDomain);
        }

        return GetUploadFilePath(
          UploadSubfolders.ProjectLogos,
          ProjectLogoFileName(projectJobNumber),
          includeDomain,
          FileNotFoundReturn.UsePlaceholder,
          placeholderIfNotFound ? OrgLogo_Missing(includeDomain) : TenantOrgLogo(brandingOrgGuid, orgGuid, placeholderIfNotFound, includeDomain));
      }
    }

    public class QueryString {

      public static string GetValue(string completeQueryString, string key) {
        // Gets value from a querystring (e.g. "?x=y&z=a&...")
        if (key.IsNullOrEmpty()) return null;
        int keyPos = completeQueryString.IndexOf(key + "=", StringComparison.InvariantCultureIgnoreCase);
        if (keyPos < 0) return null;
        if (keyPos > 0 && completeQueryString.Substring(keyPos - 1, 1) != "?" && completeQueryString.Substring(keyPos - 1, 1) != "&") return null;
        int valuePos = keyPos + key.Length + 1;
        int valueEnd = completeQueryString.IndexOf("&", valuePos);
        if (valueEnd < 0) return completeQueryString.Substring(valuePos); // To end of string.
        return completeQueryString.Substring(valuePos, valueEnd - valuePos);
      }

      public static string GetValue(string key) {
        return WebHelper.GetQueryStringValue(key);
      }

      public static string GetStringOrEmpty(string key) {
        if (key.IsNullOrEmpty()) return "";
        string val = WebHelper.GetQueryStringValue(key);
        if (val.IsNullOrEmpty()) return "";
        else return val;
      }

      public static int GetSurveyIdOrZero() {
        int svid = 0;
        int.TryParse(GetStringOrEmpty(AbleUrlKeys.SurveyId), out svid);
        return svid;
      }

      public static int GetQnGroupIdOrZero() {
        int qgid = 0;
        int.TryParse(GetStringOrEmpty("qngroupid"), out qgid);
        return qgid;
      }
    }

    private static string PageUrl(string pageFile, bool includeDomain = false) {
      return $"{includeDomain.ToValue(ConfigHelper.SiteDomain)}{WebRoot}{pageFile}";
    }

    private static string PageUrlWithQueryId(string PageFile, string idkey, bool useIdFromQuery, bool includeDomain = false) {

      string url;

      if (PageFile.Contains("/")) {
        url = PageFile; // Assume it's already a proper url.
      } else {
        url = PageUrl(PageFile, includeDomain);
      }

      if (useIdFromQuery && !idkey.IsNullOrEmpty()) {
        url += url.Contains("?") ? "&" : "?";
        url += $"{idkey}={QueryString.GetStringOrEmpty(idkey)}";
      }

      return url;
    }

    // todo rename to PageUrlWithQueryId()
    private static string PathWithQueryIds(string PageFile, string idkey, string idkey2, bool useIdFromQuery, bool includeDomain = false) {

      string url = PageUrlWithQueryId(PageFile, idkey, useIdFromQuery, includeDomain);

      if (useIdFromQuery && !idkey2.IsNullOrEmpty()) {
        url += url.Contains("?") ? "&" : "?";
        url += $"{idkey2}={QueryString.GetStringOrEmpty(idkey2)}";
      }

      return url;
    }

    private static void EnsureUploadFoldersExist() {
      // These are all the server folders that point to external storage.
      Directory.CreateDirectory(ServerPaths.TenantOrgLogos);
      Directory.CreateDirectory(ServerPaths.PartnerProfilePhotos);
      Directory.CreateDirectory(ServerPaths.UserProfilePhotos);
      Directory.CreateDirectory(ServerPaths.ProjectLogos);
      Directory.CreateDirectory(ServerPaths.QuoteOwnerSignatures);
      Directory.CreateDirectory(ServerPaths.QuoteSalesPDFs);
      Directory.CreateDirectory(ServerPaths.UploadTemp);
      Directory.CreateDirectory(ServerPaths.Content);
    }

    private static string GetPageViewAsRole(PageViewAsRole viewAsRole) {

      if (viewAsRole == PageViewAsRole.Default) return "";

      string keyValue = $"&{AbleUrlKeys.PageViewAsRole}=";

      if (viewAsRole == PageViewAsRole.Participant) {
        return keyValue + AbleUrlValues.PageViewAsRole_Participant;
      }

      return "";
    }
  }
}
