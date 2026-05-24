$(document).ready(function () {

  $(".btn-group input:radio").focus(function () {
    var grp = $(this).closest(".btn-group");
    if (grp.length == 1) {
      grp.addClass("focus");
    }
  }).blur(function () {
    var grp = $(this).closest(".btn-group");
    if (grp.length == 1) {
      grp.removeClass("focus");
    }
  });

  $(".btn-group.btn-group-toggle.disabled .btn").click(function (ev) { ev.preventDefault(); ev.stopPropagation(); });

});

function bsModalObject(name, $modal, options) {
  this.uniqueId = BSDLG.GetUniqueId();
  this.name = name; // modals can be accessed by name or index
  this.$modal = $modal; // jquery modal object
  this.options = $.extend({}, options);
  this.hide = function() { $modal.modal("hide"); }
}

var BSDLG = new function() {

  var thisObj = this;

  thisObj.ModalObjects = new Array(); // array of bsModalObject, contains currently shown modal info.
  thisObj.plsWaitModalName = "plsWait";
  thisObj.plsWaitModalClass = "plsWait";

  thisObj.Modal = function() {

    var logIt = false, logItBtn = false;

    var title = "", content = "", options = {};
    for (var i = 0; i < arguments.length; i++) {
      var arg = arguments[i];
      if (isString(arg)) {
        if (content == "") content = arg;
        else { title = content; content = arg; }
      } else if (isObject(arg)) {
        options = arg;
      }
    }
    if (title + content == "") content = "Please wait...";
    options = $.extend({
      name: "", // modal name, optional, allowing it to be explicitly found.
      title: title,
      content: content,
      largeSize: false,
      smallSize: false,
      closeButton: false,
      showFooter: false,
      keyboard: true,
      allowClose: true,
      addClass: "",
      glyphIcon: "",
      glyphColor: "",
      imageIcon: "", // specifies an image instead of a glyphIcon
      verticalCenter: true,
      buttons: "Ok",
      hide: null,  // before hidden
      close: null, // after hidden
      shown: null,
      callback: null,
      width: 600,
      focus: ".modal-footer button:last-child",
      focusElementOnClose: null
    }, options);

    var modalCount = thisObj.ModalObjects.length;
    if (logIt) console.log("Modal: adding modal " + (modalCount + 1) + ", name = '" + options.name + "', content = " + options.content);

    var $modal = $("#siteModalTemplate").clone();
    $modal.addClass("modal-options");
    var dialog = $modal.find(".modal-dialog");
    var glyphCol = $modal.find(".modal-col-glyph");
    var modalHeader = $modal.find(".modal-header");
    var modalBody = $modal.find(".modal-body");
    var modalFooter = $modal.find(".modal-footer");
    var headerHasContent = false;
    var headerIsHidden = true;
    var bodyHasContent = false;
    var bodyIsHidden = true;
    var footerHasContent = false;
    if (logIt) console.log("Modal: vars done");

    var glyphSpan = glyphCol.find(".glyphicon");
    if (glyphSpan.length == 1) {
      glyphSpan.attr("class", "glyphicon").css("color", ""); // reset
      if (options.imageIcon != "") {
        glyphSpan.css("background-image", "url(" + options.imageIcon + ")");
        glyphCol.show();
      } else if (options.glyphIcon != "") {
        glyphSpan.addClass("glyphicon-" + options.glyphIcon);
        if (options.glyphColor != "") glyphSpan.css("color", options.glyphColor);
        glyphCol.show();
      } else {
        glyphCol.hide(); // hide glyph area of dialog
      }
    }

    if (!options.allowClose) { $modal.addClass("modal-noclose"); options.keyboard = false; }
    if (options.verticalCenter) $modal.addClass("modal-vertical-center");
    if (options.addClass.length > 0) $modal.addClass(options.addClass);

    if (options.smallSize) dialog.addClass("modal-sm");
    else if (options.largeSize) dialog.addClass("modal-lg");
    else if (options.width) dialog.css("width", options.width);

    if (options.title == "") {
      modalHeader.hide();
      dialog.addClass("no-header");
    } else {
      modalHeader.find(".modal-title").html(options.title);
      headerHasContent = true;
      headerIsHidden = false;
      if (!options.closeButton) modalHeader.find("button").hide();
    }
    if (options.content == "") {
      modalBody.hide();
    } else {
      if (isJQuery(options.content)) modalBody.html(options.content.removeClass("displaynone").show());
      else modalBody.html(options.content);
      bodyHasContent = true;
      bodyIsHidden = false;
    }
    if (!headerIsHidden && bodyIsHidden) modalHeader.addClass("bbnone");
    if (logIt) console.log("Modal: more vars done");

    if (isString(options.buttons)) {
      var btnTexts = options.buttons.split(",");
      options.buttons = [];
      for (var ib = 0; ib < btnTexts.length; ib++) {
        var btnData = {
          text: btnTexts[ib],
          close: true
        };
        options.buttons.push(btnData);
      }
    }

    if (logIt) console.log("Modal: name = '" + options.name + "'");
    if (isString(options.name) && options.name.length > 0) thisObj.HideModal(options.name); // will replace any modals with same name.

    // Footer content (buttons).
    modalFooter.empty();
    if (isArray(options.buttons)) {
      if (logItBtn) console.log("Modal: buttons: " + options.buttons.length);
      var hasDefaultButton = false;
      var hasPrimaryButton = false;
      modalFooter.empty();
      for (var ib = 0; ib < options.buttons.length; ib++) {
        var btnData = options.buttons[ib];
        if (btnData.text.toLowerCase() == "ok") btnData.text = "Ok";
        else if (btnData.text.toLowerCase() == "save") btnData.text = "Save Changes";
        else if (btnData.text.toLowerCase() == "cancel") btnData.text = "Cancel";
        // if (logItBtn) console.log("ib == options.buttons.length - 1 && !hasDefaultButton ? true : false = " + (ib == options.buttons.length - 1 && !hasDefaultButton ? true : false));
        btnData = $.extend({
          text: "Close",
          class: "",
          close: true,
          isDefault: ib == options.buttons.length - 1 && !hasDefaultButton ? true : false,
          isPrimary: ib == options.buttons.length - 1 && !hasPrimaryButton ? true : false
        }, btnData);
        if (btnData.isDefault) hasDefaultButton = true;
        if (btnData.isPrimary) hasPrimaryButton = true;
        var jBtn = $('<button type="button" class="btn" />').text(btnData.text || "Close");
        if (btnData.close) jBtn.attr("data-dismiss", "modal");
        if (btnData.isDefault) jBtn.addClass("btn-default");
        if (btnData.isPrimary) jBtn.addClass("btn-primary");
        if (isString(btnData.id)) jBtn.prop("id", btnData.id);
        if (isString(btnData.class) && btnData.class.length > 0) jBtn.addClass(btnData.class);
        if (isFunction(btnData.click)) jBtn.click(btnData.click);
        modalFooter.append(jBtn);
        footerHasContent = true;
      }
    }
    if (!footerHasContent) modalFooter.hide();
    if (logIt) console.log("Modal: footer content done");

    // Add to ModalObjects[]
    var modalObject = new bsModalObject(options.name, $modal, options);
    thisObj.ModalObjects.push(modalObject);

    //if (!options.allowClose) { $modal.on('hide.bs.modal', function(e) { return false; }); } // prevent close.
    $modal.on('hide.bs.modal', function (e) {
      thisObj.OnModalHide(modalObject);
      if (typeof (options.hide) == "function") options.hide($modal, e);
    });
    $modal.on('hidden.bs.modal', function (e) {
      if (options.focusElementOnClose && typeof options.focusElementOnClose.focus == "function") options.focusElementOnClose.focus();
      else thisObj.FocusTopModal();
      if (typeof (options.close) == "function") options.close($modal, e);
    });
    $modal.on('shown.bs.modal', function (e) {
      if (options.focus && options.focus != null) {
        if (isString(options.focus)) $modal.find(options.focus).focus();
        else if (isJQuery(options.focus)) options.focus.focus();
        else if (isFunction(options.focus)) options.focus();
      }
      if (typeof (options.shown) == "function") options.shown($modal, e);
    });

    if (logIt) console.log("Modal: added to array, uid = " + modalObject.uniqueId + ", modals = " + thisObj.ModalObjects.length);
    // Show. TODO: change to use BoostrapDialog (https://nakupanda.github.io/bootstrap3-dialog)
    $modal.data("options", options);
    $modal.modal({ keyboard: options.keyboard });
    if (logIt) console.log("Modal: shown");

    // Return reference.
    return modalObject;
  };

  thisObj.GetUniqueId = function() {
    var validChars = "abcdefghijklmnopqrstuvwxyz0123456789";
    var uid = "";
    var len = Math.floor(Math.random() * 6) + 4;
    do {
      for (var i = 0; i < len; i++) { // create uid.
        var pos = Math.floor(Math.random() * validChars.length);
        uid += validChars.substring(pos, pos + 1);
      }
    } while (thisObj.FindModalIndexByUniqueId(uid) >= 0); // until unique.
    return uid;
  };

  thisObj.FindModalIndexByUniqueId = function(uid) {
    var modalIndex = -1;
    if (isString(uid) && uid.length > 0 && thisObj.ModalObjects.length > 0) {
      for (var mi = 0; mi < thisObj.ModalObjects.length; mi++) {
        if (uid == thisObj.ModalObjects[mi].uniqueId) modalIndex = mi;
      }
    }
    return modalIndex;
  };

  thisObj.FindModalIndexByName = function(modalName) {
    var modalIndex = -1;
    if (isString(modalName) && modalName.length > 0 && thisObj.ModalObjects.length > 0) {
      for (var mi = 0; mi < thisObj.ModalObjects.length; mi++) {
        if (modalName == thisObj.ModalObjects[mi].name) modalIndex = mi;
      }
    }
    return modalIndex;
  };

  thisObj.WaitModal = function() {
    // show please wait modal.
    var message = "", moreText = "", modalOptions = {};
    for (var i = 0; i < arguments.length; i++) {
      var arg = arguments[i];
      if (isString(arg)) {
        if (moreText == "") moreText = arg;
        else { message = moreText; moreText = arg; }
      } else if (isObject(arg)) {
        modalOptions = arg;
      }
    }
    if (message + moreText == "") message = "Please wait...";
    var smallSize = moreText.length == 0 ? true : false;
    modalOptions = $.extend({
      name: thisObj.plsWaitModalName, addClass: thisObj.plsWaitModalClass, title: message, content: moreText, allowClose: false, smallSize: smallSize, buttons: []
    }, modalOptions);
    return thisObj.Modal(modalOptions);
  };

  thisObj.HideWaitModal = function() {
    thisObj.HideModal(thisObj.plsWaitModalName);
  };

  thisObj.HideModal = function(modalName) { // if modalName == null, hide topmost (latest) modal.
    var logIt = false;
    if (thisObj.ModalObjects.length == 0) return;
    var modalIndex = thisObj.ModalObjects.length - 1; // hide last by default.
    if (isString(modalName) && modalName.length > 0) {
      modalIndex = thisObj.FindModalIndexByName(modalName);
      if (modalIndex == -1) { if (logIt) console.log("HideModal: name '" + modalName + "' not found"); return; } // not found.
    }
    var modalObject = thisObj.ModalObjects[modalIndex];
    if (logIt) console.log("HideModal: name '" + modalName + "', index = " + modalIndex);
    modalObject.options.allowClose = true;
    modalObject.$modal.modal("hide");
    if (logIt) console.log("HideModal: remaining = " + thisObj.ModalObjects.length);
  };

  thisObj.OnModalHide = function(modalObject, force) {
    var logIt = false;
    force = force ? true : false;
    if (logIt) console.log("OnModalHide: uid = " + modalObject.uniqueId + ", name = '" + modalObject.name + "', force = " + force);
    if (!force && !modalObject.options.allowClose) return false; // cancel close.
    // remove from array.
    var modalIndex = thisObj.FindModalIndexByUniqueId(modalObject.uniqueId);
    if (logIt) console.log("OnModalHide: index = " + modalIndex);
    if (modalIndex == -1) return;
    thisObj.ModalObjects.splice(modalIndex, 1); // remove modal object from array.
    //thisObj.FocusTopModal();
  };

  thisObj.FocusTopModal = function() { // set focus on topmost modal (last in array).
    var logIt = false;
    var $modal = null;
    if (thisObj.ModalObjects.length > 0) {
      var lastIndex = thisObj.ModalObjects.length - 1;
      modalObject = thisObj.ModalObjects[lastIndex];
      if (logIt) console.log("FocusTopModal() index = " + lastIndex + ", modalObject.name = '" + modalObject.name + "'");
      $modal = modalObject.$modal;
    } else {
      $modal = $('.modal[role="dialog"][id!="siteModalTemplate"]:visible:last');
      if (logIt) console.log("FocusTopModal() $modal.length = " + $modal.length + "");
      if ($modal.length != 1) return;
    }
    if (logIt) console.log("FocusTopModal() id = " + $modal[0].id);
    $modal.modal("show");
    $inp = $modal.find("a,button,input,select").not(".close");
    if (logIt) console.log("FocusTopModal() $inp.length = " + $inp.length);
    $inp.eq(0).focus();
  };

  thisObj.Dialog = function(modalTitle, modalContent, options) {
    options = $.extend({
      title: modalTitle,
      content: modalContent,
      buttons: [
        { text: "Close", close: true }
      ]
    }, options);
    return thisObj.Modal(modalTitle, modalContent, options);
  }
};

