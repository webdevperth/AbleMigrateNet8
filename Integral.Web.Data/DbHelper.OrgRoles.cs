using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class OrgRoles {

      private const string TblPfx = "o";

      // Get all Organisation Roles
      public static List<OrgRolesInfo> GetOrgRolesList() {
        return GetOrgRolesInfoList();
      }

      private static List<OrgRolesInfo> GetOrgRolesInfoList() {

        var orgRolesList = new List<OrgRolesInfo>();

        Common.Query($@"
          SELECT
            {TblPfx}.OrgRoleId, {TblPfx}.RoleTitle, {TblPfx}.RoleDescription
          FROM sv_OrgRole {TblPfx}
          ORDER BY {TblPfx}.RoleTitle",
          dr => {
            var roleInfo = new OrgRolesInfo(
              dr.GetInt("OrgRoleId"),
              dr.GetString("RoleTitle"),
              dr.GetString("RoleDescription")
            );
            orgRolesList.Add(roleInfo);
          }
        );
        return orgRolesList;
      }

      public class OrgRolesInfo {

        public int OrgRoleId { get; internal set; }
        public string RoleTitle { get; private set; }
        public string RoleDescription { get; private set; }

        public OrgRolesInfo() { }

        public OrgRolesInfo(
          int orgRole,
          string roleTitle,
          string roleDescription
        ) {
          this.OrgRoleId = orgRole;
          this.RoleTitle = roleTitle;
          this.RoleDescription = roleDescription;
        }
      }

    }
  }
}

