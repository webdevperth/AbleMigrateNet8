<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="SurveyViewer.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.SurveyViewer"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<asp:Content ID="HeadContent" runat="server" ContentPlaceHolderID="HeadContent">
  <link rel="stylesheet" type="text/css" href="<%= PathHelper.UrlPath.CSS %>survey-viewer-common.css" />
</asp:Content>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <%= WebHelper.GetPageTabs(
      new WebHelper.PageTabsInfo() { LastTabFloatRight = true, TabListID = WebHelper.ElementID.ReportTabs },
      new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.selection.ToString(), "Selection", true),
      new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.overview.ToString(), "Overview"),
      new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.detailed.ToString(), "Detailed"),
      new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.comments.ToString(), "Comments"),
      new WebHelper.PageTabItem() { ItemID = "PageTabBenchSelect" }
  ) %>

  <div id="divBenchmarkSelect" class="flex flex-align-center pl15" data-appendto="PageTabBenchSelect">
    <div class="mr10">Benchmark: </div>
    <div class="ml10">
      <select id="selBenchmark" class="w125 noselect2">
        <option value="<%= PathHelper.SurveyViewerBenchmarkEnum.Global.ToString() %>">Global</option>
        <option value="<%= PathHelper.SurveyViewerBenchmarkEnum.Org.ToString() %>">Organisation</option>
      </select>
    </div>
  </div>

  <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.selection %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.selection %>">

    <div class="form-horizontal mt30">

      <%= WebHelper.GetSelectRow("Company:", FormFields.CompanyIds, 1, 6, GetCompanyOptionsHtml(SelectedCompanyList)) %>

      <%= WebHelper.GetMultiSelectRow("Survey Types:", FormFields.ReportTypeIds, 1, 6, GetReportTypeOptionsHtml(SelectedReportTypeList)) %>

      <%= WebHelper.GetMultiSelectRow("Surveys:", FormFields.IntakeCodeIds, 1, 6, "") %>

    </div>

  </div>

  <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.overview %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.overview %>">

    <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.SummaryScore,
        Url = PathHelper.Partials.SurveyViewer_Overview(null, null),
        InitialWidth = "400px",
        InitialHeight = "200px",
        DeferInitialLoad = true,
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart,
        //WaitForPageTabName = PathHelper.SurveyViewerTabEnum.overview.ToString()
      }) %>

    <br/>

    <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.Categories,
        Url = PathHelper.Partials.SurveyViewer_Categories(null, null),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart,
        //WaitForPageTabName = PathHelper.SurveyViewerTabEnum.overview.ToString(),
        //DelayMs = 500
      }) %>

  </div>

  <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.detailed %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.detailed %>">

    <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.QuestionDetail,
        Url = PathHelper.Partials.SurveyViewer_Detail(null, null),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart,
        //WaitForPageTabName = PathHelper.SurveyViewerTabEnum.detailed.ToString()
      }) %>

  </div>

  <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.comments %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.comments %>">

    <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.Comments,
        Url = PathHelper.Partials.SurveyViewer_Comments(null),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart,
        //WaitForPageTabName = PathHelper.SurveyViewerTabEnum.comments.ToString()
      }) %>

  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {

      var selCompany, selReportType, selSurvey, selBenchmark;

      $(document).ready(function() {

        selCompany = $('select[name="<%= FormFields.CompanyIds %>"]');
        selReportType = $('select[name="<%= FormFields.ReportTypeIds %>"]');
        selSurvey = $('select[name="<%= FormFields.IntakeCodeIds %>"]');
        selBenchmark = $("#selBenchmark");

        selCompany.change(LoadSurveys);
        selReportType.change(LoadSurveys);
        selSurvey.change(LoadReport);
        selBenchmark.change(LoadReport);

        LoadSurveys();

      }); // ready.

      function LoadSurveys() {

        var companyIds = selCompany.val() == null ? "" : selCompany.val().toString();
        var reportTypeIds = selReportType.val() == null ? "" : selReportType.val().toString();

        selSurvey.empty().prop("disabled", true).trigger("change").busyLoad("show");

        $.post(
          location.href,
          {
            "<%= PathHelper.FormKeys.AjaxAction %>": "<%= AjaxAction.GetSurveys %>",
            "<%= FormFields.CompanyIds %>": "" + companyIds,
            "<%= FormFields.ReportTypeIds %>": "" + reportTypeIds
          },
          function (data, textStatus, jqXHR) {
            selSurvey.html(data).prop("disabled", false).trigger("change").busyLoad("hide");
            var resultCount = selSurvey.children('option[value!=""]').length;
            selSurvey.select2({ placeholder: (resultCount + " result" + (resultCount != 1 ? "s" : "")) });
          }
        );
      }

      function LoadReport() {

        var intakeIds = selSurvey.val() == null ? "" : selSurvey.val().toString();
        var delay = 300;

        $.EachPartial(function ($partial, partialInfo) {
          partialInfo.Clear();
          if (!isStringNullOrEmpty(intakeIds)) {
            setTimeout(function (thisPartialInfo) {
              thisPartialInfo.LoadUrl(
                thisPartialInfo.initialUrl,
                {
                  "<%= PathHelper.AbleUrlKeys.SurveyIntakeCodeId %>": intakeIds,
                  "<%= PathHelper.AbleUrlKeys.SurveyViewerBenchmark %>": selBenchmark.val()
                }
              );
            }, delay, partialInfo);
            delay += 300;
          }
        });

      }

    })(jQuery);
  </script>

</asp:Content>
