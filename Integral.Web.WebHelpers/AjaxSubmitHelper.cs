using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Integral.Web {

  public class AjaxSubmitHelper {

    private Dictionary<string, string> dicBadFields;
    private Dictionary<string, object> dicReturnValues;

    private string successDialogMessage = "";
    private string successDialogTitle = "";
    private string dialogConfirmMessage = "";
    private string buttonMessage = null;
    private string dialogConfirmTitle = "";
    private string dialogConfirmFieldName = "";
    private string redirectUrl = "";
    private bool redirectReplace = false;
    private bool reloadPage = false;

    public class JsonKeys {
      public const string ResponseLog = "responseLog";
    }

    private PageMessageType pageMessageType = PageMessageType.None;
    private ResultStatusEnum resultStatus = ResultStatusEnum.Unset;
    private WebHelper.HttpStatusEnum httpStatus = WebHelper.HttpStatusEnum.Ok;
    private const string BadFields_Dialog_Key = "dialog";
    private const string BadFields_Status_Key = "status";

    // List of toasts to show - makes it possible to show more than 1 if needed.
    private List<ToastInfo> toastList = new List<ToastInfo>();

    public Uri Referrer = null;

    public string Action { get; private set; }

    private enum ResultStatusEnum {
      Unset = 0,
      Error = 1,
      Success = 2
    }

    // Message types for page load (reload, redirect)
    public enum PageMessageType {
      None = 0,
      InfoDialog = 1,
      SuccessDialog = 2,
      ErrorDialog = 3,
      InfoToast = 4,
      SuccessToast = 5,
      ErrorToast = 6
    }

    // Toast types for same-page ajax response.
    public enum ToastType {
      Info = 1,
      Success = 2,
      Error = 3
    }

    private class ToastInfo {
      public ToastType type = ToastType.Info;
      public string message;
      public ToastInfo(ToastType type, string message) {
        this.type = type;
        this.message = message;
      }
    }

    private AjaxSubmitHelper() {

      // Remember this object if needed elsewhere during the request.
      AppHelper.SetRequestItem(ConfigHelper.RequestItems.AjaxHelper_Object, this);

      // The page sending the ajax request.
      // See common-functions.js, it sets this header for all jqXHR requests.
      Referrer = WebHelper.GetReferrerUri();

      // Universal field used to specify an action to perform.
      Action = WebHelper.GetAjaxaction();

      dicBadFields = new Dictionary<string, string>(); // Field name, message.
      dicReturnValues = new Dictionary<string, object>(); // Name, Value.
      resultStatus = ResultStatusEnum.Unset;
    }

    public static AjaxSubmitHelper GetAjaxContext() {
      return AppHelper.GetRequestItemOrNull(ConfigHelper.RequestItems.AjaxHelper_Object) as AjaxSubmitHelper;
    }

    public static void Process(Action<AjaxSubmitHelper> action) {

      // If an ajax object has already been created in this request, then re-use it
      // to prevent accidental recreation and losing existing state.
      // See AjaxSubmitHelper constructor, which adds the new object to request storage.
      // If one does not exist in request storage then create a new one.
      var ajax = GetAjaxContext() ?? new AjaxSubmitHelper();

      action(ajax);

      WebHelper.WriteAndEnd(ajax.toJson(), WebHelper.HttpContentType.json, ajax.httpStatus);
    }

    public int BadFieldCount {
      get {
        return dicBadFields.Count;
      }
    }

    public bool BadFieldExists(string fieldName) {
      return dicBadFields.ContainsKey(fieldName.EmptyIfNull());
    }

    public string FormValue(string fieldName, string defaultValue = null) {
      return WebHelper.GetFormValue(fieldName, defaultValue);
    }

    public string CheckFieldRaw(string fieldName, string fieldTitle, bool isRequired) {
      var formValue = WebHelper.GetFormValue(fieldName, "");
      if (formValue.IsNullOrEmpty() && isRequired) AddBadField(fieldName, fieldTitle + " is required.");
      return formValue;
    }

    public string CheckFieldEmail(string fieldName, string fieldTitle, bool isRequired, string customInvalidMsg = null) {
      return CheckFieldEmail(fieldName, WebHelper.GetFormValue(fieldName, ""), fieldTitle, isRequired, customInvalidMsg);
    }
    public string CheckFieldEmail(string fieldName, string sFieldValue, string fieldTitle, bool isRequired, string customInvalidMsg = null) {
      return CheckFieldRegex(fieldName, sFieldValue, fieldTitle, null, AppHelper.Regex.Email, isRequired, customInvalidMsg);
    }

    public string CheckFieldMobile(string fieldName, string fieldTitle, bool isRequired, string customInvalidMsg = null) {
      return CheckFieldMobile(fieldName, WebHelper.GetFormValue(fieldName, ""), fieldTitle, isRequired, customInvalidMsg);
    }
    public string CheckFieldMobile(string fieldName, string sFieldValue, string fieldTitle, bool isRequired, string customInvalidMsg = null) {
      return CheckFieldRegex(fieldName, sFieldValue, fieldTitle, null, AppHelper.Regex.Mobile, isRequired, customInvalidMsg);
    }

    public string CheckFieldPlainText(string fieldName, string fieldTitle, bool isRequired, string customInvalidMsg = null) {
      return CheckFieldRegex(fieldName, WebHelper.GetFormValue(fieldName, ""), fieldTitle, null, AppHelper.Regex.GeneralText, isRequired, customInvalidMsg);
    }

    public string CheckFieldRegex(string fieldName, string fieldTitle, string sRegex, bool isRequired, string customInvalidMsg = null) {
      return CheckFieldRegex(fieldName, WebHelper.GetFormValue(fieldName, ""), fieldTitle, null, sRegex, isRequired, customInvalidMsg);
    }

    public string CheckFieldRegex(string fieldName, string fieldTitle, int maxLength, string sRegex, bool isRequired, string customInvalidMsg = null) {
      return CheckFieldRegex(fieldName, WebHelper.GetFormValue(fieldName, ""), fieldTitle, maxLength, sRegex, isRequired, customInvalidMsg);
    }

    private string CheckFieldRegex(string fieldName, string sFieldValue, string fieldTitle, int? maxLength, string sRegex, bool isRequired, string customInvalidMsg) {

      string sFormValue = Regex.Replace(sFieldValue.EmptyIfNull(), "^\\s+|\\s+$", "");

      if (sRegex == AppHelper.Regex.Mobile) {
        // remove spaces or dots in mobile number
        sFormValue = Regex.Replace(sFormValue, @"[\s.]", "");
      }

      if (isRequired || sFormValue != "") {
        if (sRegex.Substring(0, 1) != "^") sRegex = "^" + sRegex;
        if (sRegex.Substring(sRegex.Length - 1, 1) != "$") sRegex += "$";
        if (sFormValue == "")
          AddBadField(fieldName, fieldTitle + " is required.");
        else if (!Regex.IsMatch(sFormValue, sRegex, RegexOptions.IgnoreCase | RegexOptions.Multiline)) {
          if (!customInvalidMsg.IsNullOrEmpty())
            AddBadField(fieldName, customInvalidMsg);
          else {
            AddBadField(fieldName, "Invalid characters.");
            // Return only characters that match the regex.
            Match match = Regex.Match(sFormValue, sRegex, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            sFormValue = "";
            while (match.Success) {
              sFormValue += match.ToString();
              match = match.NextMatch();
            }
          }
        }
      }

      if (maxLength != null && sFormValue != null && sFormValue.Length > maxLength) {
        AddBadField(fieldName, $"{(fieldTitle.ValueIfNullOrEmpty("Text"))} cannot exceed {maxLength} characters.");
      }

      return sFormValue;
    }

    public bool GetCheckbox(string fieldName) {
      return WebHelper.GetFormValue(fieldName) == WebHelper.DefaultCheckboxValue;
    }

    public bool TryGetDatePickerDateUnspecified(string fieldName, string fieldTitle, bool isRequired, out DateTime resultDate, DateTime defaultIfInvalid, string invalidMsg) {
      DateTime? dtTemp;
      dtTemp = GetDatePickerDateUnspecified(fieldName, fieldTitle, isRequired, invalidMsg);
      if (dtTemp == null) {
        resultDate = defaultIfInvalid;
        return false;
      }
      resultDate = (DateTime)dtTemp;
      return true;
    }

    public DateTime? GetDatePickerDateUnspecified(string fieldName, string fieldTitle, bool isRequired, string invalidMsg) {
      string sFormValue = WebHelper.GetFormValue(fieldName, "");
      if (sFormValue.IsNullOrEmpty()) {
        if (isRequired) AddBadField(fieldName, fieldTitle + " is required.");
        return null;
      }
      var dt = WebHelper.GetDatePickerDateUnspecified(sFormValue);
      if (dt == null) AddBadField(fieldName, invalidMsg); // If it has a value then it must be a valid date or fail.
      return dt;
    }

    public DateTime? GetDatePickerToUtc(string fieldName, TimeZoneInfo userTimeZone, string fieldTitle, bool isRequired, string invalidMsg) {
      var dt = GetDatePickerDateUnspecified(fieldName, fieldTitle, isRequired, invalidMsg);
      return dt.ToUniversalTimeOrNull(userTimeZone);
    }

    public DateTimeOffset? GetMomentFormatDate(string fieldName, string fieldTitle, bool isRequired, string invalidMsg) {
      // Assumes input string is the result of MomentJS format() - i.e. ISO string with UTC offset incuded, e.g. "+0800".
      string momentJSdate = WebHelper.GetFormValue(fieldName);
      if (momentJSdate.IsNullOrEmpty()) {
        if (isRequired) AddBadField(fieldName, fieldTitle + " is required.");
        return null;
      }
      DateTimeOffset dt;
      if (DateTimeOffset.TryParse(momentJSdate, out dt)) {
        return dt;
      } else {
        AddBadField(fieldName, invalidMsg);
        return null; // failed
      }
    }

    public TimeSpan? CheckTimePickerSpan(string fieldName, string fieldTitle, bool isRequired, string invalidMsg) {
      string h, m, t;
      var ts = WebHelper.GetFormTimePickerSpan(fieldName, out h, out m, out t);
      if ("" + h + m == "") {
        if (isRequired) AddBadField(fieldName, fieldTitle + " is required.");
        return null;
      }
      if (ts == null) AddBadField(fieldName, invalidMsg);
      return ts;
    }

    public Guid? CheckGuid(string fieldName, string fieldTitle, bool isRequired, string invalidMsg) {
      Guid guid;
      var guidString = WebHelper.GetFormValue(fieldName);
      if (guidString.IsNullOrEmpty()) {
        if (isRequired) AddBadField(fieldName, fieldTitle + " is required.");
        return null;
      }
      if (Guid.TryParse(guidString, out guid)) {
        return guid;
      } else {
        AddBadField(fieldName, invalidMsg);
        return null;
      }
    }

    public int? CheckFieldIDOrNull(string fieldName, string fieldTitle, bool isRequired, string invalidMsg) {
      int? iFormValue = WebHelper.GetFormValue(fieldName, "").Trim().ToIntOrNull();
      if (iFormValue == null && isRequired) AddBadField(fieldName, fieldTitle + " is required.");
      if (iFormValue < 1) AddBadField(fieldName, invalidMsg);
      return iFormValue;
    }

    public int CheckFieldID(string fieldName, string fieldTitle, bool isRequired, string invalidMsg) {
      string sFormValue = WebHelper.GetFormValue(fieldName, "").Trim();
      int iFormValue = 0;
      if (sFormValue.IsNullOrEmpty()) {
        if (isRequired) AddBadField(fieldName, fieldTitle + " is required.");
      } else {
        if (!int.TryParse(sFormValue, out iFormValue) || iFormValue < 0) AddBadField(fieldName, invalidMsg);
      }
      return iFormValue;
    }

    public int CheckFieldInt(string fieldName, string fieldTitle, int? iMin, int? iMax, bool isRequired, string invalidMsg) {

      int? val = CheckFieldIntOrNull(fieldName, fieldTitle, iMin, iMax, isRequired, invalidMsg);
      if (val == null) return iMin.GetValueOrDefault(0);

      return (int)val;
    }
    public int CheckFieldInt(string fieldName, bool isRequired, string invalidMsg = "") {
      return CheckFieldInt(fieldName, "", null, null, isRequired, invalidMsg);
    }

    public int? CheckFieldIntOrNull(string fieldName, string fieldTitle, int? iMin, int? iMax, bool isRequired, string invalidMsg) {

      string sFormValue = WebHelper.GetFormValue(fieldName, "").EmptyIfNull().Trim();
      int iFormValue;

      if (sFormValue == "") {
        if (isRequired) AddBadField(fieldName, fieldTitle + " is required.");
        return null;
      } else if (!int.TryParse(sFormValue, out iFormValue)) {
        AddBadField(fieldName, invalidMsg);
        return null;
      } else {
        if (iMin != null && iFormValue < iMin)
          AddBadField(fieldName, invalidMsg);
        else if (iMax != null && iFormValue > iMax)
          AddBadField(fieldName, invalidMsg);
      }
      return iFormValue;
    }
    public int? CheckFieldIntOrNull(string fieldName, bool isRequired, string invalidMsg = "") {
      return CheckFieldIntOrNull(fieldName, "", null, null, isRequired, invalidMsg);
    }
    public int? CheckFieldIntOrNull(string fieldName) {
      return CheckFieldIntOrNull(fieldName, "", null, null, false, "");
    }

    public List<int> CheckFieldIntList(string fieldName) {
      var intList = new List<int>();
      string strList = WebHelper.GetFormValue(fieldName, "").Trim();
      if (strList.IsNullOrEmpty()) return intList;
      if (!Regex.IsMatch(strList, AppHelper.Regex.IntegerList)) AddBadField(fieldName, "Please type a list of numbers separated by commas.");
      else intList = strList.ToIntList();
      return intList;
    }

    public string DoValueValidation(string formValue, string sRegex) {
      if (formValue.IsNullOrEmpty()) return null;
      if (!Regex.IsMatch(formValue, sRegex)) {
        AddBadField("formValue", "Invalid characters in form value.");
        return null;
      }
      return formValue;
    }

    public decimal? CheckFieldDecimal(string fieldName, string fieldTitle, bool isInteger, int? iMin, int? iMax, bool isRequired, string invalidMsg) {
      string valueStr = WebHelper.GetFormValue(fieldName, "").EmptyIfNull().Trim();
      if (valueStr == "") {
        if (isRequired) AddBadField(fieldName, fieldTitle + " is required.");
        return null;
      }
      decimal valueNum = 0;
      bool parsed = decimal.TryParse(valueStr, out valueNum);
      if (!parsed) {
        AddBadField(fieldName, invalidMsg);
        return null;
      } else if (isInteger && valueNum % 1 != 0) // Integer required.
        AddBadField(fieldName, invalidMsg);
      else if (iMin != null && valueNum < iMin)
        AddBadField(fieldName, fieldTitle + " minimum is " + iMin);
      else if (iMax != null && valueNum > iMax)
        AddBadField(fieldName, fieldTitle + " maximum is " + iMax);
      return valueNum;
    }
    public decimal? CheckFieldDecimal(string fieldName) {
      return CheckFieldDecimal(fieldName, "", false, null, null, false, "");
    }

    public decimal? CheckFieldPercent(string fieldName, string fieldTitle, bool isInteger, bool isRequired, string invalidMsg, int iMin = 0, int iMax = 100) {
      // Note that while percentages are input from the UI as a range of 0-100, they are stored as a range 0-1.
      // That is, 50% is stored as 0.5.
      decimal? valueNum = CheckFieldDecimal(fieldName, fieldTitle, isInteger, iMin, iMax, isRequired, invalidMsg);
      return valueNum == null ? null : valueNum / 100;
    }

    public bool CheckFieldBool(string fieldName, string trueValue = "true") {
      return WebHelper.GetFormValue(fieldName, "") == trueValue;
    }

    public TEnum CheckFieldEnum<TEnum>(string fieldName, string fieldTitle) where TEnum : struct {
      // Note enums aren't nullable so this is assumed to be a required field.
      TEnum result = default;
      if (!Enum.TryParse(WebHelper.GetFormValue(fieldName, ""), out result)) {
        AddBadField(fieldName, fieldTitle + " is required.");
      }
      return result; // Note result will be the default enum value if TryParse fails.
    }

    public void ClearBadFields() {
      dicBadFields.Clear();
    }
    public void AddBadField(string fieldName, string messageHtml) {
      if (fieldName == null || messageHtml == null) return;
      if (!dicBadFields.ContainsKey(fieldName))
        dicBadFields.Add(fieldName, messageHtml);
      else dicBadFields[fieldName] = messageHtml;
      resultStatus = ResultStatusEnum.Error;
    }

    public bool DialogMessageExists() {
      return dicBadFields.ContainsKey(BadFields_Dialog_Key);
    }

    public void AddDialogMessage(string messageHtml, bool appendNewLine = false) {
      if (messageHtml.IsNullOrEmpty()) return;
      if (!dicBadFields.ContainsKey(BadFields_Dialog_Key)) {
        dicBadFields.Add(BadFields_Dialog_Key, messageHtml);
      } else if (appendNewLine) {
        dicBadFields[BadFields_Dialog_Key] += "<br>" + messageHtml;
      } else {
        dicBadFields[BadFields_Dialog_Key] = messageHtml;
      }
      resultStatus = ResultStatusEnum.Error;
    }

    // Display exception under message (dev only)
    public void AddDialogMessage(string messageHtml, Exception ex, bool sendSupportEmail = false) {
      if (!ConfigHelper.IsDevServer || ex == null) {
        AddDialogMessage(messageHtml);
      } else {
        AddReturnValue(WebHelper.AjaxReturnValues.IsAppException, "true");
        AddDialogMessage(messageHtml
          + "<div class=\"bootstrap-dialog-exception\">"
          + "<div class=\"bootstrap-dialog-exception-title\">Latest SQL:</div>"
          + "<div class=\"bootstrap-dialog-exception-sql\"><pre>" + LogHelper.GetLatestSqlQueryText() + "</pre></div>"
          + "<div class=\"bootstrap-dialog-exception-title\">Dev Stack Trace:</div>"
          + "<div class=\"bootstrap-dialog-exception-stacktrace\">" + LogHelper.GetStackTraceFormattedHtml(ex, true, ".bootstrap-dialog-exception ") + "</div>"
          + "</div>");
      }
      if (!ConfigHelper.IsDevServer && ex != null && sendSupportEmail) {
        EmailHelper.SendInternalSupportEmail(ex, "AjaxSubmitHelper Error");
      }
    }

    public void ClearDialogMessage() {
      if (dicBadFields.ContainsKey(BadFields_Dialog_Key)) dicBadFields.Remove(BadFields_Dialog_Key);
      resultStatus = ResultStatusEnum.Unset;
    }

    public string GetDialogMessage() {
      if (!dicBadFields.ContainsKey(BadFields_Dialog_Key)) return null;
      return dicBadFields[BadFields_Dialog_Key];
    }

    public void AddDialogConfirm(string message, string fieldName) {
      if (message.IsNullOrEmpty() || fieldName.IsNullOrEmpty()) return;
      dialogConfirmMessage = message;
      dialogConfirmFieldName = fieldName;
    }

    public void AddSuccessDialog(string message, bool appendNewLine = false) {
      if (message.IsNullOrEmpty()) return;
      if (!appendNewLine) {
        successDialogMessage = message;
      } else {
        if (!successDialogMessage.IsNullOrEmpty()) successDialogMessage += "<br>";
        successDialogMessage += message;
      }
      resultStatus = ResultStatusEnum.Success;
    }

    public bool HasErrors => BadFieldCount > 0 || dicBadFields.ContainsKey(BadFields_Dialog_Key) || resultStatus == ResultStatusEnum.Error;

    public bool MessagesExist() {
      return dicBadFields.ContainsKey(BadFields_Dialog_Key)
        || !successDialogMessage.IsNullOrEmpty()
        || !dialogConfirmMessage.IsNullOrEmpty()
        || !buttonMessage.IsNullOrEmpty()
        || !toastList.IsNullOrEmpty();
    }

    // Adds some more text to whatever the current message is (success or otherwise).
    public void AppendToCurrentMessage(string appendMessageHtml) {
      if (dicBadFields.ContainsKey(BadFields_Dialog_Key)) dicBadFields[BadFields_Dialog_Key] += appendMessageHtml;
      else if (!successDialogMessage.IsNullOrEmpty()) successDialogMessage += appendMessageHtml;
      else if (!dialogConfirmMessage.IsNullOrEmpty()) dialogConfirmMessage += appendMessageHtml;
      else if (!buttonMessage.IsNullOrEmpty()) buttonMessage += appendMessageHtml;
      else if (!WebHelper.GetNextPageMessageText().IsNullOrEmpty()) WebHelper.AppendNextPageMessageText(appendMessageHtml);
    }

    public void AddSuccessStatus() {
      if (dicBadFields.ContainsKey(BadFields_Status_Key)) {
        dicBadFields[BadFields_Status_Key] = "success";
      } else {
        dicBadFields.Add(BadFields_Status_Key, "success");
      }
      resultStatus = ResultStatusEnum.Success;
    }

    public void AddReturnJson<T>(string sName, T serializableObject) {
      if (sName.IsNullOrEmpty()) return;
      if (dicReturnValues.ContainsKey(sName)) {
        dicReturnValues[sName] = JsonConvert.SerializeObject(serializableObject);
      } else {
        dicReturnValues.Add(sName, JsonConvert.SerializeObject(serializableObject));
      }
    }

    public void AddReturnValue(string sName, Enum value) {
      if (sName.IsNullOrEmpty()) return;
      if (dicReturnValues.ContainsKey(sName)) {
        dicReturnValues[sName] = value.ToString();
      } else {
        dicReturnValues.Add(sName, value.ToString());
      }
    }

    public void AddReturnValue(string sName, Guid? value) {
      if (sName.IsNullOrEmpty()) return;
      if (dicReturnValues.ContainsKey(sName)) {
        dicReturnValues[sName] = value.ToStringNoBracesOrNull();
      } else {
        dicReturnValues.Add(sName, value.ToStringNoBracesOrNull());
      }
    }

    public void AddReturnValue(string sName, object value) {
      if (sName.IsNullOrEmpty()) return;
      if (dicReturnValues.ContainsKey(sName)) {
        dicReturnValues[sName] = value;
      } else {
        dicReturnValues.Add(sName, value);
      }
    }

    public void RemoveReturnValue(string sName) {
      if (sName.IsNullOrEmpty()) return;
      if (dicReturnValues.ContainsKey(sName)) {
        dicReturnValues.Remove(sName);
      }
    }

    public static string GetCurrentUrlPathAndQuery() {
      // Return the actual requested path entered into the browser, eg. "/CoacheeSurveyStatus?id=28"
      // NOT the "real" script path eg. not "/pages_albert/CoacheeSurveyStatus.aspx?id=28"
      return SystemWeb.RequestRawUrl;
    }

    public bool HasReloadPage() {
      return !WebHelper.GetNextPageMessageText().IsNullOrEmpty();
    }

    public void SetReloadPage() {
      SetReloadPage("", PageMessageType.None);
    }

    public void SetReloadPage(string nextPageMessageText, PageMessageType nextPageMessageType = PageMessageType.InfoDialog, bool appendToExisting = false) {
      if (!nextPageMessageText.IsNullOrEmpty()) {
        WebHelper.SetNextPageMessageType(nextPageMessageType);
        if (appendToExisting) {
          WebHelper.AppendNextPageMessageText(GetDialogMessage().EmptyIfNull() + successDialogMessage.EmptyIfNull() + nextPageMessageText);
          ClearDialogMessage();
          successDialogMessage = "";
        } else {
          WebHelper.SetNextPageMessageText(nextPageMessageText);
        }
      }
      reloadPage = true;
    }

    public void SetRedirectUrl(string url, string nextPageInfoMessage, bool replace = false) {
      SetRedirectUrl(url, nextPageInfoMessage, PageMessageType.InfoDialog, replace);
    }

    public void SetRedirectUrl(string url, string nextPageInfoMessage, PageMessageType nextPageMessageType, bool replace = false) {
      if (url.IsNullOrEmpty()) return;
      if (!nextPageInfoMessage.IsNullOrEmpty()) WebHelper.SetNextPageMessageText(nextPageInfoMessage);
      WebHelper.SetNextPageMessageType(nextPageMessageType);
      SetRedirectUrl(url, replace);
    }

    public void SetRedirectUrl(string url, bool replace = false) {
      if (url.IsNullOrEmpty()) return;
      redirectUrl = url;
      redirectReplace = replace;
    }

    public void AddInfoToast(string message) {
      if (message.IsNullOrEmpty()) return;
      toastList.Add(new ToastInfo(ToastType.Info, message));
    }

    public void AddSuccessToast(string message) {
      if (message.IsNullOrEmpty()) return;
      toastList.Add(new ToastInfo(ToastType.Success, message));
      resultStatus = ResultStatusEnum.Success;
    }

    public void AddErrorToast(string message) {
      if (message.IsNullOrEmpty()) return;
      toastList.Add(new ToastInfo(ToastType.Error, message));
      resultStatus = ResultStatusEnum.Error;
    }

    public void AppendToLastToast(string appendMessageHtml) {
      if (!toastList.IsNullOrEmpty()) toastList[toastList.Count - 1].message += appendMessageHtml;
    }

    public string toJson() {

      var sOut = new StringBuilder();
      int fieldCount = 0;

      sOut.Append("{ ");

      sOut.Append("\"badfields\": [");
      foreach (var oField in dicBadFields) {
        fieldCount++;
        if (fieldCount > 1) sOut.Append(",");
        if (oField.Key == BadFields_Dialog_Key || oField.Key == BadFields_Status_Key)
          sOut.Append("{" + JsonConvert.ToString(oField.Key) + ":" + JsonConvert.ToString(oField.Value) + "}");
        else
          sOut.Append("{\"name\":" + JsonConvert.ToString(oField.Key) + ",\"msg\":" + JsonConvert.ToString(oField.Value) + "}");
      }
      sOut.Append("]");

      // Additional name:value pairs.
      sOut.Append($", \"data\": {(dicReturnValues == null ? "{}" : JsonConvert.SerializeObject(dicReturnValues))}");

      sOut.Append(", \"toasts\": ");
      if (toastList.IsNullOrEmpty()) {
        sOut.Append("[]");
      } else {
        sOut.Append(JsonConvert.SerializeObject(toastList));
      }

      sOut.Append(", \"successDialog\": ");
      if (successDialogMessage.IsNullOrEmpty())
        sOut.Append("null");
      else {
        sOut.Append("{ ");
        sOut.Append("\"title\":" + JsonConvert.ToString(successDialogTitle));
        sOut.Append(", \"message\":" + JsonConvert.ToString(successDialogMessage));
        sOut.Append(" }");
      }

      sOut.Append(", \"buttonMessage\": ");
      if (buttonMessage == null)
        sOut.Append("null");
      else {
        sOut.Append("{ ");
        sOut.Append("\"message\":" + JsonConvert.ToString(buttonMessage));
        sOut.Append(", \"status\": \"" + resultStatus.ToString() + "\"");
        sOut.Append(" }");
      }

      sOut.Append(", \"dialogConfirm\": ");
      if (dialogConfirmMessage.IsNullOrEmpty())
        sOut.Append("null");
      else {
        sOut.Append("{ ");
        sOut.Append("\"title\":" + JsonConvert.ToString(dialogConfirmTitle));
        sOut.Append(", \"message\":" + JsonConvert.ToString(dialogConfirmMessage));
        sOut.Append(", \"fieldname\":" + JsonConvert.ToString(dialogConfirmFieldName));
        sOut.Append(" }");
      }

      // Reload page.
      sOut.Append(", \"reloadPage\":" + (reloadPage ? "true" : "false"));

      // Page message type.
      sOut.Append(", \"pageMessageType\":" + (int)pageMessageType);

      // Redirect to page { url: string, replace: boolean }
      // If replace = true, redirect is done via "location.replace()" otherwise simply "location.href = url".
      sOut.Append(", \"redirect\": ");
      if (redirectUrl.IsNullOrEmptyOrWhitespace())
        sOut.Append("null");
      else {
        sOut.Append("{ ");
        sOut.Append("\"url\":" + JsonConvert.ToString(redirectUrl));
        sOut.Append(", \"replace\":" + (redirectReplace ? "true" : "false"));
        sOut.Append(" }");
      }

      sOut.AppendLine($", \"{JsonKeys.ResponseLog}\": " + LogHelper.GetResponseLogJson());

      LogHelper.ClearResponseLog();

      sOut.AppendLine(" }");

      return sOut.ToString();
    }

    public void RespondSessionExpired() {
      AddDialogMessage("Your login session has expired. Please log in again to continue.");
      AddReturnValue(WebHelper.AjaxReturnValues.SessionExpired, "true");
      httpStatus = WebHelper.HttpStatusEnum.Unauthorized;
    }

    public void RespondApplicationError(string extraMessage = "") {
      resultStatus = ResultStatusEnum.Error;
      string message = "Unfortunately a problem has occurred.<br/>Please try again in a short while.";
      if (!extraMessage.IsNullOrEmpty()) message += " " + extraMessage;
      AddDialogMessage(message);
    }

    public static void RespondNoAccessToFunction(string message = null) {
      Process(ajax => {
        ajax.AddDialogMessage(message.ValueIfNullOrEmpty("Action was not allowed."));
      });
    }

    public void RespondNoAccessToFunction() {
      AddDialogMessage("Action was not allowed.");
    }

  }
}
