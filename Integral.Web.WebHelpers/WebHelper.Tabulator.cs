using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class WebHelper {

    public class Tabulator {

      public enum AjaxAction {
        Unknown = 0,
        GetTableData = 1
      }

      public class JsonKeys {
        public static readonly string TableData = nameof(ResponseData<int>.TableData);
        public static readonly string ResponseLog = nameof(ResponseData<int>.ResponseLog);
      }

      public static AjaxAction GetAjaxAction() {
        string ajaxActionStr = SystemWeb.GetFormValue(PathHelper.FormKeys.AjaxAction);
        if (ajaxActionStr.IsNullOrEmpty()) {
          ajaxActionStr = SystemWeb.RequestQueryStringValue(PathHelper.FormKeys.AjaxAction);
        }
        if (ajaxActionStr != null && Enum.TryParse(ajaxActionStr, true, out AjaxAction ajaxAction)) {
          return ajaxAction;
        }
        return AjaxAction.Unknown;
      }

      // Use this class for all Tabulator response data.
      public class ResponseData<T> {

        public readonly List<LogHelper.ResponseLogItem> ResponseLog = LogHelper.GetResponseLog();

        public IList<T> TableData { get; private set; }

        public ResponseData(IList<T> tableData) {
          TableData = tableData;
        }
      }

    }
  }
}
