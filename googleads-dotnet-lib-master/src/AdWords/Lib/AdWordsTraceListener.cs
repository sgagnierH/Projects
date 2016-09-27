// Copyright 2011, Google Inc. All Rights Reserved.
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
using Google.Api.Ads.Common.Logging;
using Google.Api.Ads.Common.Util;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml;
using System.Xml.Serialization;

namespace Google.Api.Ads.AdWords.Lib {
  /// <summary>
  /// Listens to SOAP messages sent and received by this library.
  /// </summary>
  public class AdWordsTraceListener : TraceListener {
    /// <summary>
    /// The singleton instance.
    /// </summary>
    protected static AdWordsTraceListener instance = new AdWordsTraceListener();

    /// <summary>
    /// Protected constructor.
    /// </summary>
    protected AdWordsTraceListener() : base(new AdWordsAppConfig()) {
    }

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static SoapListener Instance {
      get {
        return instance;
      }
    }

    /// <summary>
    /// Gets the summary request logs.
    /// </summary>
    /// <param name="soapRequest">The request xml for this SOAP call.</param>
    /// <returns>The summary request logs.</returns>
    protected override string GetSummaryRequestLogs(string soapRequest) {
      XmlDocument xDoc = SerializationUtilities.LoadXml(soapRequest);
      XmlNamespaceManager xmlns = new XmlNamespaceManager(xDoc.NameTable);
      xmlns.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
      XmlNode methodNode =
          xDoc.SelectSingleNode("soap:Envelope/soap:Body/*", xmlns);
      string operators = "None";
      if (methodNode.Name == "mutate") {
        StringBuilder builder = new StringBuilder();
        foreach (XmlNode child in methodNode.ChildNodes) {
          if (child.Name == "operations") {
            foreach (XmlNode grandChild in child.ChildNodes) {
              if (grandChild.Name == "operator") {
                builder.Append(grandChild.InnerText + "|");
              }
            }
          }
        }
        operators = builder.ToString().TrimEnd('|');
      }
      return string.Format(CultureInfo.InvariantCulture, "method={0},operator={1}",
          methodNode.Name, operators);
    }

    /// <summary>
    /// Gets the summary response logs.
    /// </summary>
    /// <param name="soapResponse">The response xml for this SOAP call.</param>
    /// <returns>The summary response logs.</returns>
    protected override string GetSummaryResponseLogs(string soapResponse) {
      XmlDocument xDoc = SerializationUtilities.LoadXml(soapResponse);
      XmlNamespaceManager xmlns = new XmlNamespaceManager(xDoc.NameTable);
      xmlns.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
      XmlNodeList childNodes =
          xDoc.SelectNodes("soap:Envelope/soap:Header/*", xmlns);
      if (childNodes.Count == 1 && childNodes[0].Name == "ResponseHeader") {
        childNodes = childNodes[0].ChildNodes;
      }
      StringBuilder responseText = new StringBuilder();
      foreach (XmlNode childNode in childNodes) {
        if (childNode is XmlElement) {
          responseText.AppendFormat("{0}={1},", childNode.Name, childNode.InnerText);
        }
      }
      return responseText.ToString().TrimEnd(',');
    }

    /// <summary>
    /// Gets a list of fields to be masked in xml logs.
    /// </summary>
    /// <returns>The list of fields to be masked.</returns>
    protected override ISet<string> GetFieldsToMask() {
      return new HashSet<string>(new string[] { "developerToken", "Authorization" },
          StringComparer.OrdinalIgnoreCase);
    }
  }
}
