<%@ Page Language="C#" AutoEventWireup="true" CodeFile="CoachEdit.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.CoachEdit" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.css">
  <script src="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.js"></script>

  <%= GetPageTabs() %>

  <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoachTabEnum.profile %>">
    <% new WebHelper.Form.SectionTitle() {
        TitleText = "Profile",
        HelpText = "Your personal profile and settings.",
        HelpLinkUrl = "https://help.helloable.co/en/articles/12504233-complete-your-profile",
      }.WriteHtml(); %>

    <form class="form-horizontal" id="clientForm">

      <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.UpdateProfile %>" />
      <input type="hidden" name="CoachUserId" value="<%= IsNewCoach ? "new" : CoachInfo.UserId.ToString() %>" />

      <% new WebHelper.Form.FormRow() {
          LabelText = "Profile Image:",
          LabelHelpText = "Image must be PNG or JPEG and under 10MB",
          ContentHtml = ProfileImageControl.ToHtml()
        }.WriteHtml(); %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Your Name: ",
          ContentHtml = new WebHelper.Form.TextInputDual() {
            IsReadOnly = !CanEditProfile,
            Input1Name = FormFields.FirstName,
            Input1Value = CoachInfo.FirstName,
            Input2Name = FormFields.LastName,
            Input2Value = CoachInfo.LastName
          }.ToHtml()
        }.WriteHtml(); %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Email Address:",
          LabelHelpText = "Visible to your clients, team and other partners.",
          ContentHtml = new WebHelper.Form.TextInput() {
            IsReadOnly = !CanEditProfile,
            InputName = FormFields.EmailAddress,
            Value = CoachInfo.EmailAddress.HTMLEncode(),
            Attributes = ProfileReadOnlyAttr,
            Type = "email"
          }.ToHtml()
        }.WriteHtml(); %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Mobile Number:",
          LabelHelpText = "Visible to your clients and team.",
          ContentHtml = new WebHelper.Form.TextInput() {
            IsReadOnly = !CanEditProfile,
            InputName = FormFields.MobileNumber,
            Value = CoachInfo.MobileNumber.HTMLEncode(),
            Attributes = ProfileReadOnlyAttr
          }.ToHtml()
        }.WriteHtml(); %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Location:",
          LabelHelpText = "Visible to your clients, team and other partners.",
          ContentHtml = new WebHelper.Form.TextInputDual() {
            IsReadOnly = !CanEditProfile,
            Input1Name = FormFields.City,
            Input1Value = CoachInfo.City,
            Input1Placeholder = "City",
            Input2Name = FormFields.Country,
            Input2Value = CoachInfo.Country,
            Input2Placeholder = "Country"
          }.ToHtml()
        }.WriteHtml(); %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Role Title:",
          ContentHtml = new WebHelper.Form.TextInput() {
            InputName = FormFields.RoleTitle,
            Value = CoachInfo.RoleTitle.HTMLEncode(),
            IsReadOnly = !CanEditProfile
          }.ToHtml()
        }.WriteHtml(); %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Org Role:",
          ContentHtml = new WebHelper.Form.Select() {
            IsReadOnly = !CanEditProfile,
            InputName = FormFields.OrgRoleId,
            TopOptionsHtml = GetOrgRolesOptions()
          }.ToHtml()
        }.WriteHtml(); %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Time Zone:",
          LabelHelpText = "Emails are delivered based on your Timezone",
          ContentHtml = new WebHelper.Form.Select() {
            InputName = FormFields.TimeZoneIdIana,
            TopOptionsHtml = GetTimeZoneOptions(),
            IsReadOnly = !CanEditProfile
          }.ToHtml()
        }.WriteHtml(); %>

      <% if (CanViewProfileUrls) { %>

        <% new WebHelper.Form.FormRow() {
            LabelText = "Calendly Url:",
            LabelHelpText = "Your Calendly url to connect to your calendars.",
            LabelHelpUrl = "https://help.helloable.co/en/articles/11967865-setup-your-calendar",
            ContentHtml = new WebHelper.Form.TextInput() {
              IsReadOnly = !CanEditProfile,
              InputName = FormFields.CalendlyUrlName,
              Value = CoachInfo.CalendlyUrlName.HTMLEncode(),
              Attributes = ProfileReadOnlyAttr,
              LeftSideLabelText = "https://calendly.com/",
              RightSideLabelText = "/coaching-XXmin",
            }.ToHtml()
          }.WriteHtml(); %>

        <% new WebHelper.Form.FormRow() {
            LabelText = "Your Profile:",
            LabelHelpText = "This link will appear as a link for others.",
            ContentHtml = new WebHelper.Form.TextInput() {
              IsReadOnly = !CanEditProfile,
              LeftSideLabelText = "https://",
              InputName = FormFields.WebProfileUrl,
              Value = CoachInfo.WebProfileUrl.RegexReplace("https?://", "", RegexOptions.IgnoreCase).HTMLEncode(),
              Attributes = ProfileReadOnlyAttr
            }.ToHtml()
          }.WriteHtml(); %>

      <% } %>

      <% new WebHelper.Form.SectionTitle() {
          TitleText = "Profile Status",
      }.WriteHtml(); %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Partner Status:",
          LabelPosition = WebHelper.Form.LabelPosition.LeftWide,
          ContentAlign = WebHelper.Form.ContentAlign.Right,
          LabelHelpText = @"Your profile is automatically active and published in the Partner network for other practitioners and organisations to see,
                            unless you are a member of a team and then you may need to complete onboarding activities before your profile is active.",
          ContentHtml = "<p class='font-weight-bold'>" + (CoachInfo.IsPartnerActive ? "Active" : "Inactive") + "</p>",
        }.WriteHtml(); %>

      <% if (CanViewAndEditHideProfileToggle) { %>
        <% new WebHelper.Form.FormRow() {
            LabelText = "Hide Profile:",
            LabelPosition = WebHelper.Form.LabelPosition.LeftWide,
            ContentAlign = WebHelper.Form.ContentAlign.Right,
            LabelHelpText = "This will hide your profile from the partner list. Your profile is still visible in other areas and you can be added to programs or quotes.",
            ContentHtml = new WebHelper.Form.CheckBox() {
              InputName = FormFields.HideProfile,
              Checked = CoachInfo.IsProfileHidden,
              IsReadOnly = !CanViewAndEditHideProfileToggle,
            }.ToHtml()
          }.WriteHtml(); %>
      <% } %>

      <% if (CanUpdateCoachRoleFlags) { %>

        <% new WebHelper.Form.SectionTitle() {
            TitleText = "User Roles",
            HelpText = "Select the account types you would like to access. Only people with permission to invite members can see this.",
            HelpLinkUrl = "https://help.helloable.co/en/articles/12087331-able-account-types",
          }.WriteHtml(); %>

        <% new WebHelper.Form.FormRow() {
            LabelText = "Practitioner",
            LabelHelpText = "An account type for L&D providers eg. coaches & facilitators. This is your default account.",
            LabelPosition = WebHelper.Form.LabelPosition.LeftWide,
            ContentAlign = WebHelper.Form.ContentAlign.Right,
            ContentHtml = new WebHelper.Form.CheckBox() {
              IsReadOnly = !CanEditProfile,
              InputName = FormFields.UserRole_IsPractitioner,
              Checked = CoachInfo.IsAbleCoach,
            }.ToHtml()
          }.WriteHtml();
        %>

        <% new WebHelper.Form.FormRow() {
            RowTopMargin = WebHelper.Form.RowTopMargin.None,
            LabelText = "Client",
            LabelHelpText = "An account type for client organisations.",
            LabelPosition = WebHelper.Form.LabelPosition.LeftWide,
            ContentAlign = WebHelper.Form.ContentAlign.Right,
            ContentHtml = new WebHelper.Form.CheckBox() {
              IsReadOnly = !CanEditProfile,
              InputName = FormFields.UserRole_IsClient,
              Checked = CoachInfo.IsAbleClient,
            }.ToHtml()
          }.WriteHtml();
        %>

        <% new WebHelper.Form.FormRow() {
            RowTopMargin = WebHelper.Form.RowTopMargin.None,
            LabelText = "Leader",
            LabelHelpText = "An account type for learners and program participants. Some types require a paid Plan.",
            LabelPosition = WebHelper.Form.LabelPosition.LeftWide,
            ContentAlign = WebHelper.Form.ContentAlign.Right,
            ContentHtml = new WebHelper.Form.CheckBox() {
              IsReadOnly = !CanEditProfile,
              InputName = FormFields.UserRole_IsParticipant,
              Checked = CoachInfo.IsParticipant,
            }.ToHtml()
          }.WriteHtml();
        %>

      <% } %>

      <% if (CanEditProfile) { %>
        <div class="row mt30">
          <div class="col-lg-8 col-md-12">
            <% if (CanDeleteUser && !CoachInfo.IsSoftDeleted) { %>
              <button type="button" class="btn btn-warning btnDelete floatleft" id="btnBlockLogin">Block Login</button>
            <% } else if (CoachInfo.IsSoftDeleted) { %>
              <%= WebHelper.GetStatusBadge("Login Blocked") %>
            <% } %>
            <button type="button" id="btnUpdateProfile" class="btn btn-primary floatright" data-waitmsg="Updating..."><%= IsNewCoach ? "Add Coach" : "Update Details" %></button>
          </div>
        </div>
      <% } %>

    </form>
  </div>

  <% if (CanViewParticipantsTabs) { %>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoachTabEnum.engageSetting %>">

      <form class="form-horizontal" id="engageSettingsForm">

        <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.UpdateEngageSettings %>" />

        <div class="row mb10"><div class="col-md-6"><h4>Engage Settings</h4></div></div>

        <%= WebHelper.GetYesNoButtons("Enable Nudges:", FormFields.EnableNudges, !CoachInfo.LatestCoacheeInfo.DisableNudges, !CanEditParticipantsSettings) %>
        <%= GetEnablePulseOptions("Enable Pulse:") %>

        <% if (CanEditParticipantsSettings) { %>
          <div class="row mt30">
            <div class="col-md-12">
              <button type="button" id="btnUpdateSettings" class="btn btn-primary floatright" data-waitmsg="Updating...">Update</button>
            </div>
          </div>
        <% } %>
      </form>
    </div>

  <% } else if (CanViewNonProfileTabs) { %>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoachTabEnum.bio %>">

      <form class="form-horizontal container" id="bioForm">

        <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.UpdateBio %>" />

        <% new WebHelper.Form.SectionTitle() { TitleText = "Partner Bio" }.WriteHtml(); %>

        <% new WebHelper.Form.FormRow() {
            LabelText = "Short Bio:",
            LabelHelpText = "A short snippet so people know who you are.",
            LabelPosition = WebHelper.Form.LabelPosition.Above,
            Classes = "mb40 mt40",
            ContentHtml = new WebHelper.Form.TextArea() {
              IsReadOnly = !CanEditProfile,
              InputName = FormFields.BioShort,
              Value = CoachInfo.BioShort
            }.ToHtml()
          }.WriteHtml(); %>

        <% new WebHelper.Form.FormRow() {
            LabelText = "Bio for Cards:",
            LabelPosition = WebHelper.Form.LabelPosition.Above,
            Classes = "mb40 mt40",
            ContentHtml = new WebHelper.Form.TextArea() {
              IsReadOnly = !CanEditProfile,
              InputName = FormFields.CoachCardBio,
              Value = CoachInfo.PartnerBio_CoachCardBio
            }.ToHtml()
          }.WriteHtml(); %>

        <% if (CanViewIntegralBio) { %>

          <% new WebHelper.Form.SectionTitle() {
              TitleText = "Integral Bio",
              BottomMargin = false
            }.WriteHtml(); %>

          <div class="row mb30">
            <div class="col-md-8">
              <p>
                As a partner at Integral we need to know you, and be able to post information on our website and send
                (as needed) information to our clients. We'd like to know <b>you</b> and the business side of <b>you</b>.
              </p>
              <p>
                <b>In your personal voice (“I”), please comment on the following items - one or two paragraphs at most.</b>
                (Please remember we are putting you forward as a coach/facilitator, so it must be relevant
                to the work we are trying to win for you.)
              </p>
            </div>
          </div>

          <% new WebHelper.Form.FormRow() {
              LabelText = "Background:",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30 mt30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Personal_Background,
                Value = CoachInfo.PartnerBio_Personal_Background
              }.ToHtml()
            }.WriteHtml(); %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "My Why:",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Personal_MyWhy,
                Value = CoachInfo.PartnerBio_Personal_MyWhy
              }.ToHtml()
            }.WriteHtml(); %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "How I work:",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Personal_WhatIDo,
                Value = CoachInfo.PartnerBio_Personal_WhatIDo
              }.ToHtml()
            }.WriteHtml(); %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "What I love:",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Personal_WhatILove,
                Value = CoachInfo.PartnerBio_Personal_WhatILove
              }.ToHtml()
            }.WriteHtml(); %>

          <hr />

          <div class="row mb30">
            <div class="col-md-8">
              <p>
                <b>In the third person (using “he", “she", or “they”), respond below with no more than two paragraphs each:</b>
              </p>
            </div>
          </div>

          <% new WebHelper.Form.FormRow() {
              LabelText = "High Level Introduction",
              LabelHelpText = "Sentence on your contributions as a coach / facilitator, most notable accreditation and greatest client achievement:",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Professional_Introduction,
                Value = CoachInfo.PartnerBio_Professional_Introduction
              }.ToHtml()
            }.WriteHtml(); %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "Leadership Experience:",
              LabelHelpText = "Your experience as a leader or could could contain some similar qualities as those pretend above in personal voice",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Professional_Background,
                Value = CoachInfo.PartnerBio_Professional_Background
              }.ToHtml()
            }.WriteHtml(); %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "Strengths in Coaching and Facilitating:",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Professional_Strengths,
                Value = CoachInfo.PartnerBio_Professional_Strengths
              }.ToHtml()
            }.WriteHtml(); %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "Recent Work:",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Professional_RecentWork,
                Value = CoachInfo.PartnerBio_Professional_RecentWork
              }.ToHtml()
            }.WriteHtml(); %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "Professional Impact:",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Professional_Impact,
                Value = CoachInfo.PartnerBio_Professional_Impact
              }.ToHtml()
            }.WriteHtml(); %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "Accreditations and Credentials:",
              LabelHelpText = "(List in bullet points)",
              LabelPosition = WebHelper.Form.LabelPosition.Above,
              Classes = "mb30",
              ContentHtml = new WebHelper.Form.TextArea() {
                IsReadOnly = !CanEditProfile,
                InputName = FormFields.PartnerBio_Professional_Credentials,
                Value = CoachInfo.PartnerBio_Professional_Credentials
              }.ToHtml()
            }.WriteHtml(); %>

        <% } %>

        <% if (CanEditProfile) { %>
          <div class="row mt30">
            <div class="col-lg-8 col-md-12">
              <button type="button" id="btnUpdateBio" class="btn btn-primary floatright" data-waitmsg="Updating...">Update Bio</button>
            </div>
          </div>
        <% } %>

      </form>
    </div>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoachTabEnum.partnertags %>">

      <div class="row mb10"><div class="col-md-6"><h4>Partner Tags</h4></div></div>

      <form class="form-horizontal" id="partnerTagsForm">

        <% foreach (var categoryTag in CategoryTagsList) { %>
          <div class="form-group ajaxSubmit-field row">
            <label class="control-label col-md-2 col-sm-12 col-xs-12">
            <%= categoryTag.CategoryName %>
            </label>
            <div class="col-md-5 col-sm-12 col-xs-12">
              <select multiple="" class="form-control" style="width:100%;" name="<%= FormFields.PartnerTagCategoryIdPrefix %>" <% if (!CanChangeTags) { %> disabled="" <% } %> >
                <% foreach (var tag in categoryTag.TagInfoList) { %>
                  <% bool canUpdateTag = tag.PartnerCanEdit || (!tag.PartnerCanEdit && SessionHelper.IsUserRoleAdmin); %>
                  <option <%= canUpdateTag ? "" : "disabled" %> value="<%= tag.TagId %>" <%= tag.IsSelected ? " selected " : string.Empty%>><%= tag.TagName %></option>
                <% } %>
              </select>
            </div>
          </div>
        <% } %>

        <% if (CanChangeTags) { %>
          <div class="row form-group mt30">
            <div class="col-md-8">
              <button type="button" id="btnUpdatePartnerTags" class="btn btn-primary floatright" data-waitmsg="Updating..."> Update Tags</button>
            </div>
          </div>
        <% } %>

      </form>
    </div>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoachTabEnum.company %>">

      <div class="row mb10"><div class="col-md-6"><h4>Company</h4></div></div>

      <form class="form-horizontal" id="companyForm">

        <%= WebHelper.GetTextInput("Company Name:", FormFields.CompanyName, TenantOrgInfo.OrgName, 5, GoToOwnerLinkHtml, !CanEditCompany) %>
        <%= WebHelper.GetTextInput("Company Friendly Name:", FormFields.CompanyFriendlyName, TenantOrgInfo.OrgFriendlyName, 5, "", !CanEditCompany) %>
        <%= WebHelper.GetTextInput("Bus. Identification Number:", FormFields.BusinessIdNumber, TenantOrgInfo.BusinessIDNumber, 3, "", !CanEditCompany) %>
        <%= WebHelper.GetTextInput("Contact Phone Number:", FormFields.ContactPhoneNumber, TenantOrgInfo.OrgPhone, 3, "", !CanEditCompany) %>
        <%= WebHelper.GetTextInput("General Email:", FormFields.GeneralEmail, TenantOrgInfo.OrgEmail, 5, "", !CanEditCompany) %>
        <%= WebHelper.GetTextInput("Website URL:", FormFields.WebSiteURL, TenantOrgInfo.WebSiteURL, 5, "", !CanEditCompany) %>
        <%= WebHelper.GetFormSubheader("Custom Sender Email") %>
        <%= WebHelper.GetTextInput("Sender Email Name:", FormFields.GenericSenderEmailName, TenantOrgInfo.GenericSenderEmailName, 5, "", !CanEditCompany) %>
        <%= WebHelper.GetTextInput("Sender Email Address:", FormFields.GenericSenderEmailAddress, TenantOrgInfo.GenericSenderEmailAddress, 5, "", !CanEditCompany) %>

        <% new WebHelper.Form.FormRow() {
            LabelPosition = WebHelper.Form.LabelPosition.LeftLegacy,
            RowTopMargin = WebHelper.Form.RowTopMargin.Larger2,
            LabelText = "Company Logo:",
            LabelHelpText = "Image must be PNG or JPEG and under 10MB",
            ContentHtml = CompanyLogoControl.ToHtml()
          }.WriteHtml(); %>

        <div class="row form-group mt30">
          <label class="control-label col-md-2 col-sm-12 col-xs-12"></label>
          <div class="col-md-5 col-sm-12 col-xs-12">
            <div class="floatright">
              <% if (CanEditCompany) { %>
                <%= WebHelper.GetButton("Update Details", "btnUpdateCompany") %>
              <% } %>
            </div>
          </div>
          <div class="col-md-5 col-sm-12 col-xs-12"></div>
        </div>
      </form>
    </div>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoachTabEnum.partners %>">

      <div class="flex flex-fill mb20">
        <div><h4>Company Partners</h4></div>
        <% if (CanInvitePartners) { %>
          <div class="align-right"><button class="btn btn-primary" id="btnInvitePartner">Invite Partner</button></div>
        <% } %>
      </div>

      <% if (CanInvitePartners) { %>

        <div id="divInvitePartner" class="hidden">

          <form id="formInvitePartner" method="post" action="#" onsubmit="return false;">

            <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.SendInvite %>" />

            <div class="table-responsive">
              <table class="tbl-form table borderless width-auto align-top">
                <tr class="ajaxSubmit-field">
                  <td class="w125 align-right pt15">First Name:</td>
                  <td class="w400"><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.FirstName, NoRow = true }) %>
                    <div class="<%= WebHelper.CSSClasses.AjaxFieldErrorMsg %>"></div>
                  </td>
                </tr>
                <tr class="ajaxSubmit-field">
                  <td class="align-right pt15">Last Name:</td>
                  <td><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.LastName, NoRow = true }) %>
                    <div class="<%= WebHelper.CSSClasses.AjaxFieldErrorMsg %>"></div>
                  </td>
                </tr>
                <tr class="ajaxSubmit-field">
                  <td class="align-right pt15">Email:</td>
                  <td><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.EmailAddress, NoRow = true }) %>
                    <div class="<%= WebHelper.CSSClasses.AjaxFieldErrorMsg %>"></div>
                  </td>
                </tr>
                <tr>
                  <td></td>
                  <td>
                    <div class="flex flex-fill">
                      <div><button class="btn btn-secondary" id="btnCancelInvite" tabindex="-1">Cancel</button></div>
                      <div class="align-right"><button class="btn btn-primary" id="btnSendInvite">Send Invitation</button></div>
                    </div>
                  </td>
                </tr>
              </table>
            </div>

          </form>
        </div>

      <% } %>

      <div class="table-responsive">
        <table class="tblCoaches table table-bordered table-hover table-rowlink limit-width" data-rowlink-url="<%= GetDefaultRowUrl() %>">
          <thead>
            <tr>
              <th class="">Partner Name</th>
              <th class="w300">Email Address</th>
              <th class="w150">Phone</th>
              <th class="w175">Invite</th>
              <th class="w175">Inviter</th>
              <% if (CanViewHiddenPartners) { %>
                <th class="w50"></th>
              <% } %>
              <% if (CanViewInactivePartners) { %>
                <th class="w50"></th>
              <% } %>
            </tr>
          </thead>
          <tbody>
            <% if (CanViewPendingInvites) { %>
              <tr>
                <td colspan="<%= TableColSpan %>">
                  <h4>My Pending Invites</h4>
                </td>
              </tr>
              <% if (PendingInvitesByUser.IsNullOrEmpty()) { %>
                <tr>
                  <td colspan="<%= TableColSpan %>">None</td>
                </tr>
              <% } else { %>
                <% foreach (var invitedCoach in PendingInvitesByUser) { %>
                  <tr>
                    <td><%= invitedCoach.GetFullName().HTMLEncode() %></td>
                    <td><%= invitedCoach.EmailAddress.HTMLEncode() %></td>
                    <td><%= invitedCoach.MobileNumber.HTMLEncode() %></td>
                    <td><%= GetInviteColumn(invitedCoach) %></td>
                    <td></td>
                    <% if (CanViewHiddenPartners) { %> <td></td> <% } %>
                    <% if (CanViewInactivePartners) { %> <td></td> <% } %>
                  </tr>
                <% } %>
              <% } %>
            <% } %>
            <tr>
              <td colspan="<%= TableColSpan %>">
                <h4>Others' Pending Invites</h4>
              </td>
            </tr>
            <% if (PendingInvitesByOthersInOrg.IsNullOrEmpty()) { %>
              <tr>
                <td colspan="<%= TableColSpan %>">None</td>
              </tr>
            <% } else { %>
              <% foreach (var invitedCoach in PendingInvitesByOthersInOrg) { %>
                <tr>
                  <td><%= invitedCoach.GetFullName().HTMLEncode() %></td>
                  <td><%= invitedCoach.EmailAddress.HTMLEncode() %></td>
                  <td><%= invitedCoach.MobileNumber.HTMLEncode() %></td>
                  <td><%= GetInviteColumn(invitedCoach) %></td>
                  <td><%= invitedCoach.GetInvitedByFullName().HTMLEncode() %></td>
                  <% if (CanViewHiddenPartners) { %> <td></td> <% } %>
                  <% if (CanViewInactivePartners) { %> <td></td> <% } %>
                </tr>
              <% } %>
            <% } %>
            <tr>
              <td colspan="<%= TableColSpan %>">
                <h4>My Partners</h4>
              </td>
            </tr>
            <% foreach (var partner in CoachesInOrg) { %>
              <% if (partner.IsRegistered && partner.OrgId == CoachInfo.OrgId) { %>
                <tr tabindex="0" class="rowData" data-rowlink-id="<%= partner.UserId %>" data-rowlink-url="<%= GetRowUrlForUser(partner) %>">
                  <td><%= partner.GetFullName().HTMLEncode() %></td>
                  <td><%= partner.EmailAddress.HTMLEncode() %></td>
                  <td><%= partner.MobileNumber.HTMLEncode() %></td>
                  <td><%= GetInviteColumn(partner) %></td>
                  <td></td>
                  <% if (CanViewHiddenPartners) { %>
                    <td class="w50"><%= WebHelper.GetPartnerHiddenIcon(partner.IsProfileHidden, CanViewHiddenPartners) %></td>
                  <% } %>
                  <% if (CanViewInactivePartners) { %>
                    <td><%= WebHelper.GetPartnerStatusIcon(partner.IsPartnerActive) %></td>
                  <% } %>
                </tr>
              <% } %>
            <% } %>
          </tbody>
        </table>
      </div>
    </div>

    <% if (CanViewContract) { %>

      <div class="tab-panel" data-appendTo="panel-<%= PathHelper.CoachTabEnum.contract %>" style="max-width: 800px">

        <div class="row mb10">
          <div class="col-md-6">
            <h4>Partner Account Agreement</h4>
          </div>
        </div>

        <p>
          This agreement is between Integral Development Associates Pty Ltd, ABN 41 008 738 672 ("Integral", the “Company”, "we", or "us"),
          and “you”, the person or entity using our Platform (the "Partner").
          Our Agreement with you incorporates these related documents: the Partner Form (this form); our Terms & Conditions,
          our Terms of Use, our Privacy Policy, and the Definitions (collectively our agreement with you).
        </p>
        <p>
          For more information see: <a href="https://www.integral.global/partner-legals" target="_blank">https://www.integral.global/partner-legals</a>.
        </p>

        <% if (ContractInfo != null) { %>
        <p class="mt20">
          This agreement was accepted on <%= WebHelper.DisplayDate(TimeHelper.UtcToTimeZoneId(ContractInfo.SubmittedUtc, ConfigHelper.DefaultTimeZoneIdIana).ToDateTimeOrNull()) %>
          (<%= ConfigHelper.DefaultTimeZoneAbbrev %>)
        </p>
        <% } %>

        <hr />

        <form class="form-horizontal" id="formContract" onsubmit="return false;">

          <%= WebHelper.GetSelectRow("Partner Agreement Type:", ContractFormFields.ContractType, 8, GetContractTypeOptionsHtml(), "", IsContractFormReadOnly) %>

          <hr />
          <%= WebHelper.GetInputDateRow("Date of Birth:", ContractFormFields.IDDateOfBirth, ContractInfo?.IDDateOfBirth, "", IsContractFormReadOnly) %>
          <%= WebHelper.GetTextInput("Postal Address:", ContractFormFields.PostalAddress1, "", ContractInfo?.PostalAddress1, IsContractFormReadOnly, WebHelper.InputMaxLength.NoLimit) %>
          <%= WebHelper.GetTextInput("Post/Zip Code:", ContractFormFields.PostalPostCode, "", ContractInfo?.PostalPostCode, IsContractFormReadOnly, WebHelper.InputMaxLength.NoLimit) %>
          <%= WebHelper.GetTextInput("Country:", ContractFormFields.PostalCountry, "", ContractInfo?.PostalCountry, IsContractFormReadOnly, WebHelper.InputMaxLength.NoLimit) %>

          <hr />
          <%= WebHelper.GetTextInput("Passport or Drivers License ID:", ContractFormFields.IDLicenseOrPassport, "", ContractInfo?.IDLicenseOrPassport, IsContractFormReadOnly, WebHelper.InputMaxLength.NoLimit) %>
          <%= WebHelper.GetTextInput("Country of Issue:", ContractFormFields.IDCountryOfIssue, "", ContractInfo?.IDCountryOfIssue, IsContractFormReadOnly, WebHelper.InputMaxLength.NoLimit) %>

          <hr />
          <%= WebHelper.GetTextInput("Name on Bank Account / Account Name:", ContractFormFields.BankAccountName, "", ContractInfo?.BankAccountName, IsContractFormReadOnly) %>
          <%= WebHelper.GetTextInputDual("Bank Account Details:", ContractFormFields.BankAccountBSB, ContractInfo?.BankAccountBSB, "BSB Number", ContractFormFields.BankAccountNumber, ContractInfo?.BankAccountNumber, "Account Number", IsContractFormReadOnly) %>

          <hr />
          <%= WebHelper.GetTextInput("Next of Kin Name:", ContractFormFields.NextKinFullName, "", ContractInfo?.NextOfKinFullName, IsContractFormReadOnly) %>
          <%= WebHelper.GetTextInput("Next of Kin Phone Number:", ContractFormFields.NextKinMobileNumber, "", ContractInfo?.NextOfKinMobileNumber, IsContractFormReadOnly, WebHelper.InputMaxLength.MobilePhoneNumber) %>

          <hr />

          <div id="ContractType_<%= DbHelper.UserContract.ContractType.Casual %>">

            <h4>Casual Employment Agreement</h4>

            <p>Our Agreement with you incorporates these related documents: the “Partner Contract Form” (this form);
              our Casual Terms & Conditions, our Terms of Use, our Privacy Policy, the Definitions & Interpretation,
              and the Casual Agreement (collectively the “Casual Employment Agreement”).</p>
            <p>The related documents can be found here:</p>
            <ul>
              <li><a href="https://www.integral.global/partner-legals" target="_blank">https://www.integral.global/partner-legals</a></li>
              <li><a href="https://www.integral.global/partner-casual-agreement" target="_blank">https://www.integral.global/partner-casual-agreement</a></li>
            </ul>

            <p class="mt10"><%= WebHelper.CustomCheckBox(ContractFormFields.Agree_IntegralPaySuper, "1", ContractInfo?.Agree_IntegralPaySuper, IsContractFormReadOnly,
                "I understand & agree Integral will pay Superannuation, income tax, and GST on my behalf, where relevant.") %></p>
            <p class="mt10"><%= WebHelper.CustomCheckBox(ContractFormFields.Agree_CasualTerms, "1", ContractInfo?.Agree_CasualTerms, IsContractFormReadOnly,
                "I have read & agree with the Casual Employment Agreement.") %></p>

          </div>

          <div id="ContractType_<%= DbHelper.UserContract.ContractType.Contractor %>">

            <h4>Partner Services Agreement</h4>

            <p>Our Agreement with you incorporates these related documents: the “Partner Contract Form” (this form);
              our Partner Terms & Conditions, our Terms of Use, our Privacy Policy, the Definitions & Interpretation,
              and the Partner Services Agreement (collectively the “Partner Services Contract”).</p>
            <p>The related documents can be found here:</p>
            <ul>
              <li><a href="https://www.integral.global/partner-legals" target="_blank">https://www.integral.global/partner-legals</a></li>
              <li><a href="https://www.integral.global/partner-services-agreement" target="_blank">https://www.integral.global/partner-services-agreement</a></li>
            </ul>
            <p>If you do not fulfil the below criteria, please select the "Casual Partner" contract.</p>

            <%= WebHelper.GetTextInput("ABN:", ContractFormFields.ContractorABN, "", ContractInfo?.ABNNumber, IsContractFormReadOnly) %>
            <%= WebHelper.GetTextInput("Business Entity Name:", ContractFormFields.ContractorBusinessName, "", ContractInfo?.BusinessEntityName, IsContractFormReadOnly) %>

            <p class="mt10"><%= WebHelper.CustomCheckBox(ContractFormFields.Agree_RegisteredABN, "1", ContractInfo?.Agree_RegisteredABN, IsContractFormReadOnly,
                "I have a registered ABN, and have over $75,000 in revenue, and pay GST.") %></p>
            <p class="mt10"><%= WebHelper.CustomCheckBox(ContractFormFields.Agree_PayOwnSuper, "1", ContractInfo?.Agree_PayOwnSuper, IsContractFormReadOnly,
                "I will pay my own Superannuation, any income related taxes, and GST.") %></p>
            <p class="mt10"><%= WebHelper.CustomCheckBox(ContractFormFields.Agree_OwnLiabilityInsurance, "1", ContractInfo?.Agree_OwnLiabilityInsurance, IsContractFormReadOnly,
                "I have my own Public Liability insurance.") %></p>
            <p class="mt10"><%= WebHelper.CustomCheckBox(ContractFormFields.Agree_ContractorTerms, "1", ContractInfo?.Agree_ContractorTerms, IsContractFormReadOnly, "",
                @"I have read & agree with the Partner Contractor Agreement.<br/><a href=""https://www.integral.global/partner-services-agreement"" target=""_blank"">https://www.integral.global/partner-services-agreement</a>") %></p>

          </div>

          <hr />
          <% if (!IsContractFormReadOnly) { %>
            <center class="mt20 mb40"><%=WebHelper.GetButton("Accept Agreement", "btnSubmitContract", false) %></center>
          <% } else if (CanCreateContract) { %>
            <center class="mt20 mb40"><%=WebHelper.GetButton("New Agreement", "btnNewAgreement", false) %></center>
          <% } %>

        </form>

        <script>

          // Closure for contract form.
          (function ($) {

            var selContractType, btnSubmitContract, formContract, btnNewAgreement;

            $(document).ready(function () {

              selContractType = $('select[name="<%= ContractFormFields.ContractType %>"]');
              btnSubmitContract = $("#btnSubmitContract");
              btnNewAgreement = $("#btnNewAgreement");
              formContract = $("#formContract");

              selContractType.change(ShowContractType);
              ShowContractType();

              btnSubmitContract.click(SubmitContract);
              btnNewAgreement.click(NewAgreement);

            });

            function SubmitContract() {

              $.busyLoadFull("show");

              AjaxSubmit({
                form: formContract,
                action: "<%= AjaxAction.SubmitContract %>",
                onSuccess: function (jqXHR, data) { },
                onFail: function (jqXHR, data) { },
                onError: function (jqXHR, textStatus, errorThrown) {
                  if (app_isDev) common_InfoDialog(jqXHR.responseText);
                  else common_InfoDialog("Oops, a problem occurred! Please try again later.");
                },
                onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) {
                  $.busyLoadFull("hide");
                }
              });
            }

            function NewAgreement() {

              $.busyLoadFull("show");
              location.href
                = "<%= PathHelper.Pages.CoachEdit(CoachInfo.UserId, PathHelper.CoachTabEnum.contract) %>"
                + "&<%= PathHelper.AbleUrlKeys.UserContract %>=<%= PathHelper.AbleUrlValues.IdNew %>";
            }

            function ShowContractType() {

              var ctVal = selContractType.val();
              $('div[id^="ContractType_"]').hide();
              $('div[id="ContractType_' + ctVal + '"]').show();

            }

          })(jQuery);
        </script>

      </div>

    <% } // CanViewContract %>


  <% } %>

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

  <% if (CanInvitePartners) { %>

    <script type="text/javascript">

      (function ($) {

        var btnInvitePartner = $("#btnInvitePartner");
        var divInvitePartner = $("#divInvitePartner");
        var formInvitePartner = $("#formInvitePartner");
        var btnSendInvite = $("#btnSendInvite");
        var btnCancelInvite = $("#btnCancelInvite");

        $(document).ready(function () {

          formInvitePartner.submit(function (ev) { ev.preventDefault(); return false; });
          formInvitePartner.find("input").keydown(function (ev) { if (ev.which == 13) return false; });
          btnInvitePartner.click(InvitePartner);
          btnSendInvite.click(SendInvite);
          btnCancelInvite.click(CancelInvite);

        });

        function InvitePartner(ev) {
          if (btnInvitePartner.is(":disabled")) return;
          btnInvitePartner.prop("disabled", true);
          divInvitePartner.slideDown();
          formInvitePartner.find("input").eq(1).focus();
        }

        function SendInvite(ev) {
          AjaxSubmit({
            form: formInvitePartner,
            onSuccess: function (jqXHR, data) { },
            onFail: function (jqXHR, data) { },
            onError: function (jqXHR, textStatus, errorThrown) { },
            onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
          });
        }

        function CancelInvite(ev) {
          divInvitePartner.slideUp();
          formInvitePartner[0].reset();
          btnInvitePartner.prop("disabled", false);
        }

      })(jQuery);
    </script>

  <% } %>

  <script type="text/javascript">
    (function ($) {

      var $clientForm = $("#clientForm");
      var $settingsForm = $("#engageSettingsForm");
      var $btnUpdateProfile = $("#btnUpdateProfile");
      var $btnUpdateSettings = $("#btnUpdateSettings");
      var $btnUpdateBio = $("#btnUpdateBio");
      var $bioForm = $("#bioForm");
      var $btnUpdateCompany = $("#btnUpdateCompany");
      var $btnUpdatePartnerTags = $("#btnUpdatePartnerTags");
      var $formTabs = $("#formTabs");
      var $companyForm = $("#companyForm");
      var $partnerTagsForm = $("#partnerTagsForm");
      var isNewCoach = <%= IsNewCoach ? "true" : "false" %>;
      var $blockUser = $("#btnBlockLogin");

      var Cropper, cropper, $cropperModal, $cropperModalImage;

      $(document).ready(function () {

        Cropper = window.Cropper;
        $cropperModal = $("#cropperModal");
        $cropperModalImage = $("#cropperModal .cropperImage");

        if (isNewCoach) {
          // Adjust menu.
          $(".activeparent > ul.submenu > li.active a span").text("New Coach");
          $(".activeparent > ul.submenu > li:not(.active)").hide();
        }

        $btnUpdateProfile.click(UpdateProfile);
        $blockUser.click(UpdateBlockUser);
        $btnUpdateSettings.click(UpdateSettings);
        $btnUpdateBio.click(UpdateBio);
        $btnUpdateCompany.click(UpdateCompany);
        $btnUpdatePartnerTags.click(UpdatePartnerTags);

        $('#<%= ProfileImageControl.InputID %>').on('change', function () {
          CropImage(this, false, $('#<%= ProfileImageControl.ImgID %>'), "image/jpeg");
          $('#<%= ProfileImageControl.InputMessageID %>').text("Remember to Update Your Details to Save Changes");
        });

        $('#<%= CompanyLogoControl.InputID %>').on('change', function () {
          CropImage(this, true, $('#<%= CompanyLogoControl.ImgID %>'), "image/png");
        });

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

        // Activate initially selected tab.
        $('.nav-tabs a[href="#panel-<%= SelectedPageTab %>"]').tab('show');

        $("#bioForm textarea").each(function () {
          var textareaName = $(this).attr("name");

          TinyMCEInit("textarea[name='" + textareaName + "']", {
            autoresize_bottom_margin: 20,
            min_height: 150,
            plugins: 'autoresize lists link image paste code',
            toolbar: 'undo redo | formatselect | bold italic underline | alignleft aligncenter alignright | bullist numlist | link  | removeformat | code',

            setup: function (editor) {
              // Apply character limit ONLY for BioShort and CoachCardBio
              if (textareaName === "<%= FormFields.BioShort %>" || textareaName === "<%= FormFields.CoachCardBio %>") {
                const maxChars = <%= ConfigHelper.Coach_ShortBio_MaxLength %>;

                editor.on('keydown', function (e) {
                  const content = editor.getContent({ format: 'text' });
                  if (content.length >= maxChars) {
                    // Allow backspace, delete, arrow keys, home, end, and modifier-key combos (Ctrl+A, Ctrl+Z, etc.)
                    var allowedKeys = [8, 46, 37, 38, 39, 40, 35, 36]; // Backspace, Delete, Arrows, Home, End
                    if (allowedKeys.indexOf(e.keyCode) === -1 && !e.ctrlKey && !e.metaKey) {
                      e.preventDefault();
                    }
                  }
                });

                editor.on('paste', function (e) {
                  const content = editor.getContent({ format: 'text' });
                  const pastedText = (e.clipboardData || window.clipboardData).getData('text');

                  if ((content.length + pastedText.length) > maxChars) {
                    e.preventDefault();
                    const allowedText = pastedText.substring(0, maxChars - content.length);
                    editor.insertContent(allowedText);
                  }
                });
              }
            }
          });
        });

        $('#partnerTagsForm select').each(function () {
          UpdateTagSelectOptions($(this));
        });

        // Bind change event handler to all <select> elements inside #partnerTagsForm
        $('#partnerTagsForm select').on('change', function () {
          UpdateTagSelectOptions($(this));
        });

      }); // ready.

      function UpdateTagSelectOptions(select) {
        var linkedList = select.next('.select2').find('.select2-selection__rendered');

        select.find('option').each(function () {
          var option = $(this);
          var optionText = option.text();
          var listItem = linkedList.find('li[title="' + optionText + '"]');

          // Toggle the 'disabled' class based on the option's disabled state
          if (option.is(':disabled')) {
            listItem.addClass('disabled');
            listItem.find('.select2-selection__choice__remove').remove();
          } else {
            listItem.removeClass('disabled');
          }
        });
      }

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

      function UpdateSettings() {
        AjaxSubmit({
          form: $settingsForm,
          action: "<%= AjaxAction.UpdateEngageSettings %>",
          onSuccess: function (jqXHR, data) { },
          onAlways: function () { }
        });
      }

      function UpdateProfile() {

        AjaxSubmit({
          form: $clientForm,
          action: "<%= AjaxAction.UpdateProfile %>",
          onSuccess: function (jqXHR, data) {
            var userPhotoUrl = "<%= PathHelper.Images.UserPhoto(CoachInfo, PathHelper.Images.UserPhotoSize.Large, false) %>";
            var userPlaceholderUrl = "<%= PathHelper.Images.UserPhoto_Missing() %>";
            //SavePhoto("<%= AjaxAction.PartnerPhoto %>", data.CoachId, $imgProfilePic, userPhotoUrl, userPlaceholderUrl);
          }
        });
      }

      function UpdateBlockUser() {

        common_ConfirmDialog("Confirm", "Are you sure you want to block user? This action can't be undone.", function (confirmed) {
          if (confirmed) {
            AjaxSubmit({
              action: "<%= AjaxAction.BlockUser %>",
              onSuccess: function (jqXHR, data) { }
            });
          }
        });

      }

      function UpdateBio() {

        tinymce.triggerSave(); // Sync editor content to textareas

        AjaxSubmit({
          form: $bioForm,
          action: "<%= AjaxAction.UpdateBio %>",
          onSuccess: function (jqXHR, data) {
            return;
          },
          onFail: function (jqXHR, data) {
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            if (app_isDev) {
              common_InfoDialog(jqXHR.responseText);
            } else {
              common_InfoDialog("Update failed, please try again later.");
            }
          },
          onAlways: function () { }
        });
      }

      function UpdateCompany() {

        AjaxSubmit({
          form: $companyForm,
          action: "<%= AjaxAction.UpdateCompany %>",
          onSuccess: function (jqXHR, data) {
            var logoUrl = "<%= PathHelper.Images.TenantOrgLogo(CoachInfo, true) %>";
            //SavePhoto("<%= AjaxAction.TenantOrgLogo %>", data.CoachId, $imgCompanyLogo, logoUrl);
          }
        });
      }

      function UpdatePartnerTags() {

        AjaxSubmit({
          form: $partnerTagsForm,
          action: "<%= AjaxAction.UpdatePartnerTags %>",
          onSuccess: function (jqXHR, data) {
            return;
          },
          onFail: function (jqXHR, data) {
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            if (app_isDev) {
              common_InfoDialog(jqXHR.responseText);
            } else {
              common_InfoDialog("Update failed, please try again later.");
            }
          },
          onAlways: function () { }
        });
      }

      function SavePhoto(strAjaxAction, coachId, $imgTarget, photoImageUrl, placeholderImageUrl) {

        if ($imgTarget.data("blob") == null) return;

        jQuery.ajax({
          url: "<%= PathHelper.Pages.CoachEdit(null) %>" + coachId,
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
