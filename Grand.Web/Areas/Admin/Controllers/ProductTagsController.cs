﻿using Grand.Framework.Extensions;
using Grand.Framework.Kendoui;
using Grand.Framework.Mvc;
using Grand.Framework.Security.Authorization;
using Grand.Services.Catalog;
using Grand.Services.Localization;
using Grand.Services.Security;
using Grand.Web.Areas.Admin.Extensions;
using Grand.Web.Areas.Admin.Models.Catalog;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace Grand.Web.Areas.Admin.Controllers
{
    [PermissionAuthorize(PermissionSystemName.ProductTags)]
    public partial class ProductTagsController : BaseAdminController
    {
        private readonly IProductTagService _productTagService;
        private readonly ILanguageService _languageService;

        public ProductTagsController(IProductTagService productTagService, ILanguageService languageService)
        {
            this._productTagService = productTagService;
            this._languageService = languageService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult List(DataSourceRequest command)
        {
            var tags = _productTagService.GetAllProductTags()
                //order by product count
                .OrderByDescending(x => _productTagService.GetProductCount(x.Id, ""))
                .Select(x => new ProductTagModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    ProductCount = _productTagService.GetProductCount(x.Id, "")
                })
                .ToList();

            var gridModel = new DataSourceResult
            {
                Data = tags.PagedForCommand(command),
                Total = tags.Count
            };

            return Json(gridModel);
        }

        //edit
        public IActionResult Edit(string id)
        {
            var productTag = _productTagService.GetProductTagById(id);
            if (productTag == null)
                //No product tag found with the specified id
                return RedirectToAction("List");

            var model = new ProductTagModel
            {
                Id = productTag.Id,
                Name = productTag.Name,
                ProductCount = _productTagService.GetProductCount(productTag.Id, "")
            };
            //locales
            AddLocales(_languageService, model.Locales, (locale, languageId) =>
            {
                locale.Name = productTag.GetLocalized(x => x.Name, languageId, false, false);
            });

            return View(model);
        }

        [HttpPost]
        public IActionResult Edit(ProductTagModel model)
        {
            var productTag = _productTagService.GetProductTagById(model.Id);
            if (productTag == null)
                //No product tag found with the specified id
                return RedirectToAction("List");

            if (ModelState.IsValid)
            {
                productTag.Name = model.Name;
                productTag.Locales = model.Locales.ToLocalizedProperty();
                _productTagService.UpdateProductTag(productTag);
                ViewBag.RefreshPage = true;
                return View(model);
            }
            //If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpPost]
        public IActionResult Delete(string id)
        {
            var tag = _productTagService.GetProductTagById(id);
            if (tag == null)
                throw new ArgumentException("No product tag found with the specified id");
            if (ModelState.IsValid)
            {
                _productTagService.DeleteProductTag(tag);
                return new NullJsonResult();
            }
            return ErrorForKendoGridJson(ModelState);
        }
    }
}
