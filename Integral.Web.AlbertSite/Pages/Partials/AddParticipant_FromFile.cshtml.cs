using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public class AddParticipant_FromFile : AppCode.PageBaseClasses.LoggedInPageModel {

    public DbHelper.ClientCompanies.AlbertCompanyInfo CompanyInfo { get; set; }
    private WebHelper.AddParticipantFrom addParticipantFrom;
    private bool CanAddParticipant;

    public class FormFields {
      public const string FileHasHeader = "FileHasHeader";
      public const string ColumnIndexesArray = "columnIndexesArray";
      public const string FileInput = "FileInput";
    }
    public class AjaxAction {
      public const string StartAjaxSubmit = "StartAjaxSubmit";
      public const string AddParticipantsWithFile = "AddParticipantsWithFile";
    }

    public class AjaxReturnData {
      public const string ResultMsg = "ResultMsg";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Add Participant";

      ProgramInfo = null;
      CompanyInfo = null;

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

          if (PageAjaxAction == AjaxAction.AddParticipantsWithFile) {

            if (!CanAddParticipant) {
              ajax.AddReturnValue(AjaxReturnData.ResultMsg, "Operation not allowed");
              return;
            }
            AddParticipantWithFile(ajax);
            return;

          } else if (PageAjaxAction == AjaxAction.StartAjaxSubmit) {

            ajax.AddSuccessStatus();
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    public void AddParticipantWithFile(AjaxSubmitHelper ajax) {
      if (!SystemWeb.RequestFilesContains(FormFields.FileInput)) {
        ajax.AddReturnValue(AjaxReturnData.ResultMsg, "File not received");
        return;
      }

      var fileInput = SystemWeb.GetRequestFile(FormFields.FileInput);
      var fileHasHeader = ajax.CheckFieldBool(FormFields.FileHasHeader, "1");
      var orderedColumnsIndices = ajax.CheckFieldIntList(FormFields.ColumnIndexesArray);

      if (fileInput == null || fileInput.Length == 0 || orderedColumnsIndices == null) {
        ajax.AddReturnValue(AjaxReturnData.ResultMsg, "File is empty");
        return;
      }

      DataTable dataTable = new DataTable();
      bool isFirstLine = true;
      using (Stream stream = fileInput.OpenReadStream()) {
        if (fileInput.FileName.EndsWith(".xlsx") || fileInput.FileName.EndsWith(".xls")) {
          using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream)) {
            while (reader.Read()) {
              // Skip first line if file contains a header.
              if (isFirstLine && fileHasHeader) {
                isFirstLine = false;
                continue;
              }

              if (dataTable.Columns.Count == 0) {
                for (int i = 0; i < reader.FieldCount; i++) {
                  dataTable.Columns.Add();
                }
              }

              DataRow row = dataTable.NewRow();
              for (int i = 0; i < reader.FieldCount; i++) {
                row[i] = reader.GetValue(i);
              }
              dataTable.Rows.Add(row);
            }
          }
        } else if (fileInput.FileName.EndsWith(".csv")) {
          using (StreamReader csvReader = new StreamReader(stream)) {
            string line;

            while ((line = csvReader.ReadLine()) != null) {
              if (dataTable.Columns.Count == 0) {
                string[] headers = line.Split(',');
                foreach (var header in headers) {
                  dataTable.Columns.Add(header.Trim());
                }
              }
              // Skip first line if file contains a header.
              if (fileHasHeader && isFirstLine) {
                isFirstLine = false;
                continue;
              }

              string[] parts = line.Split(',');
              DataRow row = dataTable.NewRow();
              for (int i = 0; i < parts.Length; i++) {
                row[i] = parts[i].Trim();
              }
              dataTable.Rows.Add(row);
            }
          }
        } else {
          ajax.AddReturnValue(AjaxReturnData.ResultMsg, "Unsupported file format");
          return;
        }
      }

      var participantsInFile = new List<DbHelper.AlbertCoachees.AlbertCoacheeInfo>(); // List of participants found in the file.
      var participantsToAdd = new List<DbHelper.AlbertCoachees.AlbertCoacheeInfo>(); // List of participants to be added, once they were fully validated.
      var participantsNotAdded = new List<string>(); // List of participants that were not added because something went wrong.
      var existingParticipants = new List<string>(); // List of participants that were not added because they already exist in the program or company.
      var participantsAdded = new List<string>(); // List of participants that were added successfully.
      var participantsInvalidFormat = new List<string>(); // List of participants that were not added because they have invalid data/format.

      foreach (DataRow row in dataTable.Rows) {

        string firstName = ajax.DoValueValidation(row.ItemArray[orderedColumnsIndices[0]].ToString(), AppHelper.Regex.UserName);
        string lastName = ajax.DoValueValidation(row.ItemArray[orderedColumnsIndices[1]].ToString(), AppHelper.Regex.UserName);
        string email = ajax.DoValueValidation(row.ItemArray[orderedColumnsIndices[2]].ToString(), AppHelper.Regex.Email);
        string mobilePhone = "";
        if (orderedColumnsIndices.Count > 3) {
          mobilePhone = ajax.DoValueValidation(row.ItemArray[orderedColumnsIndices[3]].ToString(), AppHelper.Regex.Mobile);
        }

        // If values of current row are not in the correct format, skip and continue to the next one.
        if (ajax.BadFieldCount > 0 || firstName.IsNullOrEmpty() || lastName.IsNullOrEmpty()) {
          // Get the actual values from the file, so the user identifies what's wrong.
          participantsInvalidFormat.Add($"<b>Name:</b> {row.ItemArray[orderedColumnsIndices[0]]} {row.ItemArray[orderedColumnsIndices[1]]} <b>Email:</b> {row.ItemArray[orderedColumnsIndices[2]]} {(orderedColumnsIndices.Count < 4 ? "" : $" <b>Mobile:</b> {row.ItemArray[orderedColumnsIndices[0]]}")}");
          ajax.ClearBadFields(); // Clear bad fields so next iteration can start fresh and possibly ger registered.
          continue;
        }

        var thisCoachee = new DbHelper.AlbertCoachees.AlbertCoacheeInfo();
        thisCoachee.FirstName = firstName;
        thisCoachee.LastName = lastName;
        thisCoachee.EmailAddress = email;
        thisCoachee.MobilePhone = mobilePhone;

        participantsInFile.Add(thisCoachee);
      }

      if (participantsInFile.Count == 0) {
        if (participantsInvalidFormat.Count > 0) {
          ajax.AddReturnValue(AjaxReturnData.ResultMsg, "No participant can be added, please make sure the <b>column selection is in the right order</b> and that the fields are in the <b>correct format</b>.<br/><br/>- " + participantsInvalidFormat.Join("<br/>- "));
        } else {
          ajax.AddReturnValue(AjaxReturnData.ResultMsg, "The file doesn't contain any valid participants. Check it out and try again.");
        }

        return;
      }

      if (addParticipantFrom == WebHelper.AddParticipantFrom.Program) {
        // Get company info before iteration, no need to consult db each time.
        GetCompanyInfoById((int)ProgramInfo.CompanyId);
        if (CompanyInfo == null) return;

        // Check if any of the participants are already in the program.
        var coacheeInProgram = DbHelper.AlbertCoachees.GetCoacheesByProgram(ProgramInfo.ProgramJobId);
        foreach (var coacheeInfo in participantsInFile) {
          if (coacheeInProgram.Exists(x => x.EmailAddress == coacheeInfo.EmailAddress)) {
            existingParticipants.Add($"{coacheeInfo.FirstName} {coacheeInfo.LastName} ({coacheeInfo.EmailAddress})");
          } else {
            participantsToAdd.Add(coacheeInfo);
          }
        }
      } else if (addParticipantFrom == WebHelper.AddParticipantFrom.Company) {

        var orgParticipants = DbHelper.OrganisationUsers.GetOrganisationParticipants(CompanyInfo.CompanyId);
        var participantsNotInOrg = new List<DbHelper.AlbertCoachees.AlbertCoacheeInfo>();

        // Check if participants in the file exists in the organisation.
        foreach (var coacheeInfo in participantsInFile) {
          if (orgParticipants.OrganisationUserInfoList.Exists(x => x.Email == coacheeInfo.EmailAddress)) {
            existingParticipants.Add($"{coacheeInfo.FirstName} {coacheeInfo.LastName} ({coacheeInfo.EmailAddress})");
          } else {
            participantsNotInOrg.Add(coacheeInfo);
          }
        }

        // Check if participants that don't exist in the organisation, are in the database.
        foreach (var coacheeInfo in participantsNotInOrg) {
          var emailUserInfo = DbHelper.AbleUser.GetUserByEmailOrNull(coacheeInfo.EmailAddress, DbHelper.AbleUser.RegisteredFilter.Any);
          if (emailUserInfo == null) {
            participantsToAdd.Add(coacheeInfo);
          } else {
            existingParticipants.Add($"{coacheeInfo.FirstName} {coacheeInfo.LastName} ({coacheeInfo.EmailAddress})");
          }
        }
      }


      // Process only array participantsToAdd after validating all from File
      foreach (var coacheeInfo in participantsToAdd) {

        bool paxWasAdded = false;

        coacheeInfo.TenantOrgId = CompanyInfo.OrgId;
        coacheeInfo.CompanyId = CompanyInfo.CompanyId;
        coacheeInfo.ProgramStatusId = DbHelper.CoacheeProgramStatus.GetStatus_WaitingLaunch().ProgramStatusId;
        coacheeInfo.CoachUserId = ConfigHelper.UserId.Unassigned;
        coacheeInfo.SubscriptionUser = true;

        if (addParticipantFrom == WebHelper.AddParticipantFrom.Program) {
          coacheeInfo.ProgramJobId = ProgramInfo.ProgramJobId;
          paxWasAdded = AddParticipantToProgram(coacheeInfo);
        } else if (addParticipantFrom == WebHelper.AddParticipantFrom.Company) {
          paxWasAdded = AddParticipantToCompany(coacheeInfo);
        }

        // If participant was added or not, add to the corresponding string array for display.
        if (paxWasAdded) {
          participantsAdded.Add($"{coacheeInfo.FirstName} {coacheeInfo.LastName} ({coacheeInfo.EmailAddress})");
        } else {
          participantsNotAdded.Add($"{coacheeInfo.FirstName} {coacheeInfo.LastName} ({coacheeInfo.EmailAddress})");
        }
      }

      // Craft display message with each activity.
      string msg = "";
      // If any participant was added
      if (participantsAdded.Count > 0) {
        if (participantsAdded.Count > 10) {
          msg += $"<b>{participantsAdded.Count}</b> participants were successfully added.";
        } else {
          msg += $"<b>Added participant{(participantsAdded.Count > 1 ? "s" : "")}:</b><br/>- ";
          msg += participantsAdded.Join("<br/>- ");
        }
      } else {
        msg += "No participants were added.<br/>";
      }

      // If any or all participants already exist.
      if (existingParticipants.Count > 0) {
        if (existingParticipants.Count == participantsInFile.Count) {
          msg += $"All participants already exist in the database.<br/>";
        } else {
          msg += "<br/><br/>";
          if (existingParticipants.Count > 5) {
            msg += $"<b>{existingParticipants.Count}</b> participants already exist in the database.";
          } else {
            msg += $"<b>These participant{(existingParticipants.Count > 1 ? "s" : "")} already exist in the database:</b><br/>- ";
            msg += existingParticipants.Join("<br/>- ");
          }
        }
      }

      // If participants were not added because something went wrong by doing it.
      if (participantsNotAdded.Count > 0) {
        msg += "<br/><br/>";
        if (participantsNotAdded.Count > 5) {
          msg += $"<b>{participantsNotAdded.Count}</b> participants couldn't be added.";
        } else {
          msg += $"<b>Participant{(participantsNotAdded.Count > 1 ? "s" : "")} could not be added:</b><br/>- ";
          msg += participantsNotAdded.Join("<br/>- ");
        }
      }

      // If participant are not in the right format. i.e. email or phone don't have the right format.
      if (participantsInvalidFormat.Count > 0) {
        msg += "<br/><br/>";
        if (participantsInvalidFormat.Count > 5) {
          msg += $"<b>{participantsInvalidFormat.Count}</b> were skipped for invalid format fields.";
        } else {
          msg += $"<b>Skipped participant{(participantsInvalidFormat.Count > 1 ? "s" : "")} for invalid fields:</b><br/>- ";
          msg += participantsInvalidFormat.Join("<br/>- ");
        }
        msg += "<br/>Please check the fields are in correct format and try again.";
      }

      if (participantsAdded.Count == 0) {
        ajax.AddReturnValue(AjaxReturnData.ResultMsg, msg);
        return;
      }

      if (addParticipantFrom == WebHelper.AddParticipantFrom.Program) {
        SetRedirect(PathHelper.Pages.ProgramParticipants(ProgramInfo.ProgramJobId), msg);
        return;
      } else {
        SetRedirect(PathHelper.Pages.OrganisationPeople(CompanyInfo.CompanyId), msg);
        return;
      }

    }

    public bool AddParticipantToProgram(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      // Check if the email of this Coachee is found in the program as deleted.
      // The function will undelete it and return.
      var existingCoacheeWasUndeleted = DbHelper.AlbertCoachees.UndeleteCoachee(null, coacheeInfo.EmailAddress, ProgramInfo.ProgramJobId);
      // Add default subscription. It will check if there's an on-going sub first.

      if (!existingCoacheeWasUndeleted) {
        Exception createError = null;
        DbHelper.Common.UsingTransaction(trans => {
          try {
            // Create new participant.
            coacheeInfo.CoacheeId = DbHelper.AlbertCoachees.CreateCoachee(trans, coacheeInfo);

          } catch (Exception ex) {
            createError = ex;
            return false; // Rollback trans.
          }
          return true; // Commit trans.
        });
        if (createError != null) return false;

        // Reload coacheeInfo to get the UserGuid which is needed for Intercom events
        coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(coacheeInfo.CoacheeId);
      }
      DbHelper.Subscriptions.User.CreateDefaultSubscriptionForCoachee(coacheeInfo, false);

      // Send Intercom event for participant creation (bulk import)
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
          operationName: "AddParticipantFromFile_ParticipantCreated",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["CoacheeId"] = coacheeInfo.CoacheeId,
            ["ProgramJobId"] = programInfo?.ProgramJobId,
            ["CompanyId"] = companyInfo?.CompanyId,
            ["ImportSource"] = "BulkFileImport"
          }
        );

        // Send Intercom event for subscription assignment (bulk import)
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
            operationName: "AddParticipantFromFile_SubscriptionAssigned",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["CoacheeId"] = coacheeInfo.CoacheeId,
              ["OrgId"] = coacheeInfo.TenantOrgId,
              ["SubscriptionName"] = coacheeInfo.UserSubscription?.SubscriptionName,
              ["ImportSource"] = "BulkFileImport"
            }
          );
        }
      }

      return true;
    }

    public bool AddParticipantToCompany(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      int? userId = null;
      Exception createError = null;
      DbHelper.Common.UsingTransaction(trans => {
        try {
          // Create new participant for Company.
          // Create only user as Participant, not in al_Coachee as there's no program to assign to.
          userId = DbHelper.AbleUser.CreateUserFromCoachee(null, coacheeInfo);

        } catch (Exception ex) {
          createError = ex;
          return false; // Rollback trans.
        }
        return true; // Commit trans.
      });
      if (createError != null) {
        return false;
      }

      if (userId != null) {
        // Reload user info to get the UserGuid which is needed for Intercom events
        var userInfo = DbHelper.AbleUser.GetBasicInfoById(userId.Value, DbHelper.AbleUser.RegisteredFilter.Any);

        // Send Intercom event for participant creation (bulk import - company level)
        var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(userInfo?.UserGuid);
        if (participantExternalId.HasValue) {
          var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(coacheeInfo.CompanyId ?? 0, SessionHelper.GetUserInfoOrNull());

          SendEvent(
            intercom => intercom.ParticipantCreated()
              .FromSession()
              .WithParticipant(participantExternalId.Value, coacheeInfo.EmailAddress)
              .WithCompany(coacheeInfo.CompanyId, companyInfo?.CompanyName)
              .WithParticipantName(coacheeInfo.GetFullName()),
            operationName: "AddParticipantFromFile_ParticipantCreatedCompany",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["CoacheeId"] = coacheeInfo.CoacheeId,
              ["CompanyId"] = companyInfo?.CompanyId,
              ["UserId"] = userId,
              ["ImportSource"] = "BulkFileImport"
            }
          );
        }
        return true;
      }

      return false;
    }

    private void GetCompanyInfoById(int companyId) {
      CompanyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(companyId, SessionHelper.GetUserInfoOrNull());
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
