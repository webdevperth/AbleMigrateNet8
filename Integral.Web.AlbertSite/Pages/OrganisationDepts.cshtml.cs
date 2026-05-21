using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class OrganisationDepts : AppCode.PageBaseClasses.CompanyInfoBase {

    public class AjaxAction {
      public const string GetDeptInfo = "GetDeptInfo";
      public const string UpdateDept = "UpdateDept";
      public const string DeleteDept = "DeleteDept";
    }

    public class FormFields {
      public const string DeptId = "DeptId";
      public const string DeptName = "DeptName";
    }

    public class FormValues {
      public int DeptId;
      public string DeptName;
    }

    public class ReturnValues {
      public const string DeptName = "DeptName";
    }

    public List<DbHelper.ClientCompanyDept.CompanyDeptInfo> DeptList;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      if (!SessionHelper.AppAccess.Companies.CanViewOrganisationDepartments(CompanyInfo)) {
        SetFallbackRedirectNoAccess();
        return new EmptyResult();
      }

      PageTitle = "Organisation Departments";
      DeptList = DbHelper.ClientCompanyDept.GetCompanyDeptList(CompanyInfo.CompanyId);

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          if (ajax.Action == AjaxAction.GetDeptInfo) {
            GetDeptInfo(ajax);
          } else if (ajax.Action == AjaxAction.UpdateDept) {
            UpdateDept(ajax);
          } else if (ajax.Action == AjaxAction.DeleteDept) {
            DeleteDept(ajax);
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    public string GetAddDepartnerButton() {
      return "<button id=\"btnAddDept\" class=\"btn btn-primary\">Add Department</button>";
    }

    void GetDeptInfo(AjaxSubmitHelper ajax) {

      int deptId = ajax.CheckFieldInt(FormFields.DeptId, true);
      var deptInfo = DeptList.Find(d => d.CompanyDeptId == deptId);
      if (deptInfo == null) {
        ajax.SetReloadPage(); // Form fiddled, reload page.
        return;
      }
      ajax.AddReturnValue(ReturnValues.DeptName, deptInfo.CompanyDeptName);

    }

    void UpdateDept(AjaxSubmitHelper ajax) {

      int deptId = ajax.CheckFieldInt(FormFields.DeptId, true);
      var deptInfo = DeptList.Find(d => d.CompanyDeptId == deptId);
      if (deptId > 0 && deptInfo == null) {
        ajax.SetReloadPage(); // Form fiddled, reload page.
        return;
      }

      string deptName = ajax.CheckFieldPlainText(FormFields.DeptName, "Dept Name", true);
      if (ajax.HasErrors) return;

      if (deptId == 0) {
        DbHelper.ClientCompanyDept.AddCompanyDept(CompanyInfo.CompanyId, deptName);
      } else {
        DbHelper.ClientCompanyDept.UpdateCompanyDept(deptId, deptName);
      }

      ajax.SetReloadPage(); // Form fiddled, reload page.
    }

    void DeleteDept(AjaxSubmitHelper ajax) {

      int deptId = ajax.CheckFieldInt(FormFields.DeptId, true);
      var deptInfo = DeptList.Find(d => d.CompanyDeptId == deptId);
      if (deptInfo == null) {
        ajax.SetReloadPage(); // Form fiddled, reload page.
        return;
      }

      try {
        DbHelper.ClientCompanyDept.DeleteCompanyDept(deptId);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("DeleteDept")
          .FromSession()
          .WithProperty("DeptId", deptId)
          .WithProperty("CompanyId", CompanyInfo.CompanyId)
          .WithProperty("DeptName", deptInfo.CompanyDeptName)
          .Track();
        ajax.AddDialogMessage("Unable to delete this Department.");
      }

      ajax.SetReloadPage(); // Form fiddled, reload page.
    }

  }
}
