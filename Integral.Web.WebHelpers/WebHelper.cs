using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Integral.Web.Services;
using Newtonsoft.Json;

// All usage of Request.Items goes here (except for session user object in SessionHelper).

namespace Integral.Web {

  public partial class WebHelper {

    public enum HttpContentType { None, Text, csv, gif, html, jpg, json, pdf, png, webp, zip }

    private static readonly IDictionary<HttpContentType, string> ContentTypeStr =
      new Dictionary<HttpContentType, string>() {
        [HttpContentType.None] = string.Empty,
        [HttpContentType.Text] = "text/plain",
        [HttpContentType.csv] = "text/csv",
        [HttpContentType.gif] = "image/gif",
        [HttpContentType.html] = "text/html",
        [HttpContentType.jpg] = "image/jpeg",
        [HttpContentType.json] = "application/json",
        [HttpContentType.pdf] = "application/pdf",
        [HttpContentType.png] = "image/png",
        [HttpContentType.webp] = "image/webp",
        [HttpContentType.zip] = "application/zip"
      };

    public static string GetContentTypeString(HttpContentType contentType) {
      if (ContentTypeStr.TryGetValue(contentType, out string ct)) return ct;
      return string.Empty;
    }

    // CSS classes used in code that are defined in adminlte-custom-2022.css.
    public static class CSSClasses {
      public const string AjaxFieldNamePrefix = "ajaxSubmit-fieldName-";
      public const string AjaxFieldErrorMsg = "ajaxSubmit-errorMsg";
      public const string ActionButtonHasText = "hastext";
      public const string ChatMessageBodyText = "chat-body-text";
      public const string Spinner = "spinner"; // General-purpose, used for several things.
      public const string PartnerDropdownClass = "partnerdropdownavatarimg";
      public const string UnshareSurveyClass = "unsharesurveybtn";
      public const string IconTooltip_RequiredField = "requiredField";
      public const string ShareDevPlan = "sharedevplan";
      public const string UpdateUI_Done = "UpdateUI_Done";
      public static class CoacheeList {
        public const string SurveyDateScheduled = "survey-date-scheduled";
        public const string SurveyDateOpen = "survey-date-open";
        public const string SurveyNotCompleted = "survey-not-completed";
        public const string SurveyCompleted = "survey-completed";
        public const string SendInviteToClient_BtnCssName = "btnSendInviteToClient";
      }
      public static class Images {
        public const string ProfileImage = "profile-image";
        public const string CompanyLogo = "company-logo";
      }
    }

    public const string DATE_OUTPUT_FORMAT = "d MMM yyyy";
    public const string DATE_OUTPUT_FORMAT_JS = "d M yyyy";
    public const string DATE_OUTPUT_FORMAT_MOMENTJS = "D MMM YYYY";
    public const string DefaultCheckboxValue = "1";
    public const string YesNoButton_ValueNo = "0";
    public const string YesNoButton_ValueYes = "1";
    public const string DefultHelpLinkHtml = @"<span class=""nowrap"">Learn more →</span>";

    internal const string TimePickerNameSuffix_Hour = "_hour";
    internal const string TimePickerNameSuffix_Mins = "_mins";
    internal const string TimePickerNameSuffix_AMPM = "_ampm";
    internal const string TimePickerAMValue = "am";
    internal const string TimePickerPMValue = "pm";
    private const int TimePickerMinsIncrement = 5;

    private class BootstrapCols {
      public const int Total = 12;
      public const int FormLabel_Legacy = 2;
      public const int FormContent_Legacy = 8;
      public const int FormLabel_lg = 3;      // Label Column size on Large Screens
      public const int FormContent_lg = 5;    // Element Column size on Large Screens
      public const int FormLabel_md = 4;      // Label Column size on Medium Screens
      public const int FormContent_md = 8;    // Element Column size on Medium Screens
    }

    public class ElementID {
      public const string SlideoutBackdrop = "slideout-backdrop";
      public const string SlideoutPanelHolder = "slideout-holder";
      public const string SlideoutPanelHeader = "slideout-header";
      public const string SlideoutPanelTitle = "slideout-title";
      public const string SlideoutPanelBody = "slideout-body";
      public const string ReportTabs = "ReportTabs";
    }

    public class DataAttrName {
      public const string RowLinkUrl = "rowlink-url";
      public const string RowLinkNewTab = "rowlink-newtab";
      public const string ModalPartialUrl = "modal-url";
      public const string ModalTitle = "modal-title";
      public const string ModalShowOnPageLoad = "modal-show-onpageload";
      public const string SlideoutTrigger = "slideout-trigger";
      public const string SlideoutTitle = "slideout-title";
      public const string SlideoutPartialUrl = "slideout-url";
      public const string SlideoutCallbackFunction = "slideout-callback-fn";
      public const string SlideoutShowOnPageLoad = "slideout-show-onpageload";
      public const string PartnerDropdownImgSrc = "partnerdropdownavatarimg";
      public const string CoacheeId = "coacheeid";
      public const string ProgramJobId = "programjobid";
      public const string SurveyShareId = "surveyshareid";
      public const string FormComponentID = "form-component-id";
      public const string AjaxDataType = "ajax-datatype";
      public const string AjaxAlternateUrl = "ajax-alternateurl";
      public const string AjaxFormData = "ajax-formdata";
      public const string AjaxSearchKey = "ajax-searchkey";
      public const string DialogRef = "dialogref";
      public const string AjaxAction = "ajax-action";
    }

    public class HtmlEntitySymbol {
      public const string PartnerActive = "&#128994;";
      public const string PartnerInactive = "&#x1F534;";
      public const string PartnerHidden = "&#x26D4;";
      public const string BulletPoint = "&#8226;";
    }

    public enum ActionButtonTypeEnum {
      edit, search, delete, invoiceItem, book, view, document, info, hidden,
      status, coachingIcon, workshopIcon, closed, survey_info, survey_report,
      survey, pdf, image, text, url, video, share, remove, apps, requiredField,
      save, learning, back, module
    }

    private static Dictionary<ActionButtonTypeEnum, string> ActionButtonIconName = new Dictionary<ActionButtonTypeEnum, string> {
      { ActionButtonTypeEnum.edit, "create-outline" },
      { ActionButtonTypeEnum.search, "search-outline"},
      { ActionButtonTypeEnum.delete, "trash-outline" },
      { ActionButtonTypeEnum.invoiceItem, "reader-outline" },
      { ActionButtonTypeEnum.book, "calendar-clear-outline" },
      { ActionButtonTypeEnum.view, "eye-outline" },
      { ActionButtonTypeEnum.document, "document-text-outline" },
      { ActionButtonTypeEnum.info, "information-circle-outline" },
      { ActionButtonTypeEnum.hidden, "eye-off-outline" },
      { ActionButtonTypeEnum.status, "radio-button-on-outline" },
      { ActionButtonTypeEnum.coachingIcon, "library-outline" },
      { ActionButtonTypeEnum.workshopIcon, "keypad-outline" },
      { ActionButtonTypeEnum.closed, "lock-closed-outline" },
      { ActionButtonTypeEnum.survey_info, "create-outline" },
      { ActionButtonTypeEnum.survey_report, Icon.SurveyReport.ClassName },
      { ActionButtonTypeEnum.survey, "document-text-outline" },
      { ActionButtonTypeEnum.pdf, Icon.PDF_outline.ClassName },
      { ActionButtonTypeEnum.image, "image-outline" },
      { ActionButtonTypeEnum.text, "text-outline" },
      { ActionButtonTypeEnum.url, "link-outline" },
      { ActionButtonTypeEnum.video, "videocam-outline" },
      { ActionButtonTypeEnum.share, "share-outline" },
      { ActionButtonTypeEnum.remove, "remove-circle-outline" },
      { ActionButtonTypeEnum.apps, "apps-outline" },
      { ActionButtonTypeEnum.requiredField, "medical" },
      { ActionButtonTypeEnum.save, "save-outline"},
      { ActionButtonTypeEnum.learning, "library-outline" },
      { ActionButtonTypeEnum.back, "arrow-back-outline" },
      { ActionButtonTypeEnum.module, "play-circle-outline" }
    };

    public enum MenuIconTypeEnum { Contact, Help, Hamburger, ChangeRole, Logout }

    public static Dictionary<MenuIconTypeEnum, string> MenuIconName = new Dictionary<MenuIconTypeEnum, string> {
      { MenuIconTypeEnum.Contact, "chatbubble-ellipses-outline" },
      { MenuIconTypeEnum.Help, "help-outline" },
      { MenuIconTypeEnum.Hamburger, "menu" },
      { MenuIconTypeEnum.ChangeRole, "people" },
      { MenuIconTypeEnum.Logout, "exit-outline" }
    };

    public enum EventDateDisplayFormat { Simple, TodayTomorrow }
    public enum TimeDisplayMinutes { No, Yes, IfAvailable }
    public enum PartialLoaderStyle { Default, Chart, Blank }
    public enum PageTabsStyle { Tabs, Links }
    public enum TargetNewTab { Yes, No }
    public enum ButtonSize { Normal, Small, XSmall }
    public enum ButtonStyle { None, Primary, Secondary }
    public enum SurveyViewerScoreBarType { Self, Rater }
    public enum ToolTipContentType { None, Text, ElementID }
    public enum ParticipantEventType { CoachingSession, Workshop, DefaultEvent, Microlearning, Module, Survey }
    public enum AddParticipantFrom { Program, Company, Invalid }
    public enum RowContentAlign { Left, Right }

    public enum HttpStatusEnum {
      None, Ok, NoContent, TemporaryRedirect, SeeOther, Unauthorized, Forbidden, NotFound, ServerError
    }

    private static Dictionary<HttpStatusEnum, int> httpStatusCodes = new Dictionary<HttpStatusEnum, int>() {
      [HttpStatusEnum.Ok] = 200,
      [HttpStatusEnum.NoContent] = 204,
      [HttpStatusEnum.TemporaryRedirect] = 302,
      [HttpStatusEnum.SeeOther] = 303,
      [HttpStatusEnum.Unauthorized] = 401,
      [HttpStatusEnum.Forbidden] = 403,
      [HttpStatusEnum.NotFound] = 404,
      [HttpStatusEnum.ServerError] = 500
    };

    public static int GetHttpStatusCode(HttpStatusEnum httpStatus) {
      if (httpStatusCodes.TryGetValue(httpStatus, out int statusCode)) {
        return statusCode;
      } else {
        throw new InvalidOperationException($"Code not found for httpStatus: {httpStatus}.");
      }
    }

    public enum InputMaxLength {
      NoLimit = 0,
      QuoteItemQuantity = 4,
      QuoteItemUnitType = 50,
      WorkshopTitle = 200,
      ContractReferralCode = 50,
      EmailName = 100,
      EmailAddress = 100,
      MobilePhoneNumber = 50,
      ContentSummary = 95
    }

    public class DataAttributes : Dictionary<string, object> {
      public DataAttributes(params (string Name, object Value)[] keyValueParams) {
        if (keyValueParams == null) return;
        foreach (var kv in keyValueParams) if (!kv.Name.IsNullOrEmpty()) this.Add(kv.Name, kv.Value);
      }
      public string ToHTML() => GetDataAttributes(this);
      public new string ToString() => GetDataAttributes(this);
    }

    public class TextInputSettings {
      public string Label { get; set; } = string.Empty;
      public string LabelNoteHtml { get; set; } = string.Empty;
      public string InputName { get; set; } = string.Empty;
      public string Value { get; set; } = string.Empty;
      public string Placeholder { get; set; } = string.Empty;
      public string InputClasses { get; set; } = string.Empty;
      public string InputAttributes { get; set; } = string.Empty;
      public int LabelCols { get; set; } = BootstrapCols.FormLabel_Legacy;
      public int InputCols { get; set; } = BootstrapCols.FormContent_Legacy;
      public string RightHtml { get; set; } = string.Empty;
      public bool IsReadOnly { get; set; } = false;
      public bool Autocomplete { get; set; } = false;
      public bool NoRow { get; set; } = false;
      public InputMaxLength MaxLength { get; set; } = InputMaxLength.NoLimit;
    }

    public class RowOptions {
      public string InputFieldName { get; set; } = string.Empty;
      public string RowClass { get; set; } = string.Empty;
      public string Label { get; set; } = string.Empty;
      public bool LabelIsHtml { get; set; } = false;
      public int? LabelCols { get; set; } = BootstrapCols.FormLabel_Legacy;
      public int? ContentCols { get; set; } = BootstrapCols.FormContent_Legacy;
      public string RightHtml { get; set; } = string.Empty;
      public RowContentAlign Align { get; set; } = RowContentAlign.Left;
      public RowOptions() { }
      public RowOptions(string labelHtml, int contentCols) {
        this.Label = labelHtml;
        this.LabelIsHtml = true;
        this.ContentCols = contentCols;
      }
    }

    public class ElementInfo {

      public string ID { get; set; } = string.Empty;
      public string Class { get; set; } = string.Empty;
      public string Style { get; set; } = string.Empty;
      public string Title { get; set; } = string.Empty;
      public DataAttributes DataAttributes { get; set; } = null;

      public void AddDataAttribute<T>(string key, T value) {
        if (DataAttributes == null) DataAttributes = new DataAttributes();
        DataAttributes.Add(key, value);
      }
    }

    public class FormElementInfo<T> : ElementInfo {
      public T Value { get; set; }
      public string InputName { get; set; } = string.Empty;
      public bool IsReadOnly { get; set; } = false;
    }

    public class CheckboxInfo : FormElementInfo<string> {

      public bool Checked { get; set; } = false;
      public string Text { get; set; } = string.Empty;
      public string CommentHtml { get; set; } = string.Empty;
    }

    public class DateInputInfo : FormElementInfo<DateTime?> {

      public TimeZoneInfo InputTimeZone { get; private set; }
      public DateInputInfo(string name, DateTime? dateTimeLocal) {
        this.InputName = name;
        this.Value = dateTimeLocal;
        this.InputTimeZone = SessionHelper.GetSessionTimeZone();
      }
    }

    public enum SelectWidth { Auto, Medium, Maximum } // Width percentage (0 is ignored)

    public class SelectInfo : FormElementInfo<string> {

      public bool NoSelect2 { get; set; } = false;
      public bool Multiple { get; set; } = false;
      public bool Select2WordWrap { get; set; } = false;
      public int? Size { get; set; } = 1;
      public string Placeholder { get; set; } = string.Empty;
      public string SearchPlaceholder { get; set; } = string.Empty;
      public string TopOptionsHtml { get; set; } = string.Empty;      // Options that are meant to be at the top.
      public SelectWidth Width = SelectWidth.Maximum;   // Default to max width

      public List<SelectOption> Options { get; set; } = new List<SelectOption>();

      public SelectInfo() { }

      public SelectInfo(string name, params SelectOption[] options) {
        this.InputName = name;
        this.Options = new List<SelectOption>(options);
      }
      public SelectInfo(string name, List<SelectOption> options) {
        this.InputName = name;
        this.Options = options;
      }
      public SelectInfo AddOption(string value, string text, bool selected = false) {
        this.Options.Add(new SelectOption(value, text, selected));
        return this;
      }
      public SelectInfo SetMultiple() {
        this.Multiple = true;
        return this;
      }
      public SelectInfo(string name, string optionsHtml, bool isReadOnly = false) {
        this.InputName = name;
        this.TopOptionsHtml = optionsHtml;
        this.IsReadOnly = isReadOnly;
      }
    }

    public class SelectOption<T> : ElementInfo {
      public T Value { get; set; }
      public string Text { get; set; } = string.Empty;
      public bool Selected { get; set; } = false;
      public SelectOption(T value, string text, bool selected = false) {
        this.Value = value;
        this.Text = text;
        this.Selected = selected;
      }
      public string ToHtml() {
        return $@"<option {AttrHtml("value", Value, RenderAttr.Always)} {Selected.ToValue("selected")}"
          + $@" {AttrHtml("id", ID, RenderAttr.IfHasValue)} {DataAttributes?.ToHTML()}"
          + $@" {AttrHtml("class", Class, RenderAttr.IfHasValue)}"
          + $@">{Text.HTMLEncode()}</option>";
      }
    }

    // Non-generic shortcut for the above for string value (for backward compatibility).
    public class SelectOption : SelectOption<string> {
      public SelectOption(string value, string text, bool selected = false) : base(value, text, selected) { }
    }

    public class Select2AjaxResult {
      public List<SelectOption> results { get; set; } = new List<SelectOption>();
      public Pagination pagination { get; set; } = new Pagination();
      public class SelectOption {
        public string id, text;
        public SelectOption(string _id, string _text) {
          id = _id;
          text = _text;
        }
      }
      public class Pagination {
        public bool more { get; set; } = false;
      }
    }

    public static class AjaxReturnValues {
      public const string AppErrorStackDump = "AppErrorStackDump";
      public const string IsAppException = "IsAppException";
      public const string NewSurveyId = "NewSurveyId";
      public const string SessionExpired = "SessionExpired";
    }

    public static string GetAjaxaction() {

      if (!SystemWeb.HasRequest) return null;

      string action = GetFormValue(PathHelper.FormKeys.AjaxAction).EmptyIfNull().Trim();

      if (action.IsNullOrEmpty()) {
        // Fallback to checking for AjaxAction in header, as image/file uploads (incl with FilePond) uses this instead of the form body.
        action = SystemWeb.GetRequestHeader(AppHelper.HttpHeaders.AjaxAction).EmptyIfNull().Trim();
      }

      return action;
    }

    public static Uri GetReferrerUri() {

      if (AppHelper.GetRequestItemOrNull(AppHelper.RequestItemKey.ReferrerUri) is Uri uri) return uri;

      // This custom header is sent with every jquery ajax (get or post) call.
      string referrerUrl = SystemWeb.GetRequestHeader(AppHelper.HttpHeaders.Referrer);

      // If custom header not present, fallback to browser referrer header (not reliable hence the custom header).
      if (referrerUrl.IsNullOrEmpty()) {
        referrerUrl = SystemWeb.GetReferrerUri()?.AbsolutePath;
      }
      // If referer(sic) not present, fallback to standard Origin header (note may not provide full url in some cases).
      if (referrerUrl.IsNullOrEmpty()) {
        referrerUrl = SystemWeb.GetRequestHeader("Origin");
      }

      Uri.TryCreate(referrerUrl, UriKind.Absolute, out uri);

      AppHelper.SetRequestItem(AppHelper.RequestItemKey.ReferrerUri, uri);

      return uri;
    }

    public static string GetRequestBody() {
      return SystemWeb.GetRequestBody();
    }

    public static bool QueryStringHasKey(string findKey) {
      return GetQueryStringValue(findKey) != null;
    }

    // Returns true if a valid value of int or "new" is found, and sets isNew and id out vars.
    public static bool TryGetQueryStringIdOrNew(string findKey, out int? id, out bool isNew) {
      id = null;
      isNew = false;
      string value = GetQueryStringValue(findKey);
      if (value.IsNullOrEmpty()) return false;
      if (value == PathHelper.AbleUrlValues.IdNew) {
        isNew = true;
        return true;
      }
      if (int.TryParse(value, out int idTemp)) {
        id = idTemp;
        return true;
      }
      return false;
    }

    public static int? GetQueryStringInt(string findKey, int? defaultValue = null) {
      if (int.TryParse(string.Empty + GetQueryStringValue(findKey), out int returnValue)) return returnValue;
      return defaultValue;
    }

    public static List<int> GetQueryStringIntList(string findKey, List<int> defaultIfInvalid = null) {
      try {
        return GetQueryStringValue(findKey, string.Empty).ToIntList();
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(nameof(GetQueryStringIntList))
          .WithProperty(ApplicationInsightsConstants.FindKey, findKey)
          .WithProperty(ApplicationInsightsConstants.QueryStringValue, GetQueryStringValue(findKey, string.Empty))
          .Track();
        return defaultIfInvalid;
      }
    }

    public static void QueryStringToEnum<T>(string findKey, out T result, T defaultValue) where T : struct, Enum {
      if (!Enum.TryParse(GetQueryStringValue(findKey) ?? string.Empty, out result)) result = defaultValue;
    }

    public static T GetQueryStringEnum<T>(string findKey, T defaultValue) where T : struct, Enum {
      T result;
      if (!Enum.TryParse(GetQueryStringValue(findKey) ?? string.Empty, out result)) result = defaultValue;
      return result;
    }

    // Get case-insensitive querystring key from a url string.
    public static string GetQueryStringValueFromUrl(string url, string findKey, string defaultValue = null) {
      if (url.IsNullOrEmpty() || !url.Contains("?")) return defaultValue;
      var urlParams = SystemWeb.ParseQueryString(url.Split('?')[1]);
      if (urlParams.ContainsKey(findKey)) return urlParams[findKey];
      return defaultValue;
    }

    public static string GetQueryStringValue(string findKey, string defaultValue = null) {
      string returnValue = SystemWeb.RequestQueryStringValue(findKey);
      if (returnValue == null) returnValue = SystemWeb.RequestQueryStringValue(findKey + "[]"); // Check if passed as an array.
      return returnValue ?? defaultValue;
    }

    public static string GetQueryStringSurveyUID(string findKey) {
      return DbHelper.AlbertSurveys.GetValidUniqueId(GetQueryStringValue(findKey));
    }

    // Used where a survey UID and intake number is passed together as a single string separated by "-", e.g. "wd829Djx-1".
    public static void GetQueryStringSurveyUIDAndIntakeNumber(string queryKey, out string surveyUID, out int intakeNumber) {

      surveyUID = null;
      intakeNumber = 0;

      string surveyUIdAndIntake = GetQueryStringValue(queryKey);

      if (surveyUIdAndIntake.IsNullOrEmpty()) return;

      surveyUIdAndIntake.SplitToStrings('-', out surveyUID, out string intakeNumberStr);

      intakeNumber = intakeNumberStr.ToIntOrDefault(ConfigHelper.DefaultSurveyIntakeNumber); // Default intake number if not provided.
    }

    // Returns true if a valid value of int or "new" is found, and sets isNew and id out vars.
    public static bool TryGetQueryStringGuidOrNew(string findKey, out Guid guid, out bool isNew) {
      guid = Guid.Empty;
      isNew = false;
      string value = GetQueryStringValue(findKey);
      if (value.IsNullOrEmpty()) return false;
      if (value == PathHelper.AbleUrlValues.IdNew) {
        isNew = true;
        return true;
      }
      if (Guid.TryParse(value, out Guid guidTemp)) {
        guid = guidTemp;
        return true;
      }
      return false;
    }

    public static Guid? GetQueryStringGuid(string findKey) {
      if (Guid.TryParse(GetQueryStringValue(findKey, string.Empty), out Guid guid)) return guid;
      return null;
    }

    public static bool TryGetQueryStringGuid(string findKey, out Guid guid) {
      return Guid.TryParse(GetQueryStringValue(findKey, string.Empty), out guid);
    }

    public static string GetFormValue(string fieldName, bool urlDecode = false) {
      return GetFormValue(fieldName, string.Empty, urlDecode);
    }

    public static int GetFormValueIntOrDefault(string findKey, int defaultValue) {
      int returnValue;
      if (int.TryParse(string.Empty + GetFormValue(findKey), out returnValue)) return returnValue;
      return defaultValue;
    }

    public static List<int> GetFormIntListOrDefault(string fieldName, List<int> defaultValue = null) {
      string value = GetFormValue(fieldName);
      if (value.IsNullOrEmpty() || value == "null") return defaultValue;
      return GetFormValue(fieldName).ToIntList();
    }

    public static string GetFormUIDList(string fieldName, string separator = ",") {
      string value = GetFormValue(fieldName, string.Empty);
      string regex = "^" + DbHelper.Participants.UniqueIdValidRegex + "(?:" + separator + DbHelper.Participants.UniqueIdValidRegex + ")*$";
      if (value.IsNullOrEmpty() || Regex.IsMatch(value, regex)) return value;
      return null;
    }

    public static string GetNextPageMessageText() {
      return SessionHelper.GetNextPageMessageText();
    }

    public static AjaxSubmitHelper.PageMessageType GetNextPageMessageType() {
      return SessionHelper.GetNextPageMessageType();
    }

    public static bool IsNextPageMessageTypeToast() {
      var messageType = GetNextPageMessageType();
      return messageType == AjaxSubmitHelper.PageMessageType.InfoToast
        || messageType == AjaxSubmitHelper.PageMessageType.SuccessToast
        || messageType == AjaxSubmitHelper.PageMessageType.ErrorToast;
    }

    public static string GetNextPageMessageTypeForBootstrapDialog() {
      var messageType = GetNextPageMessageType();
      switch (messageType) {
        case AjaxSubmitHelper.PageMessageType.InfoDialog:
          return "TYPE_INFO";
        case AjaxSubmitHelper.PageMessageType.SuccessDialog:
          return "TYPE_SUCCESS";
        case AjaxSubmitHelper.PageMessageType.ErrorDialog:
          return "TYPE_WARNING";
        default:
          return "TYPE_DEFAULT";
      }
    }

    public static void SetResponseStatus(HttpStatusEnum httpStatus) {
      if (SystemWeb.HasRequest) {
        SystemWeb.SetStatusCode(GetHttpStatusCode(httpStatus));
      }
    }

    public static void SetNextPageMessageText(string message) {
      SessionHelper.SetNextPageMessageText(message);
    }

    public static void AppendNextPageMessageText(string message) {
      SessionHelper.AppendNextPageMessageText(message);
    }

    public static void SetNextPageMessageType(AjaxSubmitHelper.PageMessageType pageMessageType) {
      SessionHelper.SetNextPageMessageType(pageMessageType);
    }

    public static void ClearNextPageMessageText() {
      SessionHelper.SetNextPageMessageText(string.Empty);
    }

    public static bool IsDatePickerFormat(string dateString) {
      return !dateString.IsNullOrEmpty() && Regex.IsMatch(dateString, "[0-9]{1,2} [a-zA-Z]{3,5} [0-9]{4}");
    }

    // Convert a date string from UI datepicker string format. Returns date as Kind=Unspecified.
    public static DateTime? GetDatePickerDateUnspecified(string datePickerString) {
      DateTime dt;
      string dateFormat = DATE_OUTPUT_FORMAT;
      if (datePickerString.Contains(":")) dateFormat += " hh:mm";
      if (!DateTime.TryParseExact(datePickerString, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dt)) return null;
      return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
    }

    public static string GetFormTimePicker(string fieldName) {
      // Return joined string from time picker dropdowns (hour, min, ampm).
      string hourValue = GetFormValue(fieldName + TimePickerNameSuffix_Hour, string.Empty);
      string minsValue = GetFormValue(fieldName + TimePickerNameSuffix_Mins, string.Empty);
      string ampmValue = GetFormValue(fieldName + TimePickerNameSuffix_AMPM, string.Empty);
      if (hourValue.IsNullOrEmpty() || minsValue.IsNullOrEmpty() || ampmValue.IsNullOrEmpty())
        return null;
      else
        return hourValue + ":" + minsValue + ampmValue;
    }

    public static TimeSpan? GetFormTimePickerSpan(string fieldName, out string hourVal, out string minsVal, out string ampmVal) {
      // Joined the separate hour, minute and am/pm strings.
      hourVal = GetFormValue(fieldName + TimePickerNameSuffix_Hour, string.Empty);
      minsVal = GetFormValue(fieldName + TimePickerNameSuffix_Mins, string.Empty);
      ampmVal = GetFormValue(fieldName + TimePickerNameSuffix_AMPM, string.Empty);
      if (!hourVal.IsNullOrEmpty() && !minsVal.IsNullOrEmpty() && !ampmVal.IsNullOrEmpty()) {
        DateTime dt;
        if (DateTime.TryParse(hourVal + ":" + minsVal + " " + ampmVal, out dt)) return dt.TimeOfDay;
      }
      return null;
    }

    // Output of dates

    public static string DateRangeForTable_UtcToUserTime(DateTime? fromDateUtc, DateTime? toDateUtc, bool utcToUserTime = false) {
      if (utcToUserTime) {
        fromDateUtc = SessionHelper.UtcToUserTime(fromDateUtc);
        toDateUtc = SessionHelper.UtcToUserTime(toDateUtc);
      }
      return DisplayDate(fromDateUtc, "-") + " - " + DisplayDate(toDateUtc, "-");
    }

    public static string ForDatePickerInput(DateTime? dateTimeToDisplay) {
      if (dateTimeToDisplay == null) return string.Empty;
      return dateTimeToDisplay.ToString(DATE_OUTPUT_FORMAT);
    }

    public static string DisplayDateTime(DateTime dateTimeToDisplay, TimeDisplayMinutes displayMinutes) {
      return $"{DisplayDate(dateTimeToDisplay)} {DisplayTime(dateTimeToDisplay, displayMinutes)}";
    }

    public static string DisplayDate(DateTime? dateToDisplay, string valueIfNull = "") {
      if (dateToDisplay == null) return valueIfNull;
      return DisplayDate(dateToDisplay.Value);
    }

    public static string DisplayDate(DateTime dateToDisplay) {
      return dateToDisplay.ToString(DATE_OUTPUT_FORMAT);
    }

    public static string DisplayDate_UtcToUserTime(DateTime? dateTimeUtc, string valueIfNull = "") {
      if (dateTimeUtc == null) return valueIfNull;
      return DisplayDate(SessionHelper.UtcToUserTime(dateTimeUtc.Value));
    }

    public static string DisplayTime(DateTime? dateTimeToDisplay, TimeDisplayMinutes displayMinutes, string valueIfNull = "") {
      if (dateTimeToDisplay == null) return valueIfNull;
      return DisplayTime(dateTimeToDisplay.Value, displayMinutes);
    }

    public static string DisplayTime(DateTime dateTimeToDisplay, TimeDisplayMinutes displayMinutes) {
      if (displayMinutes == TimeDisplayMinutes.Yes || (displayMinutes == TimeDisplayMinutes.IfAvailable && dateTimeToDisplay.Minute > 0)) {
        return dateTimeToDisplay.ToString("h:mm tt");
      }
      return dateTimeToDisplay.ToString("h tt");
    }

    private static string DisplayDateTimeForEvent(DateTime? eventDateUtc, string toTimeZoneIANA, EventDateDisplayFormat displayFormat, string valueIfNull = "") {
      if (eventDateUtc == null) return valueIfNull;
      return DisplayDateTimeForEvent(eventDateUtc.Value, toTimeZoneIANA, displayFormat);
    }

    private static string DisplayDateTimeForEvent(DateTime eventDateUtc, string toTimeZoneIANA, EventDateDisplayFormat displayFormat) {

      var eventDateLocal = eventDateUtc.UtcToTZId(toTimeZoneIANA);

      if (displayFormat == EventDateDisplayFormat.Simple) {
        // Simple format.
        return DisplayDateTime(eventDateLocal, TimeDisplayMinutes.Yes);
      }

      // Long format: "today", "tomorrow", weekday if next week, otherwise normal date.

      DateTime nowLocal = TimeHelper.UtcToTimeZoneId(DateTime.UtcNow, toTimeZoneIANA).Value.DateTime;
      int daysFromToday = (int)(eventDateLocal.Date - nowLocal.Date).TotalDays;

      if (daysFromToday == 0) {
        // Date is today, show time + "Today"
        return DisplayTime(eventDateLocal, TimeDisplayMinutes.IfAvailable) + " Today";
      }
      if (daysFromToday == 1) {
        // Date is tomorrow, show time + "Tomrrow"
        return DisplayTime(eventDateLocal, TimeDisplayMinutes.IfAvailable) + " Tomorrow";
      }
      if (daysFromToday > 1 && daysFromToday <= 7) {
        // Show the day of the week if it's within the same week
        return DisplayTime(eventDateLocal, TimeDisplayMinutes.IfAvailable) + " " + eventDateLocal.ToString("dddd"); // (e.g., "Monday")
      }
      // Otherwise normal date.
      return DisplayDate(eventDateLocal);
    }

    public static string GetSpan(string id, string classes, string innerHtml) {
      return $"<span class=\"{classes.HTMLEncode()}\""
        + (id.IsNullOrEmpty() ? string.Empty : $" id=\"{id.HTMLEncode()}\"")
        + $">{innerHtml}</span>";
    }

    public static string GetButton(
      string buttonText,
      string buttonID,
      bool disabled = false,
      ButtonStyle buttonStyle = ButtonStyle.Primary,
      ButtonSize buttonSize = ButtonSize.Normal,
      string extraClasses = ""
    ) {
      string html = string.Empty;

      html += "<button type=\"button\" class=\"btn";
      if (buttonStyle == ButtonStyle.Primary) {
        html += " btn-primary";
      } else if (buttonStyle == ButtonStyle.Secondary) {
        html += " btn-secondary";
      }
      if (buttonSize == ButtonSize.Small) {
        html += " btn-sm";
      } else if (buttonSize == ButtonSize.XSmall) {
        html += " btn-xsm";
      }
      if (!extraClasses.IsNullOrEmpty()) html += $" {extraClasses.HTMLEncode()}";
      html += "\""; // end of class

      if (!buttonID.IsNullOrEmpty()) html += $" id=\"{buttonID.HTMLEncode()}\"";
      if (disabled) html += $" disabled";

      html += $">{buttonText.HTMLEncode()}</button>";

      return html;
    }

    public static string GetWorkshopDropdownHtml(List<DbHelper.WorkshopEvents.WorkshopEventInfo> workshopList, string formName, int labelCols, int inputCols, bool isReadOnly) {

      if (workshopList.IsNullOrEmpty()) {
        return string.Empty;
      }

      var workshopOption = new List<SelectOption>();
      workshopOption.Add(new SelectOption(string.Empty, "[Select a workshop...]", true));

      foreach (var w in workshopList) {

        if (w.HideFromProgramContent) continue;

        workshopOption.Add(new SelectOption(w.WorkshopEventId.ToString(), w.WorkshopTitle));
      }

      return GetSelectRow("Workshop:", formName, labelCols, inputCols, GetSelectOptionListHtml(workshopOption), string.Empty, isReadOnly);
    }

    public static string GetContentDropdownForEmail(List<DbHelper.Content.ProgramContentInfo> contentList, string formName, int labelCols, int inputCols) {

      if (contentList.IsNullOrEmpty()) {
        return string.Empty;
      }

      contentList.OrderBy(x => x.ProgramContentId != null).ThenBy(x => x.ContentInfo.ContentTitle);

      var contentOption = new List<SelectOption>();
      contentOption.Add(new SelectOption(string.Empty, "[Select a microlearning...]", true));

      foreach (var ci in contentList) {
        string optionText = $"[{ci.ContentInfo.ContentTypeName}] {ci.ContentInfo.ContentTitle} - {ci.ContentInfo.ContentSummary}".LimitLengthTo(80, "...");
        contentOption.Add(new SelectOption(ci.ContentInfo.ContentId.ToString(), optionText));
      }

      return GetSelectRow("Microlearning:", formName, labelCols, inputCols, GetSelectOptionListHtml(contentOption), string.Empty, true);
    }

    public static string GetAddParticipantDropdownButton(AddParticipantFrom addParticipantFrom, int addFromId, bool canSendInviteToAddParticipants = false) {
      string dropdownItemsHtml = string.Empty;
      bool isModal = true;

      dropdownItemsHtml += GetDropdownButtonItem(PathHelper.Partials.AddParticipant_Singular(addParticipantFrom, addFromId), "Add Single Participant", isModal);
      dropdownItemsHtml += GetDropdownButtonItem(PathHelper.Partials.AddParticipant_FromFile(addParticipantFrom, addFromId), "Add Participants From File", isModal);

      if (addParticipantFrom == AddParticipantFrom.Program) {
        // Only display if the button is placed in a program page.
        dropdownItemsHtml += GetDropdownButtonItem(PathHelper.Partials.AddParticipant_QuickAdd_Existing(addFromId), "Quick Add Existing Participants", isModal);

        // Only display if SessionHelper.AppAccess.Programs.CanSendInviteToAddParticipants is true
        if (canSendInviteToAddParticipants) {
          dropdownItemsHtml += $"<a class=\"dropdown-item {CSSClasses.CoacheeList.SendInviteToClient_BtnCssName}\">Invite Client to Add Participants</a>";
        }
      }

      return GetDropdownButton("Add Participant", dropdownItemsHtml, "add-participant-dropdown");
    }

    private static string GetDropdownButton(string buttonText, string dropdownItemsHtml, string cssClass) {
      return $@"
        <div class=""{cssClass}"">
          <div class=""btn-group"">
            <button class=""btn btn-primary dropdown-toggle"" type=""button"" id=""dropdownMenuButton"" data-toggle=""dropdown"" aria-haspopup=""true"" aria-expanded=""false"">
              {buttonText}
            </button>
            <div class=""dropdown-menu dropdown-menu-right"" aria-labelledby=""dropdownMenuButton"">
              {dropdownItemsHtml}
            </div>
          </div>
        </div>";
    }

    private static string GetDropdownButtonItem(string addPaxPath, string itemText, bool isModal) {
      if (isModal) {
        return $"<a class=\"dropdown-item\" href=\"#\" data-addpaxurl=\"{addPaxPath}\" data-modaltitle=\"{itemText}\">{itemText}</a>";
      } else {
        return $"<a class=\"dropdown-item\" href=\"{addPaxPath}\">{itemText}</a>";
      }
    }

    public class ButtonGroupButton {
      public string ButtonText { get; private set; }
      public string Value { get; private set; }
      public DataAttributes DataAttributes { get; private set; }
      public ActionButtonTypeEnum ButtonIcon { get; private set; }
      public ButtonGroupButton(string buttonText, string value, DataAttributes dataAttributes = null, ActionButtonTypeEnum buttonIcon = default) {
        ButtonText = buttonText;
        Value = value;
        DataAttributes = dataAttributes;
        ButtonIcon = buttonIcon;
      }
    }

    public static string GetButtonGroupClass(string fieldName) => $"btnGroup_{fieldName.HTMLEncode()}";

    public static string GetButtonGroup(string fieldName, List<ButtonGroupButton> buttonList, string selectedValue, bool isReadOnly = false) {

      return $@"<div class=""btn-group btn-group-toggle {GetButtonGroupClass(fieldName)}"" data-toggle=""buttons"">{GetButtonGroupButtons(fieldName, buttonList, selectedValue, isReadOnly)}</div>";
    }

    public static string GetButtonGroup(string labelHtml, string fieldName, List<ButtonGroupButton> buttonList,
      string selectedValue, bool isReadOnly = false, string rightHtml = "",
      int? labelColumns = BootstrapCols.FormLabel_Legacy, bool preventSelectDefault = false, string customClass = "") {

      int inputCols = BootstrapCols.Total - labelColumns.GetValueOrDefault(BootstrapCols.FormLabel_Legacy);

      return GetButtonGroupHtml(fieldName, labelHtml,
        GetButtonGroupButtons(fieldName, buttonList, selectedValue, isReadOnly, preventSelectDefault),
        isReadOnly ? "disabled" : string.Empty,
        GetInputRightHtml(labelColumns, inputCols, rightHtml),
        labelColumns,
        inputCols,
        customClass
      );
    }

    private static string GetButtonGroupHtml(string formFieldName, string labelText, string buttons, string disabledCss, string rightHtml,
      int? labelColumns = BootstrapCols.FormLabel_Legacy, int? inputColumns = BootstrapCols.Total, string customClass = "") {
      return $@"
        <div class=""form-group ajaxSubmit-field row {GetRowClassForName(formFieldName)} {customClass}"" data-for=""{formFieldName}"">
          <label class=""control-label col-md-{labelColumns} col-sm-12 col-xs-12"">{labelText}</label>
          <div class=""col-md-{inputColumns} col-sm-12 col-xs-12"">
            <div class=""flex flex-align-center"">
              <div class=""btn-group btn-group-toggle {GetButtonGroupClass(formFieldName)} {disabledCss}"" data-toggle=""buttons"">
                {buttons}
              </div>
              <div class=""flex1"">{rightHtml}</div>
            </div>
          </div>
        </div>";
    }

    public static string GetButtonGroupButtons(string fieldName, List<ButtonGroupButton> buttonList, string selectedValue, bool isReadOnly = false, bool preventSelectDefault = false) {

      var buttonHtml = new StringBuilder();
      int itemCount = 0;
      int blankValueCount = 0;
      bool isSelected;

      if (buttonList.IsNullOrEmpty()) throw new InvalidOperationException("Button List is empty.");
      if (fieldName.IsNullOrEmpty()) throw new InvalidOperationException("fieldName is blank.");

      foreach (var button in buttonList) {

        if (button.Value.IsNullOrEmpty()) {
          blankValueCount++;
          if (blankValueCount > 1) throw new InvalidOperationException("Button List - more than 1 item value is blank.");
        }

        if (selectedValue.IsNullOrEmpty()) {
          isSelected = itemCount == 0 && !preventSelectDefault ? true : false;
        } else {
          isSelected = (button.Value ?? string.Empty).Equals(selectedValue, StringComparison.OrdinalIgnoreCase);
        }

        string buttonIcon = button.ButtonIcon != default ? GetIconHtml(button.ButtonIcon, "mr5") : string.Empty;
        string id = fieldName + "-" + button.Value;

        buttonHtml.Append($@"<label for=""{id}"" class=""btn {(isSelected ? " active" : string.Empty)}"">"
          + $@"<input id=""{id}"" type=""radio"" name=""{fieldName.HTMLEncode()}"" value=""{button.Value.HTMLEncode()}"" {(button.DataAttributes?.ToHTML() ?? string.Empty)}"
          + $@" autocomplete=""off"" {(isSelected ? " checked" : string.Empty)} {(isReadOnly ? "readonly" : string.Empty)} />"
          + buttonIcon
          + $@"{button.ButtonText.HTMLEncode()}</label>");

        itemCount++;
      }

      return buttonHtml.ToString();
    }

    public static string GetYesNoButtons(string labelHtml, string formName, bool? selectedFalseNoTrueYes, bool isReadOnly = false, string rightHtml = "") {

      string selectedValue = null;
      if (selectedFalseNoTrueYes == false) selectedValue = YesNoButton_ValueNo;
      else if (selectedFalseNoTrueYes == true) selectedValue = YesNoButton_ValueYes;

      return GetButtonGroup(labelHtml, formName,
        new List<WebHelper.ButtonGroupButton>() {
            new WebHelper.ButtonGroupButton("No", YesNoButton_ValueNo),
            new WebHelper.ButtonGroupButton("Yes",YesNoButton_ValueYes)
        },
        selectedValue, isReadOnly, rightHtml);
    }

    public static string GetTimePickerRow(string labelHtml, string fieldName, DateTime? displayTime, string rightHtml = "", bool isReadOnly = false) {

      int labelCols = BootstrapCols.FormLabel_Legacy;
      int inputCols = 3;
      int? displayHour, displayMins;
      string displayAMPM;

      if (displayTime == null) {
        displayAMPM = string.Empty;
        displayHour = null;
        displayMins = null;
      } else {
        displayAMPM = displayTime.Value.Hour < 12 ? TimePickerAMValue : TimePickerPMValue;
        if (displayTime.Value.Hour > 12) displayHour = displayTime.Value.Hour - 12;
        else if (displayTime.Value.Hour == 0) displayHour = 12;
        else displayHour = displayTime.Value.Hour;
        displayMins = displayTime.Value.Minute;
      }

      string options_hour = string.Empty;
      for (var hour = 0; hour <= 12; hour++) { // 0 = no value
        options_hour += "<option";
        if (displayHour != null && hour == displayHour) options_hour += " selected";
        options_hour += " value=\"" + (hour == 0 ? "" : hour.ToString()) + "\">" + (hour == 0 ? "" : hour.ToString()) + "</option>";
      }
      string options_mins = string.Empty;
      bool foundMins = false;
      for (var mins = -TimePickerMinsIncrement; mins < 60; mins += TimePickerMinsIncrement) {
        options_mins += "<option";
        if (!foundMins && displayMins != null && mins >= displayMins) { options_mins += " selected"; foundMins = true; }
        options_mins += " value=\"" + (mins == -TimePickerMinsIncrement ? "" : mins.ToString()) + "\">" + (mins == -TimePickerMinsIncrement ? "" : mins.ToString("00")) + "</option>";
      }

      string options_ampm
        = "<option" + (displayAMPM == TimePickerAMValue ? " selected" : string.Empty) + " value=\"" + TimePickerAMValue + "\">" + TimePickerAMValue + "</option>"
        + "<option" + (displayAMPM == TimePickerPMValue ? " selected" : string.Empty) + " value=\"" + TimePickerPMValue + "\">" + TimePickerPMValue + "</option>";

      return $@"
        <div class=""form-group ajaxSubmit-field row {GetRowClassForName(fieldName)} timePickerRow"">
          <label class=""control-label col-md-{labelCols} col-sm-12 col-xs-12"">{labelHtml}</label>
          <div class=""col-md-{12 - labelCols} col-timepicker"">
            <div class=""timePickerFlex"">
              <select data-minimumresultsforsearch=""1"" data-dropdowncssclass=""w100"" class=""form-control control-timepicker timepicker-hour"" size=""1"" name=""{fieldName + TimePickerNameSuffix_Hour}"" {(isReadOnly ? "readonly" : "")}>{options_hour}</select>
              <select data-minimumresultsforsearch=""1"" data-dropdowncssclass=""w100"" class=""form-control control-timepicker timepicker-minute"" size=""1"" name=""{fieldName + TimePickerNameSuffix_Mins}"" {(isReadOnly ? "readonly" : "")}>{options_mins}</select>
              <select data-minimumresultsforsearch=""1"" data-dropdowncssclass=""w100"" class=""form-control control-timepicker timepicker-ampm"" size=""1"" name=""{fieldName + TimePickerNameSuffix_AMPM}"" {(isReadOnly ? "readonly" : "")}>{options_ampm}</select>
              {GetInputRightHtml(labelCols, inputCols, rightHtml)}
            </div>
            <div class=""{CSSClasses.AjaxFieldErrorMsg}""></div>
          </div>
        </div>";
    }

    public static string GetAmountCurrencyFormat(decimal? amountToFormat, bool roundToDollars = false) {
      return amountToFormat.GetValueOrDefault(0).ToString($"C{(roundToDollars ? "0" : string.Empty)}", CultureInfo.CurrentCulture);
    }

    public enum PartnerDropdownSelect { Single, Multiple }
    public enum PartnerDropdownPurpose { Regular, TeamUserSelection, AssignCoachForParticipant, ContentAuthor }

    public class PartnerDropdownInfo {
      public string FormName;
      public string CssClasses = null;
      public bool IsReadOnly = false;
      public string LabelText = null;
      public string RightHtml = null;
      public int? InputCols = null;
      public int? SelectedPartnerUserId = ConfigHelper.UserId.Unassigned;
      public bool CanViewHiddenPartners = false;
      public bool CanViewInactivePartners = false;
      public bool IncludeUnassignedUser = false;
      public bool CoacheeHas360WithRatersForCoachAllocation = false;
      public List<int> TeamMemberIdList = null;
      public DataAttributes DataAttrs = null;
      public PartnerDropdownSelect DropdownSelect = PartnerDropdownSelect.Single;
      public PartnerDropdownPurpose DropdownPurpose = PartnerDropdownPurpose.Regular;
      public List<DbHelper.AlbertCoaches.AlbertCoachInfo> PartnerInfoList;
    }

    public static string GetPartnerDropdownOptionsHtml(PartnerDropdownInfo dropdownInfo) {

      if (dropdownInfo.PartnerInfoList == null || dropdownInfo.PartnerInfoList.Count == 0) {
        return string.Empty;
      }

      if (!dropdownInfo.IncludeUnassignedUser) {
        dropdownInfo.PartnerInfoList.RemoveAll(x => x.UserId == ConfigHelper.UserId.Unassigned);
      }

      // If selectedPartner is null or doesn't exist in the list, set it to unnasigned.
      if ((dropdownInfo.SelectedPartnerUserId == null || !dropdownInfo.PartnerInfoList.Exists(x => x.UserId == dropdownInfo.SelectedPartnerUserId)) && dropdownInfo.TeamMemberIdList == null) {
        dropdownInfo.SelectedPartnerUserId = ConfigHelper.UserId.Unassigned;
      }

      // Sort Partner list -> First show users in the current user's OrgId. Always alphabetically.
      int userOrgId = SessionHelper.UserInfo.OrgId;
      var sortedPartnerList = dropdownInfo.PartnerInfoList
        .OrderBy(p => p.OrgId == userOrgId ? 0 : 1)
        .ThenBy(p => p.FirstName)
        .ThenBy(p => p.LastName)
        .ToList();

      // Ensure "unassigned" is at the top.
      var unassignedUserIndex = sortedPartnerList.FindIndex(x => x.UserId == ConfigHelper.UserId.Unassigned);
      if (unassignedUserIndex > 0) {
        var unassignedUser = sortedPartnerList[unassignedUserIndex];
        sortedPartnerList.RemoveAt(unassignedUserIndex);
        sortedPartnerList.Insert(0, unassignedUser);
      }

      var html = new StringBuilder();
      if (dropdownInfo.SelectedPartnerUserId == ConfigHelper.UserId.Unassigned && !dropdownInfo.PartnerInfoList.Exists(x => x.UserId == ConfigHelper.UserId.Unassigned)) {
        html.Append(@"<option value="""">[Select Team Member]</option>");
      }

      bool orgSeparationWasAdded = false; // Dropdown option that functions as separator
      int itemCount = 0;

      foreach (var pi in sortedPartnerList) {

        if (dropdownInfo.TeamMemberIdList != null && dropdownInfo.TeamMemberIdList.Contains(pi.UserId)) {
          dropdownInfo.SelectedPartnerUserId = pi.UserId;
        }

        bool isSelected = pi.UserId == dropdownInfo.SelectedPartnerUserId;
        bool isDisabled = false;

        // Skip users that can't be viewed, unless if it's the currently selected one.
        if (!isSelected) {
          if (pi.IsProfileHidden && !dropdownInfo.CanViewHiddenPartners) continue;
          if (!pi.IsPartnerActive && !dropdownInfo.CanViewInactivePartners) continue;
        }

        // Add separador if this is the first user that doesn't belong to the same OrgId
        if (!orgSeparationWasAdded && itemCount > 0 && pi.OrgId != userOrgId) {
          html.Append(@"<option class=""dropdown-separator"" value="""" disabled></option>");
          orgSeparationWasAdded = true;
        }

        // TODO: Instead of this special case, PartnerDropdownInfo should include a bool "DisableInactiveItems".
        if (dropdownInfo.DropdownPurpose == PartnerDropdownPurpose.TeamUserSelection && !pi.IsPartnerActive) {
          isDisabled = true;
        }

        if (dropdownInfo.DropdownPurpose == PartnerDropdownPurpose.AssignCoachForParticipant) {
          if (!SessionHelper.AppAccess.Coaches.CanBeAssignedAsCoachForParticipant(pi, dropdownInfo.CoacheeHas360WithRatersForCoachAllocation)) {
            isDisabled = true;
          }
        }

        html.Append("<option");

        if (isSelected) {
          html.Append(" selected");
        } else if (isDisabled) {
          html.Append(" disabled");
        }

        html.Append($" value=\"{pi.UserId}\"");
        html.Append($" data-{CSSClasses.PartnerDropdownClass}=\"{PathHelper.Images.UserPhoto(pi, PathHelper.Images.UserPhotoSize.Thumbnail, true)}\"");
        html.Append($" {dropdownInfo.DataAttrs?.ToHTML()}");

        html.Append($">{pi.GetFullName().HTMLEncode()}");

        // Not applying icons to unnasigned to not confuse users as it's always selectable in dropdows.
        if (pi.UserId != ConfigHelper.UserId.Unassigned) {
          if (!pi.IsPartnerActive) {
            html.Append(" " + HtmlEntitySymbol.PartnerInactive);
          }
          if (pi.IsProfileHidden) {
            html.Append(" " + HtmlEntitySymbol.PartnerHidden);
          }
        }

        html.Append("</option>");

        itemCount++; // Keep track of items. Used as condition to add a separator in dropdown.
      }

      return html.ToString();
    }

    public static string GetPartnerDropdown(PartnerDropdownInfo dropdownInfo) {

      string dropdownOptionsHtml = GetPartnerDropdownOptionsHtml(dropdownInfo);

      var selectInfo = new SelectInfo() {
        IsReadOnly = dropdownInfo.IsReadOnly,
        InputName = dropdownInfo.FormName,
        Class = CSSClasses.PartnerDropdownClass,
        TopOptionsHtml = dropdownOptionsHtml
      };

      if (dropdownInfo.DropdownSelect == PartnerDropdownSelect.Multiple) {

        string multiSelectHtml = $@"
          <select multiple class=""form-control {CSSClasses.PartnerDropdownClass} {dropdownInfo.CssClasses.HTMLEncode()}"""
          + $@" size=""1"" name=""{dropdownInfo.FormName.HTMLEncode()}"" {(dropdownInfo.IsReadOnly ? "disabled" : "")}>
            {dropdownOptionsHtml}
          </select>";

        if (dropdownInfo.LabelText == null) {
          return multiSelectHtml;
        }

        return GetGenericRow(
          new RowOptions(
            dropdownInfo.LabelText,
            dropdownInfo.InputCols ?? BootstrapCols.FormContent_Legacy) {
            RightHtml = dropdownInfo.RightHtml
          },
          multiSelectHtml);

      } else {

        if (dropdownInfo.LabelText == null) {
          return GetSelect(selectInfo);
        }

        return GetSelectRow(
          new RowOptions(
            dropdownInfo.LabelText,
            dropdownInfo.InputCols ?? BootstrapCols.FormContent_Legacy) {
            RightHtml = dropdownInfo.RightHtml
          },
          selectInfo);
      }
    }

    public static string GetPartnerHiddenIcon(bool isProfileHidden, bool canViewHiddenPartners) {

      if (!canViewHiddenPartners) return string.Empty;

      if (isProfileHidden) {
        return GetIconTooltip(ActionButtonTypeEnum.hidden, "This profile is hidden.", "This partner has hidden their profile from the public.");
      } else {
        return string.Empty;
      }
    }

    public static string GetXeroContactOptions(bool includeEmailAddress, List<DbHelper.XeroContacts.XeroContactsInfo> xeroContacts, int? selectedXeroContactId = null) {

      var html = new StringBuilder();
      html.Append(@"<option value="""">[select contact]</option>");

      foreach (var xc in xeroContacts) {
        html.Append("<option");
        if (selectedXeroContactId != null && xc.XeroContactId == selectedXeroContactId) {
          html.Append(" selected");
        }
        html.Append($@" value=""{xc.XeroContactId}"">{xc.ContactName.HTMLEncode()}");
        if (includeEmailAddress && !xc.EmailAddress.IsNullOrEmpty()) {
          html.Append($" ({xc.EmailAddress.HTMLEncode()})");
        }
        html.Append("</option>");
      }

      return html.ToString();
    }

    public static string GetCurrencyInput(string labelHtml, string inputName, decimal? value, int decimalPlaces = 0,
      int inputCols = 2, string rightHtml = "", bool isReadOnly = false, bool preventNegative = false) {

      string html = GetTextInput(new TextInputSettings() {
        Label = labelHtml,
        InputName = inputName,
        Value = value.ToStringOrEmptyIfNull(),
        InputCols = inputCols,
        RightHtml = rightHtml,
        IsReadOnly = isReadOnly,
        InputClasses = "inp-currency",
        InputAttributes = "data-decimalPlaces=\"" + decimalPlaces + "\"" + (preventNegative ? " data-preventnegative=\"true\"" : "")
      });
      return html;
    }

    public static string GetCurrencyInputNoRow(string inputName, decimal? value, int decimalPlaces, bool isReadOnly = false, bool preventNegative = false) {

      string html = GetTextInput(new TextInputSettings() {
        NoRow = true,
        InputName = inputName,
        Value = value.ToStringOrEmptyIfNull(),
        IsReadOnly = isReadOnly,
        InputClasses = "inp-currency",
        InputAttributes = "data-decimalPlaces=\"" + decimalPlaces + "\"" + (preventNegative ? " data-preventnegative=\"true\"" : "")
      });
      return html;
    }

    // Note that valueAsFraction should be 0-1 not 0-100. i.e. valueAsFraction = 0.5 means "50%".
    public static string GetPercentInput(
      string labelHtml, string inputName, decimal? valueAsFraction, int decimalPlaces = 0,
      int inputCols = 1, string rightHtml = "", bool isReadOnly = false) {

      return GetPercentInput(labelHtml, inputName, valueAsFraction, decimalPlaces, 2, inputCols, rightHtml, isReadOnly);
    }

    public static string GetPercentInput(
      string labelHtml, string inputName, decimal? valueAsFraction, int decimalPlaces = 0,
      int labelCols = 1, int inputCols = 1, string rightHtml = "", bool isReadOnly = false, bool removeLabelColumn = false) {

      string html = GetTextInput(new TextInputSettings() {
        LabelCols = labelCols,
        Label = labelHtml,
        InputName = inputName,
        Value = valueAsFraction == null ? "" : ((decimal)valueAsFraction * 100).ToString(),
        InputCols = inputCols,
        RightHtml = rightHtml,
        IsReadOnly = isReadOnly,
        InputClasses = "inp-percent",
        InputAttributes = "data-decimalPlaces=\"" + decimalPlaces + "\"",
        NoRow = removeLabelColumn
      });
      return html;
    }

    public static string GetTextInput(string labelHtml, string inputName, string placeholder, string value) {

      return GetTextInput(new TextInputSettings() {
        Label = labelHtml,
        InputName = inputName,
        Value = value
      });
    }

    public static string GetTextInput(string labelHtml, string inputName, string placeholder, string value, bool isReadOnly, InputMaxLength maxLength = InputMaxLength.NoLimit) {
      return GetTextInput(new TextInputSettings() {
        Label = labelHtml,
        InputName = inputName,
        Value = value,
        IsReadOnly = isReadOnly,
        MaxLength = maxLength
      });
    }

    public static string GetTextInput(string labelHtml, string inputName, string placeholder, string value, bool isReadOnly, bool autocomplete, InputMaxLength maxLength = InputMaxLength.NoLimit) {
      return GetTextInput(new TextInputSettings() {
        Label = labelHtml,
        InputName = inputName,
        Value = value,
        IsReadOnly = isReadOnly,
        Autocomplete = autocomplete,
        MaxLength = maxLength
      });
    }

    public static string GetTextInput(string labelHtml, string inputName, string value, int inputCols = BootstrapCols.FormContent_Legacy, string rightHtml = "", bool isReadOnly = false, bool autocomplete = false, InputMaxLength maxLength = InputMaxLength.NoLimit) {
      return GetTextInput(new TextInputSettings() {
        Label = labelHtml,
        InputName = inputName,
        Value = value,
        InputCols = inputCols,
        RightHtml = rightHtml,
        IsReadOnly = isReadOnly,
        Autocomplete = autocomplete,
        MaxLength = maxLength
      });
    }

    public static string GetTextInput(string labelHtml, string inputName, string value, string placeholder, int labelCols = BootstrapCols.FormLabel_Legacy, int inputCols = BootstrapCols.FormContent_Legacy, string rightHtml = "", bool isReadOnly = false, bool autocomplete = false, InputMaxLength maxLength = InputMaxLength.NoLimit) {
      return GetTextInput(new TextInputSettings() {
        Label = labelHtml,
        InputName = inputName,
        Value = value,
        Placeholder = placeholder,
        LabelCols = labelCols,
        InputCols = inputCols,
        RightHtml = rightHtml,
        IsReadOnly = isReadOnly,
        Autocomplete = autocomplete,
        MaxLength = maxLength
      });
    }

    public static string GetTextInputNoRow(string inputName, string value, string placeholder, bool readOnly, InputMaxLength maxLength = InputMaxLength.NoLimit) {
      return GetTextInput(new TextInputSettings() {
        InputName = inputName,
        Value = value,
        Placeholder = placeholder,
        IsReadOnly = readOnly,
        NoRow = true,
        MaxLength = maxLength
      });
    }

    public static string GetTextInput(TextInputSettings settings) {

      string textInputHtml = $@"
        <input {GetReadOnlyAttrs(settings.IsReadOnly)} type=""text"" {GetMaxLengthAttr(settings.MaxLength)}"
          + $@" class=""form-control control-textinput {settings.InputClasses}"" {settings.InputAttributes} name=""{settings.InputName.HTMLEncode()}"""
          + $@" value=""{settings.Value.HTMLEncode()}"" placeholder=""{settings.Placeholder.EmptyIfNull()}"""
          + $@" {GetAutocompleteAttr(settings.Autocomplete)} />";

      if (settings.NoRow) return textInputHtml;

      return $@"
        <div class=""form-group ajaxSubmit-field row {GetRowClassForName(settings.InputName)}"">
          <label class=""control-label col-md-{settings.LabelCols} col-sm-12 col-xs-12"">{settings.Label.HTMLEncode()}"
            + $@"<div class=""control-label-note"">{settings.LabelNoteHtml}</div></label>
          <div class=""col-md-{settings.InputCols} col-sm-12 col-xs-12"">{textInputHtml}</div>
          {GetInputRightHtml(settings.LabelCols, settings.InputCols, settings.RightHtml)}
        </div>";
    }

    private static string GetRowClassForName(string fieldName) {
      if (fieldName.IsNullOrEmpty()) return string.Empty;
      return CSSClasses.AjaxFieldNamePrefix + fieldName;
    }

    private static string GetReadOnlyAttrs(bool isReadOnly) {
      if (isReadOnly) return @"readonly=""readonly"" tabindex=""-1""";
      return string.Empty;
    }

    private static string GetMaxLengthAttr(InputMaxLength maxLength) {
      if (maxLength != InputMaxLength.NoLimit) return $@"maxlength=""{((int)maxLength)}""";
      return string.Empty;
    }

    private static string GetTargetAttr(TargetNewTab targetNewTab) {
      return targetNewTab == TargetNewTab.Yes ? "target=\"_blank\"" : string.Empty;
    }

    private static string GetAutocompleteAttr(bool isAutoComplete) {
      return $@"autocomplete=""{isAutoComplete.ToValue("on", "off")}""";
    }

    public static string GetTextArea(string labelHtml, string inputName, int inputCols, string value, string rightHtml = "") {
      return GetTextArea(labelHtml, inputName, 2, inputCols, value, rightHtml);
    }

    public static string GetTextArea(string labelHtml, string inputName, int labelCols, int inputCols, string value, string rightHtml = "", bool isReadOnly = false) {

      return $@"
        <div class=""form-group ajaxSubmit-field row"" {GetRowClassForName(inputName)}>
          <label class=""control-label col-md-{labelCols} col-sm-12 col-xs-12"">{labelHtml}</label>
          <div class=""col-md-{inputCols} col-sm-12 col-xs-12"">
            <textarea {GetReadOnlyAttrs(isReadOnly)} rows=""3"" class=""form-control control-textarea"""
            + $@" name=""{inputName.HTMLEncode()}"">{value.HTMLEncode()}</textarea>
          </div>
          {GetInputRightHtml(labelCols, inputCols, rightHtml)}
        </div>";
    }

    public static string GetTextAreaVertical(string labelHtml, bool isBoldLabel, string inputName, int rowCols, string value, string styleClasses = "", string rightHtml = "", bool isReadOnly = false) {

      return $@"
        <div class=""form-group ajaxSubmit-field {GetRowClassForName(inputName)} row w100p {styleClasses.HTMLEncode()}"">
          <div class=""col-md-{rowCols} col-sm-12 col-xs-12"">
            <p class=""ml5 mb15"">{isBoldLabel.ToValue("<b>")}{labelHtml}{isBoldLabel.ToValue("</b>")}</p>
            <textarea {GetReadOnlyAttrs(isReadOnly)} rows=""3"" class=""form-control control-textarea"" name=""{inputName.HTMLEncode()}"">{value.HTMLEncode()}</textarea>
          </div>
          {GetInputRightHtml(null, rowCols, rightHtml)}
        </div>";
    }

    public static string GetRichTextArea(string labelHtml, string inputName, int labelCols, int inputCols, string value, string rightHtml = "", bool isReadOnly = false) {

      return $@"
        <div class=""form-group ajaxSubmit-field row"" {GetRowClassForName(inputName)}>
          <label class=""control-label col-md-{labelCols} col-sm-12 col-xs-12"">{labelHtml}</label>
          <div class=""col-md-{inputCols} col-sm-12 col-xs-12"">
            <textarea {GetReadOnlyAttrs(isReadOnly)} rows=""3"" class=""form-control control-textarea tinymce displaynone"""
            + $@" id=""txt{inputName}"" name=""{inputName.HTMLEncode()}"">{value.HTMLEncode()}</textarea>
          </div>
          {GetInputRightHtml(labelCols, inputCols, rightHtml)}
        </div>";
    }

    private static string GetInputRightHtml(int? labelCols, int? contentCols, string rightHtml) {

      if (rightHtml.IsNullOrEmpty()) return string.Empty;
      if (labelCols == null) labelCols = BootstrapCols.FormLabel_Legacy;
      if (contentCols == null) contentCols = BootstrapCols.FormContent_Legacy;
      int rightCols = BootstrapCols.Total - labelCols.Value - contentCols.Value;

      if (rightCols < 1) { // No cols left for right side.
        if (rightHtml.IsNullOrEmpty()) return string.Empty; // No content, so don't add any html.
        rightCols = BootstrapCols.Total; // Force show content on new line.
      }
      return $@"<div class=""col-md-{rightCols} col-sm-12 col-xs-12 extra-text"">{rightHtml}</div>";
    }

    public static string GetTextInputDual(string labelHtml,
      string inp1Name, string inp1Value, string inp1Placeholder,
      string inp2Name, string inp2Value, string inp2Placeholder,
      bool isReadOnly,
      InputMaxLength maxLength = InputMaxLength.NoLimit,
      int inputCols = BootstrapCols.FormContent_Legacy
    ) {
      int remainingCols = BootstrapCols.Total - BootstrapCols.FormLabel_Legacy - inputCols;
      return $@"
        <div class=""form-group ajaxSubmit-field row {GetRowClassForName(inp1Name)}"">
          <label class=""control-label col-md-2 col-sm-12 col-xs-12"">{labelHtml}</label>
          <div class=""col-md-{inputCols} col-sm-12 col-xs-12"">
            <div class=""input-text-dual"">
              <input {GetReadOnlyAttrs(isReadOnly)} type=""text"" {GetMaxLengthAttr(maxLength)} class=""form-control control-textinput"""
                + $@" name=""{inp1Name.HTMLEncode()}"" value=""{inp1Value.HTMLEncode()}"""
                + $@" placeholder=""{inp1Placeholder.HTMLEncode()}"" {GetAutocompleteAttr(false)} />
              <input {GetReadOnlyAttrs(isReadOnly)} type=""text"" {GetMaxLengthAttr(maxLength)} class=""form-control control-textinput"""
                + $@" name=""{inp2Name.HTMLEncode()}"" value=""{inp2Value.HTMLEncode()}"""
                + $@" placeholder=""{inp2Placeholder.HTMLEncode()}"" {GetAutocompleteAttr(false)} />
            </div>
          </div>
          {(remainingCols > 0 ? $@"<div class=""col-md-{remainingCols} hidden-sm hidden-xs""></div>" : "")}
        </div>";
    }

    public static string GetInputDateRow(string labelHtml, string inputName, DateTime? displayDateLocal, string rightHtml, bool isReadOnly = false, string customClass = "") {
      return GetInputDateRow(labelHtml, inputName, displayDateLocal, BootstrapCols.FormLabel_Legacy, BootstrapCols.FormContent_Legacy, rightHtml, isReadOnly, customClass);
    }

    public static string GetInputDateRow(
      string labelHtml, string inputName, DateTime? displayDateLocal,
      int labelCols = BootstrapCols.FormLabel_Legacy, int inputCols = BootstrapCols.FormContent_Legacy,
      string rightHtml = "", bool isReadOnly = false, string customClass = "") {

      return GetInputDateRow(
        new RowOptions() {
          LabelCols = labelCols,
          Label = labelHtml,
          LabelIsHtml = true,
          ContentCols = inputCols,
          RightHtml = rightHtml,
          RowClass = customClass
        },
        new DateInputInfo(inputName, displayDateLocal) { IsReadOnly = isReadOnly }
      );
    }

    public static string GetInputDateRow(RowOptions rowOptions, DateInputInfo dateInputInfo) {

      int labelCols = rowOptions.LabelCols ?? BootstrapCols.FormLabel_Legacy;
      int contentCols = rowOptions.ContentCols ?? (BootstrapCols.Total - labelCols);

      return $@"
        <div class=""form-group ajaxSubmit-field {GetRowClassForName(dateInputInfo.InputName)} row {rowOptions.RowClass.HTMLEncode()}"">
          <label class=""control-label col-md-{labelCols} col-sm-12 col-xs-12"">{(rowOptions.LabelIsHtml ? rowOptions.Label : rowOptions.Label.HTMLEncode())}</label>
          <div class=""col-md-{contentCols}"">
            <div class=""flex flex-align-center gap15"">
              {GetInputDate(dateInputInfo.InputName, dateInputInfo.Value, dateInputInfo.IsReadOnly)}
              {rowOptions.RightHtml}
            </div>
            <div class=""{CSSClasses.AjaxFieldErrorMsg} {dateInputInfo.InputName.HTMLEncode()}""></div>
          </div>
        </div>";
    }

    public static string GetInputDate(string inputName, DateTime? dateToDisplay, bool isReadOnly = false) {
      return new Form.DatePicker() {
        InputName = inputName,
        Value = dateToDisplay,
        IsReadOnly = isReadOnly
      }.ToHtml();
    }

    public static string GetSelectRow(string labelHtml, string fieldName, int inputCols, string optionsHtml, string rightHtml = "", bool isReadOnly = false) {
      return GetSelectRow(
        new WebHelper.RowOptions() {
          Label = labelHtml,
          LabelIsHtml = true,
          ContentCols = inputCols,
          RightHtml = rightHtml
        },
        new WebHelper.SelectInfo() {
          InputName = fieldName,
          TopOptionsHtml = optionsHtml,
          IsReadOnly = isReadOnly
        });
    }

    public static string GetSelectRow(string labelHtml, string fieldName, int labelCols, int inputCols, string optionsHtml, string rightHtml = "", bool isReadOnly = false) {
      return GetSelectRow(
        new WebHelper.RowOptions() {
          Label = labelHtml,
          LabelIsHtml = true,
          LabelCols = labelCols,
          ContentCols = inputCols,
          RightHtml = rightHtml
        },
        new WebHelper.SelectInfo() {
          InputName = fieldName,
          TopOptionsHtml = optionsHtml,
          IsReadOnly = isReadOnly
        });
    }

    public static string GetMultiSelectRow(string labelHtml, string fieldName, int labelCols, int inputCols, string optionsHtml, string rightHtml = "", bool isReadOnly = false) {
      return GetSelectRow(
        new WebHelper.RowOptions() {
          Label = labelHtml,
          LabelIsHtml = true,
          LabelCols = labelCols,
          ContentCols = inputCols,
          RightHtml = rightHtml
        },
        new WebHelper.SelectInfo() {
          InputName = fieldName,
          TopOptionsHtml = optionsHtml,
          IsReadOnly = isReadOnly,
          Multiple = true
        });
    }

    public static string GetSelectRow(string labelHtml, SelectInfo selectInfo) {
      return GetSelectRow(new RowOptions(labelHtml, BootstrapCols.FormContent_Legacy), selectInfo);
    }

    public static string GetSelectRow(RowOptions rowOptions, SelectInfo selectInfo) {
      return GetGenericRow(rowOptions, GetSelect(selectInfo));
    }

    public static string GetQuoteItemSelectRow(
      string labelText, int inputCols,
      string fieldName, bool isNewItem,
      DbHelper.ProgramComponents.ComponentQuoteInfo componentQuoteInfo,
      string optionsHtml, bool isReadOnly) {

      return GetSelectRow(
        new WebHelper.RowOptions() {
          Label = labelText,
          LabelCols = BootstrapCols.FormLabel_Legacy,
          ContentCols = inputCols,
          RightHtml = isNewItem ? "" : GetComponentQuoteTooltipAndLink(componentQuoteInfo?.QuotePublicGuid, componentQuoteInfo?.QuoteItemDescriptionHtml)
        },
        new WebHelper.SelectInfo() {
          InputName = fieldName,
          TopOptionsHtml = optionsHtml,
          IsReadOnly = isReadOnly,
          Select2WordWrap = true
        });
    }


    private static string GetSelectRow(string labelHtml, bool isMultiple, string fieldName, int labelCols, int inputCols, string cssSelWidth, bool isReadOnly, string optionsHtml, string rightHtml = "") {

      return $@"
        <div class=""form-group ajaxSubmit-field row {GetRowClassForName(fieldName)}"">
          <label class=""control-label col-md-{labelCols} col-sm-12 col-xs-12"">{labelHtml}</label>
          <div class=""col-md-{inputCols} col-sm-12 col-xs-12"">
            {GetSelect(
              fieldName: fieldName,
              optionsHtml: optionsHtml,
              isReadOnly: isReadOnly,
              isMultiple: isMultiple
            )}
          </div>
          {GetInputRightHtml(labelCols, inputCols, rightHtml)}
        </div>";
    }

    public static string GetSelect(string fieldName, string optionsHtml, bool isReadOnly = false, bool isMultiple = false) {

      return GetSelect(new SelectInfo() {
        InputName = fieldName,
        TopOptionsHtml = optionsHtml,
        IsReadOnly = isReadOnly,
        Multiple = isMultiple
      });
    }

    public static string GetSelect(SelectInfo selectInfo) {

      string dataAttributes = "";
      if (!selectInfo.Placeholder.IsNullOrEmpty()) dataAttributes += $" data-placeholder=\"{selectInfo.Placeholder.HTMLEncode()}\"";
      if (!selectInfo.SearchPlaceholder.IsNullOrEmpty()) dataAttributes += $" data-searchplaceholder=\"{selectInfo.SearchPlaceholder.HTMLEncode()}\"";

      string classes = $"form-control {selectInfo.Class.HTMLEncode()}";
      if (selectInfo.NoSelect2) classes += " noselect2";
      if (selectInfo.Select2WordWrap) classes += " select2-word-wrap";
      if (selectInfo.Width == SelectWidth.Medium) classes += " legacy-selectwidth-medium";
      if (selectInfo.Width == SelectWidth.Maximum) classes += " legacy-selectwidth-maximum";

      string styles = $"{selectInfo.Style.HTMLEncode()}";

      return $"<select"
        + (classes.IsNullOrEmpty() ? string.Empty : $" class=\"{classes}\"")
        + (styles.IsNullOrEmpty() ? string.Empty : $" style=\"{styles}\"")
        + (selectInfo.ID.IsNullOrEmpty() ? string.Empty : $" id=\"{selectInfo.ID.HTMLEncode()}\"")
        + (selectInfo.InputName.IsNullOrEmpty() ? string.Empty : $" name=\"{selectInfo.InputName.HTMLEncode()}\"")
        + (selectInfo.Size == null ? string.Empty : $" size=\"{selectInfo.Size}\"")
        + (selectInfo.Multiple ? " multiple" : string.Empty)
        + (selectInfo.IsReadOnly ? " readonly tabindex=\"-1\"" : string.Empty)
        + dataAttributes
        + ">"
        + selectInfo.TopOptionsHtml.EmptyIfNull()
        + GetSelectOptionListHtml(selectInfo.Options)
        + "</select>";
    }

    private enum RenderAttr { IfHasValue, Always }

    private static string AttrHtml<T>(string attributeName, T value, RenderAttr render) {
      if (attributeName.IsNullOrEmptyOrWhitespace()) return string.Empty;
      if (render == RenderAttr.IfHasValue) {
        // Don't return an attribute if value is null or empty string.
        if (value == null || value is string s && s == string.Empty) return string.Empty;
      }
      return $@"{attributeName.HTMLEncode()}=""{FormatAttrValue(value).HTMLEncode()}""";
    }

    private static string ClassAttrHtml(List<string> classes) {
      if (classes.IsNullOrEmpty()) return string.Empty;
      return $@"class=""{classes.Join(" ").HTMLEncode()}""";
    }

    // Returns various types in a standard format appropriate for each type.
    // e.g. all dates are output in a standard format.
    // Don't HtmlEncode here. HtmlEncode is only done during final html output.
    private static string FormatAttrValue<T>(T value) {

      if (value == null) return string.Empty;

      if (value is string s) {
        // Already a string, output as is, or blank.
        return s;

      } else if (value is DateTime dt) {
        // Return date value as a JS compatible string.
        // Note that JS seconds precision is 3 decimal places, whereas c# is 7 dec places,
        // so ensure to only output 3 (i.e. 'fff').
        if (dt.Kind == DateTimeKind.Utc) {
          return dt.UtcToJS();
        } else {
          return dt.ToString("yyyy-MM-dd'T'HH:mm:ss.fff"); // As-is with no offset info.
        }

      } else if (value is DateTimeOffset dto) {
        return dto.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"); // Include provided offset ('zzz')

      } else if (value is Guid guid) {
        return guid.ToStringNoBraces();
      }

      // ToString will do for the rest.
      return value.ToStringOrEmptyIfNull();
    }

    public static string GetSelectOptionListHtml(List<SelectOption> options) {

      if (options.IsNullOrEmpty()) return string.Empty;

      var sb = new StringBuilder();

      foreach (var option in options) {
        sb.AppendLine(GetSelectOptionHtml(option));
      }

      return sb.ToString();
    }

    public static string GetSelectOptionHtml(SelectOption option) {
      return option.ToHtml();
    }
    public static string GetSelectOptionHtml(string value, string text) {
      return GetSelectOptionHtml(value, text, false);
    }
    public static string GetSelectOptionHtml(string value, string text, string selectedValue) {
      return GetSelectOptionHtml(value, text, value == selectedValue);
    }
    public static string GetSelectOptionHtml(string value, string text, bool isSelected, DataAttributes dataAttributes = null) {
      return $@"<option value=""{value.HTMLEncode()}"" {isSelected.ToValue("selected")} {GetDataAttributes(dataAttributes)}>{text.HTMLEncode()}</option>";
    }

    public static string GetGenericRow(RowOptions rowOptions, string contentHtml) {

      string labelHtml = rowOptions.LabelIsHtml ? rowOptions.Label : rowOptions.Label.HTMLEncode();
      int labelCols = rowOptions.LabelCols ?? BootstrapCols.FormLabel_Legacy;
      int contentCols = rowOptions.ContentCols ?? BootstrapCols.FormContent_Legacy;
      string ajaxFieldNameClass = GetRowClassForName(rowOptions.InputFieldName);

      return $@"
        <div class=""form-group ajaxSubmit-field {ajaxFieldNameClass} row {rowOptions.RowClass.HTMLEncode()}"">
          <label class=""control-label col-md-{labelCols} col -sm-12 col-xs-12"">{labelHtml}</label>
          <div class=""col-md-{contentCols} col-sm-12 col-xs-12"">
            <div>{contentHtml}</div>
            <div class=""{CSSClasses.AjaxFieldErrorMsg}""></div>
          </div>
          {GetInputRightHtml(labelCols, contentCols, rowOptions.RightHtml)}
        </div>";
    }

    public static string GetTextDisplayRow(string labelHtml, string contentHtml, string rightHtml = "") {
      return GetTextDisplayRow(labelHtml, BootstrapCols.Total - BootstrapCols.FormLabel_Legacy, contentHtml, rightHtml);
    }

    public static string GetTextDisplayRow(string labelHtml, int contentCols, string contentHtml, string rightHtml = "") {
      return GetTextDisplayRow("form-group display-only", labelHtml, contentCols, contentHtml, rightHtml);
    }

    private static string GetTextDisplayRow(string rowClasses, string labelHtml, int contentCols, string contentHtml, string rightHtml = "") {

      int labelCols = BootstrapCols.FormLabel_Legacy;
      int rightCols = rightHtml.IsNullOrEmpty() ? 0 : BootstrapCols.FormLabel_Legacy;
      if (contentCols + rightCols > BootstrapCols.Total) contentCols = BootstrapCols.Total - rightCols;
      if (contentCols + rightCols + labelCols > BootstrapCols.Total) labelCols = BootstrapCols.Total - rightCols - contentCols;

      string html = $@"<div class=""row {rowClasses.HTMLEncode()}"">";
      if (labelCols > 0) html += $@"<label class=""control-label col-md-{labelCols} col-sm-12 col-xs-12"">{labelHtml}</label>";
      html += $@"<div class=""col-md-{contentCols} col-sm-12 col-xs-12"">{contentHtml}</div>";
      if (rightCols > 0) html += $@"{GetInputRightHtml(labelCols, contentCols, rightHtml)}";
      html += "</div>";

      return html;
    }

    public static string GetRowStart(string labelHtml, int widthCols) {

      return $@"
        <div class=""row form-group display-only"">
          <label class=""control-label col-md-2 col-sm-12 col-xs-12"">{labelHtml}</label>
          <div class=""col-md-{widthCols} col-sm-12 col-xs-12"">";
    }

    public static string GetRowStart(int widthCols = 12) {

      return $@"<div class=""row form-group display-only""><div class=""col-md-{widthCols} col-sm-12 col-xs-12"">";
    }

    public static string GetRowEnd(string rightHtml = "") {

      return $@"</div>{rightHtml}</div>";
    }

    public static string GetTextDisplayHeading(string label, int contentCols, string contentHtml, string rightHtml = "") {

      return $@"
        <div class=""row form-group display-only"">
          <label class=""control-label col-md-2 col-sm-12 col-xs-12"">{label.HTMLEncode()}</label>
          <div class=""col-md-{contentCols} col-sm-12 col-xs-12""><h4>{contentHtml.HTMLEncode()} {rightHtml}</h4></div>
        </div>";
    }

    public static string CustomCheckBox(string name, string value, bool? isChecked, string text, string commentHtml = "") {
      return CustomCheckBox(name, value, isChecked, false, text, commentHtml);
    }

    public static string CustomCheckBox(string name, string value, bool? isChecked, bool isReadOnly, string text, string commentHtml = "") {
      return CustomCheckBox(new CheckboxInfo() {
        InputName = name,
        Value = value,
        Checked = isChecked ?? false,
        IsReadOnly = isReadOnly,
        Text = text,
        CommentHtml = commentHtml
      });
    }

    public static string CustomCheckBox(CheckboxInfo cb) {

      string name = cb.InputName.HTMLEncode();
      string value = cb.Value.HTMLEncode();
      string hascontent = cb.Text.IsNullOrEmpty() && cb.CommentHtml.IsNullOrEmpty() ? "no-content" : "has-content";
      string commentHtml = cb.CommentHtml.SurroundWith("<div class=\"control-comment\">", "</div>");

      return $@"
        <div class=""checkbox"">
          <table class=""checkbox-table""><tr><td class=""col-control"">
            <label class=""control-label"" for=""chk_{name}_{value}"">"
            + $@"<input type=""checkbox"" {cb.Checked.ToValue("checked")} {cb.IsReadOnly.ToValue("readonly")}"
            + $@" id=""chk_{name}_{value}"" name=""{name}"" class=""icheck control-checkbox"""
            + $@" value=""{value}"" {(cb.DataAttributes?.ToHTML() ?? "")} /></label>
          </td><td class=""col-text {hascontent}"">
            <div class=""righttext"">{cb.Text.HTMLEncode()}</div>
            <div class=""righthtml"">{commentHtml}</div>
          </td></tr></table>
        </div>";
    }

    public static string CustomCheckBoxRow(string label, string name, string value, bool isChecked, string text, string commentHtml = "", string labelCustomClass = "", int labelCols = BootstrapCols.FormLabel_Legacy) {
      return CustomCheckBoxRow(label, labelCols, name, value, isChecked, false, text, commentHtml, labelCustomClass);
    }

    public static string CustomCheckBoxRow(string label, string name, string value, bool isChecked, bool isReadOnly, string text, string commentHtml = "", string labelCustomClass = "", int labelCols = BootstrapCols.FormLabel_Legacy) {
      return CustomCheckBoxRow(label, labelCols, name, value, isChecked, isReadOnly, text, commentHtml, labelCustomClass);
    }

    public static string CustomCheckBoxRow(string label, int labelCols, string name, string value, bool isChecked, bool isReadOnly, string text, string commentHtml = "", string labelCustomClass = "") {

      return $@"
        <div class=""form-group ajaxSubmit-field row checkboxRow"">
          <label class=""control-label col-md-{labelCols} col-sm-12 col-xs-12 {labelCustomClass}"">{label.HTMLEncode()}</label>
          <div class=""col-md-{BootstrapCols.Total - labelCols} col-sm-12 col-xs-12"">{CustomCheckBox(name, value, isChecked, isReadOnly, text, commentHtml)}</div>
        </div>";
    }

    public static string GetAbleTermsAndConditionsCheckBoxHtml(string formName) {
      return CustomCheckBox(formName, DefaultCheckboxValue, false, "", "I have read and agreed on the " + GetSimpleLink(ConfigHelper.ExternalUrls.AbleTermsAndConditionsUrl, "Terms and Conditions", true) + ".");
    }

    public static string GetProgramStatusBadge(int? programStatusId) {
      return GetStatusBadge(DbHelper.AlbertProgramStatus.GetDisplayTitleOrNull(programStatusId));
    }

    public static string GetStatusBadge(string badgeText, string customClass = "", string secondLineText = null) {

      badgeText = badgeText.HTMLEncode();

      return $@"
        <span class=""badge badge-{customClass.ValueIfNullOrEmpty(badgeText).Replace(" ", string.Empty).ToLowerInvariant().HTMLEncode()} badge-table"""
        + $@">{badgeText.HTMLEncode() + secondLineText.HTMLEncode().EnsureStartsWith("<br>", true)}</span>";
    }

    public enum ProgressBarType { Numeric, Currency, CurrencyRoundToDollars }

    public static string GetProgressBarHtml(decimal completedAmount, decimal totalAmount, string customClasses = "", ProgressBarType progressBarType = ProgressBarType.Numeric) {

      string completedAmountHtml = FormatAmount(completedAmount);
      string totalAmountHtml = FormatAmount(totalAmount);
      string percentNumber = GetProgressPercentage(completedAmount, totalAmount).ToString();

      return $@"
        <div class=""flex flex-align-center flex-fill align-center"">
          <div class=""progress-holder {customClasses.HTMLEncode()}"">
            <div class=""progress-bar"">
              <div class=""progress"" data-percent=""{percentNumber}""  style=""width: {percentNumber.HTMLEncode()}% "" ></div>
            </div>
            <span class=""progress-label"">{completedAmountHtml} / {totalAmountHtml}</span>
          </div>
        </div>";

      string FormatAmount(decimal amount) {
        if (progressBarType == ProgressBarType.Currency) {
          return GetAmountCurrencyFormat(amount);
        } else if (progressBarType == ProgressBarType.CurrencyRoundToDollars) {
          return GetAmountCurrencyFormat(amount, true);
        } else {
          return amount.ToString();
        }
      }
    }

    private static decimal GetProgressPercentage(decimal currentValue, decimal maxValue) {
      if (maxValue <= 0 || currentValue <= 0) return 0;
      if (currentValue > maxValue) return 100;
      return currentValue / maxValue * 100m;
    }

    public static string GetSurveyViewerScoreBar(
      SurveyViewerScoreBarType barType,
      decimal scoreMinValue, decimal scoreMaxValue,
      string barTitle, decimal? barScore,
      string normTitle, decimal? normScore,
      string customClass = "") {

      decimal scoreRange = scoreMaxValue - scoreMinValue;
      decimal barPercent = Math.Round((barScore == null ? 0 : (barScore.Value - scoreMinValue)) / scoreRange * 100);
      decimal normPercent = Math.Round((normScore == null ? 0 : (normScore.Value - scoreMinValue)) / scoreRange * 100);
      return
        $"<div class=\"scoreBar {customClass}\">"
        + $"<div class=\"barTitle\">{barTitle.HTMLEncode()}</div>"
        + $"<div class=\"barBg\" data-score-min=\"{scoreMinValue}\" data-score-max=\"{scoreMaxValue}\">"
        + $"<span class=\"barLine {(barType == SurveyViewerScoreBarType.Self ? "barSelf" : "barRater")}\" style=\"width: {barPercent}%\"></span>"
        + (normScore == null ? string.Empty : $"<span class=\"barDot {(barType == SurveyViewerScoreBarType.Self ? "dotSelf" : "dotRater")}\" title=\"{normTitle.HTMLEncode()} Norm = {GetSurveyViewerScoreFormatted(normScore)}\" style=\"left: {normPercent}%\"></span>")
        + $"</div>"
        + $"<div class=\"barScore\">{GetSurveyViewerScoreFormatted(barScore)}</div>"
        + $"</div>";
    }

    public static string GetSurveyViewerScoreFormatted(decimal? score, string textIfNull = "NA") {
      if (!score.HasValue) return textIfNull;
      return score.Value.ToString("0.0");
    }

    public static string GetDeliveryBadge(bool isSessionInPerson) {

      string badgeIcon, badgeText;
      if (isSessionInPerson) {
        badgeText = "In person";
        badgeIcon = "storefront-outline";
      } else {
        badgeText = "Online";
        badgeIcon = "laptop-outline";
      }

      return $@"
        <span class=""badge"">
          <ion-icon name=""{badgeIcon}""></ion-icon>
          &nbsp; {badgeText}
        </span>";
    }

    public static string GetUpcomingEventTypeTooltip(string eventImagePath, string tooltipTitleHtml, string customClasses) {

      return $@"
        <div class=""iconTooltip eventTypeTooltip {tooltipTitleHtml.ToLowerInvariant().Replace(" ", "").HTMLEncode()} {customClasses.EmptyIfNull()}"">
          <span data-tooltiptitle=""{tooltipTitleHtml}"">
            <img src =""{eventImagePath.HTMLEncode()}""/>
          </span>
        </div>";
    }

    public static string GetSurveyDeliveryBadge(DbHelper.SurveyShare.SurveyInfo surveyInfo) {

      return GetSurveyDeliveryBadge(surveyInfo.SurveyType, surveyInfo.ReportType);
    }

    public static string GetSurveyDeliveryBadge(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {

      return GetSurveyDeliveryBadge(surveyInfo.SurveyType, surveyInfo.ReportType);
    }

    private static string GetSurveyDeliveryBadge(DbHelper.AlbertSurveys.SurveyTypeEnum surveyType, DbHelper.ReportTypes.ReportTypeInfo reportType) {

      string badgeIcon, badgeText;

      if (surveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Eval) {
        badgeText = "Evaluation";
        badgeIcon = "bar-chart-outline";
      } else if (surveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Intake) {
        badgeText = "Intake";
        badgeIcon = "push-outline";
      } else if (surveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.IOS) {
        badgeText = "Org";
        badgeIcon = "business-outline";
      } else if (surveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Pulse360) {
        badgeText = "Pulse";
        badgeIcon = "pulse-outline";
      } else if (surveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Standard360) {
        badgeText = "Profile";
        badgeIcon = "git-compare-outline";
      } else if (surveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) {
        badgeText = "Dev Plan";
        badgeIcon = "navigate-circle-outline";
      } else {
        badgeText = reportType.ReportTypeName;
        badgeIcon = "extension-puzzle-outline";
      }

      return $@"
        <span class=""badge"">
          <ion-icon name=""{badgeIcon}""></ion-icon>
          &nbsp; {badgeText.HTMLEncode()}
        </span>";
    }

    public static string GetSurveyStatusBadge(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
      if (surveyInfo?.FoundParticipantBrief?.IsValidStateForReport == true) return GetStatusBadge("Complete");
      if (surveyInfo.IsClosed) return GetStatusBadge("Closed");
      if (surveyInfo.IsScheduledInFuture) {
        return GetStatusBadge(
          "Scheduled", null,
          SessionHelper.UtcToUserTime(surveyInfo.ScheduledStartDateUtc).ToString("d MMM yyyy"));
      }
      return GetStatusBadge("Open");
    }

    public static string GetSurveyCloseDateSelf(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
      if (surveyInfo == null || surveyInfo.IsRatersOnly) return "-";
      string closeDateSelfLocal = DisplayDate(surveyInfo.CloseDateSelfLocal, "-");
      return GetSurveyDisplayDateHtml(surveyInfo, closeDateSelfLocal);
    }

    private static string GetSurveyDisplayDateHtml(DbHelper.AlbertSurveys.SurveyInfo surveyInfo, string dateToDisplay) {
      if (surveyInfo == null || dateToDisplay.IsNullOrEmpty() || surveyInfo?.FoundParticipantBrief?.CompletedUtc == null) return dateToDisplay;
      return dateToDisplay.EnsureStartsWith("<span class=\"survey-completed\">").EnsureEndsWith("</span>", StringExt.Ensure.IfNotBlank);
    }

    public static string GetSurveyCloseDateRaters(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
      if (surveyInfo == null || surveyInfo.IsSelfOnly) return "-";
      return DisplayDate(surveyInfo.CloseDateRatersLocal, "-");
    }

    public static string GetSurveyRatersCompleted(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
      if (surveyInfo == null || surveyInfo.IsSelfOnly) return string.Empty;
      return $"{surveyInfo.RatersCompleted ?? 0} of {surveyInfo.RatersInvited ?? 0}";
    }

    private static string GetSurveyClosedTooltip(bool isClosed) {
      if (!isClosed) return string.Empty;
      return GetIconTooltip(ActionButtonTypeEnum.closed, "Closed", string.Empty, "ml5");
    }

    public static string GetSurveyRatersInfoCol(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
      if (surveyInfo == null) return "-";

      string raterHtml = GetSurveyCloseDateRaters(surveyInfo);
      // Add span with class for rater completion or incompletion
      if (surveyInfo.IsSelfOnly) {
        raterHtml = raterHtml.EnsureStartsWith("<span class=\"survey-not-completed\">").EnsureEndsWith("</span>", StringExt.Ensure.IfNotBlank);
      }
      string surveyRatersInfo = $"{raterHtml}<br />{GetSurveyRatersCompleted(surveyInfo)}";
      return GetSurveyDisplayDateHtml(surveyInfo, surveyRatersInfo);
    }

    public static string GetProfileImage(string profileImagePath) {

      return $@"<div class=""user-avatar""><img src =""{profileImagePath.HTMLEncode()}""/></div>";
    }

    public static string GetProfileImageWithSlideout(string profileImagePath, int? userId) {

      string attributes = GetSlideoutTriggerDataAttributes("Partner Details", PathHelper.Partials.PartnerSlideoutPanel(userId));

      return $@"<div class=""user-avatar"" {attributes}><img src =""{profileImagePath.HTMLEncode()}""/></div>";
    }

    public static string GetAvatarForTable_Participant(string profileImagePath, string profileName, string underNameText, int? coacheeId = null) {

      string attributes = string.Empty, customClasses = "pax-avatar-horizontal";

      string colDisplayName = GetListViewMainColumnText(profileName, underNameText);

      if (coacheeId != null) {
        // If userId is not null, then add the slideout panel data attributes.
        attributes = GetSlideoutTriggerDataAttributes("Participant Details", PathHelper.Partials.ParticipantSlideoutPanel(coacheeId));
      }

      if (attributes.IsNullOrEmpty()) customClasses += " nohover";

      return GetAvatarForTable(profileImagePath, colDisplayName, customClasses, attributes);
    }

    public static string GetAvatarForTable_User(string profileImagePath, string profileName, int? userId, bool noSlideoutTrigger = false) {

      string attributes = string.Empty, customClasses = string.Empty;

      if (noSlideoutTrigger) userId = null; // If no slideout trigger is needed, set userId to null.

      if (userId == ConfigHelper.UserId.Unassigned) {
        // If user is unassigned coach, remove all special characters from the profile name and return just the name.
        return profileName;
      } else if (userId != null) {
        // If userId is not null, then add the slideout panel data attributes.
        attributes = GetSlideoutTriggerDataAttributes("Partner Details", PathHelper.Partials.PartnerSlideoutPanel(userId));
      }

      if (attributes.IsNullOrEmpty()) customClasses += " nohover";

      string profileNameHtml = $@"<span class=""user-name strong"">{profileName.HTMLEncode()}</span>";

      return GetAvatarForTable(profileImagePath, profileNameHtml, customClasses, attributes);
    }

    private static string GetAvatarForTable(string profileImagePath, string profileNameHtml, string customClasses, string attributes) {
      return $@"
        <a tabindex=""-1"" class=""user-avatar-horizontal {customClasses}"" {attributes}>
          <div class=""user-avatar"">
            <img src =""{profileImagePath}""/>
          </div >
          <div class=""user-details"">
            {profileNameHtml}
          </div>
        </a>";
    }

    public static string GetActionButton(ActionButtonTypeEnum action, string customClasses, string toolTipText, DataAttributes dataAttributes = null) {

      return GetActionButton(action, customClasses, false, toolTipText, dataAttributes);
    }

    public static string GetIconHtml(ActionButtonTypeEnum action, string customClass = "") {

      string iconName = ActionButtonIconName[action];

      if (iconName.StartsWith(Icon.FontAwesomePrefixClass, StringComparison.OrdinalIgnoreCase)) {
        // FontAwesome
        return Icon.GetFontAwesomeHtml(iconName);
      } else {
        // IonIcon
        return $@"<ion-icon class=""{customClass}"" name=""{iconName}"" role=""img""></ion-icon>";
      }
    }

    public static string GetActionButton(ActionButtonTypeEnum action, string customClasses, bool isDisabled, string toolTipText, DataAttributes dataAttributes = null) {

      return $@"<button type=""button"" class=""action-button {(customClasses.HTMLEncode() + (isDisabled ? " disabled" : ""))}"""
        + $@" {GetOptionalTitleAttr(toolTipText)} {GetDataAttributes(dataAttributes)} {isDisabled.ToValue("disabled", "")}"
        + $@">{GetIconHtml(action)}</button>";
    }

    public static string GetActionLinkDisabled(ActionButtonTypeEnum action) {
      return GetActionLink(action, string.Empty, string.Empty, string.Empty, string.Empty, true);
    }

    public static string GetActionLink(
      ActionButtonTypeEnum action,
      string customClasses, string linkText, string toolTipText, string url,
      TargetNewTab targetNewTab = TargetNewTab.No,
      DataAttributes dataAttributes = null) {

      return GetActionLink(action, customClasses, linkText, toolTipText, url, false, targetNewTab, dataAttributes);
    }

    public static string GetActionLink(
      ActionButtonTypeEnum action,
      string customClasses, string linkText, string toolTipText, string url,
      bool isDisabled,
      TargetNewTab targetNewTab = TargetNewTab.No,
      DataAttributes dataAttributes = null) {

      if (!linkText.IsNullOrEmpty()) customClasses += " " + CSSClasses.ActionButtonHasText;
      if (isDisabled) customClasses += " disabled";

      return $@"
        <a {GetTargetAttr(targetNewTab)} href=""{url.HTMLEncode()}"" class=""action-button {customClasses.HTMLEncode()}"""
        + $@" {GetOptionalTitleAttr(toolTipText)} {GetDataAttributes(dataAttributes)}"
        + $@">{GetIconHtml(action)}<span>{linkText.HTMLEncode()}</span></a>";
    }

    public static string GetComponentQuoteTooltipAndLink(Guid? quotePublicGuid, string quoteItemDescriptionHtml) {
      return $@"
        <a class=""mt5"" href=""{PathHelper.Pages.QuoteDetails(quotePublicGuid, PathHelper.QuoteTabEnum.components)}"" target=""_blank"">
          {GetIconTooltip(ActionButtonTypeEnum.view, "Click to view details", $"Item description: {quoteItemDescriptionHtml}")}
        </a>";
    }

    public static string GetIconTooltip(ActionButtonTypeEnum iconType, string tooltipTitle, string tooltipText, string customClass = null) {
      return GetIconTooltip(iconType, tooltipTitle, ToolTipContentType.Text, tooltipText, customClass);
    }

    public static string GetIconTooltipByElementId(ActionButtonTypeEnum iconType, string tooltipTitle, string tooltipElementID, string customClass = null) {
      return GetIconTooltip(iconType, tooltipTitle, ToolTipContentType.ElementID, tooltipElementID, customClass);
    }

    private static string GetIconTooltip(ActionButtonTypeEnum iconType, string tooltipTitle, ToolTipContentType contentType, string tooltipTextOrElementID, string customClass = null) {

      if (iconType == ActionButtonTypeEnum.requiredField) {
        customClass += " " + CSSClasses.IconTooltip_RequiredField;
      }

      return GetTooltipWithContent(GetIconHtml(iconType), tooltipTitle, contentType, tooltipTextOrElementID, customClass);
    }

    public static string GetFormWithTooltip(string formHtml, string tooltipTitle, ToolTipContentType contentType, string tooltipText, string customClass = null) {
      return GetTooltipWithContent(formHtml, tooltipTitle, contentType, tooltipText, customClass);
    }

    private static string GetTooltipWithContent(string contentHtml, string tooltipTitleText, ToolTipContentType contentType, string tooltipTextOrElementID, string customClass = null) {

      return $@"
        <div class=""iconTooltip {customClass.HTMLEncode()}"">
          <span data-tooltiptitle=""{tooltipTitleText.HTMLEncode()}"""
          + $@" data-tooltiptext=""{(contentType == ToolTipContentType.Text ? tooltipTextOrElementID.HTMLEncode() : "")}"""
          + $@" data-tooltipElementID=""{(contentType == ToolTipContentType.ElementID ? tooltipTextOrElementID.HTMLEncode() : "")}"">"
          + $@"{contentHtml}</span>
        </div>";
    }

    public static string GetPartnerStatusIcon(bool isActive, string customClass = null) {

      string statusText = isActive ? "Active" : "Inactive";
      string statusClass = isActive ? " iconActiveStatus" : " iconInactiveStatus";
      if (!customClass.IsNullOrEmpty()) statusClass += " " + customClass;

      return GetIconTooltip(ActionButtonTypeEnum.status, statusText, ToolTipContentType.None, null, statusClass);
    }

    public static string GetAIChatMessageListItem(bool isFromAI, string avatarUrl, string senderName, string messageText, bool isTemplate = false) => $@"
      <div class=""chat-message"" data-isfromai=""{isFromAI.ToJSTrueFalse()}"">
        <div class=""chat-avatar""><img src=""{avatarUrl.HTMLEncode()}"" alt=""Avatar""></div>
        <div class=""chat-body"">
          <div class=""chat-body-sender"">{senderName.HTMLEncode()}</div>
          <div class=""{CSSClasses.ChatMessageBodyText}"">{messageText.HTMLEncode()}</div>
        </div>
      </div>";

    public static string GetParticipantEventItem(DateTime? eventDateUtc, string eventName, string eventVenue, string eventVenueAddressOrPath,
      ParticipantEventType eventType, string slideoutUrl, string localTimeZoneIANA, bool includeEventIconTooltip = false) {

      if (!Uri.TryCreate(slideoutUrl.EmptyIfNull(), UriKind.RelativeOrAbsolute, out var uri)) {
        slideoutUrl = string.Empty;
      }

      string eventTooltipIcon = string.Empty;
      if (includeEventIconTooltip && eventType != ParticipantEventType.DefaultEvent) {
        if (eventType == ParticipantEventType.CoachingSession) {
          eventTooltipIcon = GetIconTooltip(ActionButtonTypeEnum.coachingIcon, "Coaching Session", string.Empty, "mr5"); ;
        } else if (eventType == ParticipantEventType.Workshop) {
          eventTooltipIcon = GetIconTooltip(ActionButtonTypeEnum.workshopIcon, "Workshop", string.Empty, "mr5");
        }
      }

      // Default Event means the user doesn't have any events and we will always show a default.
      // If it's default event, use the date time row to display the name as there's not a date and empty the row the corresponds to the name.

      return $@"
        <div class=""event-item"" {GetSlideoutTriggerDataAttributes("Event Information", slideoutUrl)}>
          <div class=""flex-inline "">
            {eventTooltipIcon}
            <p class=""evt-datetime "">{((eventType == ParticipantEventType.DefaultEvent) ? eventName : DisplayDateTimeForEvent(eventDateUtc, localTimeZoneIANA, EventDateDisplayFormat.TodayTomorrow))}</p>
          </div>
          <p class=""evt-name"">{((eventType == ParticipantEventType.DefaultEvent) ? "" : eventName.HTMLEncode())}</p>
          <p class=""evt-venue"">{GetEventVenueHtml(eventVenue, eventDateUtc)}</p>
          <p class=""evt-venueaddr"">{eventVenueAddressOrPath.HTMLEncode()}</p>
        </div>";
    }

    public static string GetDefaultEventItem() {

      return GetParticipantEventItem(null, "Webinars Integral",
        ConfigHelper.ExternalUrls.AbleWebinarsUrl,
        string.Empty,
        ParticipantEventType.DefaultEvent, string.Empty, string.Empty);
    }

    public static string GetParticipantActionItem(string actionTitleText, string actionDescriptionText, string href, TargetNewTab targetNewTab = TargetNewTab.No, string customClass = "") {

      return $@"
        <div class=""event-item {customClass.EmptyIfNull()}"">
          <p class=""evt-actionname""><a target=""{(targetNewTab == TargetNewTab.Yes ? "_blank" : string.Empty)}"" href=""{href.HTMLEncode()}"">{actionTitleText.HTMLEncode()}</a></p>
          <p class=""evt-description"">{actionDescriptionText.HTMLEncode()}</p>
        </div>";
    }

    public static string GetYouTubeEmbedHtml(string youtubeVideoUrl, string labelText, int labelCols, string customClass = "") {

      string videoId = PathHelper.GetVideoIdFromYouTubeUrl(youtubeVideoUrl);

      if (!videoId.IsNullOrEmptyOrWhitespace()) {
        return $@"
          <div class=""row form-group ajaxSubmit-field"">
            <div class=""col-md-{labelCols} col-sm-12""><label>{labelText}</label></div>
            {GetYouTubeEmbedHtmlIframe(videoId, customClass)}
          </div>";
      }

      return string.Empty;
    }

    public static string GetYouTubeEmbedHtml(string youtubeVideoUrl, string customClass = "") {

      string videoId = PathHelper.GetVideoIdFromYouTubeUrl(youtubeVideoUrl);

      if (!videoId.IsNullOrEmptyOrWhitespace()) {
        return GetYouTubeEmbedHtmlIframe(videoId, customClass);
      }

      return string.Empty;
    }

    public static string GetYouTubeEmbedHtmlIframe(string videoId, string customClass = "") {
      return $@"
        <div class=""{customClass}"">
          <iframe width=""560"" height=""315"" src=""https://www.youtube.com/embed/{videoId}""
            title=""YouTube video player"" frameborder=""0""
            allow=""accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture""
            allowfullscreen>
          </iframe>
        </div>";
    }

    public static string GetEmptyStatePageHtml(string title, string description) {
      return GetEmptyStatePageHtml(title, description, string.Empty);
    }

    public static string GetEmptyStatePageHtml(string title, string description, bool addActionHtml, string customActionHtml) {
      return GetEmptyStatePageHtml(title, description, addActionHtml ? customActionHtml : string.Empty);
    }

    public static string GetEmptyStatePageHtml(string title, string description, bool addActionHtml, string actionButtonText, string actionButtonPath) {

      string actionHtml = string.Empty;

      if (addActionHtml) {
        actionHtml = $@"
          <a class=""btn btn-primary"" href=""{actionButtonPath}"">
            <span class=""plus-icon"">+</span>
            {actionButtonText}
          </a>";
      }

      return GetEmptyStatePageHtml(title, description, actionHtml);
    }

    public static string GetEmptyStatePageHtml(string title, string description, string actionHtml) {

      return $@"
        <div class=""emptystatepage"">
          <div class=""icon-container"">
            <img class="""" src=""{PathHelper.Images.EmptyStatePageIcon()}"" />
          </div>
          <h1 class=""title"">{title}</h1>
          <p class=""subtitle"">{description}</p>
          {actionHtml}
        </div>";
    }

    public enum ShareSurveyButtonTypeEnum { ActionButton, RegularButton, ActionCard }

    public static string GetShareSurveyButtonHtml(
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      ShareSurveyButtonTypeEnum shareSurveyButtonType,
      string customClass = "") {

      bool canShareSurvey = SessionHelper.AppAccess.Surveys.CanShareSurvey(surveyInfo);

      if (canShareSurvey) {

        string html = string.Empty;

        var dataAttrs = new WebHelper.DataAttributes() {
          { DataAttrName.SlideoutTitle, "Share Survey" },
          { DataAttrName.ModalPartialUrl, PathHelper.Partials.ShareSurveyModal(surveyInfo.SurveyUID, surveyInfo.FoundParticipantBrief.PartUniqueId) },
          { DataAttrName.ModalTitle, "Share Survey" }
        };

        if (shareSurveyButtonType == ShareSurveyButtonTypeEnum.ActionButton) {

          html = GetActionButton(ActionButtonTypeEnum.share, string.Empty, "Share Survey", dataAttrs);

        } else if (shareSurveyButtonType == ShareSurveyButtonTypeEnum.RegularButton) {

          html = $@"
            <button class=""btn btn-primary {CSSClasses.ShareDevPlan} {customClass}"" {dataAttrs.ToHTML()}>
              <span class=""visible-xs"">{GetIconHtml(ActionButtonTypeEnum.share)}</span>
              <span class=""hidden-xs"">Share</span>
            </button>";

        } else if (shareSurveyButtonType == ShareSurveyButtonTypeEnum.ActionCard) {

          html += GetParticipantActionCard(new WebHelper.ParticipantActionCard(
            headerText: "Share Your Report",
            descriptionText: "Easily share your leadership report with others.",
            actionText: "Share Report",
            iconPath: PathHelper.Images.ShareIcon(),
            iconClass: Icon.ActionCardIconClass.ShareSurvey,
            linkUrl: string.Empty,
            targetNewTab: TargetNewTab.No,
            dataAttributes: dataAttrs.ToHTML()));
        }

        return html;
      }

      return string.Empty;
    }

    public static string GetUnshareSurveyButtonHtml(
      DbHelper.SurveyShare.SharedSurveysInfo sharedSurveyInfo,
      ShareSurveyButtonTypeEnum shareSurveyButtonType,
      string customClass = "") {

      bool canUnshareSurvey = SessionHelper.AppAccess.Surveys.CanUnshareSurvey(sharedSurveyInfo);

      if (canUnshareSurvey) {

        string buttonHtml = string.Empty;

        var dataAttrs = new WebHelper.DataAttributes();
        dataAttrs.Add(DataAttrName.SurveyShareId, sharedSurveyInfo.SurveyShareId.ToString());

        if (shareSurveyButtonType == ShareSurveyButtonTypeEnum.ActionButton) {

          buttonHtml = GetActionButton(ActionButtonTypeEnum.remove, customClass, "Unshare Survey", dataAttrs);
        } else {

          buttonHtml = $"<button class=\"btn btn-primary {customClass}\" {dataAttrs.ToHTML()}>Unshare Survey</button>";
        }

        return buttonHtml;
      }

      return string.Empty;
    }

    public static string GetSharedSurveyDataAttrs(DbHelper.SurveyShare.SharedSurveysInfo sharedSurveysInfo) {

      if (sharedSurveysInfo == null || sharedSurveysInfo.SurveyInfo?.CoacheeId == null) {
        return string.Empty;
      }

      var dataAttrs = new WebHelper.DataAttributes();

      if (sharedSurveysInfo.SurveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Standard360) {
        dataAttrs.Add(DataAttrName.RowLinkUrl, PathHelper.Reports.CoacheeSurvey(sharedSurveysInfo));
      } else {
        dataAttrs.Add(DataAttrName.ModalPartialUrl, PathHelper.Partials.CoacheeSurveyDetailsModal(sharedSurveysInfo.SurveyInfo.CoacheeId.Value, sharedSurveysInfo.SurveyInfo.SurveyUID, sharedSurveysInfo.SurveyShareId));
        dataAttrs.Add(DataAttrName.ModalTitle, "Survey Details");
      }

      return dataAttrs.ToHTML();
    }

    public static string GetSurveyListRowDataAttrs(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
      return GetSurveyListRowDataAttrs(null, null, surveyInfo);
    }

    public static string GetSurveyListRowDataAttrs(DbHelper.ClientCompanies.AlbertCompanyInfo company, DbHelper.AlbertSurveys.SurveyInfo survey) {
      // For org survey list.

      if (survey?.SurveyType != DbHelper.AlbertSurveys.SurveyTypeEnum.IOS
        || !SessionHelper.AppAccess.Companies.CanViewOrganisationIOSReports(company)
        || !survey.IsOrgReportAvailable_Online) return string.Empty;

      var dataAttrs = new WebHelper.DataAttributes();
      dataAttrs.Add(DataAttrName.RowLinkUrl, PathHelper.Reports.OrganisationIOSReports(survey));
      return dataAttrs.ToHTML();
    }

    public static string GetSurveyListRowDataAttrs(
      DbHelper.AblePrograms.AbleProgramInfo programInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {

      if (surveyInfo == null) return null;

      bool canCompleteSurvey = SessionHelper.AppAccess.Surveys.CanCompleteSurvey(surveyInfo);
      bool canViewDetails = SessionHelper.AppAccess.Surveys.CanViewDetails(programInfo, coacheeInfo, surveyInfo);
      bool canViewReports = true; // SessionHelper.AppAccess.Surveys.CanViewReports(programInfo, coacheeInfo, surveyInfo);

      var dataAttrs = new WebHelper.DataAttributes();

      // Add coacheeId & program id to data attributes.
      dataAttrs.Add(DataAttrName.CoacheeId, surveyInfo.FoundParticipantBrief?.CoacheeId.ToStringOrEmptyIfNull());
      dataAttrs.Add(DataAttrName.ProgramJobId, surveyInfo.FoundParticipantBrief?.ProgramJobId.ToStringOrEmptyIfNull());

      if (surveyInfo.IsOrgReportAvailable_Online && canViewReports) {

        if (PathHelper.IsCurrentPage(PathHelper.Reports.InsightsIOSReports())) {
          dataAttrs.Add(DataAttrName.RowLinkUrl, PathHelper.Reports.InsightsIOSReports(surveyInfo));
        } else {
          dataAttrs.Add(DataAttrName.RowLinkUrl, PathHelper.Reports.OrganisationIOSReports(surveyInfo));
        }

      } else if (surveyInfo.FoundParticipantBrief?.IsReportAvailable_Online == true && canViewReports) {

        // Online report available, row links to the online report.
        dataAttrs.Add(DataAttrName.RowLinkUrl, PathHelper.Reports.CoacheeSurvey(coacheeInfo, surveyInfo));

      } else if (surveyInfo.FoundParticipantBrief?.IsSurveyAvailable == true && canCompleteSurvey) {

        // Survey is open - row links to survey.
        dataAttrs.Add(DataAttrName.RowLinkUrl, PathHelper.Pages.Survey(surveyInfo));

        // Show survey in a new tab if it's an Able survey, except for intakes and devplans.
        if (surveyInfo.IsAbleSurvey
          && surveyInfo.SurveyType != DbHelper.AlbertSurveys.SurveyTypeEnum.Intake
          && surveyInfo.SurveyType != DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) dataAttrs.Add(DataAttrName.RowLinkNewTab, "true");

      } else if (canViewDetails) {

        // Fallback to showing details modal.
        if (surveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.IOS) {
          dataAttrs.Add(DataAttrName.ModalPartialUrl, PathHelper.Partials.CoacheeSurveyDetailsModal(surveyInfo.FoundParticipantBrief?.CoacheeId ?? 0, surveyInfo.SurveyUID));
        } else {
          dataAttrs.Add(DataAttrName.ModalPartialUrl, PathHelper.Partials.CoacheeSurveyDetailsModal(coacheeInfo, surveyInfo));
        }
        dataAttrs.Add(DataAttrName.ModalTitle, "Survey Details");

      }

      return dataAttrs.ToHTML();
    }

    public static string GetSurveyListActionButtons(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
      return GetSurveyListActionButtons(null, null, surveyInfo);
    }

    public static string GetSurveyListActionButtons(
      DbHelper.AblePrograms.AbleProgramInfo programInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {

      if (surveyInfo == null) return null;

      bool canCompleteSurvey = SessionHelper.AppAccess.Surveys.CanCompleteSurvey(surveyInfo);
      bool canViewDetails = SessionHelper.AppAccess.Surveys.CanViewDetails(programInfo, coacheeInfo, surveyInfo);
      bool canViewReports = SessionHelper.AppAccess.Surveys.CanViewReports(programInfo, coacheeInfo, surveyInfo);

      string html = "<div class=\"action-button-list\">";

      // Details modal button always visible, disabled if canViewDetails is false.
      html += GetActionButton(
        action: ActionButtonTypeEnum.survey_info,
        customClasses: string.Empty,
        isDisabled: !canViewDetails,
        toolTipText: "View Survey Details",
        dataAttributes: new DataAttributes(
          (DataAttrName.ModalPartialUrl, PathHelper.Partials.CoacheeSurveyDetailsModal(coacheeInfo, surveyInfo)),
          (DataAttrName.ModalTitle, "Survey Details")
        )
      );

      // Survey button or report button - just one or the other, not both.
      if (surveyInfo.IsOrgReportAvailable_Online && canViewReports) {

        // Show online report button.
        html += GetActionLink(
          action: ActionButtonTypeEnum.survey_report,
          customClasses: string.Empty,
          linkText: string.Empty,
          toolTipText: "View Report",
          url: PathHelper.Reports.OrganisationIOSReports(surveyInfo),
          targetNewTab: surveyInfo.IsAbleSurvey ? TargetNewTab.No : TargetNewTab.Yes);

      } else if (surveyInfo.FoundParticipantBrief?.IsReportAvailable_Online == true && canViewReports) {

        // Show online report button.
        html += GetActionLink(
          action: ActionButtonTypeEnum.survey_report,
          customClasses: string.Empty,
          linkText: string.Empty,
          toolTipText: "View Report",
          url: PathHelper.Reports.CoacheeSurvey(coacheeInfo, surveyInfo),
          targetNewTab: surveyInfo.IsAbleSurvey ? TargetNewTab.No : TargetNewTab.Yes);

      } else if (surveyInfo.FoundParticipantBrief?.IsSurveyAvailable == true && canCompleteSurvey) {

        // Show complete survey button.
        html += GetActionLink(
          action: ActionButtonTypeEnum.survey,
          customClasses: "btn btn-primary",
          linkText: "Complete",
          toolTipText: "Complete Survey",
          url: PathHelper.Pages.Survey(surveyInfo),
          targetNewTab: surveyInfo.IsAbleSurvey ? TargetNewTab.No : TargetNewTab.Yes);

      }

      // PDF button if available.
      if (surveyInfo.FoundParticipantBrief?.IsReportAvailable_PDF == true && canViewReports) {
        html += GetActionLink(
          action: ActionButtonTypeEnum.pdf,
          customClasses: string.Empty,
          linkText: string.Empty,
          toolTipText: "View PDF Report",
          url: PathHelper.Reports.ParticipantPDFReport(surveyInfo.ReportType, surveyInfo.SurveyId, surveyInfo.FoundParticipantBrief.PartUniqueId),
          targetNewTab: TargetNewTab.Yes);
      }

      html += GetShareSurveyButtonHtml(surveyInfo, ShareSurveyButtonTypeEnum.ActionButton);

      return html + "</div>";
    }

    public static string GetUserTooltipInfoHtml(DbHelper.AbleUser.UserIdentity user) {

      const string greenCircleEmoji = "&#128994;", redCircleEmoji = "&#128308;", checkMarkEmoji = "&#10004;&#65039;";

      var tooltipLines = new List<string>();

      tooltipLines.Add(user.IsPartnerActive ? ($"{greenCircleEmoji} Active") : ($"{redCircleEmoji} Inactive"));
      tooltipLines.Add("<hr/>");

      if (!user.IsAbleUser) {
        tooltipLines.Add($"<b>No roles assigned.</b>");
      } else {
        if (user.IsAbleAdmin) tooltipLines.Add($"{checkMarkEmoji} Admin");
        if (user.IsTenantOrgAdmin) tooltipLines.Add($"{checkMarkEmoji} Admin");
        if (user.IsAbleCoach) tooltipLines.Add($"{checkMarkEmoji} Practitioner");
        if (user.IsAbleClient) tooltipLines.Add($"{checkMarkEmoji} Client");
        if (user.IsParticipant) tooltipLines.Add($"{checkMarkEmoji} Leader");
      }

      return
        GetFormWithTooltip(
          user.IsPartnerActive ? greenCircleEmoji : redCircleEmoji,
          "Info",
          ToolTipContentType.Text,
          "<p>" + tooltipLines.Join("</p><p>") + "</p>"
        );
    }

    public static bool IsValidUrl(string checkUrl, UriKind kind = UriKind.Absolute) {
      return Uri.TryCreate(checkUrl, kind, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public class ParticipantActionCard {

      public string HeaderText { get; private set; }
      public string DescriptionText { get; private set; }
      public string ActionText { get; private set; }
      public string IconPath { get; private set; }
      public string IconClass { get; private set; }
      public string LinkUrl { get; private set; }
      public WebHelper.TargetNewTab TargetNewTab { get; private set; }
      public string DataAttributes { get; private set; }

      public ParticipantActionCard(
        string headerText, string descriptionText, string actionText, string iconPath, string iconClass,
        string linkUrl, WebHelper.TargetNewTab targetNewTab, string dataAttributes = "") {

        HeaderText = headerText;
        DescriptionText = descriptionText;
        ActionText = actionText;
        IconPath = iconPath;
        IconClass = iconClass;
        LinkUrl = linkUrl;
        TargetNewTab = targetNewTab;
        DataAttributes = dataAttributes;
      }
    }

    public static string GetParticipantActionCard(ParticipantActionCard actionCard) {
      return $@"
        <a href=""{(actionCard.LinkUrl.IsNullOrEmpty() ? "#" : actionCard.LinkUrl)}"" target=""{(actionCard.TargetNewTab == TargetNewTab.Yes ? "_blank" : string.Empty)}"" class=""action-card"" {actionCard.DataAttributes.EmptyIfNull()}>
          <div class=""action-card-header"">
            <p>{actionCard.HeaderText}</p>
            <img class=""action-card-icon float-right {actionCard.IconClass.EmptyIfNull()}"" src=""{actionCard.IconPath}"" />
          </div>
          <div class=""action-card-desc"">{actionCard.DescriptionText}</div>
          <div class=""action-card-click-area"">
            <p>{actionCard.ActionText}</p>
            <svg xmlns=""http://www.w3.org/2000/svg"" width=""23"" height=""18"" viewBox=""0 0 23 18"" fill=""none"">
              <path d=""M12.0386 3.93726L18.5073 8.99976L12.0386 14.0623"" stroke=""#634CFF"" stroke-width=""1.00189"" stroke-linecap=""round"" stroke-linejoin=""round""/>
              <path d=""M17.6094 9H4.49219"" stroke=""#634CFF"" stroke-width=""1.00189"" stroke-linecap=""round"" stroke-linejoin=""round""/>
            </svg>
          </div>
        </a>";
    }

    public static string GetRevenueCompletionColTitle() {
      // Exclusively change for Client
      if (SessionHelper.IsUserRoleClient) return "Cost";
      else return "Revenue";
    }

    public static string GetPartnerRevenueValue(decimal? totalRevenue, decimal? partnerPercentage, bool itemIsAssignedToUserId, bool canViewAllPartnersRevenue) {
      decimal partnerRevenue = 0;

      if (!canViewAllPartnersRevenue && !itemIsAssignedToUserId) {
        return "-"; // Do not display any amount for users where the item isn't assigned to them.

      } else if (canViewAllPartnersRevenue || (!canViewAllPartnersRevenue && itemIsAssignedToUserId)) {
        partnerRevenue = totalRevenue.GetValueOrDefault(0) * partnerPercentage.GetValueOrDefault(0);
      }

      return partnerRevenue.ToString("C");
    }

    public static string GetEventVenueHtml(string venueNameOrUrl, DateTime? eventDate = null) {
      var yesterdayDate = DateTime.UtcNow.Date.AddDays(-1);
      // The venue can either be a location name or a URL.
      // If it's a URL return a link, with just the domain name as the link text.
      if (Uri.TryCreate(venueNameOrUrl, UriKind.Absolute, out var uri)) {
        if (eventDate.HasValue && eventDate < yesterdayDate) {
          return "Online";
        }

        return $@"<a href=""{uri.AbsoluteUri}"" class=""btn btn-primary btn-xsm event-venue-link"" target=""_blank"">Join</a>";
      }
      return venueNameOrUrl.HTMLEncode();
    }

    // Title attribute added around text, or return blank if no text (i.e title attr is not included)
    // This is so that things like tooltips don't show up empty if title="". The entire title attr must be omitted.
    private static string GetOptionalTitleAttr(string titleText) {

      if (titleText.IsNullOrEmpty()) return string.Empty;

      return titleText.HTMLEncode().SurroundWith("title=\"", "\"");
    }

    private static string GetDataAttributes(DataAttributes dataAttributes) {
      if (dataAttributes.IsNullOrEmpty()) return string.Empty;
      string dataAttributesHtml = string.Empty;
      foreach (var d in dataAttributes) {
        if (d.Key.IsNullOrEmpty()) continue;
        if (dataAttributesHtml.Length > 0) dataAttributesHtml += " ";
        dataAttributesHtml += DataAttrHtml(d.Key, d.Value);
      }
      return dataAttributesHtml;
    }

    private static string DataAttrHtml(string name, object value) {
      if (name.IsNullOrEmpty()) return string.Empty;
      return AttrHtml("data-" + name.ToLowerInvariant(), value, RenderAttr.Always);
    }

    public static string GetSlideoutTriggerDataAttributes(string slideoutTitleText, string slideoutPartialUrl, bool showOnPageLoad = false) {

      return new DataAttributes(
        (DataAttrName.SlideoutTrigger, "true"),
        (DataAttrName.SlideoutTitle, slideoutTitleText),
        (DataAttrName.SlideoutPartialUrl, slideoutPartialUrl),
        showOnPageLoad ? (DataAttrName.SlideoutShowOnPageLoad, "true") : (null, null)
      ).ToHTML();
    }

    public static string GetModalTriggerDataAttributes(string modalPartialUrl, bool showOnPageLoad = false) {

      return new DataAttributes(
        (DataAttrName.ModalPartialUrl, modalPartialUrl),
        showOnPageLoad ? (DataAttrName.ModalShowOnPageLoad, "true") : (null, null)
      ).ToHTML();
    }

    public static string GetMenuIcon(MenuIconTypeEnum iconName) {

      return $@"<ion-icon name=""{MenuIconName[iconName]}"" role=""img""></ion-icon>&nbsp;";
    }

    public static string GetFormSubheader(string headerText) {

      return GetGenericRow(new RowOptions(string.Empty, 8) { RowClass = "form-subheader" }, headerText.HTMLEncode());
    }

    // NOTE: This is only to be used to parse a raw javascript date intentionally sent separately to normal form dates.
    // It parses a javascript date string (e.g. "Thu Nov 16 2023 11:54:41 GMT+0800 (Australian Western Standard Time") including the GMT offset to create a DateTimeOffset value.
    // Do NOT use this for normal form processing.
    // To parse form dates from a datepicker, use ajax.GetDatePickerDateUnspecified() which gets a DateTime? with unspecified locale (i.e. neither utc or server-local).
    public static DateTimeOffset? ParseLongBrowserDate(string longBrowserDate) {
      string dateStringToParse = longBrowserDate;
      DateTimeOffset result;
      dateStringToParse = Regex.Replace(dateStringToParse, @" GMT ?[^+-]?([0-9]{4}) ", " +$1 ", RegexOptions.IgnoreCase); // Ensure "+" if not "-".
      dateStringToParse = Regex.Replace(dateStringToParse, @"\([^\)]*\)|GMT ?", string.Empty, RegexOptions.IgnoreCase); // Remove GMT and anything in brackets.
      dateStringToParse = Regex.Replace(dateStringToParse, @"^(?:mon|tue|wed|thu|fri|sat|sun)[a-z]* ", string.Empty, RegexOptions.IgnoreCase); // Remove leading day name.
      return DateTimeOffset.TryParse(dateStringToParse, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out result) ? (DateTimeOffset?)result : null;
    }

    public static string GetFormValue(string fieldName, string defaultValue, bool urlDecode = false) {
      if (fieldName == null || !SystemWeb.HasFormFields) return defaultValue;
      string rtn = SystemWeb.GetFormValue(fieldName).TrimWhitespace();
      if (rtn == null) return defaultValue;
      return (urlDecode || SystemWeb.IsFormUrlEncoded) ? SystemWeb.UrlDecode(rtn) : rtn;
    }

    public static void AddBodyClass(string value) {
      string bodyClass = AppHelper.GetRequestItemOrNull(AppHelper.RequestItemKey.BodyClass).ToStringOrEmptyIfNull();
      AppHelper.SetRequestItem(AppHelper.RequestItemKey.BodyClass, bodyClass.AppendWithSeparator(" ", value.Trim()));
    }

    public static string GetBodyClass() {
      return AppHelper.GetRequestItemOrNull(AppHelper.RequestItemKey.BodyClass).ToStringOrEmptyIfNull();
    }

    public static bool IsRequestExiting() {
      return AppHelper.GetRequestItemOrNull<bool>(AppHelper.RequestItemKey.RequestExiting) ?? false;
    }

    public static void SetRequestExiting(bool exiting = true) {
      AppHelper.SetRequestItem(AppHelper.RequestItemKey.RequestExiting, exiting);
    }

    public static void Redirect(string destinationUrl, string message = null) {

      if (!SystemWeb.HasRequest) return;

      bool isPartial = PathHelper.IsCurrentUrlPartial();

      if (SystemWeb.IsHttpPost) {

        var ajax = AjaxSubmitHelper.GetAjaxContext();
        if (ajax != null) {
          if (!message.IsNullOrEmpty()) {
            ajax.AddDialogMessage(message);
          }
          if (!isPartial && !destinationUrl.IsNullOrEmpty()) {
            ajax.SetRedirectUrl(destinationUrl);
          }
        } else {
          SystemWeb.AddResponseHeader("Location", destinationUrl);
          EndRequest(HttpStatusEnum.TemporaryRedirect);
        }
        return;

      } else if (isPartial) {
        // Can't redirect from a partial, just show message.

        if (!message.IsNullOrEmpty()) {
          WriteAndEnd(message);
        } else {
          WriteAndEnd("An unknown problem occurred.");
        }
        EndRequest();
        return;

      } else if (!destinationUrl.IsNullOrEmpty()) {

        SystemWeb.AddResponseHeader("Location", destinationUrl);
        if (SystemWeb.IsHttpPost) {
          SystemWeb.SetStatusCode(GetHttpStatusCode(HttpStatusEnum.SeeOther));
        } else {
          SystemWeb.SetStatusCode(GetHttpStatusCode(HttpStatusEnum.TemporaryRedirect));
        }
        return;

      } else {

        SystemWeb.AddResponseHeader("Location", "/");
        EndRequest(HttpStatusEnum.TemporaryRedirect);
        return;
      }
    }

    public static void WriteJsonAndEnd<T>(T obj, HttpStatusEnum httpStatus = HttpStatusEnum.Ok) {
      string jsonStr = JsonConvert.SerializeObject(obj);
      WriteAndEnd(jsonStr, HttpContentType.json, httpStatus);
    }

    public static void WriteAndEnd(string output, HttpStatusEnum httpStatus = HttpStatusEnum.Ok) {
      WriteAndEnd(output, HttpContentType.None, httpStatus);
    }

    public static void WriteAndEnd(string output, HttpContentType contentType, HttpStatusEnum httpStatus = HttpStatusEnum.Ok) {
      if (SystemWeb.HasRequest) {
        SystemWeb.ClearResponseContent();
        if (contentType != HttpContentType.None) {
          SystemWeb.SetContentType(ContentTypeStr[contentType]);
        }
        SystemWeb.SetStatusCode(GetHttpStatusCode(httpStatus));
        SystemWeb.ResponseWrite(output);
      }
      EndRequest(contentType, httpStatus);
    }

    public static void EndRequest(HttpStatusEnum statusCode = HttpStatusEnum.Ok) {
      EndRequest(false, HttpContentType.None, statusCode);
    }
    public static void EndRequest(HttpContentType contentType, HttpStatusEnum httpStatus = HttpStatusEnum.Ok) {
      EndRequest(false, contentType, httpStatus);
    }
    public static void EndRequest(bool forceResponseEnd, HttpStatusEnum httpStatus = HttpStatusEnum.Ok) {
      EndRequest(forceResponseEnd, HttpContentType.None, httpStatus);
    }
    public static void EndRequest(bool forceResponseEnd, HttpContentType contentType, HttpStatusEnum httpStatus = HttpStatusEnum.Ok) {
      //FlushAndComplete(forceResponseEnd, contentType, httpStatus);
    }

    /// <summary>
    /// Safe "Response.End()": no aspx output, no ThreadAbort exception. See also SetRequestExiting().
    /// </summary>
    /// <param name="httpStatus">HTTP Status code for response, default 200 OK.</param>
    private static void FlushAndComplete(bool forceResponseEnd, HttpContentType contentType, HttpStatusEnum httpStatus = HttpStatusEnum.Ok) {

      if (IsRequestExiting()) return; // throw new InvalidOperationException("Method should only be called once in a request.");
      SetRequestExiting(true);

      if (!SystemWeb.HasRequest) throw new InvalidOperationException("Http Context is null.");

      if (forceResponseEnd) {
        throw new NotImplementedException("Need Core handler for response.end.");
        // context.Response.End(); // Hard stop, will raise a ThreadAbortedException in logging. currentPage == null is not expected.
      }
    }

    private static void FlushAndComplete(HttpStatusEnum statusCode = HttpStatusEnum.Ok) {
      FlushAndComplete(false, HttpContentType.None, statusCode);
    }

    public static string HtmlToText(string html) {

      string text = html.RegexReplace("</?[^>]*>", string.Empty);
      text = text.Replace("&amp;", "&").Replace("&ordm;", "º").Replace("&deg;", "º").Replace("&quot;", "\"").Replace("&nbsp;", " ");

      return text;
    }
    public class LinkInfo {

      public string InnerHtml;
      public string Href;
      public string Id;
      public string Class = string.Empty;
      public string Title = string.Empty;
      public bool NewTab = false;
      public bool Disabled = false;
      public int ClickSpinnerSeconds;
      public int ClickDisableSeconds;
      public ButtonStyle ButtonStyle = ButtonStyle.None;
      public ButtonSize ButtonSize = ButtonSize.Normal;

      public LinkInfo() { }

      public LinkInfo(string href, bool newTab = false) {
        Href = href;
        InnerHtml = href.HTMLEncode();
        NewTab = newTab;
      }

      public LinkInfo(string href, string innerHtml, bool newTab = false) {
        Href = href;
        InnerHtml = innerHtml;
        NewTab = newTab;
      }
    }

    public static string GetSimpleLink(string href, bool newPage = false) {
      return GetSimpleLink(href, href.HTMLEncode(), newPage);
    }

    public static string GetSimpleLink(string href, string innerHtml, bool newPage = false) {
      return GetLink(new LinkInfo(href, innerHtml, newPage));
    }

    public static string GetLink(LinkInfo linkInfo) {

      var html = new StringBuilder();

      html.Append($"<a href=\"{linkInfo.Href.HTMLEncode()}\"");

      html.Append(" class=\"");
      if (linkInfo.ButtonStyle != ButtonStyle.None) {
        html.Append("btn ");
        if (linkInfo.ButtonStyle == ButtonStyle.Primary) {
          html.Append("btn-primary ");
        } else if (linkInfo.ButtonStyle == ButtonStyle.Secondary) {
          html.Append("btn-secondary ");
        }
        if (linkInfo.ButtonSize == ButtonSize.Small) {
          html.Append("btn-sm ");
        } else if (linkInfo.ButtonSize == ButtonSize.XSmall) {
          html.Append("btn-xsm ");
        }
      }
      if (!linkInfo.Class.IsNullOrEmpty()) html.Append(linkInfo.Class.HTMLEncode());
      html.Append("\""); // End class quote.

      if (linkInfo.NewTab) html.Append(" target=\"_blank\"");
      if (!linkInfo.Title.IsNullOrEmpty()) html.Append($" title=\"{linkInfo.Title.HTMLEncode()}\"");
      if (!linkInfo.Id.IsNullOrEmpty()) html.Append($" id=\"{linkInfo.Id.HTMLEncode()}\"");
      if (linkInfo.Disabled) html.Append($" disabled");
      if (linkInfo.ClickSpinnerSeconds > 0) html.Append($" data-click_spinner_timeout=\"{linkInfo.ClickSpinnerSeconds * 1000}\"");
      if (linkInfo.ClickDisableSeconds > 0) html.Append($" data-click_disable_timeout=\"{linkInfo.ClickDisableSeconds * 1000}\"");

      html.Append($">{linkInfo.InnerHtml}</a>");

      return html.ToString();
    }

    public static string GetNoRecordsBadge(string noRecordText = "No records available.") {
      return $"<p class=\"badge-no-record\">{noRecordText}</p>";
    }

    public class PageTabsInfo {
      public string TabListID = null;
      public string SelectedTabName = null;
      public bool LastTabFloatRight = false;
      public WebHelper.PageTabsStyle PageTabsStyle = PageTabsStyle.Tabs;
      public bool BorderBottom = false;
    }

    public class PageTabItem {

      public string TabName { get; private set; }
      public string TabText { get; private set; }
      public bool IsDefault { get; private set; }
      public bool IsDisabled { get; set; }
      public bool IsHidden { get; set; }
      public string ItemID { get; set; }
      public string CustomHtml { get; set; }

      internal readonly string TabNameEncoded;
      internal readonly string TabTextEncoded;

      public PageTabItem() { }

      public PageTabItem(string tabName, string tabText, bool isDefault = false) {
        TabName = tabName;
        TabNameEncoded = tabName.HTMLEncode();
        TabText = tabText;
        TabTextEncoded = tabText.HTMLEncode();
        IsDefault = isDefault;
        IsDisabled = false;
        IsHidden = false;
      }
    }

    public static string GetPageTabs(PageTabsInfo pageTabsInfo, params PageTabItem[] pageTabs) {

      if (pageTabs.IsNullOrEmpty()) return string.Empty;

      var firstTab = pageTabs.FirstOrDefault(pt => pt != null); // First non-null item.
      if (firstTab == null) return string.Empty;

      // Default active tab to a) specifcied tab name, or b) first one marked default, or c) first in list.
      PageTabItem activeTab = null;
      if (!pageTabsInfo.SelectedTabName.IsNullOrEmpty()) {
        activeTab = pageTabs.FirstOrDefault(pt => pt != null && pt.TabName.Equals(pageTabsInfo.SelectedTabName, StringComparison.OrdinalIgnoreCase));
      }
      if (activeTab == null) activeTab = pageTabs.FirstOrDefault(pt => pt != null && pt.IsDefault);
      if (activeTab == null) activeTab = firstTab;

      var sb = new StringBuilder();
      string activeTabName = activeTab.TabName;

      sb.Append("<ul class=\"nav nav-tabs");
      if (pageTabsInfo.PageTabsStyle == PageTabsStyle.Links) sb.Append(" nav-tabs-style-links");
      if (pageTabsInfo.BorderBottom) sb.Append(" border-bottom");
      if (pageTabsInfo.LastTabFloatRight) sb.Append(" last-tab-right");
      sb.Append("\"");
      if (!pageTabsInfo.TabListID.IsNullOrEmpty()) sb.Append($" ID=\"{pageTabsInfo.TabListID.HTMLEncode()}\"");
      sb.AppendLine(">");

      for (int tabIndex = 0; tabIndex < pageTabs.Length; tabIndex++) {

        var pageTab = pageTabs[tabIndex];
        if (pageTab == null) continue;

        bool isLastTab = tabIndex == pageTabs.Length - 1;
        bool isTab = !pageTab.TabName.IsNullOrEmpty();

        sb.Append("<li");

        if (!pageTab.ItemID.IsNullOrEmpty()) sb.Append($" id=\"{pageTab.ItemID.HTMLEncode()}\"");
        if (isTab) sb.Append($@" role=""presentation"" data-tabname=""{pageTab.TabNameEncoded}""");

        sb.Append(" class=\"");
        if (isTab) {
          if (pageTab.TabName == activeTabName) sb.Append(" active");
          if (pageTab.IsDisabled) sb.Append(" disabled");
          if (pageTab.IsHidden) sb.Append(" hidden");
        }
        sb.Append("\"");

        sb.Append(">");

        if (isTab) {
          sb.Append($@"<a class=""nav-link"" id=""tab-{pageTab.TabNameEncoded}""");
          sb.Append($@" data-toggle=""tab"" data-tabname=""{pageTab.TabNameEncoded}""");
          sb.Append($@" href=""#panel-{pageTab.TabNameEncoded}"" role=""tab""");
          sb.Append($@" aria-controls=""panel-{pageTab.TabNameEncoded}"" aria-selected=""true"">");
        }
        if (!pageTab.CustomHtml.IsNullOrEmpty()) {
          sb.Append(pageTab.CustomHtml);
        } else if (!pageTab.TabTextEncoded.IsNullOrEmpty()) {
          sb.Append(pageTab.TabTextEncoded);
        }
        if (isTab) sb.Append("</a>");

        sb.AppendLine("</li>");
      }

      sb.AppendLine("</ul>");

      sb.AppendLine(@"<div class=""tab-content"">");
      foreach (var pageTab in pageTabs) {
        if (pageTab == null) continue;
        sb.AppendLine($@"<div class=""tab-pane tab-quote tab-{pageTab.TabNameEncoded} fade in {(pageTab.TabName == activeTabName ? "active" : string.Empty)}"" id=""panel-{pageTab.TabNameEncoded}"" role=""tabpanel"" aria-labelledby=""tab-{pageTab.TabNameEncoded}""></div>");
      }
      sb.AppendLine("</div>");

      // Force firing of 'shown.bs.tab' event for the first active tab.
      // Delay gives some time for listeners to be added.
      sb.AppendLine(@"
        <script>
          (function ($) {
            $(document).ready(function() {
              setTimeout(function() {
                $('#tab-" + activeTabName.HTMLEncode() + @"').trigger('shown.bs.tab');
              }, 500);
            });
          })(jQuery);
        </script>");

      return sb.ToString();
    }

    public static string GetListViewMainColumnText(string mainTile, string underTitle) {
      string underTitleDisplay = underTitle.EmptyIfNull().SurroundWith("<p class=\"under-title\">", "</p>", false);
      string mainTitleDisplay = underTitle.IsNullOrEmptyOrWhitespace() ? mainTile : mainTile.SurroundWith("<b>", "</b>", true);
      return $@"
        <div class=""listview-col-content w100p"">
          {mainTitleDisplay.EnsureStartsWith("<p class=\"main-title\">", true).EnsureEndsWith($"</p>", StringExt.Ensure.IfNotBlank)}
          {underTitleDisplay}
        </div>";
    }

    public static string GetListViewLocatorColumnHtml(string projectName, string jobNumber, string companyName) {
      return $@"
        <div class=""listview-col-content w100p"">
          {projectName.EnsureStartsWith("<p class=\"main-title\"><b>", true).EnsureEndsWith($"</b></p>", StringExt.Ensure.IfNotBlank)
          .SurroundWith("<div class=\"flex-inline w100p\">", $" {jobNumber.EmptyIfNull().SurroundWith("&nbsp; <span>(", ")</span>", false)}</div>")}
          {companyName.EmptyIfNull().SurroundWith("<p class=\"under-title\">", "</p>", false)}
        </div>";
    }

    public static class Logging {

      private static string RequestItemName_WebHelperLog = "WebHelperLog"; // Must be unique to this class.

      public static List<string> GetLog() {
        if (!AppHelper.RequestItemExists(RequestItemName_WebHelperLog)) AppHelper.SetRequestItem(RequestItemName_WebHelperLog, new List<string>());
        return (List<string>)AppHelper.GetRequestItemOrNull(RequestItemName_WebHelperLog);
      }

      public static void AddLog(string LogText) {
        if (LogText.IsNullOrEmpty()) return;
        GetLog().Add(LogText);
      }

      public static string GetLastLogText() {
        var RequestLog = GetLog();
        if (RequestLog.Count == 0) return string.Empty;
        else return RequestLog.Last();
      }
    }

    // Get value of score as a percentage, e.g. 8.3 out of 10 = "83%"
    public static string GetCSSPercentFromRatio(double? score, double? maximum) {
      return GetCSSPercentFromRatio((decimal?)score, (decimal?)maximum);
    }
    public static string GetCSSPercentFromRatio(decimal? score, decimal? maximum) {
      if (score == null || maximum == null) return "0";
      decimal percent = score.Value / maximum.Value * 100;
      return percent.ToString("0") + "%";
    }

    public class PartialLoaderOptions {
      public string ID;
      public string Url;
      public PartialLoaderStyle InitialStyle = PartialLoaderStyle.Default;
      public PartialLoaderStyle LoaderStyle = PartialLoaderStyle.Default;
      public bool DeferInitialLoad = false;
      public string InitialWidth = "100%";
      public string InitialHeight = "100px";
      public string WaitForPartialID = string.Empty;
      public bool WaitUntilVisible = false;
      public string WaitForPageTabName = string.Empty;
      public int DelayMs = 0;
    }

    public static string GetPartialLoaderHtml(PartialLoaderOptions options) {
      // Note make sure these data names match those in common-functions.js InitPartialLoaders()
      string html = $"<div"
        + (options.ID.IsNullOrEmpty() ? string.Empty : $" ID=\"{options.ID.HTMLEncode()}\"")
        + $" class=\"partial-loader-container\""
        + $" data-partial-url=\"{options.Url.HTMLEncode()}\""
        + $" data-partial-initial-style=\"{options.InitialStyle.ToString().ToLowerInvariant()}\""
        + $" data-partial-loader-style=\"{options.LoaderStyle.ToString().ToLowerInvariant()}\""
        + $" data-partial-initial-width=\"{options.InitialWidth}\""
        + $" data-partial-initial-height=\"{options.InitialHeight}\""
        + $" data-partial-defer-initial-load=\"{(options.DeferInitialLoad ? "true" : "false")}\""
        + $" data-partial-waitforid=\"{options.WaitForPartialID.HTMLEncode()}\""
        + $" data-partial-waituntilvisible=\"{(options.WaitUntilVisible ? "true" : "false")}\""
        + $" data-partial-waitforpagetabname=\"{options.WaitForPageTabName.HTMLEncode()}\""
        + $" data-partial-delayms=\"{options.DelayMs}\""
        + $" data-partial-rndid=\"{(new Random()).Next(1000, 10000)}\"";
      html += "></div>";
      return html;
    }

    public static string GetJQueryPartialInfo() {
      // Return jquery call to get partialinfo js object.
      // Usage in partial scripts: var partialInfo = <%= WebHelper.GetJQueryPartialInfo() %>;
      string randomId = GetQueryStringValue(PathHelper.AbleUrlKeys.PartialRandomId);
      return $@"$('.partial-loader-container[data-partial-rndid=""{randomId}""]').data(""partial-info"")";
    }

    public static string GetCoachTagsHtml(List<int> PartnerTagIdList) {

      const int MaxTags = 9;
      const int MaxRemainingTags = 3;
      string tagStr = string.Empty;
      int tagCount = 0;

      var AllTagInfo = DbHelper.PartnerTags.GetAllTags(new DbHelper.PartnerTags.GetAllTagsParams() { OnlyAssignedTags = true, OnlyPartnerTags = true });

      if (!PartnerTagIdList.IsNullOrEmpty()) {
        foreach (int tagId in PartnerTagIdList) {
          foreach (var cat in AllTagInfo.CategoryList) {
            if (cat.TagsById.TryGetValue(tagId, out var tagInfo)) {
              tagCount++;
              tagStr += "<span>" + tagInfo.TagName.HTMLEncode() + "</span>";
              if (tagCount >= MaxTags && PartnerTagIdList.Count > MaxTags + MaxRemainingTags) {
                tagStr += "<span class=\"moreTags\"> +" + (PartnerTagIdList.Count - tagCount) + " more.</span>";
                return tagStr;
              }
            }
          }
        }
      }
      return tagStr;
    }

    public static string GetDevOrStagingSiteTagText() {

      if (ConfigHelper.IsDevServer) return "[dev]";

      if (ConfigHelper.IsStagingServer) {
        // There isn't a separate "IsDevelopServer" flag, so check domain name for now (i.e. integral-able-develop.azurewebsites.net vs integral-able-staging..).
        if (SystemWeb.RequestUrlHost.ContainsIgnoreCase("able-develop")) {
          return "[develop]";
        } else {
          return "[staging]";
        }
      }

      return string.Empty; // Don't show anything on prod.
    }

    public static string GetLoggedOutAbleLogo() {

      return $@"
        <div class=""able-logo"">
          <img src=""{PathHelper.Images.AbleHeaderLogo()}"" alt=""Able"" /> {GetDevOrStagingSiteTagText()}
          {(ConfigHelper.EmailRecipientOverrideAddress.IsNullOrEmpty() ? "" : $"<div>All email goes to: {ConfigHelper.EmailRecipientOverrideAddress.HTMLEncode()}</div>")}
        </div>";
    }

    public enum AlertBannerType { Succes, Info, Warning, Danger }
    private static Dictionary<AlertBannerType, string> AlertBannerClasses = new Dictionary<AlertBannerType, string>() {
      { AlertBannerType.Succes, "alert-success" },
      { AlertBannerType.Info, "alert-info" },
      { AlertBannerType.Warning, "alert-warning" },
      { AlertBannerType.Danger, "alert-danger" }
    };

    public static string GetAlertBanner(AlertBannerType alertBannerType, string messageHtml) {

      if (!AlertBannerClasses.ContainsKey(alertBannerType)) return string.Empty;

      return $@"<div class=""alert {AlertBannerClasses[alertBannerType]}"" role=""alert"">{messageHtml}</div>";
    }

    public static string GetProgramTeamMemberCard(DbHelper.AblePrograms.ProgramTeamMember teamMember) {

      var roles = new List<string>();

      if (teamMember.IsLeadConsultant) roles.Add("Project Lead");
      if (teamMember.IsProjectCoordinator) roles.Add("Coordinator");
      if (teamMember.IsCoach) roles.Add("Coaching");
      if (teamMember.IsFacilitator) roles.Add("Facilitator");

      string rolesHtml = string.Empty;
      rolesHtml += @"<div class=""flex flex-wrap gap3"">";
      foreach (string role in roles) {
        rolesHtml += $@"<div class=""badge no-min-height"">{role.HTMLEncode()}</div>";
      }
      rolesHtml += @"</div>";

      string location = teamMember.State.AppendWithSeparator(", ", teamMember.Country);
      string locationHtml = string.Empty;
      if (!location.IsNullOrEmptyOrWhitespace()) {
        locationHtml = $@"<div class=""flex gap5""><b>Location:</b>{location.HTMLEncode()}</div>";
      }

      string mobileNumberHtml = string.Empty;
      if (!teamMember.MobileNumber.IsNullOrEmptyOrWhitespace()) {
        mobileNumberHtml = $@"<div class=""flex gap5""><b>Mobile:</b>{teamMember.MobileNumber.HTMLEncode()}</div>";
      }

      return GetCoachInfoCard(teamMember.UserId, teamMember.FirstName, teamMember.LastName, $@"
        <div class=""font-size-07rem flex-column gap3 mt5"">
          {locationHtml}
          {mobileNumberHtml}
          <div class=""flex gap5""><b>Role:</b>{rolesHtml}</div>
        </div>");
    }

    public static string GetCoachInfoCard(DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo) {

      string cardFooter = $@"
        <div class=""team-card-stats"">
          <span class=""sessions-count"">{coachInfo.TotalCoachingSessionsCompleted}</span>
          <span class=""sessions-label"">completed coaching sessions</span>
        </div>
        <button class=""btn btn-primary w100p"">See more</button>";

      string coachBio = coachInfo.PartnerBio_CoachCardBio.ToStringOrDefaultIfNull(coachInfo.BioShort).LimitLengthTo(ConfigHelper.Coach_ShortBio_MaxLength, "...");

      return GetCoachInfoCard(coachInfo.UserId, coachInfo.FirstName, coachInfo.LastName, coachBio, cardFooter);
    }

    private static string GetCoachInfoCard(int userId, string firstName, string lastName, string cardBodyHtml, string cardFooterHtml = "") {

      string slideoutAttr = GetSlideoutTriggerDataAttributes("Partner Details", PathHelper.Partials.PartnerSlideoutPanel(userId));

      return $@"
        <div class=""team-card flex-column info-card-hover margin-align-center-xsm"" {slideoutAttr}>
          <div class=""team-card-avatar"">
            <img class=""profile-photo"" src=""{PathHelper.Images.UserPhoto(firstName, lastName, PathHelper.Images.UserPhotoSize.Large, true)}"" />
          </div>
          <h3 class=""team-card-name"">{firstName.HTMLEncode()} {lastName.HTMLEncode()}</h3>
          {(cardBodyHtml.IsNullOrEmpty() ? string.Empty : $"<div class=\"team-card-body\">{cardBodyHtml}</div>")}
          {(cardFooterHtml.IsNullOrEmpty() ? string.Empty : $"<div class=\"team-card-footer\">{cardFooterHtml}</div>")}
        </div>";
    }

    public static string GetPeopleMetrics(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {

      if (companyInfo == null) return null;

      string metricsHtml = string.Empty;

      metricsHtml +=
        GetOverviewScoreBox(
          "Participants",
          $"There are {companyInfo.TotalActiveCoachees} active participants out of {companyInfo.TotalCoachees}.",
          new OverviewBoxScore(companyInfo.TotalActiveCoachees.ToString(), "/ " + companyInfo.TotalCoachees.ToString(), string.Empty));

      if (companyInfo.NumberOfStaff.GetValueOrDefault(0) > 0) {

        metricsHtml +=
          GetOverviewScoreBox(
            "Onboarding Score",
            $"Out of the {companyInfo.NumberOfStaff} people in the organisation's staff, {companyInfo.TotalCoachees} are participants.",
            new OverviewBoxScore(GetScorePercentage(companyInfo.TotalCoachees, companyInfo.NumberOfStaff.GetValueOrDefault(0)), "%"));

      } else {

        metricsHtml +=
          GetOverviewScoreBox(
            "Onboarding Score",
            "Please fill the field 'Total Number of Staff' field in your organisation's settings.",
            null,
            companyInfo.NumberOfStaff.GetValueOrDefault(0) > 0 ? null : PathHelper.Pages.OrganisationSettings(companyInfo.CompanyId),
            new OverviewBoxScore(null, "Set total number of staff"));
      }

      metricsHtml +=
        GetOverviewScoreBox(
          "Active Learners",
          $"Out of {companyInfo.TotalCoachees} participants in the organisation, {companyInfo.ActiveLearnerCount} are currently active.",
          new OverviewBoxScore(companyInfo.ActiveLearnerCount.ToString(), "/ " + companyInfo.TotalCoachees.ToString(), string.Empty));

      metricsHtml +=
        GetOverviewScoreBox(
          "360s Completed",
          "Surveys 360 completed by participants in the organisation.",
          string.Empty,
          PathHelper.Pages.OrganisationCapabilities(companyInfo.CompanyId),
          new OverviewBoxScore(companyInfo.Total360sCompleted.ToString()));

      decimal? averageEvalScore = DbHelper.EvalSurveys.GetAverageEvalScoreForCompany(companyInfo.CompanyId);
      metricsHtml +=
        GetOverviewScoreBox(
          "Average Eval Score",
          "Evaluations completed by participants in the organisation.",
          new OverviewBoxScore(averageEvalScore.ToString("#.0", "-")));

      return metricsHtml.ToString();

      string GetScorePercentage(double doneScore, double totalScore) {
        if (totalScore <= 0 || doneScore <= 0) return "0";
        if (doneScore >= totalScore) return "100";
        return ((doneScore * 100) / totalScore).ToString("F1");
      }
    }

    public class OverviewBox {
      public string TitleText, TitleTooltipText, BodyHtml, ExtraClasses, LinkUrl, ExtraAttributesHtml;
      public bool ApplyHoverStyle = false;
      public OverviewBox(string titleText, string bodyHtml) { // These required, rest optional.
        TitleText = titleText;
        BodyHtml = bodyHtml;
      }
    }

    public class OverviewBoxScore {
      public string MainText { get; private set; }
      public string SubText { get; private set; }
      public string CustomClass { get; private set; }
      public string LinkUrl { get; private set; }
      public OverviewBoxScore(string mainText, string subText = null, string customClass = null, string linkUrl = null) {
        MainText = mainText;
        SubText = subText;
        CustomClass = customClass;
        LinkUrl = linkUrl;
      }
    }

    // Generic box. Called by the other specialised box functions.
    private static string GetOverviewBox(OverviewBox boxInfo) {

      // class structure is:
      // .overview-box (flex column)
      // | .body
      // | .title

      string boxClassesHtml = $"overview-box {boxInfo.ExtraClasses.HTMLEncode()}";
      if (!boxInfo.LinkUrl.IsNullOrEmpty() || boxInfo.ApplyHoverStyle) boxClassesHtml += " box-hover";

      string boxAttributesHtml = boxInfo.ExtraAttributesHtml;
      if (!boxInfo.LinkUrl.IsNullOrEmpty()) boxAttributesHtml += $@" onclick=""location.href='{boxInfo.LinkUrl.HTMLEncode()}'""";

      string html = string.Empty;
      html += $@"<div class=""{boxClassesHtml}"" {boxAttributesHtml}>";
      html += $@"<div class=""body"">{boxInfo.BodyHtml}</div>";
      if (!boxInfo.TitleText.IsNullOrEmpty()) {
        html += $@"<div class=""title"">{(GetTooltipWithContent(boxInfo.TitleText.HTMLEncode(), null, ToolTipContentType.Text, boxInfo.TitleTooltipText))}</div>";
      }
      html += "</div>";

      return html;
    }

    public static string GetOverviewScoreBox(string titleText, string titleTooltipText, params OverviewBoxScore[] scores) {
      return GetOverviewScoreBox(titleText, titleTooltipText, null, null, scores);
    }

    public static string GetOverviewScoreBox(string titleText, string titleTooltipText, string customClass, string linkUrl, params OverviewBoxScore[] scores) {

      string html = string.Empty;
      const string scoreClass = "score";

      if (!scores.IsNullOrEmpty()) {

        html += $@"<div class=""scores"">";

        foreach (var score in scores) {

          if (score == null || score.MainText.IsNullOrEmpty() && score.SubText.IsNullOrEmpty()) continue;

          if (!score.LinkUrl.IsNullOrEmpty()) { // Surround score with link or div.
            html += $@"<a class=""{scoreClass} {score.CustomClass.HTMLEncode()}"" href=""{score.LinkUrl.HTMLEncode()}"">";
          } else {
            html += $@"<div class=""{scoreClass} {score.CustomClass.HTMLEncode()}"">";
          }

          if (!score.MainText.IsNullOrEmpty()) html += $@"<span class=""score-main"">{score.MainText.HTMLEncode()}</span>";
          if (!score.SubText.IsNullOrEmpty()) html += $@"<span class=""score-sub"">{score.SubText.HTMLEncode()}</span>";

          if (!score.LinkUrl.IsNullOrEmpty()) { // Finish link or div for item.
            html += "</a>";
          } else {
            html += "</div>";
          }
        }

        html += "</div>";
      }

      return GetOverviewBox(new OverviewBox(titleText, html) {
        TitleTooltipText = titleTooltipText,
        ExtraClasses = customClass,
        LinkUrl = linkUrl
      });
    }

    public static string GetOverviewUserBox(string titleText, DbHelper.AbleUser.AbleUserBasicInfo userInfo) {

      if (userInfo == null) return string.Empty;

      string html = $"{GetProfileImage(PathHelper.Images.UserPhoto(userInfo, PathHelper.Images.UserPhotoSize.Thumbnail, true))}<div>{userInfo.GetFullName().HTMLEncode()}</div>";

      return GetOverviewBox(
        new OverviewBox(titleText, html) {
          ApplyHoverStyle = true,
          ExtraAttributesHtml = GetSlideoutTriggerDataAttributes("Partner Details", PathHelper.Partials.PartnerSlideoutPanel(userInfo.UserId))
        });
    }

    public static string GetCompanyLeadBox(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {

      if (companyInfo?.ClientLeadUserId == null) return string.Empty;

      string html = $@"<div class=""company-lead-box"">{GetProfileImage(PathHelper.Images.UserPhoto(companyInfo, PathHelper.Images.UserPhotoSize.Thumbnail, true))}</div>";

      return GetOverviewBox(
        new OverviewBox("Client Lead", html) {
          ApplyHoverStyle = true,
          ExtraAttributesHtml = GetSlideoutTriggerDataAttributes("Partner Details", PathHelper.Partials.PartnerSlideoutPanel(companyInfo.ClientLeadUserId))
        });
    }

    public static int GetNextRequestSequentialNumber() {
      // Return a sequential number starting from 1, incremented within the scope of the current request.
      // Useful to use with element IDs which need to be unique on the page.
      int.TryParse(AppHelper.GetRequestItemOrNull(AppHelper.RequestItemKey.SequentialNumber).ToStringOrEmptyIfNull(), out int n);
      n += 1;
      AppHelper.SetRequestItem(AppHelper.RequestItemKey.SequentialNumber, n);
      return n;
    }

    // This is potentially dangerous, should only be called within WebHelper with safe literal javascript - no user input.
    private static string GetJQueryScriptBlock(string safeJavascript) {
      return ($@"
        <script>
          (function ($) {{
            $(document).ready(function () {{
              {safeJavascript};
            }});
          }})(jQuery);
        </script>");
    }

    /// <summary>
    /// Converts a dictionary to an application/x-www-form-urlencoded string.
    /// Example: {{"a","1"},{"b","hello world"}} -> "a=1&b=hello+world"
    /// </summary>
    public static string ToFormUrlEncoded(ICollection<KeyValuePair<string, string>> data) {

      if (data == null || data.Count == 0) return string.Empty;

      var sb = new StringBuilder();

      foreach (var kvp in data) {
        if (kvp.Key.IsNullOrEmpty()) continue;
        if (sb.Length > 0) sb.Append('&');
        sb.Append(kvp.Key.URLEncode());
        sb.Append('=');
        if (!kvp.Value.IsNullOrEmpty()) sb.Append((kvp.Value.URLEncode()));
      }

      return sb.ToString();
    }

    public static string MarkdownToHtml(string markdownText) {

      markdownText = markdownText.RegexReplace(@"\?{2,}", string.Empty); // Remove "??" resulting from failed emojis.
      return $@"<div class=""markdown-to-html"">{Markdig.Markdown.ToHtml(markdownText)}</div>";
    }

    public class QuoteSigning_FormValues : DbHelper.Interfaces.IQuoteSignoffInfo {
      public string ClientFirstName { get; set; }
      public string ClientLastName { get; set; }
      public string ClientEmail { get; set; }
      public string AccFirstName { get; set; }
      public string AccLastName { get; set; }
      public string AccEmail { get; set; }
      public bool PurchaseOrderRequired { get; set; }
      public string PurchaseOrderNumber { get; set; }
    }

    public static class FilePond {

      public static string GetDeleteFilename() {

        if (!SystemWeb.HasRequest) return null;
        string fileName;
        using (var reader = new StreamReader(SystemWeb.RequestInputStream)) {
          fileName = reader.ReadToEnd();
          reader.Close();
        }
        SystemWeb.RequestInputStream.Position = 0;
        return fileName;
      }

    }

  }
}
