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
using Google.Api.Ads.Dfp.Util.v201602;
using Google.Api.Ads.Dfp.v201602;

using System;

namespace Google.Api.Ads.Dfp.Examples.CSharp.v201602 {
  /// <summary>
  /// This code example gets all products created from a product template.
  /// </summary>
  class GetProductsForProductTemplate : SampleBase {
    /// <summary>
    /// Returns a description about the code example.
    /// </summary>
    public override string Description {
      get {
        return "This code example gets all products created from a product template.";
      }
    }

    /// <summary>
    /// Main method, to run this code example as a standalone application.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public static void Main(string[] args) {
      SampleBase codeExample = new GetProductsForProductTemplate();
      Console.WriteLine(codeExample.Description);
      codeExample.Run(new DfpUser());
    }

    /// <summary>
    /// Run the code example.
    /// </summary>
    /// <param name="user">The DFP user object running the code example.</param>
    public override void Run(DfpUser user) {
      // Get the ProductService.
      ProductService productService =
          (ProductService) user.GetService(DfpService.v201602.ProductService);

      // Set the ID of the product template to filter products by.
      long productTemplateId = long.Parse(_T("INSERT_PRODUCT_TEMPLATE_ID_HERE"));

      // Create a statement to only select products that were created from a specific
      // product template.
      StatementBuilder statementBuilder = new StatementBuilder()
          .Where("WHERE productTemplateId = :productTemplateId")
          .OrderBy("name ASC")
          .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT)
          .AddValue("productTemplateId", productTemplateId);

      // Set default for page.
      ProductPage page = new ProductPage();

      try {
        do {
          // Get products by statement.
          page = productService.getProductsByStatement(statementBuilder.ToStatement());

          if (page.results != null && page.results.Length > 0) {
            int i = page.startIndex;
            foreach (Product product in page.results) {
              Console.WriteLine("{0}) Product with ID = '{1}' and name '{2}' was found.",
                  i++, product.id, product.name);
            }
          }
          statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
        } while(statementBuilder.GetOffset() < page.totalResultSetSize);
        Console.WriteLine("Number of results found: {0}", page.totalResultSetSize);
      } catch (Exception e) {
        Console.WriteLine("Failed to get products. Exception says \"{0}\"",
            e.Message);
      }
    }
  }
}
