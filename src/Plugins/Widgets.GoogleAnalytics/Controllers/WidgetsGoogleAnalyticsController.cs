﻿using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using SmartStore.Core;
using SmartStore.Core.Domain;
using SmartStore.Core.Domain.Catalog;
using SmartStore.Core.Domain.Orders;
using SmartStore.Plugin.Widgets.GoogleAnalytics.Models;
using SmartStore.Services.Catalog;
using SmartStore.Services.Configuration;
using SmartStore.Core.Logging;
using SmartStore.Services.Orders;
using SmartStore.Services.Stores;
using SmartStore.Web.Framework.Controllers;
using SmartStore.Web.Framework.Settings;

namespace SmartStore.Plugin.Widgets.GoogleAnalytics.Controllers
{

    public class WidgetsGoogleAnalyticsController : SmartController
    {
        private readonly IWorkContext _workContext;
		private readonly IStoreContext _storeContext;
		private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IOrderService _orderService;
        private readonly ICategoryService _categoryService;

        public WidgetsGoogleAnalyticsController(IWorkContext workContext,
			IStoreContext storeContext, IStoreService storeService,
			ISettingService settingService, IOrderService orderService,
            ICategoryService categoryService)
        {
            this._workContext = workContext;
			this._storeContext = storeContext;
			this._storeService = storeService;
            this._settingService = settingService;
            this._orderService = orderService;
            this._categoryService = categoryService;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
			//load settings for a chosen store scope
			var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
			var googleAnalyticsSettings = _settingService.LoadSetting<GoogleAnalyticsSettings>(storeScope);
            var model = new ConfigurationModel();
            model.GoogleId = googleAnalyticsSettings.GoogleId;
            model.TrackingScript = googleAnalyticsSettings.TrackingScript; 
            model.EcommerceScript = googleAnalyticsSettings.EcommerceScript;
            model.EcommerceDetailScript = googleAnalyticsSettings.EcommerceDetailScript;
            
            model.ZoneId = googleAnalyticsSettings.WidgetZone;
            model.AvailableZones.Add(new SelectListItem() { Text = "<head> HTML tag", Value = "head_html_tag"});
            model.AvailableZones.Add(new SelectListItem() { Text = "Before <body> end HTML tag", Value = "body_end_html_tag_before" });

			var storeDependingSettingHelper = new StoreDependingSettingHelper(ViewData);
			storeDependingSettingHelper.GetOverrideKeys(googleAnalyticsSettings, model, storeScope, _settingService);

            return View(model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
		[ValidateInput(false)]
        public ActionResult Configure(ConfigurationModel model, FormCollection form)
        {
			//load settings for a chosen store scope
			var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
			var googleAnalyticsSettings = _settingService.LoadSetting<GoogleAnalyticsSettings>(storeScope);
            googleAnalyticsSettings.GoogleId = model.GoogleId;
            googleAnalyticsSettings.TrackingScript = model.TrackingScript; 
            googleAnalyticsSettings.EcommerceScript = model.EcommerceScript;
            googleAnalyticsSettings.EcommerceDetailScript = model.EcommerceDetailScript;
            googleAnalyticsSettings.WidgetZone = model.ZoneId;

			_settingService.SaveSetting(googleAnalyticsSettings, x => x.WidgetZone, 0, false);

			var storeDependingSettingHelper = new StoreDependingSettingHelper(ViewData);
			storeDependingSettingHelper.UpdateSettings(googleAnalyticsSettings, form, storeScope, _settingService);

			//now clear settings cache
			_settingService.ClearCache();

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PublicInfo(string widgetZone)
        {
            string globalScript = "";
            var routeData = ((System.Web.UI.Page)this.HttpContext.CurrentHandler).RouteData;

            try
            {
                //Special case, if we are in last step of checkout, we can use order total for conversion value
                if (routeData.Values["controller"].ToString().Equals("checkout", StringComparison.InvariantCultureIgnoreCase) &&
                    routeData.Values["action"].ToString().Equals("completed", StringComparison.InvariantCultureIgnoreCase))
                {
                    var lastOrder = GetLastOrder();
                    globalScript += GetEcommerceScript(lastOrder);
                }
                else
                {
                    globalScript += GetTrackingScript();
                }
            }
            catch (Exception ex)
            {
                Logger.InsertLog(SmartStore.Core.Domain.Logging.LogLevel.Error, "Error creating scripts for google ecommerce tracking", ex.ToString());
            }
            return Content(globalScript);
            //return View("SmartStore.Plugin.Widgets.GoogleAnalytics.Views.WidgetsGoogleAnalytics.PublicInfo", model);
        }

        private Order GetLastOrder()
        {
			var order = _orderService.SearchOrders(_storeContext.CurrentStore.Id, _workContext.CurrentCustomer.Id,
				null, null, null, null, null, null, null, null, 0, 1).FirstOrDefault();
			return order;
        }
        
        //<script type="text/javascript"> 

        //var _gaq = _gaq || []; 
        //_gaq.push(['_setAccount', 'UA-XXXXX-X']); 
        //_gaq.push(['_trackPageview']); 

        //(function() { 
        //var ga = document.createElement('script'); ga.type = 'text/javascript'; ga.async = true; 
        //ga.src = ('https:' == document.location.protocol ? 'https://ssl' : 'http://www') + '.google-analytics.com/ga.js'; 
        //var s = document.getElementsByTagName('script')[0]; s.parentNode.insertBefore(ga, s); 
        //})(); 

        //</script>
        private string GetTrackingScript()
        {
			var googleAnalyticsSettings = _settingService.LoadSetting<GoogleAnalyticsSettings>(_storeContext.CurrentStore.Id);
            string analyticsTrackingScript = "";
            analyticsTrackingScript = googleAnalyticsSettings.TrackingScript + "\n";
            analyticsTrackingScript = analyticsTrackingScript.Replace("{GOOGLEID}", googleAnalyticsSettings.GoogleId);
            analyticsTrackingScript = analyticsTrackingScript.Replace("{ECOMMERCE}", "");
            return analyticsTrackingScript;
        }
        
        //<script type="text/javascript"> 

        //var _gaq = _gaq || []; 
        //_gaq.push(['_setAccount', 'UA-XXXXX-X']); 
        //_gaq.push(['_trackPageview']); 
        //_gaq.push(['_addTrans', 
        //'1234',           // order ID - required 
        //'Acme Clothing',  // affiliation or store name 
        //'11.99',          // total - required 
        //'1.29',           // tax 
        //'5',              // shipping 
        //'San Jose',       // city 
        //'California',     // state or province 
        //'USA'             // country 
        //]); 

        //// add item might be called for every item in the shopping cart 
        //// where your ecommerce engine loops through each item in the cart and 
        //// prints out _addItem for each 
        //_gaq.push(['_addItem', 
        //'1234',           // order ID - required 
        //'DD44',           // SKU/code - required 
        //'T-Shirt',        // product name 
        //'Green Medium',   // category or variation 
        //'11.99',          // unit price - required 
        //'1'               // quantity - required 
        //]); 
        //_gaq.push(['_trackTrans']); //submits transaction to the Analytics servers 

        //(function() { 
        //var ga = document.createElement('script'); ga.type = 'text/javascript'; ga.async = true; 
        //ga.src = ('https:' == document.location.protocol ? 'https://ssl' : 'http://www') + '.google-analytics.com/ga.js'; 
        //var s = document.getElementsByTagName('script')[0]; s.parentNode.insertBefore(ga, s); 
        //})(); 

        //</script>
        private string GetEcommerceScript(Order order)
        {
			var googleAnalyticsSettings = _settingService.LoadSetting<GoogleAnalyticsSettings>(_storeContext.CurrentStore.Id);
            var usCulture = new CultureInfo("en-US");
            string analyticsTrackingScript = "";
			analyticsTrackingScript = googleAnalyticsSettings.TrackingScript + "\n";
			analyticsTrackingScript = analyticsTrackingScript.Replace("{GOOGLEID}", googleAnalyticsSettings.GoogleId);

            string analyticsEcommerceScript = "";
            if (order != null)
            {
                analyticsEcommerceScript = googleAnalyticsSettings.EcommerceScript + "\n";
                analyticsEcommerceScript = analyticsEcommerceScript.Replace("{GOOGLEID}", googleAnalyticsSettings.GoogleId);
                analyticsEcommerceScript = analyticsEcommerceScript.Replace("{ORDERID}", order.GetOrderNumber());
				analyticsEcommerceScript = analyticsEcommerceScript.Replace("{SITE}", _storeContext.CurrentStore.Url.Replace("http://", "").Replace("/", ""));
                analyticsEcommerceScript = analyticsEcommerceScript.Replace("{TOTAL}", order.OrderTotal.ToString("0.00", usCulture));
                analyticsEcommerceScript = analyticsEcommerceScript.Replace("{TAX}", order.OrderTax.ToString("0.00", usCulture));
                analyticsEcommerceScript = analyticsEcommerceScript.Replace("{SHIP}", order.OrderShippingInclTax.ToString("0.00", usCulture));
                analyticsEcommerceScript = analyticsEcommerceScript.Replace("{CITY}", order.BillingAddress == null ? "" : FixIllegalJavaScriptChars(order.BillingAddress.City));
                analyticsEcommerceScript = analyticsEcommerceScript.Replace("{STATEPROVINCE}", order.BillingAddress == null || order.BillingAddress.StateProvince == null ? "" : FixIllegalJavaScriptChars(order.BillingAddress.StateProvince.Name));
                analyticsEcommerceScript = analyticsEcommerceScript.Replace("{COUNTRY}", order.BillingAddress == null || order.BillingAddress.Country == null ? "" : FixIllegalJavaScriptChars(order.BillingAddress.Country.Name));

                var sb = new StringBuilder();
                foreach (var item in order.OrderItems)
                {
                    string analyticsEcommerceDetailScript = googleAnalyticsSettings.EcommerceDetailScript;
                    //get category
                    string categ = "";
                    var defaultProductCategory = _categoryService.GetProductCategoriesByProductId(item.ProductId).FirstOrDefault();
                    if (defaultProductCategory != null)
                        categ = defaultProductCategory.Category.Name;
                    analyticsEcommerceDetailScript = analyticsEcommerceDetailScript.Replace("{ORDERID}", order.GetOrderNumber());
                    //The SKU code is a required parameter for every item that is added to the transaction
                    item.Product.MergeWithCombination(item.AttributesXml);
                    analyticsEcommerceDetailScript = analyticsEcommerceDetailScript.Replace("{PRODUCTSKU}", FixIllegalJavaScriptChars(item.Product.Sku));
                    analyticsEcommerceDetailScript = analyticsEcommerceDetailScript.Replace("{PRODUCTNAME}", FixIllegalJavaScriptChars(item.Product.Name));
                    analyticsEcommerceDetailScript = analyticsEcommerceDetailScript.Replace("{CATEGORYNAME}", FixIllegalJavaScriptChars(categ));
                    analyticsEcommerceDetailScript = analyticsEcommerceDetailScript.Replace("{UNITPRICE}", item.UnitPriceInclTax.ToString("0.00", usCulture));
                    analyticsEcommerceDetailScript = analyticsEcommerceDetailScript.Replace("{QUANTITY}", item.Quantity.ToString());
                    sb.AppendLine(analyticsEcommerceDetailScript);
                }

                analyticsEcommerceScript = analyticsEcommerceScript.Replace("{DETAILS}", sb.ToString());

                analyticsTrackingScript = analyticsTrackingScript.Replace("{ECOMMERCE}", analyticsEcommerceScript);

            }

            return analyticsTrackingScript;
        }

        private string FixIllegalJavaScriptChars(string text)
        {
            if (String.IsNullOrEmpty(text))
                return text;

            //replace ' with \' (http://stackoverflow.com/questions/4292761/need-to-url-encode-labels-when-tracking-events-with-google-analytics)
            text = text.Replace("'", "\\'");
            return text;
        }
    }
}