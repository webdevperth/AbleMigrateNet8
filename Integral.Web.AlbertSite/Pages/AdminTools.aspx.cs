using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class AdminTools : AppCode.PageBaseClasses.LoggedInPageBase {

    public class AjaxAction {
      public const string LogInAsUser = "LogInAsUser";
      public const string UpdateXeroContact = "UpdateXeroContact";
      public const string UpdateClientCompany = "UpdateClientCompany";
    }

    public class FormFields {
      public const string LogInAsUserId = "LogInAsUserId";
      public const string XeroContact = "XeroContact";
      public const string XeroContactNewName = "XeroContactNewName";
      public const string UserIdForCompany = "UserIdForCompany";
      public const string CompanyId = "CompanyId";
    }

    public class FormAttr {
      public const string CompanyId = "companyid";
      public const string NotAssigned = "notassigned";
      public const string CurrentCompanyName = "companyname";
    }

    public List<DbHelper.XeroContacts.XeroContactsInfo> XeroContactsList;
    public List<DbHelper.AbleUser.LogInAsUser> LogInAsUserList;
    public List<DbHelper.ClientCompanies.BriefCompanyInfo> CompanyList;

    protected void Page_Load(object sender, EventArgs e) {

      XeroContactsList = DbHelper.XeroContacts.GetXeroContacts();
      LogInAsUserList = DbHelper.AbleUser.GetLogInAsUsers();
      CompanyList = DbHelper.ClientCompanies.GetCompanyList(SessionHelper.GetUserInfoOrNull());

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (!SessionHelper.AppAccess.PageAccess.CanAccessAdminTools()) {
            ajax.RespondNoAccessToFunction();
            return;
          }

          if (ajax.Action == AjaxAction.LogInAsUser) {
            DoLogInAsUser(ajax);
          } else if (ajax.Action == AjaxAction.UpdateXeroContact) {
            DoUpdateXeroContact(ajax);
          } else if (ajax.Action == AjaxAction.UpdateClientCompany) {
            DoUpdateUserCompany(ajax);
          } else {
            ajax.AddDialogMessage("Invalid mode.");
          }

        });
        return;
      }

    }

    public string GetLogInAsUserOptionsHtml() {

      var options = new List<WebHelper.SelectOption>();

      foreach (var user in LogInAsUserList) {

        var userTypes = new List<string>();

        if (user.IsAbleAdmin) userTypes.Add("Admin");
        if (user.IsTenantOrgAdmin) userTypes.Add("Tenant-Admin");
        if (user.IsAbleCoach) userTypes.Add("Practitioner");
        if (user.IsAbleClient) userTypes.Add("Client");
        if (user.IsParticipant) userTypes.Add("Leader");

        options.Add(new WebHelper.SelectOption(user.UserId.ToString(),
          $"{user.FirstName} {user.LastName} ({user.EmailAddress}, {user.UserId})"
          + $"{userTypes.Join(", ").EnsureStartsWith(" - ", true)}"
          + $"{user.SubscriptionName.SurroundWith(" [", "]")}"
        ));
      }
      return WebHelper.GetSelectOptionListHtml(options);
    }

    void DoLogInAsUser(AjaxSubmitHelper ajax) {

      int logInAsUserId = ajax.CheckFieldInt(FormFields.LogInAsUserId, true);
      var userInfo = DbHelper.AbleUser.GetUserByIdOrNull(logInAsUserId, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);

      if (userInfo == null || userInfo.IsUnassignedUser) {
        ajax.AddDialogMessage("Please choose a valid user.");
        return;
      }

      SessionHelper.SetLoggedInUser(userInfo);

      ajax.SetRedirectUrl(PathHelper.WebRoot);
    }

    void DoUpdateXeroContact(AjaxSubmitHelper ajax) {

      int xeroContactId = ajax.CheckFieldInt(FormFields.XeroContact, true);
      string xeroContactNewName = ajax.CheckFieldRegex(FormFields.XeroContactNewName, "Xero Contact Name", AppHelper.Regex.GeneralText, true, "You must enter a Xero Contact Name");

      var xeroContactInfo = XeroContactsList.Find(x => x.XeroContactId == xeroContactId);

      if (xeroContactInfo == null) {
        ajax.AddDialogMessage("Please choose a valid Xero contact.");
        return;
      } else if (xeroContactInfo.ContactName == xeroContactNewName) {
        ajax.AddDialogMessage("The new name for the Xero Contact is the same as the current name.");
        return;
      }

      DbHelper.XeroContacts.UpdateXeroContact(xeroContactId, xeroContactNewName);

      ajax.SetReloadPage("Xero contact updated.");
    }

    public string GetUserOptionsForCompanyHtml() {

      string options = WebHelper.GetSelectOptionHtml("", "[Select User]", true, new WebHelper.DataAttributes((FormAttr.CompanyId, FormAttr.NotAssigned)));

      foreach (var user in LogInAsUserList) {

        if (!user.IsParticipant && !user.IsAbleClient) continue; // Don't add users without at least one of these flags.

        var userTypes = new List<string>();
        if (user.IsAbleAdmin) userTypes.Add("Admin");
        if (user.IsAbleCoach) userTypes.Add("Practitioner");
        if (user.IsAbleClient) userTypes.Add("Client");
        if (user.IsParticipant) userTypes.Add("Leader");
        options += (WebHelper.GetSelectOptionHtml(
          user.UserId.ToString(),
          $"{user.FirstName} {user.LastName} {userTypes.Join(", ").SurroundWith("(", ")", false)}",
          false,
          new WebHelper.DataAttributes(
            (FormAttr.CompanyId, user.ClientCompanyId.ToStringOrNullValue("")),
            (FormAttr.CurrentCompanyName, GetUserCurrentCompanyName(user.ClientCompanyId))
          ))
        );
      }

      return options;
    }

    public string GetUserCurrentCompanyName(int? clientCompanyName) {

      if (clientCompanyName == null) return "Not assigned.";

      var company = CompanyList.Find(x => x.CompanyId == clientCompanyName);

      if (company == null) return "Not assigned.";

      return company.CompanyName;

    }

    public string GetCompanyOptionsHtml() {
      var options = new List<WebHelper.SelectOption>();
      options.Add(new WebHelper.SelectOption(FormAttr.NotAssigned, "[Select Company]", true));
      CompanyList.ForEach(company => {
        options.Add(new WebHelper.SelectOption(company.CompanyId.ToString(), company.CompanyName, false));
      });

      return WebHelper.GetSelectOptionListHtml(options);
    }

    void DoUpdateUserCompany(AjaxSubmitHelper ajax) {

      int userIdForCompany = ajax.CheckFieldInt(FormFields.UserIdForCompany, true);
      int? companyId = ajax.CheckFieldIntOrNull(FormFields.CompanyId, false);

      var userForCompany = LogInAsUserList.Find(u => u.UserId == userIdForCompany);

      if (userForCompany == null) {
        ajax.AddDialogMessage("Please choose an existing user");
        return;
      } else if (companyId != null && !CompanyList.Exists(x => x.CompanyId == companyId)) {
        ajax.AddDialogMessage("Please choose a valid company.");
        return;
      }

      bool updated = DbHelper.AbleUser.UpdateClientCompanyId(null, userForCompany, companyId);

      if (updated) {
        ajax.SetReloadPage("Client Company updated.");
      } else {
        ajax.AddDialogMessage("Client company could not be updated.");
      }
    }
  }
}
