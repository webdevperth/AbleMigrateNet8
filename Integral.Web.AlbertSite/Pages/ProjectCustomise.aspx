<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ProjectCustomise.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.ProjectCustomise" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.css">
  <script src="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.js"></script>

  <form class="form-horizontal" id="projectForm">

    <button type="button" id="btnUpdate" class="btn btn-primary mb15 floatright">Save Changes</button>

    <ul class="nav nav-tabs nav-tabs-underlined width-fit-content" id="formTabs">
      <li role="presentation" class="active" data-tabname="<%= TabName.General %>">
        <a class="nav-link" id="tab-<%= TabName.General %>" data-toggle="tab" href="#panel-<%= TabName.General %>" role="tab" aria-controls="panel-<%= TabName.General %>" aria-selected="true">General</a>
      </li>
      <li role="presentation" data-tabname="<%= TabName.WelcomeEmail %>">
        <a class="nav-link" id="tab-<%= TabName.WelcomeEmail %>" data-toggle="tab" href="#panel-<%= TabName.WelcomeEmail %>" role="tab" aria-controls="panel-<%= TabName.WelcomeEmail %>">Welcome Email</a>
      </li>
      <li role="presentation" data-tabname="<%= TabName.MeetCoachEmail %>">
        <a class="nav-link" id="tab-<%= TabName.MeetCoachEmail %>" data-toggle="tab" href="#panel-<%= TabName.MeetCoachEmail %>" role="tab" aria-controls="panel-<%= TabName.MeetCoachEmail %>">Meet Coach Email</a>
      </li>
      <li role="presentation" data-tabname="<%= TabName.BookNextSession %>">
        <a class="nav-link" id="tab-<%= TabName.BookNextSession %>" data-toggle="tab" href="#panel-<%= TabName.BookNextSession %>" role="tab" aria-controls="panel-<%= TabName.BookNextSession %>">Book Session Email</a>
      </li>
      <li role="presentation" data-tabname="<%= TabName.EmailCadence %>">
        <a class="nav-link" id="tab-<%= TabName.EmailCadence %>" data-toggle="tab" href="#panel-<%= TabName.EmailCadence %>" role="tab" aria-controls="panel-<%= TabName.EmailCadence %>">Emails & Cadence</a>
      </li>
      <li role="presentation" data-tabname="<%= TabName.SendTests %>">
        <a class="nav-link" id="tab-<%= TabName.SendTests %>" data-toggle="tab" href="#panel-<%= TabName.SendTests %>" role="tab" aria-controls="panel-<%= TabName.SendTests %>">Send Test Emails</a>
      </li>
      <li role="presentation" data-tabname="<%= TabName.Surveys %>">
        <a class="nav-link" id="tab-<%= TabName.Surveys %>" data-toggle="tab" href="#panel-<%= TabName.Surveys %>" role="tab" aria-controls="panel-<%= TabName.Surveys %>">Surveys</a>
      </li>
    </ul>

    <div class="tab-content">
      <div class="tab-pane tab-quote tab-<%= TabName.General %> fade in active" id="panel-<%= TabName.General %>" role="tabpanel" aria-labelledby="tab-<%= TabName.General %>"></div>
      <div class="tab-pane tab-quote tab-<%= TabName.WelcomeEmail %> fade in" id="panel-<%= TabName.WelcomeEmail %>" role="tabpanel" aria-labelledby="tab-<%= TabName.WelcomeEmail %>"></div>
      <div class="tab-pane tab-quote tab-<%= TabName.MeetCoachEmail %> fade in" id="panel-<%= TabName.MeetCoachEmail %>" role="tabpanel" aria-labelledby="tab-<%= TabName.MeetCoachEmail %>"></div>
      <div class="tab-pane tab-quote tab-<%= TabName.BookNextSession %> fade in" id="panel-<%= TabName.BookNextSession %>" role="tabpanel" aria-labelledby="tab-<%= TabName.BookNextSession %>"></div>
      <div class="tab-pane tab-quote tab-<%= TabName.EmailCadence %> fade in" id="panel-<%= TabName.EmailCadence %>" role="tabpanel" aria-labelledby="tab-<%= TabName.EmailCadence %>"></div>
      <div class="tab-pane tab-quote tab-<%= TabName.SendTests %> fade in" id="panel-<%= TabName.SendTests %>" role="tabpanel" aria-labelledby="tab-<%= TabName.SendTests %>"></div>
      <div class="tab-pane tab-quote tab-<%= TabName.Surveys %> fade in" id="panel-<%= TabName.Surveys %>" role="tabpanel" aria-labelledby="tab-<%= TabName.Surveys %>"></div>
    </div>

    <div class="tab-panel" data-appendTo="panel-<%= TabName.General %>">

      <h4>General</h4>
      <br />

      <%= WebHelper.GetTextInput("Friendly Title:", FormFields.ProjectFriendlyTitle, ProjectInfo.FriendlyProjectTitle, 7) %>

      <div class="file-selection">

        <%= WebHelper.GetSelectRow("Project Logo:", FormFields.BrandingOrgId, 5, GetBrandingOrgOptions()) %>

        <% new WebHelper.Form.FormRow() {
            LabelPosition = WebHelper.Form.LabelPosition.LeftLegacy,
            ContentHtml = CompanyLogoControl.ToHtml(),
          }.WriteHtml(); %>

      </div>

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
                <img class="cropperImage floatleft" src="<%= GetLogoUrl() %>" alt="Project Logo">
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

      <hr />

      <%= WebHelper.CustomCheckBoxRow("Allow Participants to self select Coach:", FormFields.CanSelfSelectCoach, "1", ProjectInfo.CanSelfSelectCoach, false, "") %>

    </div>

    <div class="tab-panel" data-appendTo="panel-<%= TabName.WelcomeEmail %>">

      <h4>Welcome Email Custom Text</h4>

      <div class="row">
        <div class="col-md-8">
          <textarea class="" id="txt<%= FormFields.WelcomeEmailHTML %>" name="<%= FormFields.WelcomeEmailHTML %>"><%= ProjectInfo.WelcomeEmailCustomHTML.HTMLEncode() %></textarea>
        </div>
      </div>

      <div class="mt20">
        <%= WebHelper.CustomCheckBox(FormFields.WelcomeEmail_ProgramSummaryDisabled, "1", ProjectInfo.WelcomeEmail_ProgramSummaryDisabled, "Do not include program summary.") %>
      </div>
    </div>

    <div class="tab-panel" data-appendTo="panel-<%= TabName.MeetCoachEmail %>">
      <h4>Meet-Coach Email Custom Text</h4>
      <div class="row">
        <div class="col-md-8">
          <textarea class="" id="txt<%= FormFields.MeetCoachEmailHTML %>" name="<%= FormFields.MeetCoachEmailHTML %>"><%= ProjectInfo.MeetCoachEmailCustomHTML.HTMLEncode() %></textarea>
        </div>
      </div>
    </div>

    <div class="tab-panel" data-appendTo="panel-<%= TabName.BookNextSession %>">

      <div class="mb20">
        <%= WebHelper.CustomCheckBox(FormFields.AllowLoggedOutCoachBooking, "1", ProjectInfo.AllowLoggedOutCoachBooking, "Do not require login for coach booking.") %>
      </div>

      <h4>Book-Next-Session Email Custom Text</h4>
      <div class="row">
        <div class="col-md-8">
          <textarea class="" id="txt<%= FormFields.BookNextSessionEmailHTML %>" name="<%= FormFields.BookNextSessionEmailHTML %>"><%= ProjectInfo.BookSessionEmailCustomHTML.HTMLEncode() %></textarea>
        </div>
      </div>

    </div>

    <div class="tab-panel" data-appendTo="panel-<%= TabName.EmailCadence %>">

      <h4 class="mb15">Email Cadence Options</h4>

      <div class="flex-column ml15 gap20">

        <div class="flex gap50">

          <div>
            <h5>Survey Invites and Reminders:</h5>
            <div class="flex-column gap10">
              <% DbHelper.SurveyReminderCadence.ForEach(rc => { %>
                <% CadenceOption(FormFields.SurveyReminderCadenceId, ProjectInfo.SurveyReminderCadence.Id, rc.Id, rc.DisplayText); %>
                <% return true; %>
              <% }); %>
            </div>
          </div>
          <div>
            <h5>Survey Invites and Reminders (Raters):</h5>
            <div class="flex-column gap10">
              <% DbHelper.SurveyReminderCadence.ForEach(rc => { %>
                <% CadenceOption(FormFields.SurveyReminderCadenceId_Raters, ProjectInfo.SurveyReminderCadence_Raters.Id, rc.Id, rc.DisplayText); %>
                <% return true; %>
              <% }); %>
            </div>
          </div>

        </div>

        <div class="flex0">
          <h5>Session Booking Reminder Cadence:</h5>
          <p># Days after last session. Leave blank for default.</p>
          <p class="mt10"><input type="text" name="<%= FormFields.BookingReminderCadenceDays %>" class="form-control"
            placeholder="<%= ConfigHelper.BookingReminderCadenceDays.ToStringList().Replace(",", ", ") %>" value="<%= ProjectInfo.BookingReminderCadenceDays.EmptyIfNull().Replace(",", ", ") %>" /></p>
        </div>

      </div>

      <% if (CanDisablePaxRegistrationReminders) { %>
        <h4 class="mt25">Participant Registration Reminders:</h4>
        <div class="ml15">
          <%= WebHelper.CustomCheckBox(FormFields.DisablePaxRegReminders, "1", ProjectInfo.DisablePaxRegReminders, "Disable Participant Registration Reminders") %>
        </div>
      <% } %>

      <h4 class="mt25">Other Notifications:</h4>
      <div class="ml15">
        <%= WebHelper.CustomCheckBox(FormFields.NotifySelfWhen180RaterCompleted, "1", ProjectInfo.NotifySelfWhen180RaterCompleted, "Notify Self When 180 Rater Completed") %>
      </div>

      <h4 class="mt25 mb15">Override Sender Name and Address:</h4>
      <div class="ml15">
        <%= WebHelper.GetTextInput("Sender Name:", FormFields.OverrideSenderEmailName, "", ProjectInfo.OverrideSenderEmailName, false, WebHelper.InputMaxLength.EmailName) %>
        <%= WebHelper.GetTextInput("Sender Email Address:", FormFields.OverrideSenderEmailAddress, "", ProjectInfo.OverrideSenderEmailAddress, false, WebHelper.InputMaxLength.EmailAddress) %>
      </div>

    </div>

    <div class="tab-panel" data-appendTo="panel-<%= TabName.SendTests %>">
      <h4>Send Test Emails</h4>
      <div class="row">
        <div class="col-md-8">

          Send emails to check the content before saving your changes.<br/>
          You will receive one of each type of email, addressed as if you are the participant selected below:<br/>
          <br/>
          <%= WebHelper.GetSelect(new WebHelper.SelectInfo() { InputName = FormFields.TestCoacheeId, TopOptionsHtml = CoacheesInProjectOptionHTML, Class = "w300" }) %>

          <button type="button" id="btnSendTests" class="btn btn-primary mt20">Send Test Emails</button>

        </div>
      </div>
    </div>

    <div class="tab-panel" data-appendTo="panel-<%= TabName.Surveys %>">
      <h4>Surveys Timings / Triggers</h4>
      <div id="surveyselection" class="mt20">

        <%= WebHelper.GetSelectRow("Program Commencement / Intake:", FormFields.IntakeSurveyTemplateId, 5, GetSurveyTemplateOptions(ProjectInfo.IntakeSurveyTemplateId),
          ($"<span class=\"flex flex-align-center gap10\"><span class=\"ml10\">Disable Sending: </span>{WebHelper.CustomCheckBox(FormFields.IntakeSurveyDisable, "1", ProjectInfo.IntakeSurveyDisabled,"")}</span>") +
          ($"<span class=\"flex flex-align-center gap10\"><span class=\"ml10\">Send to All Participants: </span>{WebHelper.CustomCheckBox(FormFields.SendIntakeSurveyToAllParticipants, "1", ProjectInfo.SendIntakeSurveyToAllParticipants,"")}</span>")) %>

        <%= WebHelper.GetSelectRow("Pulse:", FormFields.PulseSurveyTemplateId, 5, GetSurveyTemplateOptions(ProjectInfo.PulseSurveyTemplateId),
          ("<span class=\"ml10\">Disable Sending: </span>" + WebHelper.CustomCheckBox(FormFields.PulseSurveyDisable, "1", ProjectInfo.PulseSurveyDisabled,""))) %>

        <%= WebHelper.GetSelectRow("Coaching Session:", FormFields.CoachingSessionEvalSurveyTemplateId, 5, GetSurveyTemplateOptions(ProjectInfo.CoachingSessionEvalSurveyTemplateId),
          ("<span class=\"ml10\">Disable Sending: </span>" + WebHelper.CustomCheckBox(FormFields.CoachingSessionEvalSurveyDisabled, "1", ProjectInfo.CoachingSessionEvalSurveyDisabled,""))) %>

        <%= WebHelper.GetSelectRow("Coaching Program:", FormFields.CoachingProgramEvalSurveyTemplateId, 5, GetSurveyTemplateOptions(ProjectInfo.CoachingProgramEvalSurveyTemplateId),
          ("<span class=\"ml10\">Disable Sending: </span>" + WebHelper.CustomCheckBox(FormFields.CoachingProgramEvalSurveyDisabled, "1", ProjectInfo.CoachingProgramEvalSurveyDisabled,""))) %>

        <%= WebHelper.GetSelectRow("Workshop Session:", FormFields.WorkshopSessionEvalSurveyTemplateId, 5, GetSurveyTemplateOptions(ProjectInfo.WorkshopSessionEvalSurveyTemplateId),
          ("<span class=\"ml10\">Disable Sending: </span>" + WebHelper.CustomCheckBox(FormFields.WorkshopSessionEvalSurveyDisabled, "1", ProjectInfo.WorkshopSessionEvalSurveyDisabled,""))) %>

        <%= WebHelper.GetSelectRow("Program:", FormFields.GenericProgramEvalSurveyTemplateId, 5, GetSurveyTemplateOptions(ProjectInfo.GenericProgramEvalSurveyTemplateId),
          ("<span class=\"ml10\">Disable Sending: </span>" + WebHelper.CustomCheckBox(FormFields.GenericProgramEvalSurveyDisabled, "1", ProjectInfo.GenericProgramEvalSurveyDisabled,""))) %>

        <%= WebHelper.GetSelectRow("Final Workshop and Program:", FormFields.WorkshopAndProgramEvalSurveyTemplateId, 5, GetSurveyTemplateOptions(ProjectInfo.WorkshopAndProgramEvalSurveyTemplateId),
          ("<span class=\"ml10\">Disable Sending: </span>" + WebHelper.CustomCheckBox(FormFields.WorkshopAndProgramEvalSurveyDisabled, "1", ProjectInfo.WorkshopAndProgramEvalSurveyDisabled,""))) %>

        <%= WebHelper.GetSelectRow("Development Plan:", FormFields.DevelopmentPlanTemplateId, 5, GetSurveyTemplateOptions(ProjectInfo.DevelopmentPlanTemplateId, ConfigHelper.SurveyTypeCodes.DevPlan)) %>

      </div>
    </div>

  </form>

  <% void CadenceOption(string fieldName, int projectCadenceId, int optionId, string optionText) { %>
    <% string tagId = fieldName + "_" + optionId; %>
    <div class="flex gap5">
      <input type="radio" name="<%= fieldName %>" value="<%= optionId %>" id="<%= tagId %>" <%= optionId == projectCadenceId ? "checked" : "" %> />
      <label for="<%= tagId %>"><%= optionText.HTMLEncode() %></label>
    </div>
  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var $projectForm = $("#projectForm");
      var $formTabs = $("#formTabs");
      var $btnUpdate = $("#btnUpdate");
      var $btnSendTests = $("#btnSendTests");
      var $selBrandingOrgId = $('select[name="<%= FormFields.BrandingOrgId %>"]');
      var $logoUploadInput = $("#<%= CompanyLogoControl.InputContainerID %>");
      var $companyLogoImg = $("#<%= CompanyLogoControl.ImgID %>");
      var sendToAllParticipants = "<%= FormFields.SendIntakeSurveyToAllParticipants %>";
      var Cropper, cropper, $cropperModal, $cropperSourceImage, $cropperModalImage;

      $(document).ready(function () {

        //Branding dropdown
        $selBrandingOrgId.change(BrandingOrgChange);
        BrandingOrgChange();

        $btnSendTests.click(function () { Submit(true); });
        $btnUpdate.click(function () { Submit(false); });
        DisableSurveySelectOnCheckboxVal();

        Cropper = window.Cropper;
        $cropperSourceImage = $companyLogoImg;
        $cropperModalImage = $("#cropperModal .cropperImage");
        $cropperModal = $("#cropperModal");
        $cropperModal.addClass("IsLogo");

        observer = new MutationObserver((changes) => {
          changes.forEach(change => {
            if (change.attributeName.includes('src')) {
              if ($companyLogoImg[0].src.indexOf("data:image/") === 0) {
                $selBrandingOrgId.val("").trigger("change");
              }
            }
          });
        });
        observer.observe($companyLogoImg[0], { attributes: true });

        $companyLogoImg.on("change", () => {
          console.log(`changed`);
        });

        $('#<%= CompanyLogoControl.InputID %>').on('change', function () {

          if (this.files && this.files[0]) {
            if (this.files[0].type.match(/^image\//)) {
              var reader = new FileReader();
              reader.onload = function (evt) {
                $cropperModalImage.on("load", function () {

                  if (typeof cropper != "undefined" && cropper != null) {
                    if (cropper.destroy) cropper.destroy();
                    cropper = null;
                  }

                  cropper = new Cropper($cropperModalImage[0], {
                    viewMode: 1,
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
              reader.readAsDataURL(this.files[0]);
            }
            else {
              alert("Invalid file type! Please select an image file.");
            }
          }
          else {
            alert('No file(s) selected.');
          }
        });

        $("#cropperModal .btnRotateLeft").click(function (e) { cropper.rotate(-90); });
        $("#cropperModal .btnRotateRight").click(function (e) { cropper.rotate(90); });
        $("#cropperModal .btnZoomOut").click(function (e) { cropper.zoom(-0.1); });
        $("#cropperModal .btnZoomIn").click(function (e) { cropper.zoom(0.1); });

        $("#btnCropDone").click(function (e) {
          if (typeof cropper == "undefined" || cropper == null) return;
          var croppedCanvas = cropper.getCroppedCanvas();
          $cropperSourceImage.attr("src", croppedCanvas.toDataURL());
          croppedCanvas.toBlob(function (blob) {
            $cropperSourceImage.data("blob", blob);
            $cropperModal.modal("hide")
          }, "image/png", 0.9);
        });

        $cropperModal.on('hidden.bs.modal', function () {
          if (typeof cropper != "undefined" && cropper != null) {
            if (cropper.destroy) cropper.destroy();
            cropper = null;
          }
        });

        TinyMCEInit("#txt<%= FormFields.WelcomeEmailHTML %>", {
          mergeTags: [
            { name: "Friendly Project Title", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.FriendlyProjectTitle) %>" },
            { name: "Coachee First Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.CoacheeFirstName) %>" },
            { name: "Coach First Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.CoachFirstName) %>" },
            { name: "Coach Full Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.CoachFullName) %>" },
            { name: "Program Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.ProgramName) %>" },
            { name: "Sessions Allocated", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.SessionsAllocated) %>" },
            { name: "Total Workshops", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.TotalWorkshops) %>" },
          ]
        });

        TinyMCEInit("#txt<%= FormFields.MeetCoachEmailHTML %>", {
          mergeTags: [
            { name: "Friendly Project Title", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.FriendlyProjectTitle) %>" },
            { name: "Coachee First Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.CoacheeFirstName) %>" },
            { name: "Coach First Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.CoachFirstName) %>" },
            { name: "Coach Full Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.CoachFullName) %>" },
            { name: "Program Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.ProgramName) %>" },
            { name: "Sessions Allocated", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.SessionsAllocated) %>" },
          ]
        });

        TinyMCEInit("#txt<%= FormFields.BookNextSessionEmailHTML %>", {
          mergeTags: [
            { name: "Friendly Project Title", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.FriendlyProjectTitle) %>" },
            { name: "Coachee First Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.CoacheeFirstName) %>" },
            { name: "Coach Full Name", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.CoachFullName) %>" },
            { name: "Sessions Allocated", value: "<%= AlbertEmails.GetMergeTag(AlbertEmails.MandrillMergeTags.SessionsAllocated) %>" },
          ]
        });

      }); // ready.

      function DisableSurveySelectOnCheckboxVal() {

        $('#surveyselection input[type="checkbox"]').on('change', function () {

          if ($(this).attr('name') === sendToAllParticipants) {
            return; // If checkbox is SendToAllParticipants, do nothing
          }

          // Get the corresponding select element
          var select = $(this).closest('.form-group').find('select');
          // Enable or disable the select based on the checkbox value
          if ($(this).prop('checked')) {
            select.prop('disabled', true);
          } else {
            select.prop('disabled', false);
          }
        });

        // Set checkbox and select states based on saved value (On page load mainly)
        $('#surveyselection input[type="checkbox"]').each(function () {
          if ($(this).attr('name') === sendToAllParticipants) {
            return; // If checkbox is SendToAllParticipants, do nothing
          }

          if ($(this).prop('checked')) {
            var select = $(this).closest('.form-group').find('select');
            select.prop('disabled', true);
          }
        });
      }

      function BrandingOrgChange() {

        let brandingOrgId = $selBrandingOrgId.val();

        if (isStringNullOrEmpty(brandingOrgId) && $companyLogoImg[0].src.indexOf("data:image/") === 0) {
          // Image previously updated, this is just a dropdown reset.
          return;
        }

        // If there's a company selected from the list hide image selection UI
        if (!isStringNullOrEmpty(brandingOrgId.length)) {
          $logoUploadInput.hide();
        } else {
          $logoUploadInput.show();
        }

        // Update displayed image from selection.
        let src = $selBrandingOrgId.find(':selected').attr('data-url');
        $companyLogoImg.attr("src", src);
      }

      function Submit(isTest) {

        var $btn = isTest ? $btnSendTests : $btnUpdate;

        AjaxSubmit({
          form: $projectForm,
          action: (isTest ? "<%= AjaxAction.SendTests %>" : "<%= AjaxAction.Update %>"),
          onSuccess: function (jqXHR, data) { },
          onFail: function (jqXHR, data) { },
          onError: function(jqXHR, textStatus, errorThrown) { },
          onAlways: function(data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });

      }

    })(jQuery);
  </script>

</asp:Content>


