<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProgramSendEmail.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProgramSendEmail"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <div class="container-fluid form-horizontal">

    <% if (!ShowForm) { %>

      No Participants or Team Members to send to.<br/>
      (Participants with program status 'NA' are not included.)

    <% } else { %>

      <form id="formProgram" method="post" action="#" onsubmit="return false" class="form-horizontal">

        <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="update" />
        <input type="hidden" name="<%= FormFields.FileNameList %>" value="" />

        <%= WebHelper.GetSelectRow("Send to:", FormFields.SendToOption, 3, GetSendingOptions()) %>

        <%= WebHelper.GetTextDisplayRow("", FormContentCols,
          "<p>This will email <b><span class=\"recipient-count\"></span></b> <span class=\"recipient-text\"></span> in this program.</p>" +
          "<p class=\"recipient-partMessage\">(Participants with program status 'NA' are not included.)</p>") %>

        <div id="FilterParticipantsRow">
          <%= WebHelper.GetSelectRow("Filter Participants:", FormFields.ParticipantFilter, 3, GetParticipantFilterOptions()) %>
          <%= WebHelper.CustomCheckBoxRow("", FormFields.IncludeBookingLink, "1", false, "Include Coaching Booking Link.",
            WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, "If you check this field, the booking link will be attached only to the emails of those participants with pending sessions to book.", "")) %>

          <% if (CanSendContentEmail) { %>
            <%= WebHelper.CustomCheckBoxRow("", FormFields.IncludeContentLink, "1", false, "Include Microlearning Link.",
              WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, "If you check this field, you cannot include booking link in the same email.", "")) %>
            <%= WebHelper.GetContentDropdownForEmail(ContentList, FormFields.ContentId, 2, 8) %>
          <% } %>
        </div>

        <% if (CoacheeEndProgramCount > 0) { %>
          <div id="ExcludeEndProgramRow">
            <%= WebHelper.CustomCheckBoxRow("Exclude End-Program:", FormFields.ExcludeEndProgram, "1", false, "", "(Do NOT send to End-Program participants)") %>
          </div>
        <% } %>
        <%= WebHelper.GetTextInput("Email Subject:", FormFields.EmailSubject, string.Empty, FormContentCols) %>
        <%= WebHelper.GetRichTextArea("Email Body:", FormFields.EmailBodyHTML, 2, FormContentCols, "Hi " + FirstName_MergedField + ",<br/><br/><br/>", "") %>
        <%= WebHelper.GetTextDisplayRow("", FormContentCols,
          @"<input type=""file"" class=""filepond"" name=""filepond"" multiple data-allow-reorder=""false"" data-max-file-size=""50MB"" data-max-files=""3"">", "") %>

        <%= WebHelper.GetGenericRow(new WebHelper.RowOptions() { ContentCols = FormContentCols }, "<div id=\"FormButtons\"></div>") %>

        <div data-appendTo="FormButtons" class="flex flex-space-between">
          <div class="maxw200">
            <button type="button" class="btn btn-success btnTest" id="btnTest">Send Test Email</button>
            <p class="mt15">Send a test email to<br/><%= userInfo.EmailAddress %></p>
          </div>
          <div class="align-right">
            <button type="button" class="btn btn-primary btnSend" id="btnSend">Send Program Email</button>
            <p class="mt15">
              Send email to: <b><span class="recipient-count"></span></b> <span class="recipient-text"></span><br />
              From: <b><%= EmailFromAddress.HTMLEncode() %></b>
            </p>
          </div>
        </div>

      </form>

    <% } %>

  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var btnTest, btnSend, formProgram, filePond;
      var excludeEndProgramRow, chkExcludeEndProgram;
      var coacheeEndProgramCount = <%= CoacheeEndProgramCount %>;
      var spanRecipientCount, spanRecipientText, spanRecipientpartMessage;
      var selSendToOption = $('select[name="<%= FormFields.SendToOption %>"]');
      var sendingToText, sendingToCount, selParticipantFilter, filterParticipantsRow;
      var canSendContentEmail, chkIncludeBookingLink, chkIncludeContentLink, selMicrolearning;

      $(document).ready(function() {

        btnTest = $("#btnTest");
        btnSend = $("#btnSend");
        formProgram = $("#formProgram");
        spanRecipientCount = $(".recipient-count");
        spanRecipientText = $(".recipient-text");
        spanRecipientpartMessage = $(".recipient-partMessage");
        excludeEndProgramRow = $("#ExcludeEndProgramRow");
        chkExcludeEndProgram = $('input[name="<%= FormFields.ExcludeEndProgram %>"]');
        selParticipantFilter = $('select[name="<%= FormFields.ParticipantFilter %>"]');
        filterParticipantsRow = $("#FilterParticipantsRow");
        chkIncludeBookingLink = $('input[name="<%= FormFields.IncludeBookingLink %>"]');
        chkIncludeContentLink = $('input[name="<%= FormFields.IncludeContentLink %>"]');
        selMicrolearning = $('select[name="<%= FormFields.ContentId %>"]');
        canSendContentEmail = <%= CanSendContentEmail.ToJSTrueFalse() %>;

        btnTest.click(function () { SendClicked(true) }); // true = test
        btnSend.click(function () { SendClicked(false) });

        selSendToOption.change(UpdateForm);
        selParticipantFilter.change(UpdateForm);
        if (chkExcludeEndProgram.length == 1) chkExcludeEndProgram.change(UpdateForm);
        UpdateForm();

        SafeSetupFilePond();
        EmailAddingsEmail();

      });

      function EmailAddingsEmail() {

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
      }

      function UpdateForm() {

        var selectedSendToOption = selSendToOption.find(":selected");
        if (selectedSendToOption.length == 0) return;

        var excludeEndProgram = chkExcludeEndProgram.is(":checked");
        var displayTextSingular = selectedSendToOption.data("text-singular");
        var displayTextPlural = selectedSendToOption.data("text-plural");

        sendingToCount = selectedSendToOption.data("<%= DataAttrs.SendCount %>");

        if (selectedSendToOption.val() == "<%= SendToEnum.Participants.ToString() %>") {
          var selectedFilterOption = selParticipantFilter.find(":selected");
          sendingToCount = selectedFilterOption.data("<%= DataAttrs.SendCount %>");

          if (excludeEndProgram) sendingToCount -= coacheeEndProgramCount;
          spanRecipientpartMessage.show();
          excludeEndProgramRow.show();
          filterParticipantsRow.show();
        } else {
          spanRecipientpartMessage.hide();
          excludeEndProgramRow.hide();
          filterParticipantsRow.hide();
        }

        sendingToText = sendingToCount == 1 ? displayTextSingular : displayTextPlural;

        spanRecipientCount.text(sendingToCount);
        spanRecipientText.text(sendingToText);

        if (sendingToCount == 0) btnSend.prop("disabled", true);
        else btnSend.prop("disabled", false);
      }

      function SafeSetupFilePond() {
        if (typeof FilePond === 'undefined') {
          setTimeout(SafeSetupFilePond, 100);
          return;
        }
        SetupFilePond();
      }

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
                url: location.href,
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
            formProgram[0]["<%= FormFields.FileNameList %>"].value = fileNameList.join("<%= FormFields.FileNameListDelimiter %>");
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

      function SendClicked(isTest) {

        if (filePond && FilePond && filePond.status === FilePond.Status.BUSY) {
          common_InfoDialog("Please wait until files are finished uploading before sending.");
          return;
        }

        BootstrapDialog.show({
          type: BootstrapDialog.TYPE_WARNING,
          title: 'Send Program Email',
          message: isTest ? "Send TEST email to your email address?" : ("Send Email to " + sendingToCount + " " + sendingToText + " in this Program?"),
          buttons: [
            {
              label: 'No', cssClass: 'btn-secondary',
              action: function (dialog) { dialog.close(); }
            },
            {
              label: 'Yes', cssClass: 'btn-primary',
              action: function (dialog) { dialog.close(); DoSend(isTest); }
            }
          ]
        });
      }

      function DoSend(isTest) {

        AjaxSubmit({
          form: formProgram,
          action: (isTest ? "<%= AjaxAction.Test %>" : "<%= AjaxAction.Send %>")
        });
      }

    })(jQuery);
  </script>

</asp:Content>
