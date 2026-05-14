namespace Integral.Web {

  public partial class DbHelper {

    public partial class Interfaces {

      public interface IQuoteSignoffInfo {
        string ClientFirstName { get; set; }
        string ClientLastName { get; set; }
        string ClientEmail { get; set; }
        string AccFirstName { get; set; }
        string AccLastName { get; set; }
        string AccEmail { get; set; }
        bool PurchaseOrderRequired { get; set; }
        string PurchaseOrderNumber { get; set; }
      }
    }

  }
}
