<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProgramContent.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProgramContent"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (CanSearchContent || CanAddContentToProgram) { %>
    <div class="content-action-bar">
      <div class="left">
        <% if (CanSearchContent) { %>
          <div class="search-input">
            <i class="fa fa-search"></i>
            <input type="text" id="txtSearch" name="<%= FormFields.SearchTerm %>" value="" placeholder="Search any keyword..." autofocus="autofocus">
          </div>
        <% } %>
      </div>

      <div class="right">
        <% if (CanAddContentToProgram) { %>
          <a class="btn btn-primary float-right" href="<%= PathHelper.Pages.ContentDetails_AddContent(ProgramInfo.ProgramJobId) %>">Add Microlearning</a>
        <% } %>
      </div>
    </div>
  <% } %>

  <%= GetPanelHtml() %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {


      var programModuleContainer, moduleLibraryContainer, programContentContainer, contentLibraryContainer;
      var txtSearch, lastSearchValue, canSearchContent, canAddContent;
      var keyTimeout = null;

      $(document).ready(function () {

        lastSearchValue = "";
        canSearchContent = <%= CanSearchContent.ToJSTrueFalse() %>;
        canAddContent = <%= CanAddContentToProgram.ToJSTrueFalse() %>;
        contentLibraryContainer = $(".<%= WebHelper.Content.CSS.ContentLibraryContainer %> .<%= WebHelper.Content.CSS.ContentCardContainer %>");
        moduleLibraryContainer = $(".<%= WebHelper.Modules.CSS.ModuleLibraryContainer %> .<%= WebHelper.Content.CSS.ContentCardContainer %>");
        programContentContainer = $(".<%= WebHelper.Content.CSS.ProgramContentContainer %>");
        programModuleContainer = $(".<%= WebHelper.Modules.CSS.ProgramModuleContainer %>");

        txtSearch = canSearchContent ? $("#txtSearch") : $("<input>").attr({ type: "text", id: "txtSearch" });;
        txtSearch.val("");

        if (canSearchContent) {
          txtSearch.keyup(SearchKeyUp);
        }

        $(document).on('click', '.<%= WebHelper.Content.CSS.ContentCardTopContainer %> .<%= WebHelper.Content.CSS.AddContentButton %>', function (ev) {
          GetCardInfo(ev, '.<%= WebHelper.Content.CSS.AddContentButton %>');
        });
        $(document).on('click', '.<%= WebHelper.Content.CSS.ContentCardTopContainer %> .<%= WebHelper.Content.CSS.RemoveContentButton %>', function (ev) {
          GetCardInfo(ev, '.<%= WebHelper.Content.CSS.RemoveContentButton %>');
        });


      }); // ready.

      function GetCardInfo(ev, action) {

        ev.preventDefault();

        var $target = $(ev.target); // Get clicked element
        var $tabPane = $target.closest('.tab-pane'); // Get clicked tab
        var $itemCardHtml = $target.closest('.content-card');

        // Identify which tab we're in by class
        var isContentTab = $tabPane.hasClass('tab-<%= TypeTabsEnum.Content %>');
        var isModuleTab = $tabPane.hasClass('tab-<%= TypeTabsEnum.Module %>');

        // Depending on the tab, get the right data attribute
        var itemId, tabType;
        if (isContentTab) {
          itemId = $target.data('<%= WebHelper.Content.DataAttrs.ContentId %>');
          tabType = '<%= TypeTabsEnum.Content %>';
        } else if (isModuleTab) {
          itemId = $target.data('<%= WebHelper.Modules.DataAttrs.ModuleId %>');
          tabType = '<%= TypeTabsEnum.Module %>';
        } else {
          return; // Unknown tab
        }

        if (typeof itemId === 'undefined' || itemId === null) {
          common_InfoDialog("Item not found, please try again");
          return;
        }

        // Redirect to the action handler
        switch (action) {
          case '.<%= WebHelper.Content.CSS.RemoveContentButton %>':
            DeleteItemModal(itemId, $itemCardHtml, tabType);
            break;
          case '.<%= WebHelper.Content.CSS.AddContentButton %>':
            AddItemModal(itemId, $itemCardHtml, tabType);
            break;
          default:
            common_InfoDialog("Action not found, please try again");
        }

      }

      function DeleteItemModal(itemId, itemCardHtml, tabType) {

        var message = (tabType === '<%= TypeTabsEnum.Content %>')
          ? "Are you sure you want to remove this microlearning from the program?"
          : "Are you sure you want to remove this module from the program?";

        BootstrapDialog.show({
          type: BootstrapDialog.TYPE_WARNING,
          title: 'Confirmation',
          message: message,
          buttons: [
            {
              label: 'No', cssClass: 'btn-secondary',
              action: function (dialog) { dialog.close(); }
            },
            {
              label: 'Yes', cssClass: 'btn-primary',
              action: function (dialog) {
                dialog.close();
                if (tabType == '<%= TypeTabsEnum.Content %>') {

                  RemoveContentFromProgram(itemId, itemCardHtml);

                } else if (tabType == '<%= TypeTabsEnum.Module %>') {

                  RemoveModuleFromProgram(itemId, itemCardHtml);

                } else {

                  return;
                }
              }
            }
          ]
        });
      }

      function RemoveContentFromProgram(contentId, contentCardHtml) {

        AjaxSubmit({
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.RemoveContentFromProgram %>",
          data: {
            "<%= FormFields.ContentId %>": contentId
          },
          onSuccess: function (jqXHR, data) {
            var cardHtml = data["<%= AjaxReturnData.CardItemHtml %>"];
            UpdateCardContainer(programContentContainer, contentCardHtml, false); // Remove the added content from Program
            UpdateCardContainer(contentLibraryContainer, cardHtml, true); // Update items in library

            common_SuccessToast("Microlearning removed from program successfully.");
          },
          onFail: function (jqXHR, data) {
            common_ErrorToast("Failed to add Microlearning to program.");
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_ErrorToast("Failed to add Microlearning to program, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function RemoveModuleFromProgram(moduleId, moduleCardHtml) {

        AjaxSubmit({
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.RemoveModuleFromProgram %>",
          data: {
            "<%= FormFields.ModuleId %>": moduleId
          },
          onSuccess: function (jqXHR, data) {
            var cardHtml = data["<%= AjaxReturnData.CardItemHtml %>"];
            UpdateCardContainer(programModuleContainer, moduleCardHtml, false); // Remove the added content from Program
            UpdateCardContainer(moduleLibraryContainer, cardHtml, true); // Update items in library

            common_SuccessToast("Module removed from program successfully.");
          },
          onFail: function (jqXHR, data) {
            common_ErrorToast("Failed to add Module to program.");
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_ErrorToast("Failed to add Module to program, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function AddItemModal(itemId, itemCardHtml, tabType) {
        var dialogTitle = (tabType === '<%= TypeTabsEnum.Content %>')
          ? "Add Microlearning to Program"
          : "Add Module to Program";

        var modalId = tabType === '<%= TypeTabsEnum.Content %>' ? '#AddContentModal' : '#AddModuleModal';

        var dlg = common_InfoDialog(modalId, {
          name: "Add" + tabType,
          title: dialogTitle,
          width: '40%',
          buttons: [
            { text: "Cancel", class: "btn-secondary mr20 left", isDefault: false, isPrimary: false, close: true },
            {
              text: "Add", class: "mr20 left", isDefault: true, isPrimary: true, close: false, click: function (e) {
                if (tabType == '<%= TypeTabsEnum.Content %>') {

                  AddContentToProgram(itemId, itemCardHtml);

                } else if (tabType == '<%= TypeTabsEnum.Module %>') {

                  AddModuleToProgram(itemId, itemCardHtml);

                } else {

                  return;
                }
              }
            }
          ],
          shown: function () { },
          hide: function () { }
        });
      }

      function AddContentToProgram(contentId, contentCardHtml) {

        AjaxSubmit({
          form: $("#formAddContent"),
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.AddContentToProgram %>",
          data: {
            "<%= FormFields.ContentId %>": contentId
          },
          onSuccess: function (jqXHR, data) {
            var cardHtml = data["<%= AjaxReturnData.CardItemHtml %>"];
            UpdateCardContainer(contentLibraryContainer, contentCardHtml, false); // Remove card from Library
            UpdateCardContainer(programContentContainer, cardHtml, true); // Add card to items in program

            $(".modal-content .close").trigger('click'); // Close Modal
            $("#formAddContent")[0].reset(); // Reset values of forms in Modal
            common_SuccessToast("Microlearning added to program successfully.");
          },
          onFail: function (jqXHR, data) {
            common_ErrorToast("Failed to add Microlearning to program.");
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_ErrorToast("Failed to add Microlearning to program, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function AddModuleToProgram(moduleId, moduleCardHtml) {

        AjaxSubmit({
          form: $("#formAddModule"),
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.AddModuleToProgram %>",
          data: {
            "<%= FormFields.ModuleId %>": moduleId
          },
          onSuccess: function (jqXHR, data) {
            var cardHtml = data["<%= AjaxReturnData.CardItemHtml %>"];
            UpdateCardContainer(moduleLibraryContainer, moduleCardHtml, false); // Remove card from Library
            UpdateCardContainer(programModuleContainer, cardHtml, true); // Add card to items in program

            $(".modal-content .close").trigger('click'); // Close Modal
            $("#formAddModule")[0].reset(); // Reset values of forms in Modal
            common_SuccessToast("Module added to program successfully.");
          },
          onFail: function (jqXHR, data) {
            common_ErrorToast("Failed to add module to program.");
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_ErrorToast("Failed to add module to program, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function UpdateCardContainer(divContainer, cardHtml, isAdding) {
        const badgeSelector = 'p.badge-no-record';

        if (isAdding) {
          // Remove the "no record" badge if it exists
          divContainer.find(badgeSelector).remove();
          // Append the new card HTML
          divContainer.append(cardHtml);
        } else {
          // Remove the cardHtml
          cardHtml.remove();

          // If the div is now empty, add the "no record" badge
          if (divContainer.children().length === 0) {
            divContainer.append('<%= WebHelper.GetNoRecordsBadge() %>');
          }
        }
      }

      function SearchKeyUp(ev, data) {

        if (!canSearchContent) return;

        var isImmediate = (data && data.immediate);
        if (keyTimeout) clearTimeout(keyTimeout);
        if (isImmediate) SearchContentLibrary();
        else keyTimeout = setTimeout(function () { keyTimeout = null; SearchContentLibrary(); }, 800);
      }

      function SearchContentLibrary() {

        if (!canAddContent && !canSearchContent) return;

        var searchValue = txtSearch.val();
        var minSearchVal = 3;

        if (searchValue != lastSearchValue || searchValue.length > minSearchVal) {
          contentLibraryContainer.empty();
          moduleLibraryContainer.empty();
          lastSearchValue = searchValue;

          AjaxSubmit({
            url: "<%= PathHelper.CurrentUrl %>",
            action: "<%= AjaxAction.SearchContentLibrary %>",
            data: {
              "<%= FormFields.SearchTerm %>": searchValue
            },
            onSuccess: function (jqXHR, data) {
              contentLibraryContainer.append(data["<%= AjaxReturnData.ContentLibraryHtml %>"]);
              moduleLibraryContainer.append(data["<%= AjaxReturnData.ModuleLibraryHtml %>"]);
            },
            onFail: function (jqXHR, data) {
            },
            onError: function (jqXHR, textStatus, errorThrown) {
              common_InfoDialog("Failed to get content, please try again later.");
            },
            onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) {
              txtSearch.focus();
            }
          });
        }
      }

    })(jQuery);
  </script>

</asp:Content>


