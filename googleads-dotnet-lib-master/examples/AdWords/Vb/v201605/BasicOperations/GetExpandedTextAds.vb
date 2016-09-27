' Copyright 2016, Google Inc. All Rights Reserved.
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
Imports Google.Api.Ads.AdWords.v201605

Imports System
Imports System.Collections.Generic
Imports System.IO

Namespace Google.Api.Ads.AdWords.Examples.VB.v201605
  ''' <summary>
  ''' This code example retrieves all expanded text ads given an existing ad
  ''' group. To add expanded text ads to an existing ad group, run
  ''' AddExpandedTextAds.vb.
  ''' </summary>
  Public Class GetExpandedTextAds
    Inherits ExampleBase
    ''' <summary>
    ''' Main method, to run this code example as a standalone application.
    ''' </summary>
    ''' <param name="args">The command line arguments.</param>
    Public Shared Sub Main(ByVal args As String())
      Dim codeExample As New GetExpandedTextAds
      Console.WriteLine(codeExample.Description)
      Try
        Dim adGroupId As Long = Long.Parse("INSERT_ADGROUP_ID_HERE")
        codeExample.Run(New AdWordsUser, adGroupId)
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
        Return "This code example retrieves all expanded text ads given an existing ad group. " & _
            "To add expanded text ads to an existing ad group, run AddExpandedTextAds.vb."
      End Get
    End Property

    ''' <summary>
    ''' Runs the code example.
    ''' </summary>
    ''' <param name="user">The AdWords user.</param>
    ''' <param name="adGroupId">Id of the ad group from which expanded text ads
    ''' are retrieved.</param>
    Public Sub Run(ByVal user As AdWordsUser, ByVal adGroupId As Long)
      ' [START getExpandedTextAds] MOE:strip_line
      ' Get the AdGroupAdService.
      Dim service As AdGroupAdService = CType(user.GetService( _
          AdWordsService.v201605.AdGroupAdService), AdGroupAdService)

      ' Create a selector.
      Dim selector As New Selector

      selector.fields = New String() {
        ExpandedTextAd.Fields.Id, AdGroupAd.Fields.Status, ExpandedTextAd.Fields.HeadlinePart1,
        ExpandedTextAd.Fields.HeadlinePart2, ExpandedTextAd.Fields.Description
      }

      selector.ordering = New OrderBy() {OrderBy.Asc(TextAd.Fields.Id)}

      selector.predicates = New Predicate() {
        Predicate.Equals(AdGroupAd.Fields.AdGroupId, adGroupId),
        Predicate.Equals("AdType", "EXPANDED_TEXT_AD"),
        Predicate.In(AdGroupAd.Fields.Status, New String() {
          AdGroupAdStatus.ENABLED.ToString(),
          AdGroupAdStatus.PAUSED.ToString(),
          AdGroupAdStatus.DISABLED.ToString()
        })
      }

      ' Select the selector paging.
      selector.paging = Paging.Default

      Dim page As New AdGroupAdPage

      Try
        Do
          ' Get the expanded text ads.
          page = service.get(selector)

          ' Display the results.
          If ((Not page Is Nothing) AndAlso (Not page.entries Is Nothing)) Then
            Dim i As Integer = selector.paging.startIndex

            For Each adGroupAd As AdGroupAd In page.entries
              Dim expandedTextAd As ExpandedTextAd = CType(adGroupAd.ad, ExpandedTextAd)
              Console.WriteLine("{0} : Expanded text ad with ID '{1}', headline '{2} - {3}' " & _
                  "and description '{4} was found.", i + 1, expandedTextAd.id,
                  expandedTextAd.headlinePart1, expandedTextAd.headlinePart2,
                  expandedTextAd.description)
            Next
            i += 1
          End If
          selector.paging.IncreaseOffset()
        Loop While (selector.paging.startIndex < page.totalNumEntries)
        Console.WriteLine("Number of expanded text ads found: {0}", page.totalNumEntries)
      Catch e As Exception
        Throw New System.ApplicationException("Failed to get expanded text ads.", e)
      End Try
      ' [END getExpandedTextAds] MOE:strip_line
    End Sub
  End Class
End Namespace
