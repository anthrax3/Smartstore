﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Collections;
using Smartstore.Core;
using Smartstore.Core.Catalog.Brands;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Catalog.Search;
using Smartstore.Core.Content.Menus;
using Smartstore.Core.Data;
using Smartstore.Core.Domain.Catalog;
using Smartstore.Core.Stores;
using Smartstore.Diagnostics;
using Smartstore.Web.TagHelpers;

namespace Smartstore.Web.Rendering
{
    /// <summary>
    /// A generic implementation of <see cref="IMenu" /> which represents a <see cref="MenuEntity"/> entity.
    /// </summary>
    internal class DatabaseMenu : MenuBase 
    {
        private static object s_lock = new object();

        private readonly Lazy<ICatalogSearchService> _catalogSearchService;
        private readonly Lazy<ICategoryService> _categoryService;
        private readonly IMenuStorage _menuStorage;
        private readonly CatalogSettings _catalogSettings;
        private readonly SearchSettings _searchSettings;
        private readonly IDictionary<string, Lazy<IMenuItemProvider, MenuItemProviderMetadata>> _menuItemProviders;

        public DatabaseMenu(
            string menuName,
            ICommonServices services,
            IMenuPublisher menuPublisher,
            Lazy<ICatalogSearchService> catalogSearchService,
            Lazy<ICategoryService> categoryService,
            IMenuStorage menuStorage,
            CatalogSettings catalogSettings,
            SearchSettings searchSettings,
            IEnumerable<Lazy<IMenuItemProvider, MenuItemProviderMetadata>> menuItemProviders)
        {
            Guard.NotEmpty(menuName, nameof(menuName));

            Name = menuName;
            Services = services;
            MenuPublisher = menuPublisher;

            _catalogSearchService = catalogSearchService;
            _categoryService = categoryService;
            _menuStorage = menuStorage;
            _catalogSettings = catalogSettings;
            _searchSettings = searchSettings;
            _menuItemProviders = menuItemProviders.ToDictionarySafe(x => x.Metadata.ProviderName, x => x);
        }

        public DbQuerySettings QuerySettings { get; set; } = DbQuerySettings.Default;

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public override string Name { get; }

        public override bool ApplyPermissions => true;

        public override void ResolveElementCount(TreeNode<MenuItem> curNode, bool deep = false)
        {
            if (curNode == null || !ContainsProvider("catalog") || !_catalogSettings.ShowCategoryProductNumber)
            {
                return;
            }

            try
            {
                using (Services.Chronometer.Step($"DatabaseMenu.ResolveElementsCount() for {curNode.Value.Text.NaIfEmpty()}"))
                {
                    // Perf: only resolve counts for categories in the current path.
                    while (curNode != null)
                    {
                        if (curNode.Children.Any(x => !x.Value.ElementsCount.HasValue))
                        {
                            lock (s_lock)
                            {
                                if (curNode.Children.Any(x => !x.Value.ElementsCount.HasValue))
                                {
                                    var nodes = deep ? curNode.SelectNodes(x => true, false) : curNode.Children.AsEnumerable();
                                    nodes = nodes.Where(x => x.Value.EntityId != 0);

                                    foreach (var node in nodes)
                                    {
                                        var isCategory = node.Value.EntityName.EqualsNoCase(nameof(Category));
                                        var isManufacturer = node.Value.EntityName.EqualsNoCase(nameof(Manufacturer));

                                        if (isCategory || isManufacturer)
                                        {
                                            var entityIds = new HashSet<int>();
                                            if (isCategory && _catalogSettings.ShowCategoryProductNumberIncludingSubcategories)
                                            {
                                                // Include sub-categories.
                                                node.Traverse(x =>
                                                {
                                                    entityIds.Add(x.Value.EntityId);
                                                }, true);
                                            }
                                            else
                                            {
                                                entityIds.Add(node.Value.EntityId);
                                            }

                                            var context = new CatalogSearchQuery()
                                                .VisibleOnly()
                                                .WithVisibility(ProductVisibility.Full)
                                                .HasStoreId(Services.StoreContext.CurrentStoreIdIfMultiStoreMode)
                                                .BuildFacetMap(false)
                                                .BuildHits(false);

                                            if (isCategory)
                                            {
                                                context = context.WithCategoryIds(null, entityIds.ToArray());
                                            }
                                            else
                                            {
                                                context = context.WithManufacturerIds(null, entityIds.ToArray());
                                            }

                                            if (!_searchSettings.IncludeNotAvailable)
                                            {
                                                context = context.AvailableOnly(true);
                                            }

                                            // TODO: (mh) (core) In the context of the lock statement cannot be waited.
                                            var query = _catalogSearchService.Value.SearchAsync(context).Await();
                                            node.Value.ElementsCount = query.TotalHitsCount;
                                        }
                                    }
                                }
                            }
                        }

                        curNode = curNode.Parent;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public override TreeNode<MenuItem> ResolveCurrentNode(ControllerContext context)
        {
            if (context == null || !ContainsProvider("catalog"))
            {
                return base.ResolveCurrentNode(context);
            }

            TreeNode<MenuItem> currentNode = null;

            try
            {
                // TODO: (mh) (core) Finish the job.
                //var rootContext = context.GetRootControllerContext();

                //int currentCategoryId = GetRequestValue<int?>(rootContext, "currentCategoryId") ?? GetRequestValue<int>(rootContext, "categoryId");
                //int currentProductId = 0;

                //if (currentCategoryId == 0)
                //{
                //    currentProductId = GetRequestValue<int?>(rootContext, "currentProductId") ?? GetRequestValue<int>(rootContext, "productId");
                //}

                //if (currentCategoryId == 0 && currentProductId == 0)
                //{
                //    // Possibly not a category node of a menu where the category tree is attached to.
                //    return base.ResolveCurrentNode(rootContext);
                //}

                //var cacheKey = $"sm.temp.category.breadcrumb.{currentCategoryId}-{currentProductId}";
                //currentNode = Services.RequestCache.Get(cacheKey, () =>
                //{
                //    var root = Root;
                //    TreeNode<MenuItem> node = null;

                //    if (currentCategoryId > 0)
                //    {
                //        node = root.SelectNodeById(currentCategoryId) ?? root.SelectNode(x => x.Value.EntityId == currentCategoryId);
                //    }

                //    if (node == null && currentProductId > 0)
                //    {
                //        var productCategories = _categoryService.Value.GetProductCategoriesByProductId(currentProductId);
                //        if (productCategories.Any())
                //        {
                //            currentCategoryId = productCategories[0].Category.Id;
                //            node = root.SelectNodeById(currentCategoryId) ?? root.SelectNode(x => x.Value.EntityId == currentCategoryId);
                //        }
                //    }

                //    return node;
                //});
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            return currentNode;
        }

        protected override async Task<TreeNode<MenuItem>> BuildAsync()
        {
            var db = Services.DbContext;
            var entities = await db.MenuItems
                .ApplyMenuFilter(0, Name)
                .ApplyStoreFilter(Services.StoreContext.CurrentStore.Id)
                .ToListAsync();

            var tree = entities.GetTree("DatabaseMenu", _menuItemProviders);

            return tree;
        }

        protected override string GetCacheKey()
        {
            var cacheKey = "{0}-{1}-{2}".FormatInvariant(
                Services.WorkContext.WorkingLanguage.Id,
                QuerySettings.IgnoreMultiStore ? 0 : Services.StoreContext.CurrentStore.Id,
                QuerySettings.IgnoreAcl ? "0" : Services.WorkContext.CurrentCustomer.GetRolesIdent());

            return cacheKey;
        }
    }
}
