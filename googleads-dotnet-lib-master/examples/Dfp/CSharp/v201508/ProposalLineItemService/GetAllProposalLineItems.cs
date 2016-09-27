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
  /// This code example gets all proposal line items. To create proposal line items, run
  /// CreateProposalLineItems.cs.
  /// </summary>
  class GetAllProposalLineItems : SampleBase {
    /// <summary>
    /// Returns a description about the code example.
    /// </summary>
    public override string Description {
      get {
        return "This code example gets all proposal line items. To create proposal line items, " +
            "run CreateProposalLineItems.cs.";
      }
    }

    /// <summary>
    /// Main method, to run this code example as a standalone application.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public static void Main(string[] args) {
      SampleBase codeExample = new GetAllProposalLineItems();
      Console.WriteLine(codeExample.Description);
      codeExample.Run(new DfpUser());
    }

    /// <summary>
    /// Run the code example.
    /// </summary>
    /// <param name="user">The DFP user object running the code example.</param>
    public override void Run(DfpUser user) {
      // Get the ProposalLineItemService.
      ProposalLineItemService proposalLineItemService =
          (ProposalLineItemService) user.GetService(DfpService.v201508.ProposalLineItemService);

      // Create a statement to get all proposal line items.
      StatementBuilder statementBuilder = new StatementBuilder()
          .OrderBy("id ASC")
          .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

      // Sets default for page.
      ProposalLineItemPage page = new ProposalLineItemPage();
      try {
        do {
          // Get proposal line items by statement.
          page = proposalLineItemService
              .getProposalLineItemsByStatement(statementBuilder.ToStatement());

          if (page.results != null && page.results.Length > 0) {
            int i = page.startIndex;
            foreach (ProposalLineItem proposalLineItem in page.results) {
              Console.WriteLine("{0}) Proposal line item with ID = '{1}' and name '{2}' was" +
                  " found.", i++, proposalLineItem.id, proposalLineItem.name);
            }
          }

          statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
        } while (statementBuilder.GetOffset() < page.totalResultSetSize);

        Console.WriteLine("Number of results found: {0}", page.totalResultSetSize);
      } catch (Exception e) {
        Console.WriteLine("Failed to get proposal line items. Exception says \"{0}\"",
            e.Message);
      }
    }
  }
}
