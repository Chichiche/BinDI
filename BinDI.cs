// https://github.com/Chichiche/BinDI

// ReSharper disable RedundantUsingDirective
// #undef BINDI_SUPPORT_VCONTAINER
// #undef BINDI_SUPPORT_R3
// #undef BINDI_SUPPORT_UNIRX
// #undef BINDI_SUPPORT_UNITASK
// #undef BINDI_SUPPORT_ADDRESSABLE
// #undef UNITY_EDITOR

#if BINDI_SUPPORT_VCONTAINER
using System.Collections.ObjectModel;
using VContainer;
using VContainer.Unity;
#endif

#if BINDI_SUPPORT_ADDRESSABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor.AddressableAssets;
#endif
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
#endif

#if BINDI_SUPPORT_R3
using R3;
#elif BINDI_SUPPORT_UNIRX
using UniRx;
#endif

#if BINDI_SUPPORT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace BinDI
{
    #region Lisence

/*
MIT License

Copyright (c) 2024 Chichiche

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

    #endregion Lisence

    #region Usings

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using UnityObject = UnityEngine.Object;

    #endregion Usings

    // ReSharper disable once InconsistentNaming
    internal static class BinDI { }

    #region Installation

    public static class BinDiInstaller
    {
#if BINDI_SUPPORT_VCONTAINER
        public static T RegisterBinDi<T>(this T builder, IAssemblyFilter assemblyFilter) where T : IContainerBuilder
        {
            return builder.RegisterBinDi(options: null, assemblyFilter);
        }
#endif

#if BINDI_SUPPORT_VCONTAINER
        // ReSharper disable once MemberCanBePrivate.Global
        public static T RegisterBinDi<T>(this T builder, BinDiOptions options = null, IAssemblyFilter assemblyFilter = null) where T : IContainerBuilder
        {
            var binDiScopeBuilder = new ContainerBuilder();
            if (options != null) binDiScopeBuilder.RegisterInstance(options);
            if (assemblyFilter != null) binDiScopeBuilder.RegisterInstance(assemblyFilter);
            PrefabBuilder.TryInstall(binDiScopeBuilder);
            using var binDiScope = binDiScopeBuilder.Build();
            var registrationBinder = binDiScope.Resolve<RegistrationBinder>();
            registrationBinder.Bind(builder, GlobalScope.Default);
            builder.RegisterInstance(binDiScope.Resolve<RegistrationProvider>());
            builder.RegisterInstance(binDiScope.Resolve<ConnectionBinder>());
            builder.RegisterInstance(binDiScope.Resolve<ConnectionProvider>());
            builder.RegisterInstance(binDiScope.Resolve<AppDomainProvider>());
            builder.RegisterInstance(binDiScope.Resolve<IAssemblyFilter>());
            builder.RegisterInstance(binDiScope.Resolve<BinDiOptions>());
            ScopedDisposable.TryInstall(builder);
            PrefabBuilder.TryInstall(builder);
            return builder;
        }
#endif
    }

    public sealed class BinDiOptions
    {
        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        public bool CollectAssemblyLogEnabled = false;

        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        public bool DomainRegistrationLogEnabled = false;

        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        public bool PubSubConnectionLogEnabled = false;

#if BINDI_SUPPORT_VCONTAINER
        public static void TryInstall(IContainerBuilder builder)
        {
            ScopedDisposable.TryInstall(builder);
            if (builder.Exists(typeof( BinDiOptions ), findParentScopes: true)) return;
            builder.Register<BinDiOptions>(Lifetime.Singleton);
        }
#endif
    }

    #endregion Installation

    #region Disposables

    public sealed class EmptyDisposable : IDisposable
    {
        public static readonly IDisposable Default = new EmptyDisposable();
        public void Dispose() { }
    }

    public sealed class RemoveDisposable<T> : IDisposable
    {
        readonly List<T> _list;
        readonly T _value;
        bool _disposed;

        public RemoveDisposable(List<T> list, T value)
        {
            _list = list;
            _value = value;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _list.Remove(_value);
            _disposed = true;
        }
    }

    public interface IScopedDisposable
    {
        void Add(IDisposable disposable);
        void AddRange(IEnumerable<IDisposable> disposables);
    }

    public sealed class ScopedDisposable : IScopedDisposable, IDisposable
    {
#if BINDI_SUPPORT_R3
        DisposableBag _bag;

        public void Add(IDisposable disposable)
        {
            _bag.Add(disposable);
        }

        public void AddRange(IEnumerable<IDisposable> disposables)
        {
            foreach (var disposable in disposables)
            {
                _bag.Add(disposable);
            }
        }

        public void Dispose()
        {
            _bag.Dispose();
        }
#elif BINDI_SUPPORT_UNIRX
        readonly CompositeDisposable _compositeDisposable = new();

        public void Add(IDisposable disposable)
        {
            _compositeDisposable.Add(disposable);
        }

        public void AddRange(IEnumerable<IDisposable> disposables)
        {
            foreach (var disposable in disposables)
            {
                _compositeDisposable.Add(disposable);
            }
        }

        public void Dispose()
        {
            _compositeDisposable.Dispose();
        }
#else
        readonly List<IDisposable> _disposables = new();

        public void Add(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        public void AddRange(IEnumerable<IDisposable> disposables)
        {
            _disposables.AddRange(disposables);
        }

        public void Dispose()
        {
            for (var i = 0; i < _disposables.Count; i++)
            {
                if (_disposables[i] == null) continue;
                _disposables[i].Dispose();
            }
            _disposables.Clear();
        }
#endif

#if BINDI_SUPPORT_VCONTAINER
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( ScopedDisposable ))) return;
            builder.Register<IScopedDisposable, ScopedDisposable>(Lifetime.Scoped);
        }
#endif
    }

#if BINDI_SUPPORT_VCONTAINER
    public sealed class OnDestroyTrigger : MonoBehaviour
    {
        public Action OnDestroyHandler;

        void OnDestroy()
        {
            OnDestroyHandler?.Invoke();
        }
    }
#endif

    public static class DisposableUtil
    {
        public static T AddTo<T>(this T disposable, IScopedDisposable scopedDisposable) where T : IDisposable
        {
            scopedDisposable.Add(disposable);
            return disposable;
        }

#if BINDI_SUPPORT_VCONTAINER
        // ReSharper disable once MemberCanBePrivate.Global
        public static T AddTo<T>(this T disposable, IObjectResolver scope) where T : IDisposable
        {
            if (! scope.TryResolve<IScopedDisposable>(out var scopedDisposable))
            {
                throw new InvalidOperationException($"{nameof( IScopedDisposable )} is not registered in the current scope.");
            }
            scopedDisposable.Add(disposable);
            return disposable;
        }
#endif

#if BINDI_SUPPORT_VCONTAINER
        public static IScopedObjectResolver CreateDisposableLinkedChildScope(this IObjectResolver scope, Action<IContainerBuilder> installation = null)
        {
            var childScope = scope.CreateScope(installation);
            childScope.AddTo(scope);
            return childScope;
        }
#endif
    }

    #endregion Disposables

    #region EditorWindow

#if UNITY_EDITOR && BINDI_SUPPORT_VCONTAINER
    // ReSharper disable once InconsistentNaming
    public sealed class BinDIWindow : EditorWindow
    {
        SearchField _searchField;
        [SerializeField] BinDiHeader _header;
        [SerializeField] BinDiTree _tree;

        [MenuItem("Window/" + nameof( BinDI ))]
        public static void ShowWindow()
        {
            GetWindow<BinDIWindow>().Show();
        }

        void OnEnable()
        {
            _searchField ??= new SearchField();
            _header ??= new BinDiHeader();
            _header.Refresh();
            _tree ??= new BinDiTree();
            _tree.Refresh(_header);
        }

        void OnGUI()
        {
            _tree.View.searchString = _searchField.OnGUI(EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight)), _tree.View.searchString);
            _tree.View.OnGUI(EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)));
            EditorGUILayout.BeginHorizontal();
            if (GUI.Button(EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight)), "Reload")) _tree.Refresh(_header);
            if (GUI.Button(EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight)), "Collapse All")) _tree.View.CollapseAll();
            if (GUI.Button(EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight)), "Expand All")) _tree.View.ExpandAll();
            EditorGUILayout.EndHorizontal();
        }

        [Serializable]
        sealed class BinDiHeader
        {
            readonly MultiColumnHeaderState.Column[] _columns =
            {
                new()
                {
                    headerContent = new GUIContent("Type"),
                    width = 60,
                    autoResize = false,
                    canSort = false,
                },
                new()
                {
                    headerContent = new GUIContent("Name/Script"),
                    width = 300,
                    autoResize = false,
                    canSort = false,
                },
#if BINDI_SUPPORT_ADDRESSABLE
                new()
                {
                    headerContent = new GUIContent("Asset"),
                    width = 300,
                    autoResize = false,
                    canSort = false,
                },
#endif
            };

            [SerializeField] MultiColumnHeaderState _state;
            public MultiColumnHeader View { get; private set; }

            public void Refresh()
            {
                _state ??= new MultiColumnHeaderState(_columns);
                View ??= new MultiColumnHeader(_state);
            }
        }

        [Serializable]
        sealed class BinDiTree
        {
            [SerializeField] TreeViewState _treeViewState;
            public BinDiTreeView View { get; private set; }

            public void Refresh(BinDiHeader windowHeader)
            {
                _treeViewState ??= new TreeViewState();
                View ??= new BinDiTreeView(_treeViewState, windowHeader.View);
                View.Refresh();
                View.Reload();
            }
        }

        sealed class BinDiTreeViewItem : TreeViewItem
        {
            public string ItemType;
            public MonoScript Script;
            public UnityObject Asset;
            public string Address;
        }

        sealed class BinDiTreeViewState
        {
            public BinDiTreeViewItem RootItem { get; private set; }

            public void Refresh()
            {
                var refreshScopeBuilder = new ContainerBuilder();
                RegistrationProvider.TryInstall(refreshScopeBuilder);
                ConnectionProvider.TryInstall(refreshScopeBuilder);
                using var refreshScope = refreshScopeBuilder.Build();
                var registrationProvider = refreshScope.Resolve<RegistrationProvider>();
                var connectionProvider = refreshScope.Resolve<ConnectionProvider>();
                var id = 0;
                RootItem = new BinDiTreeViewItem { id = id++, depth = -1 };
                foreach (var scope in registrationProvider.Scopes)
                {
                    var scopeItem = new BinDiTreeViewItem
                    {
                        id = id++,
                        ItemType = "Scope",
                        displayName = scope.ToString(),
                        Script = scope is Type scopeType ? FindScriptOrDefault(scopeType.Name) : null,
                    };
                    RootItem.AddChild(scopeItem);
                    foreach (var installation in registrationProvider.GetInstallation(scope))
                    {
                        var installerType = (Type)typeof( Installation ).GetField("_installerType", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(installation);
                        var installationItem = new BinDiTreeViewItem
                        {
                            id = id++,
                            ItemType = "Installer",
                            displayName = installerType.Name,
                            Script = FindScriptOrDefault(installerType.Name),
                        };
                        scopeItem.AddChild(installationItem);
                    }
                    foreach (var registration in registrationProvider.GetRegistrations(scope))
                    {
                        switch (registration)
                        {
                            case DomainRegistration domainRegistration:
                                var domainType = (Type)typeof( DomainRegistration ).GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(domainRegistration);
                                var domainItem = new BinDiTreeViewItem
                                {
                                    id = id++,
                                    ItemType = "Domain",
                                    displayName = domainType.Name,
                                    Script = FindScriptOrDefault(domainType.Name),
                                };
                                scopeItem.AddChild(domainItem);
                                foreach (var subscribableType in connectionProvider.GetSubscribableTypes(domainType))
                                {
                                    var subscribableItem = new BinDiTreeViewItem
                                    {
                                        id = id++,
                                        ItemType = "From",
                                        displayName = subscribableType.Name,
                                        Script = FindScriptOrDefault(subscribableType.Name),
                                    };
                                    domainItem.AddChild(subscribableItem);
                                }
                                foreach (var publishableType in connectionProvider.GetPublishableTypes(domainType))
                                {
                                    var publishableItem = new BinDiTreeViewItem
                                    {
                                        id = id++,
                                        ItemType = "To",
                                        displayName = publishableType.Name,
                                        Script = FindScriptOrDefault(publishableType.Name),
                                    };
                                    domainItem.AddChild(publishableItem);
                                }
                                break;
#if BINDI_SUPPORT_ADDRESSABLE
                            case AddressableRegistration addressableRegistration:
                                var assetType = (Type)typeof( AddressableRegistration ).GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(addressableRegistration);
                                var address = (string)typeof( AddressableRegistration ).GetField("_address", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(addressableRegistration);
                                var asset = GetAssetAtAddress(address);
                                var assetItem = new BinDiTreeViewItem
                                {
                                    id = id++,
                                    ItemType = "Asset",
                                    displayName = assetType.Name,
                                    Script = FindScriptOrDefault(assetType.Name),
                                    Asset = asset,
                                    Address = address,
                                };
                                scopeItem.AddChild(assetItem);
                                break;

                                static UnityObject GetAssetAtAddress(string address)
                                {
                                    if (AddressableAssetSettingsDefaultObject.Settings == null) return null;
                                    return AddressableAssetSettingsDefaultObject.Settings.groups
                                        .SelectMany(group => group.entries)
                                        .Where(entry => entry.address == address)
                                        .Select(entry => AssetDatabase.GUIDToAssetPath(entry.guid))
                                        .Select(AssetDatabase.LoadAssetAtPath<UnityObject>)
                                        .FirstOrDefault();
                                }
#endif
                        }
                    }
                }
            }
        }

        sealed class BinDiTreeView : TreeView
        {
            readonly BinDiTreeViewState _state = new();

            public BinDiTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader)
            {
                columnIndexForTreeFoldouts = 1;
                showAlternatingRowBackgrounds = true;
            }

            public void Refresh()
            {
                _state.Refresh();
            }

            protected override TreeViewItem BuildRoot()
            {
                SetupDepthsFromParentsAndChildren(_state.RootItem);
                return _state.RootItem;
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                for (var i = 0; i < args.GetNumVisibleColumns(); i++)
                {
                    var item = (BinDiTreeViewItem)args.item;
                    var cellRect = args.GetCellRect(i);
                    var columnIndex = args.GetColumn(i);

                    switch (columnIndex)
                    {
                        case 0:
                            GUI.Label(cellRect, item.ItemType);
                            break;
                        case 1:
                            GUI.enabled = false;
                            cellRect.xMin += GetContentIndent(args.item);
                            if (item.Script) EditorGUI.ObjectField(cellRect, item.Script, typeof( MonoScript ), false);
                            else EditorGUI.LabelField(cellRect, item.displayName);
                            GUI.enabled = true;
                            break;
                        case 2:
                            GUI.enabled = false;
                            if (item.Asset) EditorGUI.ObjectField(cellRect, item.Asset, typeof( UnityObject ), false);
                            else if (! string.IsNullOrEmpty(item.Address)) EditorGUI.LabelField(cellRect, $"(missing) {item.Address}");
                            GUI.enabled = true;
                            break;
                    }
                }
            }
        }

        static MonoScript FindScriptOrDefault(string scriptName)
        {
            return AssetDatabase
                .FindAssets(scriptName)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path) == scriptName)
                .Select(AssetDatabase.LoadAssetAtPath<MonoScript>)
                .FirstOrDefault(asset => asset);
        }
    }
#endif

    #endregion EditorWindow

    #region Registration Attributes

#if BINDI_SUPPORT_VCONTAINER
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class InstallToAttribute : Attribute
    {
        public object Scope { get; }

        public InstallToAttribute(object scope)
        {
            Scope = scope;
        }
    }

    public interface IInstallable
    {
        bool TryInstall(IContainerBuilder builder);
    }
#endif

#if BINDI_SUPPORT_VCONTAINER
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RegisterToAttribute : Attribute
    {
        public object Scope { get; }
        public Lifetime Lifetime { get; }

        public RegisterToAttribute(object scope, Lifetime lifetime = Lifetime.Singleton)
        {
            Scope = scope;
            Lifetime = lifetime;
        }
    }
#endif

#if BINDI_SUPPORT_VCONTAINER
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RegisterToGlobalAttribute : Attribute
    {
        public object Scope => GlobalScope.Default;
        public Lifetime Lifetime { get; }

        public RegisterToGlobalAttribute(Lifetime lifetime = Lifetime.Singleton)
        {
            Lifetime = lifetime;
        }
    }
#endif

#if BINDI_SUPPORT_VCONTAINER && BINDI_SUPPORT_ADDRESSABLE
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RegisterAddressableToAttribute : Attribute
    {
        public object Scope { get; }
        public Lifetime Lifetime => Lifetime.Singleton;
        public readonly string Address;

        public RegisterAddressableToAttribute(object scope, string address = null)
        {
            Scope = scope;
            Address = address;
        }
    }
#endif

#if BINDI_SUPPORT_VCONTAINER && BINDI_SUPPORT_ADDRESSABLE
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RegisterAddressableToGlobalAttribute : Attribute
    {
        public object Scope => GlobalScope.Default;
        public Lifetime Lifetime => Lifetime.Singleton;
        public readonly string Address;

        public RegisterAddressableToGlobalAttribute(string address = null)
        {
            Address = address;
        }
    }
#endif

#if BINDI_SUPPORT_VCONTAINER
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ScopedComponentAttribute : Attribute { }
#endif

    #endregion Registration Attributes

    #region Registration Modules

    public interface IAssemblyFilter
    {
        bool CanCollect(string assemblyFullName);
    }

    public sealed class AssemblyWhiteListFilter : IAssemblyFilter
    {
        readonly string[] _whiteList;

        public AssemblyWhiteListFilter(params string[] whiteList)
        {
            _whiteList = whiteList;
        }

        public bool CanCollect(string assemblyFullName)
        {
            return _whiteList.Contains(assemblyFullName);
        }
    }

    public sealed class AssemblyBlackListFilter : IAssemblyFilter
    {
        static readonly string[] _defaultAssemblyBlackList =
        {
            "Unity.",
            "UnityEngine,",
            "UnityEngine.",
            "UnityEditor,",
            "UnityEditor.",
            "PlayerBuildProgramLibrary.",
            "unityplastic,",
            "Bee.",
            "nunit.",
            "PsdPlugin,",
            "Microsoft.",
            "System,",
            "System.",
            "mscorlib,",
            "netstandard,",
            "Mono.",
            "JetBrains.",
            "log4net,",
            "VContainer,",
            "VContainer.",
            "R3,",
            "R3.",
            "ObservableCollections,",
            "ObservableCollections.",
            "UniRx,",
            "UniTask,",
            "UniTask.",
            "BinDi,"
        };

        readonly string[] _assemblyNames;

        public AssemblyBlackListFilter(params string[] customAssemblyBlackList)
        {
            _assemblyNames = _defaultAssemblyBlackList.Concat(customAssemblyBlackList).ToArray();
        }

        public bool CanCollect(string assemblyFullName)
        {
            return ! _assemblyNames.Any(assemblyFullName.StartsWith);
        }

#if BINDI_SUPPORT_VCONTAINER
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( IAssemblyFilter ), includeInterfaceTypes: true, findParentScopes: true)) return;
            builder.Register<IAssemblyFilter, AssemblyBlackListFilter>(Lifetime.Singleton).WithParameter(Array.Empty<string>());
        }
#endif
    }

    public sealed class AppDomainProvider
    {
        readonly BinDiOptions _binDiOptions;
        readonly IAssemblyFilter _assemblyFilter;
        readonly Type[] _concreteClasses;

        public AppDomainProvider(BinDiOptions binDiOptions, IAssemblyFilter assemblyFilter)
        {
            _binDiOptions = binDiOptions;
            _assemblyFilter = assemblyFilter;
            _concreteClasses = CollectDomainConcreteClasses().ToArray();
        }

        public int ConcreteClassCount => _concreteClasses.Length;
        public Type GetConcreteClass(int index) => _concreteClasses[index];

        IEnumerable<Type> CollectDomainConcreteClasses()
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(CollectAssemblyConcreteClasses);
        }

        IEnumerable<Type> CollectAssemblyConcreteClasses(Assembly assembly)
        {
            if (! _assemblyFilter.CanCollect(assembly.FullName)) yield break;
            if (_binDiOptions.CollectAssemblyLogEnabled) Debug.Log($"BinDI collect assembly concrete types from {assembly.FullName}.");
            foreach (var definedType in assembly.DefinedTypes)
            {
                if (! definedType.IsClass || definedType.IsAbstract) continue;
                yield return definedType;
            }
        }

#if BINDI_SUPPORT_VCONTAINER
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( AppDomainProvider ))) return;
            builder.Register<AppDomainProvider>(Lifetime.Singleton);
            BinDiOptions.TryInstall(builder);
            AssemblyBlackListFilter.TryInstall(builder);
        }
#endif
    }

#if BINDI_SUPPORT_VCONTAINER
    public sealed class RegistrationProvider
    {
        static readonly ReadOnlyCollection<Installation> EmptyInstallations = new( Array.Empty<Installation>() );
        static readonly ReadOnlyCollection<IRegistration> EmptyRegistrations = new( Array.Empty<IRegistration>() );
        readonly Dictionary<object, List<Installation>> _scopedInstallationListSourceMap = new() { { GlobalScope.Default, new List<Installation>() } };
        readonly Dictionary<object, List<IRegistration>> _scopedRegistrationListSourceMap = new() { { GlobalScope.Default, new List<IRegistration>() } };
        readonly Dictionary<object, ReadOnlyCollection<Installation>> _scopedInstallationListMap;
        readonly Dictionary<object, ReadOnlyCollection<IRegistration>> _scopedRegistrationListMap;

        public RegistrationProvider(AppDomainProvider appDomainProvider)
        {
            for (var i = 0; i < appDomainProvider.ConcreteClassCount; i++) Collect(appDomainProvider.GetConcreteClass(i));
            _scopedRegistrationListMap = _scopedRegistrationListSourceMap.ToDictionary(kv => kv.Key, kv => new ReadOnlyCollection<IRegistration>(kv.Value));
            _scopedInstallationListMap = _scopedInstallationListSourceMap.ToDictionary(kv => kv.Key, kv => new ReadOnlyCollection<Installation>(kv.Value));
            Scopes = new ReadOnlyCollection<object>(_scopedRegistrationListSourceMap.Keys.Concat(_scopedInstallationListSourceMap.Keys).Distinct().ToArray());
        }

        public ReadOnlyCollection<object> Scopes { get; }

        public ReadOnlyCollection<Installation> GetInstallation<T>(T scope)
        {
            return _scopedInstallationListMap.GetValueOrDefault(scope, EmptyInstallations);
        }

        public ReadOnlyCollection<IRegistration> GetRegistrations<T>(T scope)
        {
            return _scopedRegistrationListMap.GetValueOrDefault(scope, EmptyRegistrations);
        }

        void Collect(Type concreteType)
        {
            foreach (var attribute in concreteType.GetCustomAttributes())
            {
                Collect(concreteType, attribute);
            }
        }

        void Collect(Type concreteType, Attribute attribute)
        {
            switch (attribute)
            {
                case InstallToAttribute installToAttribute:
                    if (concreteType.GetInterface(nameof( IInstallable )) == null)
                    {
                        Debug.LogWarning($"{concreteType} is marked with {nameof( installToAttribute )}, but does not implement {nameof( IInstallable )}.");
                        return;
                    }
                    GetScopedInstallationList(installToAttribute.Scope).Add(new Installation(concreteType));
                    break;
                case RegisterToAttribute registerToAttribute:
                    GetScopedRegistrationList(registerToAttribute.Scope).Add(new DomainRegistration(concreteType, registerToAttribute.Lifetime));
                    break;
                case RegisterToGlobalAttribute registerToGlobalAttribute:
                    _scopedRegistrationListSourceMap[GlobalScope.Default].Add(new DomainRegistration(concreteType, registerToGlobalAttribute.Lifetime));
                    break;
#if BINDI_SUPPORT_ADDRESSABLE
                case RegisterAddressableToAttribute registerAddressableToAttribute:
                    GetScopedRegistrationList(registerAddressableToAttribute.Scope).Add(new AddressableRegistration(concreteType, registerAddressableToAttribute.Address));
                    break;
                case RegisterAddressableToGlobalAttribute registerAddressableToGlobalAttribute:
                    _scopedRegistrationListSourceMap[GlobalScope.Default].Add(new AddressableRegistration(concreteType, registerAddressableToGlobalAttribute.Address));
                    break;
#endif
            }
        }

        List<Installation> GetScopedInstallationList(object scope)
        {
            if (_scopedInstallationListSourceMap.TryGetValue(scope, out var installationList)) return installationList;
            installationList = new List<Installation>();
            _scopedInstallationListSourceMap.Add(scope, installationList);
            return installationList;
        }

        List<IRegistration> GetScopedRegistrationList(object scope)
        {
            if (_scopedRegistrationListSourceMap.TryGetValue(scope, out var registrationList)) return registrationList;
            registrationList = new List<IRegistration>();
            _scopedRegistrationListSourceMap.Add(scope, registrationList);
            return registrationList;
        }

        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( RegistrationProvider ), findParentScopes: true)) return;
            builder.Register<RegistrationProvider>(Lifetime.Singleton);
            AppDomainProvider.TryInstall(builder);
        }
    }
#endif

#if BINDI_SUPPORT_VCONTAINER
    public sealed class GlobalScope
    {
        public static readonly GlobalScope Default = new();
    }
#endif

#if BINDI_SUPPORT_VCONTAINER
    public interface IRegistration
    {
        bool TryRegister(IContainerBuilder builder);
    }
#endif

#if BINDI_SUPPORT_VCONTAINER
    public sealed class Installation
    {
        readonly Type _installerType;

        public Installation(Type installerType)
        {
            _installerType = installerType;
        }

        public IInstallable GetInstaller(IObjectResolver scope)
        {
            var installer = Activator.CreateInstance(_installerType);
            scope.Inject(installer);
            return (IInstallable)installer;
        }
    }
#endif

#if BINDI_SUPPORT_VCONTAINER
    public sealed class DomainRegistration : IRegistration
    {
        readonly Type _type;
        readonly Lifetime _lifetime;

        public DomainRegistration(Type type, Lifetime lifetime)
        {
            _type = type;
            _lifetime = lifetime;
        }

        public bool TryRegister(IContainerBuilder builder)
        {
            if (builder.Exists(_type)) return false;
            EntryPointsBuilder.EnsureDispatcherRegistered(builder);
            builder.Register(_type, _lifetime).AsSelf().AsImplementedInterfaces();
            return true;
        }
    }
#endif

#if BINDI_SUPPORT_VCONTAINER && BINDI_SUPPORT_ADDRESSABLE
    public sealed class AddressableRegistration : IRegistration
    {
        readonly Type _type;
        readonly string _address;

        public AddressableRegistration(Type type, string address)
        {
            _type = type;
            _address = GetAddress(type, address);
        }

        public bool TryRegister(IContainerBuilder builder)
        {
            if (builder.Exists(_type)) return false;
            var operation = LoadAddressable(_address);
            builder.RegisterDisposeCallback(UnloadCallback(operation));
            if (IsComponent(_type)) RegisterPrefab(builder, _type, operation);
            else RegisterAsset(builder, operation);
            return true;
        }

        static string GetAddress(MemberInfo type, string address)
        {
            return address ?? type.Name;
        }

        static AsyncOperationHandle<UnityObject> LoadAddressable(string address)
        {
#if !UNITY_EDITOR
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                throw new InvalidOperationException("Addressable asset system is not set up.");
            }

            var asset = AddressableAssetSettingsDefaultObject.Settings.groups
                .SelectMany(group => group.entries)
                .Where(entry => entry.address == address)
                .Select(entry => AssetDatabase.GUIDToAssetPath(entry.guid))
                .Select(AssetDatabase.LoadAssetAtPath<UnityObject>)
                .FirstOrDefault();

            if (asset == null)
            {
                throw new MissingReferenceException($"Addressable asset with address [{address}] was not found.");
            }
#endif
            var operation = Addressables.LoadAssetAsync<UnityObject>(address);
            operation.WaitForCompletion();
            return operation;
        }

        static Action<IObjectResolver> UnloadCallback(AsyncOperationHandle<UnityObject> operation)
        {
            return _ => Addressables.Release(operation);
        }

        static bool IsComponent(Type type)
        {
            return type.IsSubclassOf(typeof( Component ));
        }

        static void RegisterPrefab(IContainerBuilder builder, Type componentType, AsyncOperationHandle<UnityObject> operation)
        {
            var gameObject = (GameObject)operation.Result;
            var component = gameObject.GetComponent(componentType);
            builder.RegisterInstance(component).AsSelf().AsImplementedInterfaces();
        }

        static void RegisterAsset(IContainerBuilder builder, AsyncOperationHandle<UnityObject> operation)
        {
            builder.RegisterInstance(operation.Result).AsSelf().AsImplementedInterfaces();
        }
    }
#endif

#if BINDI_SUPPORT_VCONTAINER
    public sealed class RegistrationBinder
    {
        readonly BinDiOptions _binDiOptions;
        readonly RegistrationProvider _registrationProvider;
        readonly IObjectResolver _scope;

        public RegistrationBinder(BinDiOptions binDiOptions, RegistrationProvider registrationProvider, IObjectResolver scope)
        {
            _binDiOptions = binDiOptions;
            _registrationProvider = registrationProvider;
            _scope = scope;
        }

        public void Bind<T>(IContainerBuilder builder, T scope)
        {
            if (scope == null) return;
            var scopeName = GetScopeName(scope);
            TryRegisterScopedModules(builder, scope, scopeName);
        }

        string GetScopeName<T>(T scope)
        {
            return _binDiOptions.DomainRegistrationLogEnabled ? scope.ToString() : default;
        }

        void TryRegisterScopedModules<T>(IContainerBuilder builder, T scope, string scopeName)
        {
            var installations = _registrationProvider.GetInstallation(scope);
            for (var i = 0; i < installations.Count; i++) TryInstall(builder, installations[i], scopeName);
            var registrations = _registrationProvider.GetRegistrations(scope);
            for (var i = 0; i < registrations.Count; i++) TryRegister(builder, registrations[i], scopeName);
        }

        void TryInstall(IContainerBuilder builder, Installation installation, string scopeName)
        {
            var installer = installation.GetInstaller(_scope);
            if (! installer.TryInstall(builder)) return;
            if (_binDiOptions.DomainRegistrationLogEnabled) Debug.Log($"{nameof( BinDI )} installed [{installer}] to [{scopeName}].");
        }

        void TryRegister(IContainerBuilder builder, IRegistration registration, string scopeName)
        {
            if (! registration.TryRegister(builder)) return;
            if (_binDiOptions.DomainRegistrationLogEnabled) Debug.Log($"{nameof( BinDI )} registered [{registration.GetType().GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(registration)}] to [{scopeName}].");
        }

        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( RegistrationBinder ), findParentScopes: true)) return;
            builder.Register<RegistrationBinder>(Lifetime.Scoped);
            BinDiOptions.TryInstall(builder);
            RegistrationProvider.TryInstall(builder);
        }
    }
#endif

    public static class RegistrationUtil
    {
#if BINDI_SUPPORT_VCONTAINER
        static readonly object[] _scopesArgument = new object[1];
#endif

#if BINDI_SUPPORT_VCONTAINER
        public static IScopedObjectResolver BuildBinDiScope(this ContainerBuilder builder, object targetScope, Action<IContainerBuilder> installation = null)
        {
            using var temporaryScope = builder.Build();
            return temporaryScope.CreateBinDiScope(targetScope, installation);
        }
#endif

#if BINDI_SUPPORT_VCONTAINER
        // ReSharper disable once MemberCanBePrivate.Global
        public static IScopedObjectResolver BuildBinDiScope(this ContainerBuilder builder, IReadOnlyList<object> targetScopes, Action<IContainerBuilder> installation = null)
        {
            using var temporaryScope = builder.Build();
            return temporaryScope.CreateBinDiScope(targetScopes, installation);
        }
#endif

#if BINDI_SUPPORT_VCONTAINER
        // ReSharper disable once MemberCanBePrivate.Global
        public static IScopedObjectResolver CreateBinDiScope(this IObjectResolver scope, object targetScope, Action<IContainerBuilder> installation = null)
        {
            return scope.CreateScope(builder => RegisterBinDiScope(builder, scope, targetScope, installation));
        }
#endif

#if BINDI_SUPPORT_VCONTAINER
        // ReSharper disable once MemberCanBePrivate.Global
        public static IScopedObjectResolver CreateBinDiScope(this IObjectResolver scope, IReadOnlyList<object> targetScopes, Action<IContainerBuilder> installation = null)
        {
            return scope.CreateScope(builder => RegisterBinDiScopes(builder, scope, targetScopes, installation));
        }
#endif

#if BINDI_SUPPORT_VCONTAINER
        // ReSharper disable once MemberCanBePrivate.Global
        public static IContainerBuilder RegisterBinDiScope(this IContainerBuilder builder, IObjectResolver parentScope, object targetScope, Action<IContainerBuilder> installation = null)
        {
            _scopesArgument[0] = targetScope;
            return RegisterBinDiScopes(builder, parentScope, _scopesArgument, installation);
        }
#endif

#if BINDI_SUPPORT_VCONTAINER
        // ReSharper disable once MemberCanBePrivate.Global
        public static IContainerBuilder RegisterBinDiScopes(this IContainerBuilder builder, IObjectResolver parentScope, IReadOnlyList<object> targetScopes, Action<IContainerBuilder> installation = null)
        {
            if (! parentScope.TryResolve<RegistrationBinder>(out var registrationBinder))
            {
                throw new InvalidOperationException($"{nameof( RegistrationBinder )} is not registered in the current scope.");
            }
            for (var i = 0; i < targetScopes.Count; i++)
            {
                registrationBinder.Bind(builder, targetScopes[i]);
            }
            installation?.Invoke(builder);
            return builder;
        }
#endif
    }

    #endregion Registration Modules

    #region Publishable Interfaces

    public interface IPublishable
    {
        void Publish();
    }

    public interface IPublishable<in T>
    {
        void Publish(T value);
    }

#if BINDI_SUPPORT_UNITASK
    public interface IAsyncPublishable
    {
        UniTask PublishAsync();
    }
#endif

#if BINDI_SUPPORT_UNITASK
    public interface IAsyncPublishable<in T>
    {
        UniTask PublishAsync(T value);
    }
#endif

    #endregion Publishable Interfaces

    #region Subscribable Interfaces

    public interface ISubscribable
    {
        IDisposable Subscribe(IPublishable publishable);
    }

    public interface ISubscribable<out T>
    {
        IDisposable Subscribe(IPublishable<T> publishable);
    }

    public interface IBufferedSubscribable<out T> : ISubscribable<T>
    {
        bool HasValue { get; }
        T Value { get; }
    }

#if BINDI_SUPPORT_UNITASK
    public interface IAsyncSubscribable
    {
        IDisposable Subscribe(IAsyncPublishable asyncPublishable);
    }
#endif

#if BINDI_SUPPORT_UNITASK
    public interface IAsyncSubscribable<out T>
    {
        IDisposable Subscribe(IAsyncPublishable<T> asyncPublishable);
    }
#endif

    #endregion Subscribable Interfaces

    #region Connection Attributes

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class SubscribeToAttribute : Attribute
    {
        public Type PublishableType { get; }

        public SubscribeToAttribute(Type publishableType)
        {
            PublishableType = publishableType;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class PublishFromAttribute : Attribute
    {
        public Type SubscribableType { get; }

        public PublishFromAttribute(Type subscribableType)
        {
            SubscribableType = subscribableType;
        }
    }

    #endregion Connection Attributes

    #region Brokers

    public class Broker : IPublishable, ISubscribable, IDisposable
    {
#if BINDI_SUPPORT_R3
        readonly Subject<Unit> _subject = new();
        Observer<Unit> _observer;
        bool _disposed;

        public Observable<Unit> AsObservable() => _subject;
        public Observer<Unit> AsObserver() => _observer ??= _subject.AsObserver();

        public void Publish()
        {
            if (_disposed) return;
            _subject.OnNext(Unit.Default);
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return ! _disposed
                ? _subject.Subscribe(publishable, static (_, p) => p.Publish())
                : Disposable.Empty;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _observer?.Dispose();
            _subject.Dispose();
            _disposed = true;
        }
#elif BINDI_SUPPORT_UNIRX
        readonly Subject<Unit> _subject = new();
        bool _disposed;

        public IObservable<Unit> AsObservable() => _subject;
        public IObserver<Unit> AsObserver() => _subject;

        public void Publish()
        {
            if (_disposed) return;
            _subject.OnNext(Unit.Default);
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return ! _disposed
                ? _subject.SubscribeWithState(publishable, static (_, p) => p.Publish())
                : Disposable.Empty;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _subject.Dispose();
            _disposed = true;
        }
#else
        readonly List<IPublishable> _publishables = new();
        bool _disposed;

        public void Publish()
        {
            if (_disposed) return;
            for (var i = 0; i < _publishables.Count; i++)
            {
                if (_publishables[i] == null) continue;
                _publishables[i].Publish();
            }
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            if (_disposed) return EmptyDisposable.Default;
            _publishables.Add(publishable);
            return new RemoveDisposable<IPublishable>(_publishables, publishable);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _publishables.Clear();
            _disposed = true;
        }
#endif
    }

    public class Broker<T> : ISubscribable, IPublishable<T>, ISubscribable<T>, IDisposable
    {
#if BINDI_SUPPORT_R3
        readonly Subject<T> _subject = new();
        Observer<T> _observer;
        bool _disposed;

        public Observable<T> AsObservable() => _subject;
        public Observer<T> AsObserver() => _observer ??= _subject.AsObserver();

        public void Publish(T value)
        {
            if (_disposed) return;
            _subject.OnNext(value);
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return ! _disposed
                ? _subject.Subscribe(publishable, static (_, p) => p.Publish())
                : Disposable.Empty;
        }

        public IDisposable Subscribe(IPublishable<T> publishable)
        {
            return ! _disposed
                ? _subject.Subscribe(publishable, static (value, p) => p.Publish(value))
                : Disposable.Empty;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _observer?.Dispose();
            _subject.Dispose();
            _disposed = true;
        }
#elif BINDI_SUPPORT_UNIRX
        readonly Subject<T> _subject = new();
        bool _disposed;

        public IObservable<T> AsObservable() => _subject;
        public IObserver<T> AsObserver() => _subject;

        public void Publish(T value)
        {
            if (_disposed) return;
            _subject.OnNext(value);
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return ! _disposed
                ? _subject.SubscribeWithState(publishable, static (_, p) => p.Publish())
                : Disposable.Empty;
        }

        public IDisposable Subscribe(IPublishable<T> publishable)
        {
            return ! _disposed
                ? _subject.SubscribeWithState(publishable, static (value, p) => p.Publish(value))
                : Disposable.Empty;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _subject.Dispose();
            _disposed = true;
        }
#else
        readonly List<IPublishable<T>> _publishables = new();
        readonly Broker _broker = new();
        bool _disposed;

        public void Publish(T value)
        {
            if (_disposed) return;
            for (var i = 0; i < _publishables.Count; i++)
            {
                if (_publishables[i] == null) continue;
                _publishables[i].Publish(value);
            }
            _broker.Publish();
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return _broker.Subscribe(publishable);
        }

        public IDisposable Subscribe(IPublishable<T> publishable)
        {
            if (_disposed) return EmptyDisposable.Default;
            _publishables.Add(publishable);
            return new RemoveDisposable<IPublishable<T>>(_publishables, publishable);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _publishables.Clear();
            _broker.Dispose();
            _disposed = true;
        }
#endif
    }

#if BINDI_SUPPORT_UNITASK
    public class AsyncBroker : IAsyncPublishable, IAsyncSubscribable, IDisposable
    {
        static readonly Stack<List<IAsyncPublishable>> _subscriberListPool = new();
        readonly List<IAsyncPublishable> _subscribers = RentAsyncSubscriberList();
        bool _disposed;

        static List<IAsyncPublishable> RentAsyncSubscriberList()
        {
            return _subscriberListPool.TryPop(out var list)
                ? list
                : new List<IAsyncPublishable>(16);
        }

        public UniTask PublishAsync()
        {
            return ! _disposed
                ? UniTask.WhenAll(_subscribers.Select(subscriber => subscriber.PublishAsync()))
                : UniTask.CompletedTask;
        }

        public IDisposable Subscribe(IAsyncPublishable asyncPublishable)
        {
            if (_disposed) return EmptyDisposable.Default;
            _subscribers.Add(asyncPublishable);
            return new RemoveDisposable<IAsyncPublishable>(_subscribers, asyncPublishable);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscribers.Clear();
            _subscriberListPool.Push(_subscribers);
        }
    }
#endif

#if BINDI_SUPPORT_UNITASK
    public class AsyncBroker<T> : IAsyncPublishable<T>, IAsyncSubscribable<T>, IDisposable
    {
        readonly List<IAsyncPublishable<T>> _subscribers = new();
        bool _disposed;

        public UniTask PublishAsync(T value)
        {
            return ! _disposed
                ? UniTask.WhenAll(_subscribers.Select(subscriber => subscriber.PublishAsync(value)))
                : UniTask.CompletedTask;
        }

        public IDisposable Subscribe(IAsyncPublishable<T> asyncPublishable)
        {
            if (_disposed) return EmptyDisposable.Default;
            _subscribers.Add(asyncPublishable);
            return new RemoveDisposable<IAsyncPublishable<T>>(_subscribers, asyncPublishable);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscribers.Clear();
        }
    }
#endif

    #endregion Brokers

    #region Properties

    public class Property<T> : ISubscribable, IBufferedSubscribable<T>, IPublishable<T>, IDisposable
    {
#if BINDI_SUPPORT_R3
        readonly ReactiveProperty<T> _property = new();
        Observer<T> _observer;
        bool _disposed;

        public Observable<T> AsObservable() => _property;
        public Observer<T> AsObserver() => _observer ??= _property.AsObserver();

        public bool HasValue { get; private set; }
        public T Value => _property.Value;

        public void Publish(T value)
        {
            if (_disposed) return;
            HasValue = true;
            _property.Value = value;
            OnPublished(value);
        }

        protected virtual void OnPublished(T value) { }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return ! _disposed
                ? _property.Subscribe(publishable, static (_, p) => p.Publish())
                : Disposable.Empty;
        }

        public IDisposable Subscribe(IPublishable<T> publishable)
        {
            return ! _disposed
                ? _property.Subscribe(publishable, static (value, p) => p.Publish(value))
                : Disposable.Empty;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _property.Dispose();
            _observer?.Dispose();
            _disposed = true;
        }
#elif BINDI_SUPPORT_UNIRX
        readonly ReactiveProperty<T> _property = new();
        IObserver<T> _observer;
        bool _disposed;

        public IObservable<T> AsObservable() => _property;
        public IObserver<T> AsObserver() => _observer ??= Observer.Create<T>(value => _property.Value = value);

        public bool HasValue { get; private set; }
        public T Value => _property.Value;

        public void Publish(T value)
        {
            if (_disposed) return;
            HasValue = true;
            _property.Value = value;
            OnPublished(value);
        }

        protected virtual void OnPublished(T value) { }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return ! _disposed
                ? _property.SubscribeWithState(publishable, static (_, p) => p.Publish())
                : Disposable.Empty;
        }

        public IDisposable Subscribe(IPublishable<T> publishable)
        {
            return ! _disposed
                ? _property.SubscribeWithState(publishable, static (value, p) => p.Publish(value))
                : Disposable.Empty;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _property.Dispose();
            _disposed = true;
        }
#else
        readonly Broker<T> _broker = new();
        bool _disposed;

        public bool HasValue { get; private set; }
        public T Value { get; private set; }

        public void Publish(T value)
        {
            if (_disposed) return;
            HasValue = true;
            Value = value;
            _broker.Publish(value);
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return _broker.Subscribe(publishable);
        }

        public IDisposable Subscribe(IPublishable<T> publishable)
        {
            return _broker.Subscribe(publishable);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _broker.Dispose();
            _disposed = true;
        }
#endif
    }

    public abstract class ReadOnlyProperty<T> : ISubscribable, IBufferedSubscribable<T>, IDisposable
    {
#if BINDI_SUPPORT_R3
        readonly ReactiveProperty<T> _property = new();
        bool _disposed;

        public Observable<T> AsObservable() => _property;

        public bool HasValue { get; private set; }
        public T Value => _property.Value;

        protected void Publish(T value)
        {
            if (_disposed) return;
            HasValue = true;
            _property.Value = value;
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return ! _disposed
                ? _property.Subscribe(publishable, static (_, p) => p.Publish())
                : Disposable.Empty;
        }

        public IDisposable Subscribe(IPublishable<T> publishable)
        {
            if (_disposed) return Disposable.Empty;
            if (HasValue) publishable.Publish(Value);
            return _property.Subscribe(publishable, static (value, p) => p.Publish(value));
        }

        void IDisposable.Dispose()
        {
            if (_disposed) return;
            _property.Dispose();
            _disposed = true;
        }
#elif BINDI_SUPPORT_UNIRX
        readonly ReactiveProperty<T> _property = new();
        bool _disposed;

        public IObservable<T> AsObservable() => _property;

        public bool HasValue { get; private set; }
        public T Value => _property.Value;

        protected void Publish(T value)
        {
            if (_disposed) return;
            HasValue = true;
            _property.Value = value;
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return ! _disposed
                ? _property.SubscribeWithState(publishable, static (_, p) => p.Publish())
                : Disposable.Empty;
        }

        public IDisposable Subscribe(IPublishable<T> publishable)
        {
            if (_disposed) return Disposable.Empty;
            if (HasValue) publishable.Publish(Value);
            return _property.SubscribeWithState(publishable, static (value, p) => p.Publish(value));
        }

        void IDisposable.Dispose()
        {
            if (_disposed) return;
            _property.Dispose();
            _disposed = true;
        }
#else
        readonly Broker<T> _broker = new();
        bool _disposed;

        public bool HasValue { get; private set; }
        public T Value { get; private set; }

        protected void Publish(T value)
        {
            if (_disposed) return;
            HasValue = true;
            Value = value;
            _broker.Publish(value);
        }

        public IDisposable Subscribe(IPublishable publishable)
        {
            return _broker.Subscribe(publishable);
        }

        public IDisposable Subscribe(IPublishable<T> publishable)
        {
            return _broker.Subscribe(publishable);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _broker.Dispose();
            _disposed = true;
        }
#endif
    }

    #endregion Properties

    #region Publishables

    public sealed class ActionPublishable : IPublishable
    {
        readonly Action _publish;

        public ActionPublishable(Action publish)
        {
            _publish = publish;
        }

        public void Publish()
        {
            _publish();
        }
    }

    public sealed class ActionPublishable<T> : IPublishable<T>
    {
        readonly Action<T> _publish;

        public ActionPublishable(Action<T> publish)
        {
            _publish = publish;
        }

        public void Publish(T value)
        {
            _publish(value);
        }
    }

    #endregion

    #region Connection Modules

    public sealed class ConnectionProvider
    {
        readonly ReadOnlyCollection<Type> _emptyTypes = new( Array.Empty<Type>() );
        readonly Dictionary<Type, List<Type>> _subscribableTypesSourceMap = new();
        readonly Dictionary<Type, ReadOnlyCollection<Type>> _subscribableTypesMap;
        readonly Dictionary<Type, List<Type>> _publishableTypesSourceMap = new();
        readonly Dictionary<Type, ReadOnlyCollection<Type>> _publishableTypesMap;

        public ConnectionProvider(AppDomainProvider appDomainProvider)
        {
            CollectConnections(appDomainProvider);
            _subscribableTypesMap = _subscribableTypesSourceMap.ToDictionary(kv => kv.Key, kv => new ReadOnlyCollection<Type>(kv.Value));
            _publishableTypesMap = _publishableTypesSourceMap.ToDictionary(kv => kv.Key, kv => new ReadOnlyCollection<Type>(kv.Value));
        }

        public ReadOnlyCollection<Type> GetSubscribableTypes(Type publishableType)
        {
            return _subscribableTypesMap.GetValueOrDefault(publishableType, _emptyTypes);
        }

        public ReadOnlyCollection<Type> GetPublishableTypes(Type subscribableType)
        {
            return _publishableTypesMap.GetValueOrDefault(subscribableType, _emptyTypes);
        }

        void CollectConnections(AppDomainProvider appDomainProvider)
        {
            for (var i = 0; i < appDomainProvider.ConcreteClassCount; i++) CollectConnections(appDomainProvider.GetConcreteClass(i));
        }

        void CollectConnections(Type concreteClass)
        {
            foreach (var attribute in concreteClass.GetCustomAttributes()) CollectConnection(concreteClass, attribute);
        }

        void CollectConnection(Type concreteClass, Attribute attribute)
        {
            switch (attribute)
            {
                case SubscribeToAttribute subscribeToAttribute:
                    CollectSubscribeToConnection(concreteClass, subscribeToAttribute);
                    break;
                case PublishFromAttribute publishFromAttribute:
                    CollectPublishFromConnection(concreteClass, publishFromAttribute);
                    break;
            }
        }

        void CollectSubscribeToConnection(Type subscribableType, SubscribeToAttribute subscribeToAttribute)
        {
            AddPublishableTypes(subscribableType, subscribeToAttribute.PublishableType);
            AddSubscribableTypes(subscribeToAttribute.PublishableType, subscribableType);
        }

        void CollectPublishFromConnection(Type publishableType, PublishFromAttribute publishFromAttribute)
        {
            AddSubscribableTypes(publishableType, publishFromAttribute.SubscribableType);
            AddPublishableTypes(publishFromAttribute.SubscribableType, publishableType);
        }

        void AddPublishableTypes(Type subscribableType, Type publishableType)
        {
            if (! _publishableTypesSourceMap.ContainsKey(subscribableType))
            {
                _publishableTypesSourceMap.Add(subscribableType, new List<Type>());
            }
            if (_publishableTypesSourceMap[subscribableType].Contains(publishableType)) return;
            _publishableTypesSourceMap[subscribableType].Add(publishableType);
        }

        void AddSubscribableTypes(Type publishableType, Type subscribableType)
        {
            if (! _subscribableTypesSourceMap.ContainsKey(publishableType))
            {
                _subscribableTypesSourceMap.Add(publishableType, new List<Type>());
            }
            if (_subscribableTypesSourceMap[publishableType].Contains(subscribableType)) return;
            _subscribableTypesSourceMap[publishableType].Add(subscribableType);
        }

#if BINDI_SUPPORT_VCONTAINER
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( ConnectionProvider ), findParentScopes: true)) return;
            builder.Register<ConnectionProvider>(Lifetime.Singleton);
            AppDomainProvider.TryInstall(builder);
        }
#endif
    }

#if BINDI_SUPPORT_VCONTAINER
    public sealed class ConnectionBinder
    {
        readonly Type[] _genericParameterArguments = new Type[1];
        readonly object[] _subscribeArguments = new object[1];
        readonly BinDiOptions _binDiOptions;
        readonly ConnectionProvider _connectionProvider;

        public ConnectionBinder(BinDiOptions binDiOptions, ConnectionProvider connectionProvider)
        {
            _binDiOptions = binDiOptions;
            _connectionProvider = connectionProvider;
        }

        public void Bind<T>(IContainerBuilder builder, T instance)
        {
            var instanceType = instance.GetType();
            var publishableTypes = _connectionProvider.GetPublishableTypes(instanceType);
            var subscribableTypes = _connectionProvider.GetSubscribableTypes(instanceType);
            if (publishableTypes.Count + subscribableTypes.Count == 0) return;
            builder.RegisterBuildCallback(scope =>
            {
                for (var i = 0; i < publishableTypes.Count; i++)
                {
                    if (! scope.TryResolve(publishableTypes[i], out var publishable)) continue;
                    Connect(instance, publishable).AddTo(scope);
                }
                for (var i = 0; i < subscribableTypes.Count; i++)
                {
                    if (! scope.TryResolve(subscribableTypes[i], out var subscribable)) continue;
                    Connect(subscribable, instance).AddTo(scope);
                }
            });
        }

        IDisposable Connect(object subscribable, object publishable)
        {
            if (subscribable is ISubscribable voidSubscribable && publishable is IPublishable voidPublishable) return ConnectVoidPubSub(voidSubscribable, voidPublishable);
            if (TryGetGenericArgument(subscribable.GetType(), typeof( ISubscribable<> ), out var valueType)) return ConnectValuePubSub(valueType, subscribable, publishable);
#if BINDI_SUPPORT_UNITASK
            if (subscribable is IAsyncSubscribable asyncSubscribable && publishable is IAsyncPublishable asyncPublishable) return ConnectAsyncVoidPubSub(asyncSubscribable, asyncPublishable);
            if (TryGetGenericArgument(subscribable.GetType(), typeof( ISubscribable<> ), out var asyncValueType)) return ConnectAsyncValuePubSub(asyncValueType, subscribable, publishable);
#endif
            throw new ArgumentException($"Failed to connect [{subscribable}] to [{publishable}].");
        }

        static bool TryGetGenericArgument(Type type, Type genericDefinition, out Type genericArgument)
        {
            foreach (var instanceInterface in type.GetInterfaces())
            {
                if (! instanceInterface.IsGenericType) continue;
                var genericTypeDefinition = instanceInterface.GetGenericTypeDefinition();
                if (genericTypeDefinition != genericDefinition) continue;
                genericArgument = instanceInterface.GenericTypeArguments[0];
                return true;
            }
            genericArgument = default;
            return false;
        }

        IDisposable ConnectVoidPubSub(ISubscribable subscribable, IPublishable publishable)
        {
            var connection = subscribable.Subscribe(publishable);
            if (_binDiOptions.PubSubConnectionLogEnabled) Debug.Log($"{nameof( BinDI )} connected [{subscribable}] to [{publishable}].");
            return connection;
        }

        IDisposable ConnectValuePubSub(Type valueType, object subscribable, object publishable)
        {
            _genericParameterArguments[0] = valueType;
            var subscribableType = typeof( ISubscribable<> ).MakeGenericType(_genericParameterArguments);
            var subscribeMethod = subscribableType.GetMethod("Subscribe");
            return subscribeMethod != null
                ? Connect(subscribable, publishable, subscribeMethod)
                : throw new ArgumentException($"Failed to connect [{subscribable}] to [{publishable}].");
        }

#if BINDI_SUPPORT_UNITASK
        IDisposable ConnectAsyncVoidPubSub(IAsyncSubscribable subscribable, IAsyncPublishable publishable)
        {
            var connection = subscribable.Subscribe(publishable);
            if (_binDiOptions.PubSubConnectionLogEnabled) Debug.Log($"{nameof( BinDI )} connected [{subscribable}] to [{publishable}].");
            return connection;
        }

        IDisposable ConnectAsyncValuePubSub(Type valueType, object subscribable, object publishable)
        {
            _genericParameterArguments[0] = valueType;
            var subscribableType = typeof( IAsyncSubscribable<> ).MakeGenericType(_genericParameterArguments);
            var subscribeMethod = subscribableType.GetMethod("Subscribe");
            return subscribeMethod != null
                ? Connect(subscribable, publishable, subscribeMethod)
                : throw new ArgumentException($"Failed to connect [{subscribable}] to [{publishable}].");
        }
#endif

        IDisposable Connect(object subscribable, object publishable, MethodInfo subscribeMethod)
        {
            _subscribeArguments[0] = publishable;
            var connection = (IDisposable)subscribeMethod.Invoke(subscribable, _subscribeArguments);
            if (_binDiOptions.PubSubConnectionLogEnabled) Debug.Log($"{nameof( BinDI )} connected [{subscribable}] to [{publishable}].");
            return connection;
        }

        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( ConnectionBinder ), findParentScopes: true)) return;
            builder.Register<ConnectionBinder>(Lifetime.Singleton);
            BinDiOptions.TryInstall(builder);
            ConnectionProvider.TryInstall(builder);
        }
    }
#endif

    public static class ConnectionUtil
    {
        public static IDisposable Subscribe(this ISubscribable subscribable, Action publish)
        {
            return subscribable.Subscribe(new ActionPublishable(publish));
        }

        public static IDisposable Subscribe<T>(this ISubscribable<T> subscribable, Action<T> publish)
        {
            return subscribable.Subscribe(new ActionPublishable<T>(publish));
        }

#if BINDI_SUPPORT_R3
        public static IDisposable Subscribe<T>(this Observable<T> observable, IPublishable publishable)
        {
            return observable.Subscribe(publishable, static (_, s) => s.Publish());
        }
#endif

#if BINDI_SUPPORT_R3
        public static IDisposable Subscribe<T>(this Observable<T> observable, IPublishable<T> publishable)
        {
            return observable.Subscribe(publishable, static (v, s) => s.Publish(v));
        }
#endif

#if BINDI_SUPPORT_R3
        public static IDisposable SubscribeWithState<TValue, TState>(this Observable<TValue> observable, TState state, Action<TValue, TState> onNext)
        {
            return observable.Subscribe((state, onNext), static (value, t) => t.onNext(value, t.state));
        }
#endif

#if !BINDI_SUPPORT_R3 && BINDI_SUPPORT_UNIRX
        public static IDisposable Subscribe<T>(this IObservable<T> observable, IPublishable publishable)
        {
            return observable.SubscribeWithState(publishable, static (_, s) => s.Publish());
        }
#endif

#if !BINDI_SUPPORT_R3 && BINDI_SUPPORT_UNIRX
        public static IDisposable Subscribe<T>(this IObservable<T> observable, IPublishable<T> publishable)
        {
            return observable.SubscribeWithState(publishable, static (v, s) => s.Publish(v));
        }
#endif

#if !BINDI_SUPPORT_R3 && BINDI_SUPPORT_UNIRX
        public static IDisposable Subscribe<TValue, TState>(this IObservable<TValue> observable, TState state, Action<TValue, TState> onNext)
        {
            return observable.SubscribeWithState((state, onNext), static (value, t) => t.onNext(value, t.state));
        }
#endif
    }

    #endregion

    #region GameObjectModules

#if BINDI_SUPPORT_VCONTAINER
    public sealed class GameObjectScopeBuilder
    {
        readonly List<Component> _getComponentsBuffer = new( 1024 );
        readonly RegistrationBinder _registrationBinder;
        readonly ConnectionBinder _connectionBinder;
        readonly IObjectResolver _scope;

        public GameObjectScopeBuilder(RegistrationBinder registrationBinder, ConnectionBinder connectionBinder, IObjectResolver scope)
        {
            _registrationBinder = registrationBinder;
            _connectionBinder = connectionBinder;
            _scope = scope;
        }

        public IScopedObjectResolver Build(GameObject gameObject, Action<IContainerBuilder> installation = null)
        {
            if (! gameObject) return default;
            var scope = _scope.CreateScope(builder =>
            {
                installation?.Invoke(builder);
                Build(gameObject, builder);
            });
            if (gameObject.TryGetComponent<OnDestroyTrigger>(out var onDestroyTrigger)) onDestroyTrigger.OnDestroyHandler = scope.Dispose;
            else gameObject.AddComponent<OnDestroyTrigger>().OnDestroyHandler = scope.Dispose;
            return scope;
        }

        public void Build(GameObject gameObject, IContainerBuilder builder)
        {
            if (! gameObject) return;
            if (! builder.Exists(typeof( GameObject ))) builder.RegisterInstance(gameObject);
            RegisterChildComponents(gameObject, builder);
            gameObject.GetComponents(_getComponentsBuffer);
            BindComponents(builder);
            var transform = gameObject.transform;
            for (var i = 0; i < transform.childCount; i++) TryBuildChild(builder, transform.GetChild(i));
        }

        void RegisterChildComponents(GameObject gameObject, IContainerBuilder builder)
        {
            gameObject.GetComponentsInChildren(_getComponentsBuffer);
            for (var i = 0; i < _getComponentsBuffer.Count; i++)
            {
                if (! _getComponentsBuffer[i]) continue;
                if (! builder.Exists(_getComponentsBuffer[i].GetType()))
                {
                    builder.RegisterInstance(_getComponentsBuffer[i]).AsSelf();
                }
            }
        }

        void TryBuildChild(IContainerBuilder builder, Transform child)
        {
            if (! child) return;
            child.GetComponents(_getComponentsBuffer);
            var isScope = _getComponentsBuffer.Any(component => component && component.GetType().GetCustomAttribute<ScopedComponentAttribute>() != null);
            if (isScope)
            {
                BuildNewScope(builder, child.gameObject);
                return;
            }
            BindComponents(builder);
            for (var i = 0; i < child.childCount; i++) TryBuildChild(builder, child.GetChild(i));
        }

        void BindComponents(IContainerBuilder builder)
        {
            for (var i = 0; i < _getComponentsBuffer.Count; i++)
            {
                var component = _getComponentsBuffer[i];
                if (! component) continue;
                var componentType = component.GetType();
                builder.RegisterBuildCallback(scope => scope.Inject(component));
                _registrationBinder.Bind(builder, componentType);
                _connectionBinder.Bind(builder, component);
            }
        }

        static void BuildNewScope(IContainerBuilder builder, GameObject gameObject)
        {
            builder.RegisterBuildCallback(scope => scope.Resolve<GameObjectScopeBuilder>().Build(gameObject));
        }

        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( GameObjectScopeBuilder ))) return;
            builder.Register<GameObjectScopeBuilder>(Lifetime.Scoped);
            RegistrationBinder.TryInstall(builder);
            ConnectionBinder.TryInstall(builder);
        }
    }
#endif

#if BINDI_SUPPORT_VCONTAINER
    public sealed class PrefabBuilder
    {
        readonly GameObjectScopeBuilder _gameObjectScopeBuilder;
        readonly IObjectResolver _scope;

        public PrefabBuilder(GameObjectScopeBuilder gameObjectScopeBuilder, IObjectResolver scope)
        {
            _gameObjectScopeBuilder = gameObjectScopeBuilder;
            _scope = scope;
        }

        public T Build<T>(T prefab, Transform parent = null, Action<IContainerBuilder> install = null) where T : Component
        {
            if (prefab == null) return null;
            var instance = UnityObject.Instantiate(prefab, parent);
            var scope = _scope.CreateScope(builder => ConfigureScope(builder, install, instance.gameObject));
            if (instance.TryGetComponent<OnDestroyTrigger>(out var onDestroyTrigger)) onDestroyTrigger.OnDestroyHandler = scope.Dispose;
            else instance.gameObject.AddComponent<OnDestroyTrigger>().OnDestroyHandler = scope.Dispose;
            return instance;
        }

        public GameObject Build(GameObject prefab, Transform parent = null, Action<IContainerBuilder> install = null)
        {
            if (prefab == null) return null;
            var instance = UnityObject.Instantiate(prefab, parent);
            var scope = _scope.CreateScope(builder => ConfigureScope(builder, install, instance));
            if (instance.TryGetComponent<OnDestroyTrigger>(out var onDestroyTrigger)) onDestroyTrigger.OnDestroyHandler = scope.Dispose;
            else instance.AddComponent<OnDestroyTrigger>().OnDestroyHandler = scope.Dispose;
            return instance;
        }

        void ConfigureScope(IContainerBuilder builder, Action<IContainerBuilder> install, GameObject gameObject)
        {
            install?.Invoke(builder);
            _gameObjectScopeBuilder.Build(gameObject, builder);
        }

        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( PrefabBuilder ))) return;
            builder.Register<PrefabBuilder>(Lifetime.Scoped);
            GameObjectScopeBuilder.TryInstall(builder);
        }
    }
#endif

    public static class GameObjectUtil
    {
#if BINDI_SUPPORT_VCONTAINER
        public static IScopedObjectResolver BuildGameObjectScope(this IObjectResolver scope, GameObject gameObject, Action<IContainerBuilder> installation = null)
        {
            if (! scope.TryResolve<GameObjectScopeBuilder>(out var gameObjectScopeBuilder))
            {
                throw new InvalidOperationException($"{nameof( GameObjectScopeBuilder )} is not registered in the current scope.");
            }
            return gameObjectScopeBuilder.Build(gameObject, installation);
        }
#endif
    }

    #endregion
}