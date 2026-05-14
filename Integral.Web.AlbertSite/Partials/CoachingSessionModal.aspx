<%@ Page Language="C#" AutoEventWireup="true" CodeFile="CoachingSessionModal.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.CoachingSessionModal" %>

<%@ Import Namespace="Integral.Web" %>

<% if (SessionInfo != null) { %>

  <div class="session-item session-upcoming">

    <form id="SessionListForm" class="form-horizontal label-narrow" method="post" action="#" onsubmit="return false;">

      <input type="hidden" name="sessionid" value="<%= SessionInfo.SessionId %>" />
      <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="" />

      <%= WebHelper.GetInputDateRow("Session Date:", FormFields.ApptDateUtc,
            IsNewSession ? null : SessionInfo?.GetApptDateInCoachTZ(), 2, 10, "", IsReadOnly) %>

      <%= WebHelper.GetTimePickerRow("Session Time:", FormFields.SessionTime,
            IsNewSession ? null : SessionInfo?.GetApptDateInCoachTZ(), "(" + CoachInfo.TimeZoneIdIana + ")", IsReadOnly) %>

      <%= WebHelper.GetTextInput("Duration (mins):", FormFields.DurationMins,
            SessionInfo.DurationMins.ToString(),
            2, "", IsReadOnly) %>

      <%= GetStatusOptions("Session Status:") %>

      <% if (CanViewCoachingNotes) { %>
        <%= WebHelper.GetRichTextArea("Coach Notes:", FormFields.CoachNotes, 2, 10, SessionInfo.CoachNotes, "", !CanEditNotes) %>
      <% } %>

      <div class="modal-footer-buttons">
        <div><button type="button" class="btn btn-secondary btnClose">Cancel</button></div>
        <div>
          <% if (IsNewSession) { %>
            <button type="button" class="btn btn-primary btnUpdateSession" data-action="<%= AjaxAction.Update %>">Add Booking</button>
          <% } else { %>
            <% if (CanDelete) { %>
              <button type="button" class="btn btn-danger btnUpdateSession" data-action="<%= AjaxAction.Delete %>">Delete Booking</button>
            <% } %>
            <button type="button" class="btn btn-primary btnUpdateSession ml30" data-action="<%= AjaxAction.Update %>">Update Booking</button>
          <% } %>
        </div>
      </div>

    </form>
  </div>

<% } %>

<% var scriptId = new Random((int)DateTime.Now.Ticks).Next(100000); %>

<script id="<%= scriptId %>">

  // Closure for Coaching Sessions Tab.
  (function ($) {

    var dialogRef = null;
    var modalBody = null;
    var modalForm = null;

    $(document).ready(function () {

      // Find modal objects based on the random ID given to this script tag.
      var scripts = document.getElementsByTagName("script");
      for (var i = scripts.length - 1; i >= 0; i--) {
        if (scripts[i].id == <%= scriptId %>) {
          var thisScript = $(scripts[i]);
          var modalDialog = thisScript.closest(".modal-dialog");
          dialogRef = modalDialog.data("<%= WebHelper.DataAttrName.DialogRef %>");
          modalBody = modalDialog.find(".modal-body");
          modalForm = modalBody.find("form");
          break;
        }
      }

      $(".btnUpdateSession").click(UpdateSession);
      $(".btnClose").click(function () { dialogRef.close(); });

      <% if (IsReadOnly) { %>
        <% if (CanEditNotes) { %>
          $("textarea[name=coachnotes]").focus();
        <% } else { %>
          $(".btnCancel").focus();
        <% } %>
      <% } else if (IsNewSession) { %>
        $("#SessionListForm input:not([type=hidden])").eq(0).focus();
      <% } %>

    });

    function UpdateSession(evt) {

      var thisBtn = $(evt.target);
      var ajaxAction = thisBtn.data("action");
      if (ajaxAction == "<%= AjaxAction.Delete %>") {
        common_ConfirmDialog("Delete Session?", "Please confirm deleting this session.", null, function (e) {
          if (e === true) ContinueUpdate(thisBtn, ajaxAction);
        })
        return;
      } else {
        ContinueUpdate(thisBtn, ajaxAction);
      }
    }

    function ContinueUpdate(thisBtn, ajaxAction) {

      AjaxSubmit({
        form: modalForm,
        action: ajaxAction,
        url: "<%= PathHelper.Partials.CoachingSessionModal(CoacheeInfo.CoacheeId, WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoachingSessionId)) %>",
        onSuccess: function (jqXHR, data) { },
        onFail: function (jqXHR, data) { },
        onError: function (jqXHR, textStatus, errorThrown) { },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
      });
    }

  })(jQuery);

</script>
