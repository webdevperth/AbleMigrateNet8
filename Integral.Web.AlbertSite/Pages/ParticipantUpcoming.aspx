<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ParticipantUpcoming.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ParticipantUpcoming"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <%= GetUserActionCards() %>
  <%= GetUserProjectOptions() %>

  <div id="paxUpcoming">
    <% if (!ProjectsUserIsIn.IsNullOrEmpty() && ProjectsUserIsIn.Count < 2) { %>
      <%= GetProjectUserHtml() %>
    <% } %>
  </div>

</asp:Content>
<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var selJobNumbers, divPaxUpcoming, actionsBarHeight, cardContainer, IsShowMoreToggleInPage;

      $(document).ready(function() {

        selJobNumbers = $('select[name="<%= FormFields.ProjectJobNumber %>"]');
        divPaxUpcoming = $("#paxUpcoming");
        cardContainer = $('.action-cards-container');
        IsShowMoreToggleInPage = $('.updateShowActions').length > 0;
        LoadBlockingModal();
        UpdateUpcomingPage();
        selJobNumbers.change(UpdateUpcomingPage);

        if (IsShowMoreToggleInPage) {
          actionsBarHeight = GetActionsRowHeight();
          cardContainer.height(actionsBarHeight); // Set initial height of action bar
        }

        UpdateShowActionCards();
        UpdateUpcomingPage();

        $(window).on('resize', function () {
          if (IsShowMoreToggleInPage) {
            actionsBarHeight = GetActionsRowHeight();
            if (cardContainer.hasClass('action-cards-container-row')) {
              // Apply only if in collapsed state
              cardContainer.height(actionsBarHeight);
            }
          }
        });

      }); // ready.

      function LoadBlockingModal() {

        <% if (ShowBlockingModal) { %>

          BootstrapDialog.show({
            title: "Purchase Services",
            closable: false,          // Remove the close button
            closeByBackdrop: false,  // Prevent closing when clicking backdrop
            closeByKeyboard: false,  // Prevent closing with ESC key
            onshow: function (dialogRef) {
              var modalDialog = dialogRef.getModalDialog();
              modalDialog.data("<%= WebHelper.DataAttrName.DialogRef %>", dialogRef);
              var modalBody = dialogRef.getModalBody();
              modalBody.busyLoad("show");
              modalBody.load("<%= PathHelper.Partials.PurchaseServices() %>",
                function (data) {
                  modalBody.html(data);
                  modalBody.busyLoad("hide");
                  common_UpdateUI(modalBody);
                }
              );
            }
          });
        <% } %>
      }

      function UpdateShowActionCards() {
        $('.updateShowActions').on('click', function () {

          // Check which class is currently applied and toggle accordingly
          if (cardContainer.hasClass('action-cards-container-row')) {

            cardContainer.css('height', 'auto'); // Expand
            cardContainer.removeClass('action-cards-container-row').addClass('action-cards-container-all');
            $(this).text('Show Less');

          } else if (cardContainer.hasClass('action-cards-container-all')) {

            cardContainer.height(actionsBarHeight); // Collapse
            cardContainer.removeClass('action-cards-container-all').addClass('action-cards-container-row');
            $(this).text('Show All');
          }
        });
      }

      function UpdateUpcomingPage() {

        if (selJobNumbers.length == 0) return;

        var projectJobNumber = selJobNumbers.val();
        AjaxSubmit({
          action: "<%= AjaxAction.ChangeProject %>",
          data: {
            "<%= FormFields.ProjectJobNumber %>": projectJobNumber
          },
          onSuccess: function (jqXHR, data) {
            divPaxUpcoming.empty();
            var upcomingHtml = data["<%= AjaxReturnData.UpcomingHtml %>"];
            divPaxUpcoming.html(upcomingHtml);
            common_UpdateUI(divPaxUpcoming);
          },
          onError: function (jqXHR, textStatus, errorThrown) {

          }
        });
      }

      function GetActionsRowHeight() {
        let height = 0;
        $('.action-card').slice(0, 1).each(function () { // Adjust the slice range as needed
          height += $(this).outerHeight(true); // true to include margin
        });
        return height;
      }

    })(jQuery);
  </script>

</asp:Content>
