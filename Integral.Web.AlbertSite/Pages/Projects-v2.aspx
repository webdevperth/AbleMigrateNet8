<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="Projects-v2.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.Projects_v2"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <style>
    .xxtabulator-cell .progress-holder { width: 100%; text-align: left; }
    a.tabulator-row-link,
    a.tabulator-row-link:hover,
    a.tabulator-row-link:active,
    a.tabulator-row-link:focus,
    a.tabulator-row-link:visited { color: var(--table-color); }
  </style>

  <%= WebHelper.GetPageTabs(
      new WebHelper.PageTabsInfo() {
        PageTabsStyle = WebHelper.PageTabsStyle.Links,
        BorderBottom = true,
        LastTabFloatRight = true,
        TabListID = "ListMode"
      },
      GetPageTabItem(ListEntity.Projects, ListStatus.Active, "Active Projects"),
      GetPageTabItem(ListEntity.Projects, ListStatus.Closed, "Completed Projects"),
      GetPageTabItem(ListEntity.Programs, ListStatus.Active, "Actve Programs"),
      GetPageTabItem(ListEntity.Programs, ListStatus.Closed, "Completed Programs"),
      new WebHelper.PageTabItem() { ItemID = "RightSideControls" }
    ) %>

  <div class="content-action-bar table-search" data-appendto="RightSideControls">
    <div class="search-input">
      <i class="fa fa-search"></i>
      <input id="tabulator-search" type="search" name="<%= PathHelper.AbleUrlKeys.JobSearchTerm %>" value="" placeholder="Search project number, name, ..." autofocus="autofocus">
    </div>
    <% if (CanCreateNewProject) { %>
      <a class="btn btn-primary" href="<%= PathHelper.Pages.Projects_Add() %>"><i class="fal fa-plus"></i> New Project</a>
    <% } %>
  </div>

  <div id="projects-table" class="content-padding-bottom-minimum tabulator-fillHeight"></div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script>

    (function () {

      const localTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
      const $searchText = $("#tabulator-search");
      const $listModeTabs = $('ul#ListMode a[data-toggle="tab"]');

      $(document).ready(function () {

        let tabulatorTable = CreateTabulatorTable("#projects-table");

        SetSearchControls();

        $listModeTabs.on('show.bs.tab', function (e) {
          let activatedTabName = $(e.target).data('tabname');
          let previousTabName = $(e.relatedTarget).data('tabname');
          LoadData();
        });

        $searchText.DelayedChange({ minLength: <%= SearchTerm_MinLength %>, callback: LoadData });
        LoadData();

        function SetSearchControls() {
          $searchText.val("<%= ActiveFilters.SearchTerm.HTMLEncode() %>");
          $listModeTabs.ShowTabName("<%= SelectedTabName.HTMLEncode() %>");
        }

        function LoadData() {

          setTimeout(DoLoadData, 150); // Let UI updates settle.

          function DoLoadData() {

            let searchParams = {
              "<%= UrlKey_TabName %>": $listModeTabs.GetActiveTabName(),
              "<%= UrlKey_SearchTerm %>": $searchText.val()
            }

            var url = AbleJS.Util.PatchQuery({
              url: location.href,
              discardNullOrEmpty: true,
              params: searchParams
            });
            window.history.replaceState(null, '', url);
            tabulatorTable.setData(url, searchParams);
          }
        }

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

        function CreateTabulatorTable(elementSelector) {

          return new Tabulator(elementSelector, {

            height: "200px",
            renderVertical: "virtual",
            columnHeaderVertAlign: "middle",
            columnDefaults: {
              vertAlign: "middle"
            },

            ajaxURL: "",
            ajaxResponse: function (url, params, responseData) {
              // AbleJS.Logging.ResponseLog(responseData["<%= WebHelper.Tabulator.JsonKeys.ResponseLog %>"]);
              return responseData["<%= WebHelper.Tabulator.JsonKeys.TableData %>"];
            },

            headerFilter: "false",
            // initialHeaderFilter: [ { field: "Status", value: "Active" } ],

            sortMode: "local",
            //headerSortClickElement: "icon", // restrict click to icon
            headerSortElement: function (column, dir) {
              switch (dir) {
                case "asc":
                  return "<i class='fas fa-arrow-up'></i>";
                  break;
                case "desc":
                  return "<i class='fas fa-arrow-down'></i>";
                  break;
                default:
                  return "<i class='far fa-arrows-v'></i>";
              }
            },

            // filterMode: "local",
            paginationMode: "local",

            placeholder: "",
            autoColumns: false,

            pagination: false,
            // paginationSize: 20,
            // paginationSizeSelector: [10, 20, 50, 100],

            dataTree: true,
            dataTreeChildField: "<%= nameof(TableRow.ChildRows) %>",
            dataTreeStartExpanded: true,
            dataTreeElementColumn: "<%= nameof(TableRow.RowItemName) %>",
            dataTreeChildIndent: 20,
            dataTreeSort: false,
            dataTreeFilter: false,
            dataTreeExpandElement: '<div class="tabulator-data-tree-root"><i class="fas fa-caret-right font-caret"></i></div>',
            dataTreeCollapseElement: '<div class="tabulator-data-tree-root"><i class="fas fa-caret-down font-caret"></i></div>',
            dataTreeSelectPropagate: false,

            // layout: "fitColumns",
            rowHeight: 60,
            columnVertAlign: "middle",

            movableColumns: true,
            resizableColumns: true,

            persistence: { sort: true, filter: true, headerFilter: true, group: true, page: true, columns: false },
            persistenceMode: "cookie",
            persistenceID: "projects-list",

            columnDefaults: {
              headerSortTristate: true, // third click removes sort
              hozAlign: "left",
              vertAlign: "middle",
              headerHozAlign: "left",
              width: 130,
              minWidth: 130
            },
            columns: [
              {
                title: "Projects and Programs", field: "RowItemName",
                width: 300, minWidth: 250, frozen: true,
                //headerFilterFunc: projectNameFilter,
                formatter: function (cell, formatterParams, onRendered) {
                  let row = cell.getRow();
                  let rowData = row.getData();
                  let $rowEl = $(row.getElement());
                  let url = '';
                  if (rowData.IsProject) {
                    url = '<%= PathHelper.Pages.ProjectPrograms("__") %>'.replace('__', rowData.JobNumber);
                  } else {
                    url = '<%= PathHelper.Pages.ProgramOverview(1010) %>'.replace('1010', rowData.ProgramJobId);
                  }
                  $rowEl.attr("data-url", AbleJS.Util.HtmlEncode(url));
                  var html = '';
                  html += `<div>`;
                  html += `<div class="overflow-ellipsis font-weight-bold">${AbleJS.Util.HtmlEncode(cell.getValue())}</div>`;
                  if (rowData.IsProject === true) {
                    html += `<div class="row-jobNumber">${AbleJS.Util.HtmlEncode(rowData.JobNumber)}</div>`;
                  }
                  html += `</div>`;
                  return html;
                }
              },
              {
                title: "Partner", field: "PartnerFirstName",
                formatter: function (cell, formatterParams, onRendered) {
                  var rowData = cell.getRow().getData();
                  return AbleJS.Tabulator.UserAvatarHtml(
                    rowData.PartnerFirstName,
                    rowData.PartnerLastName,
                    rowData.PartnerPhotoFilename
                  );
                },
                ...AbleJS.Tabulator.UserAvatarColumnDef
              },
              {
                title: "Status", field: "ProgramStatusDisplayOrder",
                sorter: "number", width: 120, minWidth: 120,
                headerHozAlign: "center", hozAlign: "center",
                formatter: function (cell, formatterParams, onRendered) {
                  let rowData = cell.getRow().getData();
                  let badgeTitle = rowData.ProgramStatusDisplayTitle;
                  return `<div class="badge badge-${AbleJS.Util.HtmlEncode(badgeTitle.toLowerCase())} badge-table">${AbleJS.Util.HtmlEncode(badgeTitle)}</div>`;
                },
              },
              {
                title: "Organisation", field: "ClientName",
                width: 150, minWidth: 75,
                formatter: function (cell, formatterParams, onRendered) {
                  return `<div class="overflow-ellipsis">${AbleJS.Util.HtmlEncode(cell.getValue())}</div>`;
                }
              },
              {
                title: "Date", field: "StartDateUtc", width: 130, minWidth: 130,
                headerHozAlign: "center", hozAlign: "right", cssClass: "col-format-date",
                formatter: function (cell, formatterParams, onRendered) {
                  var rowData = cell.getRow().getData();
                  var startDateStr = '', endDateStr = '';
                  var startDateUtc = cell.getValue();
                  var endDateUtc = rowData.EndDateUtc;
                  if (startDateUtc) startDateStr = luxon.DateTime.fromISO(startDateUtc, { zone: "utc" }).toLocal().toFormat("dd MMM yyyy");
                  if (endDateUtc) endDateStr = luxon.DateTime.fromISO(endDateUtc, { zone: "utc" }).toLocal().toFormat("dd MMM yyyy");
                  return `${startDateStr}<br/>- ${endDateStr}`;
                },
                formatterParams: {
                  inputFormat: "iso",
                  inputTimezone: "utc",
                  outputFormat: "d MMM yyyy",
                  timezone: localTimeZone,
                  invalidPlaceholder: "-"
                },
                sorter: "date",
                sorterParams: {
                  format: "iso",
                  timezone: "utc",
                  alignEmptyValues: "top"
                },
              },
              {
                title: "Participants", field: "PaxMax",
                headerHozAlign: "center", hozAlign: "center",
                sorter: "number", width: 130, minWidth: 50,
                formatter: function (cell, formatterParams, onRendered) {
                  var rowData = cell.getRow().getData();
                  return `${rowData.PaxNow} / ${rowData.PaxMax}`;
                }
              },
              {
                title: "Delivery Progress", field: "DeliverySorter",
                width: 165, minWidth: 165,
                sorter: "number",
                formatter: function (cell, formatterParams, onRendered) {
                  var rowData = cell.getRow().getData();
                  if (rowData.IsProject === true) {
                    return AbleJS.Tabulator.GetProgressBar(rowData.ProgramsComplete, rowData.ProgramCount, { unitText: " program", unitsText: " programs" });
                  } else {
                    return AbleJS.Tabulator.GetProgressBar(rowData.ComponentsNow, rowData.ComponentsMax, { unitText: " component", unitsText: " components" });
                  }
                }
              },
              {
                title: "Workshops", field: "WorkshopsMax", sorter: "number",
                formatter: function (cell, formatterParams, onRendered) {
                  var rowData = cell.getRow().getData();
                  if (toInt(rowData.WorkshopsMax, 0) <= 0) return '';
                  return AbleJS.Tabulator.GetProgressBar(rowData.WorkshopsNow, rowData.WorkshopsMax);
                }
              },
              {
                title: "Coaching", field: "SessionsAllocated", sorter: "number",
                formatter: function (cell, formatterParams, onRendered) {
                  var rowData = cell.getRow().getData();
                  if (toInt(rowData.SessionsAllocated, 0) <= 0) return '';
                  return AbleJS.Tabulator.GetProgressBar(rowData.SessionsCompleted, rowData.SessionsAllocated);
                }
              },
              {
                title: "Revenue", field: "RevenueMax", sorter: "number",
                minWidth: 170, minWidth: 170,
                formatterParams: { symbol: "$" },
                formatter: function (cell, formatterParams, onRendered) {
                  cell.getElement().classList.add("column-revenue");
                  var rowData = cell.getRow().getData();
                  if (rowData.CanViewRevenue === true) {
                    return AbleJS.Tabulator.GetProgressBar(rowData.RevenueNow, rowData.RevenueMax, { isMoney: true });
                  } else {
                    return "";
                  }
                }
              },
              {
                title: "Costs", field: "CostTotal",
                sorter: "number", cssClass: "col-format-money",
                headerHozAlign: "center", hozAlign: "right",
                formatter: function (cell, formatterParams, onRendered) {
                  var rowData = cell.getRow().getData();
                  if (rowData.CanViewRevenue === true) {
                    return AbleJS.Util.FormatCurrency(rowData.CostTotal);
                  } else {
                    return "-";
                  }
                },
              },
              {
                title: "Profit", field: "ProfitTotal",
                sorter: "number", cssClass: "col-format-money",
                headerHozAlign: "center", hozAlign: "right",
                formatter: function (cell, formatterParams, onRendered) {
                  var rowData = cell.getRow().getData();
                  if (rowData.CanViewRevenue === true) {
                    return AbleJS.Util.FormatCurrency(rowData.ProfitTotal);
                  } else {
                    return "-";
                  }
                },
              },

              { title: "Overall<br/>Score", field: "Summary.Eval_All_AnsAvg", minWidth: 50, ...AbleJS.Tabulator.ScoreColumnDef },
              { title: "Pre/Post<br/>Delta", field: "Summary.PrePost360_Delta", minWidth: 50, ...AbleJS.Tabulator.ScoreColumnDef },
              { title: "Program<br/>Score", field: "Summary.Eval_EndProgram_AnsAvg", minWidth: 50, ...AbleJS.Tabulator.ScoreColumnDef },
              { title: "Workshop<br/>Score", field: "Summary.Eval_Workshops_AnsAvg", minWidth: 50, ...AbleJS.Tabulator.ScoreColumnDef },
              { title: "Coaching<br/>Score", field: "Summary.Eval_Coaching_AnsAvg", minWidth: 50, ...AbleJS.Tabulator.ScoreColumnDef },
            ],
          });
        }

        tabulatorTable.on("tableBuilding", function (data) {
          // Set dynamic height to fill remaining vertical space, if the table has the class name.
          let $tbl = $(tabulatorTable.element);
          if ($tbl.hasClass("tabulator-fillHeight")) {
            let contentPaddingBottom = $(".content-wrapper > .content").css("padding-bottom");
            let tableTop = Math.trunc($tbl.offset().top) + 1;
            let cssHeight = `calc(100vh - ${tableTop}px - ${contentPaddingBottom} - 5px)`;
            $tbl.css("min-height", cssHeight);
            $tbl.css("max-height", cssHeight);
          }
        });

        tabulatorTable.on("tableBuilt", function (data) {

        });

        tabulatorTable.on("dataLoadError", (error) => {
          console.error("Tabulator dataLoadError:", error);
        });

        tabulatorTable.on("cellMouseEnter", function (e, cell) {
          // Wrap the cell contents in a link.
          // If tree-node cell, wrap the second child only (first child is the toggle control).

          var $row = $(cell.getRow().getElement());

          let rowUrl = $row.data("url");
          if (isStringNullOrEmpty(rowUrl)) return;

          var $cell = $(cell.getElement());
          if ($cell.children(".tabulator-cell-link").length > 0) return; // Already done.

          var linkTagHtml = `<A class="tabulator-cell-link" tabindex="-1" target="_blank" href="${rowUrl}">`;

          var $treeToggle = $cell.find(".tabulator-data-tree-branch,.tabulator-data-tree-root").eq(0);

          var $contentParent = $cell;
          if ($treeToggle.length === 1) {
            $treeToggle.next().addClass("overflow-ellipsis").wrap($(linkTagHtml));
          } else {
            $cell.html(`${linkTagHtml + $cell.html()}</A>`);
          }
        });

        tabulatorTable.on("xrowMouseEnter", function (e, row) {
          // unused
          var $row = $(row.getElement());
          if ($row.parent()[0].tagName !== "A") {
            $rowLink = $row.find("a.row-link");
            if ($rowLink.length === 1) {
              $(row.getElement()).wrap(`<a class="tabulator-row-link" target="_blank" href="${$rowLink.prop("href")}"></a>`);
            }
          }

        });

      });
    })();
  </script>

</asp:Content>
