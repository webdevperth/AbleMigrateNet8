using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Projects {

      private const string TblPfx = "prj";
      private const string ForUserIdParam = "ForUserId";

      // Only do job auto-numbering between these two values, to avoid:
      // 1. Weird job numbers and gaps before J002500, and
      // 2. Prevent existing legacy job numbers over J009900 from messing with MAX() function.
      // When job numbers get to 9900 one day, just reset these values to resume autonumbering at higher numbers while avoiding existing legacy job numbers.
      private const int JobAutoNumberFrom = 2500; // Start job auto-numbering from here to avoid all the weird job numbers & gaps before that number.
      private const int JobAutoNumberTo = 9900; // Also avoid auto-numbering over this. Should take a very very long time to get there.

      internal const string DeliveryInProjectCTEName = "DeliveryInProjectCTE";
      internal const string IsDeliveryInProjectCol = "IsDeliveryInProject";

      public static ProjectInfo GetNewProjectInfo() {
        return new ProjectInfo();
      }

      public static ProjectInfoBrief GetProjectInfoBrief(string jobNumber) {
        var projectInfoList = GetProjectListPaged(1, $"{TblPfx}.JobNumber = @JobNumber", null, null, null, Common.NewSqlParameter("JobNumber", jobNumber));
        if ((projectInfoList?.InfoList?.Count ?? 0) == 0) return null;
        return projectInfoList.InfoList[0];
      }

      public static ProjectListPaged GetProjectsForCompany(int companyId) {
        var projectInfoList = GetProjectListPaged(null,
          $"{TblPfx}.SvCompanyId = @CompanyId",
          $"{TblPfx}.CreatedUtc DESC",
          null, null,
          Common.NewSqlParameter("CompanyId", companyId)
        );
        if (projectInfoList == null || projectInfoList.InfoList.Count == 0) return null;
        return projectInfoList;
      }

      public static bool ProjectExistsInCompany(int companyId, string jobNumber) {
        return Common.GetScalarQueryInt($@"
          SELECT IIF(EXISTS(
            SELECT 1
            FROM al_Project {TblPfx}
            WHERE {TblPfx}.SvCompanyId = @CompanyId AND {TblPfx}.JobNumber = @JobNumber
          ), 1, 0)",
          Common.NewSqlParameter("CompanyId", companyId),
          Common.NewSqlParameter("JobNumber", jobNumber)) > 0;
      }

      public static ProjectListPaged GetProjectsForCompanyAndUserOrg(int companyId, AbleUser.AbleUserBasicInfo user) {
        if (user == null) throw new ArgumentNullException("user is null");
        var projectInfoList = GetProjectListPaged(null,
          $"{TblPfx}.SvCompanyId = @CompanyId AND {TblPfx}.ParentOrgId = @UserOrgId",
          $"{TblPfx}.CreatedUtc DESC",
          null, null,
          Common.NewSqlParameter("CompanyId", companyId),
          Common.NewSqlParameter("UserOrgId", user.OrgId)
        );
        if (projectInfoList == null || projectInfoList.InfoList.Count == 0) return null;
        return projectInfoList;
      }

      public static List<ProjectInfoBrief> GetProjectsForEndProjectSurvey() {
        var projectList = GetProjectListPaged(null,
          $"{TblPfx}.ProjectClosedUtc >= DATEADD(DAY, -@EndProgramEmailWindowDays, GETUTCDATE()) AND {TblPfx}.EndProjectSurveySent IS NULL",
          "", null, null,
          Common.NewSqlParameter("EndProgramEmailWindowDays", ConfigHelper.EndProgramEmailWindowDays)
        );
        if (projectList.InfoList.Count == 0) return null;
        return projectList.InfoList;
      }

      public static ProjectListPaged GetSearchResults(string searchTerm) {

        if (searchTerm.IsNullOrEmptyOrWhitespace()) return null;

        // Separate search words are separated by spaces (like a google search)
        // so create an array of words and add a block of sql conditions for each word.

        // searchTerm = Regex.Replace(searchTerm.TrimWhitespace(), @"\s+", " "); // Ensure multiple spaces are a single space.
        string[] words = searchTerm.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries); // RemoveEmpty is important here.
        var sqlClauses = new List<string>();
        var sqlParams = new List<SqlParameter>();
        for (int term = 0; term < words.Length; term++) {
          sqlClauses.Add($@"
            ( {TblPfx}.JobNumber LIKE '%' + @SearchTerm{term} + '%'
              OR {TblPfx}.ProjectName LIKE '%' + @SearchTerm{term} + '%'
              OR {TblPfx}.FriendlyProjectTitle LIKE '%' + @SearchTerm{term} + '%'
              OR cmp.CompanyName LIKE '%' + @SearchTerm{term} + '%'
            )");
          sqlParams.Add(Common.NewSqlParameter($"SearchTerm{term}", words[term]));
        }

        return GetProjectListPaged(null,
          sqlClauses.Join(" AND "),
          $"j.MaxProgramEndDateUtc DESC, {TblPfx}.CreatedUtc DESC",
          0, 1000,
          sqlParams.ToArray());
      }

      private static ProjectListPaged GetProjectListPaged(
        int? topOrNullForAll,
        string sqlWhereConditions,
        string sqlOrderBy,
        int? offsetRows, int? fetchRows,
        params SqlParameter[] sqlWhereParams) {

        return GetProjectListPaged(null, topOrNullForAll, sqlWhereConditions, sqlOrderBy, offsetRows, fetchRows, sqlWhereParams);
      }

      private static ProjectListPaged GetProjectListPaged(
        AbleUser.AbleUserInfo forUser,
        int? topOrNullForAll,
        string sqlWhereConditions,
        string sqlOrderBy,
        int? offsetRows, int? fetchRows,
        params SqlParameter[] sqlWhereParams) {

        var infoPaged = new ProjectListPaged();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);

        string sql = $@"
          {(forUser == null ? "" : GetSQLDeliveryInProjectCTE(ForUserIdParam))}
          SELECT {sqlTop}
          COUNT(*) OVER() AS TotalRows,
          {GetSQLCommonColumns(forUser)}
          {GetSQLCommonJoins(forUser)}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        if (sqlTop.IsNullOrEmpty() && !sqlOrderBy.IsNullOrEmpty() && offsetRows >= 0 && fetchRows > 0) {
          infoPaged.OffsetRows = offsetRows;
          infoPaged.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        if (ConfigHelper.IsDevServer) infoPaged.SqlText = sql;

        Common.Query(sql,
          dr => {
            if (infoPaged.TotalRows == 0) infoPaged.TotalRows = dr.GetInt("TotalRows");
            var projectInfo = new ProjectInfoBrief(dr, forUser);
            infoPaged.InfoList.Add(projectInfo);
          },
          sqlWhereParams
        );
        return infoPaged;
      }

      public static ProjectInfo GetProjectInfoOrNull(string projectJobNumber, AbleUser.AbleUserInfo forUser = null) {

        ProjectInfo projectInfo = null;

        Common.Query($@"
          {(forUser == null ? "" : GetSQLDeliveryInProjectCTE(ForUserIdParam))}
          SELECT
            {GetSQLCommonColumns(forUser)},
            {TblPfx}.XeroAccountCode, {TblPfx}.XeroContactId, {TblPfx}.InvoiceInstructionTypeId,
            {TblPfx}.InvoiceNumber, {TblPfx}.InvoicingNotes, {TblPfx}.ProjectIntent, {TblPfx}.ProgramContext,
            {TblPfx}.WelcomeEmailCustomHTML, {TblPfx}.MeetCoachEmailCustomHTML,
            {TblPfx}.SurveyReminderCadenceId, {TblPfx}.SurveyReminderCadenceId_Raters,
            {TblPfx}.BookSessionEmailCustomHTML, {TblPfx}.BookingReminderCadenceDays, {TblPfx}.DisablePaxRegReminders,
            {TblPfx}.ProjectClosedUtc, {TblPfx}.PurchaseOrderRequired,
            {TblPfx}.IntakeSurveyTemplateId, {TblPfx}.PulseSurveyTemplateId, {TblPfx}.SendIntakeSurveyToAllParticipants,
            {TblPfx}.CoachingSessionEvalSurveyTemplateId, {TblPfx}.CoachingProgramEvalSurveyTemplateId, {TblPfx}.DevelopmentPlanTemplateId,
            {TblPfx}.WorkshopSessionEvalSurveyTemplateId, {TblPfx}.WorkshopAndProgramEvalSurveyTemplateId, {TblPfx}.GenericProgramEvalSurveyTemplateId,
            {TblPfx}.IntakeSurveyDisabled, {TblPfx}.PulseSurveyDisabled, {TblPfx}.CoachingSessionEvalSurveyDisabled, {TblPfx}.CoachingProgramEvalSurveyDisabled,
            {TblPfx}.WorkshopSessionEvalSurveyDisabled, {TblPfx}.WorkshopAndProgramEvalSurveyDisabled, {TblPfx}.GenericProgramEvalSurveyDisabled,
            {TblPfx}.WelcomeEmail_ProgramSummaryDisabled, {TblPfx}.NotifySelfWhen180RaterCompleted, {TblPfx}.CreatedByUserId,
            xc.ContactName AS XeroContactName,
            j.MinProgramStartDateUtc, j.MaxProgramEndDateUtc, j.MinProgramStatusId,
            cmp.SvCompanyId, cmp.CompanyName,
            it.Title AS InvoiceInstructionTypeName
          {GetSQLCommonJoins(forUser)}
          LEFT OUTER JOIN al_XeroContacts xc ON xc.XeroContactId = {TblPfx}.XeroContactId
          LEFT OUTER JOIN al_InvoiceInstructionType it ON it.InvoiceInstructionTypeId = {TblPfx}.InvoiceInstructionTypeId

          WHERE {TblPfx}.JobNumber = @JobNumber",

          dr => {
            projectInfo = new ProjectInfo(dr, forUser);
          },
          Common.NewSqlParameter("JobNumber", projectJobNumber),
          Common.NewSqlParameter(ForUserIdParam, forUser?.UserId)
        );

        return projectInfo;
      }

      // This returns SQL for a CTE that contains JobNumbers in which the given user is doing "delivery" (coach, facilitator, etc).
      // The SQL for this CTE is centralised here in case the rules for "delievery" are changed/expanded in future, it only needs to be changed here.
      // See AlbeQuotes.GetQuotesForLists() for an example of how to implement in a query.
      // (In future could add a cut-off date for j.AbleProgramStartDateUtc in case the CTE starts to get too big for a user over time?)
      internal static string GetSQLDeliveryInProjectCTE(string userIdParam) {

        if (userIdParam.IsNullOrEmpty()) throw new ArgumentException("userIdParam is required, e.g. 'ForUserId'.");
        if (!Regex.IsMatch(userIdParam, "^[a-z]+$", RegexOptions.IgnoreCase)) throw new ArgumentException("userIdParam must contain only letters.");

        return $@"
          WITH {DeliveryInProjectCTEName} AS (
            SELECT DISTINCT j.JobNumber, 1 AS {IsDeliveryInProjectCol}
            FROM id_Job j WITH (NOLOCK)
            LEFT JOIN ev_WorkshopEvent w ON j.JobId = w.ProgramJobId
            LEFT JOIN al_Coachees c ON j.JobId = c.ProgramJobId
            WHERE @{userIdParam} > 0
              AND ( w.KeyFacilitatorUserId = @{userIdParam}
                    OR w.CoFacilitatorUserId = @{userIdParam}
                    OR c.CoachUserId = @{userIdParam} )
          )";
      }

      // Clause to check quotes for whether user is Quote Owner or has a "delivery" role in the project.
      internal static string GetSQLWhereUserIsInProjectAccess(string jobNumberParam, string userIdParam) {

        if (jobNumberParam.IsNullOrEmpty()) throw new ArgumentException("jobNumberParameterName is required.");
        if (userIdParam.IsNullOrEmpty()) throw new ArgumentException("userIdParameterName is required.");
        if (!Regex.IsMatch(jobNumberParam, "^@?[a-z.]+$", RegexOptions.IgnoreCase)) throw new ArgumentException("Invalid characters in jobNumberParameterName.");
        if (!Regex.IsMatch(userIdParam, @"^@?[a-z.]+$", RegexOptions.IgnoreCase)) throw new ArgumentException("Invalid characters in userIdParameterName.");

        return $@"
          EXISTS (
            SELECT NULL
            FROM al_UserProjectAccess prjac
            INNER JOIN id_Job prjac_j ON prjac_j.JobId = prjac.ProgramJobId
            WHERE prjac_j.JobNumber = {jobNumberParam}
              AND prjac.UserId = {userIdParam}
          )";
      }

      private static string GetSQLCommonColumns(AbleUser.AbleUserInfo forUser) {

        return $@"
          {TblPfx}.ProjectId, {TblPfx}.ParentOrgId, {TblPfx}.ProjectName, {TblPfx}.FriendlyProjectTitle,
          {TblPfx}.JobNumber, {TblPfx}.CreatedUtc, {TblPfx}.BrandingOrgId,
          {TblPfx}.DefaultCostItemMarkupPercent, {TblPfx}.AllowCostItemUnitPriceManualOverwrite, {TblPfx}.AllowLoggedOutCoachBooking, {TblPfx}.CanSelfSelectCoach,
          {TblPfx}.OverrideSenderEmailName, {TblPfx}.OverrideSenderEmailAddress,
          dbo.fnGetOrgSenderEmailName({TblPfx}.ParentOrgId) AS ComputedSenderEmailName,
          dbo.fnGetOrgSenderEmailAddress({TblPfx}.ParentOrgId) AS ComputedSenderEmailAddress,
          j.MinProgramStartDateUtc, j.MaxProgramEndDateUtc, j.MinProgramStatusId,
          cmp.SvCompanyId, cmp.CompanyName,
          p_org.OrgGuid AS ParentOrgGuid, p_org.OrgOwnerUserId, p_org.OrgName AS ParentOrgName,
          b_org.OrgGuid AS BrandingOrgGuid,
          {(forUser == null ? "0" : "ispa.IsInProjectAccess")} AS ForUser_IsInProjectAccess,
          {(forUser == null ? "0" : "ispc.IsPCOrPLC")} AS ForUser_IsPCOrPLC,
          {(forUser == null ? "0" : "isqo.IsQuoteOwner")} AS ForUser_IsQuoteOwner,
          {(forUser == null ? "0" : $"ISNULL(dipCTE.{IsDeliveryInProjectCol}, 0)")} AS ForUser_IsDeliveryInProject";
      }

      private static string GetSQLCommonJoins(AbleUser.AbleUserInfo forUser) {
        return $@"
          FROM al_Project {TblPfx}
          INNER JOIN sv_Organisation p_org ON p_org.OrgId = {TblPfx}.ParentOrgId
          LEFT OUTER JOIN sv_Organisation b_org ON b_org.OrgId = {TblPfx}.BrandingOrgId
          LEFT OUTER JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = {TblPfx}.SvCompanyId
          {(forUser == null ? "" : $"LEFT OUTER JOIN {DeliveryInProjectCTEName} dipCTE ON dipCTE.JobNumber = {TblPfx}.JobNumber")}

          OUTER APPLY (
            SELECT
              MIN(AbleProgramStartDateUtc) AS MinProgramStartDateUtc,
              MAX(AbleProgramEndDateUtc) AS MaxProgramEndDateUtc,
              MIN(ProgramStatusId) AS MinProgramStatusId,
              MAX(j.CompanyId) AS CompanyId
            FROM id_Job j
            WHERE j.JobNumber = {TblPfx}.JobNumber
          ) AS j

          {(forUser == null ? "" : $@"
            CROSS APPLY (
              SELECT IIF(EXISTS (
                SELECT 1
                FROM id_Job pcj WITH (NOLOCK)
                WHERE pcj.JobNumber = {TblPfx}.JobNumber
                  AND (@ForUserId = pcj.ProjectCoordinatorUserId
                    OR @ForUserId = pcj.LeadConsultantUserId)
              ), 1, 0) AS IsPCOrPLC
            ) AS ispc")}

          {(forUser == null ? "" : $@"
            CROSS APPLY (
              SELECT IIF(EXISTS (
                SELECT 1
                FROM al_UserProjectAccess upa WITH (NOLOCK)
                WHERE upa.ProjectId = {TblPfx}.ProjectId
                  AND upa.UserId = @ForUserId
              ), 1, 0) AS IsInProjectAccess
            ) AS ispa")}

          {(forUser == null ? "" : $@"
            CROSS APPLY (
              SELECT IIF(EXISTS (
                SELECT 1
                FROM al_Quote aq WITH (NOLOCK)
                WHERE aq.JobNumber = {TblPfx}.JobNumber
                  AND aq.OwnerUserId = @ForUserId
              ), 1, 0) AS IsQuoteOwner
            ) AS isqo")}
        ";
      }

      public static void CreateProjectAndProgram(
        SqlTransaction trans,
        int tenantOrgId,
        int companyId,
        string projectName,
        string preferredProgramName,
        bool canSelfSelectCoach,
        int? createdByUserId,
        out string newJobNumber,
        out int newProgramJobId
      ) {
        CreateProjectAndProgram(
          trans: trans,
          tenantOrgId: tenantOrgId,
          companyId: companyId,
          projectName: projectName,
          preferredProgramName: preferredProgramName,
          projectIntent: null,
          programContext: null,
          invoiceInstructionTypeId: null,
          invoiceNumber: null,
          purchaseOrderRequired: false,
          invoicingNotes: null,
          xeroContactId: null,
          xeroAccountCode: null,
          defaultCostItemMarkupPercent: null,
          allowCostItemUnitPriceManualOverwrite: false,
          canSelfSelectCoach: canSelfSelectCoach,
          createdByUserId: createdByUserId,
          newJobNumber: out newJobNumber,
          newProgramJobId: out newProgramJobId);
      }

      public static void CreateProjectAndProgram(
        SqlTransaction trans,
        int tenantOrgId,
        int companyId,
        string projectName,
        string preferredProgramName,
        string projectIntent,
        string programContext,
        int? invoiceInstructionTypeId,
        string invoiceNumber,
        bool purchaseOrderRequired,
        string invoicingNotes,
        int? xeroContactId,
        string xeroAccountCode,
        decimal? defaultCostItemMarkupPercent,
        bool allowCostItemUnitPriceManualOverwrite,
        bool canSelfSelectCoach,
        int? createdByUserId,
        out string newJobNumber,
        out int newProgramJobId
      ) {

        if (trans == null) throw new ArgumentNullException(nameof(trans), "Transaction required.");

        // Note the fancy way to get the next available job number.
        // Job numbers start with "J" followed by a 6-digit number and then sometimes other codes.
        // We get the maximum of the numeric part in the table, add 1, the format back to "J" + 6 digits with leading zeroes.
        // Note that auto-job-numbering only happens if the job number given is NULL. Otherwise it uses the given one.

        newJobNumber = "";
        newProgramJobId = 0;

        string newJobNumber_temp = "";

        Common.Query(trans, $@"
          INSERT INTO al_Project (
            SvCompanyId, ProjectName, ParentOrgId,
            JobNumber,
            XeroAccountCode, XeroContactId, InvoiceNumber, InvoiceInstructionTypeId, InvoicingNotes, ProjectIntent, ProgramContext, PurchaseOrderRequired,
            DefaultCostItemMarkupPercent, AllowCostItemUnitPriceManualOverwrite, CanSelfSelectCoach, CreatedByUserId)
          OUTPUT INSERTED.ProjectId, INSERTED.JobNumber
          VALUES (
            @SvCompanyId, @ProjectName, @ParentOrgId,
            ISNULL(@JobNumber, (SELECT 'J' + FORMAT(MAX(TRY_CONVERT(BIGINT, SUBSTRING(prj.JobNumber, 2, 50))) + 1, 'D6') AS NextJobNumber
                                FROM al_Project prj
                                WHERE prj.JobNumber LIKE 'J%'
                                AND TRY_CONVERT(BIGINT, SUBSTRING(prj.JobNumber, 2, 50)) BETWEEN @JobAutoNumberFrom AND @JobAutoNumberTo)),
            @XeroAccountCode, @XeroContactId, @InvoiceNumber, @InvoiceInstructionTypeId, @InvoicingNotes, @ProjectIntent, @ProgramContext, @PurchaseOrderRequired,
            @DefaultCostItemMarkupPercent, @AllowCostItemUnitPriceManualOverwrite, @CanSelfSelectCoach, @CreatedByUserId)",
          new List<SqlParameter>() {
            Common.NewSqlParameter("SvCompanyId", companyId),
            Common.NewSqlParameter("ParentOrgId", tenantOrgId),
            Common.NewSqlParameter("ProjectName", projectName),
            Common.NewSqlParameter("JobNumber", (string)null),
            Common.NewSqlParameter("JobAutoNumberFrom", JobAutoNumberFrom),
            Common.NewSqlParameter("JobAutoNumberTo", JobAutoNumberTo),
            Common.NewSqlParameter("XeroAccountCode", xeroAccountCode),
            Common.NewSqlParameter("XeroContactId", xeroContactId),
            Common.NewSqlParameter("InvoiceNumber", invoiceNumber),
            Common.NewSqlParameter("InvoiceInstructionTypeId", invoiceInstructionTypeId),
            Common.NewSqlParameter("InvoicingNotes", invoicingNotes.EmptyIfNull()),
            Common.NewSqlParameter("ProjectIntent", projectIntent.EmptyIfNull()),
            Common.NewSqlParameter("ProgramContext", programContext.EmptyIfNull()),
            Common.NewSqlParameter("PurchaseOrderRequired", purchaseOrderRequired),
            Common.NewSqlParameter("DefaultCostItemMarkupPercent", defaultCostItemMarkupPercent.GetValueOrDefault(ConfigHelper.DefaultCostItemMarkupPercent)),
            Common.NewSqlParameter("AllowCostItemUnitPriceManualOverwrite", allowCostItemUnitPriceManualOverwrite),
            Common.NewSqlParameter("CanSelfSelectCoach", canSelfSelectCoach),
            Common.NewSqlParameter("CreatedByUserId", createdByUserId)
          }, dr => {
            newJobNumber_temp = dr.GetString("JobNumber");
          }
        );

        var programInfo = new DbHelper.AblePrograms.AbleProgramInfo() {
          ProgramJobNumber = newJobNumber_temp,
          ProgramJobName = preferredProgramName.ValueIfNullOrEmpty(ConfigHelper.DefaultNewProgramName),
          CompanyId = companyId,
          LeadConsultantUserId = createdByUserId
        };
        AblePrograms.CreateProgram(trans, programInfo);
        newProgramJobId = programInfo.ProgramJobId;

        newJobNumber = newJobNumber_temp;
      }

      public static bool UpdateProject(
        SqlTransaction trans,
        string jobNumberToUpdate,
        int? companyId,
        string projectName,
        int tenantOrgId,
        string projectIntent,
        string programContext,
        int? invoiceInstructionTypeId,
        string invoiceNumber,
        bool purchaseOrderRequired,
        string invoicingNotes,
        int? xeroContactId,
        string xeroAccountCode,
        decimal? defaultCostItemMarkupPercent,
        bool allowCostItemUnitPriceManualOverwrite,
        bool canSelfSelectCoach
      ) {
        if (jobNumberToUpdate.IsNullOrEmpty()) throw new ArgumentException(" jobNumberToUpdateis required.");
        var rows = Common.GetScalarQueryInt(trans, @"
          UPDATE al_Project SET
            SvCompanyId = @SvCompanyId,
            ProjectName = @ProjectName,
            ParentOrgId = @ParentOrgId,
            ProjectIntent = @ProjectIntent,
            ProgramContext = @ProgramContext,
            InvoiceInstructionTypeId = @InvoiceInstructionTypeId,
            InvoiceNumber = @InvoiceNumber,
            PurchaseOrderRequired = @PurchaseOrderRequired,
            InvoicingNotes = @InvoicingNotes,
            XeroContactId = @XeroContactId,
            XeroAccountCode = @XeroAccountCode,
            DefaultCostItemMarkupPercent = @DefaultCostItemMarkupPercent,
            AllowCostItemUnitPriceManualOverwrite = @AllowCostItemUnitPriceManualOverwrite,
            CanSelfSelectCoach = @CanSelfSelectCoach
          WHERE JobNumber = @JobNumber;
          SELECT @@RowCount;
          IF @@ROWCOUNT = 1
          BEGIN
            UPDATE id_Job
              SET CompanyId = @SvCompanyId
            WHERE JobNumber = @JobNumber
          END;",
          Common.NewSqlParameter("JobNumber", jobNumberToUpdate),
          Common.NewSqlParameter("SvCompanyId", companyId),
          Common.NewSqlParameter("ProjectName", projectName),
          Common.NewSqlParameter("ParentOrgId", tenantOrgId),
          Common.NewSqlParameter("ProjectIntent", projectIntent.EmptyIfNull()),
          Common.NewSqlParameter("ProgramContext", programContext.EmptyIfNull()),
          Common.NewSqlParameter("InvoiceInstructionTypeId", invoiceInstructionTypeId),
          Common.NewSqlParameter("InvoiceNumber", invoiceNumber),
          Common.NewSqlParameter("PurchaseOrderRequired", purchaseOrderRequired),
          Common.NewSqlParameter("InvoicingNotes", invoicingNotes),
          Common.NewSqlParameter("XeroContactId", xeroContactId),
          Common.NewSqlParameter("XeroAccountCode", xeroAccountCode),
          Common.NewSqlParameter("DefaultCostItemMarkupPercent", defaultCostItemMarkupPercent.GetValueOrDefault(ConfigHelper.DefaultCostItemMarkupPercent)),
          Common.NewSqlParameter("AllowCostItemUnitPriceManualOverwrite", allowCostItemUnitPriceManualOverwrite),
          Common.NewSqlParameter("CanSelfSelectCoach", canSelfSelectCoach)
        );
        return rows == 1;
      }

      public static bool UpdateCustomisations(ProjectInfo projectInfo) {

        if (projectInfo == null) throw new ArgumentException("projectInfo is null.");

        var rows = Common.GetNonQueryInt(@"
          UPDATE al_Project SET
            FriendlyProjectTitle = @FriendlyProjectTitle,
            CanSelfSelectCoach = @CanSelfSelectCoach,
            WelcomeEmailCustomHTML = @WelcomeEmailCustomHTML,
            MeetCoachEmailCustomHTML = @MeetCoachEmailCustomHTML,
            BookSessionEmailCustomHTML = @BookSessionEmailCustomHTML,
            SurveyReminderCadenceId = @SurveyReminderCadenceId,
            SurveyReminderCadenceId_Raters = @SurveyReminderCadenceId_Raters,
            BookingReminderCadenceDays = @BookingReminderCadenceDays,
            DisablePaxRegReminders = @DisablePaxRegReminders,
            BrandingOrgId = @BrandingOrgId,
            IntakeSurveyDisabled = @IntakeSurveyDisabled,
            IntakeSurveyTemplateId = @IntakeSurveyTemplateId,
            PulseSurveyDisabled = @PulseSurveyDisabled,
            PulseSurveyTemplateId = @PulseSurveyTemplateId,
            SendIntakeSurveyToAllParticipants = @SendIntakeSurveyToAllParticipants,
            CoachingSessionEvalSurveyDisabled = @CoachingSessionEvalSurveyDisabled,
            CoachingSessionEvalSurveyTemplateId = @CoachingSessionEvalSurveyTemplateId,
            CoachingProgramEvalSurveyDisabled = @CoachingProgramEvalSurveyDisabled,
            CoachingProgramEvalSurveyTemplateId = @CoachingProgramEvalSurveyTemplateId,
            WorkshopSessionEvalSurveyDisabled = @WorkshopSessionEvalSurveyDisabled,
            WorkshopSessionEvalSurveyTemplateId = @WorkshopSessionEvalSurveyTemplateId,
            WorkshopAndProgramEvalSurveyDisabled = @WorkshopAndProgramEvalSurveyDisabled,
            WorkshopAndProgramEvalSurveyTemplateId = @WorkshopAndProgramEvalSurveyTemplateId,
            DevelopmentPlanTemplateId = @DevelopmentPlanTemplateId,
            GenericProgramEvalSurveyDisabled = @GenericProgramEvalSurveyDisabled,
            GenericProgramEvalSurveyTemplateId = @GenericProgramEvalSurveyTemplateId,
            WelcomeEmail_ProgramSummaryDisabled = @WelcomeEmail_ProgramSummaryDisabled,
            OverrideSenderEmailName = @OverrideSenderEmailName,
            OverrideSenderEmailAddress = @OverrideSenderEmailAddress,
            AllowLoggedOutCoachBooking = @AllowLoggedOutCoachBooking,
            NotifySelfWhen180RaterCompleted = @NotifySelfWhen180RaterCompleted
          WHERE ProjectId = @ProjectId",
          Common.NewSqlParameter("ProjectId", projectInfo.ProjectId),
          Common.NewSqlParameter("FriendlyProjectTitle", projectInfo.FriendlyProjectTitle),
          Common.NewSqlParameter("CanSelfSelectCoach", projectInfo.CanSelfSelectCoach),
          Common.NewSqlParameter("WelcomeEmailCustomHTML", projectInfo.WelcomeEmailCustomHTML.EmptyIfNull()),
          Common.NewSqlParameter("MeetCoachEmailCustomHTML", projectInfo.MeetCoachEmailCustomHTML.EmptyIfNull()),
          Common.NewSqlParameter("BookSessionEmailCustomHTML", projectInfo.BookSessionEmailCustomHTML.EmptyIfNull()),
          Common.NewSqlParameter("SurveyReminderCadenceId", projectInfo.SurveyReminderCadenceId),
          Common.NewSqlParameter("SurveyReminderCadenceId_Raters", projectInfo.SurveyReminderCadenceId_Raters),
          Common.NewSqlParameter("BookingReminderCadenceDays", projectInfo.BookingReminderCadenceDays),
          Common.NewSqlParameter("DisablePaxRegReminders", projectInfo.DisablePaxRegReminders),
          Common.NewSqlParameter("BrandingOrgId", projectInfo.BrandingOrgId),
          Common.NewSqlParameter("IntakeSurveyDisabled", projectInfo.IntakeSurveyDisabled),
          Common.NewSqlParameter("IntakeSurveyTemplateId", projectInfo.IntakeSurveyTemplateId),
          Common.NewSqlParameter("PulseSurveyDisabled", projectInfo.PulseSurveyDisabled),
          Common.NewSqlParameter("PulseSurveyTemplateId", projectInfo.PulseSurveyTemplateId),
          Common.NewSqlParameter("SendIntakeSurveyToAllParticipants", projectInfo.SendIntakeSurveyToAllParticipants),
          Common.NewSqlParameter("CoachingSessionEvalSurveyDisabled", projectInfo.CoachingSessionEvalSurveyDisabled),
          Common.NewSqlParameter("CoachingSessionEvalSurveyTemplateId", projectInfo.CoachingSessionEvalSurveyTemplateId),
          Common.NewSqlParameter("CoachingProgramEvalSurveyDisabled", projectInfo.CoachingProgramEvalSurveyDisabled),
          Common.NewSqlParameter("CoachingProgramEvalSurveyTemplateId", projectInfo.CoachingProgramEvalSurveyTemplateId),
          Common.NewSqlParameter("WorkshopSessionEvalSurveyDisabled", projectInfo.WorkshopSessionEvalSurveyDisabled),
          Common.NewSqlParameter("WorkshopSessionEvalSurveyTemplateId", projectInfo.WorkshopSessionEvalSurveyTemplateId),
          Common.NewSqlParameter("WorkshopAndProgramEvalSurveyDisabled", projectInfo.WorkshopAndProgramEvalSurveyDisabled),
          Common.NewSqlParameter("WorkshopAndProgramEvalSurveyTemplateId", projectInfo.WorkshopAndProgramEvalSurveyTemplateId),
          Common.NewSqlParameter("DevelopmentPlanTemplateId", projectInfo.DevelopmentPlanTemplateId),
          Common.NewSqlParameter("GenericProgramEvalSurveyDisabled", projectInfo.GenericProgramEvalSurveyDisabled),
          Common.NewSqlParameter("GenericProgramEvalSurveyTemplateId", projectInfo.GenericProgramEvalSurveyTemplateId),
          Common.NewSqlParameter("WelcomeEmail_ProgramSummaryDisabled", projectInfo.WelcomeEmail_ProgramSummaryDisabled),
          Common.NewSqlParameter("OverrideSenderEmailName", projectInfo.OverrideSenderEmailName),
          Common.NewSqlParameter("OverrideSenderEmailAddress", projectInfo.OverrideSenderEmailAddress),
          Common.NewSqlParameter("AllowLoggedOutCoachBooking", projectInfo.AllowLoggedOutCoachBooking),
          Common.NewSqlParameter("NotifySelfWhen180RaterCompleted", projectInfo.NotifySelfWhen180RaterCompleted)
        );
        return rows == 1;
      }

      public static bool UpdatePurchaseOrderRequired(int projectId, bool purchaseOrderRequired, string purchaseOrderNumber) {
        var rows = Common.GetNonQueryInt(@"
          UPDATE al_Project SET
            PurchaseOrderRequired = @PurchaseOrderRequired,
            InvoiceNumber = @PurchaseOrderNumber
          WHERE ProjectId = @ProjectId",
          Common.NewSqlParameter("ProjectId", projectId),
          Common.NewSqlParameter("PurchaseOrderRequired", purchaseOrderRequired),
          Common.NewSqlParameter("PurchaseOrderNumber", purchaseOrderNumber));
        return rows == 1;
      }

      public static bool UpdateEndProjectSurveySent(SqlTransaction trans, int projectId, int surveyId) {

        var rows = Common.GetNonQueryInt(trans, @"
          UPDATE al_Project SET
            EndProjectSurveyId = @EndProjectSurveyId,
            EndProjectSurveySent = @EndProjectSurveySent
          WHERE ProjectId = @ProjectId",
          Common.NewSqlParameter("ProjectId", projectId),
          Common.NewSqlParameter("EndProjectSurveyId", surveyId),
          Common.NewSqlParameter("EndProjectSurveySent", DateTime.UtcNow));

        return rows == 1;
      }

      public static bool DeleteProject(int projectId) {
        int rows = Common.GetNonQueryInt(@"
          DELETE FROM al_Project
          WHERE ProjectId = @ProjectId",
          Common.NewSqlParameter("ProjectId", projectId));
        return rows == 1;
      }

      // ProjectInfoBrief is used for lists as only a subset of columns are needed for lists.
      public class ProjectListPaged : InfoListPaged<ProjectInfoBrief> { }

      public class ProjectInfoBrief {

        public int ProjectId { get; internal set; }
        public string JobNumber { get; internal set; }
        public string ProjectName { get; protected set; }
        public string FriendlyProjectTitle { get; set; }
        public DateTime CreatedUtc { get; protected set; }

        public int TenantOrgId { get; private set; }
        public int TenantOrgOwnerUserId { get; private set; }
        public Guid TenantOrgGuid { get; private set; }
        public string ParentOrgName { get; private set; }

        public int? BrandingOrgId { get; set; }
        public Guid? BrandingOrgGuid { get; private set; }

        public string ComputedSenderEmailName { get; private set; }
        public string ComputedSenderEmailAddress { get; private set; }
        public string OverrideSenderEmailName { get; set; }
        public string OverrideSenderEmailAddress { get; set; }

        public DateTime? MinProgramStartDateUtc { get; protected set; }
        public DateTime? MaxProgramEndDateUtc { get; protected set; }
        public int? MinProgramStatusId { get; protected set; }
        public int? CompanyId { get; protected set; }
        public string ClientCompanyName { get; protected set; }
        public bool ForUser_WasProvided { get; protected set; }
        public bool ForUser_IsPCOrPLC { get; protected set; }
        public bool ForUser_IsInProjectAccess { get; protected set; }
        public bool ForUser_IsDeliveryInProject { get; protected set; }
        public bool ForUser_IsQuoteOwner { get; private set; }
        public decimal? DefaultCostItemMarkupPercent { get; set; }
        public bool AllowCostItemUnitPriceManualOverwrite { get; set; }
        public bool AllowLoggedOutCoachBooking { get; set; }
        public bool CanSelfSelectCoach { get; set; }

        internal ProjectInfoBrief() {
          CreatedUtc = DateTime.UtcNow;
        }

        internal ProjectInfoBrief(SqlDataReader dr, AbleUser.AbleUserInfo forUser) {
          InitBrief(dr, forUser);
        }

        protected void InitBrief(SqlDataReader dr, AbleUser.AbleUserInfo forUser) {

          ProjectId = dr.GetInt("ProjectId");
          ProjectName = dr.GetString("ProjectName");
          FriendlyProjectTitle = dr.GetString("FriendlyProjectTitle");
          JobNumber = dr.GetString("JobNumber");
          CreatedUtc = dr.GetDateTime("CreatedUtc");

          TenantOrgOwnerUserId = dr.GetInt("OrgOwnerUserId");
          TenantOrgId = dr.GetInt("ParentOrgId");

          TenantOrgGuid = dr.GetGuid("ParentOrgGuid");
          ParentOrgName = dr.GetString("ParentOrgName");
          BrandingOrgId = dr.GetIntOrNull("BrandingOrgId");
          BrandingOrgGuid = dr.GetGuidOrNull("BrandingOrgGuid");

          ComputedSenderEmailName = dr.GetString("ComputedSenderEmailName");
          ComputedSenderEmailAddress = dr.GetString("ComputedSenderEmailAddress");
          OverrideSenderEmailName = dr.GetString("OverrideSenderEmailName");
          OverrideSenderEmailAddress = dr.GetString("OverrideSenderEmailAddress");

          MinProgramStartDateUtc = dr.GetDateTimeOrNull("MinProgramStartDateUtc");
          MaxProgramEndDateUtc = dr.GetDateTimeOrNull("MaxProgramEndDateUtc");
          MinProgramStatusId = dr.GetIntOrNull("MinProgramStatusId");
          CompanyId = dr.GetIntOrNull("SvCompanyId");
          ClientCompanyName = dr.GetString("CompanyName");

          ForUser_WasProvided = forUser != null;
          ForUser_IsPCOrPLC = forUser == null ? false : dr.GetBoolFromInt("ForUser_IsPCOrPLC");
          ForUser_IsInProjectAccess = forUser == null ? false : dr.GetBoolFromInt("ForUser_IsInProjectAccess");
          ForUser_IsDeliveryInProject = forUser == null ? false : dr.GetBoolFromInt("ForUser_IsDeliveryInProject");
          ForUser_IsQuoteOwner = forUser == null ? false : dr.GetBoolFromInt("ForUser_IsQuoteOwner");

          DefaultCostItemMarkupPercent = dr.GetDecimalOrNull("DefaultCostItemMarkupPercent");
          AllowCostItemUnitPriceManualOverwrite = dr.GetBoolFromInt("AllowCostItemUnitPriceManualOverwrite");
          AllowLoggedOutCoachBooking = dr.GetBoolFromInt("AllowLoggedOutCoachBooking");
          CanSelfSelectCoach = dr.GetBoolFromInt("CanSelfSelectCoach");
        }
      }

      public class ProjectInfo : ProjectInfoBrief {

        public string XeroAccountCode { get; private set; }
        public int? XeroContactId { get; private set; }
        public string XeroContactName { get; private set; }
        public int? InvoiceInstructionTypeId { get; private set; }
        public string InvoiceInstructionTypeName { get; private set; }
        public string PurchaseOrderNumber { get; private set; }
        public string InvoicingNotes { get; private set; }
        public string ProjectIntent { get; private set; }
        public string ProgramContext { get; private set; }
        public bool PurchaseOrderRequired { get; private set; }
        public DateTime? ProjectClosedUtc { get; private set; }

        public string WelcomeEmailCustomHTML { get; set; }
        public string MeetCoachEmailCustomHTML { get; set; }
        public string BookSessionEmailCustomHTML { get; set; }
        public int? SurveyReminderCadenceId { get; set; }
        public int? SurveyReminderCadenceId_Raters { get; set; }
        public string BookingReminderCadenceDays { get; set; } // string list of ints e.g. "1,5,10,18" etc. Must be in ascending order.
        public bool DisablePaxRegReminders { get; set; } // string list of ints e.g. "1,5,10,18" etc. Must be in ascending order.

        public int? IntakeSurveyTemplateId { get; set; }
        public int? PulseSurveyTemplateId { get; set; }
        public bool SendIntakeSurveyToAllParticipants { get; set; }
        public int? CoachingSessionEvalSurveyTemplateId { get; set; }
        public int? CoachingProgramEvalSurveyTemplateId { get; set; }
        public int? WorkshopSessionEvalSurveyTemplateId { get; set; }
        public int? WorkshopAndProgramEvalSurveyTemplateId { get; set; }
        public int? GenericProgramEvalSurveyTemplateId { get; set; }
        public int? DevelopmentPlanTemplateId { get; set; }

        public bool IntakeSurveyDisabled { get; set; }
        public bool PulseSurveyDisabled { get; set; }
        public bool CoachingSessionEvalSurveyDisabled { get; set; }
        public bool CoachingProgramEvalSurveyDisabled { get; set; }
        public bool WorkshopSessionEvalSurveyDisabled { get; set; }
        public bool WorkshopAndProgramEvalSurveyDisabled { get; set; }
        public bool GenericProgramEvalSurveyDisabled { get; set; }
        public bool WelcomeEmail_ProgramSummaryDisabled { get; set; }
        public bool NotifySelfWhen180RaterCompleted { get; set; }
        public int? CreatedByUserId { get; set; }

        internal ProjectInfo() {
          CreatedUtc = DateTime.UtcNow;
        }

        internal ProjectInfo(SqlDataReader dr, AbleUser.AbleUserInfo forUser) {
          InitBrief(dr, forUser);
          XeroAccountCode = dr.GetString("XeroAccountCode");
          XeroContactId = dr.GetIntOrNull("XeroContactId");
          XeroContactName = dr.GetString("XeroContactName");
          InvoiceInstructionTypeId = dr.GetIntOrNull("InvoiceInstructionTypeId");
          InvoiceInstructionTypeName = dr.GetString("InvoiceInstructionTypeName");
          PurchaseOrderNumber = dr.GetString("InvoiceNumber");
          InvoicingNotes = dr.GetString("InvoicingNotes");
          ProjectIntent = dr.GetString("ProjectIntent");
          ProgramContext = dr.GetString("ProgramContext");
          WelcomeEmailCustomHTML = dr.GetString("WelcomeEmailCustomHTML");
          MeetCoachEmailCustomHTML = dr.GetString("MeetCoachEmailCustomHTML");
          BookSessionEmailCustomHTML = dr.GetString("BookSessionEmailCustomHTML");
          SurveyReminderCadenceId = dr.GetIntOrNull("SurveyReminderCadenceId");
          SurveyReminderCadenceId_Raters = dr.GetIntOrNull("SurveyReminderCadenceId_Raters") ?? SurveyReminderCadenceId; // Default to self setting if not set.
          BookingReminderCadenceDays = dr.GetString("BookingReminderCadenceDays");
          DisablePaxRegReminders = dr.GetBoolFromInt("DisablePaxRegReminders");
          PurchaseOrderRequired = dr.GetBoolFromInt("PurchaseOrderRequired");
          ProjectClosedUtc = dr.GetDateTimeOrNull("ProjectClosedUtc");
          IntakeSurveyTemplateId = dr.GetIntOrNull("IntakeSurveyTemplateId");
          PulseSurveyTemplateId = dr.GetIntOrNull("PulseSurveyTemplateId");
          SendIntakeSurveyToAllParticipants = dr.GetBoolFromInt("SendIntakeSurveyToAllParticipants");
          CoachingSessionEvalSurveyTemplateId = dr.GetIntOrNull("CoachingSessionEvalSurveyTemplateId");
          CoachingProgramEvalSurveyTemplateId = dr.GetIntOrNull("CoachingProgramEvalSurveyTemplateId");
          WorkshopSessionEvalSurveyTemplateId = dr.GetIntOrNull("WorkshopSessionEvalSurveyTemplateId");
          WorkshopAndProgramEvalSurveyTemplateId = dr.GetIntOrNull("WorkshopAndProgramEvalSurveyTemplateId");
          GenericProgramEvalSurveyTemplateId = dr.GetIntOrNull("GenericProgramEvalSurveyTemplateId");
          DevelopmentPlanTemplateId = dr.GetIntOrNull("DevelopmentPlanTemplateId");
          IntakeSurveyDisabled = dr.GetBoolFromInt("IntakeSurveyDisabled");
          PulseSurveyDisabled = dr.GetBoolFromInt("PulseSurveyDisabled");
          CoachingSessionEvalSurveyDisabled = dr.GetBoolFromInt("CoachingSessionEvalSurveyDisabled");
          CoachingProgramEvalSurveyDisabled = dr.GetBoolFromInt("CoachingProgramEvalSurveyDisabled");
          WorkshopSessionEvalSurveyDisabled = dr.GetBoolFromInt("WorkshopSessionEvalSurveyDisabled");
          WorkshopAndProgramEvalSurveyDisabled = dr.GetBoolFromInt("WorkshopAndProgramEvalSurveyDisabled");
          GenericProgramEvalSurveyDisabled = dr.GetBoolFromInt("GenericProgramEvalSurveyDisabled");
          WelcomeEmail_ProgramSummaryDisabled = dr.GetBoolFromInt("WelcomeEmail_ProgramSummaryDisabled");
          NotifySelfWhen180RaterCompleted = dr.GetBoolFromInt("NotifySelfWhen180RaterCompleted");
          CanSelfSelectCoach = dr.GetBoolFromInt("CanSelfSelectCoach");
          CreatedByUserId = dr.GetIntOrNull("CreatedByUserId");
        }

        // Always return a cadence object, use default cadence if CadenceId is null.
        public SurveyReminderCadence SurveyReminderCadence => SurveyReminderCadence.GetByIdOrDefault(SurveyReminderCadenceId);
        // Default to above if CadenceId_Raters is null.
        public SurveyReminderCadence SurveyReminderCadence_Raters => SurveyReminderCadence.GetByIdOrDefault(SurveyReminderCadenceId_Raters);
      }

      // Used to maintain a cache of ProjectInfo to save db calls in loops.
      public class ProjectInfoCache {

        List<ProjectInfo> _projectInfoCache;

        public ProjectInfoCache() {

          _projectInfoCache = new List<ProjectInfo>();
        }

        // Return project in cache if found, or from db then add to cache, or null if not found.
        public ProjectInfo GetProjectInfoOrNull(string jobNumber) {

          if (jobNumber.IsNullOrEmpty()) return null;

          var projectInfo = _projectInfoCache.Find(p => p.JobNumber == jobNumber);

          if (projectInfo == null) {
            projectInfo = Projects.GetProjectInfoOrNull(jobNumber); // Get from db if not in cache.
            if (projectInfo != null) _projectInfoCache.Add(projectInfo); // If found, add to cache.
          }

          return projectInfo;
        }
      }

      public class ProjectInfoBriefCache {

        List<ProjectInfoBrief> _projectInfoBriefCache;

        public ProjectInfoBriefCache() {

          _projectInfoBriefCache = new List<ProjectInfoBrief>();
        }

        // Return project in cache if found, or from db then add to cache, or null if not found.
        public ProjectInfoBrief GetProjectInfoBrief(string jobNumber) {

          if (jobNumber.IsNullOrEmpty()) return null;

          var projectInfoBrief = _projectInfoBriefCache.Find(p => p.JobNumber == jobNumber);

          if (projectInfoBrief == null) {
            projectInfoBrief = Projects.GetProjectInfoBrief(jobNumber); // Get from db if not in cache.
            if (projectInfoBrief != null) _projectInfoBriefCache.Add(projectInfoBrief); // If found, add to cache.
          }

          return projectInfoBrief;
        }
      }
    }
  }
}
