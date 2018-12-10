﻿using Grand.Core.Domain.Messages;
using Grand.Framework.Controllers;
using Grand.Framework.Kendoui;
using Grand.Framework.Mvc.Filters;
using Grand.Framework.Security.Authorization;
using Grand.Services.Localization;
using Grand.Services.Messages;
using Grand.Services.Security;
using Grand.Services.Stores;
using Grand.Web.Areas.Admin.Extensions;
using Grand.Web.Areas.Admin.Models.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Grand.Web.Areas.Admin.Controllers
{
    [PermissionAuthorize(PermissionSystemName.MessageTemplates)]
    public partial class MessageTemplateController : BaseAdminController
    {
        #region Fields

        private readonly IMessageTemplateService _messageTemplateService;
        private readonly IEmailAccountService _emailAccountService;
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly IStoreService _storeService;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly EmailAccountSettings _emailAccountSettings;

        #endregion Fields

        #region Constructors

        public MessageTemplateController(IMessageTemplateService messageTemplateService, 
            IEmailAccountService emailAccountService,
            ILanguageService languageService, 
            ILocalizationService localizationService, 
            IMessageTokenProvider messageTokenProvider, 
            IStoreService storeService,
            IStoreMappingService storeMappingService,
            IWorkflowMessageService workflowMessageService,
            EmailAccountSettings emailAccountSettings)
        {
            this._messageTemplateService = messageTemplateService;
            this._emailAccountService = emailAccountService;
            this._languageService = languageService;
            this._localizationService = localizationService;
            this._messageTokenProvider = messageTokenProvider;
            this._storeService = storeService;
            this._storeMappingService = storeMappingService;
            this._workflowMessageService = workflowMessageService;
            this._emailAccountSettings = emailAccountSettings;
        }

        #endregion
        
        #region Methods

        public IActionResult Index()
        {
            return RedirectToAction("List");
        }

        public IActionResult List()
        {
            var model = new MessageTemplateListModel();
            //stores
            model.AvailableStores.Add(new SelectListItem { Text = _localizationService.GetResource("Admin.Common.All"), Value = "" });
            foreach (var s in _storeService.GetAllStores())
                model.AvailableStores.Add(new SelectListItem { Text = s.Name, Value = s.Id.ToString() });
            
            return View(model);
        }

        [HttpPost]
        public IActionResult List(DataSourceRequest command, MessageTemplateListModel model)
        {
            var messageTemplates = _messageTemplateService.GetAllMessageTemplates(model.SearchStoreId);

            if (!string.IsNullOrEmpty(model.Name))
            {
                messageTemplates = messageTemplates.Where
                    (x => x.Name.ToLowerInvariant().Contains(model.Name.ToLowerInvariant()) ||
                    x.Subject.ToLowerInvariant().Contains(model.Name.ToLowerInvariant())).ToList();
            }

            var gridModel = new DataSourceResult
            {
                Data = messageTemplates.Select(x =>
                {
                    var templateModel = x.ToModel();
                    templateModel.PrepareStoresMappingModel(x, false, _storeService);
                    var stores = _storeService
                            .GetAllStores()
                            .Where(s => !x.LimitedToStores || templateModel.SelectedStoreIds.Contains(s.Id))
                            .ToList();
                    for (int i = 0; i < stores.Count; i++)
                    {
                        templateModel.ListOfStores += stores[i].Name;
                        if (i != stores.Count - 1)
                            templateModel.ListOfStores += ", ";
                    }
                    return templateModel;
                }),
                Total = messageTemplates.Count
            };

            return Json(gridModel);
        }

        public IActionResult Create()
        {
            var model = new MessageTemplateModel();
            
            //Stores
            model.PrepareStoresMappingModel(null, false, _storeService);
            model.AllowedTokens = _messageTokenProvider.GetListOfAllowedTokens();
            //available email accounts
            foreach (var ea in _emailAccountService.GetAllEmailAccounts())
                model.AvailableEmailAccounts.Add(ea.ToModel());

            return View(model);
        }
        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public IActionResult Create(MessageTemplateModel model, bool continueEditing)
        {
            if (ModelState.IsValid)
            {
                var messageTemplate = model.ToEntity();
                //attached file
                if (!model.HasAttachedDownload)
                    messageTemplate.AttachedDownloadId = "";
                if (model.SendImmediately)
                    messageTemplate.DelayBeforeSend = null;

                _messageTemplateService.InsertMessageTemplate(messageTemplate);

                SuccessNotification(_localizationService.GetResource("Admin.ContentManagement.MessageTemplates.AddNew"));

                if (continueEditing)
                {
                    //selected tab
                    SaveSelectedTabIndex();

                    return RedirectToAction("Edit", new { id = messageTemplate.Id });
                }
                return RedirectToAction("List");
            }


            //If we got this far, something failed, redisplay form
            model.HasAttachedDownload = !String.IsNullOrEmpty(model.AttachedDownloadId);
            model.AllowedTokens = _messageTokenProvider.GetListOfAllowedTokens();
            //available email accounts
            foreach (var ea in _emailAccountService.GetAllEmailAccounts())
                model.AvailableEmailAccounts.Add(ea.ToModel());
            //Store
            model.PrepareStoresMappingModel(null, true, _storeService);
            return View(model);

        }


        public IActionResult Edit(string id)
        {
            var messageTemplate = _messageTemplateService.GetMessageTemplateById(id);
            if (messageTemplate == null)
                //No message template found with the specified id
                return RedirectToAction("List");
            
            var model = messageTemplate.ToModel();
            model.SendImmediately = !model.DelayBeforeSend.HasValue;
            model.HasAttachedDownload = !String.IsNullOrEmpty(model.AttachedDownloadId);
            model.AllowedTokens = _messageTokenProvider.GetListOfAllowedTokens();
            //available email accounts
            foreach (var ea in _emailAccountService.GetAllEmailAccounts())
                model.AvailableEmailAccounts.Add(ea.ToModel());
            //Store
            model.PrepareStoresMappingModel(messageTemplate, false, _storeService);

            //locales
            AddLocales(_languageService, model.Locales, (locale, languageId) =>
            {
                locale.BccEmailAddresses = messageTemplate.GetLocalized(x => x.BccEmailAddresses, languageId, false, false);
                locale.Subject = messageTemplate.GetLocalized(x => x.Subject, languageId, false, false);
                locale.Body = messageTemplate.GetLocalized(x => x.Body, languageId, false, false);

                var emailAccountId = messageTemplate.GetLocalized(x => x.EmailAccountId, languageId, false, false);
                locale.EmailAccountId = !String.IsNullOrEmpty(emailAccountId) ? emailAccountId : _emailAccountSettings.DefaultEmailAccountId;
            });

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [FormValueRequired("save", "save-continue")]
        public IActionResult Edit(MessageTemplateModel model, bool continueEditing)
        {
            var messageTemplate = _messageTemplateService.GetMessageTemplateById(model.Id);
            if (messageTemplate == null)
                //No message template found with the specified id
                return RedirectToAction("List");
            
            if (ModelState.IsValid)
            {
                messageTemplate = model.ToEntity(messageTemplate);
                //attached file
                if (!model.HasAttachedDownload)
                    messageTemplate.AttachedDownloadId = "";
                if (model.SendImmediately)
                    messageTemplate.DelayBeforeSend = null;

                _messageTemplateService.UpdateMessageTemplate(messageTemplate);

                SuccessNotification(_localizationService.GetResource("Admin.ContentManagement.MessageTemplates.Updated"));
                
                if (continueEditing)
                {
                    //selected tab
                    SaveSelectedTabIndex();

                    return RedirectToAction("Edit",  new {id = messageTemplate.Id});
                }
                return RedirectToAction("List");
            }


            //If we got this far, something failed, redisplay form
            model.HasAttachedDownload = !String.IsNullOrEmpty(model.AttachedDownloadId);
            model.AllowedTokens =_messageTokenProvider.GetListOfAllowedTokens();
            //available email accounts
            foreach (var ea in _emailAccountService.GetAllEmailAccounts())
                model.AvailableEmailAccounts.Add(ea.ToModel());
            //Store
            model.PrepareStoresMappingModel(messageTemplate, true, _storeService);

            return View(model);
        }

        [HttpPost]
        public IActionResult Delete(string id)
        {
            var messageTemplate = _messageTemplateService.GetMessageTemplateById(id);
            if (messageTemplate == null)
                //No message template found with the specified id
                return RedirectToAction("List");

            _messageTemplateService.DeleteMessageTemplate(messageTemplate);
            
            SuccessNotification(_localizationService.GetResource("Admin.ContentManagement.MessageTemplates.Deleted"));
            return RedirectToAction("List");
        }

        [HttpPost, ActionName("Edit")]
        [FormValueRequired("message-template-copy")]
        public IActionResult CopyTemplate(MessageTemplateModel model)
        {
            var messageTemplate = _messageTemplateService.GetMessageTemplateById(model.Id);
            if (messageTemplate == null)
                //No message template found with the specified id
                return RedirectToAction("List");

            try
            {
                var newMessageTemplate = _messageTemplateService.CopyMessageTemplate(messageTemplate);
                SuccessNotification("The message template has been copied successfully");
                return RedirectToAction("Edit", new { id = newMessageTemplate.Id });
            }
            catch (Exception exc)
            {
                ErrorNotification(exc.Message);
                return RedirectToAction("Edit", new { id = model.Id });
            }
        }

        public IActionResult TestTemplate(string id, string languageId = "")
        {
            var messageTemplate = _messageTemplateService.GetMessageTemplateById(id);
            if (messageTemplate == null)
                //No message template found with the specified id
                return RedirectToAction("List");

            var model = new TestMessageTemplateModel
            {
                Id = messageTemplate.Id,
                LanguageId = languageId
            };
            var tokens = _messageTokenProvider.GetListOfAllowedTokens().Distinct().ToList();
            //filter them to the current template
            var subject = messageTemplate.GetLocalized(mt => mt.Subject, languageId);
            var body = messageTemplate.GetLocalized(mt => mt.Body, languageId);

            tokens = tokens.Where(x => subject.Contains(x) || body.Contains(x)).ToList();
            model.Tokens = tokens;

            return View(model);
        }

        [HttpPost, ActionName("TestTemplate")]
        [FormValueRequired("send-test")]
        
        public IActionResult TestTemplate(TestMessageTemplateModel model, IFormCollection form)
        {
            var messageTemplate = _messageTemplateService.GetMessageTemplateById(model.Id);
            if (messageTemplate == null)
                //No message template found with the specified id
                return RedirectToAction("List");

            var tokens = new List<Token>();
            foreach (var formKey in form.Keys)
                if (formKey.StartsWith("token_", StringComparison.OrdinalIgnoreCase))
                {
                    var tokenKey = formKey.Substring("token_".Length).Replace("%", "");
                    var tokenValue = form[formKey];
                    tokens.Add(new Token(tokenKey, tokenValue));
                }

            _workflowMessageService.SendTestEmail(messageTemplate.Id, model.SendTo, tokens, model.LanguageId);

            if (ModelState.IsValid)
            {
                SuccessNotification(_localizationService.GetResource("Admin.ContentManagement.MessageTemplates.Test.Success"));
            }

            return RedirectToAction("Edit", new {id = messageTemplate.Id});
        }

        #endregion
    }
}
