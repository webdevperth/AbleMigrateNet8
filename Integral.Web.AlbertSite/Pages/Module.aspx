<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="Module.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.Module"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <script src="https://cdn.jsdelivr.net/npm/canvas-confetti@1.6.0/dist/confetti.browser.min.js"></script>

  <style>
    .module-content-info { margin-top: 15px; }
    .module-lessons-info .module-lessons,
    .module-lessons-info .module-span ion-icon { position: relative; top: -1px; }
    .content-container.needs-enrol { position: relative; }
    .table-responsive.disabled .table { pointer-events: none; opacity: 0.5; }
    .enrol-overlay { position: absolute; top: 0; left: 0; right: 0; bottom: 0; background: rgba(255, 255, 255, 0.5); z-index: 10; display: flex; align-items: center; justify-content: center; }
  </style>

  <section class="w100p">

    <div class="content-details-card module-banner-info flex-wrap">
      <div class="content-card-info">
        <% if (IsParticipantView) { %>
          <div class="flex">
            <%= WebHelper.Modules.GetParticipantModuleBadge(ParticipantModuleInfo) %>
            <%= WebHelper.Modules.GetProgramDatesBadge(ParticipantModuleInfo)  %>
          </div>
        <% } %>

        <h1><%= ModuleInfo.ModuleTitle %></h1>
        <p><%= ModuleInfo.ModuleSummary %></p>

        <div class="content-card-mid-section">
          <div class="content-card-creator">
            <img src="<%= PathHelper.Images.UserPhoto(ModuleInfo.AuthorFirstName, ModuleInfo.AuthorLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true) %>" />
            <span class="ml5"> Created by <%= ModuleInfo.GetAuthorFullName() %></span>
          </div>
        </div>

        <div class="module-content-info">
          <hr />
          <%= WebHelper.Modules.GetTotalLessonsFooterHtml(ModuleInfo.TotalLessons, true) %>

          <% if (IsParticipantView && ParticipantModuleInfo != null) { %>
            <%= WebHelper.Modules.GetModuleProgressBar(ParticipantModuleInfo.IsUserEnrolled, ParticipantModuleInfo.IsCompleted, ParticipantModuleInfo.CompletedPercentage) %>
          <% } %>
        </div>
      </div>
      <div class="content-card-coverimage">
        <img src="<%= PathHelper.Images.ContentCoverImage(ModuleInfo.ModuleGuid, PathHelper.Images.ContentCoverImageSize.DetailPage) %>" alt="<%= ModuleInfo.ModuleTitle %>">
      </div>
    </div>
    <div class="mt40">
      <%= WebHelper.GetTextDisplayRow("", 12, ModuleInfo.ModuleDescriptionHtml) %>

      <!-- TABLE -->
      <div class="module-content-container mt30 displaynone">
        <div class="<%= WebHelper.Content.CSS.ContentContainer %> <%= CanEnrolInModule ? " needs-enrol" : "" %>">
          <!-- Module Enrolment Overlay -->
          <div class="enrol-overlay">
            <div style="text-align: center;">
                <h3>Enrol to start module</h3>
                <button id="enrol-btn" class="btn btn-primary">Enrol Now</button>
            </div>
          </div>
          <%= WebHelper.Modules.GetModuleContentTableHtml(ModuleInfo, UserModuleContentInfo, ParticipantModuleInfo, WebHelper.Content.ViewFromPageEnum.Module) %>
        </div>
      </div>

    </div>

  </section><!-- /.content -->

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {

      var CanEnrolInModule, btnEnrol;

      $(document).ready(function () {

        CanEnrolInModule = <%= CanEnrolInModule.ToJSTrueFalse() ?? "false" %>;
        btnEnrol = $('#enrol-btn');

        if (CanEnrolInModule) {
          $('.table-responsive').addClass('disabled');
          $('.enrol-overlay').show();

          btnEnrol.on('click', ConfirmEnrolment);

        } else {
          $('.enrol-overlay').hide();

          var isModuleCompleted = <%= ParticipantModuleInfo?.IsCompleted.ToJSTrueFalse() ?? "false" %>;
          if (isModuleCompleted) {
            ShowCompletedModuleConfetti();
          }
        }

        // The div is not visible by default to avoid the panel glitching (to enrol), so fade in the whole div to make it look nice.
        $('.module-content-container.displaynone').removeClass('displaynone').hide().fadeIn(400);

      });

      function ShowCompletedModuleConfetti() {
        // Add a short delay to ensure library is loaded
        setTimeout(function () {
          // Check if confetti is available
          if (typeof confetti === "function") {
            // Trigger confetti
            confetti({
              particleCount: 100,
              spread: 70,
              origin: { y: 0.6 }
            });

            // Show message
            $("<div>")
              .text("🎉 Congratulations! You've completed the module.")
              .css({
                position: "fixed",
                top: "20px",
                left: "50%",
                transform: "translateX(-50%)",
                background: "#4caf50",
                color: "#fff",
                padding: "10px 20px",
                "border-radius": "5px",
                "font-size": "18px",
                "z-index": 9999
              })
              .hide()
              .appendTo("body")
              .fadeIn(500)
              .delay(3000)
              .fadeOut(500);
          }
        }, 500); // 500ms delay — tweak this if needed
      }

      function ConfirmEnrolment() {

        if (!CanEnrolInModule) {
          return;
        }

        BootstrapDialog.show({
          type: BootstrapDialog.TYPE_WARNING,
          title: 'Confirmation',
          message: "Are you sure you want to enrol in this module?",
          buttons: [
            {
              label: 'No', cssClass: 'btn-secondary',
              action: function (dialog) { dialog.close(); }
            },
            {
              label: 'Yes', cssClass: 'btn-primary',
              action: function (dialog) {
                dialog.close();
                EnrolUserInModule();
              }
            }
          ]
        });
      }

      function EnrolUserInModule() {
        AjaxSubmit({
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.Enrol %>",
          onSuccess: function (jqXHR, data) {
            // Remove overlay and re-enable table
            $('.<%= WebHelper.Content.CSS.ContentContainer %>').removeClass('needs-enrol');
            $('.table-responsive').removeClass('disabled');
            $('.enrol-overlay').fadeOut(300, function () {
              $(this).remove();
            });
          },
          onFail: function (jqXHR, data) {
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Reorder failed, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

    })(jQuery);

  </script>

</asp:Content>


