<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="BookSession.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.BookSession"
  MasterPageFile="~/MasterPages/Public.Master" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Integrations" %>

<asp:Content ContentPlaceHolderID="BodyContent" runat="server">

  <div class="container">

    <div class="flex-fill flex-column">

      <div class="page-top"></div>

      <div class="flex1 page-body">

        <% if (!ProgramInfo.BookingPageInstructions.IsNullOrEmpty()) { %>
          <div class="instructions">
            <%= ProgramInfo.BookingPageInstructions %>
          </div>
        <% } %>

        <div class="flex-fill h100p body-content">

          <% if (CanBookSession) { %>
            <div class="body-sidebar">
              <div class="project-logo"><img src="<%= PathHelper.Images.ProjectLogo(ProjectInfo, false, false) %>" alt="Project Logo"></div>
              <div class="program-name"><%=  ProjectInfo.FriendlyProjectTitle.ValueIfNullOrEmpty(ProjectInfo.ProjectName) %></div>
              <div class="coach-info">
                <%= WebHelper.GetProfileImage(PathHelper.Images.UserPhoto(CoachInfo, PathHelper.Images.UserPhotoSize.Large, true)) %>
                <div class="coach-name"><%= CoachInfo.GetFullName() %></div>
              </div>
              <div class="session-info"></div>
            </div>
          <% } %>

          <div class="body-main">

            <% if (CanBookSession) { %>

              <div class="flex flex-align-center flex-justify-center h100p hidden all-booked-message">
                <div class="align-center">
                  All your sessions are booked.<br />
                  Please check your confirmation emails if you need to reschedule or cancel.
                </div>
              </div>

              <%-- See: https://help.calendly.com/hc/en-us/articles/223147027-Embed-options-overview?tab=advanced --%>
              <div class="calendly-inline-widget" id="calendly-inline-widget" data-auto-load="false">
              </div>

            <% } else { %>

              <div class="no-coaching-notice">

                <b>Hi <%= CoacheeInfo.FirstName.HTMLEncode() %>!</b><br />
                <br />
                Unable to book a session at this time.<br/>
                <br/>
                <% if (!CoacheeInfo.HasCoaching || !CoacheeInfo.IsCoachAssigned || CoachInfo == null) { %>
                  Either your Program has not yet started, or you are yet to be assigned a Coach.<br/>
                  <br/>
                  If you think this is incorrect, please contact us on <a href="mailto:coordination@integral.global">coordination@integral.global</a>.<br/>
                <% } else { %>
                  Either your Program is not completely set up, or has not yet started.<br/>
                  <br/>
                  If you think this is incorrect, please contact your coach:<br/>
                  <%= CoachInfo.GetFullName().HTMLEncode() %><br/>
                  <a href="mailto:<%= CoachInfo.EmailAddress.HTMLEncode() %>"><%= CoachInfo.EmailAddress.HTMLEncode() %></a><br/>
                <% } %>
                <br/>
                All the best,<br/>
                The team at Able.<br/>

              </div>

            <% } %>

          </div><%-- flex1 --%>
        </div><%-- flex-fill --%>

      </div><%-- flex1 page-body --%>

      <div class="page-footer">

        <div class="flex flex-align-end mt15">
          <span>Powered by</span>
          <img src="<%= PathHelper.Images.AbleHeaderLogo() %>" alt="Able" class="h20 ml5 mb2" />
        </div>

      </div>

    </div><%-- flex-column --%>

  </div><%-- content --%>

  <script type="text/javascript" src="https://assets.calendly.com/assets/external/widget.js"></script>

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

          $sessionInfo.text("Session " + firstFreeSessionNumber + " of " + sessionsAllocated + " (" + sessionDurations[firstFreeSessionNumber - 1] + " mins)");

          if (calendlyWidgetExists) {

            $(".body-main").busyLoad("show");

            Calendly.initInlineWidget({
              url: '<%= Calendly.BookingUrlDomain %>/' + coachCalendlyUrlName + '/' + sessionEventNames[firstFreeSessionNumber - 1] + '?hide_gdpr_banner=1&primary_color=634cff&hide_event_type_details=1&hide_landing_page_details=1',
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

          $(".body-main").busyLoad("hide"); // Make sure interstitial is hidden regardless of event type.

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
