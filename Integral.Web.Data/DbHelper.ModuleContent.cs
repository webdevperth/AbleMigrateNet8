using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AlbertModuleContent {

      public static ModuleContentInfo GetModuleContentByIdOrNull(int moduleContentId) {

        var contentList = GetModuleContentList(1,
          "ModuleContentId = @ModuleContentId",
          null, null, null,
          Common.NewSqlParameter("@ModuleContentId", moduleContentId));

        if (contentList != null && contentList.ContentInfoList.Count > 0) {
          return contentList.ContentInfoList[0];
        }
        return null;
      }

      public static ModuleContentInfo GetModuleByTypeAndOrderOrNull(int moduleTypeId, int moduleOrder) {

        var contentList = GetModuleContentList(1,
          "ModuleTypeId = @ModuleTypeId AND ModuleOrder = @ModuleOrder",
          null, null, null,
          Common.NewSqlParameter("@ModuleTypeId", moduleTypeId),
          Common.NewSqlParameter("@ModuleOrder", moduleOrder));

        if (contentList != null && contentList.ContentInfoList.Count > 0) {
          return contentList.ContentInfoList[0];
        }
        return null;
      }

      private static ModuleContentList GetModuleContentList(
        int? topOrNullForAll,
        string sqlWhereConditions,
        string sqlOrderBy,
        int? offsetRows,
        int? fetchRows,
        params SqlParameter[] sqlWhereParams
      ) {

        var moduleContentList = new ModuleContentList();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);

        string sql = $@"
          SELECT {sqlTop}
            COUNT(*) OVER() AS TotalRows,
            mc.ModuleContentId, mc.ModuleId, mc.ModuleOrder, mc.ModuleTopic, mc.ModuleSubject, mc.ModuleBodyText, mc.ModuleLinkURL,
            m.ModuleName
          FROM al_ModuleContent mc
          INNER JOIN al_Modules m ON mc.ModuleId = m.ModuleId
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        if (sqlTop.IsNullOrEmpty() && !sqlOrderBy.IsNullOrEmpty() && offsetRows >= 0 && fetchRows > 0) {
          moduleContentList.OffsetRows = offsetRows;
          moduleContentList.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            if (sqlWhereParams != null) cmd.Parameters.AddRange(sqlWhereParams);
            conn.Open();
            using (var dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                if (moduleContentList.TotalRows == 0) moduleContentList.TotalRows = dr.GetInt("TotalRows");
                moduleContentList.ContentInfoList.Add(new ModuleContentInfo(
                  dr.GetInt("ModuleContentId"),
                  dr.GetInt("ModuleId"),
                  dr.GetString("ModuleName"),
                  dr.GetInt("ModuleOrder"),
                  dr.GetString("ModuleTopic"),
                  dr.GetString("ModuleSubject"),
                  dr.GetString("ModuleBodyText"),
                  dr.GetString("ModuleLinkURL")
                ));
              }
            }
          }
        }
        return moduleContentList;
      }

      public static void DeleteModuleContent(int moduleContentId) {

        Common.GetNonQueryInt(@"
          DELETE FROM al_ModuleContent
          WHERE ModuleContentId = @ModuleContentId",
          Common.NewSqlParameter("@ModuleContentId", moduleContentId));
      }

      public class ModuleContentList {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public List<ModuleContentInfo> ContentInfoList { get; internal set; }
        public ModuleContentList() {
          OffsetRows = null;
          FetchRows = null;
          TotalRows = 0;
          ContentInfoList = new List<ModuleContentInfo>();
        }
      }

      public class ModuleContentInfo {

        public int ModuleContentId { get; private set; }
        public int ModuleId { get; private set; }
        public string ModuleName { get; private set; }
        public int ModuleOrder { get; private set; }
        public string ModuleTopic { get; private set; }
        public string ModuleSubject { get; private set; }
        public string ModuleBodyText { get; private set; }
        public string ModuleLinkURL { get; private set; }

        public ModuleContentInfo(
          int moduleContentId,
          int moduleId,
          string moduleName,
          int moduleOrder,
          string moduleTopic,
          string moduleSubject,
          string moduleBodyText,
          string moduleLinkURL
        ) {
          this.ModuleContentId = moduleContentId;
          this.ModuleId = moduleId;
          this.ModuleName = moduleName;
          this.ModuleOrder = moduleOrder;
          this.ModuleTopic = moduleTopic;
          this.ModuleSubject = moduleSubject;
          this.ModuleBodyText = moduleBodyText;
          this.ModuleLinkURL = moduleLinkURL;
        }
      }

    }
  }
}

