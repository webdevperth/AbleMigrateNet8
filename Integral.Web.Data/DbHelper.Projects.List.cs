using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Projects {

      public class List {

        public List<ListItem> GetListForUser(DbHelper.AbleUser.AbleUserInfo user) {

          var sqlFilters = new List<string>();
          if (!user.IsAbleAdmin) {

            int? x = null;
            x ??= 5;

          }

          return null;
        }

        public class ListItem {
          int CompanyId;
        }
      }
    }
  }
}
