<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProgramOverview.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProgramOverview"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (CoacheeList.IsNullOrEmpty() && WorkshopList.IsNullOrEmpty() && ConsultingItemList.IsNullOrEmpty() && CostItemList.IsNullOrEmpty()) { %>

    <%= WebHelper.GetNoRecordsBadge() %>

  <% } %>

  <div class="table-title">Quality</div>
  <div class="flex flex-stretch gap20">

    <%= WebHelper.GetOverviewScoreBox(
          titleText:        "Overall Evaluation",
          titleTooltipText: "All Evaluations in this Program",
          customClass:      null,
          linkUrl:          EvalScores.OverallCount == 0 ? null : PathHelper.Reports.EvalViewer(ProgramInfo, ConfigHelper.EvalType.All),
          scores:       new WebHelper.OverviewBoxScore(EvalScores.Overall.ToString("0.0"), "/ 10")) %>

    <%= EvalScores.EndProgram == 0 ? "" :
        WebHelper.GetOverviewScoreBox(
          titleText:        "Program",
          titleTooltipText: "End-of-Program Evaluations",
          customClass:      null,
          linkUrl:          EvalScores.EndProgramCount == 0 ? null : PathHelper.Reports.EvalViewer(ProgramInfo, ConfigHelper.EvalType.PostProgram),
          scores:       new WebHelper.OverviewBoxScore(EvalScores.EndProgram.ToString("0.0"), "/ 10")) %>

    <%= EvalScores.Workshops == 0 ? "" :
        WebHelper.GetOverviewScoreBox(
          titleText:        "Workshops",
          titleTooltipText: "<center><p>Evaluation Results for Workshop Sessions.</p><p>For individual workshop scores see below.</p></center>",
          customClass:      null,
          linkUrl:          EvalScores.WorkshopsCount == 0 ? null : PathHelper.Reports.EvalViewer(ProgramInfo, ConfigHelper.EvalType.Workshop),
          scores:       new WebHelper.OverviewBoxScore(EvalScores.Workshops.ToString("0.0"), "/ 10")) %>

    <%= EvalScores.Coaching == 0 ? "" :
        WebHelper.GetOverviewScoreBox(
          titleText:        "Coaching",
          titleTooltipText: "Evaluation Results for Coaching Sessions and Program",
          customClass:      null,
          linkUrl:          EvalScores.CoachingCount == 0 ? null : PathHelper.Reports.EvalViewer(ProgramInfo, ConfigHelper.EvalType.Coaching),
          scores:       new WebHelper.OverviewBoxScore(EvalScores.Coaching.ToString("0.0"), "/ 10")) %>

    <%= GetPrePostSurveyBox() %>

    <%= GetProjectLeadDisplay() %>

  </div>
  <br/>
  <% if (!CoacheeList.IsNullOrEmpty()) { %>
    <div class="table-title type-coachees">Participants</div>
    <div class="table-responsive">
      <table class="tblCoachees table table-bordered table-hover table-rowlink" <%= CanViewParticipants ? $"data-rowlink-url=\"{GetParticipantRowLinkUrl()}\"" : "" %>>
        <thead>
          <tr>
            <th class="">Name</th>
            <th class="type-status">Info</th>
            <th class="type-progress">Sessions</th>
            <th class="type-delivery">Type</th>
            <th class="type-user-nameWithAvatar">Partner</th>
            <% if (CanViewTotalRevenue) { %>
              <th class="type-money align-center"><%= RevenueTextDisplay %></th>
            <% } %>
            <% if (CanViewAllDeliveryTeamRevenue) { %>
              <th class="type-money align-center">Partner</th>
              <th class="type-money align-center <%= colHideSales %>">Sales</th>
              <th class="type-money align-center <%= colHidePLC %>">PLC</th>
            <% } %>
            <% if (CanViewPartnerRevenue) { %>
              <th class="type-money align-center">Revenue</th>
            <% } %>
          </tr>
        </thead>
        <tbody>
          <% foreach (var coachee in CoacheeList) {%>
            <tr tabindex="0" class="rowData" <%= GetRowAttribute(coachee) %>>
              <td class="">
                <%= WebHelper.GetAvatarForTable_Participant(PathHelper.Images.UserPhoto(coachee.FirstName, coachee.LastName, PathHelper.Images.UserPhotoSize.Thumbnail, true), coachee.GetFullName(), coachee.EmailAddress, coachee.CoacheeId) %>
              </td>
              <td class="type-status"><%= WebHelper.ParticipantActivities.GetBadgeParticipantUserActivityInfo(coachee.UserActivity, coachee.UserSubscription) %></td>
              <td class="type-progress"><%= WebHelper.GetProgressBarHtml(coachee.UserActivity.SessionsCompleted, coachee.UserActivity.SessionsAllocated) %></td>
              <td class="type-delivery"><%= WebHelper.GetDeliveryBadge(GetDeliveryType(coachee)) %></td>
              <td class="type-user-nameWithAvatar">
                <%= WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(coachee, PathHelper.Images.UserPhotoSize.Thumbnail, true),
                        coachee.UserActivity.CoachFullName, coachee.CoachUserId) %>
              </td>
              <% if (CanViewTotalRevenue) { %>
                <td class="type-money"><%= coachee.CoachingRevenue.GetValueOrDefault(0).ToString("C") %></td>
              <% } %>
              <% if (CanViewAllDeliveryTeamRevenue) { %>
                <td class="type-money" ><%= (coachee.CoachingRevenue * ProgramInfo.Partner_DeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money <%= colHideSales %>"><%= (coachee.CoachingRevenue * ProgramInfo.Partner_SalesDeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money <%= colHidePLC %>"><%= (coachee.CoachingRevenue * ProgramInfo.Partner_PLCPercentage).GetValueOrDefault(0).ToString("C") %></td>
              <% } %>
              <% if (CanViewPartnerRevenue) { %>
                <td class="type-money" ><%= WebHelper.GetPartnerRevenueValue(coachee.CoachingRevenue, ProgramInfo.Partner_DeliveryPercentage, coachee.CoachUserId == userInfo.UserId, CanViewAllDeliveryTeamRevenue) %></td>
              <% } %>
            </tr>
          <% } %>
        </tbody>
        <% if (CanViewTotalRevenue || CanViewAllDeliveryTeamRevenue) { %>
          <tfoot>
            <tr tabindex="-1">
              <td colspan="4" class="total-title">Coaching Total:</td>
              <% if (CanViewTotalRevenue) { %>
                <td class="type-money pr20 tfoot-total"><%= CoacheeTotalRevenue.ToString("C") %></td>
              <% } %>
              <% if (CanViewAllDeliveryTeamRevenue) { %>
                <td class="type-money tfoot-total"><%= (CoacheeTotalRevenue * ProgramInfo.Partner_DeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money tfoot-total <%= colHideSales %>"><%= (CoacheeTotalRevenue * ProgramInfo.Partner_SalesDeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money tfoot-total <%= colHidePLC %>"><%= (CoacheeTotalRevenue * ProgramInfo.Partner_PLCPercentage).GetValueOrDefault(0).ToString("C") %></td>
              <% } %>
            </tr>
          </tfoot>
        <% } %>
      </table>
    </div>
  <% } %>

  <% if (!WorkshopList.IsNullOrEmpty()) { %>
    <div class="table-title type-workshops">Workshops</div>
    <div class="table-responsive">
      <table class="tblWorkshops table table-bordered table-hover table-rowlink limitateNavigation" data-rowlink-url="<%= PathHelper.Pages.Workshops_Edit(ProgramInfo.ProgramJobId, null) %>">
        <thead>
          <tr>
            <th class="">Description</th>
            <th class="type-datetime-2lines">Date & Time</th>
            <th class="type-evalscore">Eval Score</th>
            <th class="type-status">Status</th>
            <th class="type-delivery">Type</th>
            <th class="type-user-nameWithAvatar">Partner</th>
            <% if (CanViewTotalRevenue) { %>
              <th class="type-money"><%= RevenueTextDisplay %></th>
            <% } %>
            <% if (CanViewAllDeliveryTeamRevenue) { %>
              <th class="type-money">Partner</th>
              <th class="type-money <%= colHideSales %>">Sales</th>
              <th class="type-money <%= colHidePLC %>">PLC</th>
            <% } %>
            <% if (CanViewPartnerRevenue) { %>
              <th class="type-money">Partner</th>
            <% } %>
          </tr>
        </thead>
        <tbody>
          <% foreach(var workshopEvent in WorkshopList) { %>
            <tr tabindex="0" class="rowData" data-rowlink-id="<%= workshopEvent.WorkshopEventId %>">
              <td><%= workshopEvent.WorkshopTitle.HTMLEncode() %></td>
              <td class="type-datetime-2lines"><%= GetWorkshopStartDateTimeHtml(workshopEvent) %></td>
              <td class="type-evalscore"><%= GetWorkshopEvalScore(workshopEvent) %></td>
              <td class="type-status"><%= WebHelper.GetStatusBadge(workshopEvent.WorkshopStatusName.HTMLEncode()) %></td>
              <td class="type-delivery"><%= WebHelper.GetDeliveryBadge(!workshopEvent.IsVirtual) %></td>
              <td class="type-user-nameWithAvatar">
                <%= WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(workshopEvent, PathHelper.Images.UserPhotoSize.Thumbnail, true),
                        workshopEvent.KeyFacilitatorFirstName + " " + workshopEvent.KeyFacilitatorLastName, workshopEvent.KeyFacilitatorUserId) %>
              </td>
              <% if(CanViewTotalRevenue) { %>
                <td class="type-money"><%= workshopEvent.WorkshopRevenue.GetValueOrDefault(0).ToString("C") %></td>
              <% } %>
              <% if (CanViewAllDeliveryTeamRevenue) { %>
                <td class="type-money"><%= (workshopEvent.WorkshopRevenue * ProgramInfo.Partner_DeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money <%= colHideSales %>"><%= (workshopEvent.WorkshopRevenue * ProgramInfo.Partner_SalesDeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money <%= colHidePLC %>  "><%= (workshopEvent.WorkshopRevenue * ProgramInfo.Partner_PLCPercentage).GetValueOrDefault(0).ToString("C") %></td>
              <% } %>
              <% if (CanViewPartnerRevenue) { %>
                <td class="type-money"><%= WebHelper.GetPartnerRevenueValue(workshopEvent.WorkshopRevenue, ProgramInfo.Partner_DeliveryPercentage, workshopEvent.KeyFacilitatorUserId == userInfo.UserId, CanViewAllDeliveryTeamRevenue) %></td>
              <% } %>
            </tr>
          <% } %>
        </tbody>
        <% if(CanViewTotalRevenue || CanViewAllDeliveryTeamRevenue) { %>
          <tfoot>
            <tr tabindex="-1">
              <td colspan="6" class="total-title">Workshops Total:</td>
              <% if(CanViewTotalRevenue) { %>
                <td class="type-money tfoot-total"><%= WorkshopTotalRevenue.ToString("C") %></td>
              <% } %>
              <% if (CanViewAllDeliveryTeamRevenue) { %>
                <td class="type-money tfoot-total"><%= (WorkshopTotalRevenue * ProgramInfo.Partner_DeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money tfoot-total <%= colHideSales %>"><%= (WorkshopTotalRevenue * ProgramInfo.Partner_SalesDeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money tfoot-total <%= colHidePLC %>"><%= (WorkshopTotalRevenue * ProgramInfo.Partner_PLCPercentage).GetValueOrDefault(0).ToString("C") %></td>
              <% } %>
            </tr>
          </tfoot>
        <% } %>
      </table>
    </div>
  <% } %>

  <% if (!ConsultingItemList.IsNullOrEmpty()) { %>
    <div class="table-title type-consulting">Consulting Items</div>
    <div class="table-responsive">
      <table class="tblConsulting table table-bordered table-hover table-rowlink limitateNavigation" data-rowlink-url="<%= PathHelper.Pages.Consulting_Edit(ProgramInfo.ProgramJobId, null) %>">
        <thead>
          <tr>
            <th class="">Description</th>
            <th class="type-date">Completion</th>
            <th class="type-user-nameWithAvatar">Partner</th>
            <% if(CanViewTotalRevenue) { %>
              <th class="type-money"><%= RevenueTextDisplay %></th>
            <% } %>
            <% if (CanViewAllDeliveryTeamRevenue) { %>
              <th class="type-money">Partner</th>
              <th class="type-money <%= colHideSales %>">Sales</th>
              <th class="type-money <%= colHidePLC %>">PLC</th>
            <% } %>
            <% if (CanViewPartnerRevenue) { %>
              <th class="type-money">Partner</th>
            <% } %>
          </tr>
        </thead>
        <tbody>
          <% foreach (var consultingItem in ConsultingItemList) { %>
            <tr tabindex="0" class="rowData" data-rowlink-id="<%= consultingItem.ConsultingItemId %>">
              <td class=""><%= consultingItem.ItemTitle.HTMLEncode() %></td>
              <td class="type-date"><%= consultingItem.CompletionDateUtc.UtcToTZOrNull(ConfigHelper.DefaultTimeZoneInfo).ToString("d MMM yyyy") %></td>
              <td class="type-user-nameWithAvatar">
                <%= WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(consultingItem, PathHelper.Images.UserPhotoSize.Thumbnail, true),
                        consultingItem.ConsultantFirstName + " " + consultingItem.ConsultantLastName, consultingItem.ConsultantUserId) %>
              </td>
              <% if (CanViewTotalRevenue) { %>
                <td class="type-money"><%= consultingItem.ItemAmount.ToString("C") %></td>
              <% } %>
              <% if (CanViewAllDeliveryTeamRevenue) { %>
                <td class="type-money"><%= (consultingItem.ItemAmount * ProgramInfo.Partner_DeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money <%= colHideSales %>"><%= (consultingItem.ItemAmount * ProgramInfo.Partner_SalesDeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money <%= colHidePLC %>"><%= (consultingItem.ItemAmount * ProgramInfo.Partner_PLCPercentage).GetValueOrDefault(0).ToString("C") %></td>
              <% } %>
              <% if (CanViewPartnerRevenue) { %>
                <td class="type-money"><%= WebHelper.GetPartnerRevenueValue(consultingItem.ItemAmount, ProgramInfo.Partner_DeliveryPercentage, consultingItem.ConsultantUserId == userInfo.UserId, CanViewAllDeliveryTeamRevenue) %></td>
              <% } %>
            </tr>
          <% } %>
        </tbody>
        <% if(CanViewTotalRevenue || CanViewAllDeliveryTeamRevenue) { %>
          <tfoot>
            <tr tabindex="-1">
              <td colspan="3" class="total-title">Consulting Total:</td>
              <% if(CanViewTotalRevenue) { %>
                <td class="type-money tfoot-total"><%# ConsultingTotalRevenue.ToString("C") %></td>
              <% } %>
              <% if (CanViewAllDeliveryTeamRevenue) { %>
                <td class="type-money tfoot-total"><%# (ConsultingTotalRevenue * ProgramInfo.Partner_DeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money tfoot-total <%= colHideSales %>"><%# (ConsultingTotalRevenue * ProgramInfo.Partner_SalesDeliveryPercentage).GetValueOrDefault(0).ToString("C") %></td>
                <td class="type-money tfoot-total <%= colHidePLC %>"><%# (ConsultingTotalRevenue * ProgramInfo.Partner_PLCPercentage).GetValueOrDefault(0).ToString("C") %></td>
              <% } %>
            </tr>
          </tfoot>
        <% } %>
      </table>
    </div>
  <% } %>

  <% if (!CostItemList.IsNullOrEmpty()) { %>
    <div class="table-title type-costitems">Cost Items</div>
    <div class="table-responsive">
      <table class="tblCostItems table table-bordered table-hover table-rowlink limitateNavigation" data-rowlink-url="<%= PathHelper.Pages.ProgramCostItems_Edit(ProgramInfo.ProgramJobId, null) %>">
        <thead>
          <tr>
            <th class="type-description" colspan="3">Description</th>
            <th class="type-date">Date</th>
            <% if(CanViewTotalRevenue) { %>
              <th class="type-money"><%= RevenueTextDisplay %></th>
            <% } %>
          </tr>
        </thead>
        <tbody>
          <% foreach (var costItem in CostItemList) { %>
            <tr tabindex="0" class="rowData" data-rowlink-id="<%= costItem.ProgramCostItemId %>">
              <td class="type-description" colspan="3"><%= costItem.Description.HTMLEncode() %></td>
              <td class="type-date"><%= costItem.CostIncurredUtc.UtcToTZOrNull(ConfigHelper.DefaultTimeZoneInfo).ToString("d MMM yyyy") %></td>
              <% if (CanViewTotalRevenue) { %>
                <td class="type-money"><%= (costItem.UnitPrice * costItem.Quantity).ToString("C") %></td>
              <% } %>
            </tr>
          <% } %>
        </tbody>
        <% if(CanViewTotalRevenue) { %>
          <tfoot>
            <tr tabindex="-1">
              <td colspan="4" class="total-title">Cost Items Total:</td>
              <td class="type-money tfoot-total"><%# CostItemTotalRevenue.ToString("C") %></td>
            </tr>
          </tfoot>
        <% } %>
      </table>
    </div>
  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      $(document).ready(function() {

        // If user cannot navigate from tables, remove the links of navigation.
        <% if (!CanNavigateFromTables ) { %>
         $('.limitateNavigation').removeAttr('data-rowlink-url');
        <% } %>

        $("#<%= PrePostLinkButtonID %>").click(function (ev) {
          ev.preventDefault();
          ev.stopPropagation();
          location.href = "<%= PathHelper.Pages.ProgramSendSurvey(ProgramInfo.ProgramJobId, ConfigHelper.TemplateSurveyIds.NewPostProgramSurvey) %>";
        });

      }); // ready.

    })(jQuery);
  </script>

</asp:Content>
