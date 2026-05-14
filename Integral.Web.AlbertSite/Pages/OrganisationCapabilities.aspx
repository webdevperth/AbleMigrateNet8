<%@ Page Language="C#" AutoEventWireup="true" CodeFile="OrganisationCapabilities.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.OrganisationCapabilities" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="HeadContent" runat="server" ContentPlaceHolderID="HeadContent">
  <link rel="stylesheet" type="text/css" href="<%= PathHelper.UrlPath.CSS %>survey-viewer-common.css" />
</asp:Content>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <br/>

  <%= WebHelper.GetPageTabs(
      new WebHelper.PageTabsInfo() { LastTabFloatRight = true, TabListID = WebHelper.ElementID.ReportTabs },
      SurveyStatsList?.Count > 1
        ? new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.selection.ToString(), "Selection", true) { ItemID = "PageTabSelection" }
        : null,
      new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.overview.ToString(), "Overview"),
      new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.detailed.ToString(), "Detailed"),
      new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.focus.ToString(), "Focus"),
      new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.heatmap.ToString(), "Heat Map"),
      !HasPrePost ? null : new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.prepost.ToString(), "Pre-Post") { ItemID = "PageTabPrePost" },
      new WebHelper.PageTabItem() { ItemID = "PageTabBenchSelect" }
  ) %>

  <div class="flex flex-align-center pl15 gap10" data-appendto="PageTabBenchSelect">
    <div id="divBenchmarkSelect" class="flex flex-align-center gap10">
      <div>Benchmark: </div>
      <select disabled id="selBenchmark" class="w125 noselect2">
        <option value="<%= PathHelper.SurveyViewerBenchmarkEnum.Global.ToString() %>">Global</option>
      </select>
    </div>
  </div>

  <% if (SurveyStatsList?.Count > 1) { %>
    <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.selection %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.selection %>">
      <div class="form-horizontal mt30">
        <%= WebHelper.GetSelectRow("Survey Type:", FormFields.GblAnswerTypeId, 1, 6, GetSurveyTypeOptionsHtml()) %>
      </div>
    </div>
  <% } %>

  <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.overview %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.overview %>">
    <% = WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.SummaryScore,
        Url = PathHelper.Partials.CompanyReport_Overview(CompanyInfo.CompanyId, PathHelper.SurveyViewerBenchmarkEnum.Global),
        InitialWidth = "400px",
        InitialHeight = "200px",
        DeferInitialLoad = true,
        WaitForPageTabName = PathHelper.SurveyViewerTabEnum.overview.ToString(),
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>

      <br/>

    <% = WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.Categories,
        Url = PathHelper.Partials.CompanyReport_Categories(CompanyInfo.CompanyId, PathHelper.SurveyViewerBenchmarkEnum.Global),
        InitialWidth = "600px",
        InitialHeight = "200px",
        DeferInitialLoad = true,
        WaitForPageTabName = PathHelper.SurveyViewerTabEnum.overview.ToString(),
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>
  </div>

  <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.detailed %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.detailed %>">
    <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.Detail,
        Url = PathHelper.Partials.CompanyReport_Detail(CompanyInfo.CompanyId, PathHelper.SurveyViewerBenchmarkEnum.Global),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        WaitForPageTabName = PathHelper.SurveyViewerTabEnum.detailed.ToString(),
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>
  </div>

  <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.focus %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.focus %>">
    <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.Focus,
        Url = PathHelper.Partials.CompanyReport_Focus(CompanyInfo.CompanyId, PathHelper.SurveyViewerBenchmarkEnum.Global),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        WaitForPageTabName = PathHelper.SurveyViewerTabEnum.focus.ToString(),
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>
  </div>

  <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.heatmap %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.heatmap %>">
    <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.HeatMap,
        Url = PathHelper.Partials.CompanyReport_HeatMap(CompanyInfo.CompanyId, PathHelper.SurveyViewerBenchmarkEnum.Global),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        WaitForPageTabName = PathHelper.SurveyViewerTabEnum.heatmap.ToString(),
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>
  </div>

  <% if (HasPrePost) { %>
    <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.prepost %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.prepost %>">
      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = PartialIDs.PrePost,
          Url = PathHelper.Partials.CompanyReport_PrePost(CompanyInfo.CompanyId, PathHelper.SurveyViewerBenchmarkEnum.Global),
          InitialWidth = "100%",
          InitialHeight = "400px",
          DeferInitialLoad = true,
          WaitForPageTabName = PathHelper.SurveyViewerTabEnum.prepost.ToString(),
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>
    </div>
  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script>

    (function ($) {

      var selBenchmark, pageTabSelection, pageTabPrePost, resultMessage;
      var reportTabs, divBenchmarkSelect, selQuestionType

      $(document).ready(function () {

        divBenchmarkSelect = $("#divBenchmarkSelect");
        selBenchmark = $("#selBenchmark");
        selAnswerType = $('select[name="<%= FormFields.GblAnswerTypeId %>"]');
        reportTabs = $("#<%= WebHelper.ElementID.ReportTabs %>");
        pageTabSelection = $("#PageTabSelection");
        pageTabPrePost = $("#PageTabPrePost");
        resultMessage = $("#ResultMessage");

        DisableBenchmarkSelectOnPrePostTab();
        selBenchmark.change(LoadReport);
        selAnswerType.change(LoadReport);

      }); // ready

      function DisableBenchmarkSelectOnPrePostTab() {

        // When user clicks pre-post tab, set benchmark dropdown to global and disable it.
        // Restore original selection when moving off pre-post tab.

        reportTabs.on("click", "li", function (ev) {
          var tabName = $(ev.currentTarget).data("tabname");
          if (!isStringNullOrEmpty(tabName)) {
            if (tabName == "<%= PathHelper.SurveyViewerTabEnum.prepost %>") {
              divBenchmarkSelect.hide();
            } else {
              divBenchmarkSelect.show();
            }
          }
        });
      }

      function LoadReport() {

        var urlParamsObj = {
          "<%= PathHelper.AbleUrlKeys.CompanyId %>": <%= CompanyInfo.CompanyId %>,
          "<%= PathHelper.AbleUrlKeys.GblAnswerTypeId %>": selAnswerType.val(),
          "<%= PathHelper.AbleUrlKeys.SurveyViewerBenchmark %>": selBenchmark.val()
        }

        pageTabPrePost.addClass("disabled");
        resultMessage.text("");
        console.log(`loadreport`);
        AjaxSubmit({
          url: AbleJS.Util.PatchQuery({ url: location.href, params: urlParamsObj }),
          action: "<%= AjaxAction.GetHasPreSurvey %>",
          onSuccess: function (jqXHR, data) {
            if (data["<%= ReturnValues.Message %>"]) {
              resultMessage.text(data["<%= ReturnValues.Message %>"]);
            }
            if (data["<%= ReturnValues.HasPreSurvey %>"] === true) {
              pageTabPrePost.show().removeClass("disabled");
            } else {
              pageTabPrePost.hide();
            }
            if (data["<%= ReturnValues.DisableReportTabs %>"] === true) {
              pageTabSelection.siblings().addClass("disabled");
              pageTabSelection.siblings().find("a").prop("disabled", true).prop("tabindex", -1);
            } else {
              pageTabSelection.siblings().removeClass("disabled");
              pageTabSelection.siblings().find("a").prop("disabled", false).prop("tabindex", 0);
            }
            ReloadPartials(urlParamsObj);
          }
        });

        function ReloadPartials(urlParamsObj) {

          var delay = 300;
          $.EachPartial(function ($partial, partialInfo) {
            partialInfo.Clear();
            setTimeout(function (thisPartialInfo) {
              thisPartialInfo.LoadUrl(thisPartialInfo.initialUrl, urlParamsObj);
            }, delay, partialInfo);
            delay += 300;
          });
        }
      }

    })(jQuery);
  </script>

</asp:Content>
