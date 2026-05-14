<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="CoacheeSurveySummaryReport.aspx.cs"
  MasterPageFile="~/MasterPages/AdminLTE.Master"
  Inherits="Integral.Web.PortalSite.Pages_Albert.CoacheeSurveySummaryReport" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="HeadContent" runat="server" ContentPlaceHolderID="HeadContent">
  <link rel="stylesheet" type="text/css" href="<%= PathHelper.UrlPath.CSS %>survey-viewer-common.css" />
</asp:Content>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (PageFlags.IsNoSurveys) { %>

    No surveys available.<br/>
    <br/>
    For reporting, a survey needs to include at least <%= ConfigHelper.MinimumRatersFor360Report %> completed raters.<br/>
    <br/>

  <% } else if (CanViewSurveySelector) { %>

    <div class="form-horizontal mb30 mw800">
      <%= GetSurveySelectorInfoHtml() %>
    </div>

  <% } %>

  <% if (PageFlags.IsNoSelfResponse) { %>

    Participant has not completed this survey.

  <% } else if (!PageFlags.ShowReport) { %>

    Unable to show results for the selected survey.

  <% } else { %>

    <%= WebHelper.GetPageTabs(
        new WebHelper.PageTabsInfo() { LastTabFloatRight = true, TabListID = WebHelper.ElementID.ReportTabs },
        new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.overview.ToString(), "Overview", true),
        new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.detailed.ToString(), "Detailed"),
        new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.focus.ToString(), "Focus"),
        !HasPreSurvey ? null : new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.prepost.ToString(), "Pre/Post"),
        !HasOpenText ? null : new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.comments.ToString(), "Comments"),
        AICoachLongFormText.IsNullOrEmpty() ? null : new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.aisummary.ToString(), "AI Summary"),
        ReportInformationHtml.IsNullOrEmpty() ? null : new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.reportinformationhtml.ToString(), "Information"),
        new WebHelper.PageTabItem() { ItemID = "PageTabReportLinks" },
        new WebHelper.PageTabItem() { ItemID = "RightSideControls" }
    ) %>

    <% if (ConfigHelper.IsDevServer) { %>
      <div class="flex flex-align-center pl15" data-appendto="PageTabReportLinks">
        <% if (HasPreSurvey) { %>
          <a class="ml0" href="<%= PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.surveys, PreSurveyUId, PreSurveyPartUId) %>" target="_blank">Pre Survey</a>
        <% } %>
        <a class="ml20" href="<%= PathHelper.Reports.CoacheeSurvey(CoacheeInfo, UrlSelectedSurveyUId, UrlSelectedPartUId) %>" target="_blank">Coachee Report</a>
      </div>
    <% } %>

    <div class="flex flex-align-center gap20 pl15" data-appendto="RightSideControls">

      <% if (CanShowDevPlanSlideout) { %>
        <button type="button" id="coacheeReport-btn-slideout" class="btn btn-secondary flex flex-align-center gap5">
          <ion-icon name="navigate-circle-outline" class="opacity75"></ion-icon><span class="opacity75">Dev Plan</span></button>
      <% } %>

      <% if (!Hide360ReportNorms) { %>
        <div class="flex flex-align-center gap10" id="divBenchmarkSelect">
          <div class="">Benchmark: </div>
          <select id="selBenchmark" class="w125 noselect2">
            <option value="<%= PathHelper.SurveyViewerBenchmarkEnum.Global.ToString() %>">Global</option>
            <option value="<%= PathHelper.SurveyViewerBenchmarkEnum.Org.ToString() %>">Organisation</option>
          </select>
        </div>
      </div>
    <% } %>

    <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.overview %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.overview %>">

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = PartialIDs.SummaryScore,
          Url = PathHelper.Partials.CoacheeReport_Overview(null, null, null),
          InitialWidth = "400px",
          InitialHeight = "200px",
          DeferInitialLoad = true,
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>

      <br/>

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = PartialIDs.Categories,
          Url = PathHelper.Partials.CoacheeReport_Categories(null, null, null),
          InitialWidth = "100%",
          InitialHeight = "400px",
          DeferInitialLoad = true,
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>
    </div>

    <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.detailed %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.detailed %>">

      <%= ReportHelper.Coachee.GetTabMessageForAverages(HasPreSurvey, PathHelper.SurveyViewerTabEnum.detailed) %>

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = PartialIDs.QuestionDetail,
          Url = PathHelper.Partials.CoacheeReport_Detail(null, null, null),
          InitialWidth = "100%",
          InitialHeight = "400px",
          DeferInitialLoad = true,
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>
    </div>

    <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.focus %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.focus %>">

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = PartialIDs.QuestionFocus,
          Url = PathHelper.Partials.CoacheeReport_Focus(null, null, null),
          InitialWidth = "100%",
          InitialHeight = "400px",
          DeferInitialLoad = true,
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>
    </div>

    <% if (HasPreSurvey) { %>

      <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.prepost %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.prepost %>">

        <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = PartialIDs.QuestionPrePost,
          Url = PathHelper.Partials.CoacheeReport_PrePost(0, 0, null, null),
          InitialWidth = "100%",
          InitialHeight = "400px",
          DeferInitialLoad = true,
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>
      </div>

    <% } %>

    <% if (HasOpenText) { %>
      <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.comments %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.comments %>">
        <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
            ID = PartialIDs.Comments,
            Url = PathHelper.Partials.CoacheeReport_Comments(null, null),
            InitialWidth = "100%",
            InitialHeight = "400px",
            DeferInitialLoad = true,
            InitialStyle = WebHelper.PartialLoaderStyle.Blank,
            LoaderStyle = WebHelper.PartialLoaderStyle.Chart
          }) %>
      </div>
    <% } %>

    <% if (!AICoachLongFormText.IsNullOrEmpty()) { %>
      <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.aisummary %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.aisummary %>">
        <%= WebHelper.MarkdownToHtml(AICoachLongFormText) %>
      </div>
    <% } %>

    <% if (!ReportInformationHtml.IsNullOrEmpty()) { %>
      <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.reportinformationhtml %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.reportinformationhtml %>">
        <div class="mw700">
          <%= ReportInformationHtml %>
        </div>
      </div>
    <% } %>

    <% if (CanShowDevPlanSlideout) { %>
      <div id="coacheeReport-slideout-content" class="slideout-content hidden">

        <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = "Partial_DevPlanForm",
          Url = PathHelper.Pages.CoacheeSurveyEmbed(LatestDevPlan.SurveyUniqueId, LatestDevPlan.SurveyPartUniqueId),
          InitialWidth = "100%",
          InitialHeight = "400px",
          DeferInitialLoad = true,
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>
      </div>
    <% } %>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <% if (CanShowDevPlanSlideout) { %>
    <script type="text/javascript">
      (function ($) {

        $(document).ready(function () {

          $("#<%= WebHelper.ElementID.SlideoutPanelTitle %>").html('<h4>Development Plan</h4>');
          $("#<%= WebHelper.ElementID.SlideoutPanelBody %>").empty().append($("#coacheeReport-slideout-content"));

          $("#coacheeReport-btn-slideout").click(function (ev) {
            ev.preventDefault();
            $("#coacheeReport-slideout-content").show();
            $("body").removeClass("slideout-show");
            window.setTimeout(function () {
              $("body").addClass("slideout-show");
            }, 100);
          });

        }); // ready.
      })(jQuery);
    </script>
  <% } %>

  <script>

    (function ($) {

      var isRatersOnly = <%= IsRatersOnly.ToJSTrueFalse() %>;
      var selSurveyList, selBenchmark, reportTabs;
      var divBenchmarkSelect;

      $(document).ready(function() {

        selSurveyList = $('select[name="<%= FormFields.SurveyToView %>"]');
        selBenchmark = $("#selBenchmark");
        divBenchmarkSelect = $("#divBenchmarkSelect");
        reportTabs = $("#<%= WebHelper.ElementID.ReportTabs %>");

        selSurveyList.change(function (e) {
          var surveyUId = selSurveyList.val();
          document.location.href = AbleJS.Util.PatchQuery({
            url: document.location.href,
            params: { "<%= PathHelper.AbleUrlKeys.SurveyUId %>": surveyUId }
          });
        });

        selBenchmark.change(LoadReport);

        DisableBenchmarkSelectOnPrePostTab();
        LoadReport();
        AttachInfoTooltip();

      }); // ready

      function AttachInfoTooltip() {

        var hasPreSurvey = <%= HasPreSurvey.ToJSTrueFalse() %>;

        if (!hasPreSurvey) return;

        $('.infoIcon').has('span[data-<%=ReportHelper.Coachee.tooltipData%>]').each(function () {
          var tooltipData = $(this).find('span').attr('data-<%=ReportHelper.Coachee.tooltipData%>');

          $(this).jBox('Tooltip', {
            position: { y: 'top', x: 'right' },
            title: 'Global Averages:',
            content: tooltipData
          });
        });
      }

      function DisableBenchmarkSelectOnPrePostTab() {

        // When user clicks pre-post tab, set benchmark dropdown to global and disable it.
        // Restore original selection when moving off pre-post tab.

        reportTabs.on("click", "li", function (ev) {
          var tabName = $(ev.currentTarget).data("tabname");
          if (!isStringNullOrEmpty(tabName)) {
            if (tabName == "<%= PathHelper.SurveyViewerTabEnum.overview %>"
              || tabName == "<%= PathHelper.SurveyViewerTabEnum.detailed %>"
              || tabName == "<%= PathHelper.SurveyViewerTabEnum.focus %>") {
              divBenchmarkSelect.show();
            } else {
              divBenchmarkSelect.hide();
            }
          }
        });
      }

      function LoadReport() {

        var intakeId = "<%= FoundIntakeId %>";
        var delay = 300;

        $.EachPartial(function ($partial, partialInfo) {
          partialInfo.Clear();
          if (!isStringNullOrEmpty(intakeId)) {
            setTimeout(function (thisPartialInfo) {
              var extraValues = {
                "<%= PathHelper.AbleUrlKeys.SurveyIntakeCodeId %>": intakeId,
                "<%= PathHelper.AbleUrlKeys.CoacheeGuid %>": "<%= CoacheeInfo.CoacheeUID.ToStringNoBraces() %>",
                "<%= PathHelper.AbleUrlKeys.PreSurveyPartId %>": <%= PreSurveyPartId %>,
                "<%= PathHelper.AbleUrlKeys.PreSurveyIntakeId %>": <%= PreSurveyIntakeId %>,
                "<%= PathHelper.AbleUrlKeys.SurveyViewerBenchmark %>": selBenchmark.val(),
                "<%= PathHelper.AbleUrlKeys.SurveyShareId %>": "<%= SharedSurveyInfo?.SurveyShareId %>"
              };
              thisPartialInfo.LoadUrl(thisPartialInfo.initialUrl, extraValues);
            }, delay, partialInfo);
            delay += 300;
          }
        });
      }

    })(jQuery);

  </script>

</asp:Content>
