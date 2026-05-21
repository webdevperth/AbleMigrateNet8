using Integral.Web.Services;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ProjectAccess : AppCode.PageBaseClasses.ProjectPageBase {

    public const string UserSearchQueryKey = "UserSearch";
    public bool isReadOnly = false;
    public List<DbHelper.AblePrograms.ProjectProgramsListItem> ProjectPrograms;
    public List<DbHelper.ProjectUserAccess.ProjectAccessInfo> ProgramAccessUsers;

    public class FormFields {
      public const string AccessUserId = "AccessUserId";
      public const string GrantAccessJobId = "GrantAccessJobId";
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string EmailAddress = "EmailAddress";
      public const string RoleTitle = "RoleTitle";
      public const string MobileNumber = "MobileNumber";
    }
    public class FormValues {
      public string FirstName;
      public string LastName;
      public string EmailAddress;
      public string RoleTitle;
      public string MobileNumber;
    }
    public class AjaxAction {
      public const string AddUser = "AddUser";
      public const string UpdateUser = "UpdateUser";
    }
    public class ReturnValues {
      public const string RemoveUserId = "RemoveUserId";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Project Access";

      isReadOnly = !SessionHelper.AppAccess.Projects.CanEditProjectAccess(ProjectInfo);

      var userSearchQuery = WebHelper.GetQueryStringValue(UserSearchQueryKey);
      if (!userSearchQuery.IsNullOrEmpty()) {
        WebHelper.WriteAndEnd(GetUserSearchResultJson(userSearchQuery), WebHelper.HttpContentType.json);
        return new EmptyResult();
      }

      ProjectPrograms = DbHelper.AblePrograms.GetProjectProgramsList(ProjectInfo.JobNumber);
      ProgramAccessUsers = DbHelper.ProjectUserAccess.GetProjectAccessUsers(ProjectInfo.JobNumber);

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (isReadOnly) {
            ajax.RespondNoAccessToFunction();
            return;
          }

          if (ajax.Action == AjaxAction.AddUser) {
            AddUser(ajax);
          } else if (ajax.Action == AjaxAction.UpdateUser) {
            UpdateUser(ajax);
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    string GetUserSearchResultJson(string searchFor) {

      var sb = new StringBuilder();
      int rowCount = 0;

      var usersFound = DbHelper.AbleUser.SearchByNameOrEmail(
        searchFor,
        SessionHelper.AppAccess.Projects.GetProjectAccessUserSearchFilter(),
        SessionHelper.UserInfo,
        DbHelper.AbleUser.RegisteredFilter.Any);

      if (usersFound != null) {
        foreach (var user in usersFound) {
          if (user.UserId == ConfigHelper.UserId.Unassigned) continue;
          rowCount++;
          if (rowCount > 1) sb.Append(",");
          sb.Append("{ \"id\": ");
          sb.Append(user.UserId.ToString());
          sb.Append(", \"text\": ");
          sb.Append((user.FirstName + " " + user.LastName + " (" + user.EmailAddress + ")").JSONEncode(true));
          sb.Append("}");
        }
      }

      // Insert the "add new user" option at the top.
      if (rowCount == 0) {
        sb.Insert(0, "{ \"id\":\"" + PathHelper.AbleUrlValues.IdNew + "\", \"text\":\"No matches found. Add new Contact...\" }");
      } else {
        sb.Insert(0, "{ \"id\":\"" + PathHelper.AbleUrlValues.IdNew + "\", \"text\":\"Add new Contact\" },");
      }

      sb.Insert(0, "{ \"results\": [ ");
      sb.Append("],\"pagination\": { \"more\": false } }");
      return sb.ToString();
    }

    void AddUser(AjaxSubmitHelper ajax) {

      bool isNewUser = WebHelper.GetFormValue(FormFields.AccessUserId) == PathHelper.AbleUrlValues.IdNew;
      int addUserId = 0;
      DbHelper.AbleUser.AbleUserBasicInfo inviteeUserInfo = null;

      // A client is always 'forced' to enter their team data from scratch so they don't see other users.
      // Make sure the email they enter doesn't exist before adding a new user.
      if (SessionHelper.IsUserRoleClient) {
        var email = WebHelper.GetFormValue(FormFields.EmailAddress);
        if (email != null) {
          inviteeUserInfo = DbHelper.AbleUser.GetUserByEmailOrNull(email, DbHelper.AbleUser.RegisteredFilter.Any);
          if (inviteeUserInfo != null) {
            isNewUser = false;
            addUserId = inviteeUserInfo.UserId;
          } else {
            isNewUser = true;
            inviteeUserInfo = null;
          }
        }
      }

      if (!isNewUser) {

        if (!SessionHelper.IsUserRoleClient) addUserId = ajax.CheckFieldID(FormFields.AccessUserId, "User", true, "");

        if (addUserId == 0) {
          ajax.AddBadField(FormFields.AccessUserId, "Problem finding user - please refresh page and try again.");
          return;
        }

        if (ProgramAccessUsers.Exists(i => i.UserId == addUserId)) {
          ajax.AddBadField(FormFields.AccessUserId, "This user already has access - see the list above.");
          return;
        }

        var thisAccessUser = DbHelper.AbleUser.GetProjectAccessUserOrNull(addUserId);

        if (thisAccessUser == null) {
          ajax.AddBadField(FormFields.AccessUserId, "Selected User not found.");
          return;
        }
      }

      // Whether adding new user or not, make sure at least one program is selected.
      var selectedJobIds = ("" + WebHelper.GetFormValue(FormFields.GrantAccessJobId)).ToIntList();
      if (selectedJobIds.Count == 0) {
        ajax.AddDialogMessage("Please assign one or more Programs to this User.");
        return;
      }

      if (isNewUser) {
        // Create user and get new user ID.
        if (!CreateUser(ajax, out inviteeUserInfo)) return;
      } else {
        // Check user ID exists.
        inviteeUserInfo = inviteeUserInfo != null ? inviteeUserInfo : DbHelper.AbleUser.GetBasicInfoById(addUserId, DbHelper.AbleUser.RegisteredFilter.Any);
        if (inviteeUserInfo == null) {
          ajax.AddBadField(FormFields.AccessUserId, $"Can't add - user not found.");
          return;
        }
      }

      // Update user to Client flag.
      if (inviteeUserInfo != null && !inviteeUserInfo.IsAbleClient) {
        try {
          DbHelper.AbleUser.UpdateIsClient(null, inviteeUserInfo, true);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("ProjectAccess_UpdateIsClient")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .AddExternalUserId(ExternalUserKind.Client, ConfigHelper.UserRole.Client.ToExternalUserId(inviteeUserInfo?.UserGuid))
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .WithProperty("InviteeEmail", inviteeUserInfo?.EmailAddress)
            .WithProperty("ProjectJobNumber", ProjectInfo?.JobNumber)
            .Track();

          ajax.AddDialogMessage("Problem updating Contact.", ex);
          return;
        }
      }

      // Assign.
      bool successSetProgramAccess = true;
      DbHelper.Common.UsingTransaction(trans => {
        foreach (var program in ProjectPrograms) {
          try {
            if (selectedJobIds.Contains(program.ProgramJobId)) {
              DbHelper.ProjectUserAccess.SetProgramAccess(trans, ProjectInfo.ProjectId, program.ProgramJobId, inviteeUserInfo.UserId);
            }
          } catch (Exception ex) {
            ajax.AddDialogMessage("Error assigning User. Please try again later.", ex);
            successSetProgramAccess = false;
            break;
          }
        }
        return successSetProgramAccess;
      });

      Exception sendException = null;
      AlbertEmails.UserInviteResult sendInviteEmail = null;

      try {
        // Send invite email.
        sendInviteEmail = AlbertEmails.TrySendUserInvite(inviteeUserInfo, userInfo);

      } catch (Exception ex) {
        sendException = ex;
      }

      if (successSetProgramAccess && sendInviteEmail.IsSuccessful) {

        ajax.SetRedirectUrl(PathHelper.Pages.ProjectAccess_List(ProjectInfo.JobNumber), sendInviteEmail.Message);

      } else if (successSetProgramAccess) {
        ajax.SetReloadPage(); // Reload page to show new item.
      } else {
        ajax.AddDialogMessage("Something went wrong adding user to Project Access, try again.", sendException);
      }
    }

    bool CreateUser(AjaxSubmitHelper ajax, out DbHelper.AbleUser.AbleUserBasicInfo inviteeUserInfo) {

      inviteeUserInfo = null;

      var formValues = new FormValues();
      formValues.FirstName = ajax.CheckFieldRegex(FormFields.FirstName, "First Name", AppHelper.Regex.GeneralText, true, "Use only text characters in First Name.");
      formValues.LastName = ajax.CheckFieldRegex(FormFields.LastName, "Last Name", AppHelper.Regex.GeneralText, true, "Use only text characters in Last Name.");
      formValues.EmailAddress = ajax.CheckFieldRegex(FormFields.EmailAddress, "Email Address", AppHelper.Regex.Email, true, "Please provide a valid email address.");
      formValues.MobileNumber = ajax.CheckFieldRegex(FormFields.MobileNumber, "Mobile", AppHelper.Regex.GeneralText, false, "Use only text characters in Mobile Number.");
      formValues.RoleTitle = ajax.CheckFieldRegex(FormFields.RoleTitle, "Role", AppHelper.Regex.GeneralText, false, "Use only text characters in Role.");
      if (ajax.BadFieldCount > 0) return false;

      if (DbHelper.AbleUser.GetUserByEmailOrNull(formValues.EmailAddress, DbHelper.AbleUser.RegisteredFilter.Any) != null) {
        ajax.AddBadField(FormFields.EmailAddress, "That email address already exists - please try searching again.");
        return false;
      }

      var inviteeBasicInfo = new DbHelper.AbleUser.InviteeBasicInfo(
        formValues.FirstName, formValues.LastName, formValues.EmailAddress,
        DbHelper.AbleUser.UserRoleEnum.Client,
        userInfo.OrgId
      );

      try {
        inviteeUserInfo = DbHelper.AbleUser.CreateInviteeUser(null, userInfo, inviteeBasicInfo);
      } catch (Exception ex) {
        if (DbHelper.IsDuplicateKeyError(ex)) {
          ajax.AddDialogMessage($"The email address {inviteeBasicInfo.EmailAddress} is already in use.");
        } else {
          ajax.AddDialogMessage("Error creating user. Please try again later.");
        }
        return false;
      }

      // Send Intercom event for team member invitation
      // Capture values from out parameter before using in lambda
      var invitedExternalId = ConfigHelper.UserRole.Client.ToExternalUserId(inviteeUserInfo.UserGuid);
      var invitedFullName = inviteeUserInfo.GetFullName();
      var invitedEmail = inviteeUserInfo.EmailAddress;

      if (invitedExternalId.HasValue) {
        SendEvent(
          intercom => intercom.TeamMemberInvited()
            .FromSession()
            .WithInvitedUser(invitedExternalId.Value, invitedFullName, invitedEmail)
            .WithInvitedRole("client"),
          operationName: "ProjectAccess_TeamMemberInvited",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["InvitedUserEmail"] = invitedEmail,
            ["InvitedRole"] = "client",
            ["InvitedExternalId"] = invitedExternalId.Value,
            ["ProjectJobNumber"] = ProjectInfo?.JobNumber
          }
        );
      }

      return true;
    }

    public string GetProgramChkName(int userId) {
      return "userid_" + userId + "_programid";
    }

    void UpdateUser(AjaxSubmitHelper ajax) {

      int updateUserId = ajax.CheckFieldID(FormFields.AccessUserId, "", true, "");

      // A Client cannot update their own access.
      if (SessionHelper.IsUserRoleClient && updateUserId == userInfo.UserId) {
        ajax.AddDialogMessage("You do not have permission to update this user.");
        return;
      }

      // Check userid exists in the list.
      var user = ProgramAccessUsers.Find(i => i.UserId == updateUserId);
      if (user == null) {
        ajax.AddDialogMessage("Problem checking user - please reload page and try again.");
        return;
      }

      // Go thru form and get list of selected program job IDs.
      var selectedJobIds = ("" + WebHelper.GetFormValue(GetProgramChkName(user.UserId))).ToIntList();

      try {
        DbHelper.Common.UsingTransaction(trans => {
          foreach (var program in user.Programs) {
            if (!selectedJobIds.Contains(program.ProgramJobId) && program.HasAccess) {
              DbHelper.ProjectUserAccess.DeleteProgramAccess(trans, ProjectInfo.ProjectId, program.ProgramJobId, user.UserId);
            } else if (selectedJobIds.Contains(program.ProgramJobId) && !program.HasAccess) {
              DbHelper.ProjectUserAccess.SetProgramAccess(trans, ProjectInfo.ProjectId, program.ProgramJobId, user.UserId);
            }
          }
          return true;
        });
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("ProjectAccess_UpdateUserProgramAccess")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .WithProperty("UpdateUserId", updateUserId)
          .WithProperty("ProjectJobNumber", ProjectInfo?.JobNumber)
          .WithProperty("SelectedProgramCount", selectedJobIds?.Count)
          .Track();

        ajax.AddDialogMessage("Error updating User. Please try again later.", ex);
        return;
      }

      if (selectedJobIds.Count == 0) ajax.AddReturnValue("RemoveUserId", user.UserId);

      return;
    }
  }
}
