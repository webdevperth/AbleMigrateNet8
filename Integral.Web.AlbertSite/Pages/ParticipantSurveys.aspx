<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ParticipantSurveys.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ParticipantSurveys"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <%= WebHelper.GetPageTabs(
      new WebHelper.PageTabsInfo() { PageTabsStyle = WebHelper.PageTabsStyle.Tabs },
      new WebHelper.PageTabItem(PathHelper.ParticipantSurveysTabEnum.UsersSurveys.ToString(), "My Surveys", true),
      new WebHelper.PageTabItem(PathHelper.ParticipantSurveysTabEnum.SharedWithUser.ToString(), "Shared with Me"),
      new WebHelper.PageTabItem(PathHelper.ParticipantSurveysTabEnum.SharedByUser.ToString(), "Shared by Me")) %>

  <div class="tab-panel" data-appendTo="panel-<%= PathHelper.ParticipantSurveysTabEnum.UsersSurveys %>">

    <% if (SurveyList.IsNullOrEmpty()) { %>

      <%= WebHelper.GetNoRecordsBadge("You have no surveys.") %>

    <% } else { %>

      <div class="table-responsive">
        <table class="table table-bordered table-hover table-rowlink" data-rowlink-url="">
          <thead>
            <tr>
              <th class="type-description">Survey</th>
              <th class="type-delivery">Survey Type</th>
              <th class="type-status">Status</th>
              <th class="type-date">Self Close</th>
              <th class="type-date">Rater Close</th>
              <th class="w200"></th>
            </tr>
          </thead>
          <tbody>
            <% foreach (var thisSurvey in SurveyList) { %>
              <tr tabindex="0" <%= WebHelper.GetSurveyListRowDataAttrs(thisSurvey) %>>
                <td class="type-description"><b><%= thisSurvey.SurveyName.HTMLEncode() %></b><br /><%= thisSurvey.FriendlyProjectTitle.HTMLEncode() %></td>
                <td class="type-delivery"><%= WebHelper.GetSurveyDeliveryBadge(thisSurvey) %></td>
                <td class="type-status"><%= WebHelper.GetSurveyStatusBadge(thisSurvey) %></td>
                <td class="type-date"><%= WebHelper.GetSurveyCloseDateSelf(thisSurvey) %></td>
                <td class="type-date"><%= WebHelper.GetSurveyRatersInfoCol(thisSurvey) %></td>
                <td><%= WebHelper.GetSurveyListActionButtons(thisSurvey) %></td>
              </tr>
            <% } %>
          </tbody>
        </table>
      </div>

    <% } %>

  </div>

  <div class="tab-panel" data-appendTo="panel-<%= PathHelper.ParticipantSurveysTabEnum.SharedWithUser %>">

    <%= GetSurveyInfoHtml(SharedSurveysWithUser, PathHelper.ParticipantSurveysTabEnum.SharedWithUser) %>

  </div>

  <div class="tab-panel" data-appendTo="panel-<%= PathHelper.ParticipantSurveysTabEnum.SharedByUser %>">

    <%= GetSurveyInfoHtml(SharedSurveysByUser, PathHelper.ParticipantSurveysTabEnum.SharedByUser) %>

  </div>

</asp:Content>


<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {

      var $pageTabs;

      $(document).ready(function () {

        $pageTabs = $(".content .nav-tabs");

        if ($pageTabs.length == 1) {

          var $navigationTab = $('li[data-tabname="<%= SelectedSurveyTab %>"]');
          if ($navigationTab.length > 0) {
            $navigationTab.find('a').click();
          } else {
            // Activate default tab
            $('li[data-tabname="<%= PathHelper.ParticipantSurveysTabEnum.UsersSurveys %>"]').find('a').click();
          }

          $pageTabs.click(function (e) {
            var tabName = $(e.target).closest("li").data("tabname");
            UpdateUrlAddress(tabName);
          });

        }

        $(".<%= WebHelper.CSSClasses.UnshareSurveyClass %>").on('click', function () {
          var surveyShareId = $(this).data('<%= WebHelper.DataAttrName.SurveyShareId %>');
          common_ConfirmDialog("Confirm", "Are you sure you want to unshare this survey to user? If you do so, they will no longer have access to it.", function (confirmed) {
            if (confirmed) UpdateSurveySharing(surveyShareId, $(this));
          });
        });


      }); // ready.

      function UpdateUrlAddress(tabName) {
        window.history.pushState('', '', '<%= PathHelper.Pages.ParticipantSurveys(null) %>' + tabName);
      }

      function UpdateSurveySharing(surveyShareId, btnUnshare) {

        AjaxSubmit({
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.UnshareSurvey %>",
          data: {
            "<%= FormFields.ShareSurveyId %>": surveyShareId
          },
          onSuccess: function (jqXHR, data) {
            var resultRemoved = data["<%= AjaxReturnData.UnsharedResult %>"];
            if (resultRemoved) {
              $('#tbl<%= PathHelper.ParticipantSurveysTabEnum.SharedByUser %> tr[data-<%= WebHelper.DataAttrName.SurveyShareId %>="' + surveyShareId + '"]').remove();
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
