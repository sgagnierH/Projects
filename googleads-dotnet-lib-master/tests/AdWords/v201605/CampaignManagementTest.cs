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

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using CSharpExamples = Google.Api.Ads.AdWords.Examples.CSharp.v201605;
using VBExamples = Google.Api.Ads.AdWords.Examples.VB.v201605;

namespace Google.Api.Ads.AdWords.Tests.v201605 {
  /// <summary>
  /// Test cases for all the code examples under v201605\CampaignManagement.
  /// </summary>
  class CampaignManagementTest : VersionedExampleTestsBase {
    long campaignId;
    long adGroupId;
    long criterionId;
    long adId;
    long draftId;

    long labelId;

    const Double BID_MODIFIER = 1.5;

    /// <summary>
    /// Inits this instance.
    /// </summary>
    [SetUp]
    public void Init() {
      campaignId = utils.CreateSearchCampaign(user, BiddingStrategyType.MANUAL_CPC);
      labelId = utils.CreateLabel(user);
      adGroupId = utils.CreateAdGroup(user, campaignId);
      criterionId = utils.CreateKeyword(user, adGroupId);
      adId = utils.CreateTextAd(user, adGroupId, false);
      draftId = utils.AddDraft(user, campaignId);
    }

    /// <summary>
    /// Tests the AddCampaignLabels VB.NET code example.
    /// </summary>
    [Test]
    public void TestAddCampaignLabelsVBExample() {
      long campaignId1ForLabeling = utils.CreateSearchCampaign(user,
          BiddingStrategyType.MANUAL_CPC);
      long campaignId2ForLabeling = utils.CreateSearchCampaign(user,
          BiddingStrategyType.MANUAL_CPC);
      RunExample(delegate() {
        new VBExamples.AddCampaignLabels().Run(user, campaignId1ForLabeling,
            campaignId2ForLabeling, labelId);
      });
    }

    /// <summary>
    /// Tests the AddCampaignLabels C# code example.
    /// </summary>
    [Test]
    public void TestAddCampaignLabelsCSharpExample() {
      long campaignId1ForLabeling = utils.CreateSearchCampaign(user,
          BiddingStrategyType.MANUAL_CPC);
      long campaignId2ForLabeling = utils.CreateSearchCampaign(user,
          BiddingStrategyType.MANUAL_CPC);
      RunExample(delegate() {
        new CSharpExamples.AddCampaignLabels().Run(user, campaignId1ForLabeling,
            campaignId2ForLabeling, labelId);
      });
    }

    /// <summary>
    /// Tests the GetCampaignsByLabel VB.NET code example.
    /// </summary>
    [Test]
    public void TestGetCampaignsByLabelVBExample() {
      RunExample(delegate() {
        new VBExamples.GetCampaignsByLabel().Run(user, labelId);
      });
    }

    /// <summary>
    /// Tests the GetCampaignsByLabel C# code example.
    /// </summary>
    [Test]
    public void TestGetCampaignsByLabelCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.GetCampaignsByLabel().Run(user, labelId);
      });
    }

    /// <summary>
    /// Tests the AddExperiment VB.NET code example.
    /// </summary>
    [Test]
    public void TestAddExperimentVBExample() {
      RunExample(delegate() {
        new VBExamples.AddExperiment().Run(user, campaignId, adGroupId, criterionId);
      });
    }

    /// <summary>
    /// Tests the AddExperiment C# code example.
    /// </summary>
    [Test]
    public void TestAddExperimentCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.AddExperiment().Run(user, campaignId, adGroupId, criterionId);
      });
    }

    /// <summary>
    /// Tests the GetAllDisapprovedAds VB.NET code example.
    /// </summary>
    [Test]
    public void TestGetAllDisapprovedAdsVBExample() {
      RunExample(delegate() {
        new VBExamples.GetAllDisapprovedAds().Run(user, campaignId);
      });
    }

    /// <summary>
    /// Tests the GetAllDisapprovedAds C# code example.
    /// </summary>
    [Test]
    public void TestGetAllDisapprovedAdsCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.GetAllDisapprovedAds().Run(user, campaignId);
      });
    }

    /// <summary>
    /// Tests the GetAllDisapprovedAdsWithAwql VB.NET code example.
    /// </summary>
    [Test]
    public void TestGetAllDisapprovedAdsWithAwqlVBExample() {
      RunExample(delegate() {
        new VBExamples.GetAllDisapprovedAdsWithAwql().Run(user, campaignId);
      });
    }

    /// <summary>
    /// Tests the GetAllDisapprovedAdsWithAwql C# code example.
    /// </summary>
    [Test]
    public void TestGetAllDisapprovedAdsWithAwqlCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.GetAllDisapprovedAdsWithAwql().Run(user, campaignId);
      });
    }

    /// <summary>
    /// Tests the PromoteExperiment VB.NET code example.
    /// </summary>
    [Test]
    public void TestPromoteExperimentVBExample() {
      long experimentId = CreateExperimentForPromotion();
      RunExample(delegate() {
        new VBExamples.PromoteExperiment().Run(user, experimentId);
      });
    }

    /// <summary>
    /// Tests the PromoteExperiment C# code example.
    /// </summary>
    [Test]
    public void TestPromoteExperimentCSharpExample() {
      long experimentId = CreateExperimentForPromotion();
      RunExample(delegate() {
        new CSharpExamples.PromoteExperiment().Run(user, experimentId);
      });
    }

    /// <summary>
    /// Tests the SetAdParameters VB.NET code example.
    /// </summary>
    [Test]
    public void TestSetAdParametersVBExample() {
      RunExample(delegate() {
        new VBExamples.SetAdParameters().Run(user, adGroupId, criterionId);
      });
    }

    /// <summary>
    /// Tests the SetAdParameters C# code example.
    /// </summary>
    [Test]
    public void TestSetAdParametersCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.SetAdParameters().Run(user, adGroupId, criterionId);
      });
    }

    /// <summary>
    /// Tests the ValidateTextAd VB.NET code example.
    /// </summary>
    [Test]
    public void TestValidateTextAdVBExample() {
      RunExample(delegate() {
        new VBExamples.ValidateTextAd().Run(user, adGroupId);
      });
    }

    /// <summary>
    /// Tests the ValidateTextAd C# code example.
    /// </summary>
    [Test]
    public void TestValidateTextAdCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.ValidateTextAd().Run(user, adGroupId);
      });
    }

    /// <summary>
    /// Tests the SetBidModifier C# code example.
    /// </summary>
    [Test]
    public void TestSetBidModifierCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.SetBidModifier().Run(user, campaignId, BID_MODIFIER);
      });
    }

    /// <summary>
    /// Tests the SetBidModifier VB.NET code example.
    /// </summary>
    [Test]
    public void TestSetBidModifierVBExample() {
      RunExample(delegate() {
        new VBExamples.SetBidModifier().Run(user, campaignId, BID_MODIFIER);
      });
    }

    /// <summary>
    /// Tests the AddDraft C# code example.
    /// </summary>
    [Test]
    public void TestAddDraftCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.AddDraft().Run(user, campaignId);
      });
    }

    /// <summary>
    /// Tests the AddDraft VB.NET code example.
    /// </summary>
    [Test]
    public void TestAddDraftVBExample() {
      RunExample(delegate() {
        new VBExamples.AddDraft().Run(user, campaignId);
      });
    }

    #region tests that take a long while.

    /// <summary>
    /// Tests the AddCompleteCampaignUsingBatchJob C# code example.
    /// </summary>
    [Test]
    public void TestAddCompleteCampaignUsingBatchJobCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.AddCompleteCampaignsUsingBatchJob().Run(user);
      });
    }

    /// <summary>
    /// Tests the AddCompleteCampaignUsingBatchJob VB.NET code example.
    /// </summary>
    [Test]
    public void TestAddCompleteCampaignUsingBatchJobVBExample() {
      RunExample(delegate() {
        new VBExamples.AddCompleteCampaignsUsingBatchJob().Run(user);
      });
    }

    /// <summary>
    /// Tests the AddKeywordsUsingIncrementalBatchJob C# code example.
    /// </summary>
    [Test]
    public void TestAddKeywordsUsingIncrementalBatchJobCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.AddKeywordsUsingIncrementalBatchJob().Run(user, adGroupId);
      });
    }

    /// <summary>
    /// Tests the AddKeywordsUsingIncrementalBatchJob VB.NET code example.
    /// </summary>
    [Test]
    public void TestAddKeywordsUsingIncrementalBatchJobVBExample() {
      RunExample(delegate() {
        new VBExamples.AddKeywordsUsingIncrementalBatchJob().Run(user, adGroupId);
      });
    }

    /// <summary>
    /// Tests the AddTrial C# code example.
    /// </summary>
    [Test]
    public void TestAddTrialCSharpExample() {
      RunExample(delegate() {
        new CSharpExamples.AddTrial().Run(user, draftId, campaignId);
      });
    }

    /// <summary>
    /// Tests the AddTrial VB.NET code example.
    /// </summary>
    [Test]
    public void TestAddTrialVBExample() {
      RunExample(delegate() {
        new VBExamples.AddTrial().Run(user, draftId, campaignId);
      });
    }

    /// <summary>
    /// Tests the GraduateTrial C# code example.
    /// </summary>
    [Test]
    public void TestGraduateTrialCSharpExample() {
      // Moving the initialization code here, since creating a trial ID is
      // very expensive and slows down other tests significantly.
      long trialId = utils.CreateTrial(user, draftId, campaignId);
      RunExample(delegate() {
        new CSharpExamples.GraduateTrial().Run(user, trialId);
      });
    }

    /// <summary>
    /// Tests the GraduateTrial VB.NET code example.
    /// </summary>
    [Test]
    public void TestGraduateTrialVBExample() {
      // Moving the initialization code here, since creating a trial ID is
      // very expensive and slows down other tests significantly.
      long trialId = utils.CreateTrial(user, draftId, campaignId);
      RunExample(delegate() {
        new VBExamples.GraduateTrial().Run(user, trialId);
      });
    }

    #endregion


    /// <summary>
    /// Create an experiment for promotion.
    /// </summary>
    /// <returns>Experiment id for promotion.</returns>
    /// <remarks> This code cannot be added in Init(), since there can be only
    /// one experiment per campaign, and if we add this to Init(), then
    /// AddExperiment tests will fail.</remarks>
    private long CreateExperimentForPromotion() {
      return utils.AddExperiment(user, campaignId, adGroupId, criterionId);
    }
  }
}
