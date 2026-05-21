using Integral.Web.Services;
using System;
using System.Collections.Generic;
using System.Text;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using Microsoft.AspNetCore.Mvc;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class CoachEdit : AppCode.PageBaseClasses.CoachInfoBase {

    public string ProfileReadOnlyAttr { get; private set; }
    public bool CanEditProfile, CanEditCompany, CanChangeTags, CanInvitePartners, CanViewNonProfileTabs, CanViewParticipantsTabs, CanViewProfileUrls, CanViewIntegralBio;
    public bool CanViewPendingInvites, CanEditParticipantsSettings, CanDeleteUser;
    public bool CanViewContract, CanCreateContract, CanViewAndEditHideProfileToggle, CanUpdateCoachXeroContact, CanUpdateCoachRoleFlags;
    public string GoToOwnerLinkHtml = "";
    public bool IsNewContract, IsContractFormReadOnly;
    public int TableColSpan;
    public WebHelper.Form.ImageWithUpload ProfileImageControl;
    public WebHelper.Form.ImageWithUpload CompanyLogoControl;

    public List<DbHelper.AlbertCoaches.AlbertCoachInfo> PendingInvitesByUser;
    public List<DbHelper.AlbertCoaches.AlbertCoachInfo> PendingInvitesByOthersInOrg;
    public List<DbHelper.AlbertCoaches.AlbertCoachInfo> CoachesInOrg;
    public List<DbHelper.PartnerTags.TagCategoryInfo> CategoryTagsList;
    public List<DbHelper.XeroContacts.XeroContactsInfo> XeroContactsList;
    public DbHelper.UserContract.ContractInfo ContractInfo;
    public PathHelper.CoachTabEnum SelectedPageTab;
    private List<DbHelper.OrgRoles.OrgRolesInfo> OrgRolesInfo;

    public class AjaxAction {
      public const string UpdateProfile = "UpdateProfile";
      public const string PartnerPhoto = "PartnerPhoto";
      public const string TenantOrgLogo = "TenantOrgLogo";
      public const string UpdateCompany = "updatecompany";
      public const string UpdatePartnerTags = "UpdatePartnerTags";
      public const string SendInvite = "SendInvite";
      public const string SubmitContract = "SubmitContract";
      public const string UpdateBio = "UpdateBio";
      public const string UpdateEngageSettings = "UpdateEngageSettings";
      public const string BlockUser = "BlockUser";
    }

    public class FormFields {
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string EmailAddress = "EmailAddress";
      public const string MobileNumber = "MobileNumber";
      public const string TimeZoneIdIana = "TimeZoneIdIana";
      public const string CalendlyUrlName = "CalendlyUrlName";
      public const string WebProfileUrl = "WebProfileUrl";
      public const string BioShort = "BioShort";
      public const string CoachCardBio = "CoachCardBio";
      public const string CompanyName = "CompanyName";
      public const string CompanyFriendlyName = "CompanyFriendlyName";
      public const string BusinessIdNumber = "BusinessIdNumber";
      public const string ContactPhoneNumber = "ContactPhoneNumber";
      public const string GeneralEmail = "GeneralEmail";
      public const string WebSiteURL = "WebSiteURL";
      public const string PartnerTagCategoryIdPrefix = "PartnerTagCategoryIdPrefix";
      public const string GenericSenderEmailName = "GenericSenderEmailName";
      public const string GenericSenderEmailAddress = "GenericSenderEmailAddress";
      public const string HideProfile = "HideProfile";
      public const string PartnerBio_Personal_Background = "PartnerBio_Personal_Background";
      public const string PartnerBio_Personal_MyWhy = "PartnerBio_Personal_MyWhy";
      public const string PartnerBio_Personal_HowIWork = "PartnerBio_Personal_HowIWork";
      public const string PartnerBio_Personal_WhatIDo = "PartnerBio_Personal_WhatIDo";
      public const string PartnerBio_Personal_WhatILove = "PartnerBio_Personal_WhatILove";
      public const string PartnerBio_Professional_Introduction = "PartnerBio_Professional_Introduction";
      public const string PartnerBio_Professional_Background = "PartnerBio_Professional_Background";
      public const string PartnerBio_Professional_Strengths = "PartnerBio_Professional_Strengths";
      public const string PartnerBio_Professional_RecentWork = "PartnerBio_Professional_RecentWork";
      public const string PartnerBio_Professional_Impact = "PartnerBio_Professional_Impact";
      public const string PartnerBio_Professional_Credentials = "PartnerBio_Professional_Credentials";
      public const string XeroContactId = "XeroContactId";
      public const string UserRole_IsPractitioner = "UserRole_IsPractitioner";
      public const string UserRole_IsClient = "UserRole_IsClient";
      public const string UserRole_IsParticipant = "UserRole_IsParticipant";
      public const string EnableNudges = "EnableNudges";
      public const string EnablePulse = "EnablePulse";
      public const string DateOfBirth = "DateOfBirth";
      public const string RoleTitle = "RoleTitle";
      public const string City = "City";
      public const string Country = "Country";
      public const string OrgRoleId = "OrgRoleId";
    }

    public class ContractFormFields {
      public const string PostalAddress1 = "PostalAddress1";
      public const string PostalAddress2 = "PostalAddress2";
      public const string PostalPostCode = "PostalPostCode";
      public const string PostalCountry = "PostalCountry";
      public const string IDDateOfBirth = "IDDateOfBirth";
      public const string IDLicenseOrPassport = "IDLicenseOrPassport";
      public const string IDCountryOfIssue = "IDCountryOfIssue";
      public const string BankAccountName = "BankAccountName";
      public const string BankAccountBSB = "BankAccountBSB";
      public const string BankAccountNumber = "BankAccountNumber";
      public const string NextKinFullName = "NextKinFullName";
      public const string NextKinMobileNumber = "NextKinMobileNumber";
      public const string ContractType = "ContractType";
      public const string Agree_IntegralPaySuper = "Agree_IntegralPaySuper";
      public const string Agree_CasualTerms = "Agree_CasualTerms";
      public const string Agree_RegisteredABN = "Agree_RegisteredABN";
      public const string Agree_PayOwnSuper = "Agree_PayOwnSuper";
      public const string Agree_OwnLiabilityInsurance = "Agree_OwnLiabilityInsurance";
      public const string Agree_ContractorTerms = "Agree_ContractorTerms";
      public const string ContractorABN = "ContractorABN";
      public const string ContractorBusinessName = "ContractorBusinessName";
    }

    public class ContractFormValues {
      public string PostalAddress1;
      public string PostalAddress2;
      public string PostalPostCode;
      public string PostalCountry;
      public DateTime IDDateOfBirth;
      public string IDLicenseOrPassport;
      public string IDCountryOfIssue;
      public string BankAccountName;
      public string BankAccountBSB;
      public string BankAccountNumber;
      public string NextKinFullName;
      public string NextKinMobileNumber;
      public DbHelper.UserContract.ContractType ContractType;
      public bool Agree_IntegralPaySuper;
      public bool Agree_CasualTerms;
      public bool Agree_RegisteredABN;
      public bool Agree_PayOwnSuper;
      public bool Agree_OwnLiabilityInsurance;
      public bool Agree_ContractorTerms;
      public string ContractorABN;
      public string ContractorBusinessName;
    }

    public class FormValues {
      public string CompanyName;
      public string CompanyFriendlyName;
      public string BusinessIdNumber;
      public string ContactPhoneNumber;
      public string GeneralEmail;
      public string WebSiteURL;
      public string GenericSenderEmailName;
      public string GenericSenderEmailAddress;
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Profile";

      CanEditProfile = SessionHelper.AppAccess.Coaches.CanEditUserProfile(CoachInfo);
      CanEditCompany = SessionHelper.AppAccess.Coaches.CanEditCompany(CoachInfo);
      CanChangeTags = SessionHelper.AppAccess.Coaches.CanChangeTags(CoachInfo);
      CanInvitePartners = SessionHelper.AppAccess.Coaches.CanInvitePartners(CoachInfo);
      CanViewPendingInvites = SessionHelper.AppAccess.Coaches.CanViewPendingInvites(CoachInfo);
      CanViewContract = SessionHelper.AppAccess.Coaches.CanViewContract(CoachInfo);
      CanCreateContract = SessionHelper.AppAccess.Coaches.CanCreateContract(CoachInfo);
      CanViewNonProfileTabs = SessionHelper.AppAccess.Coaches.CanViewNonProfileTabs(CoachInfo);
      CanViewIntegralBio = SessionHelper.AppAccess.Coaches.CanViewIntegralBio(CoachInfo);
      CanViewParticipantsTabs = SessionHelper.AppAccess.Coaches.CanViewParticipantsTabs(CoachInfo);
      CanEditParticipantsSettings = SessionHelper.AppAccess.Coaches.CanEditParticipantsSettings(CoachInfo);
      CanViewProfileUrls = SessionHelper.AppAccess.Coaches.CanViewProfileUrls(CoachInfo);
      CanViewAndEditHideProfileToggle = SessionHelper.AppAccess.Coaches.CanViewAndEditHideProfileToggle(CoachInfo);
      CanViewHiddenPartners = SessionHelper.AppAccess.Coaches.CanViewHiddenPartners();
      CanViewInactivePartners = SessionHelper.AppAccess.Coaches.CanViewInactivePartners();
      CanUpdateCoachXeroContact = SessionHelper.AppAccess.Coaches.CanUpdateCoachXeroContact(CoachInfo);
      CanUpdateCoachRoleFlags = SessionHelper.AppAccess.Coaches.CanUpdateCoachRoleFlags();
      CanDeleteUser = SessionHelper.AppAccess.Coaches.CanDeleteUser() && !IsNewCoach;

      OrgRolesInfo = DbHelper.OrgRoles.GetOrgRolesList();

      ProfileImageControl = new WebHelper.Form.ImageWithUpload(
        PathHelper.Images.UserPhoto(CoachInfo, PathHelper.Images.UserPhotoSize.Large, true),
        WebHelper.Form.ImageType.ProfileImage,
        AjaxAction.PartnerPhoto,
        CanEditProfile) {
        ButtonOnRight = true
      };

      CompanyLogoControl = new WebHelper.Form.ImageWithUpload(
        PathHelper.Images.TenantOrgLogo(CoachInfo, true),
        WebHelper.Form.ImageType.CompanyLogo,
        AjaxAction.TenantOrgLogo,
        CanEditCompany);

      // Define the colSpan
      int defaultTableColSpan = 5;
      TableColSpan = CanViewHiddenPartners ? defaultTableColSpan++ : defaultTableColSpan;
      TableColSpan = CanViewInactivePartners ? defaultTableColSpan++ : defaultTableColSpan;

      // Get page tab from query string or default to profile tab.
      if (!Enum.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PageTab, ""), true, out SelectedPageTab)) {
        SelectedPageTab = PathHelper.CoachTabEnum.profile;
      }

      if (!SystemWeb.IsHttpPost) {
        // If requesting contract tab and not able to view, redirect to default tab.
        if (SelectedPageTab == PathHelper.CoachTabEnum.contract && !CanViewContract) {
          WebHelper.Redirect(PathHelper.Pages.CoachEdit(true));
          return new EmptyResult();
        }
      }

      ProfileReadOnlyAttr = CanEditProfile ? "" : "readonly";

      if (SessionHelper.IsUserRoleAdmin && CoachInfo.OrgOwnerUserId != null && !CoachInfo.IsOrgOwner) {
        // Add "go to owner" link for admins if coach isn't the company owner.
        GoToOwnerLinkHtml = WebHelper.GetLink(new WebHelper.LinkInfo() {
          Href = PathHelper.Pages.CoachEdit(CoachInfo.OrgOwnerUserId),
          Title = "Go to Owner",
          InnerHtml = WebHelper.Icon.User_Circle.AddAttribute("style", "font-size:16px").ToString()
        });
      }

      // Partners list - everyone in the org, including self.
      CoachesInOrg = DbHelper.AlbertCoaches.GetCoachesInOrg(CanViewHiddenPartners, CanViewInactivePartners, CoachInfo.OrgId);
      PendingInvitesByUser = DbHelper.AlbertCoaches.GetPendingInvitesByUser(CoachInfo);
      PendingInvitesByOthersInOrg = DbHelper.AlbertCoaches.GetPendingInvitesByOthersInOrg(CoachInfo);
      XeroContactsList = DbHelper.XeroContacts.GetXeroContacts();

      // Initialize CategoryTagList to show dropdown selectors.
      var userIdToShowTags = UrlCoachUserId == 0 ? userInfo.UserId : UrlCoachUserId;

      CategoryTagsList = DbHelper.PartnerTags.GetPartnerTags(new DbHelper.PartnerTags.GetAllTagsParams() {
        UserId = userIdToShowTags,
        OnlyPartnerTags = true
      });

      // Get latest contract or new.
      if (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.UserContract) == PathHelper.AbleUrlValues.IdNew) {
        ContractInfo = null; // User is choosing to create a new contract.
      } else {
        ContractInfo = DbHelper.UserContract.GetLatestContract(null, CoachInfo.UserId); // Existing contract, or null for new contract.
      }
      IsNewContract = ContractInfo == null;
      IsContractFormReadOnly = !IsNewContract || !CanCreateContract;

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          if (PageAjaxAction == AjaxAction.UpdateProfile) {

            if (!CanEditProfile) {
              ajax.RespondNoAccessToFunction();
            } else {
              UpdateProfile(ajax);
            }
            return;

          } else if (PageAjaxAction == AjaxAction.UpdateBio) {

            if (!CanEditProfile) {
              ajax.RespondNoAccessToFunction();
            } else {
              UpdateBio(ajax);
            }
            return;

          } else if (PageAjaxAction == AjaxAction.PartnerPhoto) {

            if (!CanEditProfile) {
              ajax.RespondNoAccessToFunction();
            } else {
              SaveUploadedPartnerPhoto(ajax);
            }
            return;

          } else if (PageAjaxAction == AjaxAction.UpdateCompany) {

            if (CanEditCompany) {
              SubmittedCompanyForm(ajax);
            } else {
              ajax.RespondNoAccessToFunction();
            }
            return;

          } else if (PageAjaxAction == AjaxAction.TenantOrgLogo) {

            if (CanEditCompany) {
              SaveUploadedCompanyLogo();
            } else {
              ajax.RespondNoAccessToFunction();
            }
            return;

          } else if (PageAjaxAction == AjaxAction.UpdatePartnerTags) {

            SubmittedPartnerTags(ajax);
            return;

          } else if (PageAjaxAction == AjaxAction.SendInvite) {

            if (!CanInvitePartners) {
              ajax.RespondNoAccessToFunction();
            } else {
              SendInvitation(ajax);
            }
            return;

          } else if (PageAjaxAction == AjaxAction.SubmitContract) {

            if (!CanCreateContract) {
              ajax.RespondNoAccessToFunction();
            } else {
              SubmitContract(ajax);
            }
            return;

          } else if (PageAjaxAction == AjaxAction.UpdateEngageSettings) {

            if (!CanEditParticipantsSettings) {
              ajax.RespondNoAccessToFunction();
            } else {
              UpdateSettingsEngage(ajax);
            }
            return;

          } else if (PageAjaxAction == AjaxAction.BlockUser) {

            if (!CanDeleteUser) {
              ajax.RespondNoAccessToFunction();
            } else {
              BlockUser(ajax);
            }
            return;

          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    public string GetOrgRolesOptions() {

      string html = "<option>[Select Org Role]</option>";
      if (OrgRolesInfo == null) return html;
      foreach (var orgRole in OrgRolesInfo) {
        html += "<option";
        if (CoachInfo != null && CoachInfo.OrgRoleId != null && orgRole.OrgRoleId == CoachInfo.OrgRoleId) html += " selected";
        html += " value=\"" + orgRole.OrgRoleId + "\">" + orgRole.RoleTitle.HTMLEncode() + "</option>";
      }
      return html;
    }

    public string GetPageTabs() {

      if (!CanViewNonProfileTabs && !CanViewParticipantsTabs) return "";

      if (CanViewParticipantsTabs) {

        if (SelectedPageTab != PathHelper.CoachTabEnum.profile && SelectedPageTab != PathHelper.CoachTabEnum.engageSetting) {
          SelectedPageTab = PathHelper.CoachTabEnum.profile;
        }

        return WebHelper.GetPageTabs(
          new WebHelper.PageTabsInfo() { SelectedTabName = SelectedPageTab.ToString() },
          new WebHelper.PageTabItem(PathHelper.CoachTabEnum.profile.ToString(), "My Profile", true),
          new WebHelper.PageTabItem(PathHelper.CoachTabEnum.engageSetting.ToString(), "Engage Settings")
        );
      }

      return WebHelper.GetPageTabs(
        new WebHelper.PageTabsInfo() { SelectedTabName = SelectedPageTab.ToString() },
        new WebHelper.PageTabItem(PathHelper.CoachTabEnum.profile.ToString(), "My Profile", true),
        new WebHelper.PageTabItem(PathHelper.CoachTabEnum.bio.ToString(), "Partner Bio"),
        new WebHelper.PageTabItem(PathHelper.CoachTabEnum.partnertags.ToString(), "Partner Tags"),
        new WebHelper.PageTabItem(PathHelper.CoachTabEnum.company.ToString(), "Company"),
        new WebHelper.PageTabItem(PathHelper.CoachTabEnum.partners.ToString(), "Company Partners"),
        new WebHelper.PageTabItem(PathHelper.CoachTabEnum.contract.ToString(), "Contract") { IsHidden = !CanViewContract }
      );
    }

    public string GetContractTypeOptionsHtml() {

      var html = new StringBuilder();
      html.AppendLine($@"<option value="""">[Select]</option>");
      html.AppendLine($@"<option {(ContractInfo?.UserContractTypeId == (int)DbHelper.UserContract.ContractType.Casual ? "selected" : "")} value=""{DbHelper.UserContract.ContractType.Casual}"">Casual Employment Agreement</option>");
      html.AppendLine($@"<option {(ContractInfo?.UserContractTypeId == (int)DbHelper.UserContract.ContractType.Contractor ? "selected" : "")} value=""{DbHelper.UserContract.ContractType.Contractor}"">Services Agreement</option>");

      return html.ToString();
    }

    public void SubmitContract(AjaxSubmitHelper ajax) {

      var fv = new ContractFormValues();

      fv.IDDateOfBirth = ajax.GetDatePickerDateUnspecified(ContractFormFields.IDDateOfBirth, "Date of Birth", true, "") ?? DateTime.MinValue;
      fv.PostalAddress1 = ajax.CheckFieldRegex(ContractFormFields.PostalAddress1, "Postal Address", AppHelper.Regex.GeneralText, true, "Please use plain text for Postal Address.");
      fv.PostalPostCode = ajax.CheckFieldRegex(ContractFormFields.PostalPostCode, "Post/Zip Code", AppHelper.Regex.GeneralText, true, "Please provide valid Post/Zip Code.");
      fv.PostalCountry = ajax.CheckFieldRegex(ContractFormFields.PostalCountry, "Postal Country", AppHelper.Regex.GeneralText, true, "Please use plain text for Country.");
      fv.IDLicenseOrPassport = ajax.CheckFieldRegex(ContractFormFields.IDLicenseOrPassport, "Identification Number", AppHelper.Regex.GeneralText, true, "Please provide a valid Identification Number.");
      fv.IDCountryOfIssue = ajax.CheckFieldRegex(ContractFormFields.IDCountryOfIssue, "Country of Issue", AppHelper.Regex.GeneralText, true, "Please provide Country of Issue.");
      fv.BankAccountName = ajax.CheckFieldRegex(ContractFormFields.BankAccountName, "Bank Account Name", AppHelper.Regex.GeneralText, true, "Please use plain text for Bank Account Name.");
      fv.BankAccountBSB = ajax.CheckFieldRegex(ContractFormFields.BankAccountBSB, "Bank Account BSB", AppHelper.Regex.GeneralText, true, "Please use plain text for Bank Account BSB.");
      fv.BankAccountNumber = ajax.CheckFieldRegex(ContractFormFields.BankAccountNumber, "Bank Account Number", AppHelper.Regex.GeneralText, true, "Please use plain text for Bank Account Number.");
      fv.NextKinFullName = ajax.CheckFieldRegex(ContractFormFields.NextKinFullName, "Next of Kin Name", AppHelper.Regex.GeneralText, true, "Please use plain text for Next of Kin Name.");
      fv.NextKinMobileNumber = ajax.CheckFieldRegex(ContractFormFields.NextKinMobileNumber, "Next of Kin Phone", AppHelper.Regex.Mobile, true, "Please enter valid Next of Kin Phone Number.");

      if (!Enum.TryParse(
        ajax.CheckFieldRegex(ContractFormFields.ContractType, "Agreement Type", AppHelper.Regex.GeneralText, true, "Please select Agreement Type."),
        out fv.ContractType)
      ) {
        fv.ContractType = DbHelper.UserContract.ContractType.Unset;
        ajax.AddBadField(ContractFormFields.ContractType, "Please select Agreement Type.");
      }

      if (fv.ContractType == DbHelper.UserContract.ContractType.Casual) {

        fv.Agree_IntegralPaySuper = ajax.CheckFieldBool(ContractFormFields.Agree_IntegralPaySuper, "1");
        fv.Agree_CasualTerms = ajax.CheckFieldBool(ContractFormFields.Agree_CasualTerms, "1");

        if (!fv.Agree_IntegralPaySuper || !fv.Agree_CasualTerms) ajax.AddBadField(ContractFormFields.Agree_CasualTerms, "Please confirm both above.");

      } else if (fv.ContractType == DbHelper.UserContract.ContractType.Contractor) {

        fv.Agree_RegisteredABN = ajax.CheckFieldBool(ContractFormFields.Agree_RegisteredABN, "1");
        fv.Agree_PayOwnSuper = ajax.CheckFieldBool(ContractFormFields.Agree_PayOwnSuper, "1");
        fv.Agree_OwnLiabilityInsurance = ajax.CheckFieldBool(ContractFormFields.Agree_OwnLiabilityInsurance, "1");
        fv.Agree_ContractorTerms = ajax.CheckFieldBool(ContractFormFields.Agree_ContractorTerms, "1");

        if (!fv.Agree_RegisteredABN || !fv.Agree_PayOwnSuper || !fv.Agree_OwnLiabilityInsurance || !fv.Agree_ContractorTerms) {
          ajax.AddBadField(ContractFormFields.Agree_ContractorTerms, "Please confirm all above.");
        }

        fv.ContractorABN = ajax.CheckFieldRegex(ContractFormFields.ContractorABN, "ABN", AppHelper.Regex.GeneralText, true, "Please use plain text for ABN.");
        fv.ContractorBusinessName = ajax.CheckFieldRegex(ContractFormFields.ContractorBusinessName, "Business Name", AppHelper.Regex.GeneralText, true, "Please use plain text for Business Name.");
      }

      if (ajax.BadFieldCount > 0) return;

      // All checks passed.

      int newContractId = 0;
      try {
        newContractId = UpdateContract(fv);
      } catch (Exception ex) {
        // Track exception in Application Insights
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("ContractCreation")
          .FromSession()
          .WithProperty(ApplicationInsightsConstants.ContractType, fv.ContractType.ToString())
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .Track();

        ajax.AddDialogMessage("Oops, a problem occurred creating your contract. Please try again later.", ex);
      }

      if (newContractId > 0) {
        // Send Intercom event for contract completion
        SendEvent(
          intercom => intercom.ContractCompleted()
            .FromSession()
            .WithContractId(newContractId)
            .WithContractType(fv.ContractType.ToString()),
          operationName: "CoachEdit_ContractCompleted",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["ContractId"] = newContractId,
            ["ContractType"] = fv.ContractType.ToString(),
            ["CoachUserId"] = CoachInfo.UserId
          }
        );

        ajax.SetRedirectUrl(PathHelper.Pages.CoachEdit(CoachInfo.UserId, PathHelper.CoachTabEnum.contract), "Thank you!<br/>Your contract has been created.");
      }
    }

    private int UpdateContract(ContractFormValues fv) {

      return DbHelper.UserContract.AddContract(CoachInfo, new DbHelper.UserContract.ContractInfo(
        0,
        CoachInfo.UserId,
        (int)fv.ContractType,
        "",
        DateTime.UtcNow,
        fv.PostalAddress1,
        fv.PostalAddress2,
        fv.PostalPostCode,
        fv.PostalCountry,
        fv.IDDateOfBirth,
        fv.IDLicenseOrPassport,
        fv.IDCountryOfIssue,
        fv.BankAccountName,
        fv.BankAccountBSB,
        fv.BankAccountNumber,
        fv.NextKinFullName,
        fv.NextKinMobileNumber,
        fv.ContractorABN,
        fv.ContractorBusinessName,
        fv.Agree_RegisteredABN,
        fv.Agree_PayOwnSuper,
        fv.Agree_OwnLiabilityInsurance,
        fv.Agree_ContractorTerms,
        fv.Agree_IntegralPaySuper,
        fv.Agree_CasualTerms
      ));

    }

    public void SendInvitation(AjaxSubmitHelper ajax) {

      var firstName = ajax.CheckFieldRegex(FormFields.FirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please use plain text for name.");
      var lastName = ajax.CheckFieldRegex(FormFields.LastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please use plain text for name.");
      var emailAddress = ajax.CheckFieldRegex(FormFields.EmailAddress, "Email Address", AppHelper.Regex.Email, true, "Please provide a valid email address.");

      if (ajax.BadFieldCount > 0) return;

      if (CoachesInOrg.Exists(i => i.EmailAddress.Equals(emailAddress, StringComparison.OrdinalIgnoreCase))) {
        ajax.AddBadField(FormFields.EmailAddress, "This email address has already been invited.");
      }
      // Check if email address is already used.
      // Note we are also including unregistered users here, to include other possible existing invites.
      if (DbHelper.AbleUser.GetUserByEmailOrNull(emailAddress, DbHelper.AbleUser.RegisteredFilter.Any) != null) {
        ajax.AddBadField(FormFields.EmailAddress, "This email address belongs to an existing account.");
      }

      if (ajax.BadFieldCount > 0) return;

      var inviteeBasicInfo = new DbHelper.AbleUser.InviteeBasicInfo(
        firstName, lastName, emailAddress,
        DbHelper.AbleUser.UserRoleEnum.Coach,
        userInfo.OrgId);

      DbHelper.AbleUser.AbleUserBasicInfo inviteeUserInfo;
      try {
        inviteeUserInfo = DbHelper.AbleUser.CreateInviteeUser(null, CoachInfo, inviteeBasicInfo);
      } catch (Exception ex) {
        var user = userInfo;
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CreatePartnerInvite")
          .FromSession()
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .WithProperty(ApplicationInsightsConstants.InviteeEmail, inviteeBasicInfo.EmailAddress)
          .Track();

        ajax.AddDialogMessage("Problem creating invite, please try again later.", ex);
        return;
      }

      Exception sendException = null;
      AlbertEmails.UserInviteResult sendInviteEmail = null;

      try {
        // Send invite email.
        sendInviteEmail = AlbertEmails.TrySendUserInvite(inviteeUserInfo, userInfo);

      } catch (Exception ex) {
        sendException = ex;
      }

      if (sendInviteEmail == null || !sendInviteEmail.IsSuccessful || sendException != null) {
        ajax.AddDialogMessage($"Invite added, but failed to send email to {inviteeBasicInfo.EmailAddress.HTMLEncode()}.", sendException);
        return;
      }

      // Send Intercom event for partner invitation
      var invitedExternalId = ConfigHelper.UserRole.Coach.ToExternalUserId(inviteeUserInfo.UserGuid);
      if (invitedExternalId.HasValue) {
        SendEvent(
          intercom => intercom.TeamMemberInvited()
            .FromSession()
            .WithInvitedUser(invitedExternalId.Value, inviteeUserInfo.GetFullName(), inviteeBasicInfo.EmailAddress)
            .WithInvitedRole("coach"),
          operationName: "CoachEdit_TeamMemberInvited",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["InviteeEmail"] = inviteeBasicInfo.EmailAddress,
            ["InviteeUserId"] = inviteeUserInfo.UserId,
            ["InviterUserId"] = CoachInfo.UserId
          }
        );
      }

      ajax.SetRedirectUrl(PathHelper.Pages.CoachEdit(CoachInfo.UserId, PathHelper.CoachTabEnum.partners), sendInviteEmail.Message); // Reload page to show new item.
    }

    public string GetTimeZoneOptions() {

      string selected = CoachInfo?.TimeZoneIdIana.ValueIfNullOrEmpty(ConfigHelper.DefaultTimeZoneIdIana);
      var html = new StringBuilder();
      foreach (var item in TimeHelper.GetIANATimeZonesForSelect()) {
        html.Append($@"<option value=""{item.Key.HTMLEncode()}"" ");
        if (item.Key.Equals(selected, StringComparison.OrdinalIgnoreCase)) html.Append("selected ");
        html.Append($">{item.Value.HTMLEncode()}</option>");
      }
      return html.ToString();
    }

    public string GetInviteColumn(DbHelper.AlbertCoaches.AlbertCoachInfo coachListItem) {
      if (coachListItem.InvitedUtc != null && coachListItem.RegisteredUtc == null) {
        return $"Sent {WebHelper.DisplayDate(coachListItem.InvitedUtc)}";
      } else {
        return "";
      }
    }

    public string GetCountryOptionsHtml() {

      var html = new StringBuilder();
      html.AppendLine($@"<option value="""">[Select Country]</option>");

      DbHelper.Common.Query(@"
        SELECT CountryId, CountryName
        FROM id_Country
        ORDER BY CountryName",
        dr => {
          html.AppendLine($@"<option value=""{dr.GetInt("CountryId")}"">{dr.GetString("CountryName").HTMLEncode()}</option>");
        }
      );

      return html.ToString();
    }

    void UpdateSettingsEngage(AjaxSubmitHelper ajax) {

      CoachInfo.LatestCoacheeInfo.DisableNudges = ajax.CheckFieldBool(FormFields.EnableNudges, WebHelper.YesNoButton_ValueNo);
      CoachInfo.LatestCoacheeInfo.PulseSurveyEnabled = ajax.CheckFieldBool(FormFields.EnablePulse, WebHelper.YesNoButton_ValueYes);

      try {
        DbHelper.AlbertCoaches.UpdateSettingsEngage(CoachInfo);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UpdateCoachEngageSettings")
          .FromSession()
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .Track();

        ajax.AddDialogMessage("Error updating settings engage for coach.", ex);
        return;
      }
      ajax.AddSuccessToast("Settings updated.");
    }

    void BlockUser(AjaxSubmitHelper ajax) {

      try {
        DbHelper.AbleUser.UpdateDeletedUtc(null, CoachInfo, DateTime.UtcNow);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("BlockUserLogin")
          .FromSession()
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .Track();

        ajax.AddDialogMessage("Error updating login block for user.", ex);
        return;
      }
      ajax.SetReloadPage("User's login has been blocked.", AjaxSubmitHelper.PageMessageType.SuccessToast);
    }

    void UpdateProfile(AjaxSubmitHelper ajax) {

      if (CoachInfo == null) { // new coach
        CoachInfo = new DbHelper.AlbertCoaches.AlbertCoachInfo();
      }

      // Store original Calendly URL for change detection
      string originalCalendlyUrlName = CoachInfo.CalendlyUrlName;

      CoachInfo.FirstName = ajax.CheckFieldRegex(FormFields.FirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please enter a First Name.");
      CoachInfo.LastName = ajax.CheckFieldRegex(FormFields.LastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please enter a Last Name.");
      CoachInfo.EmailAddress = ajax.CheckFieldRegex(FormFields.EmailAddress, "Email Address", AppHelper.Regex.Email, false, "Please enter a valid Email Address.");
      CoachInfo.MobileNumber = ajax.CheckFieldRegex(FormFields.MobileNumber, "Mobile", AppHelper.Regex.Mobile, false, "Please enter a mobile number.");
      CoachInfo.DateOfBirth = ajax.GetDatePickerDateUnspecified(FormFields.DateOfBirth, "Date of Birth", false, "Please provide a date.");
      CoachInfo.City = ajax.CheckFieldRegex(FormFields.City, "City", AppHelper.Regex.GeneralText, false, "Please enter a City.");
      CoachInfo.Country = ajax.CheckFieldRegex(FormFields.Country, "Country", AppHelper.Regex.GeneralText, false, "Please enter a Country.");
      CoachInfo.RoleTitle = ajax.CheckFieldRegex(FormFields.RoleTitle, "Role Title", AppHelper.Regex.GeneralText, false, "Please enter a Role Title.");

      // Check if updating profile status has changed
      if (CanViewAndEditHideProfileToggle) {
        CoachInfo.ProfileHiddenUtc = ajax.CheckFieldBool(FormFields.HideProfile, "1") ? DateTime.UtcNow : (DateTime?)null;
      }

      CoachInfo.CalendlyUrlName = ajax.CheckFieldRegex(FormFields.CalendlyUrlName, "Calendly Url Name", AppHelper.Regex.GeneralText, false, "Please enter a Calendly Url Name.");

      CoachInfo.WebProfileUrl = ajax.CheckFieldRegex(FormFields.WebProfileUrl, "Web Profile Url", AppHelper.Regex.GeneralText, false, "Please enter a Web Profile Url.");
      if (!CoachInfo.WebProfileUrl.IsNullOrEmpty()) {
        if (CoachInfo.WebProfileUrl.ContainsIgnoreCase("://")) {
          CoachInfo.WebProfileUrl.RegexReplace(@"^.*:\/\/", string.Empty);
        }
        CoachInfo.WebProfileUrl = "https://" + CoachInfo.WebProfileUrl;
        if (!WebHelper.IsValidUrl(CoachInfo.WebProfileUrl)) {
          ajax.AddBadField(FormFields.WebProfileUrl, "This is not a valid URL.");
        }
      }

      if (ajax.HasErrors) return;

      var coachUserRolesInfo = new DbHelper.AlbertCoaches.CoachUserRolesInfo();
      if (CanUpdateCoachRoleFlags) {
        coachUserRolesInfo.IsPractitioner = ajax.CheckFieldBool(FormFields.UserRole_IsPractitioner, "1");
        coachUserRolesInfo.IsClient = ajax.CheckFieldBool(FormFields.UserRole_IsClient, "1");
        coachUserRolesInfo.IsParticipant = ajax.CheckFieldBool(FormFields.UserRole_IsParticipant, "1");
      } else {
        if (IsNewCoach) {
          coachUserRolesInfo.IsPractitioner = true;
        } else {
          coachUserRolesInfo.IsPractitioner = CoachInfo.IsAbleCoach;
          coachUserRolesInfo.IsClient = CoachInfo.IsAbleClient;
          coachUserRolesInfo.IsParticipant = CoachInfo.IsParticipant;
        }
      }

      string timeZoneIdIana = ajax.CheckFieldRegex(FormFields.TimeZoneIdIana, "Time Zone", AppHelper.Regex.GeneralText, true, "Please select a Time Zone.");
      try {
        CoachInfo.SetTimeZoneIdIana(timeZoneIdIana);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("SetCoachTimeZone")
          .FromSession()
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .WithProperty(ApplicationInsightsConstants.TimeZoneIdIana, timeZoneIdIana)
          .Track();

        ajax.AddDialogMessage("Please select a Time Zone.");
        return;
      }

      if (CanUpdateCoachXeroContact) {
        int? xeroContactId = ajax.CheckFieldIntOrNull(FormFields.XeroContactId, false);

        if (xeroContactId != null && !XeroContactsList.Exists(x => x.XeroContactId == xeroContactId)) {
          ajax.AddDialogMessage("Please select a valid Xero Contact.");
          return;
        }

        CoachInfo.XeroContactId = xeroContactId;
      }

      if (ajax.BadFieldCount > 0) return;

      bool reloadPage = false; // Reload or just show a toast message.

      if (IsNewCoach) {
        // Add new coach.
        try {
          DbHelper.AlbertCoaches.CreateCoach(userInfo.OrgId, CoachInfo, coachUserRolesInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("CreateCoach")
            .FromSession()
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .WithProperty(ApplicationInsightsConstants.CoachEmail, CoachInfo?.EmailAddress)
            .Track();

          ajax.AddDialogMessage("Error creating new coach.", ex);
          return;
        }
      } else {
        bool originalIsPartnerActive = CoachInfo.IsPartnerActive;
        try {
          DbHelper.Common.UsingTransaction(trans => {
            return DbHelper.AlbertCoaches.UpdateCoach(trans, CoachInfo, coachUserRolesInfo);
          });
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("UpdateCoach")
            .FromSession()
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .Track();

          ajax.AddDialogMessage("Error updating coach.", ex);
          return;
        }
        if (originalIsPartnerActive != CoachInfo.IsPartnerActive) reloadPage = true; // Reload page to show updated status.
      }

      // Return saved user id
      ajax.AddReturnValue("CoachId", CoachInfo.UserId);

      if (reloadPage) {
        ajax.SetReloadPage(IsNewCoach ? "Profile has been created" : "Profile data has been updated.", AjaxSubmitHelper.PageMessageType.InfoToast);
      } else {
        ajax.AddSuccessToast(IsNewCoach ? "Profile has been created" : "Profile data has been updated.");
      }

      // Send Intercom event if Calendly URL was changed
      if (!IsNewCoach && originalCalendlyUrlName != CoachInfo.CalendlyUrlName && !CoachInfo.CalendlyUrlName.IsNullOrEmpty()) {
        SendEvent(
          intercom => intercom.CalendlyUrlUpdated()
            .FromSession()
            .WithCalendlyUrlName(CoachInfo.CalendlyUrlName)
            .WithOldCalendlyUrlName(originalCalendlyUrlName ?? ""),
          operationName: "CoachEdit_CalendlyUrlUpdated",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["CoachUserId"] = CoachInfo.UserId,
            ["NewCalendlyUrl"] = CoachInfo.CalendlyUrlName,
            ["OldCalendlyUrl"] = originalCalendlyUrlName ?? ""
          }
        );
      }
    }

    void UpdateBio(AjaxSubmitHelper ajax) {

      if (CoachInfo == null) { // new coach
        CoachInfo = new DbHelper.AlbertCoaches.AlbertCoachInfo();
      }

      CoachInfo.BioShort = WebHelper.GetFormValue(FormFields.BioShort);
      CoachInfo.PartnerBio_CoachCardBio = WebHelper.GetFormValue(FormFields.CoachCardBio);
      if (CanViewIntegralBio) {
        CoachInfo.PartnerBio_Personal_Background = WebHelper.GetFormValue(FormFields.PartnerBio_Personal_Background);
        CoachInfo.PartnerBio_Personal_MyWhy = WebHelper.GetFormValue(FormFields.PartnerBio_Personal_MyWhy);
        CoachInfo.PartnerBio_Personal_HowIWork = WebHelper.GetFormValue(FormFields.PartnerBio_Personal_HowIWork);
        CoachInfo.PartnerBio_Personal_WhatIDo = WebHelper.GetFormValue(FormFields.PartnerBio_Personal_WhatIDo);
        CoachInfo.PartnerBio_Personal_WhatILove = WebHelper.GetFormValue(FormFields.PartnerBio_Personal_WhatILove);
        CoachInfo.PartnerBio_Professional_Introduction = WebHelper.GetFormValue(FormFields.PartnerBio_Professional_Introduction);
        CoachInfo.PartnerBio_Professional_Background = WebHelper.GetFormValue(FormFields.PartnerBio_Professional_Background);
        CoachInfo.PartnerBio_Professional_Strengths = WebHelper.GetFormValue(FormFields.PartnerBio_Professional_Strengths);
        CoachInfo.PartnerBio_Professional_RecentWork = WebHelper.GetFormValue(FormFields.PartnerBio_Professional_RecentWork);
        CoachInfo.PartnerBio_Professional_Impact = WebHelper.GetFormValue(FormFields.PartnerBio_Professional_Impact);
        CoachInfo.PartnerBio_Professional_Credentials = WebHelper.GetFormValue(FormFields.PartnerBio_Professional_Credentials);
      }

      if (ajax.BadFieldCount > 0) return;

      if (CoachInfo.PartnerBioId == null) {
        try {
          DbHelper.AlbertCoaches.CreatePartnerBio(CoachInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("CreatePartnerBio")
            .FromSession()
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .Track();

          ajax.AddDialogMessage("Error creating partner's bio.", ex);
          return;
        }
      } else {
        try {
          DbHelper.AlbertCoaches.UpdatePartnerBio(CoachInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("UpdatePartnerBio")
            .FromSession()
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .Track();

          ajax.AddDialogMessage("Error updating partner's bio.", ex);
          return;
        }
      }

      ajax.SetReloadPage("Bio has been updated.");

    }

    void SaveUploadedPartnerPhoto(AjaxSubmitHelper ajax) {

      var uploadedFile = SystemWeb.GetRequestFile("image");
      if (uploadedFile == null) return;

      try {

        using (var inputStream = uploadedFile.OpenReadStream()) {
          PathHelper.Images.SaveStreamToUserPhoto(inputStream, CoachInfo.FirstName, CoachInfo.LastName);
        }

        // Send Intercom event for profile photo upload
        SendEvent(
          intercom => intercom.ProfilePhotoUploaded()
            .FromSession(),
          operationName: "CoachEdit_ProfilePhotoUploaded",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["CoachUserId"] = CoachInfo.UserId
          }
        );

      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UploadPartnerPhoto")
          .FromSession()
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .Track();

        ajax.AddDialogMessage("Unable to upload photo.", ex);
      }
    }

    void SaveUploadedCompanyLogo() {
      var uploadedFile = SystemWeb.GetRequestFile("image");
      if (uploadedFile == null) return;
      using (var fileStream = System.IO.File.Create(PathHelper.Images.TenantOrgLogoServerPath(CoachInfo.OrgGuid))) {
        using (var inputStream = uploadedFile.OpenReadStream()) {
          inputStream.CopyTo(fileStream);
        }
      }
    }

    bool GetFormValues_Company(AjaxSubmitHelper ajax, FormValues formValues) {

      formValues.CompanyName = ajax.CheckFieldRegex(FormFields.CompanyName, "Company Name", AppHelper.Regex.GeneralText, true, "Please enter Company Name");
      formValues.CompanyFriendlyName = ajax.CheckFieldRegex(FormFields.CompanyFriendlyName, "Company Friendly Name", AppHelper.Regex.GeneralText, true, "Please enter Friendly Company Name");
      formValues.BusinessIdNumber = ajax.CheckFieldRegex(FormFields.BusinessIdNumber, "Business ID Number", AppHelper.Regex.GeneralText, true, "Please enter Business ID Number");
      formValues.ContactPhoneNumber = ajax.CheckFieldRegex(FormFields.ContactPhoneNumber, "Contact Phone Number", AppHelper.Regex.Mobile, false, "Please enter Contact Phone Number");
      formValues.GeneralEmail = ajax.CheckFieldRegex(FormFields.GeneralEmail, "General Email", AppHelper.Regex.Email, true, "Please enter your General Email");
      formValues.WebSiteURL = ajax.CheckFieldRegex(FormFields.WebSiteURL, "Website URL", AppHelper.Regex.URL, false, "Please enter your Website URL, starting with 'http(s)://'");
      formValues.GenericSenderEmailName = ajax.CheckFieldRegex(FormFields.GenericSenderEmailName, "Sender Email Name", AppHelper.Regex.GeneralText, false, "Please use only text for sender name.");
      formValues.GenericSenderEmailAddress = ajax.CheckFieldRegex(FormFields.GenericSenderEmailAddress, "Sender Email Address", AppHelper.Regex.Email, false, "Please enter valid email address.");

      if (ajax.BadFieldCount > 0) return false;

      // If either sender email name or address given, both are required.
      if (formValues.GenericSenderEmailName.IsNullOrEmpty() && !formValues.GenericSenderEmailAddress.IsNullOrEmpty()) {
        ajax.AddBadField(FormFields.GenericSenderEmailName, "Please enter both email name and email address.");
        return false;
      } else if (formValues.GenericSenderEmailAddress.IsNullOrEmpty() && !formValues.GenericSenderEmailName.IsNullOrEmpty()) {
        ajax.AddBadField(FormFields.GenericSenderEmailAddress, "Please enter both email name and email address.");
        return false;
      }

      // Check if sender email domain is verified.
      if (!formValues.GenericSenderEmailAddress.IsNullOrEmpty()) {
        string emailDomain = formValues.GenericSenderEmailAddress.Split('@')[1];
        var domainInfo = DbHelper.SendingDomain.GetDomainByName(emailDomain);
        if (domainInfo == null) {
          ajax.AddBadField(FormFields.GenericSenderEmailAddress, "The domain '" + emailDomain + "' has not yet been verfied.");
          return false;
        }
      }

      return true;
    }

    void SubmittedCompanyForm(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();
      if (!GetFormValues_Company(ajax, formValues)) return;

      UpdateCompany(ajax, formValues);
    }

    void SubmittedPartnerTags(AjaxSubmitHelper ajax) {

      List<int> partnerTagIdsUX = null;
      try {
        partnerTagIdsUX = WebHelper.GetFormValue(FormFields.PartnerTagCategoryIdPrefix).ToIntList();
      } catch (Exception) { } // Ignore

      // Validate tags
      List<int> partnerTagIds = new List<int>();
      foreach (var catTag in CategoryTagsList) {
        foreach (var tag in catTag.TagInfoList) {
          // Even if tag is not selected in the UX but partner can't edit it and it's selected in db, include it.
          if (tag.IsSelected && !tag.PartnerCanEdit && !SessionHelper.IsUserRoleAdmin) {
            partnerTagIds.Add(tag.TagId);
            continue;
          }
          // If tag is selected in UX, define if user can update it. Otherwise skip it.
          if (partnerTagIdsUX.Exists(x => x == tag.TagId)) {
            bool canUpdateTag = tag.PartnerCanEdit || (!tag.PartnerCanEdit && SessionHelper.IsUserRoleAdmin) || tag.IsSelected;
            if (canUpdateTag) {
              partnerTagIds.Add(tag.TagId);
            }
          }
        }
      }

      try {
        DbHelper.PartnerTags.UpdatePartnerTags(CoachInfo, partnerTagIds);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UpdatePartnerTags")
          .FromSession()
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .WithProperty(ApplicationInsightsConstants.TagCount, partnerTagIds?.Count.ToString())
          .Track();

        ajax.AddDialogMessage("A problem occurred while updating the tags - please refresh the page and try agin.");
        return;
      }

      ajax.SetReloadPage("Tags have been updated.");
    }

    bool UpdateCompany(AjaxSubmitHelper ajax, FormValues formValues) {

      TenantOrgInfo.OrgName = formValues.CompanyName;
      TenantOrgInfo.OrgFriendlyName = formValues.CompanyFriendlyName;
      TenantOrgInfo.BusinessIDNumber = formValues.BusinessIdNumber;
      TenantOrgInfo.OrgEmail = formValues.GeneralEmail;
      TenantOrgInfo.OrgPhone = formValues.ContactPhoneNumber;
      TenantOrgInfo.WebSiteURL = formValues.WebSiteURL;
      TenantOrgInfo.GenericSenderEmailName = formValues.GenericSenderEmailName;
      TenantOrgInfo.GenericSenderEmailAddress = formValues.GenericSenderEmailAddress;

      try {
        DbHelper.TenantOrg.UpdateCompany(null, TenantOrgInfo);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UpdateCompany")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .AddExternalUserId(ExternalUserKind.Coach, ConfigHelper.UserRole.Coach.ToExternalUserId(CoachInfo?.UserGuid))
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .WithProperty(ApplicationInsightsConstants.CompanyId, TenantOrgInfo?.OrgId.ToString())
          .Track();

        ajax.AddDialogMessage("Error updating company.");
        if (ConfigHelper.IsDevServer) ajax.AppendToCurrentMessage("<br/>" + ex.Message);
        EmailHelper.SendInternalSupportEmail(ex, "CoachEdit Trying to update a new Company.");
        return false;
      }

      ajax.AddReturnValue("CoachId", CoachInfo.UserId);
      return true;
    }

    public string GetDefaultRowUrl() {
      // The list row for the logged-in user will link to the Upcoming page.
      if (SessionHelper.IsUserRoleAdmin) return PathHelper.Pages.CoachUpcoming(null);
      return PathHelper.Pages.CoachEdit(null);
    }

    public string GetRowUrlForUser(DbHelper.AlbertCoaches.AlbertCoachInfo coachListItem) {
      // The list row for the logged-in user will link to the Upcoming page.
      if (!SessionHelper.IsUserRoleAdmin && coachListItem.UserId == userInfo.UserId) return PathHelper.Pages.CoachEdit(null);
      return "";
    }

    public string GetCategoryTagSelectName(int partnerTagCategoryId) {
      return $"{FormFields.PartnerTagCategoryIdPrefix}{partnerTagCategoryId}";
    }

    public string GetXeroContactOptions() {
      return WebHelper.GetXeroContactOptions(false, XeroContactsList, CoachInfo?.XeroContactId);
    }

    public string GetEnablePulseOptions(string labelText) {
      string lastSentOn = CoachInfo.LatestCoacheeInfo.PulseSurveyLastSentUtc.HasValue ? $"Sent On: {WebHelper.DisplayDate(CoachInfo.LatestCoacheeInfo.PulseSurveyLastSentUtc.UtcToTZOrNull())}." : "";
      return WebHelper.GetYesNoButtons(labelText, FormFields.EnablePulse, CoachInfo.LatestCoacheeInfo.PulseSurveyEnabled, !CanEditParticipantsSettings, lastSentOn);
    }
  }
}
