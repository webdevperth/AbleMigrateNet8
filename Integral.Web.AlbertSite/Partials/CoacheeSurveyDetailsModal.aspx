  <%@ Page Language="C#" AutoEventWireup="true" CodeFile="CoacheeSurveyDetailsModal.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.CoacheeSurveyDetailsModal" %>

<%@ Import Namespace="Integral.Web" %>

<% if (Content_ShowNoSurvey) { %>

  No information found for this survey.

<% } else { %>

  <div class="coacheeSurvey form-horizontal half-height">

    <% if (SurveyInfo != null) { %>

      <% if (SurveyInfo.SurveyTypeCode == ConfigHelper.SurveyTypeCodes.IOS) { %>

        <% ShowIOSInfo(); %>

      <% } else { %>

        <% ShowSurveyInfo(); %>

      <% } %>

      <% void ShowIOSInfo() { %>

        <div class="alert alert-primary" role="alert">IOS Survey results are anonymous and reported only in aggregate.</div>

        <%= WebHelper.GetTextDisplayRow("Survey Name:", 8, $"{SurveyInfo.SurveyName.HTMLEncode()}") %>

        <% if (SurveyInfo.GetSendDateUtc() > DateTime.UtcNow) { %>
          <%= WebHelper.GetTextDisplayRow("Scheduled to Send:", 8, WebHelper.DisplayDate(SessionHelper.UtcToUserTime(SurveyInfo.GetSendDateUtc()))) %>
        <% } else { %>
          <%= WebHelper.GetTextDisplayRow("Sent On:", 8, WebHelper.DisplayDate(SessionHelper.UtcToUserTime(SurveyInfo.GetSendDateUtc()))) %>
        <% } %>

        <% if (!SurveyInfo.IsRatersOnly) { %>
          <%= WebHelper.GetTextDisplayRow(
              "Close Date:", 8,
              WebHelper.GetSpan("txtCloseDateSelf", "", GetCloseDateSelf()) +
              (!CanChangeCloseDates
                ? ""
                : WebHelper.GetButton("Change", "btnChangeCloseDateSelf", false, WebHelper.ButtonStyle.Primary, WebHelper.ButtonSize.XSmall, "btnChange ml10"))
          ) %>
        <% } %>

        <% if (CoacheePartInfo != null) { %>
          <%= WebHelper.GetTextDisplayRow("Participant Completed:", 8, "<b>" + WebHelper.DisplayDate(SessionHelper.UtcToUserTime(CoacheePartInfo.CompletedUTC), "Not completed.") + "</b>") %>
        <% } %>

        <div class="mt20 flex gap15 flex-align-center">
          <%
            if (CanEditSurveyInJarvis) {
              // Edit in Jarvis button.
              Response.Write(WebHelper.GetLink(new WebHelper.LinkInfo() {
                InnerHtml = "Admin: Edit in Jarvis",
                Href = PathHelper.JarvisPages.OrgParticipantsUrl(SurveyInfo.SurveyId, SurveyInfo.IntakeNumber),
                NewTab = true,
                ButtonStyle = WebHelper.ButtonStyle.Primary,
                ButtonSize = WebHelper.ButtonSize.Small
              }));
            }
            if (SessionHelper.AppAccess.Surveys.CanViewOrgReportButtons()) {
              // Org report button - disabled if report can't be viewed yet.
              bool orgReportEnabled = CanViewSurveyReports && ReportsAvailable;
              Response.Write(WebHelper.GetLink(new WebHelper.LinkInfo() {
                InnerHtml = "Client IOS Report",
                Href = !orgReportEnabled ? "#" : PathHelper.Reports.OrganisationIOSReports(SurveyInfo),
                ButtonStyle = WebHelper.ButtonStyle.Primary,
                ButtonSize = WebHelper.ButtonSize.Small,
                Disabled = !orgReportEnabled
              }));
            }
          %>
        </div>

      <% } %>

      <% void ShowSurveyInfo() { %>

        <% if (SessionHelper.IsUserRoleLeader && SurveyInfo.FoundParticipantBrief?.CanLeaderView360Report == false) { %>
          <div class="alert alert-primary" role="alert">Survey results will be available once reviewed.</div>
        <% } %>

        <%= WebHelper.GetTextDisplayRow("Survey Name:", 8, $"{SurveyInfo.SurveyName.HTMLEncode()}") %>

        <% if (SurveyInfo.GetSendDateUtc() > DateTime.UtcNow) { %>
          <%= WebHelper.GetTextDisplayRow("Scheduled to Send:", 8, WebHelper.DisplayDate(SessionHelper.UtcToUserTime(SurveyInfo.GetSendDateUtc()))) %>
        <% } else { %>
          <%= WebHelper.GetTextDisplayRow("Sent On:", 8, WebHelper.DisplayDate(SessionHelper.UtcToUserTime(SurveyInfo.GetSendDateUtc()))) %>
        <% } %>

        <% if (!SurveyInfo.IsRatersOnly) { %>
          <%= WebHelper.GetTextDisplayRow(
              "Close Date (Self):", 8,
              WebHelper.GetSpan("txtCloseDateSelf", "", GetCloseDateSelf()) +
              (!CanChangeCloseDates
                ? ""
                : WebHelper.GetButton("Change", "btnChangeCloseDateSelf", false, WebHelper.ButtonStyle.Primary, WebHelper.ButtonSize.XSmall, "btnChange ml10"))
          ) %>
        <% } %>

        <% if (!SurveyInfo.IsSelfOnly) { %>
          <%= WebHelper.GetTextDisplayRow(
              "Close Date (Raters):", 8,
              WebHelper.GetSpan("txtCloseDateRaters", "", GetCloseDateRaters()) +
              (!CanChangeCloseDates
                ? ""
                : WebHelper.GetButton("Change", "btnChangeCloseDateRaters", false, WebHelper.ButtonStyle.Primary, WebHelper.ButtonSize.XSmall, "btnChange ml10"))
          ) %>
        <% } %>

        <% if (CoacheePartInfo != null) { %>
          <%= WebHelper.GetTextDisplayRow("Participant Completed:", 8, "<b>" + WebHelper.DisplayDate(SessionHelper.UtcToUserTime(CoacheePartInfo.CompletedUTC), "Not completed.") + "</b>") %>
          <%= WebHelper.GetTextDisplayRow("Raters Completed:", 8, CoacheePartInfo.RatersCompleted + " of " + CoacheePartInfo.RatersNotDeclined) %>
          <%= WebHelper.GetTextDisplayRow("Report Sent:", WebHelper.DisplayDate_UtcToUserTime(CoacheePartInfo.ReportSentUtc, "-")) %>
        <% } %>

        <div class="mt20 flex gap15 flex-align-center">
          <% if (ReportsAvailable) { %>
            <% if (SessionHelper.AppAccess.Surveys.CanViewReportButtons()) { %>
              <div class="flex-inline">
                <button class="btn btn-sm btn-primary btnSendReports <%= CanEmailSurveyReports ? "" : "disabled" %>" id="btnSendReports">Email Reports</button>
                <%= CanEmailSurveyReports ? "" : WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, "The report will be available and able to be sent once it has been closed.", "", "mr15 mt5") %>
              </div>
              <% if (SurveyInfo.FoundParticipantBrief?.IsReportAvailable_Online == true) { %>
                <a class="btn btn-sm btn-primary" href="<%= PathHelper.Reports.CoacheeSurvey(CoacheeInfo, SurveyInfo) %>">Coachee Online Report</a>
              <% } %>
            <% } %>
            <% if (SurveyInfo.FoundParticipantBrief?.IsReportAvailable_PDF == true) { %>
              <a class="btn btn-sm btn-primary" href="<%= PathHelper.Reports.ParticipantPDFReport(SurveyInfo.ReportType, SurveyInfo.SurveyId, SurveyInfo.FoundParticipantBrief.PartUniqueId) %>" target="_blank">Coachee PDF Report</a>
            <% } %>
          <% } %>
          <% if (CanEditSurveyInJarvis) { %>
            <a class="btn btn-sm btn-primary" href="<%= PathHelper.JarvisPages.ParticipantsUrl(SurveyInfo.SurveyId, SurveyInfo.IntakeNumber) %>" target="_blank">Admin: Edit in Jarvis</a>
          <% } %>
          <% if (CanInviteRaters) { %>
            <a class="btn btn-sm btn-primary" href="<%= PathHelper.Pages.ParticipantRaters(SurveyInfo.SurveyUID, SurveyInfo.FoundParticipantBrief.PartUniqueId) %>" target="_blank">Invite Raters</a>
          <% } %>
        </div>

        <div class="row-info">
          <div class="surveyContent">
            <ul class="nav nav-tabs">
              <% if (CanViewRaters) { %>
                <li role="presentation" class="<%= ActiveTab == ActiveTabEnum.Responses ? "" : "active" %>">
                  <a class="nav-link" id="raters-tab" data-toggle="tab" href="#raters-panel" role="tab" aria-controls="raters-panel" aria-selected="true">Raters (if applicable)</a>
                </li>
              <% } %>
              <li role="presentation" class="<%= ActiveTab == ActiveTabEnum.Responses ? "active" : "" %>">
                <a class="nav-link" id="responses-tab" data-toggle="tab" href="#responses-panel" role="tab" aria-controls="responses-panel" aria-selected="false">Responses</a>
              </li>
            </ul>

            <div class="tab-content" id="surveyDataTContent">
              <% if (CanViewRaters) { %>
                <div class="tab-pane fade in <%= ActiveTab == ActiveTabEnum.Responses ? "" : "active" %>" id="raters-panel" role="tabpanel" aria-labelledby="raters-tab">
                  <% if (RaterList.Participants.Count == 0 || RaterList.Participants == null) { %>

                    <%= WebHelper.GetNoRecordsBadge() %>

                  <%  } else { %>
                    <div class="table-responsive width-auto">
                    <table class="tblRaters table">
                      <thead>
                        <tr>
                          <th class="type-fullname">Name</th>
                          <th class="type-email">Email</th>
                          <th class="type-rater-completed">Completed</th>
                          <th class="type-date">Last Reminder</th>
                        </tr>
                      </thead>
                      <tbody>
                        <% foreach (var raterListItem in RaterList.Participants) { %>
                          <ItemTemplate>
                            <tr>
                              <td class="type-fullname"><%= raterListItem.FullName %></td>
                              <td class="type-email"><%= raterListItem.Email %></td>
                              <td class="type-rater-completed"><%= raterListItem.CompletedUTC == null
                                      ? (@"No&nbsp; <a class=""survey-status-view-survey"" target=""_blank"" href=""" + PathHelper.Pages.Survey(SurveyInfo, raterListItem.PartUID) + @""">" + WebHelper.Icon.Survey + "</a>")
                                      : ("Yes " + WebHelper.Icon.CheckCircle.AddClass("icon-color-green")) %></td>
                              <td class="type-date"><%= raterListItem.CompletedUTC != null ? "" :
                                                          (raterListItem.LastReminderUTC != null
                                                          ? WebHelper.DisplayDate(raterListItem.LastReminderUTC)
                                                          : WebHelper.DisplayDate(raterListItem.CreatedUTC)) %></td>
                            </tr>
                          </ItemTemplate>
                        <% } %>
                      </tbody>
                      <% if (SurveyInfo?.IsClosed == false && RaterList.Participants.Exists(p => p.CompletedUTC == null)) { %>
                        <tfoot>
                          <tr>
                            <td colspan="2">&nbsp;</td>
                            <td>
                              <button type="button" class="btn btn-primary btn-sm btnSendReminders" id="btnSendReminders">Send Reminders</button>
                            </td>
                          </tr>
                        </tfoot>
                      <% } %>
                    </table>
                    </div>
                  <% } %>
                </div>
              <% } %>

              <div class="tab-pane fade in <%= ActiveTab == ActiveTabEnum.Responses ? "active" : "" %>" id="responses-panel" role="tabpanel" aria-labelledby="responses-tab">

                <% if (SessionHelper.AppAccess.Surveys.CanViewResponses(ProgramInfo, CoacheeInfo, SurveyInfo)) { %>

                  <div class="questionlist">
                    <% foreach (var questionListItem in QuestionList.Questions) { %>
                      <% SetQuestionListItem(questionListItem); %>
                      <div class="qnrow <%= qnRowClass %>">
                        <div class="qnum"><%= questionNumber %></div>
                        <div class="qnbody">
                          <div class="qtxt <%= questionTextClass %>"><div class="qansnum <%= answerNumericClass %>"><%= answerNumeric %></div>
                            <%= questionListItem.QuestionTextSelf %>.</div>
                          <div class="qanstxt <%= answerTextClass %>"><%= answerText %></div>
                        </div>
                      </div>
                    <% } %>
                  </div>

                <% } %>

              </div>
            </div>

            <div id="modalSendReports" class="hidden">
              <form id="formSendReports" method="post" action="#">
                <% if (SurveyInfo.FoundParticipantBrief?.IsReportAvailable_Online == true) { %>
                  <%= WebHelper.CustomCheckBox(FormFields.SendWebReport, "1", DefaultSendOnlineReport, true, "Send Online Report Link") %>
                <% } %>
                <% if (SurveyInfo.FoundParticipantBrief?.IsReportAvailable_PDF == true
                    && SurveyInfo.SurveyType != DbHelper.AlbertSurveys.SurveyTypeEnum.Standard360) { %>
                  <%= WebHelper.CustomCheckBox(FormFields.SendPDFReport, "1", DefaultSendPDFReport, "Send PDF Report Link") %>
                <% } %>
              </form>
            </div>

          </div>
        </div>

      <% } %>

    <% } %>

  </div>

<% } %>

<script>

  // Closure for Surveys Tab.
  (function ($) {

    var incompleteRaters = toDecimalInt("<%= IncompleteRaters %>", 0);
    var btnSendReminders, btnChangeCloseDateSelf, btnChangeCloseDateRaters;
    var btnSendReports, modalSendReports, formSendReports;

    $(document).ready(function () {

      <% if (!IsNewCoachee) { %>

        btnSendReminders = $("#btnSendReminders");
        btnChangeCloseDateSelf = $("#btnChangeCloseDateSelf");
        btnChangeCloseDateRaters = $("#btnChangeCloseDateRaters");
        btnSendReports = $("#btnSendReports");
        modalSendReports = $("#modalSendReports");
        formSendReports = $("#formSendReports");

        if (incompleteRaters > 0 && btnSendReminders.length == 1) {
          btnSendReminders.text("Send " + incompleteRaters + " Reminder" + (incompleteRaters > 1 ? "s" : ""));
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

        btnSendReports.on("click", function () {
          var formParent = modalSendReports.parent();
          BootstrapDialog.show({
            title: "Email Report Links",
            message: "",
            buttons: [
              {
                label: 'Cancel',
                cssClass: 'btn-secondary',
                action: function (dialog) { dialog.close(); }
              },
              {
                label: 'Send Now',
                cssClass: 'btn-primary',
                action: function (dialog) { dialog.close(); SendReports(); }
              }
            ],
            onshow: function (dialog) {
              modalSendReports.appendTo(dialog.getModalBody().find(".bootstrap-dialog-message"));
              modalSendReports.show();
            },
            onhide: function (dialog) {
              modalSendReports.hide().appendTo(formParent);
            }
          });
        });

      <% } %>

    }); // Doc ready.

    function SendReports() {

      AjaxSubmit({
        form: formSendReports,
        url: "<%= PathHelper.Partials.CoacheeSurveyDetailsModal(CoacheeInfo.CoacheeId, WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyUId)) %>",
        action: "<%= AjaxAction.SendReports %>"
      });
    }

    function UpdateCloseDate(isSelf, closeDate) {

      var ajaxAction, btnChangeCloseDate, formDateKey

      if (isSelf) {
        ajaxAction = "<%= AjaxAction.UpdateCloseDateSelf %>";
        formDateKey = "<%= FormFields.CloseDateSelf %>";
        btnChangeCloseDate = btnChangeCloseDateSelf;
      } else {
        ajaxAction = "<%= AjaxAction.UpdateCloseDateRaters %>";
        formDateKey = "<%= FormFields.CloseDateRaters %>";
        btnChangeCloseDate = btnChangeCloseDateRaters;
      }

      var submitData = { };
      submitData[formDateKey] = moment(closeDate).format("<%= WebHelper.DATE_OUTPUT_FORMAT_MOMENTJS %>").toString();

      AjaxSubmit({
        url: "<%= PathHelper.Partials.CoacheeSurveyDetailsModal(CoacheeInfo.CoacheeId, WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyUId)) %>",
        action: ajaxAction,
        data: submitData
      });
    }

    function SendReminders() {

      if (incompleteRaters == 0) return;

      AjaxSubmit({
        url: "<%= Request.RawUrl %>",
        action: "<%= AjaxAction.SendReminders %>"
      });
    }

  })(jQuery);

</script>
