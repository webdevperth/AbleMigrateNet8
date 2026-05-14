using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class ClientCompanyDept {

      public static List<CompanyDeptInfo> GetCompanyDeptList(int companyId) {

        var result = new List<CompanyDeptInfo>();

        Common.Query(@"
          SELECT CompanyDeptId, CompanyId, CompanyDeptName
          FROM sv_SurveyCompanyDept
          WHERE CompanyId = @CompanyId
          ORDER BY CompanyDeptName",
          dr => {
            result.Add(new CompanyDeptInfo(
              dr.GetInt("CompanyDeptId"),
              dr.GetInt("CompanyId"),
              dr.GetString("CompanyDeptName")
            ));
          },
          Common.NewSqlParameter("CompanyId", companyId)
        );

        return result;
      }

      public static bool AddCompanyDept(int companyId, string departmentName) {

        return Common.GetNonQueryInt(@"
          INSERT INTO sv_SurveyCompanyDept (CompanyId, CompanyDeptName)
          VALUES (@CompanyId, @CompanyDeptName)",
          Common.NewSqlParameter("CompanyId", companyId),
          Common.NewSqlParameter("CompanyDeptName", departmentName, 500)
        ) == 1;
      }

      public static bool UpdateCompanyDept(int companyDeptId, string departmentName) {

        return Common.GetNonQueryInt(@"
          UPDATE sv_SurveyCompanyDept
          SET CompanyDeptName = @CompanyDeptName
          WHERE CompanyDeptId = @CompanyDeptId",
          Common.NewSqlParameter("CompanyDeptId", companyDeptId),
          Common.NewSqlParameter("CompanyDeptName", departmentName, 500)
        ) == 1;
      }

      public static bool DeleteCompanyDept(int companyDeptId) {

        return Common.GetNonQueryInt(@"
          DELETE FROM sv_SurveyCompanyDept
          WHERE CompanyDeptId = @CompanyDeptId",
          Common.NewSqlParameter("CompanyDeptId", companyDeptId)
        ) == 1;
      }

      public class CompanyDeptInfo {
        public int CompanyDeptId { get; private set; }
        public int CompanyId { get; private set; }
        public string CompanyDeptName { get; private set; }
        public CompanyDeptInfo(int companyDeptId, int companyId, string companyDeptName) {
          CompanyDeptId = companyDeptId;
          CompanyId = companyId;
          CompanyDeptName = companyDeptName;
        }
      }

    }
  }
}

