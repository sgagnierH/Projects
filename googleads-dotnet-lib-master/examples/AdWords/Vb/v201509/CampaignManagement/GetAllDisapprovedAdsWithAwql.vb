' Copyright 2015, Google Inc. All Rights Reserved.
'
' Licensed under the Apache License, Version 2.0 (the "License");
' you may not use this file except in compliance with the License.
' You may obtain a copy of the License at
'
'     http://www.apache.org/licenses/LICENSE-2.0
'
' Unless required by applicable law or agreed to in writing, software
' distributed under the License is distributed on an "AS IS" BASIS,
' WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
' See the License for the specific language governing permissions and
' limitations under the License.

Imports Google.Api.Ads.AdWords.Lib
Imports Google.Api.Ads.AdWords.v201509

Imports System
Imports System.Collections.Generic
Imports System.IO

Namespace Google.Api.Ads.AdWords.Examples.VB.v201509
  ''' <summary>
  ''' This code example retrieves all the disapproved ads in a given campaign
  ''' using an AWQL query. See
  ''' https://developers.google.com/adwords/api/docs/guides/awql for AWQL
  ''' documentation.
  ''' </summary>
  Public Class GetAllDisapprovedAdsWithAwql
    Inherits ExampleBase
    ''' <summary>
    ''' Main method, to run this code example as a standalone application.
    ''' </summary>
    ''' <param name="args">The command line arguments.</param>
    Public Shared Sub Main(ByVal args As String())
      Dim codeExample As New GetAllDisapprovedAdsWithAwql
      Console.WriteLine(codeExample.Description)
      Try
        Dim campaignId As Long = Long.Parse("INSERT_CAMPAIGN_ID_HERE")
        codeExample.Run(New AdWordsUser, campaignId)
      Catch e As Exception
        Console.WriteLine("An exception occurred while running this code example. {0}", _
            ExampleUtilities.FormatException(e))
      End Try
    End Sub

    ''' <summary>
    ''' Returns a description about the code example.
    ''' </summary>
    Public Overrides ReadOnly Property Description() As String
      Get
        Return "This code example retrieves all the disapproved ads in a given campaign using " & _
            "an AWQL query. See https://developers.google.com/adwords/api/docs/guides/awql for " & _
            "AWQL documentation."
      End Get
    End Property

    ''' <summary>
    ''' Runs the code example.
    ''' </summary>
    ''' <param name="user">The AdWords user.</param>
    ''' <param name="campaignId">Id of the campaign for which disapproved ads
    ''' are retrieved.</param>
    Public Sub Run(ByVal user As AdWordsUser, ByVal campaignId As Long)
      ' Get the AdGroupAdService.
      Dim service As AdGroupAdService = CType(user.GetService( _
          AdWordsService.v201509.AdGroupAdService), AdGroupAdService)

      ' Get all the disapproved ads for this campaign.
      Dim query As String = String.Format("SELECT Id, AdGroupAdDisapprovalReasons WHERE " & _
          "CampaignId = {0} AND AdGroupCreativeApprovalStatus = DISAPPROVED ORDER BY Id", _
          campaignId)

      Dim offset As Long = 0
      Dim pageSize As Long = 500

      Dim page As New AdGroupAdPage()

      Try
        Do
          Dim queryWithPaging As String = String.Format("{0} LIMIT {1}, {2}", query, offset, _
              pageSize)

          ' Get the disapproved ads.
          page = service.query(queryWithPaging)

          ' Display the results.
          If ((Not page Is Nothing) AndAlso (Not page.entries Is Nothing)) Then
            Dim i As Integer = CInt(offset)
            For Each adGroupAd As AdGroupAd In page.entries
              Console.WriteLine("{0}) Ad id {1} has been disapproved for the following " & _
                  "reason(s):", i, adGroupAd.ad.id)
              For Each reason As String In adGroupAd.disapprovalReasons
                Console.WriteLine("    {0}", reason)
              Next
              i = i + 1
            Next
          End If

          offset = offset + pageSize
        Loop While (offset < page.totalNumEntries)
        Console.WriteLine("Number of disapproved ads found: {0}", page.totalNumEntries)
      Catch e As Exception
        Throw New System.ApplicationException("Failed to get disapproved ads.", e)
      End Try
    End Sub
  End Class
End Namespace
