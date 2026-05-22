using System;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Integral.Web.PortalSite.Endpoints {

  // Minimal-API ports of the legacy /api/*.aspx handlers. Both endpoints are
  // admin-only "create new" operations that respond via AjaxSubmitHelper.
  public static class ApiEndpoints {

    public static void MapEndpoints(IEndpointRouteBuilder app) {

      app.MapPost("/api/companies", () => {

        if (!AuthInitialization.RequireLoggedInUser()) return;

        AjaxSubmitHelper.Process(ajax => {

          if (!SessionHelper.IsUserRoleAdmin) {
            ajax.AddDialogMessage("Access denied.");
            return;
          }

          bool isNewCompany = WebHelper.GetFormValue("CompanyId") == "new";

          if (!isNewCompany) {
            ajax.AddDialogMessage("Can only create new companies for now.");
            return;
          }

          string companyName = ajax.CheckFieldRegex("CompanyName", "Company Name", AppHelper.Regex.GeneralText, isNewCompany, "Please provide a Company Name.").TrimWhitespace();

          if (ajax.BadFieldCount > 0) return;

          var userInfo = SessionHelper.GetUserInfoOrNull();

          DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo;
          int newCompanyId;
          try {
            companyInfo = DbHelper.ClientCompanies.CreateCompany(null, userInfo.OrgId, companyName);
            newCompanyId = companyInfo.CompanyId;
          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("API_Companies_CreateCompany")
              .WithProperty(ApplicationInsightsConstants.CompanyName, companyName)
              .WithProperty(ApplicationInsightsConstants.OrgId, userInfo.OrgId)
              .Track();
            if (ex.Message.Contains("duplicate key")) {
              ajax.AddDialogMessage("That company name already exists.");
            } else {
              ajax.AddDialogMessage("Error creating company: " + ex.Message);
            }
            return;
          }

          ajax.AddReturnValue("NewCompanyId", newCompanyId.ToString());
        });
      });

      app.MapPost("/api/programs", () => {

        if (!AuthInitialization.RequireLoggedInUser()) return;

        AjaxSubmitHelper.Process(ajax => {

          if (!SessionHelper.IsUserRoleAdmin) {
            ajax.AddDialogMessage("Access denied.");
            return;
          }

          bool isNewProgram = false;
          int formProgramJobId = 0;

          if (WebHelper.GetFormValue("ProgramJobId") == "new") {
            isNewProgram = true;
          } else {
            formProgramJobId = ajax.CheckFieldID("ProgramJobId", "Program", true, "ProgramJobId is required.");
          }

          if (!isNewProgram) {
            ajax.AddDialogMessage("Can only create new Programs for now.");
            return;
          }

          int companyId = ajax.CheckFieldID("CompanyId", "Company", isNewProgram, "Please provide the CompanyId for the Program");
          string programJobNumber = ajax.CheckFieldRegex("ProgramNumber", "Program Number", AppHelper.Regex.GeneralText, isNewProgram, "Please provide the Program Number.").TrimWhitespace();
          string programName = ajax.CheckFieldRegex("ProgramName", "Program Name", AppHelper.Regex.GeneralText, isNewProgram, "Please provide the Program Name.").TrimWhitespace();
          DateTime? programStartDateUtc = ajax.GetDatePickerToUtc("ProgramStartDate", SessionHelper.GetSessionTimeZone(), "Program Start Date", true, "Please provide Program start date.");
          DateTime? programEndDateUtc = ajax.GetDatePickerToUtc("ProgramEndDate", SessionHelper.GetSessionTimeZone(), "Program End Date", true, "Please provide Program end date.");
          string programNotes = ajax.CheckFieldRegex("ProgramNotes", "Program Notes", AppHelper.Regex.GeneralText.Replace("[", "[<>"), false, "").TrimWhitespace();

          if (ajax.BadFieldCount > 0) return;

          var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(companyId, SessionHelper.GetUserInfoOrNull());
          if (companyInfo == null) {
            ajax.AddBadField("CompanyId", "CompanyId does not exist.");
            return;
          }

          DbHelper.AblePrograms.AbleProgramInfo programInfo;
          int newProgramJobId;
          try {
            programInfo = DbHelper.AblePrograms.CreateProgram(null,
              companyId,
              programJobNumber, programName,
              programStartDateUtc, programEndDateUtc,
              programNotes);
            newProgramJobId = programInfo.ProgramJobId;
          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("API_Programs_CreateProgram")
              .WithProperty(ApplicationInsightsConstants.ProgramJobNumber, programJobNumber)
              .WithProperty(ApplicationInsightsConstants.CompanyId, companyId)
              .Track();
            if (ex.Message.Contains("duplicate key")) {
              ajax.AddDialogMessage("That Program Number already exists.");
            } else {
              ajax.AddDialogMessage("Error creating Program: " + ex.Message);
            }
            return;
          }

          ajax.AddReturnValue("NewProgramJobId", newProgramJobId.ToString());
        });
      });
    }
  }
}
