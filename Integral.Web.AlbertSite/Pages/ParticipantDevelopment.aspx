<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ParticipantDevelopment.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.ParticipantDevelopment" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="stylesheet" type="text/css" href="<%= PathHelper.UrlPath.CSS %>survey-viewer-common.css" />

  <br/>

  <section class="content">

    <%= WebHelper.ParticipantActivities.GetPeopleDetailsHeaderHtml(ProfileInfo) %>
    <br/>

    <% if (SessionHelper.AppAccess.Reports.CanViewOrgPeopleDetails360Results(ProfileInfo.UserGuid)) {
        ResultsArea();
      } %>

    <% void ResultsArea() { %>

      <div class="flex-wrap gap15 mb20">
        <% if (ProfileInfo.UserActivity?.Latest360IntakeCodeId != null) { %>
          <div class="minw300"><% Overview(); %></div>
        <% } %>
        <% if (!ProfileInfo.UserActivity.AISummaryText.IsNullOrEmptyOrWhitespace()) { %>
          <% AISummary(); %>
        <% } %>
      </div>

      <% if (HasLatest360) { %>

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
            ID = "partial_FocusHighest",
            Url = PathHelper.Partials.SurveyViewer_Focus(null, null, PathHelper.Partials.SurveyViewer_Focus_ShowSection.Highest),
            InitialWidth = "100%",
            InitialHeight = "400px",
            DeferInitialLoad = true,
            InitialStyle = WebHelper.PartialLoaderStyle.Blank,
            LoaderStyle = WebHelper.PartialLoaderStyle.Chart
          }) %>

          <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
            ID = "partial_FocusLowest",
            Url = PathHelper.Partials.SurveyViewer_Focus(null, null, PathHelper.Partials.SurveyViewer_Focus_ShowSection.Lowest),
            InitialWidth = "100%",
            InitialHeight = "400px",
            DeferInitialLoad = true,
            InitialStyle = WebHelper.PartialLoaderStyle.Blank,
            LoaderStyle = WebHelper.PartialLoaderStyle.Chart
          }) %>

        </div>

      <% } %>
    <% } %>

    <% void Overview() { %>
      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = "partial_Overview",
          Url = PathHelper.Partials.SurveyViewer_Overview(null, null),
          InitialWidth = "400px",
          InitialHeight = "200px",
          DeferInitialLoad = true,
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>
    <% }; %>

    <% void AISummary() { %>
      <div class="boxBorder ai-summary-box mw600">
        <div class="boxTitle"><h4>AI Coach Development Summary</h4></div>
        <div><%= ProfileInfo.UserActivity?.AISummaryText.HTMLEncode() %></div>
      </div>
    <% }; %>

  </section>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">

    (function ($) {

      $(document).ready(function () {
        var intakeId = <%= ProfileInfo.UserActivity?.Latest360IntakeCodeId.ToStringOrDefaultIfNull("null") %>;
        if (intakeId != null) LoadReport(intakeId);
      });

      function LoadReport(intakeId) {

        var delay = 300;

        <% if (ProfileInfo.UserActivity?.Latest360IntakeCodeId != null && Latest360CoacheeInfo != null) { %>
          $.EachPartial(function ($partial, partialInfo) {
            partialInfo.Clear();
            if (isNumber(intakeId)) {
              setTimeout(function (thisPartialInfo) {
                var extraValues = {
                  "<%= PathHelper.AbleUrlKeys.SurveyIntakeCodeId %>": intakeId,
                  "<%= PathHelper.AbleUrlKeys.CoacheeGuid %>": "<%= Latest360CoacheeInfo.CoacheeUID.ToStringNoBraces() %>",
                  "<%= PathHelper.AbleUrlKeys.PreSurveyPartId %>": <%= PreSurveyPartId %>,
                  "<%= PathHelper.AbleUrlKeys.PreSurveyIntakeId %>": <%= PreSurveyIntakeId %>,
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
  </script>

</asp:Content>
