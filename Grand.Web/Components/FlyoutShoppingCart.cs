﻿using Grand.Core.Domain.Orders;
using Grand.Framework.Components;
using Grand.Services.Security;
using Grand.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Grand.Web.ViewComponents
{
    public class FlyoutShoppingCartViewComponent : BaseViewComponent
    {
        private readonly IShoppingCartViewModelService _shoppingCartViewModelService;
        private readonly IPermissionService _permissionService;
        private readonly ShoppingCartSettings _shoppingCartSettings;

        public FlyoutShoppingCartViewComponent(IShoppingCartViewModelService shoppingCartViewModelService,
            IPermissionService permissionService,
            ShoppingCartSettings shoppingCartSettings)
        {
            this._shoppingCartViewModelService = shoppingCartViewModelService;
            this._permissionService = permissionService;
            this._shoppingCartSettings = shoppingCartSettings;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!_shoppingCartSettings.MiniShoppingCartEnabled)
                return Content("");

            if (!_permissionService.Authorize(StandardPermissionProvider.EnableShoppingCart))
                return Content("");

            var model = await Task.Run(() => _shoppingCartViewModelService.PrepareMiniShoppingCart());
            return View(model);
        }
    }
}