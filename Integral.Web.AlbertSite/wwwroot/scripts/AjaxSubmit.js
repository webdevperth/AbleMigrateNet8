AjaxSubmit_Consts = {
  fieldRowClass: "ajaxSubmit-field",
  fieldErrorMsgClass: JS_AjaxFieldErrorMsg_Class,
  ajaxFieldNamePrefixClass: JS_AjaxFieldNamePrefix_Class,
  fieldErrorRowClass: "ajaxSubmit-errorRow",
  fieldErrorFieldClass: "ajaxSubmit-errorField"
};

function AjaxSubmit() { // waitMsg, jqForm, options { url, doScroll }, fnCallbackDone, fnCallbackFail, fnCallbackAlways

  var debugLog = false;

  // Assumptions:
  // 1. Form action is the correct post url, if not provided in options.
  // 2. Field "row" node is identified by class "fieldrow fieldName_<fieldname>"
  // 3. If validation message node exists, it has class "fieldmsg" within the row node.

  var showWaitMsg = true;
  var rtJson = {};

  var waitMsg = "",
    jqForm = null,
    options = null,
    functionCount = 0,
    fnCallbackDone = null,
    fnCallbackFail = null,
    fnCallbackAlways = null;

  // Options can be in any order, or any omitted except strBody
  // as long as one is a string, one is a jquery object and one is a set of options.
  // Options and callback function(s) are optional.
  // Callback functions must be in the order: fnCallbackDone, fnCallbackFail, fnCallbackAlways
  for (var argnum in arguments) {
    var arg = arguments[argnum];
    if (arg != null) {
      if (isString(arg)) waitMsg = arg;
      else if (isFunction(arg)) {
        functionCount++;
        if (functionCount == 1) fnCallbackDone = arg;
        else if (functionCount == 2) fnCallbackFail = arg;
        else if (functionCount == 3) fnCallbackAlways = arg;
      }
      else if (isObject(arg)) {
        if (isJQuery(arg)) jqForm = arg;
        else if (isHTMLElement(arg, "form")) jqForm = $(arg);
        else options = arg;
      }
    }
  }

  var defaultOptions = {
    form: jqForm,
    url: null,
    urlParams: {},
    action: null,
    data: null, // If jqForm is provided, this adds to (and overwrites) the form values.
    doScroll: false,
    waitMsg: waitMsg,
    disableElement: null,
    enableElementAlways: true,
    enableElementOnFail: true,
    enableElementOnBadFields: true,
    enableElementOnDialog: true,
    enableElementOnSuccess: true,
    enableElementOnRedirect: false,
    autoHighlightField: true,
    onHighlightField: null,
    statusElement: null,
    statusCloser: false,
    retainStatusWait: false,
    showStatusSuccess: true,
    showStatusFail: true,
    onSuccess: null,
    onDone: fnCallbackDone,
    onFail: fnCallbackFail,
    onError: null,
    onAlways: fnCallbackAlways,
    dataIsUrlEncoded: false, // by default, urlencode values in options.data
    failDialogTitle: "Oops!",
    failDialogBody: "Unfortunately there was a problem.<br/>Please try again in a short while.",
    showFailDialog: true,
    keepModal: false, // when true, executes callbacks without closing the "Please Wait" modal (for redirecting etc).
    contentType: "application/x-www-form-urlencoded; charset=UTF-8",
    busyLoadElement: null,
    headers: {}
  };

  if (options && isString(options.action)) defaultOptions.headers[HttpHeader_AjaxAction] = options.action;

  options = $.extend({}, defaultOptions, options);

  // Attach antiforgery token (emitted by the layout) so Razor Pages POSTs pass AutoValidateAntiforgeryToken.
  if (isString(app_AntiforgeryToken) && isString(HttpHeader_AntiforgeryToken)) {
    options.headers[HttpHeader_AntiforgeryToken] = app_AntiforgeryToken;
  }

  if (options.retainStatusWait === true) {
    options.showStatusSuccess = false;
    options.showStatusFail = false;
  }

  if (options.debugLog) debugLog = (options.debugLog === true);

  // If .data is mistakenly the form , move it to .form.
  if (isHTMLElement(options.data, "form") || isJQuery(options.data, "form")) {
    options.form = options.data;
    options.data = null;
  }
  // Ensure form is jquery object.
  if (isHTMLElement(options.form, "form")) options.form = $(options.form);

  var formPresent =
    (isJQuery(options.form)
      && options.form.length > 0
      && options.form.eq(0).prop("tagName").toLowerCase() == "form");

  // url defaults to the current url (i.e. "postback") if no other url given.
  if (isStringNullOrEmpty(options.url)) {
    if (options.form) {
      if (!isJQuery(options.form)) {
        options.form = $(options.form);
      }
      if (isJQuery(options.form) && !isStringNullOrEmpty(options.form.prop("action"))) {
        options.url = options.form.prop("action");
      } else if (!isStringNullOrEmpty(options.form.action)) {
        options.url = options.form.action;
      }
    }
    if (isStringNullOrEmpty(options.url) || options.url.substring(0, 1) == "#") {
      options.url = document.location.href;
    }
  }

  options.url = AbleJS.Util.PatchQuery({
    url: options.url,
    params: options.urlParams
  })

  // Data comes from form (if given), combined with options.data if given.
  // Convert all data to objects then merge (options.data overrides form data).

  var postDataObj = {};
  // Add form data if any.
  if (formPresent) $.extend(postDataObj, AbleJS.Form.ToObject(options.form, true)); // form to object, urlencode all values
  // Add extra data if any.
  if (isObject(options.data)) {
    $.extend(postDataObj, options.dataIsUrlEncoded ? options.data : getEncodedObject(options.data));
  } else if (isString(options.data)) {
    // .data can also be in the form "field1=value1;field2=value2;..."
    $.extend(postDataObj, AbleJS.Util.UnEncodedParamStringToObject(options.data, !options.dataIsUrlEncoded));
  }

  // The "AjaxAction" can be provided either in a submitted form
  //   e.g. <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="SomeAction">
  // or in the data object
  //   e.g. data: { "<%= PathHelper.FormKeys.AjaxAction %>": "SomeAction", ... }
  // or by simply setting the action value
  //   e.g. action: "SomeAction"
  // Note that data overrides form, and setting action overrides both.
  if (!isStringNullOrEmpty(options.action)) {
    postDataObj[app_formKey_AjaxAction] = options.action;
  }

  if (!isString(options.waitMsg)) {
    if (debugLog) consoleLog("AjaxSubmit() waitMsg is not a string.");
    return false;
  };

  if (formPresent) {
    options.form.find("." + AjaxSubmit_Consts.fieldErrorMsgClass).empty().hide();
    options.form.find("." + AjaxSubmit_Consts.fieldErrorFieldClass).removeClass(AjaxSubmit_Consts.fieldErrorFieldClass);
  }

  if (options.waitMsg == "") showWaitMsg = false;
  if (showWaitMsg) common_PleaseWait(options.waitMsg, fnPleaseWaitClosed);

  var fnWhenPleaseWaitClosed;  // function to call when please wait dialog has closed.
  var fnWhenPleaseWaitClosedAlways;  // function to call when please wait dialog has closed.

  // If "enctype" specified in form, and not specifically set in options, use the form value.
  if (formPresent && options.form[0].enctype && options.contentType == defaultOptions.contentType) options.contentType = options.form[0].enctype;
  if (options.contentType.toLowerCase().indexOf("application/x-www-form-urlencoded") == -1 && options.contentType.toLowerCase().indexOf("multipart/form-data") == -1) {
    alert('Content Type is "' + options.contentType + '" but can only be "application/x-www-form-urlencoded" or "multipart/form-data".');
    return;
  }

  // This is needed for multipart/form-data.
  if (options.formPresent && contentType == "multipart/form-data") postDataObj = new FormData(options.form[0]);

  $("input.inp-currency,input.inp-percent").each(function (i, e) { // Clean up currency and percent input - remove $, % and , (comma).
    if (postDataObj[e.name]) postDataObj[e.name] = encodeURIComponent(decodeURIComponent("" + postDataObj[e.name]).replace(/[$,%]/g, ""));
  });

  // Disable element (usually button).
  if (isJQuery(options.disableElement)) {
    options.disableElement.prop("disabled", true);
  }

  // Apply a small delay (100ms) to give a modal dialog a chance to remove
  // the "in" class, which is the first sign that it is going to close.
  setTimeout(function () {

    if (isJQuery(options.busyLoadElement)) {
      if (options.busyLoadElement.hasClass("select2-hidden-accessible")) {
        $(".select2-results__option.loading-results").busyLoad("show");
      } else {
        options.busyLoadElement.busyLoad("show");
      }
    } else {
      if ($("body > .bootstrap-dialog").hasClass("in")) {
        options.busyLoadElement = $("body > .bootstrap-dialog > .modal-dialog");
        options.busyLoadElement.busyLoad("show");
      } else {
        $.busyLoadFull("show");
      }
    }

    $.ajax(options.url, {
      type: "POST",
      async: true,
      cache: false,
      headers: options.headers,
      contentType: options.contentType,
      data: postDataObj // postData
    })
      .done(ajaxDone)
      .fail(ajaxFail)
      .always(ajaxAlways);

  }, 100);

  return;

  // fn to encode all the values in the data.
  // Note: Was using entires & reduce (as below) but it isn't IE compat, hence now using separate function.
  // Object.entries(obj).reduce(function(acc, [key, val]) { acc[key] = encodeURIComponent(val); return acc; }, {});
  function getEncodedObject(obj) {
    var result = {};
    $.each(obj, function (key, val) {
      result[key] = encodeURIComponent(obj[key]);
    });
    return result;
  }

  function ajaxAlways(data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { // see http://api.jquery.com/jquery.ajax/

    rtJson = data_or_jqXHR;

    if (isString(rtJson)) {
      try {
        rtJson = $.parseJSON(rtJson);
      } catch (e) {
        rtJson = null;
      }
    }

    // Output any serverlog messages to console.
    if (isObject(rtJson)) AbleJS.Logging.ResponseLog(rtJson[AjaxResponseLogKey])

    // Hide busyLoad.
    if (isJQuery(options.busyLoadElement)) {
      options.busyLoadElement.busyLoad("hide");
    } else {
      $.busyLoadFull("hide");
    }

    if (isJQuery(options.disableElement)) {
      options.disableElement.prop("disabled", false);
    }

    // Call callback.
    if (options.onAlways) fnWhenPleaseWaitClosedAlways = function () { options.onAlways(data_or_jqXHR, textStatus, jqXHR_or_errorThrown) };

    // Close modal.
    if (options.keepModal || !showWaitMsg)
      fnPleaseWaitClosed();
    else
      common_PleaseWaitOff();
  }

  function ajaxFail(jqXHR, textStatus, errorThrown) {

    // Update button.
    if (isJQuery(options.disableElement) && options.enableElementOnFail === true) options.disableElement.prop("disabled", false);
    if (isJQuery(options.statusElement) && options.showStatusFail) options.statusElement.addClass("btnFailed");

    fnWhenPleaseWaitClosed = function () {

      // Check for 401 unauthorized (login expired)
      if (jqXHR.status == 401) {
        var expiredMessage;
        try {
          expiredMessage = $.parseJSON(jqXHR.responseText).badfields[0].dialog; // Message from server is usually here.
        } catch (e) {
          expiredMessage = "You must be logged in to do this."; // Otherwise default message.
        }
        common_InfoDialog(expiredMessage);
        return;
      }

      // If not 401, show failure message if set.
      if (options.showFailDialog) {
        common_InfoDialog(options.failDialogTitle, options.failDialogBody, function () {
          if (options.onError) options.onError(jqXHR, textStatus, errorThrown);
        });
      } else {
        if (jqxhr.status == 401) { // Unauthorized
          common_InfoDialog("Permission needed to access this resource.",
            function () { if (options.onError) options.onError(jqXHR, textStatus, errorThrown); }
          );
        } else if (jqxhr.status == 403) { // Forbidden
          common_InfoDialog("Must be logged in to access this resource.",
            "If you were logged in, your session may have expired due to inactivity.<br/>Try logging in again and come back.",
            function () { if (options.onError) options.onError(jqXHR, textStatus, errorThrown); }
          );
        } else {
          if (options.onError) options.onError(jqXHR, textStatus, errorThrown);
        }
      }
    };
  }

  function ajaxDone(data, textStatus, jqXHR) {

    var errFields = null,
        returnData = null,
        jq1stErrRow = null,
        highlightedFields = 0,
        dialogs = 0;

    // Response could be blank or just "ok", assume this is a success state with no special instructions.
    if (jqXHR.responseText == null || jqXHR.responseText == '' || (jqXHR.responseText + "  ").toLowerCase().substring(0, 2) == "ok") {

      rtJson = {};

    } else {

      try {
        rtJson = $.parseJSON(jqXHR.responseText);
        errFields = rtJson.badfields;
        returnData = rtJson.data;
      } catch (e) {
        if (isJQuery(options.statusElement) && options.showStatusFail) options.statusElement.addClass("btnFailed");
        common_InfoDialog("Oops!", "Unfortunately we hit a problem.<br/>Please try again in a short while.");
        return false;
      }
    }

    if (rtJson.dialogConfirm && isString(rtJson.dialogConfirm.message) && isString(rtJson.dialogConfirm.fieldname)) {

      // Show confirm dialog? This shows a yes/no dialog.
      // If user chooses no, nothing happens. If yes, then the form
      // is re-submitted with field added with a value of "true".
      if (confirm(rtJson.dialogConfirm.message)) {
        options.data[ajaxConfirm] = true;
        options.data[rtJson.dialogConfirm.fieldname] = true;
        setTimeout(function () { AjaxSubmit(options); }, 300);
      }
      return;
    }

    // If any image uploads pending, do them now.
    if (formPresent) {
      AbleJS.Form.PostImages(options.form);
    }

    if (rtJson.successDialog && rtJson.successDialog.message) {

      ShowDialogSuccess("Success", rtJson.successDialog.message, function () {
        if (IsReloadPage()) {
          DoReloadPage(); // Reload takes precedence over redirect.
          return;
        } else if (IsRedirect()) {
          DoRedirect();
          return;
        }
      });

    } else if (isArray(rtJson.toasts) && rtJson.toasts.length > 0) {

      for (var i = 0; i < rtJson.toasts.length; i++) {
        var toast = rtJson.toasts[i];
        var toastType = toast.type;
        var toastMessage = toast.message;
        if (toastType == JS_ToastType_Success) {
          common_SuccessToast(toastMessage);
        } else if (toastType == JS_ToastType_Error) {
          common_ErrorToast(toastMessage);
        } else {
          common_InfoToast(toastMessage);
        }
      }

    } else {

      // No dialog, so do any redirect/reload now.
      if (IsReloadPage()) {
        DoReloadPage(); // Reload takes precedence over redirect.
        return;
      } else if (IsRedirect()) {
        DoRedirect();
        return;
      }
    }

    if (errFields == null) errFields = []; // precaution.

    // Highlight fields.

    this.extraMessages = ""; // messages not attached to visible fields.

    for (var i = 0; i < errFields.length; i++) {

      var fieldName = errFields[i].name;
      var fieldMessage = errFields[i].msg;
      if (!isString(fieldName) || isStringNullOrEmpty(fieldName)) continue;
      if (!isString(fieldMessage) || isStringNullOrEmpty(fieldMessage)) continue;

      highlightedFields++;
      var highlightedRow = null;
      if (options.autoHighlightField === true) {
        highlightedRow = HighlightField(fieldName, fieldMessage, this);
      }
      if (typeof options.onHighlightField == "function") {
        highlightedRow = options.onHighlightField(fieldName, fieldMessage, highlightedRow, this);
      }
      if (jq1stErrRow == null && isJQuery(highlightedRow)) jq1stErrRow = highlightedRow;
    }

    if (jq1stErrRow != null && !jq1stErrRow.is(":visible")) {
      // First error field isn't visible, maybe it's in a different tab.
      var pane = jq1stErrRow.closest('.tab-pane[id^="panel-"]');
      if (pane.length == 1) {
        var tabName = "" + pane.prop("id").replace("panel-", "");
        if (tabName.length > 0) $('#tab-' + tabName + '[role="tab"]').tab("show");
      }
    }

    if (this.extraMessages.length > 0) ShowDialogWarning("Corrections", this.extraMessages);

    fnWhenPleaseWaitClosed = function () { // Do this only after plswait dialog has closed.

      // Scroll first error field (if any) into view.
      if (debugLog) consoleLog("fnWhenPleaseWaitClosed()");
      if (jq1stErrRow) {
        if (options.doScroll) common_jqScrollToMiddle(jq1stErrRow); // Scroll first error field (if any) into view.
        if (debugLog) consoleLog('jq1stErrRow.find("input:text,select,textarea").eq(0).focus()');
        jq1stErrRow.find("input:text,select,textarea").eq(0).focus();
      }

      // Show dialog - first one only.
      for (var i = 0; i < errFields.length; i++) {
        if (isString(errFields[i].dialog)) {
          dialogs++;
          var dialogMsg = errFields[i].dialog;
          if (returnData && returnData[AjaxReturnValue_StackDump]) {
            dialogMsg += $('<pre class="debugStackDump" />').html(returnData[AjaxReturnValue_StackDump]).prop("outerHTML");
          }
          if (returnData && returnData[AjaxReturnValue_IsAppException]) {
            ShowDialogException("Exception!", dialogMsg);
          } else {
            ShowDialogWarning("Message", dialogMsg);
          }
          break;
        }
      }

      if (isJQuery(options.disableElement)) {
        if (highlightedFields + dialogs === 0) {
          if (options.enableElementOnSuccess === true) options.disableElement.prop("disabled", false);
        } else {
          if (highlightedFields > 0 && options.enableElementOnBadFields === true) options.disableElement.prop("disabled", false);
          if (dialogs > 0 && options.enableElementOnDialog === true) options.disableElement.prop("disabled", false);
        }
      }

      if (options.onDone) {
        options.onDone(jqXHR, highlightedFields, dialogs, jq1stErrRow, errFields, returnData);
      } else if (highlightedFields + dialogs == 0) {
        // Success.
        if (options.onSuccess) options.onSuccess(jqXHR, returnData);
      } else {
        // Fail
        if (options.onFail) options.onFail(jqXHR, returnData, highlightedFields, dialogs, jq1stErrRow, errFields);
      }
    };
  } // ajaxDone

  function IsReloadPage() {
    if (rtJson.reloadPage && rtJson.reloadPage === true) return true;
    return false;
  }

  function DoReloadPage() {
    if (IsReloadPage()) location.reload(true);
  }

  function IsRedirect() {
    if (rtJson.redirect && isString(rtJson.redirect.url) && rtJson.redirect.url.length > 0) return true;
    return false;
  };

  function DoRedirect() {
    if (IsRedirect()) {
      if (rtJson.replace && rtJson.replace === true) {
        location.replace(rtJson.redirect.url);
      } else {
        location.href = rtJson.redirect.url;
      }
    }
  }

  // See https://nakupanda.github.io/bootstrap3-dialog/ for message "type" constants (BootstrapDialog.TYPE_INFO, etc)
  function ShowDialogSuccess(dialogTitle, dialogMsg, fnCallback) {
    ShowDialog(BootstrapDialog.TYPE_SUCCESS, dialogTitle, dialogMsg, fnCallback);
  }
  function ShowDialogInfo(dialogTitle, dialogMsg, fnCallback) {
    ShowDialog(BootstrapDialog.TYPE_INFO, dialogTitle, dialogMsg, fnCallback);
  }
  function ShowDialogWarning(dialogTitle, dialogMsg, fnCallback) {
    ShowDialog(BootstrapDialog.TYPE_WARNING, dialogTitle, dialogMsg, fnCallback);
  }
  function ShowDialogException(dialogTitle, dialogMsg, fnCallback) {
    ShowDialog(BootstrapDialog.TYPE_WARNING, dialogTitle, dialogMsg, fnCallback, "dialog-exception");
  }

  function ShowDialog(BootstrapDialogType, dialogTitle, dialogMsg, fnCallback, cssClass) {

    if (!BootstrapDialogType) BootstrapDialogType = BootstrapDialog.TYPE_DEFAULT;

    // common_InfoDialog(dialogMsg);
    BootstrapDialog.alert({
      cssClass: isString(cssClass) ? cssClass : "",
      title: dialogTitle,
      type: BootstrapDialogType,
      message: dialogMsg,
      nl2br: false,
      animate: false,
      closable: true,
      closeByBackdrop: true,
      closeByKeyboard: true,
      onshown: function(dlg) {
        var mdl = dlg.getModalDialog();
        var top = ($(window).height() - mdl.height()) / 2 - 20;
        if (top > 0) mdl.css("margin-top", top + "px");
        setTimeout(function () { mdl.find(".btn-default").focus(); }, 200);
      },
      callback: function (result) {
        if (fnCallback && typeof fnCallback == "function") fnCallback(result);
      }
      //buttons: [{ label: 'OK', action: function(dialogRef) { dialogRef.close(); } }]
    });

  }

  function fnPleaseWaitClosed() {
    if (typeof fnWhenPleaseWaitClosed == "function") fnWhenPleaseWaitClosed();
    if (typeof fnWhenPleaseWaitClosedAlways == "function") fnWhenPleaseWaitClosedAlways();
  }

  function HighlightField(fieldName, fieldMessage, caller) {

    if (!isString(fieldName) || !isString(fieldMessage)) return null;

    var fieldRow = null; // return value
    var fieldInput;

    if (isJQuery(options.form)) {
      fieldInput = options.form.find('input[name="' + fieldName + '"],select[name="' + fieldName + '"],textarea[name="' + fieldName + '"]');
    } else {
      fieldInput = $('input[name="' + fieldName + '"],select[name="' + fieldName + '"],textarea[name="' + fieldName + '"]');
    }

    if (fieldInput.length == 0) { // Try finding field a different way.
      fieldInput = $("." + AjaxSubmit_Consts.fieldErrorMsgClass + "." + AjaxSubmit_Consts.ajaxFieldNamePrefixClass + fieldName);
    }

    if (fieldInput.length == 0) { // Field not found.

      if (caller && fieldMessage.length > 0) caller.extraMessages += fieldMessage + "<br>";

    } else if (fieldInput.length != 1) { // Multiple fields found.

      if (caller && fieldMessage.length > 0) caller.extraMessages += fieldMessage + "<br>";

    } else { // Single field, ok to continue.

      var fieldRow = fieldInput.closest("." + AjaxSubmit_Consts.fieldRowClass);

      if (fieldRow.length != 1) {
        // No fieldRowClass, try parent.
        fieldRow = fieldInput.parent();
      }

      if (fieldRow.length == 1) {

        fieldRow.addClass(AjaxSubmit_Consts.fieldErrorRowClass);

        // are there bootstrap columns in this row?
        if (fieldRow.children("div[class^=col-]").length > 0) {
          fieldRow = fieldInput.closest("div[class^=col-]");
        }

        var jMsgNode = fieldRow.find("." + AjaxSubmit_Consts.fieldErrorMsgClass).eq(0);
        var msgNodeFound = jMsgNode.length > 0;

        // Add error message node if it doesn't already exist.
        if (!msgNodeFound) {
          jMsgNode = $('<div />').addClass(AjaxSubmit_Consts.fieldErrorMsgClass).css("display", "none");
        }

        // Add error message content if it doesn't already exist.
        let msgExists = false;
        const fieldMessageText = $("<div>" + fieldMessage + "</div>").text();
        jMsgNode.find("p").each(function (i, e) {
          var $e = $(e);
          if ($e.text() === fieldMessageText) msgExists = true;
        });
        if (!msgExists) {
          jMsgNode.append("<p>" + fieldMessage + "</p>");
        }

        if (!msgNodeFound) {
          // Add error message node where appropriate.
          let fieldInputNextSib = fieldInput.next();
          let $formrowContent = fieldInput.closest(".formrow-col-content");
          if ($formrowContent.length === 1) {
            jMsgNode.appendTo($formrowContent);
          } else if (fieldInputNextSib.length == 1 && fieldInputNextSib.hasClass("select2-container") && fieldInput.prop("tagName").toLowerCase() == "select") {
            jMsgNode.insertAfter(fieldInputNextSib); // Add after select 2 element.
          } else if (fieldInput.parent().hasClass("input-text-dual") || fieldInput.parent().hasClass("input-group")) {
            jMsgNode.insertAfter(fieldInput.parent()); // Else add after input element.
          } else if (fieldInput.parent().hasClass("icheck-done")) {
            jMsgNode.insertAfter(fieldInput.closest(".checkbox-table")); // Else add after checkbox table.
          } else if (fieldInput.parent().css("display") == "flex") {
            jMsgNode.insertAfter(fieldInput.parent()); // Ensure it's outside any flexbox
          } else {
            jMsgNode.insertAfter(fieldInput); // Else add after input element.
          }
        }

        // Show it.
        jMsgNode.slideDown();

        // Add keyup handler to remove field message.
        fieldInput.data("jMsgNode", jMsgNode);
        if (fieldInput.data("ASFbound") !== true) {
          fieldInput.data("ASFbound", true);
          fieldInput.on("change", function () {
            $(this).data("jMsgNode").slideUp();
          });
        }
      }
    }

    return fieldRow;
  }

} // AjaxSubmit()
