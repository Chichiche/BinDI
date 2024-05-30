// https://github.com/Chichiche/BinDI

//#undef BINDI_SUPPORT_VCONTAINER

#undef BINDI_SUPPORT_R3
#undef BINDI_SUPPORT_UNIRX
#undef BINDI_SUPPORT_UNITASK
#undef BINDI_SUPPORT_ADDRESSABLE
#undef UNITY_EDITOR

#if BINDI_SUPPORT_VCONTAINER
using VContainer;
using VContainer.Unity;
#endif

#if BINDI_SUPPORT_ADDRESSABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
    
    #region Installation
    
    public static class BinDiInstaller
    {
#if BINDI_SUPPORT_VCONTAINER
        public static T RegisterBinDi<T>(this T builder, BinDiOptions options = null) where T : IContainerBuilder
        {
            if (options != null) builder.RegisterInstance(options);
            PrefabBuilder.TryInstall(builder);
            return builder;
        }
#endif
    }
    
    public sealed class BinDiOptions
    {
        public bool CollectAssemblyLogEnabled = false;
        public bool DomainRegistrationLogEnabled = false;
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
    
    public sealed class ActionDisposable : IDisposable
    {
        readonly Action _action;
        bool _disposed;
        
        public ActionDisposable(Action action)
        {
            _action = action;
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _action?.Invoke();
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
    
    public sealed class OnDestroyTrigger : MonoBehaviour
    {
        public Action OnDestroyHandler;
        
        void OnDestroy()
        {
            OnDestroyHandler?.Invoke();
        }
    }
    
    public static class DisposableExtensions
    {
        public static T AddTo<T>(this T disposable, IScopedDisposable scopedDisposable) where T : IDisposable
        {
            scopedDisposable.Add(disposable);
            return disposable;
        }
        
#if BINDI_SUPPORT_VCONTAINER
        public static IScopedObjectResolver CreateDisposableLinkedChildScope(this IObjectResolver scope, Action<IContainerBuilder> installation = null)
        {
            var childScope = scope.CreateScope(installation);
            childScope.AddTo(scope);
            return childScope;
        }
#endif
        
#if BINDI_SUPPORT_VCONTAINER
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
    }
    
    #endregion Disposables
    
    #region EditorWindow
    
#if UNITY_EDITOR && BINDI_SUPPORT_VCONTAINER
    public sealed class BinDiWindow : EditorWindow
    {
        [SerializeField] BinDiHeader _header;
        [SerializeField] BinDiTree _tree;
        
        [MenuItem("Window/" + nameof( BinDI ))]
        public static void ShowWindow()
        {
            var window = GetWindow<BinDiWindow>();
            window.Show();
        }
        
        void OnEnable()
        {
            _header ??= new BinDiHeader();
            _header.Refresh();
            _tree ??= new BinDiTree();
            _tree.Refresh(_header);
        }
        
        void OnGUI()
        {
            _tree?.View.OnGUI(new Rect(0, 0, position.width, position.height));
        }
    }
    
    [Serializable]
    public sealed class BinDiHeader
    {
        readonly MultiColumnHeaderState.Column[] _columns =
        {
            new()
            {
                headerContent = new GUIContent("Type"),
                width = 100,
                canSort = false,
            },
            new()
            {
                headerContent = new GUIContent("Name"),
                width = 300,
                canSort = false,
            },
            new()
            {
                headerContent = new GUIContent("Script"),
                width = 300,
                canSort = false,
            },
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
    public sealed class BinDiTree
    {
        [SerializeField] TreeViewState _treeViewState;
        public BinDiTreeView View;
        
        public void Refresh(BinDiHeader header)
        {
            _treeViewState ??= new TreeViewState();
            View ??= new BinDiTreeView(_treeViewState, header.View);
            View.Refresh();
            View.Reload();
            View.ExpandAll();
        }
    }
    
    public sealed class BinDiTreeView : TreeView
    {
        readonly FieldInfo _scopedRegistrationListMapField = typeof( RegistrationProvider ).GetField("_scopedRegistrationListMap", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly FieldInfo _concreteTypeField = typeof( DomainRegistration ).GetField("_concreteType", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly FieldInfo _subscriberTypeField = typeof( PublishToAttribute ).GetField("_subscriberType", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly FieldInfo _publisherTypeField = typeof( SubscribeFromAttribute ).GetField("_publisherType", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly Dictionary<Type, List<Registration>> _scopedRegistrationsMap = new();
        
        public BinDiTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader)
        {
            columnIndexForTreeFoldouts = 1;
            showAlternatingRowBackgrounds = true;
        }
        
        public void Refresh()
        {
            var builder = new ContainerBuilder();
            builder.RegisterBinDi();
            using var refreshScope = builder.Build();
            var domainRegistrationProvider = refreshScope.Resolve<RegistrationProvider>();
            var domainConnectionProvider = refreshScope.Resolve<DomainConnectionProvider>();
            var scopedRegistrationListMap = (Dictionary<Type, ReadOnlyCollection<DomainRegistration>>)_scopedRegistrationListMapField.GetValue(domainRegistrationProvider);
            _scopedRegistrationsMap.Clear();
            foreach (var scopeType in scopedRegistrationListMap.Keys)
            {
                _scopedRegistrationsMap.TryAdd(scopeType, new List<Registration>());
                foreach (var domainRegistration in scopedRegistrationListMap[scopeType])
                {
                    var registrationType = (Type)_concreteTypeField.GetValue(domainRegistration);
                    var publisherTypes = Enumerable
                        .Range(0, domainConnectionProvider.GetSubscribeFromConnectionCount(registrationType))
                        .Select(i => domainConnectionProvider.GetSubscribeFromConnection(registrationType, i))
                        .Select(attribute => (Type)_publisherTypeField.GetValue(attribute));
                    var subscriberTypes = Enumerable
                        .Range(0, domainConnectionProvider.GetPublishToConnectionCount(registrationType))
                        .Select(i => domainConnectionProvider.GetPublishToConnection(registrationType, i))
                        .Select(attribute => (Type)_subscriberTypeField.GetValue(attribute));
                    _scopedRegistrationsMap[scopeType].Add(new Registration(registrationType.Name, publisherTypes, subscriberTypes));
                }
            }
        }
        
        protected override TreeViewItem BuildRoot()
        {
            var root = new BinDiTreeViewItem { id = 0, depth = -1 };
            var id = 1;
            
            foreach (var (scope, registrations) in _scopedRegistrationsMap)
            {
                var scopeItem = new BinDiTreeViewItem { id = id++, displayName = scope.Name, ItemType = ItemType.Scope, Script = FindScript(scope.Name) };
                root.AddChild(scopeItem);
                foreach (var registration in registrations)
                {
                    var registrationScript = registration.TypeInfo.Script;
                    var registrationItem = new BinDiTreeViewItem { id = id++, displayName = registration.TypeInfo.TypeName, ItemType = ItemType.Registration, Script = registrationScript, };
                    scopeItem.AddChild(registrationItem);
                    
                    if (registration.Connection.Publishers.Length > 0)
                    {
                        foreach (var publisherInfo in registration.Connection.Publishers)
                        {
                            var publisherScript = publisherInfo.Script;
                            var publisherItem = new BinDiTreeViewItem { id = id++, displayName = publisherInfo.TypeName, ItemType = ItemType.SubscribeFrom, Script = publisherScript };
                            registrationItem.AddChild(publisherItem);
                        }
                    }
                    
                    if (registration.Connection.Subscribers.Length > 0)
                    {
                        foreach (var subscriberInfo in registration.Connection.Subscribers)
                        {
                            var subscriberScript = subscriberInfo.Script;
                            var subscriberItem = new BinDiTreeViewItem { id = id++, displayName = subscriberInfo.TypeName, ItemType = ItemType.PublishTo, Script = subscriberScript };
                            registrationItem.AddChild(subscriberItem);
                        }
                    }
                }
            }
            
            SetupDepthsFromParentsAndChildren(root);
            return root;
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
                        GUI.Label(cellRect, item.ItemType.ToString());
                        break;
                    case 1:
                        args.rowRect = cellRect;
                        base.RowGUI(args);
                        break;
                    case 2:
                        if (item.Script) EditorGUI.ObjectField(cellRect, item.Script, typeof( MonoScript ), false);
                        break;
                }
            }
        }
        
        enum ItemType
        {
            Scope,
            Registration,
            SubscribeFrom,
            PublishTo,
        }
        
        sealed class BinDiTreeViewItem : TreeViewItem
        {
            public ItemType ItemType;
            public MonoScript Script;
        }
        
        sealed class Registration
        {
            public readonly TypeInfo TypeInfo;
            public readonly Connection Connection;
            
            public Registration(string registrationTypeName, IEnumerable<Type> publisherTypes, IEnumerable<Type> subscriberTypes)
            {
                TypeInfo = new TypeInfo(registrationTypeName);
                Connection = new Connection(publisherTypes, subscriberTypes);
            }
        }
        
        sealed class TypeInfo
        {
            public readonly string TypeName;
            public readonly MonoScript Script;
            
            public TypeInfo(string typeName)
            {
                TypeName = typeName;
                Script = FindScript(typeName);
            }
        }
        
        sealed class Connection
        {
            public readonly TypeInfo[] Publishers;
            public readonly TypeInfo[] Subscribers;
            
            public Connection(IEnumerable<Type> publisherTypes, IEnumerable<Type> subscriberTypes)
            {
                Publishers = publisherTypes.Select(type => new TypeInfo(type.Name)).ToArray();
                Subscribers = subscriberTypes.Select(type => new TypeInfo(type.Name)).ToArray();
            }
        }
        
        static MonoScript FindScript(string scriptName)
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
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RegisterToAttribute : Attribute
    {
#if BINDI_SUPPORT_VCONTAINER
        public object Scope { get; }
        public Lifetime Lifetime { get; }
        
        public RegisterToAttribute(object scope, Lifetime lifetime = Lifetime.Singleton)
        {
            Scope = scope;
            Lifetime = lifetime;
        }
#endif
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RegisterToGlobalAttribute : Attribute
    {
#if BINDI_SUPPORT_VCONTAINER
        public object Scope => GlobalScope.Default;
        public Lifetime Lifetime { get; }
        
        public RegisterToGlobalAttribute(Lifetime lifetime = Lifetime.Singleton)
        {
            Lifetime = lifetime;
        }
#endif
    }
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RegisterAddressableToAttribute : Attribute
    {
#if BINDI_SUPPORT_VCONTAINER && BINDI_SUPPORT_ADDRESSABLE
        public object Scope { get; }
        public Lifetime Lifetime => Lifetime.Singleton;
        public readonly string Address;
        
        public RegisterAddressableToAttribute(object scope, string address = null)
        {
            Scope = scope;
            Address = address;
        }
#endif
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RegisterAddressableToGlobalAttribute : Attribute
    {
#if BINDI_SUPPORT_VCONTAINER && BINDI_SUPPORT_ADDRESSABLE
        public object Scope => GlobalScope.Default;
        public Lifetime Lifetime => Lifetime.Singleton;
        public readonly string Address;
        
        public RegisterAddressableToGlobalAttribute(string address = null)
        {
            Address = address;
        }
#endif
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ScopedComponentAttribute : Attribute { }
    
    #endregion Registration Attributes
    
    #region Registration Modules
    
    public sealed class WithoutRegistrationAssemblies
    {
        static readonly string[] _defaultWithoutAssemblyNames =
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
        
        public WithoutRegistrationAssemblies(IEnumerable<string> customWithoutAssemblyNames)
        {
            _assemblyNames = _defaultWithoutAssemblyNames.Concat(customWithoutAssemblyNames).ToArray();
        }
        
        WithoutRegistrationAssemblies()
        {
            _assemblyNames = _defaultWithoutAssemblyNames;
        }
        
        public bool Contains(string assemblyFullName)
        {
            return _assemblyNames.Any(assemblyFullName.StartsWith);
        }
        
#if BINDI_SUPPORT_VCONTAINER
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( WithoutRegistrationAssemblies ), findParentScopes: true)) return;
            builder.RegisterInstance(new WithoutRegistrationAssemblies());
        }
#endif
    }
    
    public sealed class AppDomainProvider
    {
        readonly BinDiOptions _binDiOptions;
        readonly WithoutRegistrationAssemblies _withoutRegistrationAssemblies;
        readonly Type[] _concreteClasses;
        
        public AppDomainProvider(BinDiOptions binDiOptions, WithoutRegistrationAssemblies withoutRegistrationAssemblies)
        {
            _binDiOptions = binDiOptions;
            _withoutRegistrationAssemblies = withoutRegistrationAssemblies;
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
            if (_withoutRegistrationAssemblies.Contains(assembly.FullName)) yield break;
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
            WithoutRegistrationAssemblies.TryInstall(builder);
        }
#endif
    }
    
    public sealed class RegistrationProvider
    {
#if BINDI_SUPPORT_VCONTAINER
        static readonly ReadOnlyCollection<IRegistration> EmptyRegistrations = new( Array.Empty<IRegistration>() );
        readonly Dictionary<object, List<IRegistration>> _scopedRegistrationListSourceMap = new() { { GlobalScope.Default, new List<IRegistration>() } };
        readonly Dictionary<object, ReadOnlyCollection<IRegistration>> _scopedRegistrationListMap;
        
        public RegistrationProvider(AppDomainProvider appDomainProvider)
        {
            for (var i = 0; i < appDomainProvider.ConcreteClassCount; i++) CollectRegistrations(appDomainProvider.GetConcreteClass(i));
            _scopedRegistrationListMap = _scopedRegistrationListSourceMap.ToDictionary(kv => kv.Key, kv => new ReadOnlyCollection<IRegistration>(kv.Value));
        }
        
        public ReadOnlyCollection<IRegistration> GetRegistrations<T>(T scope)
        {
            return _scopedRegistrationListMap.GetValueOrDefault(scope, EmptyRegistrations);
        }
        
        void CollectRegistrations(Type concreteType)
        {
            foreach (var attribute in concreteType.GetCustomAttributes())
            {
                CollectRegistration(concreteType, attribute);
            }
        }
        
        void CollectRegistration(Type concreteType, Attribute attribute)
        {
            switch (attribute)
            {
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
#endif
    }
    
    public sealed class GlobalScope
    {
        public static readonly GlobalScope Default = new();
    }
    
    public interface IRegistration
    {
#if BINDI_SUPPORT_VCONTAINER
        bool TryRegister(IContainerBuilder builder);
#endif
    }
    
    public sealed class DomainRegistration : IRegistration
    {
#if BINDI_SUPPORT_VCONTAINER
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
#endif
    }
    
    public sealed class AddressableRegistration : IRegistration
    {
#if BINDI_SUPPORT_VCONTAINER && BINDI_SUPPORT_ADDRESSABLE
        readonly Type _type;
        readonly string _address;
        
        public AddressableRegistration(Type type, string address)
        {
            _type = type;
            _address = address;
        }
        
        public bool TryRegister(IContainerBuilder builder)
        {
            if (builder.Exists(_type)) return false;
            var actualAddress = GetAddress(_type, _address);
            var operation = LoadAddressable(actualAddress);
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
            var operation = Addressables.LoadAssetAsync<UnityObject>(address);
            operation.WaitForCompletion();
            return operation;
        }
        
        static Action<IObjectResolver> UnloadCallback(AsyncOperationHandle<UnityObject> operation)
        {
            return _ =>
            {
                Debug.Log("◆終了時に呼ばれるか確認！");
                Addressables.Release(operation);
            };
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
#else
        public bool TryRegister(IContainerBuilder builder)
        {
            return false;
        }
#endif
    }
    
    public sealed class RegistrationBinder
    {
#if BINDI_SUPPORT_VCONTAINER
        readonly BinDiOptions _binDiOptions;
        readonly RegistrationProvider _registrationProvider;
        readonly FieldInfo _concreteTypeField = typeof( DomainRegistration ).GetField("_concreteType", BindingFlags.NonPublic | BindingFlags.Instance);
        
        public RegistrationBinder(BinDiOptions binDiOptions, RegistrationProvider registrationProvider)
        {
            _binDiOptions = binDiOptions;
            _registrationProvider = registrationProvider;
        }
        
        public void Bind(IContainerBuilder builder)
        {
            RegisterGlobalScopeModules(builder, "Global");
        }
        
        public void Bind<T>(IContainerBuilder builder, T scope)
        {
            if (scope == null) return;
            RegisterGlobalScopeModules(builder, _binDiOptions.DomainRegistrationLogEnabled ? scope.ToString() : default);
            TryRegisterScopedModules(builder, scope);
        }
        
        void RegisterGlobalScopeModules(IContainerBuilder builder, string scopeName)
        {
            var registrations = _registrationProvider.GetRegistrations(GlobalScope.Default);
            for (var i = 0; i < registrations.Count; i++) TryRegister(builder, registrations[i], scopeName);
        }
        
        void TryRegisterScopedModules<T>(IContainerBuilder builder, T scope)
        {
            var registrations = _registrationProvider.GetRegistrations(scope);
            for (var i = 0; i < registrations.Count; i++) TryRegister(builder, registrations[i], _binDiOptions.DomainRegistrationLogEnabled ? scope.ToString() : default);
        }
        
        void TryRegister(IContainerBuilder builder, IRegistration registration, string scopeName)
        {
            if (! registration.TryRegister(builder)) return;
            if (_binDiOptions.DomainRegistrationLogEnabled) Debug.Log($"{nameof( BinDI )} registered [{_concreteTypeField.GetValue(registration)}] to [{scopeName}].");
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( RegistrationBinder ), findParentScopes: true)) return;
            builder.Register<RegistrationBinder>(Lifetime.Singleton);
            BinDiOptions.TryInstall(builder);
            RegistrationProvider.TryInstall(builder);
        }
#endif
    }
    
    public static class RegistrationUtil
    {
#if BINDI_SUPPORT_VCONTAINER
        public static IScopedObjectResolver CreateBinDiScope(this IObjectResolver scope, params object[] targetScopes)
        {
            if (! scope.TryResolve<RegistrationBinder>(out var registrationBinder))
            {
                throw new InvalidOperationException($"{nameof( RegistrationBinder )} is not registered in the current scope.");
            }
            return scope.CreateScope(builder =>
            {
                registrationBinder.Bind(builder, GlobalScope.Default);
                foreach (var targetScope in targetScopes)
                {
                    registrationBinder.Bind(builder, targetScope);
                }
            });
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
    
    public interface IAsyncPublishable
    {
#if BINDI_SUPPORT_UNITASK
        UniTask PublishAsync();
#endif
    }
    
    public interface IAsyncPublishable<in T>
    {
#if BINDI_SUPPORT_UNITASK
        UniTask PublishAsync(T value);
#endif
    }
    
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
    
    public interface IAsyncSubscribable
    {
        IDisposable Subscribe(IAsyncPublishable asyncPublishable);
    }
    
    public interface IAsyncSubscribable<out T>
    {
        IDisposable Subscribe(IAsyncPublishable<T> asyncPublishable);
    }
    
    #endregion Subscribable Interfaces
    
    #region Connection Attributes
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class PublishToAttribute : Attribute
    {
        public Type PublishableType { get; }
        
        public PublishToAttribute(Type publishableType)
        {
            PublishableType = publishableType;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class SubscribeFromAttribute : Attribute
    {
        public Type SubscribableType { get; }
        
        public SubscribeFromAttribute(Type subscribableType)
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
    
        public Observable<Unit> AsObservable => _subject;
        public Observer<Unit> AsObserver => _observer ??= _subject.AsObserver();
    
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
    
        public IObservable<Unit> AsObservable => _subject;
        public IObserver<Unit> AsObserver => _subject;
    
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
    
        public Observable<T> AsObservable => _subject;
        public Observer<T> AsObserver => _observer ??= _subject.AsObserver();
    
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
    
        public IObservable<T> AsObservable => _subject;
        public IObserver<T> AsObserver => _subject;
    
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
        
#if BINDI_SUPPORT_UNITASK
        public UniTask PublishAsync()
        {
            return ! _disposed
                ? UniTask.WhenAll(_subscribers.Select(subscriber => subscriber.PublishAsync()))
                : UniTask.CompletedTask;
        }
#endif
        
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
    
    
    public class AsyncBroker<T> : IAsyncPublishable<T>, IAsyncSubscribable<T>, IDisposable
    {
        readonly List<IAsyncPublishable<T>> _subscribers = new();
        bool _disposed;
        
#if BINDI_SUPPORT_UNITASK
        public UniTask PublishAsync(T value)
        {
            return ! _disposed
                ? UniTask.WhenAll(_subscribers.Select(subscriber => subscriber.PublishAsync(value)))
                : UniTask.CompletedTask;
        }
#endif
        
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
    
    #endregion Brokers
    
    #region Properties
    
    public class Property<T> : ISubscribable, IBufferedSubscribable<T>, IPublishable<T>, IDisposable
    {
#if BINDI_SUPPORT_R3
        readonly ReactiveProperty<T> _property = new();
        Observer<T> _observer;
        bool _disposed;
    
        public Observable<T> AsObservable => _property;
        public Observer<T> AsObserver => _observer ??= _property.AsObserver();
    
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
    
        public IObservable<T> AsObservable => _property;
        public IObserver<T> AsObserver => _observer ??= Observer.Create<T>(value => _property.Value = value);
    
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
    
        public Observable<T> AsObservable => _property;
    
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
    
        public IObservable<T> AsObservable => _property;
    
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
    
    #region Managers
    
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
                case PublishToAttribute publishToAttribute:
                    CollectPublishToConnection(concreteClass, publishToAttribute);
                    break;
                case SubscribeFromAttribute subscribeFromAttribute:
                    CollectSubscribeFromConnection(concreteClass, subscribeFromAttribute);
                    break;
            }
        }
        
        void CollectPublishToConnection(Type concreteClass, PublishToAttribute publishToAttribute)
        {
            if (! _subscribableTypesSourceMap.ContainsKey(publishToAttribute.PublishableType))
            {
                _subscribableTypesSourceMap.Add(publishToAttribute.PublishableType, new List<Type>());
            }
            _subscribableTypesSourceMap[publishToAttribute.PublishableType].Add(concreteClass);
        }
        
        void CollectSubscribeFromConnection(Type concreteClass, SubscribeFromAttribute subscribeFromAttribute)
        {
            if (! _publishableTypesSourceMap.ContainsKey(subscribeFromAttribute.SubscribableType))
            {
                _publishableTypesSourceMap.Add(subscribeFromAttribute.SubscribableType, new List<Type>());
            }
            _publishableTypesSourceMap[subscribeFromAttribute.SubscribableType].Add(concreteClass);
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
                if (! scope.TryResolve<IScopedDisposable>(out var scopedDisposable))
                {
                    throw new InvalidOperationException($"{nameof( IScopedDisposable )} is not registered in the current scope.");
                }
                for (var i = 0; i < publishableTypes.Count; i++)
                {
                    if (! scope.TryResolve(publishableTypes[i], out var publishable)) continue;
                    Connect(instance, publishable).AddTo(scopedDisposable);
                }
                for (var i = 0; i < subscribableTypes.Count; i++)
                {
                    if (! scope.TryResolve(subscribableTypes[i], out var subscribable)) continue;
                    Connect(subscribable, instance).AddTo(scopedDisposable);
                }
            });
        }
        
        IDisposable Connect(object subscribable, object publishable)
        {
            if (subscribable is ISubscribable voidSubscribable && publishable is IPublishable voidPublishable) return voidSubscribable.Subscribe(voidPublishable);
            if (TryGetGenericArgument(subscribable.GetType(), typeof( IPublishable<> ), out var valueType)) return ConnectValuePubSub(valueType, subscribable, publishable);
#if BINDI_SUPPORT_UNITASK
            if (subscribable is ISubscribable asyncPublisher && publishable is IAsyncPublishable asyncSubscriber) return asyncPublisher.Subscribe(asyncSubscriber);
            if (TryGetGenericArgument(subscribable.GetType(), typeof( IAsyncPublishable<> ), out var asyncValueType)) return ConnectAsyncValuePubSub(asyncValueType, subscribable, publishable);
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
            ConnectionProvider.TryInstall(builder);
        }
    }
    
    #endregion
    
    #region GameObjectModules
    
    public sealed class GameObjectScopeBuilder
    {
#if BINDI_SUPPORT_VCONTAINER
        readonly List<MonoBehaviour> _getComponentsBuffer = new( 16 );
        readonly RegistrationBinder _registrationBinder;
        readonly ConnectionBinder _connectionBinder;
        readonly IObjectResolver _scope;
        
        public GameObjectScopeBuilder(RegistrationBinder registrationBinder, ConnectionBinder connectionBinder, IObjectResolver scope)
        {
            _registrationBinder = registrationBinder;
            _connectionBinder = connectionBinder;
            _scope = scope;
        }
        
        public IScopedObjectResolver Build(GameObject gameObject)
        {
            if (gameObject == null) return default;
            var scope = _scope.CreateScope(builder => Build(gameObject, builder));
            if (gameObject.TryGetComponent<OnDestroyTrigger>(out var onDestroyTrigger)) onDestroyTrigger.OnDestroyHandler = scope.Dispose;
            else gameObject.AddComponent<OnDestroyTrigger>().OnDestroyHandler = scope.Dispose;
            return scope;
        }
        
        public void Build(GameObject gameObject, IContainerBuilder builder)
        {
            if (! gameObject) return;
            builder.RegisterBuildCallback(scope => scope.InjectGameObject(gameObject));
            gameObject.GetComponents(_getComponentsBuffer);
            BindComponents(builder);
            var transform = gameObject.transform;
            for (var i = 0; i < transform.childCount; i++) TryBuildChild(builder, transform.GetChild(i));
        }
        
        void TryBuildChild(IContainerBuilder builder, Transform child)
        {
            if (! child) return;
            child.GetComponents(_getComponentsBuffer);
            var isScope = _getComponentsBuffer.Any(component => component.GetType().GetCustomAttribute<ScopedComponentAttribute>() != null);
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
                var componentType = component.GetType();
                if (! builder.Exists(componentType)) builder.RegisterInstance(_getComponentsBuffer[i]);
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
#endif
    }
    
    public sealed class PrefabBuilder
    {
#if BINDI_SUPPORT_VCONTAINER
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
#endif
    }
    
    public static class GameObjectUtil
    {
#if BINDI_SUPPORT_VCONTAINER
        public static IScopedObjectResolver BuildGameObjectScope(this IObjectResolver scope, GameObject gameObject)
        {
            if (! scope.TryResolve<GameObjectScopeBuilder>(out var gameObjectScopeBuilder))
            {
                throw new InvalidOperationException($"{nameof( GameObjectScopeBuilder )} is not registered in the current scope.");
            }
            return gameObjectScopeBuilder.Build(gameObject);
        }
#endif
    }
    
    #endregion
}
