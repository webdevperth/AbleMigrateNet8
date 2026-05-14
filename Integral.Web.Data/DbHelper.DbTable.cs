using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public static class DbTable {

      // public static TableInfo PortalUsers;
      public static TableInfo AnswerTypes;
      public static TableInfo Codes;
      public static TableInfo Dimensions;
      public static TableInfo Pages;
      public static TableInfo Participants;
      public static TableInfo Questions;
      public static TableInfo TextAnswers;
      public static TableInfo Answers;
      public static TableInfo AnswersMulti;
      // public static TableInfo Conditionals;
      public static TableInfo GblAnswerTypes;
      public static TableInfo GblCodes;
      // public static TableInfo GblDimensions;
      // public static TableInfo GblQnGrp_RptTypes;
      public static TableInfo GblQuestionGroup;
      public static TableInfo GblQuestions;
      public static TableInfo PortalAccounts;
      // public static TableInfo GblIdentity;
      // public static TableInfo ReportInvoice;
      public static TableInfo ReportType;
      public static TableInfo Sector;
      public static TableInfo Surveys;
      public static TableInfo SurveyCategory;
      public static TableInfo PortalAccountClients;
      public static TableInfo SurveyTypes;
      public static TableInfo User;

      static DbTable() {
        // PortalUsers = new TableInfo("pt_Users");
        AnswerTypes = new TableInfo("sv_360_AnswerTypes");
        Codes = new TableInfo("sv_360_Codes");
        Dimensions = new TableInfo("sv_360_Dimensions");
        Pages = new TableInfo("sv_360_Pages");
        Participants = new TableInfo("sv_360_Participants");
        Questions = new TableInfo("sv_360_Questions");
        TextAnswers = new TableInfo("sv_360_TextAnswers");
        Answers = new TableInfo("sv_Answers");
        AnswersMulti = new TableInfo("sv_AnswersMulti");
        // Conditionals = new TableInfo("sv_Conditionals");
        GblAnswerTypes = new TableInfo("sv_GblAnswerTypes");
        GblCodes = new TableInfo("sv_GblCodes");
        // GblDimensions = new TableInfo("sv_GblDimensions");
        // GblQnGrp_RptTypes = new TableInfo("sv_GblQnGrp_RptTypes");
        GblQuestionGroup = new TableInfo("sv_GblQuestionGroup");
        GblQuestions = new TableInfo("sv_GblQuestions");
        PortalAccounts = new TableInfo("sv_Organisation");
        // GblIdentity = new TableInfo("sv_Person");
        // ReportInvoice = new TableInfo("sv_ReportInvoice");
        ReportType = new TableInfo("sv_ReportType");
        Sector = new TableInfo("sv_Sector");
        Surveys = new TableInfo("sv_Survey");
        SurveyCategory = new TableInfo("sv_SurveyCategory");
        PortalAccountClients = new TableInfo("sv_SurveyCompany");
        SurveyTypes = new TableInfo("sv_SurveyTypes");
        User = new TableInfo("sv_User");
      }
      
      public class TableInfo {

        string tableName;
        string columnNamesWithoutIdentity = ""; // Column names except the identity column.
        string identityColumnName = ""; // Just the identity column name.

        public TableInfo(string TableName) { 
          this.tableName = TableName;
          CreateColumnCache();
        }

        public string IdentityColumn { get { return identityColumnName; } }
        public string TableName { get { return this.tableName; } }

        public override string ToString() { return this.tableName; }  // Default is return table name.

        private void CreateColumnCache() {
          // Get list of column names for this table.
          using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
            using (var cmd = new SqlCommand("SELECT c.name, c.is_identity FROM sys.columns c WHERE c.object_id = OBJECT_ID(@TableName)", conn)) {
              cmd.Parameters.Add("@TableName", SqlDbType.VarChar, 100).Value = tableName;
              conn.Open();
              try {
                using (SqlDataReader dr = cmd.ExecuteReader()) {
                  if (!dr.HasRows) throw new ArgumentException("Table name '" + tableName + "' not found");
                  while (dr.Read()) {
                    if (dr.GetBoolFromBit("is_identity")) {
                      identityColumnName = dr.GetString("name");
                    } else {
                      if (columnNamesWithoutIdentity.Length > 0) columnNamesWithoutIdentity += ",";
                      columnNamesWithoutIdentity += dr.GetString("name");
                    }
                  }
                } // dr
              } catch (Exception ex) {
                var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
                telemetry?.Exception(ex)
                  .WithOperation(nameof(CreateColumnCache))
                  .WithOperationContext("DbTable")
                  .WithProperty(DalApplicationInsightsConstants.TableName, tableName)
                  .Track();

                LogHelper.ResponseLog("Error creating column cache for table '" + tableName + "' : " + ex.Message);
                throw ex;
              } // try
            } // cmd
          } // conn
        }

        public string GetListOfColumnNames(bool IncludeIdentityColumn = false, string ExcludeColumns = "", string Prefix = "") {
          string columnNames = columnNamesWithoutIdentity;
          if (IncludeIdentityColumn && !identityColumnName.IsNullOrEmpty()) { // Add identity column.
            if (columnNames.Length > 0) columnNames = columnNames.Insert(0, ",");
            columnNames = columnNames.Insert(0, identityColumnName);
          }
          if (!ExcludeColumns.IsNullOrEmpty()) {
            // Remove column names from string.
            columnNames = Regex.Replace(columnNames + ",", "(" + ExcludeColumns.Replace(",", "|") + "),", "", RegexOptions.IgnoreCase);
          }
          if (!Prefix.IsNullOrEmpty() && columnNames.Length > 0) {
            columnNames = Regex.Replace(columnNames, @"\b(?=\w)", Prefix + "."); // e.g. if Prefix = "p" add "p." before each column name.
          }
          return columnNames;
        }
      }

    } // DbTable
  } // DbHelper
} // NS
