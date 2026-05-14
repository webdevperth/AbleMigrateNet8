<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Coachees.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.Coachees" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <div class="results-area">
    <div class="content-action-bar">
      <div class="left">
        <% if (PageMode == PageModeEnum.Global) { %>
          <div class="status-toggle input-group" id="btnGrpActive" data-toggle="buttons">
            <label class="btn <%= searchInfo.StatusEndProgram ? "" : "active" %>"><input type="radio" name="toggleActive" value="<%= PathHelper.AbleUrlValues.CoacheeStatusToggleValue_Active %>" <%= searchInfo.StatusEndProgram ? "" : "checked" %> />Active</label>
            <label class="btn <%= searchInfo.StatusEndProgram ? "active" : "" %>"><input type="radio" name="toggleActive" value="<%= PathHelper.AbleUrlValues.CoacheeStatusToggleValue_EndProgram %>" <%= searchInfo.StatusEndProgram ? "checked" : "" %> />Completed</label>
          </div>
        <% } %>
        <div class="search-input">
          <i class="fa fa-search"></i>
          <input type="text" id="txtSearch" name="<%= PathHelper.AbleUrlKeys.CoacheeSearchTerm %>" value="" placeholder="Name, Email, Program..." autofocus="autofocus">
        </div>
      </div>
      <div class="right">
        <% if (CanAddParticipant) { %>
          <% if (PageMode == PageModeEnum.Program) { %>
            <%= WebHelper.GetAddParticipantDropdownButton(WebHelper.AddParticipantFrom.Program, ProgramInfo.ProgramJobId, CanSendInviteToAddParticipants) %>
          <% } else if (SessionHelper.AppAccess.Participants.CanAdd()) { %>
            <button class="btn btn-primary floatright ml10 btnAddParticipant">+ Add Program & Participant</button>
          <% } %>
        <% } %>
      </div>
    </div>

    <input type="hidden" id="inpGetPage" value="1" />

    <div class="table-responsive">
      <table class="table table-bordered table-hover table-rowlink limit-width" <%= CanViewParticipants ? $"data-rowlink-url=\"{GetRowLinkUrl()}\"" : "" %>>
        <thead>
          <tr>
            <th class="type-fullname">Participant Name</th>
            <th class="type-status">Info</th>
            <th class="type-user-nameWithAvatar">Coach</th>
            <% if (PageMode == PageModeEnum.Global) { %>
              <th class="type-project-name">Project</th>
            <% } %>
            <th class="type-delivery">Type</th>
            <th class="type-progress">Sessions</th>
          </tr>
          <tr class="displaynone rowData" id="rowTemplate" data-rowlink-id="" tabindex="0">
            <td class="rowDataCell type-fullname" data-fieldname="CoacheeName"></td>
            <td class="rowDataCell type-status" data-fieldname="CoacheeUserActivity"></td>
            <td class="rowDataCell type-user-nameWithAvatar" data-fieldname="CoachName"></td>
              <% if (PageMode == PageModeEnum.Global) { %>
              <td class="rowDataCell type-project-name" data-fieldname="LocatorColumn"></td>
            <% } %>
            <td class="rowDataCell type-delivery" data-fieldname="CoachingType"></td>
            <td class="rowDataCell type-progress" data-fieldname="CoacheeSessions"></td>
          </tr>
        </thead>
        <tbody id="tblCoacheesBody">
        </tbody>
      </table>
    </div>

    <div class="table-bottom mb30">
      <div class="left">
        <span class="badge found-badge"><span class="pagination-total"></span>&nbsp; results</span>
      </div>
      <% if (PageMode == PageModeEnum.Global) { %>
        <div class="right">
          <div class="pagination">
            <div class="pagination-page">Page: <div class="pagination-pagebuttons"><div class="pagination-pagebutton">1</div></div></div>
          </div>
        </div>
      <% } %>
    </div>
  </div>

  <div class="emptystatepagehtml display-none">
    <%= GetEmptyStatePageHtml() %>
  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script>
    (function ($) {
      var inpGetPage;
      var rowTemplate, tblCoacheesBody, txtSearch;
      var paginationPageButtons, paginationPageButton, btnGrpActive, paginationSection;
      var keyTimeout = null;
      var currentSearchValue, currentActiveToggleValue, currentPage;
      var btnSendInviteToClient, btnAddParticipant

      $(document).ready(function () {

        const searchParams = new URLSearchParams(location.search);

        currentSearchValue = "";
        currentActiveToggleValue = "";
        currentPage = 0;

        btnGrpActive = $("#btnGrpActive");
        txtSearch = $("#txtSearch");
        inpGetPage = $("#inpGetPage");
        paginationSection = $(".pagination");
        paginationPageButtons = $(".pagination-pagebuttons");
        paginationPageButton = $(".pagination-pagebutton");
        paginationPageButton.detach();
        rowTemplate = $("#rowTemplate");
        rowTemplate.detach().removeAttr("id").removeClass("displaynone");
        tblCoacheesBody = $("#tblCoacheesBody");

        SetActiveToggleValue(<%= SessionHelper.AppState.Coachees.Search_StatusEndProgram.ToJSTrueFalse() %>);
        txtSearch.val("<%= SessionHelper.AppState.Coachees.Search_SearchTerm %>");
        inpGetPage.val("<%= SessionHelper.AppState.Coachees.Search_CurrentPage %>");

        btnGrpActive.on("change", "input:checked", ActiveToggled);
        txtSearch.keyup(SearchKeyUp);
        paginationPageButtons.click(GoToResultsPage);

        SearchKeyTimeout(txtSearch, true); // Get initial results.

        btnSendInviteToClient = $(".<%= WebHelper.CSSClasses.CoacheeList.SendInviteToClient_BtnCssName %>");
        btnSendInviteToClient.click(sendInviteToClient);

        btnAddParticipant = $('.btnAddParticipant');
        btnAddParticipant.click(AddParticipant);

        if (searchParams.has('<%= PathHelper.AbleUrlKeys.ParticipantQuickAddTrigger %>')) {
          setTimeout(AddParticipant, 300);
        }

      }); // ready

      function AddParticipant() {

        <% if (CanAddParticipant && PageMode != PageModeEnum.Program) { %>

          BootstrapDialog.show({
            title: "Add Participant",
            closable: false,
            onshow: function (dialogRef) {
              var modalDialog = dialogRef.getModalDialog();
              modalDialog.data("dialogref", dialogRef);
              var modalBody = dialogRef.getModalBody();
              modalBody.busyLoad("show");
              modalBody.load("<%= PathHelper.Partials.AddParticipantWizard() %>",
              function (data) {
                modalBody.html(data);
                common_UpdateUI(modalBody);
                modalBody.busyLoad("hide");
              });
            }
          });
        <% } %>
      }

      function GetActiveToggleValue() {
        $checked = btnGrpActive.find("input:checked");
        if ($checked.length != 1) return "";
        return $checked.val();
      }

      function SetActiveToggleValue(StatusEndProgram) {
        var btn;
        if (StatusEndProgram === false) {
          btn = btnGrpActive.find('input[value="<%= PathHelper.AbleUrlValues.CoacheeStatusToggleValue_Active %>"]');
        } else {
          btn = btnGrpActive.find('input[value="<%= PathHelper.AbleUrlValues.CoacheeStatusToggleValue_EndProgram %>"]');
        }
        if (btn.length === 1) {
          btn.prop("checked", true);
          btn.trigger("click");
        }
      }

      function ActiveToggled(evt) {
        SearchKeyTimeout(txtSearch, true);
      }

      function GoToResultsPage(ev) {
        var $btn = $(ev.target);
        if (!$btn.hasClass("pagination-pagebutton")) return;
        var page = $btn.text();
        inpGetPage.val(page);
        SearchKeyTimeout(txtSearch, true);
      }

      function SearchKeyUp(ev, data) {
        var isImmediate = (data && data.immediate);
        var $inp = $(ev.target);
        if (keyTimeout) clearTimeout(keyTimeout);
        if (isImmediate) SearchKeyTimeout($inp, isImmediate);
        else keyTimeout = setTimeout(function () { keyTimeout = null; SearchKeyTimeout($inp, isImmediate); }, 800);
      }

      function SearchKeyTimeout($inp, isImmediate) {

        var newSearchValue = "" + $inp.val();
        var newActiveToggleValue = GetActiveToggleValue();
        var newPage = parseInt(inpGetPage.val(), 10);
        var minLength = <%= Min_Search_Length %>;
        if (isNaN(newPage)) newPage = 1;

        if (newSearchValue === currentSearchValue
          && newActiveToggleValue === currentActiveToggleValue
          && newPage === currentPage
          && isImmediate !== true) return;

        if (newSearchValue.length > 0 && newSearchValue.length < minLength) {
          return;
        }

        if (newSearchValue !== currentSearchValue
          || newActiveToggleValue !== currentActiveToggleValue) newPage = 1; // new search, default to page 1

        currentSearchValue = newSearchValue;
        currentActiveToggleValue = newActiveToggleValue;
        currentPage = newPage;

        tblCoacheesBody.empty();
        $.busyLoadFull("show");

        $.get(location.pathname,
          {
            "<%= PathHelper.AbleUrlKeys.CoacheeSearchTerm %>": encodeURIComponent(newSearchValue),
            "<%= PathHelper.AbleUrlKeys.CoacheeStatusToggle %>": encodeURIComponent(GetActiveToggleValue()),
            "<%= PathHelper.AbleUrlKeys.GetPage %>": newPage,
            "<%= PathHelper.AbleUrlKeys.ProgramJobId %>": encodeURIComponent(<%= WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProgramJobId) %>),
            "<%= UrlParam_GetRows %>": true
          },
          function (data, status, jqXHR) { PopulateTableBody(data); },
          "json")
          .fail(function () { alert("Oops, there was a problem!"); })
          .always(function () { $.busyLoadFull("hide"); });
      }

      function PopulateTableBody(json) {

        tblCoacheesBody.empty();

        if (!json) return;

        var rowsPerPage = json.RowsPerPage;
        var currentPage = json.CurrentPage;

        $(".pagination-total").text("");
        paginationPageButtons.empty();

        var results = json.results;
        if (!results) return;

        // Handle empty state page html
        var showEmptyStateHtml = json.ShowEmptyStateHtml;
        if (showEmptyStateHtml) {
          $('.emptystatepagehtml').show();
        } else {
          $('.emptystatepagehtml').hide();
        }

        var totalRows = results.TotalRows;
        $(".pagination-total").text(totalRows);

        <% if (PageMode == PageModeEnum.Global) { %>

          var totalPages = Math.ceil(totalRows / rowsPerPage);

          if (rowsPerPage != null) {
            for (var page = 1; page <= totalPages; page++) {
              var newButton = paginationPageButton.clone();
              newButton.text(page);
              if (page == currentPage) newButton.addClass("current");
              paginationPageButtons.append(newButton);
            }
          }

          // Hide Pagination section if there's no results to prevent showing 'Page' text.
          if (totalRows == 0) {
            paginationSection.hide();
          } else {
            paginationSection.show();
          }

        <% } %>

        var rowList = results.CoacheeInfoList;
        if (!rowList) return;
        for (var rowNum in rowList) {
          var rowData = rowList[rowNum];
          var trRow = rowTemplate.clone();

          var newRowHtml = trRow.prop('outerHTML').replace('<tr', `<tr ${rowData.CoacheeRowAttribute}`);
          trRow = $(newRowHtml);

          trRow.find(".rowDataCell").each(function (i, e) {
            var cell = $(e);
            var fieldName = "" + cell.data("fieldname");
            var value = rowData[fieldName];
            if (value === null || value === "") {
              cell.html("");
            } else {
              if (fieldName == "CoacheeName" || fieldName == "CoachName" || fieldName == "CoachingType" || fieldName == "CoacheeSessions" || fieldName == "CoacheeUserActivity"
                || fieldName == "LocatorColumn") {
                cell.html(value);
              } else {
                cell.text(value);
              }
            }
          });
          tblCoacheesBody.append(trRow);
        }
        common_UpdateUI(tblCoacheesBody);
      }

      function sendInviteToClient() {

        <% if (PageMode == PageModeEnum.Program) { %>

          var dateSent = "";
          <% if (ProgramInfo.ParticipantFormEmailSentUtc != null) { %>
            dateSent = "<br /> <p> Last sent on <%= ProgramInfo.ParticipantFormEmailSentUtc.ToString("dd/MM/yyyy") %></p>";
          <% } %>

          var dialogBodyText = "Send email invitation to add participants <%= AccessUsersToReceiveForm %> now? " + dateSent;
          common_ConfirmDialog("Client Invitation to Add Participants", dialogBodyText, null, function(confirmed) {
            if (confirmed) {
              AjaxSubmit({
                action: "<%= AjaxAction.SendInvite %>",
              });
              var metadata = {
                email_address: ""
              };
              window.Intercom('trackEvent', 'client_invite_add_pax', metadata);
            }
          });
        <% } %>
      }

    })(jQuery);
  </script>

</asp:Content>
