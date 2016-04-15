//Copyright 2014 Microsoft

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//       http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using CatalogCommon;

namespace CatalogIndexer
{
    // This portion of the sample is the data indexer. It's a simple console app which not only makes it easy to
    // try but also is a great candidate for using it with Webjobs, where you can schedule it to run periodically
    
    // The sample not only shows how to issue a data indexing request but also explores a possible
    // approach for separating the source of data and how to represent changes from the indexing logic

    // Please remember to update the "SearchServiceName" and "SearchServiceApiKey" in the App.config with your service details

    class Program
    {
        private static Uri _serviceUri;
        private static HttpClient _httpClient;

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("{0}", "Creating index...\n");
                _serviceUri = new Uri("https://" + ConfigurationManager.AppSettings["SearchServiceName"] + ".search.windows.net");
                _httpClient = new HttpClient();
                // Get the search service connection information from the App.config
                _httpClient.DefaultRequestHeaders.Add("api-key", ConfigurationManager.AppSettings["SearchServiceApiKey"]);

                ChangeEnumeratorSql changeEnumerator = new ChangeEnumeratorSql(
                    ConfigurationManager.AppSettings["SourceSqlConnectionString"],
                    "SELECT CONVERT(NVARCHAR(32), ProductID) AS ProductID, Name, ProductNumber, Color, StandardCost, ListPrice, Size, Weight, SellStartDate, SellEndDate, DiscontinuedDate, CategoryName, ModelName, Description FROM Products",
                    "Version");
                ChangeSet changes = changeEnumerator.ComputeChangeSet(null);
                ApplyChanges(changes);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unhandled exception caught:");

                while (e != null)
                {
                    Console.WriteLine("\t{0}", e.Message);
                    e = e.InnerException;
                }

                Console.WriteLine();
                Console.WriteLine("{0}", "\nDid you remember to paste your service URL and API key into App.config?\n");
            }
            Console.Write("Complete.  Press <enter> to continue: ");
            var name = Console.ReadLine();

        }

        private static void ApplyChanges(ChangeSet changes)
        {
            // first apply the changes and if we succeed then record the new version that 
            // we'll use as starting point next time
            
            // create a fresh index every time. If you want to preserve contents between runs (a more real scenario)
            // replace this with a call to CatalogIndexExists() and create it only if it's not there already
            DeleteCatalogIndex();
            CreateCatalogIndex();

            // pull contents from the changeset and upload them to Azure Search in batches of up to 1000 documents each
            var indexOperations = new List<Dictionary<string, object>>();

            foreach (var change in changes.Changes)
            {
                change["@search.action"] = "upload"; // action can be upload, merge or delete

                indexOperations.Add(change);

                if (indexOperations.Count > 999)
                {
                    IndexCatalogBatch(indexOperations);
                    indexOperations.Clear();
                }
            }

            if (indexOperations.Count > 0)
            {
                IndexCatalogBatch(indexOperations);
            }
        }

        private static bool CatalogIndexExists()
        {
            Uri uri = new Uri(_serviceUri, "/indexes/catalog");
            HttpResponseMessage response = AzureSearchHelper.SendSearchRequest(_httpClient, HttpMethod.Get, uri);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
            response.EnsureSuccessStatusCode();
            return true;
        }

        private static bool DeleteCatalogIndex()
        {
            Uri uri = new Uri(_serviceUri, "/indexes/catalog");
            HttpResponseMessage response = AzureSearchHelper.SendSearchRequest(_httpClient, HttpMethod.Delete, uri);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
            response.EnsureSuccessStatusCode();
            return true;
        }

        private static void CreateCatalogIndex()
        {
            var definition = new 
            {
                Name = "catalog",
                Fields = new[] 
                { 
                    new { Name = "productID",        Type = "Edm.String",         Key = true,  Searchable = false, Filterable = false, Sortable = false, Facetable = false, Retrievable = true,  Suggestions = false },
                    new { Name = "name",             Type = "Edm.String",         Key = false, Searchable = true,  Filterable = false, Sortable = true,  Facetable = false, Retrievable = true,  Suggestions = true  },
                    new { Name = "productNumber",    Type = "Edm.String",         Key = false, Searchable = true,  Filterable = false, Sortable = false, Facetable = false, Retrievable = true,  Suggestions = true  },
                    new { Name = "color",            Type = "Edm.String",         Key = false, Searchable = true,  Filterable = true,  Sortable = true,  Facetable = true,  Retrievable = true,  Suggestions = false },
                    new { Name = "standardCost",     Type = "Edm.Double",         Key = false, Searchable = false, Filterable = false, Sortable = false, Facetable = false, Retrievable = true,  Suggestions = false },
                    new { Name = "listPrice",        Type = "Edm.Double",         Key = false, Searchable = false, Filterable = true,  Sortable = true,  Facetable = true,  Retrievable = true, Suggestions = false },
                    new { Name = "size",             Type = "Edm.String",         Key = false, Searchable = true,  Filterable = true,  Sortable = true,  Facetable = true,  Retrievable = true,  Suggestions = false },
                    new { Name = "weight",           Type = "Edm.Double",         Key = false, Searchable = false, Filterable = true,  Sortable = false, Facetable = true,  Retrievable = true,  Suggestions = false },
                    new { Name = "sellStartDate",    Type = "Edm.DateTimeOffset", Key = false, Searchable = false, Filterable = true,  Sortable = false, Facetable = false, Retrievable = false, Suggestions = false },
                    new { Name = "sellEndDate",      Type = "Edm.DateTimeOffset", Key = false, Searchable = false, Filterable = true,  Sortable = false, Facetable = false, Retrievable = false, Suggestions = false },
                    new { Name = "discontinuedDate", Type = "Edm.DateTimeOffset", Key = false, Searchable = false, Filterable = true,  Sortable = false, Facetable = false, Retrievable = true,  Suggestions = false },
                    new { Name = "categoryName",     Type = "Edm.String",         Key = false, Searchable = true,  Filterable = true,  Sortable = false, Facetable = true,  Retrievable = true,  Suggestions = true  },
                    new { Name = "modelName",        Type = "Edm.String",         Key = false, Searchable = true,  Filterable = true,  Sortable = false, Facetable = true,  Retrievable = true,  Suggestions = true  },
                    new { Name = "description",      Type = "Edm.String",         Key = false, Searchable = true,  Filterable = true,  Sortable = false, Facetable = false, Retrievable = true,  Suggestions = false }
                }
            };

            Uri uri = new Uri(_serviceUri, "/indexes");
            string json = AzureSearchHelper.SerializeJson(definition);
            HttpResponseMessage response = AzureSearchHelper.SendSearchRequest(_httpClient, HttpMethod.Post, uri, json);
            response.EnsureSuccessStatusCode();
        }

        private static void IndexCatalogBatch(List<Dictionary<string, object>> changes)
        {
            var batch = new
            {
                value = changes
            };

            Uri uri = new Uri(_serviceUri, "/indexes/catalog/docs/index");
            string json = AzureSearchHelper.SerializeJson(batch);
            HttpResponseMessage response = AzureSearchHelper.SendSearchRequest(_httpClient, HttpMethod.Post, uri, json);
            response.EnsureSuccessStatusCode();
        }
    }
}
