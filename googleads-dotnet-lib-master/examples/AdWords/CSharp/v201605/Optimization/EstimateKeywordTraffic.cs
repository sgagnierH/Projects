﻿// Copyright 2016, Google Inc. All Rights Reserved.
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

using System;
using System.Collections.Generic;

namespace Google.Api.Ads.AdWords.Examples.CSharp.v201605 {

  /// <summary>
  /// This code example gets keyword traffic estimates.
  /// </summary>
  public class EstimateKeywordTraffic : ExampleBase {

    /// <summary>
    /// Main method, to run this code example as a standalone application.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public static void Main(string[] args) {
      EstimateKeywordTraffic codeExample = new EstimateKeywordTraffic();
      Console.WriteLine(codeExample.Description);
      try {
        codeExample.Run(new AdWordsUser());
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
        return "This code example gets keyword traffic estimates.";
      }
    }

    /// <summary>
    /// Runs the code example.
    /// </summary>
    /// <param name="user">The AdWords user.</param>
    public void Run(AdWordsUser user) {
      // Get the TrafficEstimatorService.
      TrafficEstimatorService trafficEstimatorService = (TrafficEstimatorService) user.GetService(
          AdWordsService.v201605.TrafficEstimatorService);

      // Create keywords. Refer to the TrafficEstimatorService documentation for the maximum
      // number of keywords that can be passed in a single request.
      //   https://developers.google.com/adwords/api/docs/reference/latest/TrafficEstimatorService
      // [START createKeywordEstimateRequest] MOE:strip_line
      Keyword keyword1 = new Keyword();
      keyword1.text = "mars cruise";
      keyword1.matchType = KeywordMatchType.BROAD;

      Keyword keyword2 = new Keyword();
      keyword2.text = "cheap cruise";
      keyword2.matchType = KeywordMatchType.PHRASE;

      Keyword keyword3 = new Keyword();
      keyword3.text = "cruise";
      keyword3.matchType = KeywordMatchType.EXACT;

      Keyword[] keywords = new Keyword[] { keyword1, keyword2, keyword3 };

      // Create a keyword estimate request for each keyword.
      List<KeywordEstimateRequest> keywordEstimateRequests = new List<KeywordEstimateRequest>();

      foreach (Keyword keyword in keywords) {
        KeywordEstimateRequest keywordEstimateRequest = new KeywordEstimateRequest();
        keywordEstimateRequest.keyword = keyword;
        keywordEstimateRequests.Add(keywordEstimateRequest);
      }

      // Create negative keywords.
      Keyword negativeKeyword1 = new Keyword();
      negativeKeyword1.text = "moon walk";
      negativeKeyword1.matchType = KeywordMatchType.BROAD;

      KeywordEstimateRequest negativeKeywordEstimateRequest = new KeywordEstimateRequest();
      negativeKeywordEstimateRequest.keyword = negativeKeyword1;
      negativeKeywordEstimateRequest.isNegative = true;
      keywordEstimateRequests.Add(negativeKeywordEstimateRequest);
      // [END createKeywordEstimateRequest] MOE:strip_line

      // [START createAdGroupEstimateRequest] MOE:strip_line
      // Create ad group estimate requests.
      AdGroupEstimateRequest adGroupEstimateRequest = new AdGroupEstimateRequest();
      adGroupEstimateRequest.keywordEstimateRequests = keywordEstimateRequests.ToArray();
      adGroupEstimateRequest.maxCpc = new Money();
      adGroupEstimateRequest.maxCpc.microAmount = 1000000;
      // [END createAdGroupEstimateRequest] MOE:strip_line

      // [START createCampaignEstimateRequest] MOE:strip_line
      // Create campaign estimate requests.
      CampaignEstimateRequest campaignEstimateRequest = new CampaignEstimateRequest();
      campaignEstimateRequest.adGroupEstimateRequests = new AdGroupEstimateRequest[] {
          adGroupEstimateRequest};

      // Optional: Set additional criteria for filtering estimates.
      // See http://code.google.com/apis/adwords/docs/appendix/countrycodes.html
      // for a detailed list of country codes.
      Location countryCriterion = new Location();
      countryCriterion.id = 2840; //US

      // See http://code.google.com/apis/adwords/docs/appendix/languagecodes.html
      // for a detailed list of language codes.
      Language languageCriterion = new Language();
      languageCriterion.id = 1000; //en

      campaignEstimateRequest.criteria = new Criterion[] { countryCriterion, languageCriterion };
      // [END createCampaignEstimateRequest] MOE:strip_line
      
      try {
        // [START makeRequest] MOE:strip_line
        // Create the selector.
        TrafficEstimatorSelector selector = new TrafficEstimatorSelector() {
          campaignEstimateRequests = new CampaignEstimateRequest[] { campaignEstimateRequest },

          // Optional: Request a list of campaign level estimates segmented by platform.
          platformEstimateRequested = true
        };

        // Get traffic estimates.
        TrafficEstimatorResult result = trafficEstimatorService.get(selector);
        // [END makeRequest] MOE:strip_line

        // [START displayEstimates] MOE:strip_line
        // Display traffic estimates.
        if (result != null && result.campaignEstimates != null &&
            result.campaignEstimates.Length > 0) {
          CampaignEstimate campaignEstimate = result.campaignEstimates[0];

          // Display the campaign level estimates segmented by platform.
          if (campaignEstimate.platformEstimates != null) {
            foreach (PlatformCampaignEstimate platformEstimate in
                campaignEstimate.platformEstimates) {
              string platformMessage = string.Format("Results for the platform with ID: " +
                  "{0} and name : {1}.", platformEstimate.platform.id,
                  platformEstimate.platform.platformName);

              DisplayMeanEstimates(platformMessage, platformEstimate.minEstimate,
                  platformEstimate.maxEstimate);
            }
          }

          // Display the keyword estimates.
          if (campaignEstimate.adGroupEstimates != null &&
              campaignEstimate.adGroupEstimates.Length > 0) {
            AdGroupEstimate adGroupEstimate = campaignEstimate.adGroupEstimates[0];

            if (adGroupEstimate.keywordEstimates != null) {
              for (int i = 0; i < adGroupEstimate.keywordEstimates.Length; i++) {
                Keyword keyword = keywordEstimateRequests[i].keyword;
                KeywordEstimate keywordEstimate = adGroupEstimate.keywordEstimates[i];

                if (keywordEstimateRequests[i].isNegative) {
                  continue;
                }
                string kwdMessage = string.Format("Results for the keyword with text = '{0}' " +
                    "and match type = '{1}':", keyword.text, keyword.matchType);
                DisplayMeanEstimates(kwdMessage, keywordEstimate.min, keywordEstimate.max);
              }
            }
          }
        } else {
          Console.WriteLine("No traffic estimates were returned.");
        }
        // [END displayEstimates] MOE:strip_line
      } catch (Exception e) {
        throw new System.ApplicationException("Failed to retrieve traffic estimates.", e);
      }
    }

    /// <summary>
    /// Displays the mean estimates.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="minEstimate">The minimum stats estimate.</param>
    /// <param name="maxEstimate">The maximum stats estimate.</param>
    private void DisplayMeanEstimates(string message, StatsEstimate minEstimate,
        StatsEstimate maxEstimate) {
      // Find the mean of the min and max values.
      long meanAverageCpc = 0;
      double meanAveragePosition = 0;
      float meanClicks = 0;
      long meanTotalCost = 0;

      if (minEstimate != null && maxEstimate != null) {
        if (minEstimate.averageCpc != null && maxEstimate.averageCpc != null) {
          meanAverageCpc = (minEstimate.averageCpc.microAmount +
              maxEstimate.averageCpc.microAmount) / 2;
        }
        if (minEstimate.averagePositionSpecified && maxEstimate.averagePositionSpecified) {
          meanAveragePosition = (minEstimate.averagePosition + maxEstimate.averagePosition) / 2;
        }
        if (minEstimate.clicksPerDaySpecified && maxEstimate.clicksPerDaySpecified) {
          meanClicks = (minEstimate.clicksPerDay + maxEstimate.clicksPerDay) / 2;
        }
        if (minEstimate.totalCost != null && maxEstimate.totalCost != null) {
          meanTotalCost = (minEstimate.totalCost.microAmount +
              maxEstimate.totalCost.microAmount) / 2;
        }
      }

      Console.WriteLine(message);
      Console.WriteLine("  Estimated average CPC: {0}", meanAverageCpc);
      Console.WriteLine("  Estimated ad position: {0:0.00}", meanAveragePosition);
      Console.WriteLine("  Estimated daily clicks: {0}", meanClicks);
      Console.WriteLine("  Estimated daily cost: {0}", meanTotalCost);
    }
  }
}