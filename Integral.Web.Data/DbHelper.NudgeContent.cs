using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AlbertNudgeContent {


      static AlbertNudgeContent() {


      }

      public static NudgeContentInfo GetNudgeContentByIdOrNull(int nudgeContentId) {
        var ncl = GetNudgeContentList(1,
          "NudgeContentId = @NudgeContentId",
          Common.NewSqlParameter("@NudgeContentId", nudgeContentId));
        if (ncl != null && ncl.ContentInfoList.Count > 0) return ncl.ContentInfoList[0];
        else return null;
      }

      public static NudgeContentInfo GetNudgeByTypeAndOrderOrNull(int nudgeTypeId, int nudgeOrder) {
        var ncl = GetNudgeContentList(1,
          "NudgeTypeId = @NudgeTypeId AND NudgeOrder = @NudgeOrder",
          Common.NewSqlParameter("@NudgeTypeId", nudgeTypeId),
          Common.NewSqlParameter("@NudgeOrder", nudgeOrder));
        if (ncl != null && ncl.ContentInfoList.Count > 0) return ncl.ContentInfoList[0];
        else return null;
      }

      public static void DeleteNudgeContent(int nudgeContentId) {
        Common.GetScalarQueryInt(
          "DELETE FROM al_NudgeContent WHERE NudgeContentId = @NudgeContentId",
          Common.NewSqlParameter("@NudgeContentId", nudgeContentId));
      }

      private static NudgeContentList GetNudgeContentList(
        int? topOrNullForAll,
        string sqlWhereConditions,
        string sqlOrderBy,
        int? offsetRows,
        int? fetchRows,
        params SqlParameter[] sqlWhereParams
      ) {

        var nudgeContentList = new NudgeContentList();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);

        string sql = $@"
          SELECT {sqlTop}
            COUNT(*) OVER() AS TotalRows,
            nc.NudgeContentId, nc.NudgeTypeId, nc.NudgeOrder, nc.NudgeTitle, nc.NudgeBodyText
          FROM al_NudgeContent nc
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        if (sqlTop.IsNullOrEmpty() && !sqlOrderBy.IsNullOrEmpty() && offsetRows >= 0 && fetchRows > 0) {
          nudgeContentList.OffsetRows = offsetRows;
          nudgeContentList.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            if (sqlWhereParams != null) cmd.Parameters.AddRange(sqlWhereParams);
            conn.Open();
            using (var dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                if (nudgeContentList.TotalRows == 0) nudgeContentList.TotalRows = dr.GetInt("TotalRows");
                nudgeContentList.ContentInfoList.Add(new NudgeContentInfo(
                  dr.GetInt("NudgeContentId"),
                  dr.GetInt("NudgeTypeId"),
                  dr.GetInt("NudgeOrder"),
                  dr.GetString("NudgeTitle"),
                  dr.GetString("NudgeBodyText")
                ));
              }
            }
          }
        }
        return nudgeContentList;
      }

      private static NudgeContentList GetNudgeContentList(
        int? topOrNullForAll,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams) {
        return GetNudgeContentList(topOrNullForAll, sqlWhereConditions, sqlOrderBy, null, null, sqlWhereParams);
      }

      private static NudgeContentList GetNudgeContentList(
        int? topOrNullForAll,
        string sqlWhereConditions,
        params SqlParameter[] sqlWhereParams) {
        return GetNudgeContentList(topOrNullForAll, sqlWhereConditions, null, null, null, sqlWhereParams);
      }

      public class NudgeContentList {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public List<NudgeContentInfo> ContentInfoList { get; internal set; }
        public NudgeContentList() {
          OffsetRows = null;
          FetchRows = null;
          TotalRows = 0;
          ContentInfoList = new List<NudgeContentInfo>();
        }
      }

      public class NudgeContentInfo {

        public int NudgeContentId { get; private set; }
        public int NudgeTypeId { get; private set; }
        public string NudgeName { get; private set; }
        public int NudgeOrder { get; private set; }
        public string NudgeTitle { get; private set; }
        public string NudgeBodyText { get; private set; }

        public NudgeContentInfo(
          int nudgeContentId,
          int nudgeTypeId,
          int nudgeOrder,
          string nudgeTitle,
          string nudgeBodyText
        ) {
          this.NudgeContentId = nudgeContentId;
          this.NudgeTypeId = nudgeTypeId;
          this.NudgeOrder = nudgeOrder;
          this.NudgeTitle = nudgeTitle;
          this.NudgeBodyText = nudgeBodyText;
        }
      }

    }
  }
}

