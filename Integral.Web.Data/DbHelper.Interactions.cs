using System.Collections.Generic;

namespace Integral.Web {

  using static DbHelper.Common;

  public partial class DbHelper : HelperBase<DbHelper> {

    public class Interactions {

      public class InteractionTypeIds {
        public const int MessageSent = 1;
        public const int MessageReceived = 2;
        public const int WorkshopAttended = 3;
        public const int CoachingSession = 4;
        public const int CompletedPulse = 5;
        public const int CompletedIntake = 6;
        public const int Completed360Self = 7;
        public const int CompletedEval = 8;
        public const int CompletedDevPlan = 9;
        public const int ContentView = 10;
        public const int Completed360Rater = 11;
      }

      public static List<InteractionType> GetInteractionTypesByChartOrder() {

        var result = new List<InteractionType>();

        Query(@"
          SELECT ait.InteractionTypeId, ait.InteractionName, ait.InteractionCategoryName, ait.MinutesMultiplier, ait.ChartCSSColour, ait.ChartOrder
          FROM al_InteractionType ait
          ORDER BY ait.ChartOrder;",
          dr => {
            result.Add(new InteractionType(
              dr.GetInt("InteractionTypeId"),
              dr.GetString("InteractionName"),
              dr.GetString("InteractionCategoryName").EqualsIgnoreCase("digital"),
              dr.GetIntOrNull("MinutesMultiplier"),
              dr.GetString("ChartCSSColour"),
              dr.GetInt("ChartOrder")
            ));
          }
        );

        return result;
      }

      public class InteractionType {

        public int InteractionTypeId { get; private set; }
        public string InteractionName { get; private set; }
        public bool IsDigital { get; private set; }
        public int? MinutesMultiplier { get; private set; }
        public string ChartCSSColour { get; private set; }
        public int ChartOrder { get; private set; }

        public InteractionType(int interactionTypeId, string interactionName, bool isDigital, int? minutesMultiplier, string chartCSSColour, int chartOrder) {
          InteractionTypeId = interactionTypeId;
          InteractionName = interactionName;
          IsDigital = isDigital;
          MinutesMultiplier = minutesMultiplier;
          ChartCSSColour = chartCSSColour;
          ChartOrder = chartOrder;
        }
      }

    }
  }
}
