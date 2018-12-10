﻿using Grand.Framework.Kendoui;
using Grand.Framework.Mvc;
using Grand.Framework.Mvc.Filters;
using Grand.Framework.Security.Authorization;
using Grand.Services.Catalog;
using Grand.Services.Localization;
using Grand.Services.Logging;
using Grand.Services.Security;
using Grand.Web.Areas.Admin.Extensions;
using Grand.Web.Areas.Admin.Models.Catalog;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace Grand.Web.Areas.Admin.Controllers
{
    [PermissionAuthorize(PermissionSystemName.Attributes)]
    public partial class ProductAttributeController : BaseAdminController
    {
        #region Fields
        private readonly IProductService _productService;
        private readonly IProductAttributeService _productAttributeService;
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;
        private readonly ICustomerActivityService _customerActivityService;
        #endregion Fields

        #region Constructors

        public ProductAttributeController(IProductService productService,
            IProductAttributeService productAttributeService,
            ILanguageService languageService,
            ILocalizationService localizationService,
            ICustomerActivityService customerActivityService)
        {
            this._productService = productService;
            this._productAttributeService = productAttributeService;
            this._languageService = languageService;
            this._localizationService = localizationService;
            this._customerActivityService = customerActivityService;
        }

        #endregion

        #region Methods

        #region Attribute list / create / edit / delete

        //list
        public IActionResult Index()
        {
            return RedirectToAction("List");
        }

        public IActionResult List()
        {
            return View();
        }

        [HttpPost]
        public IActionResult List(DataSourceRequest command)
        {
            var productAttributes = _productAttributeService
                .GetAllProductAttributes(command.Page - 1, command.PageSize);
            var gridModel = new DataSourceResult
            {
                Data = productAttributes.Select(x => x.ToModel()),
                Total = productAttributes.TotalCount
            };

            return Json(gridModel);
        }
        
        //create
        public IActionResult Create()
        {
            var model = new ProductAttributeModel();
            //locales
            AddLocales(_languageService, model.Locales);
            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public IActionResult Create(ProductAttributeModel model, bool continueEditing)
        {
            if (ModelState.IsValid)
            {
                var productAttribute = model.ToEntity();
                _productAttributeService.InsertProductAttribute(productAttribute);

                //activity log
                _customerActivityService.InsertActivity("AddNewProductAttribute", productAttribute.Id, _localizationService.GetResource("ActivityLog.AddNewProductAttribute"), productAttribute.Name);

                SuccessNotification(_localizationService.GetResource("Admin.Catalog.Attributes.ProductAttributes.Added"));
                return continueEditing ? RedirectToAction("Edit", new { id = productAttribute.Id }) : RedirectToAction("List");
            }

            //If we got this far, something failed, redisplay form
            return View(model);
        }

        //edit
        public IActionResult Edit(string id)
        {
            var productAttribute = _productAttributeService.GetProductAttributeById(id);
            if (productAttribute == null)
                //No product attribute found with the specified id
                return RedirectToAction("List");

            var model = productAttribute.ToModel();
            //locales
            AddLocales(_languageService, model.Locales, (locale, languageId) =>
            {
                locale.Name = productAttribute.GetLocalized(x => x.Name, languageId, false, false);
                locale.Description = productAttribute.GetLocalized(x => x.Description, languageId, false, false);
            });

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public IActionResult Edit(ProductAttributeModel model, bool continueEditing)
        {
            var productAttribute = _productAttributeService.GetProductAttributeById(model.Id);
            if (productAttribute == null)
                //No product attribute found with the specified id
                return RedirectToAction("List");
            
            if (ModelState.IsValid)
            {
                productAttribute = model.ToEntity(productAttribute);
                _productAttributeService.UpdateProductAttribute(productAttribute);

                //activity log
                _customerActivityService.InsertActivity("EditProductAttribute", productAttribute.Id, _localizationService.GetResource("ActivityLog.EditProductAttribute"), productAttribute.Name);

                SuccessNotification(_localizationService.GetResource("Admin.Catalog.Attributes.ProductAttributes.Updated"));
                if (continueEditing)
                {
                    //selected tab
                    SaveSelectedTabIndex();

                    return RedirectToAction("Edit", new { id = productAttribute.Id });
                }
                return RedirectToAction("List");

            }
            //If we got this far, something failed, redisplay form
            return View(model);
        }

        //delete
        [HttpPost]
        public IActionResult Delete(string id)
        {
            var productAttribute = _productAttributeService.GetProductAttributeById(id);
            if (productAttribute == null)
                //No product attribute found with the specified id
                return RedirectToAction("List");
            if (ModelState.IsValid)
            {
                _productAttributeService.DeleteProductAttribute(productAttribute);

                //activity log
                _customerActivityService.InsertActivity("DeleteProductAttribute", productAttribute.Id, _localizationService.GetResource("ActivityLog.DeleteProductAttribute"), productAttribute.Name);

                SuccessNotification(_localizationService.GetResource("Admin.Catalog.Attributes.ProductAttributes.Deleted"));
                return RedirectToAction("List");
            }
            ErrorNotification(ModelState);
            return RedirectToAction("Edit", new { id = productAttribute.Id });
        }

        #endregion

        #region Used by products

        //used by products
        [HttpPost]
        public IActionResult UsedByProducts(DataSourceRequest command, string productAttributeId)
        {
            var orders = _productService.GetProductsByProductAtributeId(
                productAttributeId: productAttributeId,
                pageIndex: command.Page - 1,
                pageSize: command.PageSize);
            var gridModel = new DataSourceResult
            {
                Data = orders.Select(x =>
                {
                    return new ProductAttributeModel.UsedByProductModel
                    {
                        Id = x.Id,
                        ProductName = x.Name,
                        Published = x.Published
                    };
                }),
                Total = orders.TotalCount
            };
            return Json(gridModel);
        }
        
        #endregion

        #region Predefined values
        [HttpPost]
        public IActionResult PredefinedProductAttributeValueList(string productAttributeId, DataSourceRequest command)
        {
            var values = _productAttributeService.GetProductAttributeById(productAttributeId).PredefinedProductAttributeValues;
            var gridModel = new DataSourceResult
            {
                Data = values.Select(x =>x.ToModel()),
                Total = values.Count(),
            };

            return Json(gridModel);
        }

        //create
        public IActionResult PredefinedProductAttributeValueCreatePopup(string productAttributeId)
        {
            var productAttribute = _productAttributeService.GetProductAttributeById(productAttributeId);
            if (productAttribute == null)
                throw new ArgumentException("No product attribute found with the specified id");

            var model = new PredefinedProductAttributeValueModel();
            model.ProductAttributeId = productAttributeId;

            //locales
            AddLocales(_languageService, model.Locales);

            return View(model);
        }

        [HttpPost]
        public IActionResult PredefinedProductAttributeValueCreatePopup(PredefinedProductAttributeValueModel model)
        {
            var productAttribute = _productAttributeService.GetProductAttributeById(model.ProductAttributeId);
            if (productAttribute == null)
                throw new ArgumentException("No product attribute found with the specified id");

            if (ModelState.IsValid)
            {
                var ppav = model.ToEntity();
                productAttribute.PredefinedProductAttributeValues.Add(ppav);
                _productAttributeService.UpdateProductAttribute(productAttribute);

                ViewBag.RefreshPage = true;
                return View(model);
            }
            //If we got this far, something failed, redisplay form
            return View(model);
        }

        //edit
        public IActionResult PredefinedProductAttributeValueEditPopup(string id, string productAttributeId)
        {
            var ppav = _productAttributeService.GetProductAttributeById(productAttributeId).PredefinedProductAttributeValues.Where(x=>x.Id == id).FirstOrDefault();
            if (ppav == null)
                throw new ArgumentException("No product attribute value found with the specified id");

            var model = ppav.ToModel();
            //locales
            AddLocales(_languageService, model.Locales, (locale, languageId) =>
            {
                locale.Name = ppav.GetLocalized(x => x.Name, languageId, false, false);
            });
            return View(model);
        }

        [HttpPost]
        public IActionResult PredefinedProductAttributeValueEditPopup(PredefinedProductAttributeValueModel model)
        {
            var productAttribute = _productAttributeService.GetProductAttributeById(model.ProductAttributeId);
            var ppav = productAttribute.PredefinedProductAttributeValues.Where(x=>x.Id == model.Id).FirstOrDefault();
            if (ppav == null)
                throw new ArgumentException("No product attribute value found with the specified id");

            if (ModelState.IsValid)
            {
                ppav = model.ToEntity(ppav);
                _productAttributeService.UpdateProductAttribute(productAttribute);

                ViewBag.RefreshPage = true;
                return View(model);
            }
            //If we got this far, something failed, redisplay form
            return View(model);
        }

        //delete
        [HttpPost]
        public IActionResult PredefinedProductAttributeValueDelete(string id, string productAttributeId)
        {
            if (ModelState.IsValid)
            {
                var productAttribute = _productAttributeService.GetProductAttributeById(productAttributeId);
                var ppav = productAttribute.PredefinedProductAttributeValues.Where(x => x.Id == id).FirstOrDefault();
                if (ppav == null)
                    throw new ArgumentException("No predefined product attribute value found with the specified id");
                productAttribute.PredefinedProductAttributeValues.Remove(ppav);
                _productAttributeService.UpdateProductAttribute(productAttribute);
                return new NullJsonResult();
            }
            return ErrorForKendoGridJson(ModelState);
        }
        #endregion

        #endregion
    }
}
