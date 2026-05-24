var common_BlockMsgObject;
var common_PleaseWaitObject;

// function alert(arg1, arg2, arg3, arg4) { // doesn't work well, doesn't block.
//   common_InfoDialog(arg1, arg2, arg3, arg4);
// }

function common_SuccessDialog(content, onClose) {
  common_InfoDialog(content, { glyphIcon: "ok-sign", glyphColor: "#13C17C", addClass: "dialogSuccess", title: "" }, onClose);
}

function common_InfoDialog() {

  var strTitle = "", strBody = "", strSelector = "", options = {}, strBodyOrSelector = "";

  // Options can be in any order, or any omitted except strBody.
  for (var argnum in arguments) {
    var arg = arguments[argnum];
    if (arg != null) {
      if (typeof (arg) == "object") options = arg;
      else if (typeof (arg) == "function") options.close = arg;
      else if (typeof (arg) == "string") {
        if (strBody == "") strBody = arg; // first assign body
        else { // further strings imply first string was actually the title.
          if (strTitle == "") strTitle = strBody; // assign once only.
          strBody = arg; // always assume last string is body.
        }
      }
    }
  }

  if (options.title) strTitle = options.title;
  if (options.content) strBody = options.content;

  if (isString(strBody) && strBody.match(/^([#.][a-z][a-z0-9_-]+)+$/i)) { strSelector = strBody; strBody = ""; }
  else if (isString(strTitle) && strTitle.match(/^([#.][a-z][a-z0-9_-]+)+$/i)) { strSelector = strTitle; strTitle = ""; }

  if (strTitle == "") {
    if (strBody != "" && strSelector != "") { strTitle = strBody; strBody = ""; } // if selector exist, body must actually be the title.
    //else strTitle = "Message"; // default title if only body or selector was given.
  }

  strBodyOrSelector = "";
  if (strSelector != "" && $(strSelector).length > 0) strBodyOrSelector = strSelector;
  else if (strBody != "") strBodyOrSelector = "<div>" + strBody + "</div>";
  if (strBodyOrSelector == "") return;

  // Remove <style> tags from content and limit width of <pre> (helps when showing stack trace).
  strBodyOrSelector = strBodyOrSelector
    .replace(/<style[^>]*>[^<]*<\/style>/gi, "")
    .replace(/<pre>/gi, '<pre style="max-width: 550px; overflow-x: scroll">');

  // Show dialog with extra options if supplied.
  defaults = {
    name: null,
    defaultDialogClass: "stdpopup",
    title: strTitle,
    content: $(strBodyOrSelector), //.prop("outerHTML"),
    glyphIcon: "",
    glyphColor: "",
    imageIcon: "", // specifies an image instead of a glyphIcon
    addClass: "",
    resizable: false,
    largeSize: false,
    smallSize: false,
    width: 600,
    modal: true,
    verticalCenter: true,
    focus: ".modal-footer .btn-default",
    buttons: [{ text: "Ok" }],
    shown: null,
    hide: null, // before close
    close: null,
    focusElementOnClose: document.activeElement ? $(document.activeElement) : null
  }
  options = $.extend(true, {}, defaults, options);
  if (options.addClass != options.defaultDialogClass) options.addClass = options.addClass + " " + options.defaultDialogClass;

  if (BSDLG) {
    options.closeButton = true;
    var modal = BSDLG.Modal(options);
    common_UpdateUI(modal.$modal.find(".modal-body"));
    return modal;
  } else {
    return $(strBodyOrSelector).dialog(options);
  }
}

function common_BlockMsg(strTextOrSelector) {
  common_BlockMsgObject = $(strTextOrSelector);
  common_BlockMsgObject
  .dialog({
    dialogClass: "stdpopup ui-blockmsg",
    closeOnEscape: false,
    resizable: false,
    minWidth: 10,
    minHeight: 10,
    modal: true
  });
}
function common_BlockMsgOff() {
  common_BlockMsgObject.dialog("close");
}

function common_PleaseWait(strTextOrSelector, fnPleaseWaitClosed) {
  strTextOrSelector = regexTrim("" + strTextOrSelector)
  if (strTextOrSelector == "") strTextOrSelector = "Please Wait...";
  if(!/^(?:<[a-z]+|(?:[a-z]+)?[#.][a-z0-9]+)/.test(strTextOrSelector)) strTextOrSelector = "<p>" + strTextOrSelector + "</p>"; // not tag or selector.
  common_PleaseWaitObject = $(strTextOrSelector);

  if (BSDLG) {
    BSDLG.WaitModal(common_PleaseWaitObject.html(), { smallSize: true, close: fnPleaseWaitClosed });
  } else {
    common_PleaseWaitObject.dialog({
      dialogClass: "stdpopup ui-dlg-plswait",
      closeOnEscape: false,
      resizable: false,
      minWidth: 350,
      minHeight: 50,
      modal: true,
      close: function(event, ui) { if (typeof (fnPleaseWaitClosed) == "function") fnPleaseWaitClosed(); }
    });
  }
}
function common_PleaseWaitOff() {
  if (BSDLG)
    BSDLG.HideWaitModal();
  else
    common_PleaseWaitObject.dialog("close");
}

function common_ConfirmDialog() {

  // default options specific to confirm dialog
  var options = {
    addClass: "common-popupConfirm",
    autoResize: true,
    closeOnConfirm: true,
    spinnerOnConfirm: false, // if true, dialog won't close - shows spinner, calls callback.
    callback: null,
    title: null,
    content: null,
    buttons: [
      {
        text: "Cancel",
        class: "floatleft btnCancel",
        keyup: function (ev) { if (ev.keyCode == 39) $(this).prev().focus(); },
        click: function (ev) {
          var $btn = $(ev.target);
          var $dlg = $btn.closest(".modal-options");
          var options = $dlg.data("options");
          if (typeof options.callback === "function") {
            setTimeout(function () { options.callback(false, $btn, $dlg, options); }, 200);
          }
        }
      },
      {
        text: "Confirm",
        class: "btnConfirm",
        keyup: function (ev) { if (ev.keyCode == 39) $(this).prev().focus(); },
        click: function (ev) {
          var $btn = $(ev.target);
          var $dlg = $btn.closest(".modal-options");
          var options = $dlg.data("options");
          $dlg.find(".ui-button").attr("disabled", true);
          if (typeof options.callback === "function") {
            setTimeout(function () { options.callback(true, $btn, $dlg, options); }, 200);
          }
        }
      }
    ]
  };

  // Arguments can consist of:
  // 1 or 2 strings - if 1 string, it's the content; if 2 strings the first is the title, second is the content.
  // an object - options
  // a function - the callback function(bool confirmed)
  for (var argnum in arguments) {
    var arg = arguments[argnum];
    if (arg != null) {
      if (typeof (arg) == "object") $.extend(options, arg);
      else if (typeof (arg) == "function") options.callback = arg;
      else if (typeof (arg) == "string") {
        if (!options.content) {
          options.content = arg;
        } else {
          if (!options.title) options.title = options.content;
          options.content = arg;
        }
      }
    }
  }

  common_InfoDialog(options);
}

function common_ShowPartialModal(options) {

  if (typeof options != "object") return;

  // https://nakupanda.github.io/bootstrap3-dialog/#available-options
  defaultOptions = {
    modalTitle: null,
    partialUrl: null,
    title: isStringNullOrEmpty(options.modalTitle) ? "Details" : options.modalTitle,
    widthFitContent: false,
    customClass: null,
    customCSS: null,
    cssClass: null,
    message: null,
    buttons: [{
      icon: null,
      label: 'Close',
      cssClass: 'btn-secondary',
      autospin: false,
      action: function (dialogRef) {
        dialogRef.close();
      }
    }],
    closable: true,
    spinicon: 'glyphicon glyphicon-asterisk',
    data: null,
    onshow: function (dialogRef) {
      var modalDialog = dialogRef.getModalDialog();
      modalDialog.data(JS_DataAttrName_DialogRef, dialogRef);
      var modalHeader = dialogRef.getModalHeader();
      var modalBody = dialogRef.getModalBody();
      var modalFooter = dialogRef.getModalFooter();
      if (modalFooter.text() == "") modalFooter.addClass("hidden"); // Nothing in footer so hide it.
      modalBody.busyLoad("show");
      // Delay to avoid bootstrap backdrop stealing focus after content shown.
      setTimeout(function () {
        modalBody.load(options.partialUrl,
          function (data) {
            common_UpdateUI(modalBody);
            modalBody.busyLoad("hide");
          }
        );
      }, 300);
    },
    onshown: null,
    onhide: function (dialogRef) {
      // Properly dispose of any tinymce instance in the modal.
      var modalDialog = dialogRef.getModalDialog();
      modalDialog.find("textarea.tinymce").each(function (i, e) {
        var mce = $(e).data("editor");
        if (mce != null) mce.remove();
      });
    },
    onClosed: null, // Either onClosed or onHidden can be used as a callback.
    onhidden: function (dialogRef) {
      if (typeof this.onClosed === 'function') this.onClosed(dialogRef);
    },
    autodestroy: true,
    nl2br: false
  };

  options = $.extend(true, {}, defaultOptions, options);

  if (isStringNullOrEmpty(options.partialUrl)) return;

  var $modalDialog = BootstrapDialog.show(options).getModalDialog();

  if (isString(options.widthFitContent === true)) $modalDialog.addClass("width-fit-content");
  if (isString(options.customClass)) $modalDialog.addClass(options.customClass);
  if (isString(options.customCSS)) $modalDialog.attr("style", $modalDialog.attr("style") + "; " + options.customCSS);

}

function common_CloseDialog($elementInDialog) {
  const bsModal = common_GetBsDialog($elementInDialog);
  if (bsModal != null && typeof bsModal.hide === "function") bsModal.hide();
}

function common_GetDialogBody($elementInDialog) {
  const $bsDialog = common_GetDialog($elementInDialog)
  if ($bsDialog == null) return null;
  return $bsDialog.find(".modal-body");
}

function common_GetDialog($elementInDialog) {

  if ($elementInDialog == null) return null;
  if ($elementInDialog instanceof HTMLElement) $elementInDialog = $($elementInDialog);
  if (!isJQuery($elementInDialog) || $elementInDialog.length != 1) return null;

  var $bsDialog = $elementInDialog.closest('.modal[role="dialog"]');
  if ($bsDialog.length !== 1) return null;

  return $bsDialog;
}

function common_GetBsDialog($elementInDialog) {

  const $bsDialog = common_GetDialog($elementInDialog)
  if ($bsDialog == null) return null;

  const bsModal = $bsDialog.data('bs.modal');
  if (bsModal == null || typeof bsModal.hide !== "function") return null;

  return bsModal;
}

function common_addWaitAnimToButton(btn) {
  if (btn != null && btn.addClass && btn.length == 1) {
    common_removeWaitAnimFromButton(btn);
    btn.css("position", "relative");
    btn.attr("disabled", true);
    spin = $('<span class="ajax-uloader-43" />');
    spin.css("left", (btn.outerWidth() + 10) + "px").insertAfter(btn);
    btn.data("common_btnspinner", spin);
  }
}
function common_removeWaitAnimFromButton(btn) {
  if (btn != null && btn.addClass && btn.length == 1) {
    var spin = btn.data("common_btnspinner");
    if (spin != null && spin.addClass && spin.length > 0) spin.remove();
    btn.data("common_btnspinner", null);
    btn.removeAttr("disabled");
  }
}

function common_SuccessToast(content) {
  common_Toast(content, { addClass: "toast-type-success" });
}

function common_InfoToast(content) {
  common_Toast(content, { addClass: "toast-type-info" });
}

function common_ErrorToast(content) {
  common_Toast(content, { addClass: "toast-type-error" });
}

function common_Toast(content, options) {
  // https://stephanwagner.me/jBox/options
  new jBox('Notice', {
    position: { x: 'center', y: 'top' },
    offset: { y: 5 },
    autoClose: 4000,
    delayOnHover: true,
    showCountdown: false,
    closeOnClick: false,
    addClass: options && options.addClass ? options.addClass : null,
    animation: { open: 'slide:top', close: 'slide:top' },
    content: content
  });
}
