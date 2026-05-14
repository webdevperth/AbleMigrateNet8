using System;
using System.Collections.Generic;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class SessionHelper {

    public class AppState {

      // Note everything in AppState relies on built-in ASP.NET session,
      // so these settings will reset when session is lost (app restart etc).
      // Therefore don't store anything in "AppState" here if it must persist
      // for the duration of a user's login session.
      // UI things like search filters can be reset occasionally without concern.
      // To persist values for a user's entire login session, add them to the
      // SessionHelper.SessionSettings class, which is serialized to the database.

      public class General {

        private const string Key_LoginRedirectUrl = "LoginRedirectUrl";
        private const string Key_NextPageMessageText = "NextPageMessageText";
        private const string Key_NextPageMessageType = "NextPageMessageType";
        private const string Key_LoginRequiredRedirectedFrom = "LoginRequiredRedirectedFrom";

        internal static string LoginRedirectUrl {
          get { return SystemWeb.GetSessionValue(Key_LoginRedirectUrl) as string; }
          set { SystemWeb.SetSessionValue(Key_LoginRedirectUrl, value); }
        }

        internal static string NextPageMessageText {
          get { return SystemWeb.GetSessionValue(Key_NextPageMessageText) as string; }
          set { SystemWeb.SetSessionValue(Key_NextPageMessageText, value); }
        }

        internal static AjaxSubmitHelper.PageMessageType NextPageMessageType {
          get {
            if (Enum.TryParse(SystemWeb.GetSessionValue(Key_NextPageMessageType).ToStringOrEmptyIfNull(), out AjaxSubmitHelper.PageMessageType messageType)) {
              return messageType;
            } else {
              return AjaxSubmitHelper.PageMessageType.None;
            }
          }
          set { SystemWeb.SetSessionValue(Key_NextPageMessageType, value); }
        }

        internal static LoginRequiredRedirectedFrom LoginRequiredRedirectedFrom {
          get {
            if (Enum.TryParse(SystemWeb.GetSessionValue(Key_LoginRequiredRedirectedFrom).ToStringOrEmptyIfNull(), out LoginRequiredRedirectedFrom loginRequiredRedirectedFrom)) {
              return loginRequiredRedirectedFrom;
            } else {
              return LoginRequiredRedirectedFrom.None;
            }
          }
          set { SystemWeb.SetSessionValue(Key_LoginRequiredRedirectedFrom, value); }
        }
      }

      public class Programs {

        // Note the string const values are global (all stored in session state) so must be unique, hence prefixed with "program".
        private const string SearchKey_SearchFor = "programSearchKey_SearchFor";
        private const string SearchKey_CurrentPage = "programSearchKey_CurrentPage";
        private const string SearchKey_StatusClosed = "programSearchKey_StatusClosed";

        public static string Search_SearchTerm {
          get { return SystemWeb.GetSessionValue(SearchKey_SearchFor) as string; }
          set { SystemWeb.SetSessionValue(SearchKey_SearchFor, value); }
        }

        public static int? Search_CurrentPage {
          get { return ("" + SystemWeb.GetSessionValue(SearchKey_CurrentPage)).ToIntOrNull(); }
          set { SystemWeb.SetSessionValue(SearchKey_CurrentPage, value); }
        }

        public static bool Search_StatusClosed {
          get { return SystemWeb.GetSessionValue(SearchKey_StatusClosed).ToBooleanOrDefault(false); }
          set { SystemWeb.SetSessionValue(SearchKey_StatusClosed, value); }
        }

      }

      public class Coachees {

        // Note the string const values are global (all stored in session state) so must be unique, hence prefixed with "coachee".

        private const string SearchKey_SearchFor = "coacheeSearch_SearchFor";
        private const string SearchKey_CurrentPage = "coacheeSearch_CurrentPage";
        private const string SearchKey_StatusEndProgram = "coacheeSearch_StatusEndProgram";

        private const string Filter_TypeKey = "coacheeFilter_Type";
        private const string Filter_IdKeyPrefix = "coacheeFilter_Id";
        private const string Filter_NameKeyPrefix = "coacheeFilter_Name";

        private const int StatusEndProgramToggle_On = 1;

        // Coachee list filters.
        // If user is not admin (i.e. just a coach), then only coachees belonging to that coach are listed.
        public enum FilterScope {
          Coach = 1,      // For admin users, all coachees belonging to a single Coach.
          Company = 2,    // All coachees belonging to a Company.
          Program = 3,    // All coachees in a single Program, regardless of program status.
          EndProgramToggle = 4  // If filter Id is StatusEndProgramToggle_On, then only list End-Program coachees.
        }

        public static string Search_SearchTerm {
          get { return SystemWeb.GetSessionValue(SearchKey_SearchFor) as string; }
          set { SystemWeb.SetSessionValue(SearchKey_SearchFor, value); }
        }

        public static int? Search_CurrentPage {
          get { return ("" + SystemWeb.GetSessionValue(SearchKey_CurrentPage)).ToIntOrNull(); }
          set { SystemWeb.SetSessionValue(SearchKey_CurrentPage, value); }
        }

        public static bool Search_StatusEndProgram {
          get { return SystemWeb.GetSessionValue(SearchKey_StatusEndProgram).ToBooleanOrDefault(false); }
          set { SystemWeb.SetSessionValue(SearchKey_StatusEndProgram, value); }
        }

        private static string GetFilterIdKey(FilterScope filterType) {
          return Filter_IdKeyPrefix + filterType;
        }
        private static string GetFilterNameKey(FilterScope filterType) {
          return Filter_NameKeyPrefix + filterType;
        }

        public static FilterScope GetFilterTypeDefault() {
          return FilterScope.Coach;
        }

        public static FilterScope GetFilterTypeFromSessionOrDefault() {
          if (SystemWeb.GetSessionValue(Filter_TypeKey) is FilterScope)
            return (FilterScope)SystemWeb.GetSessionValue(Filter_TypeKey);
          return GetFilterTypeDefault();
        }

        private static int? GetFilterValueIntOrNull(string key) {
          int value;
          if (int.TryParse(SystemWeb.GetSessionValue(key).ToStringOrEmptyIfNull(), out value)) return value;
          return null;
        }

        public static int? GetFilterIdFromSession(FilterScope? filterType = null) {
          return GetFilterValueIntOrNull(GetFilterIdKey(filterType ?? GetFilterTypeFromSessionOrDefault()));
        }

        public static void SetFilterScope(FilterScope filterScope, int id, string name) {
          if (!SystemWeb.HasSession) return;
          if (filterScope != FilterScope.EndProgramToggle) {
            SystemWeb.SetSessionValue(Filter_TypeKey, filterScope); // Note toggle isn't a separate state.
          }
          SystemWeb.SetSessionValue(GetFilterIdKey(filterScope), id);
          SystemWeb.SetSessionValue(GetFilterNameKey(filterScope), name);
        }

        public static bool IsEndProgramFilter() {
          return GetFilterIdFromSession(FilterScope.EndProgramToggle) == StatusEndProgramToggle_On;
        }

        public static void SetEndProgramFilter(bool value) {
          SetFilterScope(FilterScope.EndProgramToggle, value ? StatusEndProgramToggle_On : 0, "");
        }

        public static void ToggleEndProgramFilter() {
          int? value = GetFilterIdFromSession(FilterScope.EndProgramToggle);
          SetFilterScope(FilterScope.EndProgramToggle, value == StatusEndProgramToggle_On ? 0 : StatusEndProgramToggle_On, "");
        }

        public static void ResetFilter() {
          SetFilterScope(FilterScope.Coach, GetUserIdOrNull() ?? 0, "");
        }

        public static void ResetIfNotFilteredProgram(int? coacheeProgramJobId) {
          var filterType = GetFilterTypeFromSessionOrDefault();
          if (filterType == FilterScope.Program) {
            var filterProgramJobId = GetFilterIdFromSession().GetValueOrDefault(0);
            if (filterProgramJobId > 0 && coacheeProgramJobId.GetValueOrDefault(0) != filterProgramJobId) ResetFilter();
          }
        }
      }

      public class OrganisationPeople {

        // Note the string const values are global (all stored in session state) so must be unique, hence prefixed with "people".
        private const string SearchKey_SearchFor = "peopleSearchKey_SearchFor";
        private const string SearchKey_CurrentPage = "peopleSearchKey_CurrentPage";
        private const string SearchKey_StatusInactive = "peopleSearchKey_StatusInactive";

        public static string Search_SearchTerm {
          get { return SystemWeb.GetSessionValue(SearchKey_SearchFor) as string; }
          set { SystemWeb.SetSessionValue(SearchKey_SearchFor, value); }
        }

        public static int? Search_CurrentPage {
          get { return ("" + SystemWeb.GetSessionValue(SearchKey_CurrentPage)).ToIntOrNull(); }
          set { SystemWeb.SetSessionValue(SearchKey_CurrentPage, value); }
        }

        public static bool Search_StatusInactive {
          get { return SystemWeb.GetSessionValue(SearchKey_StatusInactive).ToBooleanOrDefault(false); }
          set { SystemWeb.SetSessionValue(SearchKey_StatusInactive, value); }
        }

      }

      public class Projects {

        // Note the string const values are global (all stored in session state) so must be unique, hence prefixed with "project".
        private const string SearchKey_SearchFor = "projectSearchKey_SearchFor";
        private const string SearchKey_CurrentPage = "projectSearchKey_CurrentPage";

        public static string Search_SearchFor {
          get { return SystemWeb.GetSessionValue(SearchKey_SearchFor) as string; }
          set { SystemWeb.SetSessionValue(SearchKey_SearchFor, value); }
        }

        public static int? Search_CurrentPage {
          get { return ("" + SystemWeb.GetSessionValue(SearchKey_CurrentPage)).ToIntOrNull(); }
          set { SystemWeb.SetSessionValue(SearchKey_CurrentPage, value); }
        }
      }

      public class Quotes {

        // Note the string const values are global (all stored in session state) so must be unique, hence prefixed with "quotes".
        private const string SearchKey_SearchFor = "quotesSearchKey_SearchFor";
        private const string SearchKey_CurrentPage = "quotesSearchKey_CurrentPage";
        private const string SearchKey_StatusClosed = "quotesSearchKey_StatusClosed";

        public static string Search_SearchFor {
          // If JS had passed "undefined" as a value, revert to blank.
          get {
            string searchFor = SystemWeb.GetSessionValue(SearchKey_SearchFor) as string;
            if (searchFor == null || searchFor == "undefined") searchFor = string.Empty;
            return searchFor;
          }
          set {
            if (value == null || value == "undefined") value = string.Empty;
            SystemWeb.SetSessionValue(SearchKey_SearchFor, value);
          }
        }

        public static int? Search_CurrentPage {
          get { return ("" + SystemWeb.GetSessionValue(SearchKey_CurrentPage)).ToIntOrNull(); }
          set { SystemWeb.SetSessionValue(SearchKey_CurrentPage, value); }
        }

        public static bool Search_StatusClosed {
          get { return SystemWeb.GetSessionValue(SearchKey_StatusClosed).ToBooleanOrDefault(false); }
          set { SystemWeb.SetSessionValue(SearchKey_StatusClosed, value); }
        }
      }

      public class CoachList {

        // Note the string const values are global (all stored in session state) so must be unique, hence prefixed with "coach".
        private const string List_SelectedTagIds = "coachList_SelectedTagIds";
        private const string List_SelectedTagIds_LastSet = "coachList_SelectedTagIds_LastSet";
        private const string List_StatusInactive = "coachList_StatusInactive";
        private const int List_SelectedTagIds_ResetMins = 1; // Reset tag filter if not accessed for this time.
        private const string SearchKey_SearchFor = "coachSearchKey_SearchFor";
        private const string SearchKey_CurrentPage = "coachSearchKey_CurrentPage";

        public static List<int> GetSelectedTagIds() {
          CheckSelectedTagTimeout();
          SetSelectedTagAccessed();
          try {
            return SystemWeb.GetSessionValue(List_SelectedTagIds)?.ToStringOrNull()?.ToIntList();
          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation(nameof(GetSelectedTagIds))
              .WithOperationContext("CoachList")
              .WithProperty(ApplicationInsightsConstants.SessionValue, SystemWeb.GetSessionValue(List_SelectedTagIds)?.ToString())
              .Track();
            return null;
          }
        }

        public static void SetSelectedTagIds(List<int> tagIds) {
          SetSelectedTagAccessed();
          if (tagIds.IsNullOrEmpty()) {
            SystemWeb.SetSessionValue(List_SelectedTagIds, null);
          } else {
            SystemWeb.SetSessionValue(List_SelectedTagIds, tagIds.ToStringList());
          }
        }

        private static void ClearSelectedTagIds() {
          SystemWeb.SetSessionValue(List_SelectedTagIds, null);
        }

        private static void SetSelectedTagAccessed() {
          SystemWeb.SetSessionValue(List_SelectedTagIds_LastSet, DateTime.UtcNow);
        }
        private static void CheckSelectedTagTimeout() {
          var lastSet = (SystemWeb.GetSessionValue(List_SelectedTagIds_LastSet) as DateTime? ?? DateTime.UtcNow);
          if ((DateTime.UtcNow - lastSet).TotalMinutes > List_SelectedTagIds_ResetMins) ClearSelectedTagIds();
        }
        public static string Search_SearchFor {
          get { return SystemWeb.GetSessionValue(SearchKey_SearchFor) as string; }
          set { SystemWeb.SetSessionValue(SearchKey_SearchFor, value); }
        }
        public static bool Search_StatusInactive {
          get { return SystemWeb.GetSessionValue(List_StatusInactive).ToBooleanOrDefault(false); }
          set { SystemWeb.SetSessionValue(List_StatusInactive, value); }
        }
        public static int? Search_CurrentPage {
          get { return ("" + SystemWeb.GetSessionValue(SearchKey_CurrentPage)).ToIntOrNull(); }
          set { SystemWeb.SetSessionValue(SearchKey_CurrentPage, value); }
        }
      }

      public class Organisations {

        // Note the string const values are global (all stored in session state) so must be unique, hence prefixed with "org".
        private const string SearchKey_SearchFor = "orgSearchKey_SearchFor";
        private const string SearchKey_StatusInactive = "orgSearchKey_StatusInactive";

        public static string Search_SearchFor {
          get { return SystemWeb.GetSessionValue(SearchKey_SearchFor) as string; }
          set { SystemWeb.SetSessionValue(SearchKey_SearchFor, value); }
        }

        public static bool Search_StatusInactive {
          get { return SystemWeb.GetSessionValue(SearchKey_StatusInactive).ToBooleanOrDefault(false); }
          set { SystemWeb.SetSessionValue(SearchKey_StatusInactive, value); }
        }
      }
    }
  }
}
