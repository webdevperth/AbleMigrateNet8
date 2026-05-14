<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="DevelopmentPlan.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.DevelopmentPlan"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <style>
    .devplan-hero { display: flex; align-items: center; width: 100%; height: 300px; background-color: #B7D8F5;
                    border-radius: 10px; justify-content: space-evenly; overflow: hidden; }
    .devplan-hero .img { height: 100%; }
    .devplan-hero .intro { min-width: 200px; max-width: 450px; color: #222; }
    .devplan-hero .devplan-vision { text-align: justify; max-height: 300px; overflow: hidden; text-overflow: ellipsis; white-space: normal;
                                    display: -webkit-box; -webkit-line-clamp: 7; -webkit-box-orient: vertical; }
  </style>

  <div class="content-action-bar p0">
    <div class="left"><h3 class="text-muted">Current Plan</h3></div>
    <% if (CurrentPlan != null) { %>
      <div class="right"><button type="button" class="btn btn-primary btn-create">Create New Plan</button></div>
    <% } %>
  </div>

  <div class="devplan-hero">
    <img class="img" src="/images/devplans-alegria-trans.png" />
    <div class="intro">
      <h4><%= CurrentPlan == null ? "Create Your Development Plan" : "Your Development Plan" %></h4>
      <% if (CurrentPlan == null) { %>
        <p>Create your first professional development plan. Completing your professional development plan is a key
        step towards achieving your career goals, offering a clear roadmap to personal and professional growth.</p>
      <% } else { %>
        <p>Your Vision:</p><p class="devplan-vision"><%= CurrentPlanAnswers.Find(q => q.GblQuestionId == ConfigHelper.GlobalQuestionIds.DevelopmentPlan_ProfessionalVision)?.TextAnswer.HTMLEncode() %></p>
      <% } %>
      <p class="mt20">
        <% if (CurrentPlan == null) { %>
          <button type="button" class="btn btn-secondary btn-create">Create Your First Plan &rarr;</button>
        <% } else { %>
          <a class="btn btn-secondary btn-review" href="<%= PathHelper.Pages.DevelopmentPlanForm(CurrentPlan.SurveyUniqueId, CurrentPlan.SurveyPartUniqueId) %>">Review Your Plan &rarr;</a>
        <% } %>
      </p>
    </div>
  </div>

  <br/>

  <h3 class="text-muted mt20 mb10">Your Plans</h3>

  <div class="table-responsive">
    <table class="table table-hover table-rowlink" data-rowlink-url="">
      <thead>
        <tr>
          <th class="type-date-range">Plan Period</th>
          <th class="">Goals</th>
          <th class="type-progress">Completion</th>
        </tr>
      </thead>
      <tbody>
        <% if ((PlansForUser?.Count ?? 0) == 0) { %>
          <tr><td colspan="5">No Plans</td></tr>
        <% } else { %>
          <% foreach (var plan in PlansForUser) { %>
            <tr tabindex="0" class="rowData" data-rowlink-url="<%= PathHelper.Pages.DevelopmentPlanForm(plan.SurveyUniqueId, plan.SurveyPartUniqueId) %>">
              <td class="type-date-range"><%= GetPlanDateDisplay(plan.ClosedUtc) %></td>
              <td class=""><%= GetGoalResponseText(plan.GoalText).HTMLEncode() %></td>
              <td class="type-progress"><%= (plan.SurveyPartPercentCompleted ?? 0) %>%</td>
            </tr>
          <% } %>
        <% } %>
      </tbody>
    </table>
  </div>

  <% if (!SharedSurveysWithUser.IsNullOrEmpty()) { %>
    <h3 class="text-muted mt20 mb10">Shared With Me</h3>
    <%= GetSurveyInfoHtml(SharedSurveysWithUser, SurveyShareEnum.SharedWithUser) %>
  <% } %>

  <% if (!SharedSurveysByUser.IsNullOrEmpty()) { %>
    <h3 class="text-muted mt20 mb10">Shared By Me</h3>
    <%= GetSurveyInfoHtml(SharedSurveysByUser, SurveyShareEnum.SharedByUser) %>
  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      $(document).ready(function() {

        $(".btn-create").click(function () {
          AjaxSubmit({
            action: "<%= AjaxAction.Create %>"
          });
        });

        $(".<%= WebHelper.CSSClasses.UnshareSurveyClass %>").on('click', function () {
          var surveyShareId = $(this).data('<%= WebHelper.DataAttrName.SurveyShareId %>');
          common_ConfirmDialog("Confirm", "Are you sure you want to unshare this survey to user? If you do so, they will no longer have access to it.", function (confirmed) {
            if (confirmed) UpdateSurveySharing(surveyShareId, $(this));
          });
        });

      }); // ready.

      function UpdateSurveySharing(surveyShareId, btnUnshare) {

        AjaxSubmit({
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.UnshareSurvey %>",
          data: {
            "<%= FormFields.ShareSurveyId %>": surveyShareId
          },
          onSuccess: function (jqXHR, data) {
            var resultRemoved = data["<%= ReturnValue.UnsharedResult %>"];
            if (resultRemoved) {
              $('#tbl<%= SurveyShareEnum.SharedByUser %> tr[data-<%= WebHelper.DataAttrName.SurveyShareId %>="' + surveyShareId + '"]').remove();
            }
          },
          onFail: function (jqXHR, data) {
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Update failed, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) {
          }
        });
      }

    })(jQuery);
  </script>

</asp:Content>
