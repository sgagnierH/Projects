using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Api.Ads.Dfp.Lib;
using Google.Api.Ads.Dfp.Util.v201508;
using Google.Api.Ads.Dfp.v201508;
using Google.Api.Ads.Common.Util;
using Tc.TcMedia.Scheduler;
using Tc.TcMedia.Dfp;
using MySql.Data.MySqlClient;

namespace DFPContacts
{
    public class DFPContacts : iScheduler
    {
        public void Run(Process process)
        {
            DfpUser interfaceUser = Tc.TcMedia.Dfp.Auth.getDfpUser();

            process.log("Getting all Contacts");

            // Get the ContactService.
            ContactService contactService = (ContactService)interfaceUser.GetService(DfpService.v201508.ContactService);
            contactService.Timeout = 300000; // 5 min

            // Create a statement to get all contacts.
            StatementBuilder statementBuilder = new StatementBuilder()
                .OrderBy("id DESC")
                .Limit(StatementBuilder.SUGGESTED_PAGE_LIMIT);

            // Set default for page.
            ContactPage page = new ContactPage();

            do
            {
                // Get contacts by statement.
                process.log("Loading...");
                page = contactService.getContactsByStatement(statementBuilder.ToStatement());
                process.log("Loaded ");

                if (page.results != null)
                {
                    int i = page.startIndex;
                    foreach (Contact contact in page.results)
                    {
                        process.log(++i + "/" + page.totalResultSetSize + " " + contact.id + " : " + contact.name);
                        DFPBase.checkDfpObject(process, "dfp_contacts", "contactId", contact);
                    }
                }
                statementBuilder.IncreaseOffsetBy(StatementBuilder.SUGGESTED_PAGE_LIMIT);
            } while (statementBuilder.GetOffset() < page.totalResultSetSize);

            process.log("Number of items found: " + page.totalResultSetSize);
        }
    }
}
