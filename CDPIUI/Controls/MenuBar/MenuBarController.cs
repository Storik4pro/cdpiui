using CDPIUI.Controls.Default;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WinUIMenuBar = Microsoft.UI.Xaml.Controls.MenuBar;

namespace CDPIUI.Controls.MenuManagement
{
    public sealed class MenuBarController : IDisposable
    {
        private const string WindowScope = "window";

        private readonly WinUIMenuBar _menuBar;
        private readonly Frame _frame;
        private readonly List<MenuEntry> _rootEntries = [];
        private readonly Dictionary<string, MenuEntry> _entriesById =
            new(StringComparer.Ordinal);

        private TemplatePage _activePage;
        private MenuBarSession _activeSession;
        private bool _isDisposed;

        internal MenuBarController(WinUIMenuBar menuBar, Frame frame)
        {
            _menuBar = menuBar;
            _frame = frame;

            RegisterWindowMenu();
            _frame.Navigated += Frame_Navigated;

            if (_frame.Content is TemplatePage page)
                ActivatePage(page);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _frame.Navigated -= Frame_Navigated;
            DeactivatePage();
        }

        internal void ReplaceScope(string scopeId, DataTemplate template, object dataContext)
        {
            if (_isDisposed)
                return;

            if (!TryMaterializeTemplate(template, dataContext, out var sections))
                return;

            RemoveScope(scopeId);

            foreach (var section in sections)
                IntegrateNode(section, null, scopeId, MenuBarItemLifetime.Weak);
        }

        internal void RemoveScope(string scopeId)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(scopeId))
                return;

            foreach (var entry in EnumerateEntries())
                entry.WeakScopes.Remove(scopeId);

            PruneUnclaimedEntries();
        }

        internal void RemoveScopes(string scopePrefix)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(scopePrefix))
                return;

            foreach (var entry in EnumerateEntries())
            {
                entry.WeakScopes.RemoveWhere(scope =>
                    scope.Equals(scopePrefix, StringComparison.Ordinal) ||
                    scope.StartsWith($"{scopePrefix}:", StringComparison.Ordinal));
            }

            PruneUnclaimedEntries();
        }

        internal MenuBarMutationResult AddSection(
            string scopeId,
            MenuBarItem section,
            MenuBarItemLifetime defaultLifetime,
            object dataContext)
        {
            if (_isDisposed || section == null)
                return MenuBarMutationResult.InvalidTarget;

            ApplyDataContext(section, dataContext);
            var result = IntegrateNode(section, null, scopeId, defaultLifetime);
            if (!result.IsValid)
                return MenuBarMutationResult.InvalidTarget;

            return result.WasAdded
                ? MenuBarMutationResult.Added
                : MenuBarMutationResult.Merged;
        }

        internal MenuBarMutationResult AddItem(
            string scopeId,
            string parentId,
            MenuFlyoutItemBase item,
            MenuBarItemLifetime defaultLifetime,
            object dataContext)
        {
            if (_isDisposed || item == null || string.IsNullOrWhiteSpace(parentId))
                return MenuBarMutationResult.InvalidTarget;

            if (!_entriesById.TryGetValue(parentId.Trim(), out var parent))
                return MenuBarMutationResult.NotFound;

            if (!IsContainer(parent.Element))
                return MenuBarMutationResult.InvalidTarget;

            ApplyDataContext(item, dataContext);
            var result = IntegrateNode(item, parent, scopeId, defaultLifetime);
            if (!result.IsValid)
                return MenuBarMutationResult.InvalidTarget;

            return result.WasAdded
                ? MenuBarMutationResult.Added
                : MenuBarMutationResult.Merged;
        }

        internal MenuBarMutationResult RemoveBranch(string publicId)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(publicId) ||
                !_entriesById.TryGetValue(publicId.Trim(), out var entry))
            {
                return MenuBarMutationResult.NotFound;
            }

            var weakClaimCount = CountWeakClaims(entry);
            ClearWeakClaims(entry);
            PruneUnclaimedEntries();

            if (!_entriesById.ContainsKey(publicId.Trim()))
                return MenuBarMutationResult.Removed;

            return weakClaimCount > 0
                ? MenuBarMutationResult.PartiallyRemoved
                : MenuBarMutationResult.Protected;
        }

        private void Frame_Navigated(object sender, NavigationEventArgs args)
        {
            var nextPage = args.Content as TemplatePage;

            if (ReferenceEquals(nextPage, _activePage))
            {
                _activeSession?.Refresh();
                return;
            }

            DeactivatePage();
            if (nextPage != null)
                ActivatePage(nextPage);
        }

        private void ActivatePage(TemplatePage page)
        {
            _activePage = page;
            _activeSession = new MenuBarSession(this, page);
            page.MenuBarSession = _activeSession;
            _activeSession.Refresh();
        }

        private void DeactivatePage()
        {
            if (_activeSession == null)
                return;

            _activeSession.Deactivate();
            if (ReferenceEquals(_activePage?.MenuBarSession, _activeSession))
                _activePage.MenuBarSession = null;

            _activeSession = null;
            _activePage = null;
        }

        private void RegisterWindowMenu()
        {
            foreach (var section in _menuBar.Items.ToList())
                RegisterExistingNode(section, null);
        }

        private void RegisterExistingNode(DependencyObject element, MenuEntry parent)
        {
            var publicId = NormalizeId(MenuBarNode.GetId(element));
            if (publicId != null && _entriesById.ContainsKey(publicId))
            {
                Debug.WriteLine($"MenuBar contains a duplicate Id '{publicId}'. The first item will be used for merging.");
                publicId = null;
            }

            var entry = new MenuEntry(
                publicId,
                element,
                parent,
                MenuBarNode.GetOrder(element));

            AddClaim(entry, WindowScope, ResolveLifetime(element, MenuBarItemLifetime.Strong));
            AddEntryToRegistry(entry, addToPhysicalTree: false);

            foreach (var child in GetChildren(element).ToList())
                RegisterExistingNode(child, entry);
        }

        private IntegrationResult IntegrateNode(
            DependencyObject source,
            MenuEntry parent,
            string scopeId,
            MenuBarItemLifetime defaultLifetime)
        {
            var children = TakeChildren(source);
            var publicId = NormalizeId(MenuBarNode.GetId(source));
            var lifetime = ResolveLifetime(source, defaultLifetime);

            if (publicId != null && _entriesById.TryGetValue(publicId, out var existing))
            {
                if (existing.Element.GetType() != source.GetType() ||
                    !ReferenceEquals(existing.Parent, parent))
                {
                    Debug.WriteLine(
                        $"MenuBar item '{publicId}' cannot be merged because its type or parent differs.");
                    return new IntegrationResult(false, false);
                }

                AddClaim(existing, scopeId, lifetime);
                foreach (var child in children)
                    IntegrateNode(child, existing, scopeId, defaultLifetime);

                return new IntegrationResult(true, false);
            }

            var entry = new MenuEntry(
                publicId,
                source,
                parent,
                MenuBarNode.GetOrder(source));

            AddClaim(entry, scopeId, lifetime);
            AddEntryToRegistry(entry, addToPhysicalTree: true);

            foreach (var child in children)
                IntegrateNode(child, entry, scopeId, defaultLifetime);

            return new IntegrationResult(true, true);
        }

        private void AddEntryToRegistry(MenuEntry entry, bool addToPhysicalTree)
        {
            var siblings = entry.Parent?.Children ?? _rootEntries;
            if (!addToPhysicalTree)
            {
                siblings.Add(entry);
                if (entry.PublicId != null)
                    _entriesById[entry.PublicId] = entry;
                return;
            }

            var index = siblings.FindIndex(candidate => candidate.Order > entry.Order);
            if (index < 0)
                index = siblings.Count;

            siblings.Insert(index, entry);
            if (entry.PublicId != null)
                _entriesById[entry.PublicId] = entry;

            if (entry.Parent == null)
            {
                _menuBar.Items.Insert(index, (MenuBarItem)entry.Element);
            }
            else
            {
                GetChildren(entry.Parent.Element).Insert(
                    index,
                    (MenuFlyoutItemBase)entry.Element);
            }
        }

        private void PruneUnclaimedEntries()
        {
            for (var index = _rootEntries.Count - 1; index >= 0; index--)
            {
                if (!PruneEntry(_rootEntries[index]))
                    continue;

                _rootEntries.RemoveAt(index);
            }
        }

        private bool PruneEntry(MenuEntry entry)
        {
            for (var index = entry.Children.Count - 1; index >= 0; index--)
            {
                if (!PruneEntry(entry.Children[index]))
                    continue;

                entry.Children.RemoveAt(index);
            }

            if (entry.IsStrong || entry.WeakScopes.Count > 0 || entry.Children.Count > 0)
                return false;

            if (entry.PublicId != null)
                _entriesById.Remove(entry.PublicId);

            if (entry.Parent == null)
                _menuBar.Items.Remove((MenuBarItem)entry.Element);
            else
                GetChildren(entry.Parent.Element).Remove((MenuFlyoutItemBase)entry.Element);

            return true;
        }

        private bool TryMaterializeTemplate(
            DataTemplate template,
            object dataContext,
            out IReadOnlyList<MenuBarItem> sections)
        {
            if (template == null)
            {
                sections = [];
                return true;
            }

            try
            {
                var content = template.LoadContent();
                ApplyDataContext(content as DependencyObject, dataContext);

                if (content is WinUIMenuBar stagedMenuBar)
                {
                    var stagedSections = stagedMenuBar.Items.ToList();
                    stagedMenuBar.Items.Clear();
                    foreach (var stagedSection in stagedSections)
                        ApplyDataContext(stagedSection, dataContext);
                    sections = stagedSections;
                    return true;
                }

                if (content is MenuBarItem section)
                {
                    sections = [section];
                    return true;
                }

                Debug.WriteLine("TemplatePage.MenuBarTemplate must create a MenuBar or MenuBarItem.");
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Could not materialize MenuBar template: {exception}");
            }

            sections = [];
            return false;
        }

        private static void ApplyDataContext(DependencyObject element, object dataContext)
        {
            if (element == null || dataContext == null)
                return;

            if (element is FrameworkElement frameworkElement &&
                frameworkElement.ReadLocalValue(FrameworkElement.DataContextProperty) ==
                DependencyProperty.UnsetValue)
            {
                frameworkElement.DataContext = dataContext;
            }

            foreach (var child in GetChildren(element))
                ApplyDataContext(child, dataContext);
        }

        private static List<MenuFlyoutItemBase> TakeChildren(DependencyObject element)
        {
            var collection = GetChildren(element);
            if (collection.Count == 0)
                return [];

            var children = collection.ToList();
            collection.Clear();
            return children;
        }

        private static IList<MenuFlyoutItemBase> GetChildren(DependencyObject element) =>
            element switch
            {
                MenuBarItem menuBarItem => menuBarItem.Items,
                MenuFlyoutSubItem subItem => subItem.Items,
                _ => EmptyMenuItemCollection.Instance,
            };

        private static bool IsContainer(DependencyObject element) =>
            element is MenuBarItem or MenuFlyoutSubItem;

        private static MenuBarItemLifetime ResolveLifetime(
            DependencyObject element,
            MenuBarItemLifetime defaultLifetime)
        {
            var lifetime = MenuBarNode.GetLifetime(element);
            return lifetime == MenuBarItemLifetime.Default
                ? defaultLifetime
                : lifetime;
        }

        private static void AddClaim(
            MenuEntry entry,
            string scopeId,
            MenuBarItemLifetime lifetime)
        {
            if (lifetime == MenuBarItemLifetime.Strong)
                entry.IsStrong = true;
            else
                entry.WeakScopes.Add(scopeId);
        }

        private IEnumerable<MenuEntry> EnumerateEntries()
        {
            foreach (var root in _rootEntries)
            {
                foreach (var entry in EnumerateEntry(root))
                    yield return entry;
            }
        }

        private static IEnumerable<MenuEntry> EnumerateEntry(MenuEntry entry)
        {
            yield return entry;
            foreach (var child in entry.Children)
            {
                foreach (var descendant in EnumerateEntry(child))
                    yield return descendant;
            }
        }

        private static int CountWeakClaims(MenuEntry entry) =>
            entry.WeakScopes.Count + entry.Children.Sum(CountWeakClaims);

        private static void ClearWeakClaims(MenuEntry entry)
        {
            entry.WeakScopes.Clear();
            foreach (var child in entry.Children)
                ClearWeakClaims(child);
        }

        private static string NormalizeId(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private sealed class MenuEntry
        {
            public MenuEntry(
                string publicId,
                DependencyObject element,
                MenuEntry parent,
                int order)
            {
                PublicId = publicId;
                Element = element;
                Parent = parent;
                Order = order;
            }

            public string PublicId { get; }
            public DependencyObject Element { get; }
            public MenuEntry Parent { get; }
            public int Order { get; }
            public bool IsStrong { get; set; }
            public HashSet<string> WeakScopes { get; } = new(StringComparer.Ordinal);
            public List<MenuEntry> Children { get; } = [];
        }

        private readonly struct IntegrationResult
        {
            public IntegrationResult(bool isValid, bool wasAdded)
            {
                IsValid = isValid;
                WasAdded = wasAdded;
            }

            public bool IsValid { get; }
            public bool WasAdded { get; }
        }

        private sealed class EmptyMenuItemCollection : IList<MenuFlyoutItemBase>
        {
            public static EmptyMenuItemCollection Instance { get; } = new();

            public MenuFlyoutItemBase this[int index]
            {
                get => throw new ArgumentOutOfRangeException(nameof(index));
                set => throw new NotSupportedException();
            }

            public int Count => 0;
            public bool IsReadOnly => true;
            public void Add(MenuFlyoutItemBase item) => throw new NotSupportedException();
            public void Clear() { }
            public bool Contains(MenuFlyoutItemBase item) => false;
            public void CopyTo(MenuFlyoutItemBase[] array, int arrayIndex) { }
            public IEnumerator<MenuFlyoutItemBase> GetEnumerator() =>
                Enumerable.Empty<MenuFlyoutItemBase>().GetEnumerator();
            public int IndexOf(MenuFlyoutItemBase item) => -1;
            public void Insert(int index, MenuFlyoutItemBase item) => throw new NotSupportedException();
            public bool Remove(MenuFlyoutItemBase item) => false;
            public void RemoveAt(int index) => throw new NotSupportedException();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
