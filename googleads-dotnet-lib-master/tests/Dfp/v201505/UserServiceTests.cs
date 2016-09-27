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
using Google.Api.Ads.Dfp.v201505;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Threading;


namespace Google.Api.Ads.Dfp.Tests.v201505 {
  /// <summary>
  /// UnitTests for <see cref="UserService"/> class.
  /// </summary>
  [TestFixture]
  public class UserServiceTests : BaseTests {
    /// <summary>
    /// UnitTests for <see cref="UserService"/> class.
    /// </summary>
    private UserService userService;

    /// <summary>
    /// Salesperson user id for running tests.
    /// </summary>
    private long salespersonId;

    /// <summary>
    /// Trafficker user id for running tests.
    /// </summary>
    private long traffickerId;

    /// <summary>
    /// Default public constructor.
    /// </summary>
    public UserServiceTests() : base() {
    }

    /// <summary>
    /// Initialize the test case.
    /// </summary>
    [SetUp]
    public void Init() {
      TestUtils utils = new TestUtils();
      userService = (UserService) user.GetService(DfpService.v201505.UserService);
      salespersonId = utils.GetSalesperson(user).id;
      traffickerId = utils.GetTrafficker(user).id;
    }

    /// <summary>
    /// Test whether we can fetch all roles.
    /// </summary>
    [Test]
    public void TestGetAllRoles() {
      Role[] roles = null;

      Assert.DoesNotThrow(delegate() {
        roles = userService.getAllRoles();
      });
      Assert.NotNull(roles);
      Assert.GreaterOrEqual(roles.Length, 0);
    }

    /// <summary>
    /// Test whether we can fetch a list of existing users that match given
    /// statement.
    /// </summary>
    [Test]
    public void TestGetUsersByStatement() {
      Statement statement = new Statement();
      statement.query = "ORDER BY name LIMIT 500";

      UserPage page = null;

      Assert.DoesNotThrow(delegate() {
        page = userService.getUsersByStatement(statement);
      });

      Assert.NotNull(page);
      Assert.NotNull(page.results);

      List<long> salesPersonIds = new List<long>();
      List<long> traffickerIds = new List<long>();

      foreach(User tempUser in page.results) {
        if (tempUser.roleName == "Salesperson") {
          salesPersonIds.Add(tempUser.id);
        } else if (tempUser.roleName == "Trafficker") {
          traffickerIds.Add(tempUser.id);
        }
      }
      Assert.Contains(salespersonId, salesPersonIds);
      Assert.Contains(traffickerId, traffickerIds);
    }

    /// <summary>
    /// Test whether we can deactivate and activate a user.
    /// </summary>
    [Test]
    public void TestPerformUserAction() {
      Statement statement = new Statement();
      statement.query = string.Format("WHERE id = {0}", salespersonId);

      DeactivateUsers action = new DeactivateUsers();

      UpdateResult result = null;

      Assert.DoesNotThrow(delegate() {
        result = userService.performUserAction(action, statement);
      });

      Assert.NotNull(result);
      Assert.AreEqual(result.numChanges, 1);

      // Activate the user again.

      Assert.DoesNotThrow(delegate() {
        result = userService.performUserAction(new ActivateUsers(), statement);
      });

      Assert.NotNull(result);
      Assert.AreEqual(result.numChanges, 1);
    }

    /// <summary>
    /// Test whether we can update a list of users.
    /// </summary>
    [Test]
    public void TestUpdateUsers() {
      Statement statement = new Statement();
      statement.query = String.Format("WHERE id = {0} LIMIT 1", salespersonId);
      User user = userService.getUsersByStatement(statement).results[0];
      user.preferredLocale = (user.preferredLocale == "fr_FR")? "en_US" : "fr_FR";

      User[] newUsers = null;

      Assert.DoesNotThrow(delegate() {
        newUsers = userService.updateUsers(new User[] {user});
      });

      Assert.NotNull(newUsers);
      Assert.AreEqual(newUsers.Length, 1);
      Assert.AreEqual(newUsers[0].id, user.id);
      Assert.AreEqual(newUsers[0].preferredLocale, user.preferredLocale);
    }
  }
}
