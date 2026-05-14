<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ShareSurveyModal.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.ShareSurveyModal" %>

<%@ Import Namespace="Integral.Web" %>

<% if (SurveyToShare != null) { %>
  <form id="shareForm" class="form-horizontal">
    <%= WebHelper.GetTextDisplayRow("Survey:", SurveyToShare.SurveyName) %>
    <hr />
    <%= WebHelper.GetTextInputDual("Name:", FormFields.FirstName, "", "First Name", FormFields.LastName, "", "Last Name", false, WebHelper.InputMaxLength.NoLimit, 10) %>
    <%= WebHelper.GetTextInput("Email:", FormFields.Email, "", 10) %>
    <%= WebHelper.GetTextInput("Message:", FormFields.Message, "", 10) %>

    <button class="btn btn-primary float-right btnShareSurvey mt10">Share Survey</button>
  </form>
<% } %>

<script type="text/javascript">
  (function ($) {

    var btnShareSurvey, shareForm;

    $(document).ready(function () {

      btnShareSurvey = $(".btnShareSurvey");
      shareForm = $("#shareForm");
      btnShareSurvey.click(UpdateSurveySharing);

    });

    function UpdateSurveySharing() {

      AjaxSubmit({
        form: shareForm,
        url: "<%= PathHelper.CurrentUrl %>",
        action: "<%= AjaxAction.ShareSurvey %>",
        onSuccess: function (jqXHR, data) { },
        onFail: function (jqXHR, data) { },
        onError: function (jqXHR, textStatus, errorThrown) {
          common_InfoDialog("Update failed, please try again later.");
        },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
      });
    }

  })(jQuery);

</script>
