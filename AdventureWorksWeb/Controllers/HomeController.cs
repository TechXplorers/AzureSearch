using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.UI.WebControls;

namespace AdventureWorksWeb.Controllers
{
    public class FacetValue
    {
        public FacetValue()
        {
            IsDuplicated = false;
        }

        public string Value { get; set; }
        public string Count { get; set; }
        public bool IsDuplicated { get; set; }

    }

    public class HomeController : Controller
    {
        private CatalogSearch _catalogSearch = new CatalogSearch();

        [HttpGet]
        public ActionResult Index()
        {            
            
            //Check that the user entered in a valid service url and api-key
            if (CatalogSearch.errorMessage != null)
                ViewBag.errorMessage = "Please ensure that you have added your SearchServiceName and SearchServiceApiKey to the Web.config. Error: " + CatalogSearch.errorMessage;

            return View();
        }

        [HttpGet]
        public ActionResult Search(string q = "", string category = null, string department = null, string portfolio = null,
                                   string brandName = null, string sort = null)
        {
            dynamic result = null;

            IList<FacetValue> facetValueList = new List<FacetValue>();
            IList<FacetValue> brandNameList = new List<FacetValue>();

            // If blank search, assume they want to search everything
            if (string.IsNullOrWhiteSpace(q))
                q = "*";

            DateTime start;
            TimeSpan time;

            start = DateTime.Now;

            result = _catalogSearch.Search(q, sort, category, department, portfolio, brandName);

            time = DateTime.Now - start;
            ViewBag.timeDisplay = String.Format("{0}.{1}", time.Seconds, time.Milliseconds.ToString().PadLeft(3, '0'));           

            SetDepartmentFacets(department, result, facetValueList);

            SetBrandNameFacets(brandName, result, brandNameList);


            SetViewBagValues(q, category, department, portfolio, brandName, sort);

            return View("Index", result);
        }

        private void SetViewBagValues(string q, string category, string department, string portfolio, string brandName,
            string sort)
        {
            ViewBag.searchString = q;
            ViewBag.searchDisplay = q + " + Department :" + department + "  Category :" + category + "  Brand Name :" +
                                    brandName;
            ViewBag.category = category;
            ViewBag.department = department;
            ViewBag.portfolio = portfolio;
            ViewBag.brandName = brandName;

            ViewBag.sort = sort;

            if (category == "")
                ViewBag.specificCategorySearch = false;
            else
                ViewBag.specificCategorySearch = true;

            if (string.IsNullOrEmpty(department.Replace(',', ' ').Trim()))
                ViewBag.specificDepartmentSearch = false;
            else
                ViewBag.specificDepartmentSearch = true;

            if (string.IsNullOrEmpty(brandName.Replace(',', ' ').Trim()))
                ViewBag.specificBrandNameSearch = false;
            else
                ViewBag.specificBrandNameSearch = true;
        }

        private void SetDepartmentFacets(string department, dynamic result, IList<FacetValue> facetValueList)
        {
            if (String.IsNullOrEmpty(department.Replace(',', ' ').Trim()))
            {
                foreach (var facet in result["@search.facets"].L1_NAME)
                {
                    facetValueList.Add(new FacetValue()
                    {
                        Value = facet.value,
                        Count = facet.count
                    });
                }

                Session["facetValue"] = facetValueList;
            }
            else
            {
                facetValueList = (List<FacetValue>) Session["facetValue"];

                //Need to Investigate why this is needed.
                foreach (var fac in facetValueList)
                {
                    fac.IsDuplicated = false;
                }
            }

            foreach (var dep in result["@search.facets"].L1_NAME)
            {
                foreach (var facet in facetValueList)
                {
                    if (facet.Value == dep.value.ToString())
                        facet.IsDuplicated = true;
                }
            }

            ViewData["facetValue"] = facetValueList.Where(x => !x.IsDuplicated).Select(x => x).ToList();
        }

        private void SetBrandNameFacets(string brandName, dynamic result, IList<FacetValue> brandList)
        {
            if (String.IsNullOrEmpty(brandName.Replace(',', ' ').Trim()))
            {
                foreach (var facet in result["@search.facets"].BRAND_NAME)
                {
                    brandList.Add(new FacetValue()
                    {
                        Value = facet.value,
                        Count = facet.count
                    });
                }

                Session["brandFacet"] = brandList;
            }
            else
            {
                brandList = (List<FacetValue>)Session["brandFacet"];

                //Need to Investigate why this is needed.
                foreach (var facet in brandList)
                {
                    facet.IsDuplicated = false;
                }
            }

            foreach (var dep in result["@search.facets"].BRAND_NAME)
            {
                foreach (var facet in brandList)
                {
                    if (facet.Value == dep.value.ToString())
                        facet.IsDuplicated = true;
                }
            }

            ViewData["brandFacet"] = brandList.Where(x => !x.IsDuplicated).Select(x => x).ToList();
        }

        [HttpGet]
        public ActionResult Suggest(string term)
        {
            var options = new List<string>();
            if (term.Length >= 3)
            {
                var result = _catalogSearch.Suggest(term);

                foreach (var option in result.value)
                {
                    options.Add((string)option["@search.text"] + " (" + (string)option["productNumber"] + ")");
                }
            }

            return new JsonResult
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = options
            };
        }
    }
}
