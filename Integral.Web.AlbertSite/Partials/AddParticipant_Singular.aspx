<%@ Page Language="C#" AutoEventWireup="true" CodeFile="AddParticipant_Singular.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.AddParticipant_Singular" %>

<%@ Import Namespace="Integral.Web" %>

<form id="profileForm" class="form-horizontal">

  <%= GetAssignationInfo() %>

  <%= WebHelper.GetTextInputDual("Name:",
        FormFields.FirstName, CoacheeInfo.FirstName, "First Name",
        FormFields.LastName, CoacheeInfo.LastName, "Last Name",
        false, WebHelper.InputMaxLength.EmailName) %>
  <%= WebHelper.GetTextInput("Email Address:", FormFields.EmailAddress, CoacheeInfo.EmailAddress, 8, "", false) %>
  <%= WebHelper.GetTextInput("Mobile Number:", FormFields.MobilePhone, CoacheeInfo.MobilePhone, 8, "", false, false) %>

  <div class="btnholder">
    <button type="button" class="btn btn-primary floatright btnSaveProfile" data-addpaxafter="false" data-waitmsg="Updating...">Create Participant</button>
    <button type="button" class="btn btn-primary floatright mr20 btnSaveProfile" data-addpaxafter="true" data-waitmsg="Updating...">Create and Add Another</button>
  </div>

</form>

<script type="text/javascript">
  (function ($) {

    var btnSaveProfile = $(".btnSaveProfile");

    $(document).ready(function () {

      btnSaveProfile.click(SaveProfileChanges);

    });

    function SaveProfileChanges(evt) {

      var thisBtn = $(evt.target);
      var profileForm = $("#profileForm");
      var addpaxafter = thisBtn.data('addpaxafter');

      AjaxSubmit({
        form: profileForm,
        url: "<%= PathHelper.CurrentUrl %>",
        action: "<%= AjaxAction.UpdateProfile %>",
        data: {
          "<%= FormFields.AddParticipantAfterCurrent %>": addpaxafter
        },
        onSuccess: function (jqXHR, data) { },
        onError: function (jqXHR, textStatus, errorThrown) {
          common_InfoDialog("Update failed, please try again later.");
        },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) {
          // Clean fields if selected to add another after saving current.
          if (addpaxafter) {
            $('#profileForm input[type="text"]').val("");
          }
        }
      });
    }

  })(jQuery);

</script>
