﻿// Copyright 2015, Google Inc. All Rights Reserved.
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

using Google.Api.Ads.Common.Util;

namespace Google.Api.Ads.AdWords.Util.Shopping {

  /// <summary>
  /// NodeState implementation for <see cref="NodeType.BIDDABLE_UNIT"/>.
  /// </summary>
  internal class BiddableUnitState : NodeState {

    /// <summary>
    /// The bid in micros
    /// </summary>
    private long bidInMicros;

    /// <summary>
    /// A flag to determine whether bids in micros is specified or not.
    /// </summary>
    private bool bidsInMicrosSpecified = false;

    /// <summary>
    /// Gets or sets the bid in micros.
    /// </summary>
    internal override long BidInMicros {
      get {
        return bidInMicros;
      }
      set {
        PreconditionUtilities.CheckArgument(value > 0L,
            string.Format("Invalid bid: {0}. Bid must be null or > 0.", value));
        this.bidInMicros = value;
        this.BidInMicrosSpecified = true;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether bid in micros is specified or
    /// not.
    /// </summary>
    internal override bool BidInMicrosSpecified {
      get {
        return bidsInMicrosSpecified;
      }
      set {
        bidsInMicrosSpecified = value;
      }
    }

    /// <summary>
    /// Gets the NodeType for this state.
    /// </summary>
    internal override NodeType NodeType {
      get {
        return NodeType.BIDDABLE_UNIT;
      }
    }
  }
}
