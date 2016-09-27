// Copyright 2014, Google Inc. All Rights Reserved.
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

using Google.Api.Ads.Common.Lib;
using Google.Api.Ads.Common.Util;
using Google.Api.Ads.Dfp.Headers;

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml;
using System.Xml.Serialization;

namespace Google.Api.Ads.Dfp.Lib {
  /// <summary>
  /// Base class for DFP API services.
  /// </summary>
  public class DfpSoapClient : AdsSoapClient {
    /// <summary>
    /// The error thrown when an auth token expires.
    /// </summary>
    private const string COOKIE_INVALID_ERROR = "AuthenticationError.GOOGLE_ACCOUNT_COOKIE_INVALID";

    /// <summary>
    /// The error thrown when an oauth token expires.
    /// </summary>
    private const string OAUTH_TOKEN_EXPIRED_ERROR = "AuthenticationError.AUTHENTICATION_FAILED";

    /// <summary>
    /// Gets a custom exception that wraps the SOAP exception thrown
    /// by the server.
    /// </summary>
    /// <param name="exception">SOAPException that was thrown by the server.</param>
    /// <returns>A custom exception object that wraps the SOAP exception.
    /// </returns>
    protected override Exception GetCustomException(SoapException exception) {
      string defaultNs = GetDefaultNamespace();
      if (!string.IsNullOrEmpty(defaultNs)) {
        // Extract the ApiExceptionFault node.
        XmlElement faultNode = GetFaultNode(exception, defaultNs, "ApiExceptionFault");

        if (faultNode != null) {
          try {
            DfpApiException dfpException = new DfpApiException(
                SerializationUtilities.DeserializeFromXmlTextCustomRootNs(
                    faultNode.OuterXml,
                    Assembly.GetExecutingAssembly().GetType(
                        this.GetType().Namespace + ".ApiException"), defaultNs,
                        "ApiExceptionFault"),
                exception.Message, exception);
            return dfpException;
          } catch (Exception) {
            // deserialization failed, but we can safely ignore it.
          }
        }
      }
      return new DfpApiException(null, exception.Message, exception);
    }

    /// <summary>
    /// Initializes the service before MakeApiCall.
    /// </summary>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="parameters">The method parameters.</param>
    protected override void InitForCall(string methodName, object[] parameters) {
      DfpAppConfig config = this.User.Config as DfpAppConfig;
      string oAuthHeader = null;
      RequestHeader header = GetRequestHeader();

      if (header == null) {
        throw new DfpApiException(null, DfpErrorMessages.FailedToSetAuthorizationHeader);
      }

      if (!(this.GetType().Name == "NetworkService" && (methodName == "getAllNetworks"
          || methodName == "makeTestNetwork"))) {
        if (string.IsNullOrEmpty(header.networkCode)) {
          throw new SoapHeaderException("networkCode header is required in all API versions. " +
              "The only exceptions are NetworkService.getAllNetworks and " +
              "NetworkService.makeTestNetwork.", XmlQualifiedName.Empty);
        }
      }

      if (string.IsNullOrEmpty(header.applicationName) || header.applicationName.Contains(
          DfpAppConfig.DEFAULT_APPLICATION_NAME)) {
        throw new ApplicationException(DfpErrorMessages.RequireValidApplicationName);
      }

      if (config.AuthorizationMethod == DfpAuthorizationMethod.OAuth2) {
        if (this.User.OAuthProvider != null) {
          oAuthHeader = this.User.OAuthProvider.GetAuthHeader();
        } else {
          throw new DfpApiException(null, DfpErrorMessages.OAuthProviderCannotBeNull);
        }
      } else {
        throw new DfpApiException(null, DfpErrorMessages.UnsupportedAuthorizationMethod);
      }

      ContextStore.AddKey("OAuthHeader", oAuthHeader);
      base.InitForCall(methodName, parameters);
    }

    /// <summary>
    /// Creates a WebRequest instance for the specified url.
    /// </summary>
    /// <param name="uri">The Uri to use when creating the WebRequest.</param>
    /// <returns>
    /// The WebRequest instance.
    /// </returns>
    protected override WebRequest GetWebRequest(Uri uri) {
      WebRequest request = base.GetWebRequest(uri);
      string oAuthHeader = (string) ContextStore.GetValue("OAuthHeader");
      if (!string.IsNullOrEmpty(oAuthHeader)) {
        request.Headers["Authorization"] = oAuthHeader;
      }
      return request;
    }

    /// <summary>
    /// Gets the request header.
    /// </summary>
    /// <returns>The request header.</returns>
    private RequestHeader GetRequestHeader() {
      return (RequestHeader) this.GetType().GetProperty("RequestHeader").GetValue(this, null);
    }
  }
}
