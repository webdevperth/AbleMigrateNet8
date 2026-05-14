<%@ Page Language="C#" AutoEventWireup="true" CodeFile="OrganisationSettings.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.OrganisationSettings" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.css">
  <script src="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.js"></script>

  <form class="form-horizontal" id="clientForm">

    <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.Update %>" />
    <input type="hidden" name="CompanyId" value="<%= IsNewCompany ? "new" : CompanyInfo.CompanyId.ToString() %>" />

    <% new WebHelper.Form.FormRow() {
        LabelText = "Company Name:",
        ContentHtml = new WebHelper.Form.TextInput() {
          InputName = FormFields.CompanyName,
          Value = CompanyInfo.CompanyName,
          IsReadOnly = !CanUpdateCompany
        }.ToHtml()
      }.WriteHtml(); %>

    <% new WebHelper.Form.FormRow() {
        LabelText = "Web Site URL:",
        ContentHtml = new WebHelper.Form.TextInput() {
          InputName = FormFields.WebSiteUrl,
          Value = CompanyInfo.WebSiteUrl,
          IsReadOnly = !CanUpdateCompany
        }.ToHtml()
      }.WriteHtml(); %>

    <% new WebHelper.Form.FormRow() {
        LabelText = "Total Number of Staff:",
        ContentHtml = new WebHelper.Form.TextInput() {
          InputName = FormFields.NumberOfStaff,
          Value = CompanyInfo.NumberOfStaff.ToString(),
          IsReadOnly = !CanUpdateCompany
        }.ToHtml()
      }.WriteHtml(); %>

    <% new WebHelper.Form.FormRow() {
        LabelText = "City:",
        ContentHtml = new WebHelper.Form.TextInput() {
          InputName = FormFields.City,
          Value = CompanyInfo.City,
          IsReadOnly = !CanUpdateCompany
        }.ToHtml()
      }.WriteHtml(); %>

    <% new WebHelper.Form.FormRow() {
        LabelText = "Country:",
        ContentHtml = new WebHelper.Form.Select() {
          InputName = FormFields.CountryId,
          TopOptionsHtml = GetCountryOptionsHtml(),
          IsReadOnly = !CanUpdateCompany
        }.ToHtml()
      }.WriteHtml(); %>

    <% new WebHelper.Form.FormRow() {
        LabelText = "Sector:",
        ContentHtml = new WebHelper.Form.Select() {
          InputName = FormFields.SectorId,
          TopOptionsHtml = GetSectorOptionsHtml(),
          IsReadOnly = !CanUpdateCompany
        }.ToHtml()
      }.WriteHtml(); %>

    <% new WebHelper.Form.FormRow() {
        LabelText = "Client Lead:",
        ContentHtml = new WebHelper.Form.Select() {
          InputName = FormFields.ClientLeadUserId,
          TopOptionsHtml = GetClientLeadDropdownOptionsHtml(),
          Class = "partnerdropdownavatarimg",
          IsReadOnly = !CanUpdateCompany
        }.ToHtml()
      }.WriteHtml(); %>

    <% new WebHelper.Form.FormRow() {
        LabelText = "AI Context:",
        ContentHtml = new WebHelper.Form.TextArea() {
          InputName = FormFields.AI_Context,
          Value = CompanyInfo.AI_Context,
          IsReadOnly = !CanUpdateCompany,
          IsRichText = true
        }.ToHtml()
      }.WriteHtml(); %>

    <% if (!IsNewCompany) { %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Display Logo",
          LabelHelpText = "Choose whether to display your logo in the Navigation Bar",
          ContentHtml = new WebHelper.Form.CheckBox() {
            InputName = FormFields.DisplayLogoInNavBar,
            Checked = CompanyInfo.DisplayLogoInNavBar,
            IsReadOnly = !CanUpdateDisplayLogoInNavBar
          }.ToHtml()
        }.WriteHtml();
      %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Company Logo:",
          ContentHtml = CompanyLogoControl.ToHtml()
        }.WriteHtml();
      %>

    <% } %>

    <% if (CanUpdateCompany) { %>
      <div class="row mt30">
        <div class="col-lg-8 col-md-12">
          <button type="button" id="btnSave" class="btn btn-primary floatright" data-waitmsg="Updating..."><%= IsNewCompany ? "Add Company" : "Update Details" %></button>
        </div>
      </div>
    <% } %>

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
            <img class="cropperImage floatleft" src="<%= PathHelper.Images.TenantOrgLogo(CompanyInfo, true) %>" alt="Project Logo">
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
    (function($) {

      var $clientForm = $("#clientForm");
      var btnSave = $("#btnSave");
      var IsNewCompany = <%= IsNewCompany.ToJSTrueFalse() %>;
      var Cropper, cropper, $cropperModal, $imgCompanyLogo, $cropperModalImage;

      $(document).ready(function() {

        $imgCompanyLogo = $("#<%= CompanyLogoControl.ImgID %>");

        Cropper = window.Cropper;
        $cropperModal = $("#cropperModal");
        $cropperModalImage = $("#cropperModal .cropperImage");

        if (IsNewCompany) {
          // Adjust menu.
          $(".activeparent > ul.submenu > li.active a span").text("New Company");
          $(".activeparent > ul.submenu > li:not(.active)").hide();
        }

        $('#<%= CompanyLogoControl.InputID %>').on('change', function () {
          CropImage(this);
        });

        $("#cropperModal .btnRotateLeft").click(function (e) { cropper.rotate(-90); });
        $("#cropperModal .btnRotateRight").click(function (e) { cropper.rotate(90); });
        $("#cropperModal .btnZoomOut").click(function (e) { cropper.zoom(-0.1); });
        $("#cropperModal .btnZoomIn").click(function (e) { cropper.zoom(0.1); });

        $("#btnCropDone").click(function (e) {
          if (typeof cropper != 'object') return;
          var croppedCanvas = cropper.getCroppedCanvas();
          $imgCompanyLogo.attr("src", croppedCanvas.toDataURL());
          croppedCanvas.toBlob(function (blob) {
            $imgCompanyLogo.data("blob", blob);
            $cropperModal.modal("hide")
          }, $cropperModal.data("img-mimetype"), 0.9);
        });

        $cropperModal.on('hidden.bs.modal', function () {
          if (typeof cropper != "undefined" && cropper != null) {
            if (cropper.destroy) cropper.destroy();
            cropper = null;
          }
        });

        btnSave.click(SaveChanges);

      }); // ready.

      function SaveChanges() {
        AjaxSubmit({
          form: $clientForm,
          action: "<%= AjaxAction.Update %>",
          onSuccess: function (jqXHR, data) { }
        });
      }

      function CropImage(fileInput) {

        $cropperModal.toggleClass("IsLogo", true);
        $cropperModal.data("img-target", $imgCompanyLogo);
        $cropperModal.data("img-mimetype", "image/png");

        if (fileInput.files && fileInput.files[0]) {
          if (fileInput.files[0].type.match(/^image\//)) {
            var reader = new FileReader();
            reader.onload = function (evt) {
              $cropperModalImage.on("load", function () {

                if (typeof cropper != "undefined" && cropper != null) {
                  if (cropper.destroy) cropper.destroy();
                  cropper = null;
                }

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

    })(jQuery);
  </script>

</asp:Content>
