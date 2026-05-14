<%@ Page Language="C#" AutoEventWireup="true" CodeFile="CoacheeEdit.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.CoacheeEdit" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="stylesheet" type="text/css" href="<%= PathHelper.UrlPath.CSS %>survey-viewer-common.css" />

  <style>
    .boxTitle h5 { margin: 0px; }
    .btnUpdateProfile { border-radius: 50%; left: 81px; margin-top: -70px; }
    .checkbox-row { display: flex; justify-content: space-between; align-items: center}
    #sessList-table .col-quoteitem .select2-container { width: 100%; min-width: 300px }
    #sessList-table .col-quoteitem .select2-container .select2-selection--single .select2-selection__rendered { white-space: normal; }
  </style>

  <%= WebHelper.ParticipantActivities.GetHeaderParticipantUserActivityInfo(CoacheeInfo, CoacheeInfo.UserActivity, CoacheeInfo.UserSubscription, CanUpdateProfile) %>

  <% if (CanViewNonProfileTabs) { // If viewing only Profile, don't show any tabs. %>

    <ul class="nav nav-tabs nav-tabs-underlined" id="formTabs">
      <% if (CanViewParticipantSummary && SummaryTab.HasSummaryInfo) { %>
        <li role="presentation" class="active">
          <a class="nav-link" id="tab-<%= PathHelper.CoacheeTabEnum.summary %>" data-tabname="<%= PathHelper.CoacheeTabEnum.summary %>" data-toggle="tab" href="#panel-<%= PathHelper.CoacheeTabEnum.summary %>" role="tab" aria-controls="panel-<%= PathHelper.CoacheeTabEnum.summary %>" aria-selected="true">Summary</a>
        </li>
      <% } %>
      <li role="presentation">
        <a class="nav-link" <%= CanViewParticipantSummary  && SummaryTab.HasSummaryInfo ? "" : "class=\"active\"" %> id="tab-<%= PathHelper.CoacheeTabEnum.settings %>" data-tabname="<%= PathHelper.CoacheeTabEnum.settings %>" data-toggle="tab" href="#panel-<%= PathHelper.CoacheeTabEnum.settings %>" role="tab" aria-controls="panel-<%= PathHelper.CoacheeTabEnum.settings %>">Settings</a>
      </li>
      <li role="presentation">
        <a class="nav-link" id="tab-<%= PathHelper.CoacheeTabEnum.coaching %>" data-tabname="<%= PathHelper.CoacheeTabEnum.coaching %>" data-toggle="tab" href="#panel-<%= PathHelper.CoacheeTabEnum.coaching %>" role="tab" aria-controls="panel-<%= PathHelper.CoacheeTabEnum.coaching %>">Coaching</a>
      </li>
      <li role="presentation">
        <a class="nav-link" id="tab-<%= PathHelper.CoacheeTabEnum.notes %>" data-tabname="<%= PathHelper.CoacheeTabEnum.notes %>" data-toggle="tab" href="#panel-<%= PathHelper.CoacheeTabEnum.notes %>" role="tab" aria-controls="panel-<%= PathHelper.CoacheeTabEnum.notes %>">Notes</a>
      </li>
      <li role="presentation">
        <a class="nav-link" id="tab-<%= PathHelper.CoacheeTabEnum.surveys %>" data-tabname="<%= PathHelper.CoacheeTabEnum.surveys %>" data-toggle="tab" href="#panel-<%= PathHelper.CoacheeTabEnum.surveys %>" role="tab" aria-controls="panel-<%= PathHelper.CoacheeTabEnum.surveys %>">Intake & Surveys</a>
      </li>
      <li role="presentation">
        <a class="nav-link" id="tab-<%= PathHelper.CoacheeTabEnum.email %>" data-tabname="<%= PathHelper.CoacheeTabEnum.email %>" data-toggle="tab" href="#panel-<%= PathHelper.CoacheeTabEnum.email %>" role="tab" aria-controls="panel-<%= PathHelper.CoacheeTabEnum.email %>">Emails</a>
      </li>
    </ul>

    <div class="tab-content">
      <% if (CanViewParticipantSummary && SummaryTab.HasSummaryInfo) { %>
        <div class="tab-pane tab-quote tab-<%= PathHelper.CoacheeTabEnum.summary %> fade in active" id="panel-<%= PathHelper.CoacheeTabEnum.summary %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.CoacheeTabEnum.summary %>"></div>
      <% } %>
      <div class="tab-pane tab-quote tab-<%= PathHelper.CoacheeTabEnum.settings %> fade in <%= CanViewParticipantSummary ? "" : "active" %>" id="panel-<%= PathHelper.CoacheeTabEnum.settings %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.CoacheeTabEnum.settings %>"></div>
      <div class="tab-pane tab-quote tab-<%= PathHelper.CoacheeTabEnum.coaching %> fade in" id="panel-<%= PathHelper.CoacheeTabEnum.coaching %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.CoacheeTabEnum.coaching %>"></div>
      <div class="tab-pane tab-quote tab-<%= PathHelper.CoacheeTabEnum.notes %> fade in" id="panel-<%= PathHelper.CoacheeTabEnum.notes %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.CoacheeTabEnum.notes %>"></div>
      <div class="tab-pane tab-quote tab-<%= PathHelper.CoacheeTabEnum.surveys %> fade in" id="panel-<%= PathHelper.CoacheeTabEnum.surveys %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.CoacheeTabEnum.surveys %>"></div>
      <div class="tab-pane tab-quote tab-<%= PathHelper.CoacheeTabEnum.email %> fade in" id="panel-<%= PathHelper.CoacheeTabEnum.email %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.CoacheeTabEnum.email %>"></div>
    </div>

  <% } %>

  <% if (CanViewParticipantSummary && SummaryTab.HasSummaryInfo) { %>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoacheeTabEnum.summary %>">

      <% if (SurveyTab.SurveyList.IsNullOrEmpty()) { %>

        <%= WebHelper.GetNoRecordsBadge("No summary available for this Participant.") %>

      <% } else { %>

        <% CoacheeSummary(); %>

      <% } %>
    </div>
  <% } %>

  <!--  Profile info is always shown. If tabs aren't shown, it still displays on the page without tabs. -->
  <div id="dlgParticipantProfile" class="displaynone">

    <form id="profileForm" class="form-horizontal">

      <input type="hidden" name="<%= ProfileTabModel.FormFields.CoacheeId %>" value="<%= IsNewCoachee ? PathHelper.AbleUrlValues.IdNew : CoacheeInfo.CoacheeId.ToString() %>" />

      <% if (ShowCompanySelection) { %>

        <%= WebHelper.GetSelectRow("Company:", "CompanyId", 5, @"<option value="""">[Select Company]</option>" + ProfileTab.GetCompanyOptions()) %>

        <%= WebHelper.GetSelectRow("Program:", ProfileTabModel.FormFields.ProgramId, 5,
            @"<option value="""">[Select Program]</option>") %><%-- will be autofilled based on chosen company --%>

      <% } else { %>

        <%= WebHelper.GetTextDisplayRow("Company:", 5, CoacheeInfo.CompanyName) %>
        <%= WebHelper.GetTextDisplayRow("Program:", 5, CoacheeInfo.ProgramJobNumber + ": " + CoacheeInfo.ProgramName) %>

      <% } %>

      <hr />
      <%= WebHelper.GetTextInputDual("Name:",
            ProfileTabModel.FormFields.FirstName, CoacheeInfo.FirstName, "First Name",
            ProfileTabModel.FormFields.LastName, CoacheeInfo.LastName, "Last Name",
            IsReadOnly, WebHelper.InputMaxLength.EmailName, 5) %>
      <%= WebHelper.GetTextInput("Email Address:", ProfileTabModel.FormFields.EmailAddress, CoacheeInfo.EmailAddress, 5, "", IsReadOnly) %>
      <%= WebHelper.GetTextInput("Mobile Number:", ProfileTabModel.FormFields.MobilePhone, CoacheeInfo.MobilePhone, 5, "", IsReadOnly, false) %>
      <%= WebHelper.GetInputDateRow("Date Of Birth:", ProfileTabModel.FormFields.DateOfBirth, CoacheeInfo.UserActivity?.DateOfBirth, "", IsReadOnly) %>
      <%= WebHelper.GetTextInputDual("Location:",
            ProfileTabModel.FormFields.City, CoacheeInfo.UserActivity?.City, "City",
            ProfileTabModel.FormFields.Country, CoacheeInfo.UserActivity?.Country, "Country",
            IsReadOnly, WebHelper.InputMaxLength.NoLimit, 5) %>
      <%= WebHelper.GetTextInput("Role Title:", ProfileTabModel.FormFields.RoleTitle, CoacheeInfo.UserActivity?.RoleTitle, 5, "", IsReadOnly) %>
      <%= WebHelper.GetSelectRow("Org Role:", ProfileTabModel.FormFields.OrgRoleId, 5, ProfileTab.GetOrgRolesOptions()) %>

    </form>

  </div>

  <% if (CanViewNonProfileTabs) { // All the tabs apart from Profile. %>

    <div class="tab-panel" id="tab-panel-<%= PathHelper.CoacheeTabEnum.settings %>" data-appendTo="panel-<%= PathHelper.CoacheeTabEnum.settings %>">

      <form id="SettingsForm" class="form-horizontal">

        <%= WebHelper.GetButtonGroup(
            "Participant Status:",
            SettingsTabModel.FormFields.ProgramStatus,
            SettingsTab.GetProgramStatusOptions(),
            CoacheeInfo.ProgramStatusId.ToString(),
            !CanChangeProgramStatus) %>

        <%= WebHelper.GetInputDateRow(
              new WebHelper.RowOptions() {
                Label = "On-Boarding Date (" + ConfigHelper.DefaultTimeZoneAbbrev + "):",
                RightHtml = $@"
                  <span class=\""mr10\"">{(
                    CoacheeInfo.WelcomeEmailSentUtc == null
                      ? "Not yet sent."
                      : $@"Sent On: {TimeHelper.UtcToAppDefaultTimeZone(CoacheeInfo.WelcomeEmailSentUtc).ToString(WebHelper.DATE_OUTPUT_FORMAT)}."
                  )}</span>
                  <span>Send {(CoacheeInfo.WelcomeEmailSentUtc == null ? "" : "Again")} Now: </span>
                  {WebHelper.CustomCheckBox(new WebHelper.CheckboxInfo() {
                    InputName = SettingsTabModel.FormFields.SendWelcomeNow,
                    Value = "1",
                    IsReadOnly = IsReadOnly || !CoacheeInfo.CanReceiveWelcomeEmail,
                    Class = CoacheeInfo.CanReceiveWelcomeEmail ? "" : "no-welcome-tip"
                  })}"
              },
              new WebHelper.DateInputInfo(
                SettingsTabModel.FormFields.WelcomeEmailDate,
                TimeHelper.UtcToAppDefaultTimeZone(CoacheeInfo.WelcomeEmailUtc)
              )
              {
                IsReadOnly = IsReadOnly
              }
            ) %>

        <hr />

        <%= SettingsTab.GetQuoteItemsForSubscriptionHtml("Quote Item:") %>

        <%= SettingsTab.GetSubscriptionOptions("Subscription:") %>

        <%= SettingsTab.GetEnableNudgesOptions("Enable Nudges:") %>

        <%= SettingsTab.GetEnableAICoachingOptions("Enable AI Coach:") %>

        <%= SettingsTab.GetEnablePulseOptions("Enable Pulse:") %>

        <% if (CanApplySettingsToProgram) { %>
          <% new WebHelper.Form.FormRow() {
              LabelPosition = WebHelper.Form.LabelPosition.LeftLegacy,
              ContentHtml = new WebHelper.Form.CheckBox() {
                InputName = "ApplySettingsToProgram",
                Label = "Apply above to all " + CoacheesInProgram.Count + " Participants in Program",
              }.ToHtml()
            }.WriteHtml(); %>
        <% } %>

        <% if (CanUpdateSubscription || !IsReadOnly || CanChangeProgramStatus) { %>
          <div class="btnholder">
            <button type="button" class="btn btn-primary floatright btnUpdateSettings" data-addpaxafter="false" data-waitmsg="Updating...">Update Settings</button>
          </div>
        <% } %>

      </form>
    </div>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoacheeTabEnum.coaching %>">

      <form id="CoachingForm" class="form-horizontal">

        <%= CoachingTab.GetCoachingTypeOptions("Coaching Type:") %>

        <div class="coachingFields <%= CoacheeInfo.HasCoaching ? "" : "display-none" %>">

          <%= CoachingTab.GetCoachDropdown() %>

          <%= WebHelper.GetInputDateRow(
                $"Meet Coach Date ({ConfigHelper.DefaultTimeZoneAbbrev}):",
                CoachingTabModel.FormFields.MeetCoachEmailDate,
                TimeHelper.UtcToAppDefaultTimeZone(CoacheeInfo.MeetCoachEmailUtc),
                $@"{(CoacheeInfo.MeetCoachEmailSentUtc == null
                ? ""
                : $@"<span class="""">Sent On: {TimeHelper.UtcToAppDefaultTimeZone(CoacheeInfo.MeetCoachEmailSentUtc).ToString(WebHelper.DATE_OUTPUT_FORMAT)}</span>")}
                  <span class=""ml20"">Send {(CoacheeInfo.MeetCoachEmailSentUtc == null ? "" : "Again")} Now:</span>
                  {WebHelper.CustomCheckBox(new WebHelper.CheckboxInfo() {
                  InputName = CoachingTabModel.FormFields.SendMeetCoachNow,
                  Value = "1",
                  IsReadOnly = !SessionHelper.AppAccess.Participants.CanSendMeetCoachEmail(CoacheeInfo)
                })}",
                !CanChangeMeetCoachDate) %>

          <%= WebHelper.GetTextInput("Total Sessions Allocated:", CoachingTabModel.FormFields.SessionsAllocated, CoacheeInfo.UserActivity?.SessionsAllocated.ToString(), 1, "", LimitedEdit) %>

          <% if (CoacheeInfo.HasCoaching && CoacheeInfo.UserActivity?.SessionsBooked < CoacheeInfo.UserActivity?.SessionsAllocated) { %>
            <%= WebHelper.GetTextDisplayRow("Session Booking Link:", 8,
                  $"<a href=\"{PathHelper.Pages.CoacheeSessionBooking(CoacheeInfo.CoacheeUID, true)}\" target=\"_blank\">"
                  + PathHelper.Pages.CoacheeSessionBooking(CoacheeInfo.CoacheeUID, true).HTMLEncode() + "</a>") %>
          <% } %>

          <div class="row">
            <% if (SessionHelper.AppAccess.Participants.CanAttachBookingLinkInEmail(CoacheeInfo)) { %>
            <%= WebHelper.GetFormWithTooltip("<btn class=\"btn btn-primary mr20 btnSendEmailToCoachee\">Send Booking URL</btn>", "Send Booking URL to Participant", WebHelper.ToolTipContentType.Text, "Please click the box to include the booking URL to Participant.", "floatright") %>
            <% } %>
            <% if (CanAddCoachingSession) { %>
                <button id="btnAddSession" class="btn btn-primary floatright mr20" type="button">Add Session</button>
            <% } %>
          </div>

          <%= WebHelper.GetRowStart() %>
            <div class="table-responsive">
              <table id="sessList-table" class="table">
                <thead>
                  <tr>
                    <th class="type-date">Sessions Booked</th>
                    <th class="type-sessdur">Duration</th>
                    <% if (CoachingTab.CanViewSessionOverallRevenue) { %>
                      <th class="type-money-lg">Revenue</th>
                    <% } %>
                    <% if (CoachingTab.CanViewSessionPartnerRevenue) { %>
                      <th class="type-money-lg">Partner Revenue</th>
                    <% } %>
                    <% if (CoachingTab.CanEditSessionQuoteItem) { %>
                      <th class="type-quoteitem">Quote Item</th>
                    <% } %>
                    <th class="type-actionbutton"></th>
                  </tr>
                </thead>
                <tbody>
                  <tr class="template-row">
                    <td class="type-datetime col-apptdate"></td>
                    <td class="type-sessdur"><span class="lbl-duration"></span> mins</td>
                    <td class="col-price type-money-lg <%= CoachingTab.CanViewSessionOverallRevenue ? "" : "displaynone" %>"><%= WebHelper.GetCurrencyInputNoRow(CoachingTabModel.FormFields.SessList_Price, null, 2, !CoachingTab.CanViewSessionOverallRevenue, true) %></td>
                    <% if (CoachingTab.CanViewSessionPartnerRevenue) { %>
                      <td class="col-revenue type-money-lg"><%= CoacheeInfo.CoachingRevenue %></td>
                    <% } %>
                    <% if (CoachingTab.CanEditSessionQuoteItem) { %>
                      <td class="col-quoteitem type-quoteitem">
                        <select class="noselect2" name="<%= CoachingTabModel.FormFields.SessList_QuoteItemId %>" size="1">
                          <%= CoachingTab.GetQuoteItemOptions() %>
                        </select>
                      </td>
                    <% } %>
                    <td class="col-editSession right"><%= WebHelper.GetActionButton(WebHelper.ActionButtonTypeEnum.edit, "btnEditSession", "Edit Session") %></td>
                  </tr>
                </tbody>
                <% if (CoachingTab.CanViewSessionOverallRevenue || CoachingTab.CanViewSessionPartnerRevenue) { %>
                  <tfoot>
                    <tr height="60">
                      <td colspan="<%= CoachingTab.SessionFooterColSpan %>" class="pr20 strong">Total:</td>
                      <td></td>
                      <td class="align-right strong inp-currency type-money-lg"><span id="total-price"></span></td>
                      <td class="align-right strong inp-currency"><span id="total-coaching"></span></td>
                      <td></td>
                    </tr>
                  </tfoot>
                <% } %>
              </table>
            </div>
          <%= WebHelper.GetRowEnd() %>

        </div><%-- coachingFields --%>

        <% if (CanApplyCoachingToProgram && CoacheesInProgram.Count > 1) { %>
        <div class="checkbox-row">
          <div class="col-md-5">
            <label class="strong">Apply To All Program Participants</label>
            <p>Apply The settings above to all Participants in the Program. If selected, Meet Coach emails will also be sent to all participants.Note the assigned Coach will only be changed for this participant</p>
          </div>
          <%= WebHelper.CustomCheckBox( CoachingTabModel.FormFields.ApplyCoachingToProgram, "1", false, "", "") %>
        </div>
        <% } %>

        <div class="btnholder">
          <button <%= CanUpdateCoaching ? "" : "disabled" %> type="button" class="btn btn-primary btnUpdateCoaching floatright" id="btnUpdateCoaching">Update Coaching Settings</button>
          <button <%= CanDeleteParticipant && CoacheeInfo.CanDelete ? "" : "disabled" %> type="button" class="btn btn-warning btnDelete floatleft" id="btnDelete">Delete Participant</button>
        </div>

      </form>

    </div>

    <div class="tab-panel" id="tab-panel-<%= PathHelper.CoacheeTabEnum.notes %>" data-appendTo="panel-<%= PathHelper.CoacheeTabEnum.notes %>">
      <form id="NotesForm" class="form-horizontal notes-section">

        <%= WebHelper.GetTextDisplayRow("<div class=\"infoLabel\">Program Notes:</div>", 8, "<div class=\"infoLabel\">" + (CoacheeInfo.ProgramNotes.IsNullOrEmpty() ? "(none)" : CoacheeInfo.ProgramNotes) + "</div>") %>

        <% if (CanViewParticipantNotes) { %>
          <hr />
          <%= WebHelper.GetRichTextArea("Participant Notes: " + WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, "Private note, only accessible to PLC, PC and Coach, <br/>not accessible for Participant.", "", "mt3"), NotesTabModel.FormFields.CoacheeNotes, 2, 8, CoacheeInfo.CoacheeNotes, "", !CanEditParticipantNotes) %>
        <% } %>

          <% if (CanEditPrivateCoachNote) { %>
          <hr />
          <%= WebHelper.GetRichTextArea("Coach Notes: " + WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, "Private note, not accessible for Participants or any other users. <br/>Once the note contains info, Coach cannot be changed to the participant.", "", "mt3"), NotesTabModel.FormFields.PrivateCoachNote, 2, 8, CoacheeInfo.PrivateCoachNote, "", !CanEditPrivateCoachNote) %>
        <% } %>

        <% if (CanViewCoachingNotes) { %>
          <%= NotesTab.GetSessionNotesHtml() %>
        <% } %>

        <% if (AllowNotesUpdate) { %>
        <div class="btnholder">
          <button type="button" class="btn btn-primary floatright btnUpdateNotes">Update Notes</button>

        </div>
        <span id="AutoSaveCountdownlbl" class="floatright"></span>
      <% } %>

      </form>
    </div>

    <div class="tab-panel" id="tab-panel-<%= PathHelper.CoacheeTabEnum.surveys %>" data-appendTo="panel-<%= PathHelper.CoacheeTabEnum.surveys %>">

      <div class="w100p pos-absolute">
        <btn class="btn btn-primary mr20 floatright btnSendNewSurveyx"
          <%= WebHelper.GetModalTriggerDataAttributes(PathHelper.Partials.CoacheeSendSurveyModal(CoacheeInfo.CoacheeId)) %>>Send New Survey</btn>
      </div>

      <%= WebHelper.GetPageTabs(
            new WebHelper.PageTabsInfo() { PageTabsStyle = WebHelper.PageTabsStyle.Links },
            new WebHelper.PageTabItem(SurveyTabModel.SurveyFilterEnum.Focus.ToString(), "Focus", true),
            new WebHelper.PageTabItem(SurveyTabModel.SurveyFilterEnum.EvalsPulse.ToString(), "Evals/Pulse")
          ) %>

      <% if (SurveyTab.SurveyList.IsNullOrEmpty()) { %>

        <%= WebHelper.GetNoRecordsBadge("No surveys for this Participant.") %>

      <% } else { %>

        <% GetTabPanel(SurveyTabModel.SurveyFilterEnum.Focus); %>
        <% GetTabPanel(SurveyTabModel.SurveyFilterEnum.EvalsPulse); %>

        <% void GetTabPanel(SurveyTabModel.SurveyFilterEnum surveyFilter) { %>
          <div class="tab-panel" data-appendTo="panel-<%= surveyFilter.ToString() %>">
            <div class="table-responsive">
              <table class="table table-bordered table-hover table-rowlink" data-rowlink-url="">
                <thead>
                  <tr>
                    <th class="type-description">Survey</th>
                    <th class="type-delivery">Survey Type</th>
                    <th class="type-status">Status</th>
                    <th class="type-date">Self Close</th>
                    <th class="type-date">Rater Close</th>
                    <th class="type-date">Report Sent</th>
                    <th class="w200"></th>
                  </tr>
                </thead>
                <tbody>
                  <%
                    var surveyListFiltered = surveyFilter == SurveyTabModel.SurveyFilterEnum.Focus ? SurveyTab.FocusSurveyList : SurveyTab.EvalsPulseSurveyList;
                    if (surveyListFiltered.IsNullOrEmpty()) {
                      %>
                        <tr><td colspan="5">No records found.</td></tr>
                      <%
                    } else {
                      foreach (var thisSurvey in surveyListFiltered) { %>
                        <tr tabindex="0" <%= WebHelper.GetSurveyListRowDataAttrs(ProgramInfo, CoacheeInfo, thisSurvey) %>>
                          <td class="type-description"><b><%= thisSurvey.SurveyName.HTMLEncode() %></b><br /><%= thisSurvey.FriendlyProjectTitle.HTMLEncode() %></td>
                          <td class="type-delivery"><%= WebHelper.GetSurveyDeliveryBadge(thisSurvey) %></td>
                          <td class="type-status"><%= WebHelper.GetSurveyStatusBadge(thisSurvey) %></td>
                          <td class="type-date"><%= WebHelper.GetSurveyCloseDateSelf(thisSurvey) %></td>
                          <td class="type-date"><%= WebHelper.GetSurveyRatersInfoCol(thisSurvey) %></td>
                          <td class="type-date"><%= WebHelper.DisplayDate_UtcToUserTime(thisSurvey.FoundParticipantBrief?.ReportSentUtc, "-") %></td>
                          <td><%= WebHelper.GetSurveyListActionButtons(ProgramInfo, CoacheeInfo, thisSurvey) %></td>
                        </tr>
                      <% }
                    }
                  %>
                </tbody>
              </table>
            </div>
          </div>
        <% } %>
      <% } %>

    </div>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoacheeTabEnum.email %>">

      <div class="row mb10">
        <div class="col-md-6"><h4>Participant Email History</h4></div>
        <% if (EmailsTab.CanSendEmailToCoachee) { %>
          <btn class="btn btn-primary floatright mr20 btnSendEmailToCoachee">Send Email</btn>
        <% } %>
      </div>

      <% if (EmailsTab?.EmailHistoryNoResultsVisible == true) { %>
        No data at this time.
      <% } %>

      <% if (EmailsTab?.EmailHistoryResultsVisible == true) { %>

        <div class="table-responsive">
          <table class="tblCoachees table table-bordered">
            <thead>
              <tr>
                <th class="type-datetime">Date & Time</th>
                <th class="type-description">Subject</th>
                <th class="type-viewcontent">&nbsp;</th>
              </tr>
            </thead>
            <tbody>
              <% foreach (var thisEmailInfo in EmailsTab.EmailHistoryList) { %>
                <tr tabindex="0" class="rowData" data-rowlink-id="<%= thisEmailInfo.EmailHistoryId %>">
                  <td class="type-datetime"><%= thisEmailInfo.SentUtc.UtcToTZ(ConfigHelper.DefaultTimeZoneInfo).ToString("d MMM yyyy, h:mm tt") %></td>
                  <td class="type-description"><%= thisEmailInfo.Subject.HTMLEncode() %></td>
                  <td class="type-viewcontent">
                    <% if (thisEmailInfo.SentUtc > DateTime.UtcNow.AddDays(-30)) { %>
                      <div class="emailBody"><button class="btn btn-xsm btnViewContent">View Content</button></div>
                    <% } %>
                  </td>
                </tr>
              <% } %>
            </tbody>
          </table>
        </div>

      <% } %>

    </div>

  <% } %>

  <% void CoacheeSummary() { %>

    <%-- Overall and AI Summary --%>
    <div class="flex flex-wrap gap15 mb20 flex-column-sm">

      <% if (CoacheeInfo.UserActivity != null && CoacheeInfo.UserActivity.Latest360IntakeCodeId != null) { %>
        <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = "partial_Overview",
          Url = PathHelper.Partials.SurveyViewer_Overview(null, null),
          InitialWidth = "400px",
          InitialHeight = "200px",
          DeferInitialLoad = true,
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>
      <% } %>

      <div class="boxBorder flex1 minh250 minw350">
        <div class="boxTitle"><h4>Monthly Activity Minutes</h4></div>
        <%= WebHelper.Charts.Coachee.GetMonthlyActivityChart(CoacheeInfo.CoacheeId, DateTime.UtcNow.AddMonths(-5), DateTime.UtcNow, 250, "mt15") %>
      </div>

      <% if (CoacheeInfo != null && CoacheeInfo.UserActivity != null &&  !CoacheeInfo.UserActivity.AISummaryText.IsNullOrEmptyOrWhitespace()) { %>
        <div class="boxBorder flex1 ai-summary-box">
          <div class="boxTitle"><h4>AI Coach Development Summary</h4></div>
          <div class="boxBody nicer-scrollbar"><%= WebHelper.MarkdownToHtml(CoacheeInfo.UserActivity.AISummaryText) %></div>
        </div>
      <% } %>

    </div>

    <%-- Leadership Indicators and Focus --%>
    <div class="flex gap15 flex-fill flex-column-lg">

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = "partial_Categories",
        Url = PathHelper.Partials.SurveyViewer_Categories(null, null),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = "partial_Categories",
        Url = PathHelper.Partials.SurveyViewer_Focus(null, null, PathHelper.Partials.SurveyViewer_Focus_ShowSection.Highest),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = "partial_Categories",
        Url = PathHelper.Partials.SurveyViewer_Focus(null, null, PathHelper.Partials.SurveyViewer_Focus_ShowSection.Lowest),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>

    </div>

  <% } // CoacheeSummary %>

  <% if (CanViewNonProfileTabs) { %>
    <script>

      // Closure for Coaching Session Modal
      (function ($) {

        var coachingForm;

        $(document).ready(function () {

          coachingForm = $("#CoachingForm");

          // On "Add Session" button clicked
          $("#btnAddSession").click(function (evt) {
            ShowCoachingSessionModal('<%= PathHelper.AbleUrlValues.IdNew %>', "Add Coaching Session");
          });

          // On Sessions table button clicked
          $(document).on("click", ".btnEditSession", function (evt) {
            // Get sessionId from button clicked
            var sessionId = $(this).data('session-id');
            if (!isNumber(sessionId)) return;
            ShowCoachingSessionModal(sessionId, "Edit Coaching Session");
          });
        });

        function ShowCoachingSessionModal(sessionId, title) {

          if (coachingForm.data("savedData") != coachingForm.serialize()) {
            common_InfoDialog("Please update coaching changes before editing a session.");
            return;
          }

          BootstrapDialog.show({
            title: title,
            onshow: function (dialogRef) {
              var modalDialog = dialogRef.getModalDialog();
              modalDialog.css("width", "700px");
              modalDialog.data("<%= WebHelper.DataAttrName.DialogRef %>", dialogRef);
              var modalBody = dialogRef.getModalBody();
              modalBody.busyLoad("show");
              modalBody.load('<%= PathHelper.Partials.CoachingSessionModal(CoacheeInfo.CoacheeId, null) %>' + sessionId,
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
      })(jQuery);

      // Closure for Coaching Session List.
      // Note this block MUST be before the "PostScriptContent" section below.
      (function ($) {

        var sessListData, sessListTable, templateRow, sessListBody, inpAllocated, grpSessionType, LimitedEdit;
        var canChangeLockedSessionQuoteItem = <%= CoachingTab.CanChangeLockedSessionQuoteItem.ToJSTrueFalse() %>;
        var canViewSessionOverallRevenue = <%= CoachingTab.CanViewSessionOverallRevenue.ToJSTrueFalse() %>;
        var canViewSessionPartnerRevenue = <%= CoachingTab.CanViewSessionPartnerRevenue.ToJSTrueFalse() %>;
        var canEditSessionQuoteItem = <%= CoachingTab.CanEditSessionQuoteItem.ToJSTrueFalse() %>;
        var programDeliveryPercentageBig = new Big("<%= ProgramInfo?.Partner_DeliveryPercentage.GetValueOrDefault(0) %>");
        var btnAddSession;

        $(document).ready(function () {

          LimitedEdit = <%= LimitedEdit.ToJSTrueFalse() %>;
          sessListData = JSON.parse('<%= CoachingTab.GetSessionListJson() %>');
          sessListTable = $("#sessList-table");
          sessListBody = sessListTable.children("tbody"); // table body
          templateRow = sessListBody.children(".template-row");
          templateRow.detach().removeClass("template-row");
          sessListBody.on('change', 'input[name^="<%= CoachingTabModel.FormFields.SessList_Price %>"]', UpdateRevenue);
          inpAllocated = $('input[name="<%= CoachingTabModel.FormFields.SessionsAllocated %>"]');
          grpSessionType = $('.form-group[data-for="<%= CoachingTabModel.FormFields.CoachingType %>"]');
          btnAddSession = $("#btnAddSession");

          grpSessionType.on("change", "input:checked", UpdateDurations);

          inpAllocated.on("change", function (evt) {
            setTimeout(function () {
              AddExtraRows();
              UpdateRevenue();
              UpdateAddSessionButton();
            }, 100);
          });

          if (!CreateList()) alert("Problem occurred while creating session list.");
          if (!AddExtraRows()) alert("Problem occurred while creating the session list.");

          UpdateDurations();
          UpdateRevenue();
          UpdateAddSessionButton();
        });

        function UpdateAddSessionButton() {
          var sessionCount = <%= CoachingTab.CoachingSessionList.Count %>;
          var sessionsAllocated = toDecimalInt(inpAllocated.val(), null);
          btnAddSession.prop("disabled", sessionCount == sessionsAllocated);
        }

        function UpdateRevenue(evt) {
          if (evt) UpdateComponentRevenue(evt);
          UpdateTotalRevenue();
          UpdateTotalPrice();
        }

        function UpdateComponentRevenue(evt) {
          var priceInput = $(evt.target);
          var priceStr = priceInput.val().replace(/[^0-9.]/g, "");
          if (!isNumber(priceStr)) return;
          var priceBig = new Big(priceStr);
          var row = priceInput.closest("tr");
          var revenueCell = row.find(".col-revenue");
          revenueCell.text(CurrencyFormatter.format((priceBig.times(programDeliveryPercentageBig).toNumber())));
        }

        // CreateList return true/false for success/fail.
        function CreateList() {
          sessListBody.empty();
          if (sessListData != null && sessListData.length > 0) {
            for (var i = 0; i < sessListData.length; i++) {
              var s = sessListData[i]
              var rowOk = AppendRow(s.SessionNumber, s.SessionId, s.CompletedLocal, s.DurationMins, s.ComponentPrice, s.QuoteItemId, s.IsLocked);
              if (!rowOk) return false;
            }
          }
          common_UpdateUI(sessListBody); // Call after adding rows.
          return true;
        }

        // AppendRow returns true/false for success/fail.
        function AppendRow(sessionNumber, sessionId, apptDateLocal, durationMins, price, quoteItemId, IsLocked) {

          var row = templateRow.clone().removeClass("template-row");
          var btnEditSession = row.find(".btnEditSession");

          sessListBody.append(row); // add to DOM first, so the change events fire.

          // Add above as attributes so we can inspect them in the browser:
          row.attr("data-session-number", sessionNumber);
          row.attr("data-session-id", sessionId);
          btnEditSession.attr("data-session-id", sessionId);

          var inpDate = row.find(".col-apptdate");
          if (inpDate.length !== 1) { alert("Can't find session date field."); return false; }
          inpDate.text(apptDateLocal == null ? "TBA" : moment(apptDateLocal).format("D MMM YYYY"));

          var inpDuration = row.find(".lbl-duration")
          if (inpDuration.length !== 1) { alert("Can't find session duration field."); return false; }
          inpDuration.text(durationMins == null ? "" : durationMins);

          if (canViewSessionOverallRevenue || canViewSessionPartnerRevenue) {
            var inpPrice = row.find(".col-price input");
            if (inpPrice.length !== 1) { alert("Can't find session price field."); return false; }
            inpPrice.attr("name", inpPrice.attr("name") + "<%= CoachingTabModel.SessionKeyDelimiter %>" + sessionNumber);
            inpPrice.val(price == null ? "" : price).change();
          }
          if (canEditSessionQuoteItem) {
            var inpQuoteItem = row.find(".col-quoteitem select");
            if (inpQuoteItem.length !== 1) { alert("Can't find session quoteitem field."); return false; }
            inpQuoteItem.attr("name", inpQuoteItem.attr("name") + "<%= CoachingTabModel.SessionKeyDelimiter %>" + sessionNumber);
            inpQuoteItem.find('option[value="' + quoteItemId + '"]').attr("selected", true);
            inpQuoteItem.removeAttr("noselect2").select2();
          }
          if (IsLocked || LimitedEdit) {
            row.addClass("row-locked");
            if (canViewSessionOverallRevenue) inpPrice.prop("disabled", true);
            else row.find(".col-price").remove();
            if (canEditSessionQuoteItem && !canChangeLockedSessionQuoteItem) inpQuoteItem.prop("disabled", true);
          }

          if (sessionId == null) btnEditSession.hide();

          return true;
        }

        function UpdateTotalRevenue() {
          if (!canViewSessionPartnerRevenue) return;
          var totalRevenue = new Big(0);
          sessListBody.find(".col-revenue").each(function (i, e) {
            var rowRevenueStr = $(e).text().replace(/[^0-9.]/g, "");
            if (isNumber(rowRevenueStr)) {
              totalRevenue = totalRevenue.add(new Big(rowRevenueStr));
            }
          });
          $("#total-coaching").text(CurrencyFormatter.format(totalRevenue.toNumber()));
        }

        function UpdateTotalPrice() {
          if (!canViewSessionPartnerRevenue) return;
          var totalRevenue = new Big(0);
          sessListBody.find(".inp-currency").each(function (i, e) {
            var rowPriceStr = $(e).val().replace(/[^0-9.]/g, "");
            if (isNumber(rowPriceStr)) {
              totalRevenue = totalRevenue.add(new Big(rowPriceStr));
            }
          });
          $("#total-price").text(CurrencyFormatter.format(totalRevenue.toNumber()));
        }

        function UpdateDurations() {
          var rows = sessListBody.children();
          if (rows.length == 0) return;
          var btnSessionType = $('input[name="<%= CoachingTabModel.FormFields.CoachingType %>"]:checked');
          if (btnSessionType.length != 1) return;
          var durationStr = "" + btnSessionType.data("<%= CoachingTabModel.DataNames.Durations %>");
          if (durationStr.length == 0) return;
          var durations = durationStr.split(",");
          rows.each(function (i, e) {
            var row = $(e);
            var sessionNumber = toDecimalInt(row.data("session-number"), 0);
            if (sessionNumber > 0) {
              var duration = durations[Math.min(sessionNumber, durations.length) - 1];
              row.find(".lbl-duration").text(duration);
            }
          });
        }

        // AddExtraRows returns true/false for success/fail.
        function AddExtraRows() {
          var currentRowCount = sessListBody.children().length;
          var sessionCount = <%= CoachingTab.CoachingSessionList.Count %>;
          var sessionsAllocated = toDecimalInt(inpAllocated.val(), null);
          if (sessionsAllocated == currentRowCount) return true; // No change.
          if (sessionsAllocated == null || sessionsAllocated < sessionCount) {
            sessionsAllocated = sessionCount; // Can't be less that existing # of sessions.
            inpAllocated.val(sessionsAllocated);
          }
          if (sessionsAllocated > 50) {
            sessionsAllocated = 50; // Some upper limit.
            inpAllocated.val(sessListData.length);
          }
          if (sessionsAllocated < currentRowCount) {
            // delete bottom rows
            while (sessListBody.children().length > sessionsAllocated) sessListBody.children(":last-child").remove();
          } else {
            // add top rows
            for (var i = currentRowCount + 1; i <= sessionsAllocated; i++) {
              var rowOk = AppendRow(i, null, null, null, null, null, null);
              if (!rowOk) return false;
            }
            common_UpdateUI(sessListBody); // Call after adding rows.
          }
          return true;
        }

      })(jQuery);

    </script>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">

    // Common Vars
    var isNewCoachee;
    var $pageTabs = $("#formTabs");

    // Closure for Summary
    (function ($) {

      $(document).ready(function () {
        var intakeId = <%= CoacheeInfo.UserActivity.Latest360IntakeCodeId.ToStringOrDefaultIfNull("null") %>;
        if (intakeId != null) LoadPartReport(intakeId);
      });

      function LoadPartReport(intakeId) {

        var delay = 300;

        <% if (CoacheeInfo.UserActivity.Latest360IntakeCodeId != null) { %>
          $.EachPartial(function ($partial, partialInfo) {
            partialInfo.Clear();
            if (isNumber(intakeId)) {
              setTimeout(function (thisPartialInfo) {
                var extraValues = {
                  "<%= PathHelper.AbleUrlKeys.SurveyIntakeCodeId %>": intakeId,
                  "<%= PathHelper.AbleUrlKeys.CoacheeGuid %>": "<%= CoacheeInfo.UserActivity.Latest360CoacheeGuid.ToStringNoBracesOrNull() %>",
                  "<%= PathHelper.AbleUrlKeys.SurveyViewerBenchmark %>": "<%= PathHelper.SurveyViewerBenchmarkEnum.Global %>"
                };
                thisPartialInfo.LoadUrl(thisPartialInfo.initialUrl, extraValues);
              }, delay, partialInfo);
              delay += 300;
            }
          });
        <% } %>
      }

    })(jQuery);

    // Closure for common code, runs first.
    (function ($) {
      $(document).ready(function () {

        isNewCoachee = <%= IsNewCoachee.ToJSTrueFalse() %>;

        if ($pageTabs.length == 1) {

          var $navigationTab = $('a[data-tabname="<%= SelectedCoacheeTab %>"]');
          if ($navigationTab.length > 0) {
            // Activate initially selected tab.
            $('a[href="#panel-<%= SelectedCoacheeTab %>"]').click();
          } else {
            // Activate initially selected tab.
            $('a[href="#panel-<%= PathHelper.CoacheeTabEnum.coaching %>"]').click();
          }

          $pageTabs.click(function (e) {
            UpdateUrlAddress($(e.target).data("tabname"));
          });

        }

        $(".btnSendEmailToCoachee").on("click", function (evt) {
          $('a[href="#panel-<%= PathHelper.CoacheeTabEnum.email %>"]').click();
          var modalPath = "<%= PathHelper.Partials.CoacheeSendEmailModal(CoacheeInfo.CoacheeId) %>";
          ShowSurveyModal(modalPath, "Send New Email to Coachee");
        })

      });

      function ShowSurveyModal(modalPath, title) {

        BootstrapDialog.show({
          title: title,
          onshow: function (dialogRef) {
            var modalDialog = dialogRef.getModalDialog();
            modalDialog.css("width", "850px");
            modalDialog.data("<%= WebHelper.DataAttrName.DialogRef %>", dialogRef);
            var modalBody = dialogRef.getModalBody();
            modalBody.busyLoad("show");
            modalBody.load(modalPath,
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

    })(jQuery);

    function UpdateUrlAddress(tabName) {
      window.history.pushState('', '', '<%= PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, null) %>' + tabName);
    }

    // Closure for Profile Tab.
    (function ($) {

      var profileForm = $("#profileForm");
      var selCompanyId = $('select[name="<%= ProfileTabModel.FormFields.CompanyId %>"]');
      var selProgramId = $('select[name="<%= ProfileTabModel.FormFields.ProgramId %>"]');
      var $dlgParticipantProfile = $("#dlgParticipantProfile");

      $(document).ready(function() {

        if (isNewCoachee) {
          // Adjust menu.
          $(".activeparent > ul.submenu > li.active a span").text("New Participant");
          $(".activeparent > ul.submenu > li:not(.active)").hide();
        }

        new jBox('Tooltip', {
          attach: 'label.icheck-disabled[for="chk_<%= SettingsTabModel.FormFields.SendWelcomeNow %>_1"]', position: { y: 'top' },
          title: 'Can\'t Send Welcome Email', content: 'Requires upcoming coaching or workshops or AI coaching or customised in project.'
        });

        <% if (ShowCompanySelection) { %>
          // Company dropdown.
          selCompanyId.change(function(e) { ChangeCompany(); });
          ChangeCompany();
        <% } %>

        $(".btnUpdateProfile").click(ShowProfileDialog)

      }); // Doc ready.

      function ShowProfileDialog() {
        var dlg = common_InfoDialog("#dlgParticipantProfile", {
          name: "updateProfile",
          title: "Participant Profile",
          width: 850,
          focus: $('input[name="<%= ProfileTabModel.FormFields.FirstName %>"]'),
          buttons: [
            { text: "Cancel", class: "btn-secondary mr20 left", isDefault: false, isPrimary: false, close: true },
            {
              text: "Update Profile", isDefault: true, isPrimary: true, close: false, click: function (e) {
                if (isNewCoachee) {
                  SaveProfileChanges(e);
                } else {
                  // CheckEmailChange will redirect to Save Profile Changes after analyzing.
                  CheckEmailChange(e);
                }
              }
            }
          ],
          shown: function () {
          },
          hide: function () {
          }
        });
      }

      function ChangeCompany() {

        var cid = selCompanyId.val();
        cid = parseInt(cid, 10) || 0;
        GetPrograms(cid);
      }

      function GetPrograms(cid) {

        $.get("<%= PathHelper.Endpoints.ProgramsForCompanyId(0) %>" + cid, function (data) {
          selProgramId.empty();
          if (data && data.Programs && data.Programs.ProgramInfoList) {
            for (var i_program in data.Programs.ProgramInfoList) {
              var program = data.Programs.ProgramInfoList[i_program];
              var $option = $("<option/>", {
                value: program.ProgramJobId,
                text: program.ProgramJobNumber + ": " + program.ProgramJobName,
              }).data("jobid", program.ProgramJobId);

              if (<%= CoacheeInfo.ProgramJobId.GetValueOrDefault(0) %> == program.ProgramJobId) {
                $option.prop("selected", true);
              }

              selProgramId.append($option);
            }
          }
        }, "json");
      }

      function CheckEmailChange(evt) {
        if (isNewCoachee) return;

        var currentEmail = "<%= CoacheeInfo.EmailAddress %>";
        var emailInput = $('input[name="<%= ProfileTabModel.FormFields.EmailAddress %>"]').val();
        var canChangeUserIdIfEmailChanged = <%= CanChangeUserIdIfEmailChanged.ToJSTrueFalse() %>;

        if (currentEmail != emailInput && canChangeUserIdIfEmailChanged) {

          AjaxSubmit({
            url: document.location.href,
            action: "<%= AjaxAction.CheckEmailChange %>",
            data: {
              "<%= ProfileTabModel.FormFields.EmailAddress %>": emailInput
            },
            onSuccess: function (jqXHR, data) {
              var emailTextChange = data["<%= ProfileTabModel.AjaxReturnData.CheckEmailChangeMsg %>"];

              if (emailTextChange != "" && emailTextChange != null) {
                common_ConfirmDialog(emailTextChange,
                  function (confirmed) {
                    if (confirmed) SaveProfileChanges(evt);
                  }
                );
              } else {
                SaveProfileChanges(evt);
              }
            },
          });

        } else {
          SaveProfileChanges(evt);
        }
      }

      function SaveProfileChanges(evt) {
        var thisBtn = $(evt.target);
        var addpaxafter = thisBtn.data('addpaxafter');

        AjaxSubmit({
          form: profileForm,
          action: "<%= AjaxAction.UpdateProfile %>",
          data: {
            "<%= ProfileTabModel.FormFields.AddParticipantAfterCurrent %>": addpaxafter
          },
          onSuccess: function (jqXHR, data) {
            profileForm.data("savedData", profileForm.serialize());
          },
          onFail: function (jqXHR, data) {
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Update failed, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

    })(jQuery);

  </script>

  <% if (CanViewNonProfileTabs) { // Script for tabs other than Profile. %>

    <script type="text/javascript">

      // Closure for Notes Tab
      (function ($) {

        var countdownSeconds;
        var btnUpdateNotes, notesForm, autoSaveCountdownlbl;
        var isUpdatingNotesDb, allowNotesUpdate, countdownTimer;

        $(document).ready(function () {
          notesForm = $("#NotesForm");
          btnUpdateNotes = notesForm.find(".btnUpdateNotes");
          autoSaveCountdownlbl = notesForm.find("#AutoSaveCountdownlbl");
          countdownSeconds = 60;
          isUpdatingNotesDb = false;
          allowNotesUpdate = <%= AllowNotesUpdate.ToJSTrueFalse() %>;
          var notesTabName = "<%= PathHelper.CoacheeTabEnum.notes %>";

          if (allowNotesUpdate) {
            // On page load check if Notes tab is open so timer starts
            var activeTabName = $('#formTabs li.active a').data('tabname');
            if (activeTabName == notesTabName) {
              StartNotesTimer(); // Start timer and process to update when notes tab is active.
            }

            $pageTabs.click(function (e) {
              var thisClickedTab = $(e.target).data("tabname");
              if (thisClickedTab == notesTabName) {
                StartNotesTimer(); // Start timer and process to update when notes tab is active.
              } else {
                StopNotesTimer(); // When notes tab is not active, stop timer to prevent updating.
              }
            });

            btnUpdateNotes.click(function (e) {
              countdownSeconds = 0;
              isUpdatingNotesDb = true;
              UpdateCountdownDisplay();
              UpdateNotes();
            }); // Update on click.

            $(window).on("beforeunload", function () {
              StopNotesTimer();
            });
          }
        }); // ready.

        function StartNotesTimer() {
          StopNotesTimer();
          countdownSeconds = 60; // Reset countdown to 60 seconds
          UpdateCountdownDisplay();
          countdownTimer = setInterval(UpdateCountdown, 1000);
        }

        function StopNotesTimer() {
          clearInterval(countdownTimer);
        }

        function UpdateCountdown() {
          if (isUpdatingNotesDb) return;

          countdownSeconds--;
          if (countdownSeconds <= 0) {
            StopNotesTimer();
            isUpdatingNotesDb = true;
            UpdateNotes();
          }
          UpdateCountdownDisplay();
        }

        function UpdateCountdownDisplay() {
          if (countdownSeconds <= 0) {
            autoSaveCountdownlbl.text("Saving...");
          } else {
            autoSaveCountdownlbl.text("Auto-save in " + countdownSeconds + " seconds");
          }
        }

        function UpdateNotes() {

          if (!allowNotesUpdate) return;

          StopNotesTimer();

          // Check if there's an active/focused input in the form
          var activeInput = $('.notes-section').find(':focus');

          // tinyMCE works different, so check if an input linked to it is active and grab it's id.
          var activeEditor = tinymce.activeEditor;
          let editorId = null;
          if (activeEditor && $('.notes-section').has(activeEditor.getContainer()).length) {
            editorId = activeEditor.id;
          }

          // Text area MUST loose focus to grab the information written to it.
          btnUpdateNotes.focus();

          AjaxSubmit({
            form: notesForm,
            action: "<%= AjaxAction.UpdateNotes %>",
            onSuccess: function (jqXHR, data) {
              common_SuccessToast("Notes Updated.");
              // Restore focus to the correct input/editor based on values before the update
              if (editorId) {
                tinymce.get(editorId).focus();
              } else if (activeInput.length) {
                activeInput.focus();
              }
            },
            onFail: function (jqXHR, data) {},
            onError: function (jqXHR, textStatus, errorThrown) {
              common_InfoDialog("Update failed, please try again later.");
            },
            onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) {
              StartNotesTimer();
              isUpdatingNotesDb = false;
            }
          });
        }

      })(jQuery);

      // Closure for Coaching Tab
      (function ($) {

        var coachingTypeOpts, coachingFields, btnUpdateCoaching, btnDelete, coachingForm;

        $(document).ready(function () {

          coachingForm = $("#CoachingForm");
          coachingForm.data("savedData", coachingForm.serialize());
          coachingTypeOpts = coachingForm.find('input[name="<%= CoachingTabModel.FormFields.CoachingType %>"]');
          coachingFields = coachingForm.find(".coachingFields");
          btnUpdateCoaching = coachingForm.find("#btnUpdateCoaching");
          btnDelete = coachingForm.find("#btnDelete");

          coachingTypeOpts.change(CoachingTypeChanged);
          CoachingTypeChanged(true);

          btnUpdateCoaching.click(UpdateCoaching);
          btnDelete.click(DeleteCoachee);

        });

        function CoachingTypeChanged(docReady) {
          var opt = coachingTypeOpts.filter(":checked");
          if (opt.length == 1) {
            if (opt.val() == "<%= DbHelper.AlbertCoachingTypes.GetIntercomValue_NoCoaching() %>") {
              coachingFields.slideUp(200);
            } else {
              coachingFields.slideDown(200);
            }
          }
        }

        function UpdateCoaching() {

          AjaxSubmit({
            form: coachingForm,
            action: "<%= AjaxAction.UpdateCoaching %>",
            onSuccess: function (jqXHR, data) {
              coachingForm.data("savedData", coachingForm.serialize());
            },
            onFail: function (jqXHR, data) {
            },
            onError: function (jqXHR, textStatus, errorThrown) {
              common_InfoDialog("Update failed, please try again later.");
            },
            onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
          });
        }

        function DeleteCoachee() {
          if (!confirm("Delete this Participant?")) return;
          AjaxSubmit({
            action: "<%= AjaxAction.DeleteParticipant %>",
            onSuccess: function (jqXHR, data) { },
            onFail: function (jqXHR, data) { },
            onError: function (jqXHR, textStatus, errorThrown) {
              common_InfoDialog("Delete failed, please try again later.");
            },
            onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
          });
        }

      })(jQuery);

      // Closure for Email Tab.
      (function ($) {

        $(document).ready(function () {

          $(".btnViewContent").click(function (e) {

            var $btn = $(e.target);
            var $row = $btn.closest("tr");
            var sentDate = $row.children("td:nth-child(1)").text();
            var subject = $row.children("td:nth-child(2)").text();

            AjaxSubmit({
              action: "<%= AjaxAction.EmailMessageContent %>",
              data: {
                sentDate: sentDate,
                subject: subject
              }
            });
          });
        });

      })(jQuery);

      // Closure for Settings Tab.
      (function ($) {
        var settingsForm = $("#SettingsForm");
        var btnUpdateSettings = $(".btnUpdateSettings");
        var selSubscriptionQuoteItemId = $('select[name="<%= SettingsTabModel.FormFields.SubscriptionQuoteItemId%>"]');
        var selSubscriptionId = $('select[name="<%= SettingsTabModel.FormFields.SubscriptionId%>"]');
        var enableNudgesRadio = $('input[name="<%= SettingsTabModel.FormFields.EnableNudges %>"]');
        var enableAICoachingRadio = $('input[name="<%= SettingsTabModel.FormFields.EnableAICoaching %>"]');
        var enablePulseRadio = $('input[name="<%= SettingsTabModel.FormFields.EnablePulse %>"]');
        var canEditParticipantSettings = <%= SettingsTab.CanEditParticipantSettings.ToJSTrueFalse() %>;
        var canEditCoachAI = <%= SettingsTab.CanEditCoachAI.ToJSTrueFalse() %>;

        $(document).ready(function () {

          btnUpdateSettings.click(UpdateSettings);

          selSubscriptionId.change(EnableOrDisableSubscriptionOptions);
          EnableOrDisableSubscriptionOptions();

          selSubscriptionQuoteItemId.change(GetAvailableSubscriptionsInQuote);
          GetAvailableSubscriptionsInQuote();

          settingsForm.find('.btn-group-toggle .btn').click(function () {
            // Remove the 'active' class from all buttons in the group
            $(this).siblings().removeClass('active');
            // Add the 'active' class to the clicked button
            $(this).addClass('active');
            // Update the checked state of the radio button
            $(this).find('input[type="radio"]').prop('checked', true);
          });

        }); // ready.

        function GetAvailableSubscriptionsInQuote() {
          const selectedOption = selSubscriptionQuoteItemId.find("option:selected");
          const selectedQuoteItemValue = selectedOption.val();
          const allowedSubscriptionId = String(selectedOption.data("subscriptionid"));
          const currentSubscriptionId = selSubscriptionId.val();
          const fallbackSubscriptionId = "<%= CoacheeInfo.UserSubscription?.SubscriptionId %>";
          const SubscriptionId_FoundationFree = "<%= ConfigHelper.SubscriptionId.FoundationFree %>";
          // Build the allowed subscriptions, including what we're about to select
          const allowedSubscriptionIds = new Set();
          let newSubscriptionId;
          if (selectedQuoteItemValue) {
            newSubscriptionId = allowedSubscriptionId;
          } else {
            allowedSubscriptionIds.add(SubscriptionId_FoundationFree);
            newSubscriptionId = fallbackSubscriptionId;
          }
          allowedSubscriptionIds.add(newSubscriptionId);
          // Enable/disable options based on allowed set
          selSubscriptionId.find("option").each(function () {
            const $opt = $(this);
            const val = $opt.val();
            $opt.prop("disabled", !allowedSubscriptionIds.has(val));
          });
          // Set selected value if it's allowed
          if (allowedSubscriptionIds.has(newSubscriptionId)) {
            selSubscriptionId.val(newSubscriptionId).trigger("change.select2");
          } else {
            selSubscriptionId.val("").trigger("change.select2");
          }
        }

        function EnableOrDisableSubscriptionOptions() {
          // Check data attributes of selected subscription.
          var hasNudges = selSubscriptionId.find('option:selected').data('<%= SettingsTabModel.SubscriptionsDataAttr.HasNudges %>');
          var hasAICoaching = selSubscriptionId.find('option:selected').data('<%= SettingsTabModel.SubscriptionsDataAttr.HasAICoaching %>');
          var hasPulse = selSubscriptionId.find('option:selected').data('<%= SettingsTabModel.SubscriptionsDataAttr.HasPulse %>');

          // Besides subscription selection, check rights to edit each factor from backend.
          if (!canEditCoachAI) hasAICoaching = false;
          if (!canEditParticipantSettings) {
            hasMicroLearnings = false;
            hasNudges = false;
            hasPulse = false;
          }

          // Add or remove 'disabled' class to the elements according to subscription attributes.
          enableNudgesRadio.prop("disabled", !hasNudges);
          enableNudgesRadio.closest('.btn-group-toggle').toggleClass('disabled', !hasNudges);

          enableAICoachingRadio.prop("disabled", !hasAICoaching);
          enableAICoachingRadio.closest('.btn-group-toggle').toggleClass('disabled', !hasAICoaching);

          enablePulseRadio.prop("disabled", !hasPulse);
          enablePulseRadio.closest('.btn-group-toggle').toggleClass('disabled', !hasPulse);
        }


        function UpdateSettings(evt) {
          var thisBtn = $(evt.target);

          AjaxSubmit({
            form: settingsForm,
            action: "<%= AjaxAction.UpdateSettings %>",
            onSuccess: function (jqXHR, data) {
              var canUpdateSubscription = data["<%= SettingsTabModel.AjaxReturnData.CanUpdateSubscription %>"] === "true";
              if (!canUpdateSubscription) {
                selSubscriptionQuoteItemId.prop('disabled', true);
                selSubscriptionId.prop('disabled', true);
              }
              settingsForm.data("savedData", settingsForm.serialize());
            },
            onFail: function (jqXHR, data) {
            },
            onError: function (jqXHR, textStatus, errorThrown) {
              common_InfoDialog("Update failed, please try again later.");
            },
            onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
          });
        }

      })(jQuery);

    </script>

  <% } %>

</asp:Content>
