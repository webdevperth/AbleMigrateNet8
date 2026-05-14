using Integral.Web;
using System;
using System.Linq;
using System.Collections.Generic;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class AddParticipant_QuickAdd_Existing : AppCode.PageBaseClasses.LoggedInPageBase {

    public DbHelper.ClientCompanies.AlbertCompanyInfo CompanyInfo { get; set; }
    public List<DbHelper.OrganisationUsers.OrgParticipantBasicInfo> OrganisationParticipants { get; set; }

    public bool CanAddParticipant;

    public class FormFields {
      public const string UserId = "userid";
      public const string OrgPeople = "OrgPeople";
    }
    public class AjaxAction {
      public const string AddExistingParticipants = "AddExistingParticipants";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Add Participant";

      ProgramInfo = null;
      CompanyInfo = null;

      int? addToProgramId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.ProgramJobId, null);

      if (addToProgramId != null) {
        ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull((int)addToProgramId);
        if (ProgramInfo == null) {
          return;
        }
      } else {
        return;
      }

      CompanyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(ProgramInfo.CompanyId.Value, SessionHelper.GetUserInfoOrNull());
      if (CompanyInfo == null) return;

      // Get Participants in the company
      OrganisationParticipants = DbHelper.OrganisationUsers.GetOrgParticipantsBasicInfoNotInProgram(CompanyInfo.CompanyId, ProgramInfo.ProgramJobId);
      CanAddParticipant = SessionHelper.AppAccess.Programs.CanAddProgramParticipant(ProgramInfo) && !OrganisationParticipants.IsNullOrEmpty();

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (PageAjaxAction == AjaxAction.AddExistingParticipants) {
            if (!CanAddParticipant) {
              ajax.AddDialogMessage("Operation not allowed.");
              return;
            }
            AddParticipantToProgram(ajax);
            return;
          }
        });
        WebHelper.EndRequest();
        return;
      }
    }

    public string GetOrgPeopleOptions() {
      if (OrganisationParticipants == null || !CanAddParticipant) return "<option value=\"\">[no participants found]</option>";
      string html = "<option value=\"\">[select participants to add]</option>";
      foreach (var p in OrganisationParticipants) {
        html += "<option value=\"" + p.UserId + "\">" + p.FullName + " (" + p.Email + ")</option>";
      }
      return html;
    }

    private void AddParticipantToProgram(AjaxSubmitHelper ajax) {
      //TODO CHECK USERS
      var selectedUserId = ajax.CheckFieldIntList(FormFields.UserId);
      if (selectedUserId.Count == 0) {
        ajax.AddDialogMessage("No participants selected.");
        return;
      }

      // Get user info of selected participants
      var selectedUsersList = OrganisationParticipants.Where(x => selectedUserId.Contains(x.UserId)).ToList();

      if (selectedUsersList.Count == 0) {
        ajax.AddDialogMessage("No participants selected.");
        return;
      }

      // Get info of activity to display at the end.
      int counterPaxAdded = 0;
      string summaryDisplay = string.Empty;

      foreach (var pax in selectedUsersList) {

        // Check if participant is already in the program in deleted status
        var existingCoacheeWasUndeleted = DbHelper.AlbertCoachees.UndeleteCoachee(null, pax.Email, ProgramInfo.ProgramJobId);
        if (existingCoacheeWasUndeleted) {
          // Add default subscription. It will check if there's an on-going sub first.
          var existingCoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(pax.Email, ProgramInfo.ProgramJobId);
          DbHelper.Subscriptions.User.CreateDefaultSubscriptionForCoachee(existingCoacheeInfo, false);

          // Send Intercom event for subscription assignment (undeleted participant)
          var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(existingCoacheeInfo.UserGuid);
          if (participantExternalId.HasValue && existingCoacheeInfo.HasSubscription) {

            var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(existingCoacheeInfo.CompanyId ?? 0, SessionHelper.UserInfo);
            var programInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(existingCoacheeInfo.ProgramJobId ?? 0, DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_AnyRelated, SessionHelper.UserInfo);

            SendEvent(
              intercom => {
                var builder = intercom.SubscriptionAssigned()
                  .FromSession()
                  .WithParticipant(participantExternalId.Value, existingCoacheeInfo.EmailAddress)
                  .WithOrganisation(existingCoacheeInfo.TenantOrgId, existingCoacheeInfo.OrgName);

                if (existingCoacheeInfo.ProgramJobId.HasValue) {
                  builder.WithProject(existingCoacheeInfo.ProgramJobId.Value, programInfo?.ProgramJobName);
                }

                if (existingCoacheeInfo.UserSubscription != null) {
                  builder.WithSubscriptionDetails(
                    subscriptionType: existingCoacheeInfo.UserSubscription.SubscriptionName ?? "Foundation Free",
                    unitPrice: 0 // Default subscription is free
                  );
                }
                return builder;
              },
              operationName: "AddParticipantQuickAdd_SubscriptionAssignedUndeleted",
              requestRawUrl: SystemWeb.RequestRawUrl,
              telemetryProperties: new Dictionary<string, object> {
                ["CoacheeId"] = existingCoacheeInfo.CoacheeId,
                ["OrgId"] = existingCoacheeInfo.TenantOrgId,
                ["ProgramJobId"] = existingCoacheeInfo.ProgramJobId,
                ["WasUndeleted"] = true
              }
            );
          }

          counterPaxAdded++;
          summaryDisplay += "<p>" + pax.FullName + " (" + pax.Email + ")</p>";
          continue;
        }

        // Create a new object in each iteration
        var coacheeInfo = new DbHelper.AlbertCoachees.AlbertCoacheeInfo();

        // Assign basic fields to create participant
        coacheeInfo.FirstName = pax.FirstName;
        coacheeInfo.LastName = pax.LastName;
        coacheeInfo.EmailAddress = pax.Email;
        coacheeInfo.MobilePhone = pax.MobilePhone;
        coacheeInfo.ProgramJobId = ProgramInfo.ProgramJobId;
        coacheeInfo.CompanyId = ProgramInfo.CompanyId;
        coacheeInfo.TenantOrgId = CompanyInfo.OrgId;
        // Default Program Status setting for new coachees.
        coacheeInfo.ProgramStatusId = DbHelper.CoacheeProgramStatus.GetStatus_WaitingLaunch().ProgramStatusId;
        // Default coach = "unassigned".
        coacheeInfo.CoachUserId = ConfigHelper.UserId.Unassigned;
        // Default subscription flag.
        coacheeInfo.SubscriptionUser = true;

        // Send object to create participant in program.
        bool paxCreated = CreateCoacheeInProgram(coacheeInfo);
        if (paxCreated) {
          counterPaxAdded++;
          summaryDisplay += "<p>" + pax.FullName + " (" + pax.Email + ")</p>";
        }
      }

      if (counterPaxAdded == 0) {
        ajax.AddDialogMessage("No participant could be added to the program, please try again");
        return;
      }

      string msg = $"{counterPaxAdded}/{selectedUsersList.Count} participant{(counterPaxAdded > 1 ? "s" : "")} successfully added to program:<br/><br/>{summaryDisplay}";
      ajax.SetRedirectUrl(PathHelper.Pages.ProgramParticipants(ProgramInfo.ProgramJobId), msg);
    }

    public bool CreateCoacheeInProgram(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      Exception createError = null;
      DbHelper.Common.UsingTransaction(trans => {
        try {
          // Create new participant.
          coacheeInfo.CoacheeId = DbHelper.AlbertCoachees.CreateCoachee(trans, coacheeInfo);
          // Add default subscription. It will check if there's an on-going sub first.
          DbHelper.Subscriptions.User.CreateDefaultSubscriptionForCoachee(coacheeInfo, false);
        } catch (Exception ex) {
          createError = ex;
          return false; // Rollback trans.
        }
        return true;
      });
      if (createError != null) {
        return false;
      }

      // Reload coacheeInfo to get the UserGuid which is needed for Intercom events
      coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(coacheeInfo.CoacheeId);

      // Send Intercom event for existing participant added to new program
      var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo.UserGuid);
      if (participantExternalId.HasValue) {

        var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(coacheeInfo.CompanyId ?? 0, SessionHelper.UserInfo);
        var programInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(coacheeInfo.ProgramJobId ?? 0, DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_AnyRelated, SessionHelper.UserInfo);

        SendEvent(
          intercom => intercom.ParticipantCreated()
            .FromSession()
            .WithParticipant(participantExternalId.Value, coacheeInfo.EmailAddress)
            .WithProgram(programInfo?.ProgramJobId, programInfo?.ProgramJobName)
            .WithCompany(coacheeInfo.CompanyId, companyInfo?.CompanyName)
            .WithParticipantName(coacheeInfo.GetFullName()),
          operationName: "AddParticipantQuickAdd_ParticipantCreated",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["CoacheeId"] = coacheeInfo.CoacheeId,
            ["ProgramJobId"] = programInfo?.ProgramJobId,
            ["CompanyId"] = companyInfo?.CompanyId,
            ["WasExisting"] = true
          }
        );

        // Send Intercom event for subscription assignment (new participant)
        if (coacheeInfo.HasSubscription) {
          SendEvent(
            intercom => {
              var builder = intercom.SubscriptionAssigned()
                .FromSession()
                .WithParticipant(participantExternalId.Value, coacheeInfo.EmailAddress)
                .WithOrganisation(coacheeInfo.TenantOrgId, coacheeInfo.OrgName);

              if (coacheeInfo.ProgramJobId.HasValue) {
                builder.WithProject(coacheeInfo.ProgramJobId.Value, programInfo?.ProgramJobName);
              }

              if (coacheeInfo.UserSubscription != null) {
                builder.WithSubscriptionDetails(
                  subscriptionType: coacheeInfo.UserSubscription.SubscriptionName ?? "Foundation Free",
                  unitPrice: 0 // Default subscription is free
                );
              }
              return builder;
            },
            operationName: "AddParticipantQuickAdd_SubscriptionAssigned",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["CoacheeId"] = coacheeInfo.CoacheeId,
              ["OrgId"] = coacheeInfo.TenantOrgId,
              ["SubscriptionName"] = coacheeInfo.UserSubscription?.SubscriptionName
            }
          );
        }
      }

      return true;
    }

    public string GetAssignationInfo() {
      string html = "";
      if (ProgramInfo != null) {
        html += WebHelper.GetTextDisplayRow("Company:", 8, ProgramInfo.CompanyName);
        html += WebHelper.GetTextDisplayRow("Program:", 8, ProgramInfo.ProgramJobNumber + ": " + ProgramInfo.ProgramJobName);
      }
      return html.EnsureEndsWith("<hr />", StringExt.Ensure.IfNotBlank);
    }
  }
}
