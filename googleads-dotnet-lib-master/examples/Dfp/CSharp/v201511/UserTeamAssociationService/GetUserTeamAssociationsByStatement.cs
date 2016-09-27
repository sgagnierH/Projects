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
using Google.Api.Ads.Dfp.Util.v201511;
using Google.Api.Ads.Dfp.v201511;

using System;

namespace Google.Api.Ads.Dfp.Examples.CSharp.v201511 {
  /// <summary>
  ///  This code example gets all teams that the a user belongs to. To create
  ///  teams, run CreateTeams.cs.
  /// </summary>
  class GetUserTeamAssociationsByStatement : SampleBase {
    /// <summary>
    /// Returns a description about the code example.
    /// </summary>
    public override string Description {
      get {
        return "This code example gets all teams that a user belongs to. To create " +
            "teams, run CreateTeams.cs.";
      }
    }

    /// <summary>
    /// Main method, to run this code example as a standalone application.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public static void Main(string[] args) {
      SampleBase codeExample = new GetUserTeamAssociationsByStatement();
      Console.WriteLine(codeExample.Description);
      codeExample.Run(new DfpUser());
    }

    /// <summary>
    /// Run the code example.
    /// </summary>
    /// <param name="user">The DFP user object running the code example.</param>
    public override void Run(DfpUser user) {
      // Get the UserTeamAssociationService.
      UserTeamAssociationService userTeamAssociationService =
          (UserTeamAssociationService) user.GetService(
              DfpService.v201511.UserTeamAssociationService);

      // Set the ID of the user to fetch all user team associations for.
      long userId = long.Parse(_T("INSERT_USER_ID_HERE"));

      // Create statement to select user team associations by the user ID.
      StatementBuilder statementBuilder = new StatementBuilder()
          .Where("userId = :userId")
          .OrderBy("userId ASC, teamId ASC")
          .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT)
          .AddValue("userId", userId);

      // Set default for page.
      UserTeamAssociationPage page = new UserTeamAssociationPage();

      try {
        do {
          // Get user team associations by statement.
          page = userTeamAssociationService.getUserTeamAssociationsByStatement(
              statementBuilder.ToStatement());

          // Display results.
          if (page.results != null) {
            int i = page.startIndex;
            foreach (UserTeamAssociation userTeamAssociation in page.results) {
              Console.WriteLine("{0}) User team association between user with ID \"{1}\" and " +
                  "team with ID \"{2}\" was found.", i, userTeamAssociation.userId,
                  userTeamAssociation.teamId);
              i++;
            }
          }

          statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
        } while(statementBuilder.GetOffset() < page.totalResultSetSize);
        Console.WriteLine("Number of results found: {0}", page.totalResultSetSize);
      } catch (Exception e) {
        Console.WriteLine("Failed to get user team associations. Exception says \"{0}\"",
            e.Message);
      }
    }
  }
}
