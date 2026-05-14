<%@ Page Language="C#" AutoEventWireup="true"
  MasterPageFile="~/MasterPages/Site.Master"
  CodeFile="Register.aspx.cs"
  Inherits="Integral.Web.PortalSite.Register" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ContentPlaceHolderID="BodyContent" runat="server">

  <div class="onboarding-wrapper onboarding-registration">

    <%= WebHelper.GetLoggedOutAbleLogo() %>

    <div class="content-box">

      <div class="content-box-image" style="background-image: url('/images/onboarding_image.jpg')"></div>

      <div class="content-box-form">

        <% if (ShowRegistrationForm) { %>

          <% if (IsInvited) { %>
            <h3>You have been invited to join Able</h3>
            <%= AlbertEmails.GetReferralEmailHeaderHtml(InvitedByUserInfo) %>
          <% } else { %>
            <h3 class="mb0">Create your account as:</h3>
          <% } %>

          <form id="formRegister" method="post" action="#" onsubmit="return false;" class="<%= IsInvited ? "mt20" : "mt5" %>">

            <% if (!IsInvited) { %>

              <div class="ajaxSubmit-field row" data-for="<%= FormFields.UserRole %>">
                <div class="col-md-12">
                  <div class="role-selection-container">
                    <div class="role-box" data-role="<%= ConfigHelper.UserRole.Leader %>">
                      <input type="radio" name="<%= FormFields.UserRole %>" value="<%= ConfigHelper.UserRole.Leader %>" id="role-<%= ConfigHelper.UserRole.Leader %>" autocomplete="off" />
                      <label for="role-<%= ConfigHelper.UserRole.Leader %>">
                        <div class="role-icon"><i class="fas fa-user-tie"></i></div>
                        <div class="role-info">
                          <h3>Leader</h3>
                          <p>I want to develop my leadership skills.</p>
                        </div>
                      </label>
                    </div>
                    <div class="role-box" data-role="<%= ConfigHelper.UserRole.Client %>">
                      <input type="radio" name="<%= FormFields.UserRole %>" value="<%= ConfigHelper.UserRole.Client %>" id="role-<%= ConfigHelper.UserRole.Client %>" autocomplete="off" />
                      <label for="role-<%= ConfigHelper.UserRole.Client %>">
                        <div class="role-icon"><i class="fas fa-users"></i></div>
                        <div class="role-info">
                          <h3>Client</h3>
                          <p>I want to empower my organisation with training.</p>
                        </div>
                      </label>
                    </div>
                    <div class="role-box" data-role="<%= ConfigHelper.UserRole.Coach %>">
                      <input type="radio" name="<%= FormFields.UserRole %>" value="<%= ConfigHelper.UserRole.Coach %>" id="role-<%= ConfigHelper.UserRole.Coach %>" autocomplete="off" />
                      <label for="role-<%= ConfigHelper.UserRole.Coach %>">
                        <div class="role-icon"><i class="fas fa-chalkboard-teacher"></i></div>
                        <div class="role-info">
                          <h3>Provider</h3>
                          <p>I deliver leadership development services.</p>
                        </div>
                      </label>
                    </div>
                  </div>
                </div>
              </div>
            <% } %>

            <div class="form-row">
              <label>Your name*</label>
              <div class="input-text-dual">
                <input class="form-control" type="text" name="<%= FormFields.FirstName %>" placeholder="First name" required="" value="<%= InviteeUserInfo?.FirstName.HTMLEncode() %>">
                <input class="form-control" type="text" name="<%= FormFields.LastName %>" placeholder="Last name" required="" value="<%= InviteeUserInfo?.LastName.HTMLEncode() %>">
              </div>
            </div>

            <div class="form-row">
              <label>Company name*</label>
              <input class="form-control" type="text" name="<%= FormFields.CompanyName %>" placeholder="Company name" <%= IsInvited ? "readonly" : "required" %> value="<%= InvitedByCompanyName.HTMLEncode() %>">
            </div>

            <div class="form-row">
              <label>Email*</label>
              <input class="form-control" type="email" name="<%= FormFields.EmailAddress %>" placeholder="john.smith@email.com" <%= IsInvited ? "readonly" : "required" %> value="<%= InviteeUserInfo?.EmailAddress %>">
            </div>

            <div class="form-row">
              <div class="label-holder">
                <label>Password*</label>
                <span class="label-description">Minimum 8 characters.<br />Use <strong>letters and numbers</strong>.</span>
              </div>
              <input class="form-control" type="password" name="<%= FormFields.Password1 %>" value="" required="">
            </div>

            <div class="form-row">
              <label>Repeat password*</label>
              <input class="form-control" type="password" name="<%= FormFields.Password2 %>" value="" required="">
            </div>

            <div class="form-row">
              <label class="hidden-xs"></label>
              <%= WebHelper.CustomCheckBox(FormFields.AcceptedTerms, "1", false, null,
                "I agree to the <a target=\"_blank\" href=\"https://www.helloable.co/privacy-policy\">privacy policy</a> " +
                  "and <a target=\"_blank\" href=\"https://www.helloable.co/terms-of-use\">terms of use</a>") %>
            </div>

            <div class="form-row">
              <label></label>
              <div class="right"><button id="btnRegister" type="button" class="btn btn-primary flex0">Register</button></div>
            </div>

            <span class="form-footer">
              Already have an account? <a href="<%= PathHelper.WebRoot %>">Login</a>.
            </span>

          </form>

        <% } %>

        <% if (IsAlreadyClaimed) { %>
          <h4>This invitation code has already been claimed.</h4>
        <% } %>

        <% if (IsInvalidCode) { %>
          <h4>Sorry, this invitation code is not valid.</h4>
        <% } %>

      </div>

      <div class="content-box-success hidden">
        <h3>Thank you for registering!</h3>
        <p>You can now <a id="SignInLink" href="/">Sign In</a> with your email address and password.</p>
      </div>

    </div>

  </div>

  <script type="text/javascript">

    var formRegister, btnRegister;
    var isInvited = <%= IsInvited.ToJSTrueFalse() %>;

    $(document).ready(function() {

      formRegister = $("#formRegister");
      btnRegister = $("#btnRegister");

      if (isInvited) {
        formRegister.find('input[name="<%= FormFields.Password1 %>"]').focus();
      } else {
        formRegister.find("input:text:first").focus();
      }

      btnRegister.click(btnRegisterClicked);

    });

    function btnRegisterClicked() {

      $(".error-row").remove();

      CheckProvided("<%= FormFields.UserRole %>", "Please select an account type.");
      if (!CheckProvided("<%= FormFields.FirstName %>") || !CheckProvided("<%= FormFields.LastName %>")) {
        FieldAlert("<%= FormFields.FirstName %>", "Please provide your full name.");
      }
      CheckProvided("<%= FormFields.CompanyName %>", "Please provide your company name.");
      CheckProvided("<%= FormFields.EmailAddress %>", "Please provide your email address.");
      CheckProvided("<%= FormFields.Password1 %>", "Please provide a password.");
      CheckProvided("<%= FormFields.Password2 %>", "Please confirm your password.");
      if ($('input[name="<%= FormFields.Password1 %>"]').val() !== $('input[name="<%= FormFields.Password2 %>"]').val()) {
        FieldAlert("<%= FormFields.Password2 %>", "Passwords must match.");
      }
      if (!$('input[name="<%= FormFields.AcceptedTerms %>"]').is(":checked")) {
        FieldAlert("<%= FormFields.AcceptedTerms %>", "Please confirm.");
      }


      if ($(".error-row").length > 0) return;

      AjaxSubmit({
        form: formRegister,
        action: "<%= AjaxAction.Register %>",
        autoHighlightField: false,
        onHighlightField: FieldAlert,
        onSuccess: function (jqXHR, data) { DoSuccess(); },
        onFail: function (jqXHR, data) { },
        onError: function (jqXHR, textStatus, errorThrown) {
          if (app_isDev) common_InfoDialog(jqXHR.responseText);
          else common_InfoDialog("Registration failed, please try again later.");
        },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
      });
    }

    function DoSuccess() {
      var emailAddr = formRegister.find('input[name="<%= FormFields.EmailAddress %>"]').val();
      $(".content-box-form").hide();
      $(".content-box-success").show();
      $("#SignInLink").attr("href", "/?<%= PathHelper.AbleUrlKeys.UserEmailAddress %>=" + encodeURIComponent(emailAddr)).focus();
    }

    function CheckProvided(fieldName, message) {

      var field = $('input[name="' + fieldName + '"]');
      if (field.length !== 1) return false;
      var fieldValue = $.trim(field.val());
      if (fieldValue == "") {
        if (message) FieldAlert(fieldName, message);
        return false;
      }
      return true;
    }

    function CheckValidEmail(fieldName, message) {

      var field = $('input[name="' + fieldName + '"]');
      if (field.length !== 1) return false;
      var fieldValue = $.trim(field.val());
      if (!myTools.IsValidEmailAddress(fieldValue)) {
        FieldAlert(fieldName, message);
        return false;
      }
      return true;
    }

    function FieldAlert(fieldName, message) {

      var field = $('input[name="' + fieldName + '"]');
      if (field.length !== 1) {
        common_InfoDialog(message, { glyphIcon: "question-sign" });
        return;
      }

      var formRow = field.closest(".form-row");
      if (formRow.length !== 1) return;

      var errorRow = formRow.clone();
      errorRow.addClass("error-row");
      errorRow.children().empty();
      errorRow.children().eq(1).remove();
      errorRow.append(message);
      formRow.after(errorRow);
    }

  </script>

</asp:Content>
