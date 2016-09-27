// Copyright 2016, Google Inc. All Rights Reserved.
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

using Google.Api.Ads.AdWords.Lib;
using Google.Api.Ads.AdWords.v201605;
using Google.Api.Ads.Common.Util;

using System;
using System.Collections.Generic;
using System.IO;

namespace Google.Api.Ads.AdWords.Examples.CSharp.v201605 {
  /// <summary>
  /// This code example adds an image representing the ad using the MediaService
  /// and then adds a responsive display ad to an ad group. To get ad groups,
  /// run GetAdGroups.cs.
  /// </summary>
  public class AddResponsiveDisplayAd : ExampleBase {
    /// <summary>
    /// Main method, to run this code example as a standalone application.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public static void Main(string[] args) {
      AddResponsiveDisplayAd codeExample = new AddResponsiveDisplayAd();
      Console.WriteLine(codeExample.Description);
      try {
        long adGroupId = long.Parse("INSERT_ADGROUP_ID_HERE");
        codeExample.Run(new AdWordsUser(), adGroupId);
      } catch (Exception e) {
        Console.WriteLine("An exception occurred while running this code example. {0}",
            ExampleUtilities.FormatException(e));
      }
    }

    /// <summary>
    /// Returns a description about the code example.
    /// </summary>
    public override string Description {
      get {
        return "This code example adds an image representing the ad using the MediaService " +
            "and then adds a responsive display ad to an ad group. To get ad groups, " +
            "run GetAdGroups.cs.";
      }
    }

    /// <summary>
    /// Runs the code example.
    /// </summary>
    /// <param name="user">The AdWords user.</param>
    /// <param name="adGroupId">Id of the ad group to which ads are added.
    /// </param>
    public void Run(AdWordsUser user, long adGroupId) {
      // [START addResponsiveDisplayAd] MOE:strip_line
      // Get the AdGroupAdService.
      AdGroupAdService adGroupAdService =
          (AdGroupAdService) user.GetService(AdWordsService.v201605.AdGroupAdService);

      // Get the MediaService.
      MediaService mediaService =
          (MediaService) user.GetService(AdWordsService.v201605.MediaService);

      try {
        // Create the image.
        Image image = new Image() {
          data = MediaUtilities.GetAssetDataFromUrl("https://goo.gl/3b9Wfh", user.Config),
          type = MediaMediaType.IMAGE
        };

        // Upload the image.
        Image newImage = (Image) mediaService.upload(new Media[] { image })[0];

        // Create a responsive display ad.
        ResponsiveDisplayAd responsiveDisplayAd = new ResponsiveDisplayAd();
        responsiveDisplayAd.marketingImage = newImage;
        responsiveDisplayAd.shortHeadline = "Travel";
        responsiveDisplayAd.longHeadline = "Travel the World";
        responsiveDisplayAd.description = "Take to the air!";
        responsiveDisplayAd.businessName = "Google";
        responsiveDisplayAd.finalUrls = new string[] { "http://www.example.com" };

        // Create ad group ad.
        AdGroupAd adGroupAd = new AdGroupAd() {
          adGroupId = adGroupId,
          ad = responsiveDisplayAd,
          status = AdGroupAdStatus.PAUSED
        };

        // Create operation.
        AdGroupAdOperation operation = new AdGroupAdOperation() {
          operand = adGroupAd,
          @operator = Operator.ADD
        };

        // Make the mutate request.
        AdGroupAdReturnValue result = adGroupAdService.mutate(
            new AdGroupAdOperation[] { operation });

        // Display results.
        if (result != null && result.value != null) {
          foreach (AdGroupAd newAdGroupAd in result.value) {
            ResponsiveDisplayAd newAd = newAdGroupAd.ad as ResponsiveDisplayAd;
            Console.WriteLine("Responsive display ad with ID '{0}' and short headline '{1}'" +
                " was added.", newAd.id, newAd.shortHeadline);
          }
        } else {
          Console.WriteLine("No responsive display ads were created.");
        }
      } catch (Exception e) {
        throw new System.ApplicationException("Failed to create responsive display ad.", e);
      }
      // [END addResponsiveDisplayAd] MOE:strip_line
    }
  }
}
