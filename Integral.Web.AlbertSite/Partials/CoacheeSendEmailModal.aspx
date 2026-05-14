<%@ Page Language="C#" AutoEventWireup="true" CodeFile="CoacheeSendEmailModal.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.CoacheeSendEmailModal" %>

<%@ Import Namespace="Integral.Web" %>

<div class="container-fluid form-horizontal">

  <form id="formSendEmail" method="post" action="#" onsubmit="return false">

    <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="update" />
    <input type="hidden" name="<%= FormFields.FileNameList %>" value="" />

    <%= WebHelper.GetTextInput("Email Subject:", FormFields.EmailSubject, string.Empty, "", 1, 11, "", false) %>
    <%= WebHelper.GetRichTextArea("Email Body:", FormFields.EmailBodyHTML, 1, 11, "Hi " + FirstName_MergedField + ",<br/><br/><br/>", "", false) %>
    <%= WebHelper.GetTextDisplayRow("", 11,
      @"<input type=""file"" class=""filepond"" name=""filepond"" multiple data-allow-reorder=""false"" data-max-file-size=""50MB"" data-max-files=""3"">", "") %>

    <%= WebHelper.CustomCheckBoxRow("", 1, FormFields.IncludeBookingLink, "1", CanAttachBookingLinkInEmail, !CanAttachBookingLinkInEmail, "Include Coaching Booking Link.",
      WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, (CanAttachBookingLinkInEmail ? $"The participant has {(CoacheeInfo.UserActivity.SessionsAllocated - CoacheeInfo.UserActivity.SessionsBooked)} sessions pending to book." : "The participant doesn't have any session pending to book."), "")) %>

    <% if (CanSendContentEmail) { %>
      <%= WebHelper.CustomCheckBoxRow("", 1, FormFields.IncludeContentLink, "1", false, !CanSendContentEmail, "Include Microlearning Link.") %>
      <%= WebHelper.GetContentDropdownForEmail(ContentList, FormFields.ContentId, 1, 11) %>
    <% } %>

    <%= WebHelper.GetGenericRow(new WebHelper.RowOptions() { LabelCols = 1, ContentCols = 11 }, "<div id=\"FormButtons\"></div>") %>

    <div data-appendTo="FormButtons" class="w100p">
      <div class="align-right mb15">
        <p><button type="button" class="btn btn-primary mb5" id="btnSend">Send Email</button></p>
        <p>Sending From: <b><%= EmailFromAddress.HTMLEncode() %></b></p>
      </div>
    </div>

  </form>
</div>

<script>

  // Closure for Coaching Sessions Tab.
  (function ($) {

    var btnSendEmail, filePond, formSendEmail;
    var canSendContentEmail, chkIncludeBookingLink, chkIncludeContentLink, selMicrolearning;

    $(document).ready(function () {

      formSendEmail = $("#formSendEmail");
      btnSendEmail = formSendEmail.find("#btnSend");
      btnSendEmail.click(SendEmail);

      canSendContentEmail = <%= CanSendContentEmail.ToJSTrueFalse() %>;
      chkIncludeBookingLink = $('input[name="<%= FormFields.IncludeBookingLink %>"]');
      chkIncludeContentLink = $('input[name="<%= FormFields.IncludeContentLink %>"]');
      selMicrolearning = $('select[name="<%= FormFields.ContentId %>"]');

      SetupFilePond();

      chkIncludeBookingLink.change(function () {
        if ($(this).is(':checked')) {
          if (canSendContentEmail) {
            // Uncheck chkIncludeContentLink and set readonly on selMicrolearning
            chkIncludeContentLink.prop('checked', false).change();
            selMicrolearning.attr('readonly', 'readonly');
          }
        }
      });

      if (canSendContentEmail) {
        // Event handler for when chkIncludeContentLink changes
        chkIncludeContentLink.change(function () {
          if ($(this).is(':checked')) {
            // Uncheck chkIncludeBookingLink (if checked) and remove readonly from selMicrolearning
            chkIncludeBookingLink.prop('checked', false).change();
            selMicrolearning.removeAttr('readonly');
          } else {
            // Set readonly on selMicrolearning;
            selMicrolearning.attr('readonly', 'readonly');
          }
        });
      }
    });

    function SetupFilePond() {

      // https://pqina.nl/filepond/docs/patterns/api/filepond-instance/
      FilePond.registerPlugin(FilePondPluginFileValidateSize);
      filePond = FilePond.create(
        document.querySelector('input.filepond'), {
        maxFileSize: '<%= ConfigHelper.ServerMaxUploadFileSizeMB %>MB',
            maxFiles: <%= MaxAttachedFiles %>,
            labelIdle: 'Attach <b>up to <%= MaxAttachedFiles %></b> files, <b><%= ConfigHelper.ServerMaxUploadFileSizeMB %>MB max</b> per file.<br/>Drag & Drop here or <span class="filepond--label-action"> Browse </span>',
            itemInsertLocation: "after",
            labelTapToRetry: "Click to retry",
            labelTapToUndo: "",
            server: {
              url: '',
              process: {
                url: '<%= Request.RawUrl %>',
                method: 'POST',
                withCredentials: false,
                headers: {
                  "<%= AppHelper.HttpHeaders.AjaxAction %>": "<%= AjaxAction.Upload %>"
                },
                timeout: 7000,
                onload: null,
                onerror: null,
                ondata: null
              },
              revert: {
                method: 'POST',
                headers: {
                  "<%= AppHelper.HttpHeaders.AjaxAction %>": "<%= AjaxAction.Delete %>"
                }
              }
            },
            oninitfile: function(thisFile) {
              var files = filePond.getFiles();
              for (var i = 0; i < files.length; i++) {
                if (files[i].filename == thisFile.filename && files[i].id != thisFile.id) {
                  filePond.removeFile(thisFile.id);
                  common_InfoDialog("File is already in the list.");
                  break;
                }
              }
            },
            onupdatefiles: function(file) { // Update list of files to actually send with email.
              var files = filePond.getFiles();
              var fileNameList = [];
              for (var i = 0; i < files.length; i++) fileNameList.push(files[i].filename);
            formSendEmail[0]["<%= FormFields.FileNameList %>"].value = fileNameList.join("<%= FormFields.FileNameListDelimiter %>");
          },
          onprocessfilestart: function (file) {
            //console.log("onprocessfilestart");
          },
          onprocessfileprogress: function (file, progress) {
            var progBar = $("#filepond--item-" + file.id + " .filepond--panel-top");
            var width = Math.ceil(progress * 100);
            progBar.css("width", width + "%");
          },
          onaddfilestart: function (file) {
            //console.log("onaddfilestart");
          },
          onaddfilestart: function (file) {
            //console.log("onaddfilestart");
          },
          onaddfile: function (error, file) {
            var fileName = $("#filepond--item-" + file.id + " .filepond--file-info-main");
          },
          credits: null
        }
        );
    }

    function SendEmail() {
      if (filePond && FilePond && filePond.status === FilePond.Status.BUSY) {
        common_InfoDialog("Please wait until files are finished uploading before sending.");
        return;
      }

      AjaxSubmit({
        form: formSendEmail,
        url: "<%= PathHelper.CurrentUrl %>",
        action: "<%= AjaxAction.Send %>",
        onSuccess: function (jqXHR, data) { },
        onError: function (jqXHR, textStatus, errorThrown) {
          common_InfoDialog("Update failed, please try again later.");
        },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
      });
    }

  })(jQuery);

</script>
