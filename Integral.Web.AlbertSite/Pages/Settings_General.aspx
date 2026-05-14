<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Settings_General.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.Settings_General" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.css">
  <script src="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.js"></script>

  <form class="form-horizontal" id="companyForm">

    <%= WebHelper.GetTextInput("Company Name:", FormFields.OrgName, TenantOrgInfo.OrgName, 5) %>
    <%= WebHelper.GetTextInput("Company Friendly Name:", FormFields.OrgFriendlyName, TenantOrgInfo.OrgFriendlyName, 5) %>
    <%= WebHelper.GetTextInput("Bus. Identification Number:", FormFields.BusinessIdNumber, TenantOrgInfo.BusinessIDNumber, 3) %>
    <%= WebHelper.GetTextInput("Contact Phone Number:", FormFields.ContactPhoneNumber, TenantOrgInfo.OrgPhone, 3) %>
    <%= WebHelper.GetTextInput("General Email:", FormFields.GeneralEmail, TenantOrgInfo.OrgEmail, 5) %>
    <%= WebHelper.GetTextInput("Website URL:", FormFields.WebSiteURL, TenantOrgInfo.WebSiteURL, 5) %>
    <%= WebHelper.GetFormSubheader("Custom Sender Email") %>
    <%= WebHelper.GetTextInput("Sender Email Name:", FormFields.GenericSenderEmailName, TenantOrgInfo.GenericSenderEmailName, 5) %>
    <%= WebHelper.GetTextInput("Sender Email Address:", FormFields.GenericSenderEmailAddress, TenantOrgInfo.GenericSenderEmailAddress, 5) %>

    <% new WebHelper.Form.FormRow() {
        LabelPosition = WebHelper.Form.LabelPosition.LeftLegacy,
        LabelText = "Company Logo:",
        ContentHtml = CompanyLogoControl.ToHtml()
      }.WriteHtml(); %>

    <br />
    <% new WebHelper.Form.FormRow() {
        LabelPosition = WebHelper.Form.LabelPosition.LeftLegacy,
        ContentHtml = WebHelper.GetButton(
        "Update Details",
        "btnUpdateCompany")
      }.WriteHtml(); %>

  </form>

  <div id="cropperModal" class="modal fade" data-backdrop="static" tabindex="-1" role="dialog">
    <div class="modal-dialog" role="document">
      <div class="modal-content">
        <div class="modal-header"><h4 class="modal-title mt10">Adjust Image</h4></div>
        <div class="modal-body">
          <div class="img-container">
            <div class="floatright w200 pl10 pr10 sidenote">
              Change the framing of the image if needed.<br/>
              <br/>
              Use the controls below to rotate and zoom.<br/>
              When zoomed in, use the mouse to move the image in the frame.
            </div>
            <img class="cropperImage floatleft" src="<%= PathHelper.Images.TenantOrgLogo(TenantOrgInfo, true) %>" alt="Project Logo">
          </div>
        </div>
        <div class="modal-footer">
          <div class="actions floatleft">
            <div class="actions-titles floatleft">
              <div class="buttonset">
                <div class="title">Rotate</div>
                <div class="title title-sec">Zoom</div>
              </div>
            </div>
            <div class="buttonset">
              <button type="button" class="btn btn-secondary btnRotateLeft" title="Rotate Left"><img src="<%= PathHelper.UrlPath.Images %>btn-cropper-rotate-left.svg" /></button>
              <button type="button" class="btn btn-secondary btnRotateRight" title="Rotate Right"><img src="<%= PathHelper.UrlPath.Images %>btn-cropper-rotate-right.svg" /></button>
            </div>
            <div class="buttonset">
              <button type="button" class="btn btn-secondary btnZoomOut" title="Zoom Out"><i class="fas fa-search-minus"></i></button>
              <button type="button" class="btn btn-secondary btnZoomIn" title="Zoom In"><i class="fas fa-search-plus"></i></button>
            </div>
          </div>
          <div class="buttonsAction floatright">
            <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancel</button>
            <button type="button" id="btnCropDone" class="btn btn-primary">Done</button>
          </div>
        </div>
      </div>
    </div>
  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">

    (function ($) {

      var $companyForm = $("#companyForm");
      var $btnUpdateCompany = $("#btnUpdateCompany");

      var $imgCompanyLogo, $btnSelectCompanyLogo
      var Cropper, cropper, $cropperModal, $cropperModalImage;

      $(document).ready(function () {

        $imgCompanyLogo = $("#<%= CompanyLogoControl.ImgID %>");
        $btnSelectCompanyLogo = $('#<%= CompanyLogoControl.InputID %>');

        Cropper = window.Cropper;
        $cropperModal = $("#cropperModal");
        $cropperModalImage = $("#cropperModal .cropperImage");

        $btnUpdateCompany.click(UpdateCompany);

        $btnSelectCompanyLogo.on('change', function () { CropImage(this, true, $imgCompanyLogo, "image/png"); });

        $("#cropperModal .btnRotateLeft").click(function (e) { cropper.rotate(-90); });
        $("#cropperModal .btnRotateRight").click(function (e) { cropper.rotate(90); });
        $("#cropperModal .btnZoomOut").click(function (e) { cropper.zoom(-0.1); });
        $("#cropperModal .btnZoomIn").click(function (e) { cropper.zoom(0.1); });

        $("#btnCropDone").click(function (e) {
          if (typeof cropper != 'object') return;
          var croppedCanvas = cropper.getCroppedCanvas();
          var $imgTarget = $cropperModal.data("img-target");
          $imgTarget.attr("src", croppedCanvas.toDataURL());
          croppedCanvas.toBlob(function (blob) {
            $imgTarget.data("blob", blob);
            $cropperModal.modal("hide")
          }, $cropperModal.data("img-mimetype"), 0.9);
        });

        $cropperModal.on('hidden.bs.modal', function () {
          if (typeof cropper != "undefined" && cropper != null) {
            if (cropper.destroy) cropper.destroy();
            cropper = null;
          }
        });

      }); // ready.

      function CropImage(fileInput, isLogo, $imgTarget, imgMimeType) {

        $cropperModal.toggleClass("IsLogo", isLogo);
        $cropperModal.data("img-target", $imgTarget);
        $cropperModal.data("img-mimetype", imgMimeType);

        if (fileInput.files && fileInput.files[0]) {
          if (fileInput.files[0].type.match(/^image\//)) {
            var reader = new FileReader();
            reader.onload = function (evt) {
              $cropperModalImage.on("load", function () {

                if (typeof cropper != "undefined" && cropper != null) {
                  if (cropper.destroy) cropper.destroy();
                  cropper = null;
                }

                if (isLogo) {
                  cropper = new Cropper($cropperModalImage[0], {
                    viewMode: 2,
                    autoCrop: true,
                    autoCropArea: 1,
                    toggleDragModeOnDblclick: false,
                    restore: false,
                    movable: true,
                    rotatable: true,
                    scalable: true,
                    zoomOnWheel: false,
                    minContainerWidth: 400,
                    maxContainerWidth: 400,
                    minContainerHeight: 250,
                    maxContainerHeight: 250,
                    minCanvasWidth: 400,
                    minCanvasHeight: 250,
                    ready: function () {
                      $cropperModal.modal();
                    }
                  });
               } else {
                  cropper = new Cropper($cropperModalImage[0], {
                    viewMode: 3,
                    autoCrop: false,
                    autoCropArea: 1,
                    aspectRatio: 1,
                    dragMode: "move",
                    toggleDragModeOnDblclick: false,
                    restore: false,
                    guides: false,
                    center: false,
                    highlight: false,
                    movable: true,
                    rotatable: true,
                    scalable: true,
                    cropBoxMovable: false,
                    cropBoxResizable: false,
                    zoomOnWheel: false,
                    minContainerWidth: 250,
                    maxContainerWidth: 250,
                    minContainerHeight: 250,
                    maxContainerHeight: 250,
                    minCropBoxWidth: 250,
                    minCropBoxHeight: 250,
                    minCanvasWidth: 250,
                    minCanvasHeight: 250,
                    ready: function () {
                      this.cropper.crop(); // as autoCrop=false
                      $cropperModal.modal();
                    }
                  });
                }
                $cropperModal.data("cropper", cropper);
              });
              $cropperModalImage.attr("src", evt.target.result);
            };
            reader.readAsDataURL(fileInput.files[0]);
          }
          else {
            alert("Invalid file type! Please select an image file.");
          }
        } else {
          alert('No file(s) selected.');
        }
      }

      function UpdateCompany() {

        AjaxSubmit({
          form: $companyForm,
          action: "<%= AjaxAction.UpdateCompany %>",
          onSuccess: function (jqXHR, data) {
            var logoUrl = "<%= PathHelper.Images.TenantOrgLogo(TenantOrgInfo, true) %>";
            SavePhoto("<%= AjaxAction.TenantOrgLogo %>", data.CoachId, $imgCompanyLogo, logoUrl);
          }
        });
      }

      function SavePhoto(strAjaxAction, coachId, $imgTarget, photoImageUrl, placeholderImageUrl) {

        if ($imgTarget.data("blob") == null) return;

        jQuery.ajax({
          data: {
            "CoachId": coachId,
            "image": $imgTarget.data("blob")
          },
          action: strAjaxAction,
          processData: false,
          contentType: false
        }).done(function (response) {
          if (typeof placeholderImageUrl == "string") {
            // Change placeholder urls (if present) to proper photo url.
            $('img[src^="' + placeholderImageUrl + '"]').each(function (i, img) { img.src = photoImageUrl; });
          }
          // Force browser to refresh all images which have the photo url.
          app_ReloadImage(photoImageUrl);
        });
      }

    })(jQuery);
  </script>

</asp:Content>
