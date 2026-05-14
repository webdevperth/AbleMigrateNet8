<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ModuleEdit.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ModuleEdit"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.css">
  <script src="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.js"></script>

  <style>
    .preview-cover-image { height: 150px; width: 280px; border-radius: 5px; }

    .content-attachment-container { display: flex; flex-direction: column; padding: 10px; border: 1px solid #ccc; border-radius: 5px; width: 300px; margin: 10px; margin-left: 0px; }
    .content-attachment { display: flex; align-items: center; }
    .content-download-link { font-size: 12px; font-weight: bold; cursor: pointer; }

    .summaryContainer { position: relative; display: inline-block; width: 100% }
    .summaryContainer input { padding-right: 50px !important; }
    .summaryContainer input:focus { z-index: inherit !important; }
    .char-counter { position: absolute; right: 2%; bottom: 5px; color: gray; font-size: 12px; }
    .displaynone .content-card-container { display: none !important; }
    .module-content-container { min-height: 30px; }
    .content-card .added { display: none !important; }

  </style>

  <section class="w100p">

    <form id="moduleForm" class="form-horizontal">

      <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.UpdateModule %>" />

      <%= GetAuthorHtml() %>

      <%= WebHelper.GetTextInput("Title:", FormFields.ModuleTitle, ModuleInfo.ModuleTitle, "", 1, 11, "", !CanEditModule) %>

      <div class="summaryContainer">
        <%= WebHelper.GetTextInput("Summary:", FormFields.ModuleSummary, ModuleInfo.ModuleSummary, "", 1, 11, "", !CanEditModule, false, WebHelper.InputMaxLength.ContentSummary) %>
        <p id="summaryCharCount" class="char-counter"></p>
      </div>

      <%= WebHelper.GetRichTextArea($"Description: {GetRequiredFieldIconHtml()}", FormFields.ModuleDescriptionHtml, 1, 11, ModuleInfo.ModuleDescriptionHtml, "", !CanEditModule) %>

      <div class="publicSelection">
        <%= WebHelper.CustomCheckBoxRow("", 1, FormFields.ShowToParticipants, "1", ModuleInfo.ShowToParticipants, !CanSetPublicForParticipants, "Global Participants.", GetPublicTooltipInfo(FormFields.ShowToParticipants, CanSetPublicForParticipants)) %>
        <%= WebHelper.CustomCheckBoxRow("", 1, FormFields.ShowToPartners, "1", ModuleInfo.ShowToPartners, !CanSetPublicForPartners, "Global Partners.", GetPublicTooltipInfo(FormFields.ShowToPartners, CanSetPublicForPartners)) %>
      </div>
      <%= WebHelper.CustomCheckBoxRow("", 1, FormFields.IsPublished, "1", ModuleInfo.IsPublished, !CanEditIsPublished, "Publish.", GetPublicTooltipInfo(FormFields.IsPublished, CanEditIsPublished)) %>

      <div class="row form-group ajaxSubmit-field">
        <label class="control-label col-md-1 col-sm-12 col-xs-12">Cover Image: <%= GetRequiredFieldIconHtml() %></label>
        <div class="col-md-9 col-sm-12 col-xs-12">
          <div class="file-selection">
            <div class="content-attachment-container">
              <div class="content-attachment">
                <img id="imgCoverImage" class="preview-cover-image"
                  src="<%= PathHelper.Images.ContentCoverImage(ModuleInfo.ModuleGuid, PathHelper.Images.ContentCoverImageSize.Card) %>" alt="Cover Image">
              </div>
            </div>
            <% if (CanEditModule) { %>
              <input type="file" title="Select an Image" id="btnSelectCoverImage" accept="image/*" />
              <!-- Error message will be inserted here if conditions are met -->
              <div class="<%= WebHelper.CSSClasses.AjaxFieldErrorMsg %> error-imgCover" style="display: none;">
                <p>Cover Image is required.</p>
              </div>
            <% } %>
          </div>
        </div>
      </div>

      <% if (!IsNewModule) { %>
        <div class="module-content-container mt30">
          <% if (CanAddContentItems) { %>
            <div class="button-container" style="text-align: right; margin-bottom: 15px;">
              <button class="btn btn-primary btnAddMicrolearningItem">Add Microlearning</button>
            </div>
          <% } %>
          <div class="<%= WebHelper.Content.CSS.ContentContainer %>">
            <%= WebHelper.Modules.GetModuleContentTableHtml(ModuleInfo, ContentInModuleList, CanDeleteContentItems, WebHelper.Content.ViewFromPageEnum.ModuleEdit) %>
          </div>
        </div>

        <%= GetContentLibraryModalHtml() %>

      <% } %>

      <% if (IsProgramView) { %>
        <hr />
        <%= WebHelper.GetInputDateRow("Scheduled Send Date:", FormFields.ScheduledSendDateUtc, ProgramModuleInfo?.ScheduledSendDateUtc, 4, 4, "", !CanEditModule) %>
        <%= WebHelper.GetWorkshopDropdownHtml(WorkshopListInfo, FormFields.WorkshopEventId, 4, 6, !CanEditModule) %>
      <% } %>

      <% if (CanEditModule) { %>
        <div class="btnholder">
          <% if (CanDeleteModule) { %>
            <button type="button" class="btn btn-warning btnDeleteModule">Delete</button>
          <% } %>
          <a href="<%= PathHelper.Pages.Module(ModuleInfo?.ModuleGuid) %>" target="_blank" class="btn btn-secondary mr10 btnPreview <%= IsNewModule ? "display-none" : "" %>">Preview</a>
          <button type="button" class="btn btn-primary floatright btnSaveModule">Save Module</button>
        </div>
      <% } %>
    </form>

    <div id="cropperModal" class="modal fade" data-backdrop="static" tabindex="-1" role="dialog">
      <div class="modal-dialog" role="document">
        <div class="modal-content">
          <div class="modal-header">
            <h4 class="modal-title mt10">Adjust Image</h4>
          </div>
          <div class="modal-body">
            <div class="img-container">
              <div class="floatright w200 pl10 pr10 sidenote">
                Change the framing of the image if needed.<br/>
                <br/>
                Use the controls below to rotate and zoom.<br/>
                When zoomed in, use the mouse to move the image in the frame.<br/>
              </div>
              <img class="cropperImage floatleft" src="about:blank" alt="Crop Image">
            </div>
          </div>
          <div class="modal-footer">
            <div class="actions-titles floatleft">
              <div class="buttonset">
                <div class="title">Rotate</div>
                <div class="title title-sec">Zoom</div>
              </div>
            </div>
            <div class="actions floatleft">
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

  </section><!-- /.content -->

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {

      var btnSaveModule = $(".btnSaveModule");
      var $imgCoverImage, moduleGuid, summaryInput;
      var Cropper, cropper, $cropperModal, $cropperModalImage;
      var isNewModule, isUpdatingFiles, isProgramView, contentContainer;
      var $publicSelectionDiv, $requiredFieldDiv, allFieldsAreRequired;

      $(document).ready(function () {

        isNewModule = <%= IsNewModule.ToJSTrueFalse() %>;
        isProgramView = <%= IsProgramView.ToJSTrueFalse() %>;

        contentContainer = $(".<%= WebHelper.Modules.CSS.ModuleContentTable %>");
        $imgCoverImage = $("#imgCoverImage");
        Cropper = window.Cropper;
        $cropperModal = $("#cropperModal");
        $cropperModalImage = $("#cropperModal .cropperImage");
        moduleGuid = isNewModule ? null : '<%= ModuleInfo?.ModuleGuid %>';
        summaryInput = $('input[name="<%= FormFields.ModuleSummary %>"]');
        $publicSelectionDiv = $(".publicSelection");
        $requiredFieldDiv = $(".<%= WebHelper.CSSClasses.IconTooltip_RequiredField %>");

        btnSaveModule.click(SaveModule);
        $('.btnDeleteModule').click(DeleteModuleModal);
        $(".btnAddMicrolearningItem").click(ShowContentLibraryDialog);
        $(".action-button-list .btnRemoveOption").click(RemoveContentModal);

        $('#btnSelectCoverImage').on('change', function () { CropCoverImage(this); });

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

        GetSummaryCharCount();
        $('input[name="<%= FormFields.ModuleSummary %>"]').keyup(function () {
          GetSummaryCharCount();
        });

        function UpdateRequiredLabelDisplay() {
          allFieldsAreRequired = $publicSelectionDiv.find('input[type="checkbox"]').is(':checked');
          $requiredFieldDiv.toggleClass('displaynone', !allFieldsAreRequired);
        }
        UpdateRequiredLabelDisplay();
        $publicSelectionDiv.on('change', 'input[type="checkbox"]', UpdateRequiredLabelDisplay);

        // Add Content items to Module
        $(document).on('click', '.<%= WebHelper.Content.CSS.ContentCardTopContainer %> .<%= WebHelper.Content.CSS.AddContentButton %>', AddContentItem);

        $(".ModuleContentTable").sortable({
          items: "tr.content-item",
          handle: "td:first", // Use the first cell as the drag handle
          axis: "y",
          cursor: "move",
          opacity: 0.7,
          placeholder: "sort-highlight",
          helper: function (e, ui) {
            // Fix the cell widths during drag
            ui.children().each(function () {
              $(this).width($(this).width());
            });
            return ui;
          },
          update: function (event, ui) {
            // Get the new positions after the user has finished dragging
            UpdateRowPositions();
          }
        });

      });

      function UpdateRowPositions() {
        var hasChanges = false;
        var contentList = [];

        $(".ModuleContentTable tr.content-item").each(function (index) {
          // Get the current display order from data attribute
          var originalOrder = $(this).data("<%= WebHelper.Modules.DataAttrs.DisplayOrder %>");
          var newOrder = index + 1;
          // Check if the position has changed
          if (originalOrder !== newOrder) {
            hasChanges = true;
          }

          var contentId = $(this).data("<%= WebHelper.Content.DataAttrs.ContentId %>");
          contentList.push(contentId);
        });

        if (hasChanges) {
          var contentTable = $(".table-responsive");

          AjaxSubmit({
            url: "<%= PathHelper.CurrentUrl %>",
            action: "<%= AjaxAction.UpdateContentDisplayOrder %>",
            data: {
              "<%= FormFields.ContentList %>": contentList
            },
            onSuccess: function (jqXHR, data) {
              UpdateDisplayOrderAttributes();
            },
            onFail: function (jqXHR, data) {
            },
            onError: function (jqXHR, textStatus, errorThrown) {
              common_InfoDialog("Reorder failed, please try again later.");
            },
            onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
          });
        }
      }

      function UpdateDisplayOrderAttributes() {
        $(".ModuleContentTable tr.content-item").each(function (index) {
          $(this).attr("data-<%= WebHelper.Modules.DataAttrs.DisplayOrder %>", index + 1);
        });
      }

      function RemoveContentModal(ev) {
        ev.preventDefault();
        var contentId = $(ev.target).data('<%= WebHelper.Content.DataAttrs.ContentId %>');
        var contentRowHtml = $(ev.target).closest('.content-item');

        BootstrapDialog.show({
          type: BootstrapDialog.TYPE_WARNING,
          title: 'Confirmation',
          message: "Are you sure you want to remove this microlearning from the module?",
          buttons: [
            {
              label: 'No', cssClass: 'btn-secondary',
              action: function (dialog) { dialog.close(); }
            },
            {
              label: 'Yes', cssClass: 'btn-primary',
              action: function (dialog) {
                dialog.close(); RemoveContentItemFromModule(contentId, contentRowHtml);
              }
            }
          ]
        });
      }

      function RemoveContentItemFromModule(contentId, contentRowHtml) {

        var btnDeleteContent = $(".<%= WebHelper.Content.CSS.RemoveOptionButtonInTable %>");

        AjaxSubmit({
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.RemoveContentItemFromModule %>",
          data: {
            "<%= FormFields.ContentId %>": contentId
          },
          onSuccess: function (jqXHR, data) {
            contentRowHtml.remove();
          },
          onFail: function (jqXHR, data) {
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Removal failed, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function AddContentItem(ev) {
        ev.preventDefault();
        var contentId = $(ev.target).data('contentid');
        var contentCardHtml = $(ev.target).closest('.content-card');

        AjaxSubmit({
          form: $("#moduleForm"),
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.AddContentItemToModule %>",
          data: {
            "<%= FormFields.ContentId %>": contentId
          },
          onSuccess: function (jqXHR, data) {
            contentContainer.append(data["<%= AjaxReturnData.ContentItemHtml %>"]); // Add row Html to Content table
            contentCardHtml.remove(); // Remove the added content from Library
            $(".modal-content .close").trigger('click'); // Close Modal
          },
          onFail: function (jqXHR, data) {
            common_ErrorToast("Failed to add content to module.");
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_ErrorToast("Failed to add content to module, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function GetSummaryCharCount() {
        var maxLength = "<%= (int)WebHelper.InputMaxLength.ContentSummary %>";

        var textLength = summaryInput.val().length;
        if (textLength > maxLength) {
          summaryInput.val(summaryInput.val().substring(0, maxLength));
        }
        $('#summaryCharCount').text('Characters left: ' + (maxLength - textLength));
      }

      function CropCoverImage(fileInput) {

        $cropperModal.toggleClass("IsLogo", true);
        $cropperModal.data("img-target", $imgCoverImage);
        $cropperModal.data("img-mimetype", "image/png");

        try {
          if (fileInput.files && fileInput.files[0]) {
            if (fileInput.files[0].type.match(/^image\//)) {
              var reader = new FileReader();

              reader.onload = function (evt) {
                var image = new Image();
                image.src = evt.target.result;

                // If the image is too big, it needs to be resized, otherwise it will not be displayed after being cropped or stored.
                // When the image is loaded, check the dimensions
                image.onload = function () {
                  var maxImageWidth = 1000; // Cover image doesn't need to be full size of the original
                  var imageWidth = image.width;
                  var imageHeight = image.height;

                  // If width exceeds maxImageWidth, resize the image while maintaining aspect ratio
                  if (imageWidth > maxImageWidth) {
                    var scaleFactor = maxImageWidth / imageWidth;
                    imageWidth = maxImageWidth;
                    imageHeight = imageHeight * scaleFactor;
                  }

                  // Create a canvas to resize the image
                  var canvas = document.createElement("canvas");
                  canvas.width = imageWidth;
                  canvas.height = imageHeight;
                  var ctx = canvas.getContext("2d");

                  // Draw the resized image onto the canvas
                  ctx.drawImage(image, 0, 0, imageWidth, imageHeight);

                  // Convert the canvas to a Base64 string
                  var resizedImageDataURL = canvas.toDataURL("image/png");

                  // Assign the resized image data to the cropper
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
                      aspectRatio: 280 / 150,

                      ready: function () {
                        $cropperModal.modal();
                      }
                    });

                    $cropperModal.data("cropper", cropper);
                  });

                  // Set the resized image as the source for the cropper
                  $cropperModalImage.attr("src", resizedImageDataURL);
                };
              };

              reader.readAsDataURL(fileInput.files[0]);

            } else {
              alert("Invalid file type! Please select an image file.");
            }
          } else {
            alert('No file(s) selected.');
          }

        } catch (e) {
          console.error('An error occurred:', e);
        }
      }

      function DeleteModuleModal() {
        BootstrapDialog.show({
          type: BootstrapDialog.TYPE_WARNING,
          title: 'Confirmation',
          message: "Are you sure you want to delete this microlearning?",
          buttons: [
            {
              label: 'No', cssClass: 'btn-secondary',
              action: function (dialog) { dialog.close(); }
            },
            {
              label: 'Yes', cssClass: 'btn-primary',
              action: function (dialog) {
                dialog.close(); DeleteModule();
              }
            }
          ]
        });
      }

      function DeleteModule()  {

        var btnDeleteModule = $('.btnDeleteModule');

        AjaxSubmit({
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.DeleteModule %>",
          onSuccess: function (jqXHR, data) {},
          onFail: function (jqXHR, data) {
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Deleted failed, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function SaveModule(evt) {

        var moduleForm = $("#moduleForm");

        if (!ValidateModuleForSaving()) return; // Some attachment is missing

        AjaxSubmit({
          form: moduleForm,
          url: "<%= PathHelper.CurrentUrl %>",
          action: "<%= AjaxAction.UpdateModule %>",
          onSuccess: function (jqXHR, data) {

            moduleGuid = data["<%= AjaxReturnData.ModuleGuid %>"];
            $(".btnPreview").removeClass("display-none");

            isUpdatingFiles = true;
            SaveCoverImage(() => {
              DoPageRedirection();
            });
          },
          onFail: function (jqXHR, data) {
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Update failed, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) {
            // Always runs after the Ajax call finishes
          }
        });
      }

      function ValidateModuleForSaving() {
        // Check that cover Image and attachment aren't missing
        var coverImagePath = '<%= PathHelper.Images.ContentCoverImage(ModuleInfo.ModuleGuid, PathHelper.Images.ContentCoverImageSize.Card) %>';
        var missingImagePath = '<%= PathHelper.Images.ContentImage_Missing() %>';

        if (isNewModule  || (!isNewModule && coverImagePath == missingImagePath)) {

          var isAttachmentMissing = false;

          if ($imgCoverImage.data("blob") == null && allFieldsAreRequired) {
            isAttachmentMissing = true;
            $('.error-imgCover').css('display', 'block'); // Show the error message
          } else {
            $('.error-imgCover').css('display', 'none'); // Hide the error message
            isUpdatingFiles = true;
          }

          if (isAttachmentMissing) return false;
        }

        return true; // Passed validation
      }

      function SaveCoverImage(fnCallback) {

        if ($imgCoverImage.data("blob") == null || moduleGuid == null) {
          isUpdatingFiles = false;
          if (typeof fnCallback == "function") fnCallback();
          return;
        }

        $.busyLoadFull("show");

        var formData = new FormData();
        formData.append("<%= FormFields.ModuleGuid %>", moduleGuid);
        formData.append("<%= PathHelper.FormKeys.AjaxAction %>", "<%= AjaxAction.UploadCoverImage %>");
        formData.append("image", $imgCoverImage.data("blob"));

        jQuery.ajax({
          method: "post",
          url: "<%= PathHelper.CurrentUrl %>",
          data: formData,
          processData: false,
          contentType: false

        }).done(function (response) {

          if (typeof fnCallback == "function") fnCallback();

        }).always(() => {

          $.busyLoadFull("hide");
        });
      }

      function DoPageRedirection() {
        if (isProgramView) {
          // TODO: When linked to Program, use that url
        } else {
          window.location.href = '<%= PathHelper.Pages.ModuleEdit(null) %>' + moduleGuid;
        }
      }

      function ShowContentLibraryDialog() {

        <% if (!CanAddContentItems) {  %>
          return;
        <% } %>

        var dlg = common_InfoDialog("#dlg<%= WebHelper.Content.CSS.ContentContainer %>", {
          name: "addContent",
          title: "Microlearning Library",
          width: 950,
          buttons: [
            { text: "Cancel", class: "btn-secondary mr20 left", isDefault: false, isPrimary: false, close: true }
          ],
          shown: function () {
          },
          hide: function () {
          }
        });
      }

    })(jQuery);

  </script>

</asp:Content>


