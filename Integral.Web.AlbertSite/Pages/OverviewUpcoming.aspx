<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="OverviewUpcoming.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.OverviewUpcoming"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <style>
    .show-more-row { background-color: #f5f8fa8f; color: #6a7283; font-size: 1rem; font-weight: 700; }
    .show-more-row:hover { background-color: #f5f8fa; }
    #persistentModal .action-info { font-size: 19px; margin-bottom: 15px; }
    #persistentModal .modal-dialog { display: flex; align-items: center; justify-content: center; height: 100%; }
    #persistentModal .modal-body iframe { height: 70vh; width: 100%; border: none; }
  </style>

  <% if (!ActionsRequiredByPartnerForHtml.IsNullOrEmpty()) { %>
    <%= GetPartnerActionCards() %>
  <% } %>

  <% if (CanViewPartnerMetrics) { %>
    <div class="table-title">Metrics</div>
    <div class="flex-wrap gap10 mb25"><%= GetPartnerMetricsHtml() %></div>
  <% } %>

  <% if (CanViewOrgMetrics && CompanyInfo != null) { %>

    <div class="table-title">Organisation Metrics</div>
    <div class="flex flex-wrap gap15 mb25">
      <%= WebHelper.GetPeopleMetrics(CompanyInfo) %>
      <%= WebHelper.GetCompanyLeadBox(CompanyInfo) %>
    </div>

    <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
      Url = PathHelper.Partials.ActivityChartForCompany(CompanyInfo.CompanyId),
      InitialWidth = "100%",
      InitialHeight = "320px",
      DeferInitialLoad = false,
      InitialStyle = WebHelper.PartialLoaderStyle.Blank,
      LoaderStyle = WebHelper.PartialLoaderStyle.Chart
    }) %>

  <% } %>

  <% if (!PendingActionsRows.IsNullOrEmpty()) { %>

    <div class="table-title">Actions</div>

    <div class="table-responsive">
      <table class="tblCoachees table table-bordered table-hover table-rowlink">
        <thead>
          <tr>
            <% if (!ActionsRequiredByPartnerForHtml.IsNullOrEmpty()) { %>
              <th class="type-eventtypeicon"></th>
            <% } %>
            <th class="type-action-title">Action</th>
            <th class="type-action-description">Details</th>
             <% if (!ActionsRequiredByPartnerForHtml.IsNullOrEmpty()) { %>
               <th class="type-eventtypeicon"></th>
             <% } %>
          </tr>
        </thead>
        <tbody>
          <%= PendingActionsRows %>
        </tbody>
      </table>
    </div>

  <% } %>

  <% if (CanViewClientProgramSummary && ClientProgramSummary != null) { %>

    <div class="table-title">Program Summary</div>

    <div class="table-responsive">
      <table class="tblCoachees table table-bordered table-hover table-rowlink tblSessions" data-rowlink-url="<%= PathHelper.Pages.ProgramOverview(null) %>">
        <thead>
          <tr>
            <th class="type-project-name">Project Name</th>
            <th class="type-program-name">Program Name</th>
            <th class="type-date-range">Date</th>
            <th class="type-status">Status</th>
            <th class="type-qty has-subtitle">Participants<p>Coachees / All</p></th>
            <th class="type-qty has-subtitle">Coaching Sessions<p>Complete / Total</p></th>
            <th class="type-qty has-subtitle">Workshops<p>Complete / Total</p></th>
            <th class="type-qty has-subtitle">Evaluations<p>Overall Score</p></th>
            <th class="type-revenueprogress"><%= WebHelper.GetRevenueCompletionColTitle() %></th>
          </tr>
        </thead>
        <tbody>
          <% foreach (var program in ClientProgramSummary) { %>
            <tr tabindex="0" class="rowData" data-rowlink-id="<%= program.ProgramJobId %>">
              <td class="type-project-name"><%= program.ProjectName.HTMLEncode() %></td>
              <td class="type-program-name"><%= program.ProgramName.HTMLEncode() %></td>
              <td class="type-date-range"><%= WebHelper.DateRangeForTable_UtcToUserTime(program.StartDateUtc, program.EndDateUtc, true) %></td>
              <td class="type-qty"><%= WebHelper.GetProgramStatusBadge(program.ProgramStatusId) %></td>
              <td class="type-qty"><%= program.Participants == 0 ? "-" : (program.Coachees + " / " + program.Participants) %></td>
              <td class="type-qty"><%= program.SessionsAllocated == 0 ? "-" : (program.SessionsCompleted + " / " + program.SessionsAllocated) %></td>
              <td class="type-qty"><%= program.Workshops == 0 ? "-" : (program.WorkshopsCompleted + " / "+ program.Workshops) %></td>
              <td class="type-qty"><%= program.EvalScoreAvg.ToString("#.0", "-") %></td>
              <td class="type-revenueprogress"><%= WebHelper.GetProgressBarHtml(program.CompletedRevenueAmt, program.TotalRevenueAmt, "", WebHelper.ProgressBarType.CurrencyRoundToDollars) %></td>
            </tr>
          <% } %>
          <% if (ClientProgramSummaryTotalRows > ClientProgramSummaryMaxRows) { %>
            <tr tabindex="0" data-rowlink-url="<%= PathHelper.Pages.Projects_List() %>" class="show-more-row"><td class="show-more align-center" colspan="50">Show more</td></tr>
          <% } %>
        </tbody>
      </table>
    </div>

  <% } %>

  <% if (!SessionList.IsNullOrEmpty()) { %>

    <div class="table-title">Coaching Sessions</div>

    <div class="table-responsive">
      <table class="tblCoachees table table-bordered table-hover table-rowlink tblSessions" data-rowlink-url="">
        <thead>
          <tr>
            <th class="type-datetime">Date & Time</th>
            <th class="type-fullname">Coachee</th>
            <th class="type-project-name">Project</th>
            <th class="type-status">Info</th>
            <th class="type-delivery">Type</th>
            <th class="type-money">Delivery</th>
          </tr>
        </thead>
        <tbody>
          <% foreach (var coachingSession in SessionList) { %>
            <tr tabindex="0" class="rowData" data-rowlink-id="<%= PathHelper.Pages.CoacheeEdit(coachingSession.CoacheeId, PathHelper.CoacheeTabEnum.summary) %>">
              <td class="type-datetime"><%= coachingSession.GetApptDateInCoachTZ().ToString("d MMM yyyy, h:mm tt") %></td>
              <td class="type-fullname">
                <%= WebHelper.GetAvatarForTable_Participant(PathHelper.Images.UserPhoto(coachingSession.CoacheeFirstName, coachingSession.CoacheeLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true), (coachingSession.CoacheeFirstName + " " + coachingSession.CoacheeLastName).HTMLEncode(), coachingSession.ProgramJobName, coachingSession.CoacheeId) %>
              </td>
              <td class="type-project-name">
                <%= WebHelper.GetListViewLocatorColumnHtml(coachingSession.ProjectName.HTMLEncode(), coachingSession.ProgramJobNumber.HTMLEncode(), coachingSession.CompanyName.HTMLEncode()) %>
              </td>
              <td class="type-status"><%= WebHelper.ParticipantActivities.GetBadgeParticipantUserActivityInfo(coachingSession.UserActivity, coachingSession.UserSubscription) %></td>
              <td class="type-delivery"><%= WebHelper.GetDeliveryBadge(coachingSession.SessionTypeInPerson) %></td>
              <td class="type-money"><%= GetCoacheeDeliveryAmount(coachingSession).ToString("C") %></td>
            </tr>
          <% } %>
        </tbody>
      </table>
    </div>

  <% } %>

  <% if (!WorkshopItemsList.IsNullOrEmpty()) { %>

    <div class="table-title">Workshops</div>

    <div class="table-responsive">
      <table class="tblWorkshops table table-bordered table-hover table-rowlink">
        <thead>
          <tr>
            <th class="type-datetime">Date & Time</th>
            <th class="type-workshop-title">Title</th>
            <th class="type-project-name">Project</th>
            <th class="type-status">Status</th>
            <th class="type-delivery">Type</th>
            <th class="type-money">Delivery</th>
          </tr>
        </thead>
        <tbody>
          <% foreach(var workshopEvent in WorkshopItemsList) { %>
            <tr tabindex="0" class="rowData gotodetailpage" data-href="<%= PathHelper.Pages.Workshops_Edit(workshopEvent.ProgramJobId, workshopEvent.WorkshopEventId) %>">
              <td class="type-datetime"><%= GetStartTime(workshopEvent) %></td>
              <td class="type-workshop-title"><%= WebHelper.GetListViewMainColumnText(workshopEvent.WorkshopTitle.HTMLEncode(), workshopEvent.ProgramJobName.HTMLEncode()) %></td>
              <td class="type-project-name">
                <%= WebHelper.GetListViewLocatorColumnHtml(workshopEvent.ProjectName.HTMLEncode(), workshopEvent.ProgramJobNumber.HTMLEncode(), workshopEvent.CompanyName.HTMLEncode()) %>
              </td>
              <td class="type-status"><%= WebHelper.GetStatusBadge(workshopEvent.WorkshopStatusName.HTMLEncode()) %></td>
              <td class="type-delivery"><%= WebHelper.GetDeliveryBadge(!workshopEvent.IsVirtual) %> </td>
              <td class="type-money"><%= GetWorkshopDeliveryAmount(workshopEvent).ToString("C") %></td>
            </tr>
          <% } %>
        </tbody>
      </table>
    </div>

  <% } %>

  <% if (!ConsultingItemsList.IsNullOrEmpty()) { %>

    <div class="table-title">Consulting Items</div>

    <div class="table-responsive">
      <table class="tblConsulting table table-bordered table-hover table-rowlink">
        <thead>
          <tr>
            <th class="type-datetime">Completion</th>
            <th class="type-description">Description</th>
            <th class="type-project-name">Project</th>
            <th class="type-money">Delivery</th>
          </tr>
        </thead>
        <tbody>
          <% foreach (var consultingItem in ConsultingItemsList) { %>
            <tr tabindex="0" class="rowData gotodetailpage" data-href="<%= PathHelper.Pages.Consulting_Edit(consultingItem.ProgramJobId, consultingItem.ConsultingItemId) %>">
              <td class="type-datetime"><%= consultingItem.CompletionDateUtc.UtcToTZOrNull(ConfigHelper.DefaultTimeZoneInfo).ToString("d MMM yyyy") %></td>
              <td class="type-description"><%= WebHelper.GetListViewMainColumnText(consultingItem.ItemTitle.HTMLEncode(), consultingItem.JobName.HTMLEncode()) %></td>
              <td class="type-project-name">
                <%= WebHelper.GetListViewLocatorColumnHtml(consultingItem.ProjectName.HTMLEncode(), consultingItem.JobNumber.HTMLEncode(), consultingItem.CompanyName.HTMLEncode()) %>
              </td>
              <td class="type-money"><%= GetConsultingDeliveryAmount(consultingItem).ToString("C") %></td>
            </tr>
          <% } %>
        </tbody>
      </table>
    </div>

  <% } %>

  <% if (!SalesItemsList.IsNullOrEmpty()) { %>

    <div class="table-title">Sales Revenue</div>

    <div class="table-responsive">
      <table class="tblSales table table-bordered" data-rowlink-url="<%= PathHelper.Pages.ProgramOverview() %>">
        <thead>
          <tr>
            <th class="type-datetime">Date</th>
            <th class="type-description">Description</th>
            <th class="type-project-name">Project</th>
            <th class="type-money">Revenue</th>
          </tr>
        </thead>
        <tbody>
          <% foreach(var salesItem in SalesItemsList) { %>
            <tr tabindex="0" class="rowData" data-rowlink-id="<%= salesItem.ProgramJobId %>">
              <td class="type-datetime"><%= salesItem.ItemDateUtc.UtcToTZ(ConfigHelper.DefaultTimeZoneInfo).ToString("d MMM yyyy") %></td>
              <td class="type-description"><%= WebHelper.GetListViewMainColumnText(salesItem.ItemName.HTMLEncode(), salesItem.JobName.HTMLEncode()) %></td>
              <td class="type-project-name">
                <%= WebHelper.GetListViewLocatorColumnHtml(salesItem.ProjectName.HTMLEncode(), salesItem.JobNumber.HTMLEncode(), salesItem.CompanyName.HTMLEncode()) %>
              </td>
              <td class="type-money"><%= salesItem.SalesRevenue.ToString("C") %></td>
            </tr>
          <% } %>
        </tbody>
      </table>
    </div>

  <% } %>

  <% if (!PLCItemsList.IsNullOrEmpty()) { %>

    <div class="table-title">PLC Revenue</div>

    <div class="table-responsive">
      <table class="tblPLC table table-bordered">
        <thead>
          <tr>
            <th class="type-datetime">Date</th>
            <th class="type-description">Description</th>
            <th class="type-project-name">Project</th>
            <th class="type-money">Revenue</th>
          </tr>
        </thead>
        <tbody>
          <% foreach(var plcItem in PLCItemsList) { %>
            <tr tabindex="0" class="rowData" data-rowlink-id="<%= plcItem.ProgramJobId %>">
              <td class="type-datetime"><%= plcItem.ItemDateUtc.UtcToTZ(ConfigHelper.DefaultTimeZoneInfo).ToString("d MMM yyyy") %></td>
              <td class="type-description"><%= WebHelper.GetListViewMainColumnText(plcItem.ItemName.HTMLEncode(), plcItem.JobName.HTMLEncode()) %></td>
              <td class="type-project-name">
                <%= WebHelper.GetListViewLocatorColumnHtml(plcItem.ProjectName.HTMLEncode(), plcItem.JobNumber.HTMLEncode(), plcItem.CompanyName.HTMLEncode()) %>
              </td>
              <td class="type-money"><%= plcItem.PLCRevenue.ToString("C") %></td>
            </tr>
          <% } %>
        </tbody>
      </table>
    </div>

  <% } %>

  <% if (ShowBlockingModal) { %>
    <!-- Persistente Modal that would block users from navigating until they complete the action indicated (Depends on the role) -->
    <div class="modal fade" id="persistentModal" tabindex="-1" aria-hidden="true" data-bs-backdrop="static" data-bs-keyboard="false">
      <div class="modal-dialog modal-lg" id="modalDialog">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title" id="modalTitle">Action Required</h5>
          </div>
          <div class="modal-body" id="modalBody">
            <%= GetPersistentModalHtml() %>
          </div>
        </div>
      </div>
    </div>
  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var selPayRuns, ShowBlockingModal;

      $(document).ready(function() {

        selPayRuns = $("#selPayRuns");
        ShowBlockingModal = <%= ShowBlockingModal.ToJSTrueFalse() %>;

        if (ShowBlockingModal) {
          $('#persistentModal').modal({
            backdrop: 'static', // Prevent closing by clicking outside the modal
            keyboard: false     // Prevent closing by pressing the Esc key
          });
        }

        selPayRuns.change(function (e) {
          var payRunId = selPayRuns.val();
          if (payRunId == "")
            location.href = "<%= PathHelper.Pages.CoachPayRuns(OverviewCoachInfo.UserId) %>";
          else
            location.href = "<%= PathHelper.Pages.CoachPayRuns(OverviewCoachInfo.UserId, null) %>" + payRunId;
        });

        $(".gotodetailpage").click(function (evt) {
          // If a link in the row is clicked, don't redirect to row path.
          if (!$(evt.target).is("a")) {
            window.location = $(this).data("href");
          }
        });

        // Functionality only for Admins/Partners
        <% if (SessionHelper.IsUserRoleAdmin || SessionHelper.IsUserRoleCoach) { %>
          $(".table").each(function () {
            var $table = $(this);
            var $rows = $table.find("tr");
            var maxRowsPerTable = <%= ConfigHelper.MaxRowsPerTable_OverviewUpcoming %>;
            if ($rows.length > maxRowsPerTable) {
              $rows.slice(maxRowsPerTable).addClass('hidden extraRow');
              $table.append('<tr class="show-more-row"><td class="show-more align-center" colspan="6">Show more</td></tr>');
            }
          });

          $('.show-more').on('click', function () {
            var $this = $(this);
            var $table = $this.closest("table");
            var $extraRows = $table.find(".extraRow");

            if ($extraRows.is(":visible")) {
              $extraRows.addClass('hidden');
              $this.text("Show more");
            } else {
              $extraRows.removeClass('hidden');
              $this.text("Show less");
            }
          });
        <% } %>

      }); // ready.

    })(jQuery);
  </script>

</asp:Content>
