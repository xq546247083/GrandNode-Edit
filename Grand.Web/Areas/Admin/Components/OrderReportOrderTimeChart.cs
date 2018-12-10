﻿using Grand.Framework.Components;
using Grand.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Web.Areas.Admin.Components
{
    public class OrderReportOrderTimeChartViewComponent : BaseViewComponent
    {
        private readonly IPermissionService _permissionService;

        public OrderReportOrderTimeChartViewComponent(IPermissionService permissionService)
        {
            this._permissionService = permissionService;
        }

        public IViewComponentResult Invoke()
        {

            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return Content("");

            return View();
        }
    }
}