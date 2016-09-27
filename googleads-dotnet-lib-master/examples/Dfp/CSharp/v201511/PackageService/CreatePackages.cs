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
using Google.Api.Ads.Dfp.v201511;

using System;

namespace Google.Api.Ads.Dfp.Examples.CSharp.v201511 {
  /// <summary>
  /// This code example creates a package. To determine which packages exist,
  /// run GetAllPackages.cs.
  /// </summary>
  class CreatePackages : SampleBase {
    /// <summary>
    /// Returns a description about the code example.
    /// </summary>
    public override string Description {
      get {
        return "This code example creates a package. To determine which packages exist, " +
            "run GetAllPackages.cs.";
      }
    }

    /// <summary>
    /// Main method, to run this code example as a standalone application.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public static void Main(string[] args) {
      SampleBase codeExample = new CreatePackages();
      Console.WriteLine(codeExample.Description);
      codeExample.Run(new DfpUser());
    }

    /// <summary>
    /// Run the code example.
    /// </summary>
    /// <param name="user">The DFP user object running the code example.</param>
    public override void Run(DfpUser user) {
      // Get the PackageService.
      PackageService packageService =
          (PackageService) user.GetService(DfpService.v201511.PackageService);

      // Set the ID of the product package to create the package from.
      long productPackageId = long.Parse(_T("INSERT_PRODUCT_PACKAGE_ID"));

      // Set the proposal ID for the package.
      long proposalId = long.Parse(_T("INSERT_PROPOSAL_ID"));

      // Set the ID of the rate card the proposal line items belonging to the product
      // package are priced from.
      long rateCardId = long.Parse(_T("INSERT_RATE_CARD_ID"));

      // Create a local package.
      Package package = new Package();
      package.name = "Package #" + new Random().Next(int.MaxValue);

      // Set the proposal ID for the package.
      package.proposalId = proposalId;

      // Set the product package ID to create the package from.
      package.productPackageId = productPackageId;

      // Set the rate card ID the proposal line items are priced with.
      package.rateCardId = rateCardId;

      try {
        // Create the package on the server.
        Package[] packages = packageService.createPackages(new Package[] {package});

        foreach (Package createdPackage in packages) {
          Console.WriteLine("A package with ID \"{0}\" and name \"{1}\" was created.",
              createdPackage.id, createdPackage.name);
        }
      } catch (Exception e) {
          Console.WriteLine("Failed to create packages. Exception says \"{0}\"", e.Message);
      }
    }
  }
}
