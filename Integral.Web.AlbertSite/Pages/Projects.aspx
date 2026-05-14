<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="Projects.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.Projects"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

   <% if (!UserHasAnyPrograms) { %>

     <%= WebHelper.GetEmptyStatePageHtml(
       title: "Projects",
       description: $"No projects yet. {(CanCreateNewProject ? " Add your first one!" : "")}",
       addActionHtml: CanCreateNewProject,
       actionButtonText: "Add Project",
       actionButtonPath: PathHelper.Pages.Projects_Add()) %>

   <% } else { %>

    <div class="content-action-bar">
      <div class="left">
        <div class="status-toggle input-group" id="btnGrpActive" data-toggle="buttons">
          <label class="btn <%= searchInfo.StatusClosed ? "" : "active" %>"><input type="radio" name="toggleActive" value="<%= PathHelper.AbleUrlValues.ProjectStatusToggleValue_Active %>" <%= searchInfo.StatusClosed ? "" : "checked" %> />Active</label>
          <label class="btn <%= searchInfo.StatusClosed ? "active" : "" %>"><input type="radio" name="toggleActive" value="<%= PathHelper.AbleUrlValues.ProjectStatusToggleValue_Closed %>" <%= searchInfo.StatusClosed ? "checked" : "" %> />Completed</label>
        </div>
        <div class="search-input">
          <i class="fa fa-search"></i>
          <input type="text" id="txtSearch" name="<%= PathHelper.AbleUrlKeys.JobSearchTerm %>" value="" placeholder="Job No., Title or Company" autofocus="autofocus">
        </div>
      </div>
      <div class="right">
        <% if (CanCreateNewProject) { %>
          <a class="btn btn-primary floatright ml10" href="<%= PathHelper.Pages.Projects_Add() %>">New Project</a>
        <% } %>
      </div>
    </div>

    <input type="hidden" id="inpGetPage" value="1" />

    <div class="table-responsive">
      <table class="table table-bordered table-hover table-rowlink limit-width" data-rowlink-url="<%= GetRowLinkUrl() %>">
        <thead>
          <tr>
            <th class="type-project-name">Project Name</th>
            <th class="type-program-name">Program Name</th>
            <th class="type-date-range">Date</th>
            <th class="type-status">Status</th>
            <th class="type-qty align-center">Participants</th>
            <th class="type-qty">Workshops</th>
            <th class="type-revenueprogress"><%= WebHelper.GetRevenueCompletionColTitle() %></th>
          </tr>
          <tr class="displaynone rowData" id="rowTemplate" data-rowlink-id="" tabindex="0">
            <td class="rowDataCell type-project-name" data-fieldname="ProjectJobName"></td>
            <td class="rowDataCell type-program-name" data-fieldname="ProgramJobName"></td>
            <td class="rowDataCell type-date-range" data-fieldname="ProgramStartEndDateUtc"></td>
            <td class="rowDataCell type-status" data-fieldname="ProgramStatus"></td>
            <td class="rowDataCell type-qty align-center" data-fieldname="ParticipantCount"></td>
            <td class="rowDataCell type-qty" data-fieldname="WorkshopCount"></td>
            <td class="rowDataCell type-revenueprogress" data-fieldname="RevenueProgress"></td>
          </tr>
        </thead>
        <tbody id="tblProgramsBody">
        </tbody>
      </table>
    </div>

    <div class="table-bottom">
      <div class="left">
        <span class="badge found-badge"><span class="pagination-total"></span>&nbsp; results</span>
      </div>
      <div class="right">
        <div class="pagination">
          <div class="pagination-page">Page: <div class="pagination-pagebuttons"><div class="pagination-pagebutton">1</div></div></div>
        </div>
      </div>
    </div>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {

      var inpGetPage;
      var rowTemplate, tblProgramsBody, txtSearch;
      var paginationPageButtons, paginationPageButton, btnGrpActive;
      var keyTimeout = null;
      var currentActiveToggleValue, currentPage;

      $(document).ready(function () {

        currentActiveToggleValue = "";
        currentPage = 0;

        btnGrpActive = $("#btnGrpActive");
        txtSearch = $("#txtSearch");
        inpGetPage = $("#inpGetPage");
        paginationPageButtons = $(".pagination-pagebuttons");
        paginationPageButton = $(".pagination-pagebutton");
        paginationPageButton.detach();
        rowTemplate = $("#rowTemplate");
        rowTemplate.detach().removeAttr("id").removeClass("displaynone");
        tblProgramsBody = $("#tblProgramsBody");

        SetActiveToggleValue(<%= SessionHelper.AppState.Programs.Search_StatusClosed.ToJSTrueFalse() %>);
        txtSearch.val("<%= SessionHelper.AppState.Programs.Search_SearchTerm %>");
        inpGetPage.val("<%= SessionHelper.AppState.Programs.Search_CurrentPage %>");

        btnGrpActive.on("change", "input:checked", ActiveToggled);
        paginationPageButtons.click(GoToResultsPage);

        txtSearch.DelayedChange({
          callback: function (newSearchText, $inp, options) {
            LoadData(newSearchText);
          }
        });
        LoadData();

      }); // ready.

      function GetActiveToggleValue() {
        $checked = btnGrpActive.find("input:checked");
        if ($checked.length != 1) return "";
        return $checked.val();
      }

      function SetActiveToggleValue(statusClosed) {
        var btn;
        if (statusClosed === true) {
          btn = btnGrpActive.find('input[value="<%= PathHelper.AbleUrlValues.ProjectStatusToggleValue_Closed %>"]');
        } else {
          btn = btnGrpActive.find('input[value="<%= PathHelper.AbleUrlValues.ProjectStatusToggleValue_Active %>"]');
        }
        if (btn.length === 1) {
          //btn.prop("checked", true);
          btn.trigger("click");
        }
      }

      function ActiveToggled(evt) {
        LoadData();
      }

      function GoToResultsPage(ev) {
        var $btn = $(ev.target);
        if (!$btn.hasClass("pagination-pagebutton")) return;
        var page = $btn.text();
        inpGetPage.val(page);
        LoadData();
      }

      function LoadData(newSearchText) {

        var newActiveToggleValue = GetActiveToggleValue();
        var newPage = toInt(inpGetPage.val(), 1);

        if (newSearchText == null
          && newActiveToggleValue === currentActiveToggleValue
          && newPage === currentPage) return;

        if (newSearchText != null
          || newActiveToggleValue !== currentActiveToggleValue) newPage = 1; // new search, default to page 1

        currentActiveToggleValue = newActiveToggleValue;
        currentPage = newPage;

        tblProgramsBody.empty();
        $.busyLoadFull("show");

        $.get(location.pathname,
          {
            "<%= PathHelper.AbleUrlKeys.Action%>": "<%= Action.GetList %>",
            "<%= PathHelper.AbleUrlKeys.JobSearchTerm %>": encodeURIComponent(txtSearch.val()),
            "<%= PathHelper.AbleUrlKeys.ProjectStatusToggle %>": encodeURIComponent(GetActiveToggleValue()),
            "<%= PathHelper.AbleUrlKeys.GetPage %>": newPage
          },
          function (data, status, jqXHR) { PopulateTableBody(data); },
          "json")
          .fail(function () { alert("Oops, there was a problem!"); })
          .always(function () { $.busyLoadFull("hide"); });
      }

      function PopulateTableBody(json) {

        tblProgramsBody.empty();

        if (!json) return;

        var rowsPerPage = json.RowsPerPage;
        var currentPage = json.CurrentPage;

        $(".pagination-total").text("");
        paginationPageButtons.empty();

        var results = json.results;
        if (!results) return;

        var totalRows = results.TotalRows;
        var totalPages = Math.ceil(totalRows / rowsPerPage);

        $(".pagination-total").text(totalRows);
        if (rowsPerPage != null) {
          for (var page = 1; page <= totalPages; page++) {
            var newButton = paginationPageButton.clone();
            newButton.text(page);
            if (page == currentPage) newButton.addClass("current");
            paginationPageButtons.append(newButton);
          }
        }

        var rowList = results.ProgramInfoList;
        if (!rowList) return;
        for (var rowNum in rowList) {
          var rowData = rowList[rowNum];
          var trRow = rowTemplate.clone();
          trRow.attr("data-rowlink-id", rowData.ProgramJobId);
          trRow.find(".rowDataCell").each(function (i, e) {
            var cell = $(e);
            var fieldName = "" + cell.data("fieldname");
            var value = rowData[fieldName];
            var isUtcDate = (cell.data("isutcdate") === true);
            var format = "" + cell.data("format");
            if (value === null || value === "") {
              cell.html("");
            } else {
              if (fieldName == "ProjectJobName" || fieldName == "ProgramStatus" || fieldName == "RevenueProgress") {
                cell.html(value);
              } else if (isUtcDate) {
                if (format == "") format = "MM";
                cell.text(moment(value).format(format));
              } else {
                cell.text(value);
              }
            }
          });
          tblProgramsBody.append(trRow);
        }
      }

    })(jQuery);
  </script>

</asp:Content>


