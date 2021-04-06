using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Todoist.Net;
using Todoist.Net.Models;

namespace TodoistDaysRemaining.Functions.Tests
{
    [TestClass()]
    public class UpdateDaysRemainingTests
    {
        ITodoistClient client = null;

        [TestInitialize]
        public void Initialise()
        {
            client = new TodoistClient(Environment.GetEnvironmentVariable("TODOIST_APIKEY"));

        }

        [TestMethod()]
        public void CalculateDaysTomorrowTest()
        {
            DateTime tomorrow = DateTime.Now.Date.AddDays(1);
            (int days, int _) = UpdateDaysRemaining.CalculateDays(tomorrow);
            Assert.AreEqual(1, days);
        }

        [TestMethod()]
        public void UtcDateTest()
        {
            var dueDateUtc = DateTime.UtcNow.Date.AddDays(5);
            var item = new Item("simon demo task") { DueDate = new DueDate(DateTime.Parse("2021-04-10")) };
            client.Items.AddAsync(item).Wait();

            var itemInfo = client.Items.GetAsync(item.Id).Result;

            Assert.AreEqual(dueDateUtc, itemInfo.Item.DueDate);
        }
    }
}