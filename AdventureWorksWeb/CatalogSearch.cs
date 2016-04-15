using System;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using CatalogCommon;

namespace AdventureWorksWeb
{
   public class CatalogSearch
    {
        private static readonly Uri _serviceUri;
        private static HttpClient _httpClient;
        public static string errorMessage;

        static CatalogSearch()
        {
            try
            {
                _serviceUri = new Uri("https://" + ConfigurationManager.AppSettings["SearchServiceName"] + ".search.windows.net");
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("api-key", ConfigurationManager.AppSettings["SearchServiceApiKey"]);
            }
            catch (Exception e)
            {
                errorMessage = e.Message.ToString();
            }
        }

        public dynamic Search(string searchText, string sort, string category, string department, string portfolio, string brandName)
        {
            string search = "&search=" + Uri.EscapeDataString(searchText);
            string facets = "&facet=L3_NAME&facet=L1_NAME&facet=L2_NAME&facet=BRAND_NAME";

            string paging = "&$top=100";
           
            string filter = String.Empty;
            string orderby = String.Empty;

            string[] depList = department.Split(',');

            if (!string.IsNullOrWhiteSpace(department))
            {
                foreach (String d in depList)
                {
                    if (!String.IsNullOrEmpty(d))
                    {
                        if (string.IsNullOrEmpty(filter))
                        {
                            filter += "&$filter=(L1_NAME eq '" + EscapeODataString(d) + "'";
                        }
                        else
                        {
                            filter += " or L1_NAME eq '" + EscapeODataString(d) + "'";
                        }
                    }
                }

                if (!string.IsNullOrEmpty(filter))
                    filter += ")";
            }
            

            if (!string.IsNullOrWhiteSpace(category))
            {
                filter += "&$filter=L3_NAME eq '" + EscapeODataString(category) + "'";
            }

            
            string[] brandList = brandName.Split(',');

            if (!string.IsNullOrWhiteSpace(brandName.Replace(',',' ').Trim()))
            {
                foreach (String b in brandList)
                {
                    if (!String.IsNullOrEmpty(b))
                    {
                        if (string.IsNullOrEmpty(filter))
                        {
                            filter += "&$filter=(BRAND_NAME eq '" + EscapeODataString(b) + "'";
                        }
                        else
                        {
                            if (b == brandList[0])
                                filter += " and (BRAND_NAME eq '" + EscapeODataString(b) + "'";
                            else
                                filter += " or BRAND_NAME eq '" + EscapeODataString(b) + "'";
                        }
                    }
                }

                if (!string.IsNullOrEmpty(filter))
                    filter += ")";
            }

            Uri uri = new Uri(_serviceUri, "/indexes/dpg/docs?$count=true" + search + facets + paging + filter + orderby);

            HttpResponseMessage response = AzureSearchHelper.SendSearchRequest(_httpClient, HttpMethod.Get, uri);
            AzureSearchHelper.EnsureSuccessfulSearchResponse(response);

            var result = AzureSearchHelper.DeserializeJson<dynamic>(response.Content.ReadAsStringAsync().Result);

            return result;
        }

        public dynamic Suggest(string searchText)
        {
            // we still need a default filter to exclude discontinued products from the suggestions
            Uri uri = new Uri(_serviceUri, "/indexes/catalog/docs/suggest?$filter=discontinuedDate eq null&$select=productNumber&search=" + Uri.EscapeDataString(searchText));
            HttpResponseMessage response = AzureSearchHelper.SendSearchRequest(_httpClient, HttpMethod.Get, uri);
            AzureSearchHelper.EnsureSuccessfulSearchResponse(response);

            return AzureSearchHelper.DeserializeJson<dynamic>(response.Content.ReadAsStringAsync().Result);
        }

        private string EscapeODataString(string s)
        {
            return Uri.EscapeDataString(s).Replace("\'", "\'\'");
        }
    }
}
