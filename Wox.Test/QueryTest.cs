using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Wox.Core.Plugin;
using Wox.Plugin;
using Wox.ViewModel;

namespace Wox.Test
{
    public class QueryTest
    {
        [Test]
        [Ignore("Current query is tightly integrated with GUI, can't be tested.")]
        public void ExclusivePluginQueryTest()
        {
            Query q = PluginManager.QueryInit("> file.txt file2 file3");

            Assert.AreEqual(q.FirstSearch, "file.txt");
            Assert.AreEqual(q.SecondSearch, "file2");
            Assert.AreEqual(q.ThirdSearch, "file3");
            Assert.AreEqual(q.SecondToEndSearch, "file2 file3");
        }

        [Test]
        [Ignore("Current query is tightly integrated with GUI, can't be tested.")]
        public void GenericPluginQueryTest()
        {
            Query q = PluginManager.QueryInit("file.txt file2 file3");

            Assert.AreEqual(q.FirstSearch, "file.txt");
            Assert.AreEqual(q.SecondSearch, "file2");
            Assert.AreEqual(q.ThirdSearch, "file3");
            Assert.AreEqual(q.SecondToEndSearch, "file2 file3");
        }

        [Test]
        [Ignore("Current query is tightly integrated with GUI, can't be tested.")]
        public void SystemPluginQueryTest()
        {
            Query query = PluginManager.QueryInit("lock");

            if (query != null)
            {
                // handle the exclusiveness of plugin using action keyword
                string keyword = query.ActionKeyword;



                var plugins = PluginManager.ValidPluginsForQuery(query);
                var allresults = new List<Result>();
                Task.Run(() =>
                {
                    Parallel.ForEach(plugins, plugin =>
                    {
                        var results = PluginManager.QueryForPlugin(plugin, query);

                        allresults.AddRange(results);
                    });
                }).Wait();

                Console.Write(allresults.ToString());
            }
        }

    }
}
   