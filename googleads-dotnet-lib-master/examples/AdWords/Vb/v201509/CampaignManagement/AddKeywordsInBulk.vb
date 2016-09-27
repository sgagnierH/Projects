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
Imports System.Text.RegularExpressions
Imports System.Threading

Namespace Google.Api.Ads.AdWords.Examples.VB.v201509
  ''' <summary>
  ''' This code example shows how to add keywords in bulk using the
  ''' MutateJobService.
  ''' </summary>
  Public Class AddKeywordsInBulk
    Inherits ExampleBase
    ''' <summary>
    ''' Main method, to run this code example as a standalone application.
    ''' </summary>
    ''' <param name="args">The command line arguments.</param>
    Public Shared Sub Main(ByVal args As String())
      Dim codeExample As New AddKeywordsInBulk
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
        Return "This code example shows how to add keywords in bulk using the MutateJobService."
      End Get
    End Property

    ''' <summary>
    ''' Runs the code example.
    ''' </summary>
    ''' <param name="user">The AdWords user.</param>
    ''' <param name="adGroupId">Id of the ad groups to which keywords are
    ''' added.</param>
    Public Sub Run(ByVal user As AdWordsUser, ByVal adGroupId As Long)
      ' Get the MutateJobService.
      Dim mutateJobService As MutateJobService = CType(user.GetService( _
          AdWordsService.v201509.MutateJobService), MutateJobService)

      Const RETRY_INTERVAL As Integer = 30
      Const RETRIES_COUNT As Integer = 30
      Const KEYWORD_NUMBER As Integer = 100
      Const INDEX_REGEX As String = "operations\[(\d+)\].operand"

      Dim operations As New List(Of Operation)

      ' Create AdGroupCriterionOperation to add keywords.
      For i As Integer = 0 To KEYWORD_NUMBER
        Dim keyword As New Keyword
        keyword.text = String.Format("mars cruise {0}", i)
        keyword.matchType = KeywordMatchType.BROAD

        Dim criterion As New BiddableAdGroupCriterion
        criterion.adGroupId = adGroupId
        criterion.criterion = keyword

        Dim adGroupCriterionOperation As New AdGroupCriterionOperation
        adGroupCriterionOperation.operator = [Operator].ADD
        adGroupCriterionOperation.operand = criterion

        operations.Add(adGroupCriterionOperation)
      Next i

      Dim policy As New BulkMutateJobPolicy
      ' You can specify up to 3 job IDs that must successfully complete before
      ' this job can be processed.
      policy.prerequisiteJobIds = New Long() {}

      Dim job As SimpleMutateJob = mutateJobService.mutate(operations.ToArray, policy)

      ' Wait for the job to complete.
      Dim completed As Boolean = False
      Dim retryCount As Integer = 0
      Console.WriteLine("Retrieving job status...")

      While (completed = False AndAlso retryCount < RETRIES_COUNT)
        Dim selector As New BulkMutateJobSelector
        selector.jobIds = New Long() {job.id}

        Try
          Dim allJobs As Job() = mutateJobService.get(selector)
          If ((Not allJobs Is Nothing) AndAlso (allJobs.Length > 0)) Then
            job = CType(allJobs(0), SimpleMutateJob)
            If ((job.status = BasicJobStatus.COMPLETED) OrElse _
                (job.status = BasicJobStatus.FAILED)) Then
              completed = True
              Exit While
            Else
              Console.WriteLine("{0}: Current status is {1}, waiting {2} seconds to retry...", _
                  retryCount, job.status, RETRY_INTERVAL)
              Thread.Sleep(RETRY_INTERVAL * 1000)
              retryCount = retryCount + 1
            End If
          End If
        Catch e As Exception
          Throw New System.ApplicationException(String.Format("Failed to fetch simple mutate " & _
                "job with id = {0}.", job.id), e)
          Return
        End Try
      End While

      If (job.status = BasicJobStatus.COMPLETED) Then
        ' Handle cases where the job completed.

        ' Create the job selector.
        Dim selector As New BulkMutateJobSelector
        selector.jobIds = New Long() {job.id}

        ' Get the job results.
        Dim jobResult As JobResult = mutateJobService.getResult(selector)
        If Not jobResult Is Nothing Then
          Dim results As SimpleMutateResult = jobResult.Item
          If Not results Is Nothing Then
            'Display the results.
            If Not results.results Is Nothing Then
              For i As Integer = 0 To results.results.Length - 1
                Dim operand As Operand = results.results(i)
                Dim status As String
                If TypeOf operand.Item Is PlaceHolder Then
                  status = "FAILED"
                Else
                  status = "SUCCEEDED"
                End If
                Console.WriteLine("Operation {0} - {1}", i, status)
              Next i
            End If

            ' Display the errors.
            If Not results.errors Is Nothing Then
              For Each apiError As ApiError In results.errors
                Dim match As Match = Regex.Match(apiError.fieldPath, INDEX_REGEX, _
                    RegexOptions.IgnoreCase)
                Dim index As String = "???"
                If (match.Success) Then
                  index = match.Groups(1).Value
                End If
                Console.WriteLine("Operation index {0} failed due to reason: '{1}', " & _
                    "trigger: '{2}'", index, apiError.errorString, apiError.trigger)
              Next
            End If
          End If
        End If
        Console.WriteLine("Job completed successfully!")
      ElseIf (job.status = BasicJobStatus.FAILED) Then
        ' Handle the cases where job failed.
        Console.WriteLine("Job failed with reason: " & job.failureReason.ToString())
      ElseIf (job.status = BasicJobStatus.PROCESSING OrElse job.status = BasicJobStatus.PENDING) _
          Then
        ' Handle the cases where job didn't complete after wait period.
        Console.WriteLine("Job did not complete in {0} secconds.", RETRY_INTERVAL * RETRIES_COUNT)
      End If
    End Sub
  End Class
End Namespace
