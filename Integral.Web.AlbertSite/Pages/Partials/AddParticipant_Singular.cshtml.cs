using System;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public class AddParticipant_Singular : AppCode.PageBaseClasses.LoggedInPageModel {

    public DbHelper.ClientCompanies.AlbertCompanyInfo CompanyInfo { get; set; }
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo { get; set; }

    private WebHelper.AddParticipantFrom addParticipantFrom;
    private bool CanAddParticipant;

    public class FormFields {
      public const string CoacheeId = "CoacheeId";
      public const string CompanyId = "CompanyId";
      public const string ProgramId = "ProgramId";
      public const string ProgramJobNumber = "ProgramJobNumber";
      public const string ProgramName = "ProgramName";
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string EmailAddress = "EmailAddress";
      public const string MobilePhone = "MobilePhone";
      public const string CompanyName = "CompanyName";
      public const string AddParticipantAfterCurrent = "AddParticipantAfterCurrent";
    }

    public class AjaxAction {
      public const string UpdateProfile = "UpdateProfile";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Add Participant";

      ProgramInfo = null;
      CompanyInfo = null;
      CoacheeInfo = new DbHelper.AlbertCoachees.AlbertCoacheeInfo();

      int? addToProgramId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.ProgramJobId, null);
      int? addToCompanyId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.CompanyId, null);

      if (addToProgramId != null) {
        ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull((int)addToProgramId);
        if (ProgramInfo != null) {
          CanAddParticipant = SessionHelper.AppAccess.Programs.CanAddProgramParticipant(ProgramInfo);
          addParticipantFrom = WebHelper.AddParticipantFrom.Program;
        }
      } else if (addToCompanyId != null) {
        GetCompanyInfoById((int)addToCompanyId);
        if (CompanyInfo != null) {
          CanAddParticipant = SessionHelper.AppAccess.Companies.CanAddCompanyParticipants(CompanyInfo);
          addParticipantFrom = WebHelper.AddParticipantFrom.Company;
        }
      } else {
        // Catching null ProgramInfo and CompanyInfo without redirecting. This is a safety net for the page.
        // The user will not be able to add a participant without a valid Program or Company.
        addParticipantFrom = WebHelper.AddParticipantFrom.Invalid;
      }

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (PageAjaxAction == AjaxAction.UpdateProfile) {
            if (!CanAddParticipant) {
              ajax.AddDialogMessage("Update not allowed.");
              return;
            }
            if (addParticipantFrom == WebHelper.AddParticipantFrom.Program) {
              AddParticipantToProgram(ajax);
              return;
            } else if (addParticipantFrom == WebHelper.AddParticipantFrom.Company) {
              AddParticipantToCompany(ajax);
              return;
            }
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    private void GetCompanyInfoById(int companyId) {
      CompanyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(companyId, SessionHelper.GetUserInfoOrNull());
    }

    private void AssignCoacheeInfoValues(AjaxSubmitHelper ajax) {
      CoacheeInfo.FirstName = ajax.CheckFieldRegex(FormFields.FirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please enter a First Name.");
      CoacheeInfo.LastName = ajax.CheckFieldRegex(FormFields.LastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please enter a Last Name.");
      CoacheeInfo.EmailAddress = ajax.CheckFieldRegex(FormFields.EmailAddress, "Email Address", AppHelper.Regex.Email, true, "Please enter a valid Email Address.");
      CoacheeInfo.MobilePhone = ajax.CheckFieldRegex(FormFields.MobilePhone, "Mobile Phone", AppHelper.Regex.Mobile, false, "Please enter a valid mobile number.");

      if (ajax.BadFieldCount > 0) return;

      if (addParticipantFrom == WebHelper.AddParticipantFrom.Program) {
        CoacheeInfo.ProgramJobId = ProgramInfo.ProgramJobId;
        CoacheeInfo.CompanyId = ProgramInfo.CompanyId;
        GetCompanyInfoById((int)ProgramInfo.CompanyId);
        if (CompanyInfo == null) return;
      } else if (addParticipantFrom == WebHelper.AddParticipantFrom.Company) {
        CoacheeInfo.CompanyId = CompanyInfo.CompanyId;
      }

      CoacheeInfo.TenantOrgId = CompanyInfo.OrgId;
      // Default Program Status setting for new coachees.
      CoacheeInfo.ProgramStatusId = DbHelper.CoacheeProgramStatus.GetStatus_WaitingLaunch().ProgramStatusId;
      // Default coach = "unassigned".
      CoacheeInfo.CoachUserId = ConfigHelper.UserId.Unassigned;
      // Default subscription flag.
      CoacheeInfo.SubscriptionUser = true;
    }

    public void AddParticipantToProgram(AjaxSubmitHelper ajax) {

      AssignCoacheeInfoValues(ajax);

      if (ajax.BadFieldCount > 0) return;

      // Check if a participant with that email already exists in the program.
      var coacheeInProgram = DbHelper.AlbertCoachees.GetCoacheesByEmail(CoacheeInfo.EmailAddress);
      if (coacheeInProgram.Exists(x => x.ProgramJobId == ProgramInfo.ProgramJobId)) {
        ajax.AddDialogMessage("A participant with this email already exists in the program.");
        return;
      }

      var existingCoacheeWasUndeleted = DbHelper.AlbertCoachees.UndeleteCoachee(null, CoacheeInfo.EmailAddress, ProgramInfo.ProgramJobId);
      if (!existingCoacheeWasUndeleted) {
        Exception createError = null;
        DbHelper.Common.UsingTransaction(trans => {
          try {
            // Create new participant.
            CoacheeInfo.CoacheeId = DbHelper.AlbertCoachees.CreateCoachee(trans, CoacheeInfo);

          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("AddParticipantToProgram_CreateCoachee")
              .FromSession()
              .WithPageUrl(SystemWeb.RequestRawUrl)
              .WithProperty(ApplicationInsightsConstants.ProgramJobId, ProgramInfo?.ProgramJobId)
              .WithProperty(ApplicationInsightsConstants.ProgramJobNumber, ProgramInfo?.ProgramJobNumber)
              .WithProperty(ApplicationInsightsConstants.CompanyId, CompanyInfo?.CompanyId)
              .WithProperty(ApplicationInsightsConstants.CoacheeEmail, CoacheeInfo?.EmailAddress)
              .Track();

            createError = ex;
            return false; // Rollback trans.
          }
          return true; // Commit trans.
        });
        if (createError != null) {
          ajax.AddDialogMessage("Error creating new Able Participant: " + createError.Message);
          return;
        }

        // Reload CoacheeInfo to get the UserGuid which is needed for Intercom events
        CoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(CoacheeInfo.CoacheeId);
      }
      // Add default subscription. It will check if there's an on-going sub first.
      DbHelper.Subscriptions.User.CreateDefaultSubscriptionForCoachee(CoacheeInfo, false);

      // Send Intercom event for participant creation
      var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo.UserGuid);
      if (participantExternalId.HasValue) {
        SendEvent(
          intercom => intercom.ParticipantCreated()
            .FromSession()
            .WithParticipant(participantExternalId.Value, CoacheeInfo.EmailAddress)
            .WithProgram(ProgramInfo?.ProgramJobId, ProgramInfo?.ProgramJobName)
            .WithCompany(CoacheeInfo.CompanyId, CompanyInfo?.CompanyName)
            .WithParticipantName(CoacheeInfo.GetFullName()),
          operationName: "AddParticipantSingular_ParticipantCreated",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new System.Collections.Generic.Dictionary<string, object> {
            ["CoacheeId"] = CoacheeInfo.CoacheeId,
            ["ProgramJobId"] = ProgramInfo?.ProgramJobId,
            ["CompanyId"] = CompanyInfo?.CompanyId
          }
        );

        // Send Intercom event for subscription assignment
        if (CoacheeInfo.HasSubscription) {
          SendEvent(
            intercom => {
              var builder = intercom.SubscriptionAssigned()
                .FromSession()
                .WithParticipant(participantExternalId.Value, CoacheeInfo.EmailAddress)
                .WithOrganisation(CoacheeInfo.TenantOrgId, CoacheeInfo.OrgName);

              if (CoacheeInfo.ProgramJobId.HasValue) {
                builder.WithProject(CoacheeInfo.ProgramJobId.Value, ProgramInfo?.ProgramJobName);
              }

              if (CoacheeInfo.UserSubscription != null) {
                builder.WithSubscriptionDetails(
                  subscriptionType: CoacheeInfo.UserSubscription.SubscriptionName ?? "Foundation Free",
                  unitPrice: 0 // Default subscription is free
                );
              }
              return builder;
            },
            operationName: "AddParticipantSingular_SubscriptionAssigned",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new System.Collections.Generic.Dictionary<string, object> {
              ["CoacheeId"] = CoacheeInfo.CoacheeId,
              ["OrgId"] = CoacheeInfo.TenantOrgId,
              ["SubscriptionName"] = CoacheeInfo.UserSubscription?.SubscriptionName
            }
          );
        }
      }

      FinishAddingTrans(ajax);
      return;
    }

    public void AddParticipantToCompany(AjaxSubmitHelper ajax) {

      AssignCoacheeInfoValues(ajax);

      // Check if user with this email exist.
      var emailUserInfo = DbHelper.AbleUser.GetUserByEmailOrNull(CoacheeInfo.EmailAddress, DbHelper.AbleUser.RegisteredFilter.Any);
      if (emailUserInfo != null) {
        ajax.AddDialogMessage("The email is already registered.");
        return;
      }

      int? userId = null;
      Exception createError = null;
      DbHelper.Common.UsingTransaction(trans => {
        try {
          // Create new participant for Company.
          // Create only user as Participant, not in al_Coachee as there's no program to assign to.
          userId = DbHelper.AbleUser.CreateUserFromCoachee(null, CoacheeInfo);

        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("AddParticipantToCompany_CreateUser")
            .FromSession()
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .WithProperty(ApplicationInsightsConstants.CompanyId, CompanyInfo?.CompanyId)
            .WithProperty(ApplicationInsightsConstants.CoacheeEmail, CoacheeInfo?.EmailAddress)
            .WithProperty(ApplicationInsightsConstants.CoacheeFirstName, CoacheeInfo?.FirstName)
            .WithProperty(ApplicationInsightsConstants.CoacheeLastName, CoacheeInfo?.LastName)
            .Track();

          createError = ex;
          return false; // Rollback trans.
        }
        return true; // Commit trans.
      });
      if (createError != null) {
        ajax.AddDialogMessage("Error creating new Able Participant: " + createError.Message);
        return;
      }

      if (userId != null) {
        // Reload user info to get the UserGuid which is needed for Intercom events
        var userInfo = DbHelper.AbleUser.GetBasicInfoById(userId.Value, DbHelper.AbleUser.RegisteredFilter.Any);

        // Send Intercom event for participant creation
        var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(userInfo?.UserGuid);
        if (participantExternalId.HasValue) {
          SendEvent(
            intercom => intercom.ParticipantCreated()
              .FromSession()
              .WithParticipant(participantExternalId.Value, CoacheeInfo.EmailAddress)
              .WithCompany(CoacheeInfo.CompanyId, CompanyInfo?.CompanyName)
              .WithParticipantName(CoacheeInfo.GetFullName()),
            operationName: "AddParticipantSingular_ParticipantCreatedCompany",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new System.Collections.Generic.Dictionary<string, object> {
              ["CoacheeId"] = CoacheeInfo.CoacheeId,
              ["CompanyId"] = CompanyInfo?.CompanyId,
              ["UserId"] = userId
            }
          );
        }

        FinishAddingTrans(ajax);
        return;
      }
    }

    private void FinishAddingTrans(AjaxSubmitHelper ajax) {

      bool addParticipantAfterCurrent = WebHelper.GetFormValue(FormFields.AddParticipantAfterCurrent).ToBooleanOrDefault(false);

      if (addParticipantAfterCurrent) {
        ajax.AddSuccessToast("Participant successfully added,<br/>ready to create another one.");
      } else {
        if (addParticipantFrom == WebHelper.AddParticipantFrom.Program) {
          ajax.SetRedirectUrl(PathHelper.Pages.ProgramParticipants(ProgramInfo.ProgramJobId), "Participant added to program.", AjaxSubmitHelper.PageMessageType.SuccessToast);
        } else {
          ajax.SetRedirectUrl(PathHelper.Pages.OrganisationPeople(CompanyInfo.CompanyId), "Participant added to organisation.", AjaxSubmitHelper.PageMessageType.SuccessToast);
        }
      }
    }

    public string GetAssignationInfo() {
      string html = "";
      if (ProgramInfo != null) {
        html += WebHelper.GetTextDisplayRow("Company:", 5, ProgramInfo.CompanyName);
        html += WebHelper.GetTextDisplayRow("Program:", 5, ProgramInfo.ProgramJobNumber + ": " + ProgramInfo.ProgramJobName);

      } else if (CompanyInfo != null) {
        html += WebHelper.GetTextDisplayRow("Company:", 5, CompanyInfo.CompanyName);
      }
      return html.EnsureEndsWith("<hr />", StringExt.Ensure.IfNotBlank);
    }
  }
}
