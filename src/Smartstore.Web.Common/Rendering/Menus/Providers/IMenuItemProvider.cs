﻿using Smartstore.Collections;
using Smartstore.Core.Content.Menus;
using Smartstore.Web.TagHelpers;

namespace Smartstore.Web.Rendering
{
    public interface IMenuItemProvider
    {
        /// <summary>
        /// Converts a <see cref="MenuItemEntity"/> object and appends it to the parent tree node.
        /// </summary>
        /// <param name="request">Contains information about the request to the provider.</param>
        /// <returns>Appended node.</returns>
		TreeNode<MenuItem> Append(MenuItemProviderRequest request);
    }

    public class MenuItemProviderRequest
    {
        /// <summary>
        /// Represents the origin for the creation of the tree.
        /// </summary>
        public string Origin { get; set; }

        /// <summary>
        /// Node to which items are to be appended.
        /// </summary>
        public TreeNode<MenuItem> Parent { get; set; }

        /// <summary>
        /// Entity that is converted to a menu item.
        /// </summary>
        public MenuItemEntity Entity { get; set; }

        /// <summary>
        /// Inidicates whether the request is for backend menu editing.
        /// </summary>
        public bool IsEditMode => Origin.EqualsNoCase("EditMenu");
    }
}
