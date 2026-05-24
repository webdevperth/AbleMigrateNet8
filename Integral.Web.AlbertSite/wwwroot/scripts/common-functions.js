// Number rounding ref: https://stackoverflow.com/questions/11832914/how-to-round-to-at-most-2-decimal-places-if-necessary
// Number parsing ref: https://www.bennadel.com/blog/3803-i-prefer-the-unary-plus-operator-over-parseint-and-parsefloat-when-coercing-strings-to-numbers-in-javascript.htm

var CurrencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });

var app_CommonFunctions = {}; // populated with app level values and functions.

app_CommonFunctions.BootMultiSelect_DefaultOptions = {

  buttonContainer: '<div class="btn-group boot-multiselect-container" />',
  //buttonWidth: '350px',
  //enableClickableOptGroups: true,
  //enableCollapsibleOptGroups: true,
  //includeSelectAllOption: true,
  //dropUp: true,
  includeResetOption: false,
  enableFiltering: false,
  enableCaseInsensitiveFiltering: false,
  includeSelectAllOption: false,
  maxHeight: 350,
  dropRight: true,
  enableClickableOptGroups: true,
  onInitialized: function (qSelect, qContainer) {
    var dropMenu = qContainer.children("ul.dropdown-menu"); // visible dropdown
    if (dropMenu.length != 1) return;
    dropMenu.find("li.multiselect-group > a  > label").addClass("checkbox"); // fix
    if (this.options.optNoOptionTitles) dropMenu.children("li").children("a").children("label").removeAttr("title");
    this.updateOptGroupsOrig = this.updateOptGroups;     // augment the
    this.updateOptGroups = this.options.updateOptGroups; // updateOptGroups function
    qSelect.attr("tabindex", "-1");
  },
  updateOptGroups: function () {
    this.updateOptGroupsOrig();
    var $groups = $('li.multiselect-group', this.$ul);
    var selectedClass = this.options.selectedClass;
    $groups.each(function () {
      var groupItem = $(this);
      var activeItems = groupItem.nextUntil('li.multiselect-group').filter("." + selectedClass);
      if (activeItems.length > 0) groupItem.addClass("active-group");
      else groupItem.removeClass("active-group");
    });
  },
  onSelectAll: function () {
    if (this.options.selectClear) { // Turns the "select all" item into "clear all".
      // this.deselectAll(false, false);
      this.clearSelection();
      this.$ul.children(".multiselect-group").removeClass("active-group");
    }
  },
  buttonText: function (qOptions, qSelect) {
    var opt = "" + qSelect.data("bms-allornum");
    if (opt != null) {
      if (qOptions.length == 0) return opt.replace("_", "all");
      return opt.replace("_", qOptions.length);
    }
    if (qOptions.length == 1) return qOptions.val();
    return "";
  },
  buttonTitle: function (qOptions, qSelect) {
    var title = qSelect.attr("title");
    if (title != null) return title;
    else return "";
  },
  onChange: function (qOption, isChecked) {
    this.updateOptGroups();
    /*
    // note qOption is either single jq object or array of them.
    if (qOption.length > 1) qOption = qOption[0]; // ensure only 1 jq obj.
    var val = qOption.val();
    var dropMenu = qOption.closest("select").next(".boot-multiselect-container"); // visible dropdown container
    if (dropMenu.length != 1) return;
    var dropItem = dropMenu.find('> ul > li > a > label > input[value="' + val + '"]').closest("li"); // dd item with same value as option.
    if (dropItem.length != 1) return;
    var prevItems = dropItem.prevUntil(".multiselect-group");
    var groupItem;
    if (prevItems.length > 0) groupItem = prevItems.last().prev(".multiselect-group"); // preceeding group if any.
    else groupItem = dropItem.prev(".multiselect-group");
    if (groupItem.length != 1) return; // no group found.
    var activeItems = groupItem.nextUntil(".multiselect-group").filter(".active"); // active items in this group.
    // add group class if any "sub-options" selected.
    if (activeItems.length > 0) groupItem.addClass("active-group");
    else groupItem.removeClass("active-group");
    */
  }
};

app_CommonFunctions.BootMultiSelect = function (jqSelectArg, optionsArg) {

  jqSelectArg.each(function (i, e) {

    $select = $(e);

    var opts = "" + $select.data("bms-options"); // options in select tag
    var optNoOptionTitles = opts.search(/\bnotitles\b/) >= 0;
    var optSelectAll = opts.search(/\bselectall\b/) >= 0;
    var optReset = opts.search(/\breset\b/) >= 0;
    var optEnableFiltering = opts.search(/\bfiltering\b/) >= 0;
    var options = app_CommonFunctions.BootMultiSelect_DefaultOptions;

    options.enableFiltering = optEnableFiltering;
    options.enableCaseInsensitiveFiltering = optEnableFiltering;
    options.includeResetOption = optReset;
    options.includeSelectAllOption = optSelectAll;
    options.optNoOptionTitles = optNoOptionTitles; // custom.
    options = $.extend(options, optionsArg);
    $select.multiselect(options);
  });
};

var TinyMCEInit;
var tableRowLinkHelper;
var tableRowLinkHelper_Timeout = null;

var CurrencyFormatter = new Intl.NumberFormat('en-AU', {
  style: 'currency',
  currency: 'AUD',
  //minimumFractionDigits: 0, // (this suffices for whole numbers, but will print 2500.10 as $2,500.1)
  //maximumFractionDigits: 0, // (causes 2500.99 to be printed as $2,501)
});

(function ($) {

  // Custom functions for button "wait" messages, often used with ajax calls.
  function SetMessageContent(thisBtn, message, dataName) {
    var msgResult = null;
    if (message) msgResult = message; // overrides btn data value
    else if (dataName) { // otherwise adopt btn data value if present
      var dataValue = thisBtn.data(dataName);
      if (dataValue) msgResult = dataValue;
    }
    if (msgResult != null) thisBtn.attr("data-content", msgResult);
  }

  const originalFocus = $.fn.focus;
  $.fn.focus = function () {
    return this.each(function () {
      let oThis = this;
      let $this = $(this);
      setTimeout(function () { $this[0].focus(); originalFocus.apply($this, arguments); }, 100);
    });
  };

  $.fn.hideRow = function (callback) {
    return this.each(function () {
      let $this = $(this);
      let $row = $this.closest(".row");
      if ($row.length === 1) $row.hide(callback);
    });
  };

  $.fn.showRow = function (callback) {
    return this.each(function () {
      let $this = $(this);
      let $row = $this.closest(".row");
      if ($row.length === 1) $row.slideDown(callback);
    });
  };

  // Note if a value should be set along with disable(),
  // make sure val() is called *before* disable() not after.
  $.fn.disable = function () {
    return ChangeDisabled(this, true);
    return this.each(function () {
      let $this = $(this);
      if ($this.is("button")) {
        $this.prop("disabled", true);
      } else if ($this.is("input") || $this.is("select") || $this.is("textarea")) {
        $this.prop("readonly", true);
        $this.data("tabindex", $this.prop("tabindex"));
        $this.prop("tabindex", "-1");
        if ($this.is(":checkbox") || $this.is(":radio")) {
          $this.prop("disabled", true);
        }
      }
    });
  };

  $.fn.readonly = function () {
    return ChangeDisabled(this, true);
    return this.each(function () {
      let $this = $(this);
      $this.prop("readonly", true);
      $this.addClass("disabled");
    });
  };

  $.fn.enable = function () {
    return ChangeDisabled(this, false);
    return this.each(function () {
      let $this = $(this);
      if ($this.is("input") || $this.is("select") || $this.is("textarea") || $this.is("button")) {
        $this.removeAttr("readonly");
        $this.removeAttr("disabled");
        if ($this.is("button")) $this.removeClass("disabled");
      }
      if ($this.data("tabindex") != null) $this.prop("tabindex", $this.data("tabindex"));
    });
  };

  function ChangeDisabled($els, disable) {

    return $els.each(function () {
      const $this = $(this);

      if (disable) {

        const tabIndex = toInt($this.prop("tabindex"), null);
        if (tabIndex > 0) $this.data("tabindex", $this.prop("tabindex")); // remember tabindex if > 0
        $this.prop("tabindex", -1)

        if ($this.is('textarea,input[type="text"],input[type="email"],input[type="number"],input[type="tel"],input[type="url"]')) {
          $this.prop("readonly", disable); // Allows copying text
        } else {
          $this.prop("disabled", disable);
        }
        $this.addClass("disabled");

      } else {

        $this.removeClass("disabled");
        $this.prop("disabled", false);
        $this.prop("readonly", false);
        $this.removeAttr("disabled");
        $this.removeAttr("readonly");
        // Restore tabindex if necessary.
        const tabIndex = toInt($this.data("tabindex"), null);
        if ($this.is('select:has("+ .select2")')) {
          // Sel2: restore on special element, requires delay to work.
          setTimeout(() => {
            $this.next().find('.select2-selection[tabindex="-1"]').attr("tabindex", tabIndex || 0);
          }, 400);
        } else if ($this.prop("tabindex") == -1) {
          $this.prop("tabindex", tabIndex || 0);
        }
      }
    });
  }

  $.fn.setChecked = function (selectedValue) {
    if (selectedValue == null) {
      return this.prop("checked", true);
    } else {
      return this.each(function () {
        if ($(this).val() === selectedValue) {
          this.checked = true;
        }
      });
    }
  };

  $.fn.filterByName = function (name) {
    if (name == null) return this;
    return this.filter(function () {
      return this.name === name;
    });
  };

  $.fn.filterByValue = function (value) {
    if (value == null) return this;
    return this.filter(function () {
      return $(this).val() === value;
    });
  };

  $.fn.filterByNameAndValue = function (name, value) {
    return this.filterByName(name).filterByValue(value);
  };

  $.fn.ShowTabName = function (tabName) {
    return this.each(function () {
      let $tabs = $(this);
      if ($tabs.eq(0).is("li,a")) $tabs = $tabs.closest("ul.nav-tabs");
      if (!$tabs.is("ul.nav-tabs")) return;
      $tabs.find(`li[data-tabname="${tabName}"] > a`).tab('show');
    });
  };

  $.fn.GetActiveTabName = function () {
    let $tabs = $(this);
    if ($tabs.eq(0).is("li,a")) $tabs = $tabs.closest("ul.nav-tabs");
    if (!$tabs.is("ul.nav-tabs")) return '';
    var $tab = $tabs.find('li.active')
    if ($tab.length !== 1) return '';
    return $tab.attr("data-tabname");
  };

  $.urlParam = function (paramName, optTestValue) {
    return urlParam(paramName, optTestValue);
  }

  function urlParam(paramName, optTestValue) {
    // If optTestValue is present, return true or false for case-insensitive match, or null if paramName not found.
    // If optTestValue is NOT present, return value or "" if no value exists (eg. "&x=&...") or null if paramName not found.
    // console test:
    // new RegExp('[?&]'
    //   + encodeURIComponent('modals/-!([edi.t,or]+{(:')
    //       .replaceAll(/([\[\]\(\)\+\{\}\(\)\%\.])/g,'\\$1')
    //   + '=([^&#]*)').exec('?modals/-!([edi.t,or]+{(:=5');
    var results = new RegExp('[\?&]' + encodeURIComponent(paramName).replaceAll(/([\[\]\(\)\+\{\}\(\)\%\.])/g, '\\$1') + '=?((?:(?<==)[^&#]*)|(?=&|#))').exec(window.location.search);
    if (results == null || typeof results.length === "undefined" || results.length !== 2) {
      return null;
    }
    if (typeof optTestValue !== "undefined") return optTestValue.toLowerCase() === results[1].toLowerCase();
    return decodeURIComponent(results[1]);
  }

  $.fn.DelayedChange = function (options) {

    const dataKey_oldSearchText = "oldsearchtext";
    const dataKey_keyTimeout = "keyTimeout";

    return this.each(function () {
      let $inp = $(this);
      options = {
        ...{
          minLength: 3,
          timeout: 600,
          callback: null
        },
        ...options
      };
      if (typeof options.callback !== 'function' || typeof options.minLength !== 'number' || typeof options.timeout !== 'number') return;
      if (!$inp.is('input[type="text"') && !$inp.is('input[type="search"')) return;
      $inp.data(dataKey_oldSearchText, $inp.val());
      $inp.keyup(function (evt) {
        if ($inp.data(dataKey_keyTimeout)) clearTimeout($inp.data(dataKey_keyTimeout));
        $inp.data(dataKey_keyTimeout, setTimeout(function () { $inp.data(dataKey_keyTimeout, null); SearchKeyTimeout($inp, options); }, options.timeout));
      });
    });

    function SearchKeyTimeout($inp, options) {
      const oldSearchText = "" + $inp.data(dataKey_oldSearchText);
      const newSearchText = "" + $inp.val();
      if (newSearchText === oldSearchText) return;
      $inp.data(dataKey_oldSearchText, newSearchText);
      if (newSearchText.length !== 0 && newSearchText.length < options.minLength) return;
      options.callback(newSearchText, $inp, options);
    }
  }

  $.EachPartial = function (callback) {

    var partialsFound = $(".partial-loader-container").length;
    if (partialsFound == 0) return;

    var partialsProcessed = 0;
    var busyProcessing = false;

    // Keep looping over all the partials until they have all been processed.
    // This solves a problem where this function ($.EachPartial) is called before all the partials have been initialised.
    var interval = setInterval(function () {

      if (busyProcessing) return;
      busyProcessing = true;

      $(".partial-loader-container").each(function (i, e) {

        var $e = $(e);
        var partialInfo = $e.data("partial-info");

        if (isObject(partialInfo)) { // Partial is initialised.

          if (partialInfo.isProcessed !== "true") {
            partialInfo.isProcessed = true;
            callback($e, partialInfo);
          }

          partialsProcessed++;
          if (partialsProcessed == partialsFound) {
            clearInterval(interval);
          }
        }
      });

      busyProcessing = false;

    }, 500);
  }

  $.fn.PartialInfo = function () {
    return this.data("partial-info");
  }

  $(document).ready(function () {

    // jQuery function overrides.
    var oldJqFnShow = $.fn.show;
    $.fn.show = function () {
      this.removeClass("hidden hide displaynone display-none"); // show function should also remove these.
      return oldJqFnShow.call(this);
    };

    // Global default animation speed.
    $.fx.speeds._default = 200;

    tableRowLinkHelper = $("#table-rowlink-helper");

    if ($('.search-input').length) {
      $('#txtSearch').val(''); // Clear the input value
    }

    $(".vh100").height($(window).height());

    // Highlight menu for current page,
    // Show menu item ".onlyShowCurrent" if link matches location.pathname.
    // If a submenu item exists which ALSO matches location.pathname then highlight the submenu item instead.
    (function () {
      var highlightClassCompare = app_menuHighlightPathPrefix + location.pathname.replace("/", "");
      $(".sidebar-menu li")
        .removeClass("active")
        .each(function (i, e) {
          var $e = $(e);
          var $a = $e.children("a:first:not(.btnNavMenu-noActive)");
          if ($a.length == 1) {
            if ($a[0].pathname.toLowerCase() == location.pathname.toLowerCase() || $a.hasClass(highlightClassCompare)) {
              // Check if a matching submenu exists.
              var $foundSubLink = null;
              $e.next("ul.submenu").find("li.submenu-item > a").each(function (subIndex, subLink) {
                var $subLink = $(subLink);
                if (subLink.pathname.toLowerCase() == location.pathname.toLowerCase() || $subLink.hasClass(highlightClassCompare)) {
                  $foundSubLink = $subLink;
                  return false;
                }
              });
              if ($foundSubLink != null) {
                $a = $foundSubLink;
                $e = $a.parent();
              }
              // Highlight menu.
              $a.parent("li.onlyShowCurrent").show();
              $e.addClass("active");
              $e.parent("ul").show();
              $e.parent("ul").parent("li").addClass("activeparent");
              // Show menu text as page title unless it is already there.
              var $pageTitle = $(".content-header:first .pageTitle");
              if ($pageTitle.length == 1 && "" + $pageTitle.text() == "") $pageTitle.text($a.text());
              return false;
            }
          }
        });
      $(".onlyShowCurrent a").click(function (e) { e.preventDefault(); });
    })();

    $.fn.select2.defaults.set("selectOnClose", false);
    $.fn.select2.defaults.set("minimumInputLength", 0);
    $.fn.select2.defaults.set("dropdownAutoWidth", true);
    $.fn.select2.defaults.set("minimumResultsForSearch", 21);
    $.fn.select2.defaults.set("searchPlaceholder", "");
    $.fn.select2.defaults.set("width", ""); // Prevent auto-resolve (https://select2.org/appearance#container-width)

    // Keyboard scroll while input is focussed.
    var $inpkeyScroll = $('input[type="text"][autocomplete="off"]:not(.keyscroll-done)')
    $inpkeyScroll
      .addClass("keyscroll-done")
      .keydown(function (event) {
        if (event.which === 38) { // up arrow
          window.scrollBy(0, -50);
        } else if (event.which === 40) { // down arrow
          window.scrollBy(0, 50);
        }
      });

    $.ajaxSetup({});

    $.ajaxPrefilter(function (options, originalOptions, jqXHR) {

      // Ensure a reference to referrer page is always sent (built-in referer(sic) header isn't consistent).
      var isSameOrigin = !options.crossDomain;
      if (isSameOrigin) {
        jqXHR.setRequestHeader(HttpHeader_IsAjax, true);
        jqXHR.setRequestHeader(HttpHeader_Referrer, window.location.href);
      }
    });

    // This disables the select2 tooltips.
    $(document).on('mouseenter', '.select2-selection__rendered', function () {
      var popupId = $(this).attr("aria-describedby");
      $("#" + popupId).hide();
      $(this).unbind('mouseenter mouseleave');
    });

    $("body").on('keydown', ".select2-container", function (e) {
      if (e.which < 32) return;
      var $sel2 = $(e.target);
      if (!$sel2.hasClass("select2-container")) $sel2 = $sel2.closest(".select2-container");
      if ($sel2.length != 1) return;
      var $sel = $sel2.prev();
      if ($sel.length != 1 || $sel[0].tagName.toLowerCase() != "select") return;
      if (e.which == 37 || e.which == 39) {
        var $selectedOpt = $sel.find("option:selected");
        if ($selectedOpt.length != 1) return;
        var newVal;
        if (e.which == 37)
          $selectedOpt = $selectedOpt.prevAll(":enabled").first();
        else
          $selectedOpt = $selectedOpt.nextAll(":enabled").first();
        if ($selectedOpt.length != 1) return;
        $sel.val($selectedOpt.val());
        $sel.trigger("change");
      } else {
        $sel.select2('open');
        var search = $sel.data('select2').dropdown.$search || $sel.data('select2').selection.$search;
        search.focus();
      }
    });

    $("body").mousemove(TableRowLink_BodyMouseMove);

    // Prevent jQuery UI dialog from blocking focusin
    // See https://www.tiny.cloud/docs/integrations/jquery/
    $(document).on('focusin', function (e) {
      if ($(e.target).closest(".tox-tinymce, .tox-tinymce-aux, .moxman-window, .tam-assetmanager-root").length) {
        e.stopImmediatePropagation();
      }
    });

    addEventListener('beforeunload', (event) => {
      setTimeout(function () { $.busyLoadFull("show"); }, 50); // Slight delay fixes issue with spinner not animating.
    });

    $(".switch-user-role").on("click keypress", function (ev) {
      if (ev.which != 1 && ev.which != 13) return; // Left mouse or Enter.
      var ajaxData = {}; ajaxData[app_formKey_UserRole] = $(this).data("role");
      AjaxSubmit({
        url: app_SetUserRoleUrl,
        data: ajaxData
      });
    });

    // Hide slideout when clicking outside it or in the header, or pressing ESC.
    $(document).on("click", "#" + JS_ElementID_SlideoutBackdrop + ", #" + JS_ElementID_SlideoutPanelHeader, function (ev) {
      ev.stopImmediatePropagation();
      $("body").removeClass("slideout-init slideout-show");
    });
    $(document).on("keyup", function (ev) {
      if (ev.which == 27 && $("body").hasClass("slideout-show")) {
        ev.stopImmediatePropagation();
        $("body").removeClass("slideout-init slideout-show");
      }
    });

    // Common, general-purpose slideout activator. Left-click or press Enter on it.
    // Note difference between ev.target and ev.CurrentTarget (https://developer.mozilla.org/en-US/docs/Web/API/Event/Comparison_of_Event_Targets)
    $(document).on('click keypress', '[data-' + JS_DataAttrName_SlideoutTrigger + ']', function (ev) {

      if (ev.which != 13 && ev.which != 1) return; // Ignore all except Enter and left-mouse.
      if (ev.target.tagName == "A" && ev.target != ev.currentTarget) return; // Ignore links inside the trigger element so the links still work.
      ev.preventDefault();
      ev.stopImmediatePropagation();

      var triggerElement = $(ev.currentTarget); // The trigger element (currentTarget) has the data.

      if (triggerElement.hasClass("disabled") || triggerElement.is(":disabled") || triggerElement.data(JS_DataAttrName_SlideoutTrigger) !== true) return;

      ShowSlideout(triggerElement);
    });

    // Common, general-purpose modal activator. Left-click or press Enter on it.
    // Note difference between ev.target and ev.CurrentTarget (https://developer.mozilla.org/en-US/docs/Web/API/Event/Comparison_of_Event_Targets)
    $(document).on('click keypress', '[data-' + JS_DataAttrName_ModalPartialUrl + ']', function (ev) {

      if (ev.which != 13 && ev.which != 1) return; // Ignore all except Enter and left-mouse.
      if (ev.target.tagName == "A" && ev.target != ev.currentTarget) return; // Ignore links inside the trigger element so the links still work.
      ev.preventDefault();
      ev.stopImmediatePropagation();

      var triggerElement = $(ev.currentTarget); // The trigger element (currentTarget) has the data.

      if (triggerElement.hasClass("disabled") || triggerElement.is(":disabled")) return;

      ShowModal(triggerElement);
    });

    $(".navbar-nav > .dropdown > .dropdown-toggle").keydown(function (ev) {
      var $e = $(this);
      var $dd = $e.next(".dropdown-menu:visible");
      if ($dd.length == 1 && ev.which === 9 && ev.shiftKey) { // shift-tab
        $dd.dropdown('toggle');
      }
    });
    $(".dropdown-menu > .dropdown-item").keydown(function (ev) {
      var $e = $(this);
      if (ev.which === 9) { // tab
        if ($e.is(":last-child") && !ev.shiftKey || $e.is(":first-child") && ev.shiftKey) {
          $(this).closest(".dropdown-menu").dropdown('toggle'); // close menu when tabbing off
        }
      }
    });
    $('.add-participant-dropdown').on('click', '.dropdown-item', function (event) {
      event.preventDefault();
      var addpaxurl = $(this).data('addpaxurl');
      var modaltitle = $(this).data('modaltitle');

      // The dropdown can contain items that don't trigger modals.
      // In which case the item won't contain these data attributes.
      if (!addpaxurl || !modaltitle) return;

      ShowAddParticipantPopup(addpaxurl, modaltitle);
    });

    common_UpdateUI();

    InitPartialLoaders(); // common_UpdateUI is called after each partial is loaded.

    DoSlideoutOnPageLoad();
    DoModalOnPageLoad(); // Note modal should be last so it is on top, even if a slideout is also shown. Modals are usually more important.

  }); // doc ready.

  function ShowModal(dataElement) {

    var modalPartialUrl = dataElement.data(JS_DataAttrName_ModalPartialUrl);
    var modalTitle = dataElement.data(JS_DataAttrName_ModalTitle);
    if (isStringNullOrEmpty(modalPartialUrl)) return;

    BootstrapDialog.show({
      title: isStringNullOrEmpty(modalTitle) ? "Details" : modalTitle,
      onshow: function (dialogRef) {
        var modalDialog = dialogRef.getModalDialog();
        modalDialog.css("width", "850px");
        modalDialog.data(JS_DataAttrName_DialogRef, dialogRef);
        var modalBody = dialogRef.getModalBody();
        modalBody.busyLoad("show");
        modalBody.load(modalPartialUrl,
          function (data) {
            modalBody.html(data);
            modalBody.busyLoad("hide");
            common_UpdateUI(modalBody);
          }
        );
      },
      onhide: function (dialogRef) {
        // Properly dispose of any tinymce instance in the modal.
        var modalDialog = dialogRef.getModalDialog();
        modalDialog.find("textarea.tinymce").each(function (i, e) {
          var mce = $(e).data("editor");
          if (mce != null) mce.remove();
        });
      }
    });
  }

  function DoModalOnPageLoad() {

    var dataElement = $('[data-' + JS_DataAttrName_ModalShowOnPageLoad + ']:first');
    if (dataElement.length > 0) {
      if (dataElement.length > 1) dataElement = dataElement.eq(0); // Only process the first one.
      ShowModal(dataElement)
    }
  }

  function ShowAddParticipantPopup(urlPath, modalTitle) {
    BootstrapDialog.show({
      title: modalTitle,
      onshow: function (dialogRef) {
        var modalDialog = dialogRef.getModalDialog();
        modalDialog.css("width", "700px");
        modalDialog.data(JS_DataAttrName_DialogRef, dialogRef);
        var modalBody = dialogRef.getModalBody();
        modalBody.busyLoad("show");
        modalBody.load(urlPath,
          function (data) {
            modalBody.html(data);
            modalBody.busyLoad("hide");
            common_UpdateUI(modalBody);
          }
        );
      },
      onhide: function (dialogRef) {
        var modalDialog = dialogRef.getModalDialog();
        modalDialog.find("textarea.tinymce").each(function (i, e) {
          var mce = $(e).data("editor");
          if (mce != null) mce.remove();
        });
      }
    });
  }

  function ShowSlideout(dataElement) {

    var slideoutTitle = dataElement.data(JS_DataAttrName_SlideoutTitle) ?? "Information";
    var slideoutPartialUrl = dataElement.data(JS_DataAttrName_SlideoutPartialUrl);
    var slideoutCallbackFn = dataElement.data(JS_DataAttrName_SlideoutCallbackFunction);

    if (isStringNullOrEmpty(slideoutPartialUrl)) return;

    $("#" + JS_ElementID_SlideoutPanelTitle).text(slideoutTitle);
    $("#" + JS_ElementID_SlideoutPanelBody).empty();

    if (!isStringNullOrEmpty(slideoutPartialUrl)) {
      ShowSlideoutWithUrl(slideoutPartialUrl); // Content from Url.
    } else if (isFunction(slideoutCallbackFn)) {
      ShowSlideoutWithCallback(slideoutCallbackFn); // Content provided by callback.
    }
  }

  function DoSlideoutOnPageLoad() {

    var dataElement = $('[data-' + JS_DataAttrName_SlideoutShowOnPageLoad + ']:first');
    if (dataElement.length > 0) {
      if (dataElement.length > 1) dataElement = dataElement.eq(0); // Only process the first one.
      ShowSlideout(dataElement)
    }
  }

  function ShowSlideoutWithUrl(slideoutPartialUrl) {

    // If the URL is a relative path (starts with "/") then add the current domain to make a complete URL.
    if (slideoutPartialUrl.substring(0, 1) == "/") slideoutPartialUrl = location.origin + slideoutPartialUrl;
    // Check if URL string is valid.
    try {
      slideoutPartialUrl = new URL(slideoutPartialUrl).href;
    } catch (e) {
      return; // Invalid url.
    }

    var slideoutBody = $("#" + JS_ElementID_SlideoutPanelBody);

    slideoutBody.busyLoad("show");
    ShowSlideoutWithCallback(function () {
      $.get(slideoutPartialUrl, function (data) {
        $("#" + JS_ElementID_SlideoutPanelBody).append(data);
        window.setTimeout(function () {
          common_UpdateUI($("#" + JS_ElementID_SlideoutPanelBody));
          $("#" + JS_ElementID_SlideoutPanelBody).busyLoad("hide");
        }, 500);
      });
    });
  }

  function ShowSlideoutWithCallback(slideoutCallbackFn) {
    if (isFunction(slideoutCallbackFn)) {
      $("body").addClass("slideout-init slideout-show");
      slideoutCallbackFn();
    }
  }

  function InitPartialLoaders() {

    $('.partial-loader-container[data-partial-url]').each(function (i, e) {

      $eachContainer = $(e);

      var partialInfo = {
        containerElement: $eachContainer,
        id: $eachContainer.attr("id"),
        rndid: $eachContainer.attr("data-partial-rndid"),
        url: $eachContainer.attr("data-partial-url"),
        initialUrl: $eachContainer.attr("data-partial-url"),
        initialWidth: $eachContainer.attr("data-partial-initial-width") || "100%",
        initialHeight: $eachContainer.attr("data-partial-initial-height") || "100px",
        deferInitialLoad: $eachContainer.attr("data-partial-defer-initial-load") == "true",
        initialStyle: "partial-loader-style-" + $eachContainer.attr("data-partial-initial-style"),
        loaderStyle: "partial-loader-style-" + $eachContainer.attr("data-partial-loader-style"),
        waitUntilVisible: $eachContainer.attr("data-partial-waituntilvisible") == "true",
        isVisible: true, // make false when visivility watcher is implemented.
        waitForPageTabName: $eachContainer.attr("data-partial-waitforpagetabname"),
        waitForPageTabElement: null,
        isPageTabActive: false,
        waitForPartialIdLoaded: $eachContainer.attr("data-partial-waitforpartialidloaded"),
        isPartialIdLoaded: false,
        delayMs: toDecimalInt($eachContainer.attr("data-partial-delayms"), 0),
        spinnerElement: $('<div class="partial-loader-placeholder-spinner displaynone" />'),
        contentElement: $('<div class="partial-loader-content" />'),
        placeholderElement: $('<div class="partial-loader-placeholder" />'),
        reloadButtonElement: $('<button title="[dev] Reload Partial" class="partial-loader-reload-btn"><ion-icon name="reload-outline"></ion-icon></button>'),
        forceNextLoad: false,
        isProcessed: false, // set when looping through partials to load them.
        isLoaded: false, // set when loading is completed.

        Clear: function () {
          this.contentElement.empty();
          this.isProcessed = false;
        },
        LoadUrl: function (url, paramsObject) {
          var newUrl = url || this.url
          this.url = AbleJS.Util.PatchQuery({
            url: newUrl,
            params: paramsObject
          });
          this.forceNextLoad = true;
          this.containerElement.trigger("partial-loader-begin");
        },
        Reload: function () {
          this.forceNextLoad = true;
          this.containerElement.trigger("partial-loader-begin");
        }
      };

      $eachContainer.data("partial-info", partialInfo);
      $eachContainer.addClass(partialInfo.initialStyle);

      partialInfo.placeholderElement.css({
        "width": partialInfo.initialWidth,
        "height": partialInfo.initialHeight
      });
      partialInfo.placeholderElement.append(partialInfo.spinnerElement);
      partialInfo.contentElement.css({ "display": "none" });
      $eachContainer.append(partialInfo.placeholderElement);
      $eachContainer.append(partialInfo.contentElement);

      if (app_isDev) {
        // Add reload button.
        partialInfo.reloadButtonElement.data("container", $eachContainer);
        partialInfo.reloadButtonElement.click(function (ev) {
          var thisContainer = $(ev.currentTarget).data("container");
          if (isJQuery(thisContainer)) {
            var thisPartialInfo = thisContainer.data("partial-info");
            if (thisPartialInfo) thisPartialInfo.Reload();
          }
        });
        $eachContainer.append(partialInfo.reloadButtonElement);
      }

      if (!isStringNullOrEmpty(partialInfo.waitForPageTabName)) {
        partialInfo.waitForPageTabElement = $("a#tab-" + partialInfo.waitForPageTabName);
        if (partialInfo.waitForPageTabElement.length != 1) {
          // Page tab must exist at this point.
          partialInfo.waitForPageTabName = null;
          partialInfo.waitForPageTabElement = null;
        } else {
          (function (thisPartialInfo) {
            thisPartialInfo.waitForPageTabElement.on('shown.bs.tab', function (ev) {
              thisPartialInfo.isPageTabActive = true;
              TryLoadPartial(thisPartialInfo.containerElement);
            });
          })(partialInfo);
        }
      }

      // This event is triggered to begin loading.
      // It is also triggered by each event the partial may be waiting for (visibility, page tab, another partial loaded, etc)
      $eachContainer.on("partial-loader-begin", function (ev) {
        TryLoadPartial($(ev.target));
      });

      // Check if can load now.
      if (!partialInfo.deferInitialLoad) TryLoadPartial($eachContainer);

    }); // Each partial container.
  }

  function TryLoadPartial($partialContainer) {

    var partialInfo = $partialContainer.data("partial-info");
    if (!partialInfo) return;

    if (partialInfo.forceNextLoad !== true) {
      // Check if all conditions are met to load.
      if (partialInfo.isLoaded === true) return; // Already loaded.
      if (isStringNullOrEmpty(partialInfo.url) || !isJQuery(partialInfo.contentElement)) return false;
      if (!isStringNullOrEmpty(partialInfo.waitForPartialIdLoaded) && partialInfo.isPartialIdLoaded !== true) return false;
      if (!isStringNullOrEmpty(partialInfo.waitForPageTabName) && partialInfo.isPageTabActive !== true) return false;
    }

    partialInfo.forceNextLoad = false;
    $partialContainer.removeClass(partialInfo.initialStyle).addClass(partialInfo.loaderStyle);

    // Ok to load.
    try {
      partialInfo.contentElement.hide();
      partialInfo.placeholderElement.show();
      partialInfo.spinnerElement.show();

      var params = {};
      params[app_urlKey_PartialRandomId] = partialInfo.rndid;
      var url = AbleJS.Util.PatchQuery({
        url: partialInfo.url,
        params: params
      });

      setTimeout(function (thisPartialInfo) {
        $.get(url, function (data) {
          thisPartialInfo.contentElement.empty().append(data);
          thisPartialInfo.placeholderElement.hide();
          thisPartialInfo.contentElement.show();
          common_UpdateUI(thisPartialInfo.containerElement);
          thisPartialInfo.isLoaded = true;
          thisPartialInfo.containerElement.trigger("partial-loader-loaded");
        });
      }, partialInfo.delayMs, partialInfo);
    } catch (e) {
      return false;
    }

    return true;
  }

  TinyMCEInit = function (strSelector, options) {
    var $textArea = $(strSelector);
    if ($textArea.length != 1) return;
    defaults = {
      mergeTags: null,
      preSetupFn: null,
      postSetupFn: null,
      selector: strSelector,
      readonly: $textArea.prop("readonly") || $textArea.prop("disabled") ? 1 : 0,
      autoresize_min_height: $(strSelector).eq(0).height(),
      autoresize_max_height: 300,
      autoresize_bottom_margin: 5,
      min_height: $(strSelector).eq(0).height(),
      elementpath: false,
      menubar: false,
      statusbar: false,
      body_class: 'richEditorBody',
      content_css: [
        app_cssPath + 'AdminLTE-2.3.11-no-importants.min.css',
        'https://cdnjs.cloudflare.com/ajax/libs/admin-lte/2.3.11/css/skins/skin-blue.min.css',
        app_cssPath + 'portal-site.css?v=' + app_scriptVer,
        app_cssPath + 'adminlte-custom.css?v=' + app_scriptVer],
      toolbar: $textArea.prop("readonly") || $textArea.prop("disabled") ? false : 'undo redo | bold italic underline | alignleft aligncenter alignright | bullist numlist | link',
      plugins: 'autoresize lists link image paste', // note must include 'paste' to handle pasting from MS Word.
      paste_as_text: true,
      convert_urls: false,
      relative_urls: false,
      remove_script_host: false,
      paste_preprocess: function (pl, o) { }, // If paste processing needed, o.content contains pasted html.
      iframe_aria_text: '',
      setup: function (editor) {
        AddMergeTags(editor);
        if (typeof editor.settings.preSetupFn == "function") editor.settings.preSetupFn(editor);
        editor.on('init', function (e) {
          editor.setContent($textArea.val());
          $('#' + e.target.id + '_ifr').removeAttr('title'); // Remove annoying tooltip.
          $textArea.data("editor", editor);
        })
          .on('focus', function (e) {
            $(editor.getContainer()).addClass("focus");
          })
          .on('blur', function (e) {
            $textArea.val(editor.getContent()); // Copy tinymce content back to textarea.
            $(editor.getContainer()).removeClass("focus");
          });
        if (typeof editor.settings.postSetupFn == "function") editor.settings.postSetupFn(editor);
      }
    };
    function AddMergeTags(editor) {
      var mergeTags = editor.settings.mergeTags;
      if (typeof mergeTags != "object" || mergeTags == null || typeof mergeTags.forEach != "function") return;
      editor.settings.toolbar += " | insertmerge ";
      editor.ui.registry.addMenuButton('insertmerge', {
        text: 'Insert Merge Tag',
        fetch: function (callback) {
          var items = [];
          mergeTags.forEach(item => {
            items.push({
              type: 'menuitem',
              text: item.name,
              onAction: function () { editor.insertContent(item.value); }
            });
          });
          callback(items);
        }
      });
    }
    tinymce.init($.extend(defaults, options));
    $textArea.change(function (e) {
      var editor = $textArea.data("editor");
      if (editor == null) return;
      if (editor.setContent) editor.setContent($textArea.val());
    });
  }

  function TableRowLink_BodyMouseMove(ev) {
    if (tableRowLinkHelper.data("row") == null) return;
    var tlPos = tableRowLinkHelper.position();
    if (tableRowLinkHelper_Timeout != null) return; // Wait till last one is finished.
    tableRowLinkHelper_Timeout = setTimeout(function () {
      if (tableRowLinkHelper.data("row")) {
        if (ev.pageX < tlPos.left || ev.pageX > tlPos.left + tableRowLinkHelper.width() || ev.pageY < tlPos.top || ev.pageY > tlPos.top + tableRowLinkHelper.height()) {
          var $tr = tableRowLinkHelper.data("row");
          tableRowLinkHelper.data("row", null);
          tableRowLinkHelper.data("url", null);
          tableRowLinkHelper.offset({ left: -20, top: -20 });
          if ($tr && $tr.removeClass) $tr.removeClass("hover");
        }
      }
      tableRowLinkHelper_Timeout = null;
    }, 50);
  }

})(jQuery);

function common_GetPartialInfo($element) {
  if ($element == null) return;
  if ($element.tagName) $element = $($element);
  if (!isJQuery($element) || $element.length != 1) return;
  return $element.closest(".partial-loader-container").data("partial-info");
}

function common_GetMomentDate(dtStringOrDate, strMomentFormat) {
  if (dtStringOrDate == null || dtStringOrDate == "") return null;
  if (moment.isMoment(dtStringOrDate)) return dtStringOrDate.clone();
  if (isString(dtStringOrDate)) {
    if (isString(strMomentFormat)) return new moment(dtStringOrDate, strMomentFormat, true);
    else return new moment(dtStringOrDate);
  } else if (isDate(dtStringOrDate)) {
    return new moment(dtStringOrDate);
  } else {
    throw "Must be string or date.";
  }
}

function common_GetMomentFromDatepicker(inputNameOrJQ) {
  if (isString(inputNameOrJQ)) {
    inpDate = $('input[name="' + inputNameOrJQ + '"]');
  } else if (isJQuery(inputNameOrJQ)) {
    inpDate = inputNameOrJQ;
  } else return;
  if (inpDate.length != 1) return null;
  return new moment(inpDate.val(), DATEPICKER_OUTPUT_FORMAT_MOMENTJS, true);
}

function common_UpdateDatePicker(inputNameOrJQ, displayDateStringOrMoment, disable) {
  var inpDate;
  if (isString(inputNameOrJQ)) {
    inpDate = $('input[name="' + inputNameOrJQ + '"]');
  } else if (isJQuery(inputNameOrJQ)) {
    inpDate = inputNameOrJQ;
  } else return;
  if (inpDate.length != 1) return;
  var inpContainer = inpDate.parent(".input-group.datepicker");
  if (inpContainer.length != 1) return;
  var momentDisplayDate = common_GetMomentDate(displayDateStringOrMoment);
  if (momentDisplayDate == null) {
    inpContainer.datepicker("update", "");
  } else {
    if (momentDisplayDate.isValid()) inpContainer.datepicker("update", momentDisplayDate.format(DATEPICKER_OUTPUT_FORMAT_MOMENTJS));
  }
  if (disable === true) inpDate.prop("disabled", true);
  else inpDate.prop("disabled", false);
}

function common_UpdateUI(withinElement) {

  if (withinElement) {
    setTimeout(function () { UpdateUI(withinElement); }, 200); // Small delay with dynamic content.
  } else {
    UpdateUI(); // Immediate for page on load.
  }

  function UpdateUI(withinElement) {

    if (isString(withinElement) || isHTMLElement(withinElement)) {
      withinElement = $(withinElement);
    } else if (!isJQuery(withinElement)) {
      withinElement = $("body");
    }

    withinElement.find("form").submit(function (ev) {
      ev.preventDefault();
      return false;
    });

    // apply date pickers
    withinElement.find('.input-group:has(.control-datepicker):not(.datepicker-initdone)').each(function (i, e) {
      $inp = $(e);
      $inp.addClass("datepicker-initdone");
      $inp.datepicker({ // see https://uxsolutions.github.io/bootstrap-datepicker/
        format: DATEPICKER_OUTPUT_FORMAT_JS,
        todayBtn: true,
        clearBtn: true,
        autoclose: true,
        todayHighlight: true,
        enableOnReadonly: false
      }).on("changeDate", function (e) {
        $inp.find("input").trigger("change");
      }).on("show", function (e) {
        $(".datepicker-dropdown tfoot").each(function (i, e) {
          // Put footer button next to each other, not on separate rows.
          var $tfoot = $(this);
          var $th = $tfoot.find("tr > th:first-child");
          if ($th.length == 2) {
            $th.attr("colspan", "4");
            $th.eq(0).after($th.eq(1));
          }
        });
      });
    });

    // Prevent use of up & down arrow keys on button groups, to avoid unexpected change of selection.
    withinElement.find(".btn-group-toggle").on("keydown", evt => {
      if (evt.which == 38 || evt.which == 40) evt.preventDefault();
    });

    // iCheck init.
    if ($.browser && (!$.browser.msie || parseFloat($.browser.version) > 10) && $.fn.on && $.fn.iCheck) {

      var $iChecks = withinElement.find("input.icheck:not(.icheck-done)");

      // Register iCheck events before iCheck init.
      $iChecks
        .addClass("icheck-done")
        .on('ifUpdated', function () {
          //console.log("icheck updated");
        })
        .on('ifCreated', function () {
          var $chk = $(this);
          var icheckTypeClass = "icheck-" + this.type.toLowerCase(); // icheck-checkbox or icheck-radio
          var $holder = $chk.parent();
          if ($holder.parent().prop("tagName") == "LABEL") $holder = $holder.parent();
          $chk.data("holder", $holder);
          $holder.addClass("icheck-holder " + icheckTypeClass);
          $holder.toggleClass("icheck-disabled", $chk.is(":disabled") || $chk.prop("readonly") === true);
          $holder.parent(".checkbox,.radio").addClass("icheck-holder-margin");
          if ($holder.has(".control-comment").length > 0) $holder.addClass("hasComment");
          // label
          $chk.data("label", null);
          if (this.id) {
            var $lbl = $('label[for="' + this.id + '"]');
            if ($lbl.length > 0) {
              $chk.data("label", $lbl);
              $lbl.addClass("icheck-label " + icheckTypeClass).attr("tabindex", "0");
            }
          }
          $holder.on("keypress", evt => {
            if (evt.which != 32 && evt.which != 13) return;
            evt.preventDefault();
            $chk[0].checked = !$chk[0].checked;
            icheck_change($chk);
          });
          icheck_change($chk);
        })
        .on('ifChanged', function () {
          icheck_change($(this));
        })
        .on('change', function (evt, isInternal) {
          var $chk = $(evt.target);
          if (!isInternal) icheck_change($chk);
        });
      function icheck_change($chk) {
        var $holder = $chk.data("holder");
        if (!$holder) {
          $holder = $chk.closest("icheck-holder");
          $chk.data("holder", $holder);
        }
        var $lbl = $chk.data("label");
        $holder.toggleClass("icheck-on", $chk.is(":checked"));
        $holder.toggleClass("icheck-disabled", $chk.is(":disabled") || $chk.prop("readonly") === true);
        if ($lbl != null) {
          $lbl.toggleClass("icheck-on", $chk.is(":checked"));
          $lbl.toggleClass("icheck-disabled", $chk.is(":disabled") || $chk.prop("readonly") === true);
        }
        $chk.trigger("change", true);
      }
      $iChecks.each(function (i, e) {
        var thisRadio = $(e);
        if (thisRadio.hasClass("yellow")) {
          thisRadio.iCheck({
            checkboxClass: 'icheckbox_square-yellow',
            radioClass: 'iradio_square-yellow',
            cursor: true,
            inheritClass: true
            //,increaseArea: '20%'
          });
        } else {
          thisRadio.iCheck({
            checkboxClass: 'icheckbox_square-blue',
            radioClass: 'iradio_square-blue',
            cursor: true,
            inheritClass: true
            //,increaseArea: '20%'
          });
        }
        thisRadio
          .on("click", function () { $(this).iCheck("update"); })
          .on("change", function () { $(this).iCheck("update"); })
          .on("focus", function () { $(this).closest(".icheck-holder").addClass("icheck-focus"); /*$(this).closest(".icheck-label").addClass("icheck-focus");*/ })
          .on("blur", function () { $(this).closest(".icheck-holder").removeClass("icheck-focus"); /*$(this).closest(".icheck-label").removeClass("icheck-focus");*/ })
        //.parent() // add "holder" div
        //.addClass("icheck-holder")
        //.parent(".radioWrap")
        //.addClass("icheck-wrap")
        if (thisRadio.prop("readonly") === true) thisRadio.iCheck('disable');
        ;
      }); // elements loop
      if ($iChecks.length > 0) {
        // periodically update icheck elements in case of unusual changes (form recovery etc).
        var icheckUpdate = function () {
          if (app_jsErrorOccurred) return;
          $iChecks.each(function (i, e) {
            var $chk = $(e); // $iChecks.eq(i);
            var $iCheckHolder = $chk.data("holder");
            if (!$iCheckHolder) {
              $iCheckHolder = $chk.closest("icheck-holder");
              $chk.data("holder", $iCheckHolder);
            }
            var hasCheckedClass = $iCheckHolder.hasClass("icheck-on");
            var hasDisabledClass = $iCheckHolder.hasClass("icheck-disabled");
            if (e.checked != hasCheckedClass || e.disabled != hasDisabledClass) $chk.iCheck("update"); // update if there is a discrepancy in state.
          });
        }
        setInterval(icheckUpdate, 3000);
        $(document).on("mouseenter", icheckUpdate); // more immediate in some cases.
      } // elements found.

    } // browser/icheck check.

    withinElement.find("select.boot-multiselect-auto:not(.multiselect-done)").each(function (i, e) {
      var $e = $(e);
      $e.addClass("multiselect-done");
      app_CommonFunctions.BootMultiSelect($e);
    });

    // Elements to create square checkbox
    withinElement.find("input.square:checkbox:not(.square-checkbox-done)").each(function (i, e) {
      var chk = $(e);
      chk.addClass("square-checkbox-done");
      chk.wrap("<label class='inpchk-square-wrapper'></label>").after("<span class='inpchk-square-marker'></span>");
    });

    // Elements to create sliding checkbox.
    withinElement.find("input.slider:checkbox:not(.slider-checkbox-done)").each(function (i, e) {
      var chk = $(e);
      chk.addClass("slider-checkbox-done");
      chk.wrap("<label class='inpchk-slider-wrapper'></label>").after("<span class='inpchk-slider-marker'></span>");
    });

    // Currency inputs.
    withinElement.find('input.inp-currency:not(.autonumeric)').each(function (i, e) {
      var $e = $(e);
      var decimalPlaces = $e.data('decimalplaces') || 0;
      var preventNegative = e.hasAttribute('data-preventnegative');
      var autoNumeric = new AutoNumeric(e, {
        currencySymbol: '$',
        emptyInputBehavior: "always",
        decimalPlaces: decimalPlaces,
        unformatOnSubmit: false,
        modifyValueOnWheel: false,
        watchExternalChanges: true,
        onInvalidPaste: 'ignore',
        showWarnings: false,
        overrideMinMaxLimits: withinElement.prop('readonly') ? 'ignore' : 'ceiling'
      });

      if (preventNegative) {
        try {
          autoNumeric.options.minimumValue(0); // Throws error when value is already -ve, so ignore.
          autoNumeric.options.onInvalidPaste('replace');
        } catch (e) { }
      }
      $e.addClass("autonumeric").data('autonumeric', autoNumeric);
    })
      .change(function () {
        $(this).data('autonumeric').set(this.value);
      });

    // Percent inputs.
    withinElement.find('input.inp-percent:not(.autonumeric)').each(function (i, e) {
      var $e = $(e);
      $e.addClass("autonumeric");
      var decimalPlaces = $e.data('decimalplaces') || 0;
      new AutoNumeric(e, {
        suffixText: '%',
        decimalPlaces: decimalPlaces,
        unformatOnSubmit: false,
        modifyValueOnWheel: false,
        watchExternalChanges: true,
        minimumValue: 0,
        onInvalidPaste: 'ignore',
        showWarnings: false,
        overrideMinMaxLimits: withinElement.prop('readonly') ? 'ignore' : 'ceiling'
      });
    });

    // Attach jBox to IconTooltip
    withinElement.find('.iconTooltip').has('span[data-tooltiptitle]').each(function () {
      var thisTooltip = $(this).find('span');
      var tooltipTitle = thisTooltip.attr('data-tooltiptitle');
      var tooltipText = thisTooltip.attr('data-tooltiptext');
      var tooltipElementId = thisTooltip.attr('data-tooltipElementId');
      if (isStringNullOrEmpty(tooltipTitle) && isStringNullOrEmpty(tooltipText) && isStringNullOrEmpty(tooltipElementId)) return;
      if (isStringNullOrEmpty(tooltipText) && !isStringNullOrEmpty(tooltipTitle)) {
        tooltipText = tooltipTitle;
        tooltipTitle = null;
      }
      SetJBoxTooltip(thisTooltip, {
        position: { y: 'top', x: 'center' },
        title: isStringNullOrEmpty(tooltipTitle) ? null : tooltipTitle,
        content: isStringNullOrEmpty(tooltipElementId) ? tooltipText : $("#" + tooltipElementId)
      });
    });

    // Selects.
    var select2selects = withinElement.find("select").not(".noselect2,[data-bms-options]");
    for (let i = 0; i < select2selects.length; i++) {

      var sel = select2selects.eq(i);
      var options = {};

      options.minimumResultsForSearch = sel.data("minimumresultsforsearch") || null;
      options.minimumInputLength = sel.data("minimuminputlength") || null;
      options.dropdownAutoWidth = (sel.data("dropdownautowidth") || "false") == "true";
      options.dropdownCssClass = sel.data("dropdowncssclass") || null;
      options.placeholder = sel.data("placeholder") || null;
      options.searchPlaceholder = sel.data("searchplaceholder") || null;

      if (sel.hasClass(JS_PartnerDropdown_Class)) {
        options.templateResult = FormatPartnerDropdownState;
        options.templateSelection = FormatPartnerDropdownState;
      }

      (function () {
        var ajaxData = common_GetFormControlAjaxData(sel);
        var ajaxSearchKey = sel.data(JS_DataAttrName_AjaxSearchKey) || null;
        // Ajax data if provided.
        if (ajaxData.dataType != null && ajaxSearchKey != null) {
          options.minimumInputLength = options.minimumInputLength || 3;
          options.ajax = {
            dataType: ajaxData.dataType,
            url: ajaxData.alternateUrl || document.location.href,
            data: function (params) {
              if (ajaxData.formData == null) ajaxData.formData = {};
              ajaxData.formData[ajaxSearchKey] = params.term;
              return ajaxData.formData;
            },
            delay: 750
          }
        }
      })();

      // If showing in modal, need to set dropdownParent. See: https://stackoverflow.com/questions/18487056/select2-doesnt-work-when-embedded-in-a-bootstrap-modal/54100010#54100010
      var modalParent = sel.closest(".modal-content");
      if (modalParent.length == 1) options.dropdownParent = modalParent;

      // If no options initially selected and there is a placeholder, ensure selection is null so placeholder text shows.
      if (sel.has("[selected]").length === 0 && sel.data("placeholder")) {
        sel.val(null);
      }

      sel
        .data("sel2options", options)
        .select2(options)
        .on("onfocus", function (ev) {
          if (evt.target.tagName == "SELECT") $(evt.target).select2("focus");
        })
        .on('select2:open', function (ev) {
          var sel2options = $(ev.target).data("sel2options") || {};
          $('input.select2-search__field').prop('placeholder', sel2options.searchPlaceholder || "");
        });

      setTimeout((sel) => {
        if (sel.next().hasClass("select2")) {
          // Copy the "control-width-" class to the select2 container if it exists.
          const widthClass = sel[0].className.match(/control-width-[^\s]+/);
          if (widthClass != null) {
            sel.next().addClass(widthClass.toString());
          }
        }
      }, 100, sel);
    }

    withinElement.find("[data-appendTo]").each(function (i, e) {
      var $e = $(e);
      var appendToId = "" + $e.data("appendto");
      if (appendToId.length > 0) {
        var $appendTo = $("#" + appendToId);
        if ($appendTo.length == 1) {
          $e.appendTo($appendTo);
        }
      }
      $e.removeAttr("data-appendTo"); // Note, remove this regardless of success above.
    });

    withinElement.find("input[readonly],select[readonly],textarea[readonly]").attr("tabindex", "-1");

    // Table rowlink
    withinElement.find("table[data-rowlink-url] > tbody")
      .on("mousemove", "td", TableRowLink_CellMouseMove)
      .on("keyup", "tr", TableRowLink_RowKeyUp);

    // Tinymce init.
    withinElement.find("textarea.tinymce").each(function (i, e) {
      if (e.id) TinyMCEInit("#" + e.id);
    });

    withinElement.find("a.disabled").click(function (ev) { ev.preventDefault(); });

    withinElement.find(".displayonload").show();

    withinElement.find("a[data-click_spinner_timeout]").click(function () {
      var $e = $(this);
      AddClassTimeout($e, "link-spinner", toDecimalInt($e.data("click_spinner_timeout"), 0));
    });
    withinElement.find("a[data-click_disable_timeout]").click(function () {
      var $e = $(this);
      AddClassTimeout($e, "disabled", toDecimalInt($e.data("click_disable_timeout"), 0));
    });

    withinElement.find(".scoreBars span[title]").css("cursor", "pointer");

    SetJBoxTooltip(withinElement.find("button[title],a[title], .scoreBars span[title]"), { delayOpen: 500 });
    SetJBoxTooltip(withinElement.find("i[data-tooltip], .scoreBars[data-tooltip]"), { delayOpen: 500, getContent: 'data-tooltip' });

    // Set first focus.
    let $firstFocus = withinElement.find("[autofocus]:visible").eq(0);
    if ($firstFocus.length == 0) {
      $firstFocus = withinElement.find("a#tpl_body_top");
    }
    if ($firstFocus.length == 1) {
      if ($firstFocus.offset().top > window.scrollY) {
        $firstFocus.focus();
      } else {
        // top=0 so perhaps not yet in the DOM, try again after a delay.
        setTimeout(function () {
          if ($firstFocus.offset().top > window.scrollY) $firstFocus.focus();
        }, 500);
      }
    }

    function SetJBoxTooltip(jqElement, jBoxOptionObject) {
      if (!isJQuery(jqElement) || jqElement.length == 0 || !isObject(jBoxOptionObject)) return;
      jqElement.jBox('Tooltip', jBoxOptionObject);
    }

    function AddClassTimeout($e, className, timeout) {
      if (!isJQuery($e) || !isString(className) || !isNumber(timeout)) return;
      if (className == "" || timeout <= 0) return;
      $e.addClass(className);
      setTimeout(function () { $e.removeClass(className); }, timeout);
    }

    function TableRowLink_CellMouseMove(ev) {
      if (!ev.target || !ev.currentTarget) return;
      if (ev.target.tagName.toLowerCase() == "a" && !ev.target.classList.contains("nohover")) return;
      var $td = $(ev.currentTarget);
      if ($td.length != 1 || $td[0].tagName.toLowerCase() != "td") return;
      var $tr = $td.parent();
      if (!$tr.is("[data-rowlink-id]") && !$tr.is("[data-rowlink-url]")) return;
      var href = GetTableRowLink_Url($tr);
      if (("" + href).length == 0) return;
      tableRowLinkHelper.data({ "row": $tr });
      $tr.siblings(".hover").removeClass("hover");
      $tr.addClass("hover");
      tableRowLinkHelper
        .attr("href", href)
        .offset({ left: ev.pageX - tableRowLinkHelper.width() / 2, top: ev.pageY - tableRowLinkHelper.height() / 2 });
      if ($tr.closest("table").data("rowlink-newtab") == true) {
        tableRowLinkHelper.attr("target", "_blank");
      }

      // If row contains this class add attribute to open in a new tab when clicked
      // It can vary from one row to another so make sure to remove the attr if the class is not present
      if ($tr.data("rowlink-newtab") == true) {
        tableRowLinkHelper.attr("target", "_blank");
      } else {
        tableRowLinkHelper.removeAttr("target");
      }
    }

    function FormatPartnerDropdownState(selectForm) {

      // Detect if option has separator class
      if ($(selectForm.element).hasClass('dropdown-separator')) {
        // return span with visual separator line
        return $('<span class="dropdown-separator-text w100p">Other Practitioners</span><br/><span class="dropdown-separator-line"></span>');
      }

      const textClass = "select2-state-text";

      let stateText = selectForm.text;
      let stateHtml = "";
      // hack: if indicator char present, put in separate span.
      if (stateText.indexOf("\\") > -1) {
        let textArr = stateText.split('\\');
        stateHtml = `<span class="${textClass}">${textArr[0]}</span><span>${textArr[1]}</span>`;
      } else {
        stateHtml = `<span class="${textClass}">${stateText}</span>`;
      }

      let $state = $('<span class="select2-state-holder"/>').append(stateHtml);

      let imgSrc = $(selectForm.element).data(JS_PartnerDropdown_ImgSrcData);
      if (!isStringNullOrEmpty(imgSrc)) {
        $state.prepend(`<span class="select2-state-avatar"><img src="${imgSrc}" class="img-flag dropdown-avatar" /></span>`);
      }

      return $state;
    }

    function TableRowLink_RowKeyUp(ev) { // go to url on enter
      if (ev.which !== 13) return;
      var $tr = $(ev.target);
      if ($tr.length != 1) return;
      if ($tr[0].tagName.toLowerCase() === "td") $tr = $tr.parent();
      if ($tr[0].tagName.toLowerCase() != "tr") return;
      var url = GetTableRowLink_Url($tr);
      if ("" + url == "") return;
      location.href = url;
    }

    function GetTableRowLink_Url($tr) {
      var $table = $tr.closest("table");
      var url = $tr.data("rowlink-url") || $table.data("rowlink-url"); // prefer row url stub over table url stub.
      if (url == null) return null;
      var rowid = $tr.data("rowlink-id");
      if (rowid == null) return url; // stub without id
      return url + rowid; // append id
    }

    // At end of UpdateUI, add "UpdateUI_Done" class to the target element to indicate it is done.
    // See common_OnUpdateUIDone() to wait for it before doing something.
    if (withinElement) {
      withinElement.addClass(JS_UpdateUI_Done_Class);
    } else {
      $("body").addClass(JS_UpdateUI_Done_Class);
    }
  }
}

function common_GetFormControlAjaxData(jqElement) {

  var ajaxData = {
    dataType: null,
    alternateUrl: null,
    formData: null
  };

  if (isJQuery(jqElement)) {
    ajaxData.dataType = jqElement.data(JS_DataAttrName_AjaxDataType) || null;
    ajaxData.alternateUrl = jqElement.data(JS_DataAttrName_AjaxAlternateUrl) || null;
    ajaxData.formData = jqElement.data(JS_DataAttrName_AjaxFormData) || null;
  }

  return ajaxData;
}

function common_OnUpdateUIDone($updatedElement, callbackFunction, timeoutMs = 4000) {

  if (isHTMLElement($updatedElement)) {
    $updatedElement = $($updatedElement);
  } else if (!isJQuery($updatedElement)) {
    return;
  }

  var startTime = Date.now();

  // Wait up to timeout for the "UpdateUI_Done" class to be added to the element.
  var interval = setInterval(function () {
    var isDone = $updatedElement.hasClass(JS_UpdateUI_Done_Class);
    var isTimeoutExceeded = (Date.now() - startTime > timeoutMs);
    if (isDone || isTimeoutExceeded) {
      clearInterval(interval);
      if (typeof callbackFunction == "function") callbackFunction($updatedElement, isTimeoutExceeded);
      return;
    }
  }, 200);
}

/*
More precise alternative for typeof, e.g:
typeof null       === "object" --> typeOf(null)       === "null"
typeof /a/        === "object" --> typeOf(/a/)        === "regexp"
typeof new Date() === "object" --> typeOf(new Date()) === "date"
typeof new Map()  === "object" --> typeOf(new Map())  === "map"
*/
function typeOf(obj) {
  if (typeof obj === "undefined") return "undefined";
  return {}.toString.call(obj).split(' ')[1].slice(0, -1).toLowerCase();
}

function decodeHTML(encodedString) {
  var textArea = document.createElement('textarea');
  textArea.innerHTML = encodedString;
  return textArea.value;
}

function toString(obj) {
  if (obj == null) return "";
  if (isString(obj)) return obj;
  if ((typeof obj.toString).toLowerCase() == "function") return obj.toString();
  return "";
}

function toRoundedNumber(numOrStr, decimalplaces) {

  var num = NaN;
  if (typeof numOrStr == "number" || typeof numOrStr == "string") num = Number(numOrStr);
  if (isNaN(num)) throw "Parameter 'numOrStr' must be a number or a string.";

  if (typeof decimalplaces == "undefined") {
    decimalplaces = 0; // not provided, default to 0 (integer).
  } else if (typeof decimalplaces != "number" || decimalplaces != Math.round(decimalplaces) || decimalplaces < 0) {
    throw "Parameter 'decimalplaces' must be a positive integer.";
  }

  var placesMultiplier = Number("1e" + decimalplaces); // 1, 10, 100, etc.
  return Math.round((num + Number.EPSILON) * placesMultiplier) / placesMultiplier;
}

function toInt(numOrStr, failValue) {
  return toDecimalInt(numOrStr, failValue);
}

function toDecimalInt(numOrStr, failValue) {
  // Note parseInt does not round. For rounding, use toRoundedNumber().
  if (typeof failValue == "undefined") throw "Parameter 'failValue' is required.";
  if (typeof numOrStr != "string" && typeof numOrStr != "number") return failValue;
  if (typeof numOrStr == "string" && numOrStr.match(/[0-9]+/g) == null) return failValue;
  var result = parseInt(Number(numOrStr), 10);
  return isNaN(result) ? failValue : result; // Ensure parseInt is base 10.
}

function toFloat(obj, failValue) {
  if (typeof obj != "string" && typeof obj != "number") return failValue;
  if (typeof obj == "string") {
    obj = regexTrim(obj);
    if (obj.length == 0) return failValue;
    if (isNaN(obj)) return failValue; // isNaN will reject things like "4x4" which parseInt and parseFloat will return as 4.
  }
  return parseFloat(obj) || failValue;
}

function isGuid(value) {
  const regex = /^[0-9a-f]{8}-[0-9a-f]{4}-[4][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  return regex.test(value);
}

function isNumber(obj) {
  if (typeof obj != "string" && typeof obj != "number") return false;
  if (typeof obj == "string") obj = regexTrim(obj);
  if (isNaN(obj)) return false; // isNaN will reject things like "4x4" which parseInt and parseFloat will return as 4.
  // Note that parseInt does not consider number strings without leading zeros to be numbers,
  // e.g. ".4", so only use parseFloat as a double-check:
  if (isNaN(parseFloat(obj))) return false;
  return true;
}

function isBool(obj) {
  return (typeof obj).toLowerCase() == "boolean";
}

function isString(obj) {
  if (obj == null) return false;
  if ((typeof obj).toLowerCase() != "string") return false;
  if (obj.substr && (typeof obj.substr).toLowerCase() == "function") return true;
  return false;
}

function isStringNullOrEmpty(obj) {
  if (!isString(obj) || obj.length == 0) return true;
  return false;
}

function isDate(obj) {
  if (obj == null) return false;
  if (obj.getMonth && (typeof obj.getMonth).toLowerCase() == "function") return true;
  return false;
}

function isFunction(obj) {
  if (obj == null) return false;
  if ((typeof obj).toLowerCase() != "function") return false;
  return true;
}

function isHTMLElement(obj, tagNameOptional) {
  if (obj == null) return false;
  if ((typeof obj).toLowerCase() != "htmlelement") return false;
  if (!isString(tagNameOptional)) return true;
  return (obj.tagName.toLowerCase() == tagNameOptional);
}

function isJQuery(obj, andSelector) {
  if (!!obj) {
    if (obj instanceof jQuery || (!!obj.jquery && (typeof obj.addClass).toLowerCase() == "function")) {
      if (isStringNullOrEmpty(andSelector)) return true;
      return obj.is(andSelector);
    }
  }
  return false;
}

function isObject(obj) {
  if (obj == null) return false;
  if ((typeof obj).toLowerCase() != "object") return false;
  return true;
}

if (!Array.isArray) {
  Array.isArray = function (arg) {
    return Object.prototype.toString.call(arg).toLowerCase() === '[object array]';
  };
}
function isArray(obj) { return Array.isArray(obj); }

function consoleLog() { // any number of params
  try {
    var sout = "";
    for (var iarg in arguments) {
      var varg = arguments[iarg];
      var targ = (typeof varg).toLowerCase();
      if (varg == null) sout += "[null]";
      else if (targ == "string") sout += "" + varg;
      else if (targ == "number") sout += varg.toString();
      else if (targ == "boolean") sout += (varg ? "<true>" : "<false>");
      else sout += "[" + targ + "]";
    }
    console.log(sout);
  } catch (e) { console.log("consoleLog() error"); }
}

function regexTrim(s) {
  if (typeof s != "string") return s
  return s.replace(/^\s+|\s+$/g, "");
}
function trimRegex(s) { return regexTrim(s); }

function common_jqScrollToMiddle(jqNode) {
  $('html,body').animate({ scrollTop: (jqNode.offset().top - $(window).height() / 2) }, 300);
}

Object.TypeOf = function (obj) { // from: http://javascriptweblog.wordpress.com/2011/08/08/fixing-the-javascript-typeof-operator/
  return ({}).toString.call(obj).match(/\s([a-z|A-Z]+)/)[1].toLowerCase();
}

// Array.indexOf for IE.
if (!Array.prototype.indexOf) {
  Array.prototype.indexOf = function (obj, start) {
    for (var i = (start || 0), j = this.length; i < j; i++) {
      if (this[i] === obj) { return i; }
    } return -1;
  }
}

var app_arrWeekdayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
var app_arrWeekdayNamesAbbrev = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
var app_arrMonthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
var app_arrMonthNamesAbbrev = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function app_FormatDate(dt, sFormat) {

  var oMatches, sMatch, s, s2, iHour, iMatch;

  if (!isDate(dt)) return "";

  iHour = dt.getHours(); // 0-23
  if (/p/i.test(sFormat)) {
    // 12 hour format
    if (iHour == 0) {
      iHour = 12;
    } else if (iHour > 12) {
      iHour = iHour - 12;
    }
  }

  s = "";
  //rx = /(dth)*(d*)(m*)(y*)(h*)(n*)(s*)(p?)([^dmyhnsp]+)*/ig;
  rx = /[dmyhnsp]+|[^dmyhnsp]+/gi;
  while (true) {
    oMatches = rx.exec(sFormat);
    if (oMatches == null) break;
    switch (oMatches[0]) {
      case "d":
        s = s + dt.getDate(); break;
      case "dd":
        s = s + (dt.getDate() < 10 ? "0" : "") + dt.getDate(); break;
      case "ddd":
        s = s + app_arrWeekdayNamesAbbrev[dt.getDay()]; break;
      case "dddd":
        s = s + app_arrWeekdayNames[dt.getDay()]; break;
      case "m":
        s = s + (dt.getMonth() + 1); break;
      case "mm":
        s = s + (dt.getMonth() + 1 < 10 ? "0" : "") + (dt.getMonth() + 1); break;
      case "mmm":
        s = s + app_arrMonthNamesAbbrev[dt.getMonth()]; break;
      case "mmmm":
        s = s + app_arrMonthNames[dt.getMonth()]; break;
      case "yy":
        s = s + dt.getFullYear().toString().substr(2); break;
      case "yyyy":
        s = s + dt.getFullYear(); break;
      case "h":
        s = s + iHour; break;
      case "hh":
        s = s + (iHour < 10 ? "0" : "") + iHour; break;
      case "n":
        s = s + dt.getMinutes(); break;
      case "nn":
        s = s + (dt.getMinutes() < 10 ? "0" : "") + dt.getMinutes(); break;
      case "s":
        s = s + dt.getSeconds(); break;
      case "ss":
        s = s + (dt.getSeconds() < 10 ? "0" : "") + dt.getSeconds(); break;
      case "p":
        if (dt.getHours() < 12)
          s = s + "am";
        else
          s = s + "pm";
        break;
      case "P":
        if (dt.getHours() < 12)
          s = s + "AM";
        else
          s = s + "PM";
        break;
      default:
        s = s + oMatches[0]; break;
    }
  }
  return s;
}

function app_GetCentralSpacePosInString(strIn) {
  // Returns the position of the "most central" space in a string.
  // That is, going outward from the middle, the first space encountered in either direction.
  var midPos = 0, spacePos = 0
  if (strIn == null || strIn == "") return null;
  spacePos = strIn.indexOf(" ")
  if (spacePos < 1 || spacePos > strIn.length - 2) return null;
  midPos = Math.round(strIn.length / 2) - 1;
  spacePos = midPos; // Default = split in half.
  for (var i = 0; i < midPos; i++) {
    if (strIn.substr(midPos - i, 1) == " ") {
      spacePos = midPos - i; // found a space to the left
      break;
    } else if (strIn.substr(midPos + i, 1) == " ") {
      spacePos = midPos + i; // found a space to the right
      break;
    }
  }
  return spacePos;
}

function app_getPageNameFromBody() {
  var s = $("body");
  if (s.length != 1) return "";
  s = s[0].className;
  if (s == null) return "";
  s = s.match(/\bpage-(.+?)\b/);
  if (s == null || s.length != 2) return "";
  return s[1]; // string after "page-"
}

// Refreshes the browser cache of an image, then reloads it anywhere it appears on the page.
function app_ReloadImage(imageUrl) {
  if (typeof fetch != "function" || typeof imageUrl != "string" || imageUrl.indexOf("/") == -1) return;
  fetch(imageUrl, { cache: 'reload', mode: 'no-cors' })
    .then(function (response) {
      $('img[src^="' + imageUrl + '"]').each(function (i, img) {
        img.src = imageUrl + (imageUrl.indexOf("?") == -1 ? "?" : "&") + "imgrnd=" + new Date().getTime()
      });
    });
}

function createCookie(name, value, days) { if (days) { var date = new Date(); date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000)); var expires = "; expires=" + date.toGMTString(); } else var expires = ""; document.cookie = name + "=" + value + expires + "; path=/"; }
function readCookie(name) { var nameEQ = name + "="; var ca = document.cookie.split(';'); for (var i = 0; i < ca.length; i++) { var c = ca[i]; while (c.charAt(0) == ' ') c = c.substring(1, c.length); if (c.indexOf(nameEQ) == 0) return c.substring(nameEQ.length, c.length); } return null; }
function eraseCookie(name) { createCookie(name, "", -1); }

// Navigation

function HistoryPushUrl(url) {
  window.history.pushState('', '', url);
}

function HistoryPushUrlParams(newParamObject, discardExistingQuery) {
  window.history.pushState('', '', AbleJS.Util.PatchQuery({
    url: location.href,
    discardExisting: discardExistingQuery,
    params: newParamObject
  }));
}

function HistoryReplaceUrl(url) {
  window.history.replaceState('', '', url);
}

function HistoryReplaceUrlParams(newParamObject, discardExistingQuery) {
  window.history.replaceState('', '', AbleJS.Util.PatchQuery({
    url: location.href,
    discardExisting: discardExistingQuery,
    params: newParamObject
  }));
}

function LocationReplace(PageUrlWithoutSvId) {
  location.replace(PageUrlWithoutSvId);
}

function LocationReplaceWithSvId(PageUrlWithoutSvId, SvId) {
  location.replace(PageUrlWithoutSvId + "?" + app_urlKey_SvId + "=" + SvId);
}

// TODO Move all global scope functions etc. in here:
var AbleJS = AbleJS || {};

(function ($) {

  // Common vars here.
  ThisNS = AbleJS; // Use ThisNS in case namespace name changes.

  ThisNS.Stripe = (function () {

    // Call GetStripe() early to kick off loading stripe.js.
    // When wanting to use it, do GetStripe(clientSecret).then(stripe => doIt(stripe));
    let stripeJsPromise;
    let stripeInstancePromise;

    return {

      GetStripe: function (clientKey) {

        if (typeof clientKey !== 'string') {
          return LoadStripeJsOnce(); // no key passed, just preload the script.
        }

        if (!stripeInstancePromise) {
          stripeInstancePromise = (async () => {
            await LoadStripeJsOnce();
            return Stripe(clientKey);
          })();
        }
        return stripeInstancePromise;

        function LoadStripeJsOnce() {
          if (!stripeJsPromise) {
            stripeJsPromise = new Promise((resolve, reject) => {
              const script = document.createElement('script');
              script.src = 'https://js.stripe.com/v3/';
              script.async = true;
              script.onload = resolve;
              script.onerror = reject;
              document.head.appendChild(script);
            });
          }
          return stripeJsPromise;
        }
      }

    };

  })();

  ThisNS.Util = (function () {

    // Private vars here.

    return {

      // Patches a url or separate query (e.g. form data string) with new parameters & values.
      // e.g. AbleJS.Util.PatchQuery({url: "http://www.something.com?x=1", params: { x: 2, y: 'foo' }})
      //      returns: "http://www.something.com?x=2&y=foo".
      // If discardExisting=true, query will contain only the new values.
      // Pass null to ensure a key isn't in the result. e.g. { x: null } will ensure 'x' isn't in the result.
      PatchQuery: function (options) {
        // All options:
        //    PatchQuery({
        //      url: string, <- may also include query after "?"
        //      query: string, <- can do a query alone instead of a url
        //      discardExisting: bool,
        //      discardNullOrEmpty: bool,
        //      params: {
        //        param1: value1,
        //        ...
        //     }
        //    })

        if (options == null || !isObject(options)) throw "Options missing.";

        options = {
          url: isString(options.url) ? options.url : "",
          query: isString(options.query) ? options.query : "",
          discardExisting: options.discardExisting === true,
          discardNullOrEmpty: options.discardNullOrEmpty === true,
          params: isObject(options.params) ? options.params : {}
        };

        // If url includes query, separate and combine later.
        let urlBody = options.url;
        let urlQuery = "";
        let urlHash = "";

        if (!isStringNullOrEmpty(urlBody)) {
          if (urlBody.indexOf("#") >= 0) {
            let arr = urlBody.split("#");
            urlBody = arr[0];
            urlHash = arr[1];
          }
          if (urlBody.indexOf("?") >= 0) {
            let arr = urlBody.split("?");
            urlBody = arr[0];
            if (options.discardExisting) arr[1] = "";
            urlQuery = new URLSearchParams(arr[1]);
          }
        }

        if (!isStringNullOrEmpty(options.query)) {
          urlQuery = new URLSearchParams({
            ...Object.fromEntries(urlQuery),
            ...Object.fromEntries(new URLSearchParams(options.query))
          });
        }

        urlQuery = new URLSearchParams({
          ...Object.fromEntries(urlQuery),
          ...options.params
        });

        if (options.discardNullOrEmpty) {
          const entriesCopy = [...urlQuery.entries()];
          for (const [key, value] of entriesCopy) {
            if (value == "" || value == "null" || value == "undefined") urlQuery.delete(key);
          }
        }

        urlQuery = urlQuery.toString();

        let result = urlBody
        if (!isStringNullOrEmpty(urlQuery)) {
          if (!isStringNullOrEmpty(result)) result += "?";
          result += urlQuery;
        }
        if (!isStringNullOrEmpty(urlHash)) {
          if (!isStringNullOrEmpty(result)) result += "#";
          result += urlHash;
        }

        return result;
      },

      HtmlEncode: function (str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
      },

      // Converts url parameter string into a javascript object.
      // e.g. "mode=add&productid=5" to { mode: 'add', productid: '5' }
      // Note do not use this to convert $form.serialize() (bad handling of "+"), use Form.ToObject($form, true) instead.
      UrlEncodedParamStringToObject: function (paramString, decodeValues) {
        decodeValues = decodeValues === true ? true : false; // default to false
        return JSON.parse('{"' + paramString.replace(/&/g, '","').replace(/=/g, '":"') + '"}',
          function (key, val) { return (key == undefined || key == null || key === "" || !decodeValues) ? val : decodeURIComponent(val) });
      },

      UnEncodedParamStringToObject: function (paramString, urlEncodeValues) {
        // Note: paramString must NOT contain any encoded values.
        //       paramString is assumed to be unencoded, plain text with "&" and "=" separating keys and values.
        if (!isString(paramString) || paramString == "") return {};
        urlEncodeValues = urlEncodeValues === true ? true : false; // default to false
        var outObj = {};
        paramArray = paramString.split("&");
        for (var ip in paramArray) {
          var param = paramArray[ip];
          if (param.match(/^[^=]+=[^=]*$/)) { // single "=" in string but not at beginning
            var kvp = param.split("=");
            outObj[kvp[0]] = urlEncodeValues ? encodeURIComponent(kvp[1]) : kvp[1];
          }
        }
        return outObj;
      },

      ObjectToParamString: function (dataObj, urlDecodeValues) {
        urlDecodeValues = urlDecodeValues === true ? true : false; // default to false
        rtnString = "";
        $.each(dataObj, function (key, val) {
          if (rtnString.length > 0) rtnString += "&";
          if (!val || val == null) val = "";
          else if (urlDecodeValues) val = decodeURIComponent(val).replace(/[&=]/g, ""); // remove these from values to preserve paramstring function.
          rtnString += key + '=' + val;
        });
        return rtnString;
      },

      FormatCurrency: function (amount, currency = 'USD', locale = navigator.language) {
        return new Intl.NumberFormat(locale, {
          style: 'currency',
          currency: currency,
          currencyDisplay: 'narrowSymbol'
        }).format(amount);
      },
    };
  })();

  ThisNS.Form = (function () {

    // Private vars here.

    return {

      PostImages: ($form) => {

        if (!isJQuery($form) || $form.prop("tagName").toLowerCase() != "form") return;

        let $uploadImages = $form.find("img[data-ajax-action]");
        if ($uploadImages.length > 0) {
          $uploadImages.each((i, e) => {

            let $img = $(e);

            if ($img.data("blob") == null) return; // No change.

            let formData = new FormData();
            formData.append("image", $img.data("blob"));

            let headers = {};
            headers[HttpHeader_AjaxAction] = $img.attr("data-ajax-action");

            return new Promise((resolve, reject) => {
              $.ajax({
                method: "post",
                headers: headers,
                url: location.href,
                data: formData,
                timeout: 30000,
                processData: false,
                contentType: false,
                success: (response) => {
                  resolve(response);
                },
                error: (response) => {
                  reject(response);
                }
              });
            });
          });
        }
      },

      ToObject: function (jqForm, urlEncodeValues) {
        // Note if serializeArray() method doesn't work with some forms, try: jqForm.serialize().replace("+", "%20")
        // This is because jquery serialize() turns spaces into "+" instead of "%20" which causes encoding confusion.

        urlEncodeValues = urlEncodeValues === true ? true : false; // defaults to false.
        var outObj = {};
        var arr = jqForm.serializeArray();
        if (isArray(arr)) {
          for (var i in arr) {
            if (arr[i].name && arr[i].value) {
              try {
                if (outObj[arr[i].name]) outObj[arr[i].name] += ",";
                else outObj[arr[i].name] = "";
                outObj[arr[i].name] += (urlEncodeValues ? encodeURIComponent(arr[i].value) : arr[i].value)
              } catch (ex) {
              }
            }
          }
        }
        return outObj;
      }

    };
  })();

  ThisNS.Logging = (function () {

    // Private vars here.

    return {

      ResponseLog: function (log) {
        if (log == null || !isArray(log) || log.length == 0) return;
        for (logItem of log) {
          if (console.serverlog) {
            console.serverlog(logItem);
          } else {
            console.log(logItem);
          }
        }
      }

    };
  })();

  ThisNS.Tabulator = (function () {

    // Private vars here.

    return {

      ProjectNameFilter: (filterValue, rowValue, rowData, filterParams) => {
        if (filterValue == null || filterValue === "") return true;
        return String("" + rowData.JobNumber + rowData.RowItemName)
          .toLowerCase()
          .includes(String(filterValue).toLowerCase());
      },

      StatusFilter: (filterValue, rowValue, rowData, filterParams) => {
        if (isStringNullOrEmpty(filterValue) || filterValue == "All") return true;
        if (isStringNullOrEmpty(rowValue)) return false;
        // const childField = filterParams?.childField;
        // if (Array.isArray(rowData?.[childField])) return true; // Always show parent rows.
        return rowValue === filterValue;
      },

      ScoreColumnDef: {
        headerHozAlign: "center",
        hozAlign: "center",
        sorter: "number",
        formatter: function (cell, formatterParams, onRendered) {
          const value = cell.getValue();
          if (value == null) return "";
          return `<div class="">${value.toFixed(1)}</div>`;
        },
      },

      MoneyColumnDef: {
        sorter: "number",
        formatter: "money",
        cssClass: "col-format-money",
        hozAlign: "right",
        formatterParams: { symbol: "$" }
      },

      UserAvatarColumnDef: {
        width: 200,
        minWidth: 200,
        hozAlign: "left",
      },

      UserAvatarHtml: (firstName, lastName, photoFilename) => {
        if (isStringNullOrEmpty(firstName) || isStringNullOrEmpty(lastName)) return "";
        let photoPath = JS_UserPhotoPathTemplate.replace(JS_UserPhotoPathTemplateReplaceName, photoFilename);
        return `
          <div class="user-avatar-horizontal nohover">
            <div class="user-avatar"><img src="${AbleJS.Util.HtmlEncode(photoPath)}"></div>
            <div class="user-details"><span class="user-name font-weight-bold">${AbleJS.Util.HtmlEncode(firstName + " " + lastName)}</span></div>
          </div>`;
      },

      GetProgressBar: (currentValue, maxValue, options) => {

        options = {
          ...{
            isMoney: false,
            blankIfMaxZero: false,
            unitText: '',   // e,g, "widget"
            unitsText: '',  // e.g. "widgets"
          },
          ...options
        };

        maxValue = toFloat(maxValue, 0);
        if (maxValue < 0) maxValue = 0;
        if (options.blankIfMaxZero === true && maxValue == 0) return '';

        currentValue = toFloat(currentValue, 0);
        if (currentValue < 0) currentValue = 0;
        if (currentValue > maxValue) currentValue = maxValue;

        let percent = maxValue <= 0 ? 0 : Math.round(currentValue * 100 / maxValue);

        if (options.isMoney === true) {
          currentValue = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(currentValue);
          maxValue = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(maxValue);
        }
        return `
          <div class="progress-holder">
            <div class="progress-bar"><div class="progress" data-percent="${percent}" style="width: ${percent}%"></div></div>
            <div class="progress-label">${currentValue} / ${maxValue}${AbleJS.Util.HtmlEncode(maxValue === 1 ? options.unitText : options.unitsText)}</div>
          </div>`;
      }

    };
  })();

})(jQuery);

