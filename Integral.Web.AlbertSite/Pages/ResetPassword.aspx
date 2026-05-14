<%@ Page Language="C#" AutoEventWireup="true"
  MasterPageFile="~/MasterPages/Site.Master"
  CodeFile="ResetPassword.aspx.cs"
  Inherits="Integral.Web.PortalSite.ResetPassword" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ContentPlaceHolderID="BodyContent" runat="server">

  <div class="onboarding-wrapper onboarding-login">

    <%= WebHelper.GetLoggedOutAbleLogo() %>

    <div class="content-box">

      <div class="content-box-image" style="background-image: url('/images/onboarding_image.jpg')"></div>

      <div class="content-box-form">

        <% if (Reset1Visible) { %>

          <form id="formLogin" method="post" action="#" onsubmit="return false;">

            <h3>Reset Your Password</h3>

            <h5 class="mb30">A password reset link will be sent to your registered email address:</h5>

            <div class="form-row mb5">
              <label>Email Address:</label>
              <div><input class="form-control" type="email" tabindex="1" name="<%= FormFields.EmailAddress %>" placeholder="john.smith@email.com" required=""></div>
            </div>
            <div class="form-row flex">
              <label class="flex1"></label>
              <button id="btnSubmit" tabindex="2" type="button" class="btn btn-primary flex0">Send Password Reset</button>
            </div>

          </form>

          <div id="successMsg" class="hidden">
            <h3>Password Reset Sent!</h3>
            When you receive the password-reset email,<br/>
            click the link to complete the process.<br/>
            <br/>
            You can now close this browser tab,<br/>
            or go back to the <a href="/">login page</a>.
          </div>

        <% } %>

        <% if (Reset2Visible) { %>

          <form id="formLogin" onsubmit="return false">
            <h3>Reset Password</h3>

            <fieldset class="form-group">

              <h4>Hi <%= _UserFirstName %>!</h4>

              <h5 class="fieldTitle mb20">Type your new password twice below.</h5>

              <div class="ajaxSubmit-field">
                <input class="form-control" type="email" name="email" value="<%= _UserEmailAddress %>" style="display:none" />
                <input type="password" name="<%= FormFields.Password %>" autocomplete="off" id="password1" class="form-control" placeholder="" tabindex="1" autofocus />
              </div>
              <div class="ajaxSubmit-field">
                <input type="password" name="<%= FormFields.Password2 %>" autocomplete="off" id="password2" class="form-control mt20" placeholder="" tabindex="2" />
              </div>
              <div class="ajaxSubmit-field">
                <div class="<%= WebHelper.CSSClasses.AjaxFieldErrorMsg %> loginErrorMsg"></div>
              </div>
              <div class="flex flex-fill mt5">
                <div class="align-right pr10">New password must contain:</div>
                <div>- At least 8 characters<br/>
                  - At least one number (0-9)<br/>
                  - At least one symbol (<%= SystemWeb.HtmlEncode(DbHelper.AbleUser.PASSWORD_REQUIRED_SYMBOLS) %>)</div>
                </div>
              <button id="btnSubmit" type="button" class="btn btn-primary mt20" data-waitmsg="" tabindex="3">Save New Password</button>
            </fieldset>
          </form>

          <div id="successMsg" class="displaynone">
            <div class="fieldTitle">
              Done! Your password is now reset.<br/><br/>
              Click here to <a class="focus-underline" href="/">Log In</a> with your new password.
            </div>
          </div>

        <% } %>

        <% if (CodeNotFoundVisible) { %>
          <p>
            Sorry, that password reset code is no longer valid.<br/>
            <br/>
            Please <a class="focus-underline" href="<%= GetPostUrl("") %>">restart</a> your password reset to get a new code.
          </p>
        <% } %>

      </div>
    </div>

  </div>

  <script type="text/javascript">

    var $formLogin;
    var $btnSubmit;

    $(document).ready(function() {

      $formLogin = $("#formLogin");
      $btnSubmit = $("#btnSubmit");

      $("#txtEmail").focus();

      $btnSubmit.click(onSubmit);
      $formLogin.find("input").keypress(function (e) { if (e.which == 13) onSubmit(); });

    });

    function onSubmit() {
      AjaxSubmit({
        form: $formLogin,
        url: "<%= GetPostUrl() %>",
        onSuccess: function (jqXHR, data) {
          $formLogin.hide();
          $("#successMsg").show();
        }
      });
    }

  </script>

</asp:Content>
