<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Coaches.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.Coaches" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Pages_Albert" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <form class="form-horizontal" id="partnerTagsForm">

    <div class="content-action-bar">
      <div class="left">
        <% if (CanViewStatusToggle) { %>
          <div class="status-toggle input-group" id="btnGrpActive" data-toggle="buttons">
            <label data-<%= DataAttr.SearchMode %>="<%= DbHelper.AlbertCoaches.SearchModeEnum.SearchPartners %>" class="btn <%= searchInfo.StatusInactive ? "" : "active" %>"><input type="radio" name="toggleActive" value="<%= PathHelper.AbleUrlValues.PartnerStatus_Active %>" <%= searchInfo.StatusInactive ? "" : "checked" %> />Active</label>
            <label data-<%= DataAttr.SearchMode %>="<%= DbHelper.AlbertCoaches.SearchModeEnum.SearchPartners %>" class="btn <%= searchInfo.StatusInactive ? "active" : "" %>"><input type="radio" name="toggleActive" value="<%= PathHelper.AbleUrlValues.PartnerStatus_Inactive %>" <%= searchInfo.StatusInactive ? "checked" : "" %> />Inactive</label>
            <% if (CanViewAllUsers) { %>
              <label data-<%= DataAttr.SearchMode %>="<%= DbHelper.AlbertCoaches.SearchModeEnum.SearchAllUsers %>" class="btn <%= SearchMode == DbHelper.AlbertCoaches.SearchModeEnum.SearchAllUsers ? "active" : "" %>"><input type="radio" name="toggleActive" value="<%= DbHelper.AlbertCoaches.SearchModeEnum.SearchAllUsers  %>" <%= SearchMode == DbHelper.AlbertCoaches.SearchModeEnum.SearchAllUsers ? "checked" : "" %> />All Users</label>
            <% } %>
          </div>
        <% } %>
        <div class="search-input">
          <i class="fa fa-search"></i>
          <input type="text" id="txtSearch" name="<%= PathHelper.AbleUrlKeys.PartnerSearchTerm %>" value="" placeholder="Search name, email, tags, bio..." autofocus="autofocus">
        </div>
      </div>
    </div>

    <div class="multipleTagsRow">
      <% foreach (var categoryTag in AllTagInfo.CategoryList) { %>
        <div class="eachTagContent">
          <div class="nowrap"><%= categoryTag.CategoryName %></div>
          <select multiple="" class="form-control" name="<%= CoachEdit.FormFields.PartnerTagCategoryIdPrefix %>" >
            <% foreach (var tag in categoryTag.TagInfoList) { %>
              <option <%= GetTagSelected(tag) %> value="<%= tag.TagId %>"><%= tag.TagName %></option>
            <% } %>
          </select>
        </div>
      <% } %>
    </div>
  </form>

  <input type="hidden" id="inpGetPage" value="1" />

  <div class="table-responsive">
    <table class="tblCoaches table table-bordered table-hover table-rowlink limit-width" data-rowlink-url="<%= GetDefaultRowUrl() %>">
      <thead>
        <tr>
          <th class="type-user-nameWithAvatar">Partner Name</th>
          <th class="type-company-name">Company</th>
          <th class="">Tags</th>
          <th class="w30 userTooltip"></th>
          <% if (CanViewHiddenPartners) { %>
            <th class="w50"></th>
          <% } %>
        </tr>
      </thead>
      <tbody  id="PartnerListBody" >
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

</asp:Content>


<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {

      var $partnerListBody = $('#PartnerListBody');
      var btnGrpActive = $("#btnGrpActive");
      var txtSearch = $("#txtSearch");
      var keyTimeout = null, currentSearchValue, currentActiveToggleValue, canViewHiddenPartners;
      var currentSearchMode, CanViewAllUsers, IsSearchingMode_AllUsers, $multipleTagsRow;
      var paginationPageButtons, paginationPageButton, inpGetPage, currentPage;

      $(document).ready(function () {

        currentPage = 0;
        paginationPageButtons = $(".pagination-pagebuttons");
        paginationPageButton = $(".pagination-pagebutton");
        paginationPageButton.detach();
        inpGetPage = $("#inpGetPage");
        currentSearchValue = "";
        currentActiveToggleValue = "";
        currentSearchMode = "<%= DbHelper.AlbertCoaches.SearchModeEnum.SearchPartners %>";
        canViewHiddenPartners = <%= CanViewHiddenPartners.ToJSTrueFalse() %>;
        CanViewAllUsers = <%= CanViewAllUsers.ToJSTrueFalse() %>;
        IsSearchingMode_AllUsers = false;
        $multipleTagsRow = $('.multipleTagsRow');

        SetActiveToggleValue(<%= SessionHelper.AppState.CoachList.Search_StatusInactive.ToJSTrueFalse() %>);
        txtSearch.val("<%= SessionHelper.AppState.CoachList.Search_SearchFor %>");
        inpGetPage.val("<%= SessionHelper.AppState.CoachList.Search_CurrentPage %>");

        btnGrpActive.on("change", "input:checked", ActiveToggled);
        txtSearch.keyup(SearchKeyUp);
        SearchKeyTimeout(txtSearch, true); // Get initial results.
        paginationPageButtons.click(GoToResultsPage);

        $(".eachTagContent select").change(UpdatedPartnerTags);

      }); // ready.

      function GoToResultsPage(ev) {
        var $btn = $(ev.target);
        if (!$btn.hasClass("pagination-pagebutton")) return;
        var page = $btn.text();
        inpGetPage.val(page);
        SearchKeyTimeout(txtSearch, true);
      }

      function GetCurrentSearchMode() {
        var activeLabel = btnGrpActive.find('.btn.active');
        var searchMode = activeLabel.data('searchmode'); // Get the value of the data-searchmode attribute

        if (searchMode === '<%= DbHelper.AlbertCoaches.SearchModeEnum.SearchAllUsers %>' && CanViewAllUsers) {
          IsSearchingMode_AllUsers = true;
          // Clear all selections in each select element within the div
          $multipleTagsRow.find('select').each(function () {
            $(this).val(null).trigger('change'); // Clear selections for select2
          });
          $multipleTagsRow.hide(); // Hide the div
          $('.pageTitle').text('All Users');

        } else {
          IsSearchingMode_AllUsers = false;
          $multipleTagsRow.show(); // Show the div
          $('.pageTitle').text('Partners');
        }

        return searchMode;
      }

      function SetActiveToggleValue(statusInactive) {
        var btn;
        GetCurrentSearchMode();

        if (IsSearchingMode_AllUsers) {
          btn = btnGrpActive.find('input[value="<%= DbHelper.AlbertCoaches.SearchModeEnum.SearchAllUsers %>"]');
        } else {
          if (statusInactive === true) {
            btn = btnGrpActive.find('input[value="<%= PathHelper.AbleUrlValues.PartnerStatus_Inactive %>"]');
        } else {
            btn = btnGrpActive.find('input[value="<%= PathHelper.AbleUrlValues.PartnerStatus_Active %>"]');
          }
        }

        if (btn.length === 1) {
          btn.trigger("click");
        }
      }

      function GetActiveToggleValue() {
        $checked = btnGrpActive.find("input:checked");
        if ($checked.length != 1) return "";
        return $checked.val();
      }

      function ActiveToggled(evt) {
        SearchKeyTimeout(txtSearch, true);
      }

      function UpdatedPartnerTags(evt) {
        if (IsSearchingMode_AllUsers) return;
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
        var newSearchMode = GetCurrentSearchMode();
        var minLength = <%= Min_Search_Length %>;
        var newPage = parseInt(inpGetPage.val(), 10);
        if (isNaN(newPage)) newPage = 1;

        const partnerTagIds = [];

        // Loop through each div element in multipleTagsRow with the class eachTagContent
        $('.eachTagContent').each(function () {

          const selectElement = $(this).find('select[name="PartnerTagCategoryIdPrefix"]'); // Find the select element within the current div

          const selectedOptionElements = selectElement.find('option:selected'); // Get selected option elements within the select element

          // Extract and store the selected values in the array
          selectedOptionElements.each(function () {
            partnerTagIds.push($(this).val());
          });
        });

        if (newSearchValue === currentSearchValue
          && newActiveToggleValue === currentActiveToggleValue
          && newPage === currentPage
          && isImmediate !== true) return;

        if (newSearchValue.length > 0 && newSearchValue.length < minLength) {
          return;
        }

        currentPage = newPage;
        currentSearchValue = newSearchValue;
        currentActiveToggleValue = newActiveToggleValue;
        currentSearchMode = newSearchMode;

        $.busyLoadFull("show");

        $.get(location.pathname,
          {
            "<%= PathHelper.AbleUrlKeys.PartnerSearchTerm %>": encodeURIComponent(currentSearchValue),
            "<%= PathHelper.AbleUrlKeys.PartnerStatusToggle %>": encodeURIComponent(currentActiveToggleValue),
            "<%= PathHelper.AbleUrlKeys.PartnerSearchMode %>": encodeURIComponent(currentSearchMode),
            "<%= PathHelper.AbleUrlKeys.PartnerTagIds %>": partnerTagIds.join(","),
            "<%= PathHelper.AbleUrlKeys.GetPage %>": newPage
          },
          function (data, status, jqXHR) {
            PopulateTableBody(data);
          },
          "json")
          .fail(function () { alert("Oops, there was a problem!"); })
          .always(function () { $.busyLoadFull("hide"); });
      }

      function GetTableRows(data) {

        $partnerListBody.empty();

        var colSpan = 3;
        if (canViewHiddenPartners) colSpan++;

        if (!data || !data.length) {
          $partnerListBody.append($("<tr>").append($("<td>").attr("colspan", colSpan).text("No results found.")));
          return;
        }

        for (var i = 0; i < data.length; i++) {

          var partner = data[i];
          var row = $("<tr>").attr("data-rowlink-id", partner.UserId);

          var avatarDiv = $("<div>").html(partner.PartnerName);
          var partnerName = $("<td>").html(avatarDiv).addClass("type-user-nameWithAvatar");

          var companyName = $("<td>").text(partner.CompanyName).addClass("type-company-name");

          var coachTagsDiv = $("<div>").addClass("coachListTags").html(partner.CoachTags);
          var coachTags = $("<td>").append(coachTagsDiv);

          if (IsSearchingMode_AllUsers && CanViewAllUsers) {
            $('.userTooltip').show();
            var tooltipHtml = $("<td>").html(partner.UserTooltipHtml).addClass("w30");
            row.append(partnerName, companyName, coachTags, tooltipHtml);

          } else {
            $('.userTooltip').hide();
            row.append(partnerName, companyName, coachTags);
          }

          if (canViewHiddenPartners) {
            var partnerHiddenIcon = $("<td>").addClass("w50").html(partner.PartnerHiddenIcon);
            row.append(partnerHiddenIcon);
          }

          $partnerListBody.append(row);
        }
        common_UpdateUI($partnerListBody);
      }

      function PopulateTableBody(json) {

        $partnerListBody.empty();

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

        var rowList = results.CoachInfoList;
        GetTableRows(rowList);
      }

    })(jQuery);
  </script>

</asp:Content>
