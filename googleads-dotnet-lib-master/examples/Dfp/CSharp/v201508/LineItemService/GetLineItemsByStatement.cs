// Copyright 2015, Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;

using System;

namespace Google.Api.Ads.Dfp.Examples.CSharp.v201508 {
  /// <summary>
  /// This code example gets all line items that need creatives for the given order.
  /// To create line items, run CreateLineItems.cs.
  /// </summary>
  class GetLineItemsByStatement : SampleBase {
    /// <summary>
    /// Returns a description about the code example.
    /// </summary>
    public override string Description {
      get {
        return "This code example gets all line items that need creatives for the given order. " +
            "To create line items, run CreateLineItems.cs.";
      }
    }

    /// <summary>
    /// Main method, to run this code example as a standalone application.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public static void Main(string[] args) {
      SampleBase codeExample = new GetLineItemsByStatement();
      Console.WriteLine(codeExample.Description);
      codeExample.Run(new DfpUser());
    }

    /// <summary>
    /// Run the code example.
    /// </summary>
    /// <param name="user">The DFP user object running the code example.</param>
    public override void Run(DfpUser user) {
      // Get the LineItemService.
      LineItemService lineItemService =
          (LineItemService) user.GetService(DfpService.v201508.LineItemService);

      // Set the ID of the order to get line items from.
      long orderId = long.Parse(_T("INSERT_ORDER_ID_HERE"));

      // Create a statement to only select line items that need creatives from a
      // given order.
      StatementBuilder statementBuilder = new StatementBuilder()
          .Where("orderId = :orderId AND isMissingCreatives = :isMissingCreatives")
          .OrderBy("id ASC")
          .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT)
          .AddValue("orderId", orderId)
          .AddValue("isMissingCreatives", true);

      // Set default for page.
      LineItemPage page = new LineItemPage();

      try {
        do {
          // Get line items by statement.
          page = lineItemService.getLineItemsByStatement(statementBuilder.ToStatement());

          if (page.results != null && page.results.Length > 0) {
            int i = page.startIndex;
            foreach (LineItem lineItem in page.results) {
              Console.WriteLine("{0}) Line item with ID ='{1}', belonging to order ID = '{2}' " +
                   "and named '{3}' was found.", i, lineItem.id, lineItem.orderId, lineItem.name);
              i++;
            }
          }
          statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
        } while (statementBuilder.GetOffset() < page.totalResultSetSize);
        Console.WriteLine("Number of results found: {0}", page.totalResultSetSize);
      } catch (Exception e) {
        Console.WriteLine("Failed to get line item by statement. Exception says \"{0}\"",
            e.Message);
      }
    }
  }
}
