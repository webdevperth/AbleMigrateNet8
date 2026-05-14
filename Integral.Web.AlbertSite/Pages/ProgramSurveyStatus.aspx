<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProgramSurveyStatus.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProgramSurveyStatus"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Integrations" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (NoSurveyVisible) { %>

    <%= WebHelper.GetEmptyStatePageHtml(
      title: "Surveys",
      description: $"No surveys yet. {(CanSendSurvey ? "Send the first one!" : "")}",
      addActionHtml: CanSendSurvey,
      actionButtonText: "Send Survey",
      actionButtonPath: PathHelper.Pages.ProgramSendSurvey()) %>

  <% } %>

  <% if (SurveyInfoVisible) { %>

    <% if (CanSendSurvey) { %>
      <a class="btn btn-primary floatright mt10" href="<%= PathHelper.Pages.ProgramSendSurvey() %>">Send New Survey</a>
    <% } %>

    <div class="row-info">
      <label>Survey to View:</label>
      <div class="form-holder">
        <select size="1" id="selSurveyList" class="form-control" style="width:70%">
          <% foreach (var survey in SurveyList) { %>
            <option <%= GetSurveyListOptionSelected(survey) %> value="<%= GetSurveyListOptionValue(survey) %>"><%= GetSurveyListOptionText(survey) %></option>
          <% } %>
        </select>
      </div>
    </div>

    <div class="row-info">
      <label>Survey Notes:</label>
      <div class="form-holder infoLabel">
        <%= FoundSurveyInfo == null ? "" : FoundSurveyInfo.SurveyName %>
      </div>
    </div>

    <% if (FoundSurveyInfo != null) { %>

      <% if (FoundSurveyInfo.GetSendDateUtc() > DateTime.UtcNow) { %>
        <div class="row-info">
          <label>Scheduled to Send:</label>
          <div class="form-holder">
            <%= WebHelper.DisplayDate(SessionHelper.UtcToUserTime(FoundSurveyInfo.GetSendDateUtc())) %>
          </div>
        </div>
      <% } else { %>
        <div class="row-info">
          <label>Sent On:</label>
          <div class="form-holder">
            <%=WebHelper.DisplayDate(SessionHelper.UtcToUserTime(FoundSurveyInfo.GetSendDateUtc()))%>
          </div>
        </div>
      <% } %>

      <% if (!FoundSurveyInfo.IsRatersOnly) { %>
        <div class="row-info">
          <label>Close Date (Self):</label>
          <div class="form-holder">
            <span id="txtCloseDateSelf"><%= GetCloseDateSelf() %></span>
            <% if (CanChangeCloseDates) { %>
              <button type="button" class="btn btn-sm btn-primary btnChange" id="btnChangeCloseDateSelf">Change</button>
            <% } %>
          </div>
        </div>
      <% } %>

      <% if (FoundSurveyInfo.FeedbackOption != DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters) { %>
        <div class="row-info">
          <label>Close Date (Raters):</label>
          <div class="form-holder">
            <span id="txtCloseDateRaters"><%= GetCloseDateRaters() %></span>
            <% if (CanChangeCloseDates) { %>
              <button type="button" class="btn btn-sm btn-primary btnChange" id="btnChangeCloseDateRaters">Change</button>
            <% } %>
          </div>
        </div>
      <% } %>

      <% if (SessionHelper.IsUserRoleAdmin) { %>
        <div class="row-info">
          <a href="<%= PathHelper.JarvisPages.ParticipantsUrl(FoundSurveyInfo.SurveyId, FoundSurveyInfo.IntakeNumber) %>" target="_blank">Edit Survey in Jarvis</a>
        </div>
      <% } %>

    <% } %>

    <% if (FoundSurveyInfo != null) { %>

      <div class="row-info">
        <div class="surveyContent">

          <ul class="nav nav-tabs">
            <li role="presentation" class="active">
              <a class="nav-link" id="parts-tab" data-toggle="tab" href="#parts-panel" role="tab" aria-controls="parts-panel" aria-selected="true">Participants</a>
            </li>
          </ul>

          <div class="fade in active table-responsive" id="parts-panel" role="tabpanel" aria-labelledby="parts-tab">
            <table class="table tblForm table-rowlink" <%= CanViewParticipants ? $"data-rowlink-url={GetParticipantRowLinkUrl()}" : "" %>>
              <thead>
                <tr>
                  <th class="type-fullname">Name</th>
                  <th class="type-email">Email</th>
                  <th class="type-date">Completed</th>
                  <th class="type-date">Last Reminder</th>
                  <th class="type-raters">Raters</th>
                  <th class="type-date">Report Sent</th>
                </tr>
              </thead>
              <tbody>
                <% if (ParticipantList?.Participants != null) foreach (var participant in ParticipantList.Participants) { %>
                  <tr data-rowlink-id="<%= participant.CoacheeId %>&svuid=<%= participant.SurveyUID %>-<%= participant.PartUID %>">
                    <td class="type-fullname"><%= GetPartName(participant) %></td>
                    <td class="type-email"><%= participant.Email.HTMLEncode() %></td>
                    <td class="type-date"><%=
                        participant.CompletedUTC == null
                        ? ("No&nbsp; <a target=\"_blank\" class=\"survey-status-view-survey\" title=\"Go To Survey\" href=\""
                        + PathHelper.Pages.Survey(FoundSurveyInfo, participant.PartUID) + "\">" + WebHelper.Icon.Survey + "</a>")
                        : ("Yes " + WebHelper.Icon.CheckCircle.AddClass("icon-color-green")) %></td>
                    <td class="type-date"><%=
                        participant.LastReminderUTC != null
                        ? WebHelper.DisplayDate_UtcToUserTime(participant.LastReminderUTC)
                        : WebHelper.DisplayDate_UtcToUserTime(participant.CreatedUTC) %></td>
                    <td class="type-raters"><%= participant.RatersCompleted + " / " + participant.RatersNotDeclined %></td>
                    <td class="type-date"><%= WebHelper.DisplayDate_UtcToUserTime(participant.ReportSentUtc, "-") %></td>
                  </tr>
                <% } %>
              </tbody>
            </table>
          </div>

          <div data-appendTo="responses-panel"></div>

        </div>
      </div>
    <% } %>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var incompleteParts = parseInt("<%= incompleteParts %>");
      var btnSendReminders, btnChangeCloseDateSelf, btnChangeCloseDateRaters, selSurveyList;
      var btnSendReports, modalSendReports, dialogBtnSend, formSendReports, chkSendWebReport, chkSendPDFReport;

      $(document).ready(function() {

        selSurveyList = $("#selSurveyList");
        btnSendReminders = $("#btnSendReminders");
        btnChangeCloseDateSelf = $("#btnChangeCloseDateSelf");
        btnChangeCloseDateRaters = $("#btnChangeCloseDateRaters");
        btnSendReports = $("#btnSendReports");

        modalSendReports = $("#modalSendReports");
        dialogBtnSend = $("#dialogBtnSend");
        formSendReports = $("#formSendReports");
        chkSendWebReport = $("#chkSendWebReport");
        chkSendPDFReport = $("#chkSendPDFReport");

        selSurveyList.change(function (e) {
          document.location.href = AbleJS.Util.PatchQuery({
            url: document.location.href,
            params: { "<%= PathHelper.AbleUrlKeys.SurveyUId %>": selSurveyList.val() }
          });
        });

        if (incompleteParts > 0 && btnSendReminders.length == 1) {
          btnSendReminders.text("Send " + incompleteParts + " Reminder" + (incompleteParts > 1 ? "s" : ""));
          btnSendReminders.show();
          btnSendReminders.click(SendReminders);
        }

        btnChangeCloseDateSelf
          .datepicker({ format: "dd/mm/yyyy", title: "Select Self Close Date", autoclose: true })
          .datepicker("update", "<%= GetDatePickerCloseDateSelf() %>")
          .on("changeDate", function (e) {
            UpdateCloseDate(true, e.date);
          })
          .click(function (e) {
            $(this).datepicker("show");
          });

        btnChangeCloseDateRaters
          .datepicker({ format: "dd/mm/yyyy", title: "Select Rater Close Date", autoclose: true })
          .datepicker("update", "<%= GetDatePickerCloseDateRaters() %>")
          .on("changeDate", function (e) {
            UpdateCloseDate(false, e.date);
          })
          .click(function (e) {
            $(this).datepicker("show");
          });

        modalSendReports.on('shown.bs.modal', function () { $('#myInput').focus() });
        dialogBtnSend.click(SendReports);

      }); // ready.

      function SendReports() {
        AjaxSubmit({ form: formSendReports });
      }

      function UpdateCloseDate(isSelf, closeDate) {

        var action, btnChangeCloseDate, formDateKey

        if (isSelf) {
          action = "<%= AjaxAction.UpdateCloseDateSelf %>";
          formDateKey = "<%= FormKeys.CloseDateSelf %>";
          btnChangeCloseDate = btnChangeCloseDateSelf;
        } else {
          action = "<%= AjaxAction.UpdateCloseDateRaters %>";
          formDateKey = "<%= FormKeys.CloseDateRaters %>";
          btnChangeCloseDate = btnChangeCloseDateRaters;
        }

        var submitData = {};
        submitData[formDateKey] = moment(closeDate).format("<%= WebHelper.DATE_OUTPUT_FORMAT_MOMENTJS %>").toString();

        AjaxSubmit({
          action: action,
          data: submitData
        });
      }

      function SendReminders() {

        if (incompleteParts == 0) return;

        AjaxSubmit({
          action: "SendReminders",
          onSuccess: function () {
            common_SuccessDialog("Reminders have been sent.", function () {
              location.replace(location.href);
            });
          }
        });
      }

    })(jQuery);
  </script>

</asp:Content>
