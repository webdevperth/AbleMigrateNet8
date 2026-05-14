<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="EvalViewer.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.EvalViewer"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<asp:Content ID="HeadContent" runat="server" ContentPlaceHolderID="HeadContent">
  <link rel="stylesheet" type="text/css" href="<%= PathHelper.UrlPath.CSS %>survey-viewer-common.css" />
</asp:Content>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <br/>

  <% if (PageValues.ShowNoAccessToProjects) { %>

    No acess to any Projects.

  <% } else { %>

    <%= WebHelper.GetPageTabs(
        new WebHelper.PageTabsInfo() { LastTabFloatRight = true, TabListID = WebHelper.ElementID.ReportTabs },
        PageValues.ShowSelectionTab ? new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.selection.ToString(), "Selection", true) : null,
        new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.overview.ToString(), "Overview"),
        new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.detailed.ToString(), "Detailed"),
        PageValues.ShowOpenTextTab ? new WebHelper.PageTabItem(PathHelper.SurveyViewerTabEnum.comments.ToString(), "Comments") : null,
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

    <% if (PageValues.ShowSelectionTab) { %>

      <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.selection %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.selection %>">
        <div class="form-horizontal mt30">
          <%= WebHelper.GetSelectRow(
            new WebHelper.RowOptions() { Label = "Project:", LabelCols = 1, ContentCols = 6 },
            new WebHelper.SelectInfo() {
              ID = "selProject",
              InputName = FormFields.ProjectJobNumber,
              Placeholder = "[Select Project]",
              TopOptionsHtml = GetSelectedProjectOption()
            }) %>
          <%= WebHelper.GetMultiSelectRow("Programs:", FormFields.ProgramJobIds, 1, 6, "") %>
          <%= WebHelper.GetMultiSelectRow("Surveys:", FormFields.IntakeCodeIds, 1, 6, "") %>
          <%= WebHelper.GetTextDisplayRow("", 8, "<div id=\"ResultMessage\"></div>") %>
        </div>
      </div>

    <% } %>

    <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.overview %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.overview %>">

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
          ID = PartialIDs.SummaryScore,
          Url = PathHelper.Partials.SurveyViewer_Overview(null, null, null, null),
          InitialWidth = "400px",
          InitialHeight = "200px",
          DeferInitialLoad = true,
          InitialStyle = WebHelper.PartialLoaderStyle.Blank,
          LoaderStyle = WebHelper.PartialLoaderStyle.Chart
        }) %>
      <br/>

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.Categories,
        Url = PathHelper.Partials.SurveyViewer_Categories(null, null, null, null),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>

    </div>

    <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.detailed %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.detailed %>">

      <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
        ID = PartialIDs.QuestionDetail,
        Url = PathHelper.Partials.SurveyViewer_Detail(null, null, null, null),
        InitialWidth = "100%",
        InitialHeight = "400px",
        DeferInitialLoad = true,
        InitialStyle = WebHelper.PartialLoaderStyle.Blank,
        LoaderStyle = WebHelper.PartialLoaderStyle.Chart
      }) %>

    </div>

    <% if (PageValues.ShowOpenTextTab) { %>
      <div class="tab-panel" id="tab-panel-<%= PathHelper.SurveyViewerTabEnum.comments %>" data-appendTo="panel-<%= PathHelper.SurveyViewerTabEnum.comments %>">
        <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
            ID = PartialIDs.Comments,
            Url = PathHelper.Partials.SurveyViewer_Comments(null),
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

  <% if (!PageValues.ShowNoAccessToProjects) { %>

    <script type="text/javascript">
      (function ($) {

        var selProject, selPrograms, selSurveys, selBenchmark, hasError;
        var showSelectionTab = <%= PageValues.ShowSelectionTab.ToJSTrueFalse() %>;

        $(document).ready(function() {

          selProject = $('select[name="<%= FormFields.ProjectJobNumber %>"]');
          selPrograms = $('select[name="<%= FormFields.ProgramJobIds %>"]');
          selSurveys = $('select[name="<%= FormFields.IntakeCodeIds %>"]');
          selBenchmark = $("#selBenchmark");

          hasError = false;

          if (showSelectionTab) {
            selProject
              .select2({
                minimumInputLength: 3,
                dropdownAutoWidth: false,
                width: "100%",
                ajax: {
                  delay: 750,
                  processResults: function (data) { return data; },
                  transport: function (params, success, failure) {
                    AjaxSubmit({
                      busyLoadElement: selProject,
                      autoHighlightField: false,
                      action: "<%= AjaxAction.GetProjects %>",
                      data: {
                        "<%= FormFields.ProjectSearchTerm %>": params.data.term
                      },
                      onSuccess: function (jqXHR, data) { success(data['<%= AjaxKey.OptionsHtml %>']); },
                      onFail: function (jqXHR, data) { } //failure(); }
                    });
                  }
                },
              })
              .change(LoadPrograms);

            selPrograms.change(LoadSurveys);
            selSurveys.change(LoadReport);

            LoadPrograms();
            LoadSurveys();
          }

          selBenchmark.change(LoadReport);

          LoadReport();

        }); // ready.

        function GetViewerPostData() {
          return {
            "<%= FormFields.ProjectJobNumber %>": selProject.val() ?? "",
            "<%= FormFields.ProgramJobIds %>": selPrograms.val() ?? "",
            "<%= FormFields.IntakeCodeIds %>": selSurveys.val() ?? "",
          };
        }

        function LoadPrograms() {
          selPrograms.empty();
          selSurveys.empty();
          AjaxSubmit({
            busyLoadElement: selPrograms,
            autoHighlightField: false,
            url: location.href,
            action: "<%= AjaxAction.GetPrograms %>",
            data: GetViewerPostData(),
            onSuccess: function (jqXHR, data) {
              selPrograms.html(data['<%= AjaxKey.OptionsHtml %>']).trigger("update");
            },
            onFail: function (jqXHR, data) { }
          });
        }

        function LoadSurveys() {
          selSurveys.empty();
          AjaxSubmit({
            busyLoadElement: selSurveys,
            autoHighlightField: false,
            url: location.href,
            action: "<%= AjaxAction.GetSurveys %>",
            data: getViewerPostData(),
            onSuccess: function (jqXHR, data) {
              selSurveys.html(data['<%= AjaxKey.OptionsHtml %>']).trigger("update");
              var resultCount = selSurveys.children('option[value!=""]').length;
              selSurveys.select2({ placeholder: (resultCount + " result" + (resultCount != 1 ? "s" : "")) });
            },
            onFail: function (jqXHR, data) { }
          });
        }

        function LoadReport() {

          var intakeIds, evalType;
          if (showSelectionTab) {
            intakeIds = selSurveys.val() == null ? "" : selSurveys.val().toString();
            evalType = "";
          } else {
            intakeIds = "<%= PageValues.IntakeIdsForReport.ToStringList() %>";
            evalType = "<%= PageValues.EvalType %>"
          }
          var delay = 300;

          $.EachPartial(function ($partial, partialInfo) {
            partialInfo.Clear();
            setTimeout(function (thisPartialInfo) {
              thisPartialInfo.LoadUrl(
                thisPartialInfo.initialUrl,
                {
                  "<%= PathHelper.AbleUrlKeys.SurveyIntakeCodeId %>": intakeIds,
                  "<%= PathHelper.AbleUrlKeys.GblAnswerTypeId %>": <%= ConfigHelper.GblAnsTypeId_Eval %>,
                  "<%= PathHelper.AbleUrlKeys.EvalType %>": evalType,
                  "<%= PathHelper.AbleUrlKeys.SurveyViewerBenchmark %>": selBenchmark.val()
                }
              );
            }, delay, partialInfo);
            delay += 300;
          });
        }

      })(jQuery);
    </script>

  <% } %>

</asp:Content>
