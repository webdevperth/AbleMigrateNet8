<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ParticipantCoaching.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ParticipantCoaching"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Integrations" %>

<asp:Content ContentPlaceHolderID="BodyContent" runat="server">

  <% if (PageMode == PageModeEnum.BookSession) { %>

    <div class="container ml0 coaching-container">
      <div class="coaching-info">

        <% if (CanViewCoachInfo) { %>
          <div class="coach-profile-info">
            <div class="profile-image">
              <%= WebHelper.GetProfileImageWithSlideout(PathHelper.Images.UserPhoto(CoachInfo, PathHelper.Images.UserPhotoSize.Thumbnail, true), CoachInfo.UserId) %>
            </div>

            <div class="profile-info">
              <% if (ProjectInfo != null) { %>
                <h4 class="project-name"><%= ProjectInfo?.FriendlyProjectTitle.ValueIfNullOrEmpty(ProjectInfo.ProjectName).HTMLEncode() %></h4>
              <% } %>

              <div class="coach-name"><%= CoachInfo.GetFullName().HTMLEncode() %></div>
               <% if (CanBookSession) { %>
                 <div class="session-info"></div>
               <% } %>
            </div>
          </div>
        <% } %>

        <%= GetBookingMessageHtml() %>

        <% if (CanBookSession && !CanViewCoachInfo) { %>
          <div class="session-info"></div>
        <% } %>
      </div>

      <div class="main-content">

        <% if (CanViewEventSessions) { %>
          <div class="sessions-container">
            <h3 class="sessions-title">Your Sessions</h3>
            <div class="session-list">
              <%= GetBookedSessionsHtml(CoacheeInfo) %>
            </div>
          </div>
        <% } %>

        <% if (CoacheeInfo != null && CoacheeInfo.HasCoaching && CanBookSession && !UserIsNotInAProgram && !HasBookedAllSessions) { %>
          <%-- See: https://help.calendly.com/hc/en-us/articles/223147027-Embed-options-overview?tab=advanced --%>
          <div class="iframe-container">
            <div class="calendly-inline-widget" id="calendly-inline-widget" data-auto-load="false"></div>
          </div>
        <% } %>

      </div>
    </div>

  <% } else if (PageMode == PageModeEnum.SelfCoachSelection) { %>

    <% if (CoachesList.IsNullOrEmpty()) { %>

      <%= WebHelper.GetNoRecordsBadge("No coaches available at this time.") %>

    <% } else { %>

      <h3 class="text-muted">Select your Coach</h3>
      <div class="flex-wrap gap25">
        <%= GetTeamCardsHtml() %>
      </div>

    <% } %>

  <% } %>

  <script type="text/javascript" src="https://assets.calendly.com/assets/external/widget.js"></script>

  <script type="text/javascript">
    (function($) {

      $(document).ready(function() {

        <% if (PageMode == PageModeEnum.BookSession) {  %>

          $("body").addClass("page-booksession");

           <% if (TriggerCoachSlideout) { %>
            setTimeout(function () {
              $(".nav-main .partner-contact-slideout-trigger").trigger("click"); // Trigger click on Partners avatar to open slideout.
            }, 1000);
          <% } %>

        <% } else if (PageMode == PageModeEnum.SelfCoachSelection) { %>

        <% } %>

      }); // ready.

    })(jQuery);
  </script>

  <% if (CanBookSession) { %>

    <script>
      (function ($) {

        var $sessionInfo = $(".session-info");
        var calendlyWidgetExists = $("#calendly-inline-widget").length == 1;

        // Initial session info.
        var firstFreeSessionNumber = <%= FirstFreeSessionNumber %>;
        var sessionsAllocated = <%= CoacheeInfo.UserActivity?.SessionsAllocated %>;
        var sessionDurations = JSON.parse("[<%= CoacheeInfo.GetCoachingType().GetDurations(CoacheeInfo.UserActivity.SessionsAllocated).ToStringList() %>]");
        var sessionEventNames = "<%= CoacheeInfo.GetCoachingType().GetEventNames(CoacheeInfo.UserActivity.SessionsAllocated).Join(",") %>".split(",");
        var coachCalendlyUrlName = "<%= CoachInfo.CalendlyUrlName %>";

        $(document).ready(function () {

          // Listen to messages from Calendly iframe.
          window.addEventListener("message", (evt) => { CheckCalendlyEvent(evt); }, false);

          ShowSessionInfo();

        });

        function ShowSessionInfo() {

          if (calendlyWidgetExists) $("#calendly-inline-widget").empty();

          window.scrollTo(0, 0);

          if (<%= HasBookedAllSessions.ToJSTrueFalse() %>) {
            $sessionInfo.text("All Sessions Booked.");
            $(".all-booked-message").removeClass("hidden");
            return;
          }

          $sessionInfo.text("Now booking session " + firstFreeSessionNumber + " of " + sessionsAllocated + " (" + sessionDurations[firstFreeSessionNumber - 1] + " mins)");

          if (calendlyWidgetExists) {

            Calendly.initInlineWidget({
              url: '<%= Calendly.BookingUrlDomain %>/' + coachCalendlyUrlName + '/' + sessionEventNames[firstFreeSessionNumber - 1]
                + '?hide_gdpr_banner=1&primary_color=634cff&hide_event_type_details=1&hide_landing_page_details=1'
                + '&name=<%= CoacheeInfo.GetFullName().JSEncode() %>&email=<%= CoacheeInfo.EmailAddress.JSEncode() %>',
              parentElement: document.getElementById('calendly-inline-widget'),
              prefill: {
                name: "<%= CoacheeInfo.GetFullName().HTMLEncode() %>",
                email: "<%= CoacheeInfo.EmailAddress.HTMLEncode() %>"
              }
            });
          }
        }

        function CheckCalendlyEvent(evt) {

          if (!IsCalendlyEvent(evt)) return;

          // If Calendly booking is complete, move to next session if there is one.
          if (evt.data.event == '<%= Calendly.GetEmbedWindowMessageString(Calendly.EmbedWindowMessageEnum.event_scheduled) %>') {
            firstFreeSessionNumber++;
            ShowBookedMessage();
            ShowSessionInfo();
            return;
          }
        }

        function IsCalendlyEvent(evt) {
          return evt.origin === "<%= Calendly.BookingUrlDomain.ToLower() %>" && evt.data.event && evt.data.event.indexOf("calendly.") === 0;
        }

        // Note only use BootstrapDialog for a mobile-friendly dialog.
        function ShowBookedMessage() {
          BootstrapDialog.alert({
            title: 'Booking Successful',
            message: (firstFreeSessionNumber <= sessionsAllocated ? 'You may also continue to book your next session.' : 'All your sessions have now been booked.'),
            type: BootstrapDialog.TYPE_SUCCESS,
            closable: true,
            onshown: function (dialogRef) {
              dialogRef.getModalFooter().find('.btn-default').focus();
            },
          });
        }

      })(jQuery);
    </script>

  <% } %>

</asp:Content>
