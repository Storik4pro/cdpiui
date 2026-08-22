using CDPIUI.Controls.Default;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace CDPIUI.Controls.MenuManagement
{
    public sealed class MenuBarSession
    {
        private const string PageScopeName = "page";

        private readonly MenuBarController _controller;
        private readonly TemplatePage _page;
        private readonly string _scopePrefix;
        private bool _isActive = true;

        internal MenuBarSession(MenuBarController controller, TemplatePage page)
        {
            _controller = controller;
            _page = page;
            _scopePrefix = $"page:{Guid.NewGuid():N}";
        }

        public void Refresh()
        {
            if (!_isActive)
                return;

            _controller.ReplaceScope(
                BuildScopeId(PageScopeName),
                _page.MenuBarTemplate,
                _page.DataContext);
        }

        public void ReplaceScope(string scopeId, DataTemplate template)
        {
            if (!_isActive || string.IsNullOrWhiteSpace(scopeId))
                return;

            _controller.ReplaceScope(
                BuildScopeId($"state:{scopeId.Trim()}"),
                template,
                _page.DataContext);
        }

        public void ClearScope(string scopeId)
        {
            if (!_isActive || string.IsNullOrWhiteSpace(scopeId))
                return;

            _controller.RemoveScope(BuildScopeId($"state:{scopeId.Trim()}"));
        }

        public MenuBarMutationResult AddSection(
            MenuBarItem section,
            MenuBarItemLifetime defaultLifetime = MenuBarItemLifetime.Weak)
        {
            if (!_isActive || section == null)
                return MenuBarMutationResult.InvalidTarget;

            return _controller.AddSection(
                BuildScopeId(PageScopeName),
                section,
                defaultLifetime,
                _page.DataContext);
        }

        public MenuBarMutationResult AddItem(
            string parentId,
            MenuFlyoutItemBase item,
            MenuBarItemLifetime defaultLifetime = MenuBarItemLifetime.Weak)
        {
            if (!_isActive || item == null)
                return MenuBarMutationResult.InvalidTarget;

            return _controller.AddItem(
                BuildScopeId(PageScopeName),
                parentId,
                item,
                defaultLifetime,
                _page.DataContext);
        }

        public MenuBarMutationResult RemoveItem(string itemId) =>
            _isActive
                ? _controller.RemoveBranch(itemId)
                : MenuBarMutationResult.NotFound;

        public MenuBarMutationResult RemoveSection(string sectionId) =>
            _isActive
                ? _controller.RemoveBranch(sectionId)
                : MenuBarMutationResult.NotFound;

        internal void Deactivate()
        {
            if (!_isActive)
                return;

            _isActive = false;
            _controller.RemoveScopes(_scopePrefix);
        }

        private string BuildScopeId(string localScopeId) =>
            $"{_scopePrefix}:{localScopeId}";
    }
}
