// https://github.com/Chichiche/BinDI

// #define BINDI_R3_ENABLED
// #define BINDI_UNITASK_ENABLED
// #define BINDI_ADDRESSABLE_ENABLED

#if BINDI_R3_ENABLED
using R3;
#else
using UniRx;
#endif

#if BINDI_UNITASK_ENABLED
using Cysharp.Threading.Tasks;
#endif

#if BINDI_ADDRESSABLE_ENABLED
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace BinDI
{
    #region Usings
    
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using UnityObject = UnityEngine.Object;
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.IMGUI.Controls;
#endif
    using VContainer;
    using VContainer.Unity;
    
    #endregion
    
    #region Interfaces
    
    public interface IPublisher
    {
        IDisposable Subscribe(ISubscriber subscriber);
    }
    
    public interface IPublisher<out T>
    {
        IDisposable Subscribe(ISubscriber<T> subscriber);
    }
    
    public interface IBufferedPublisher<out T> : IPublisher<T>
    {
        bool HasValue { get; }
        T Value { get; }
    }
    
    public interface ISubscriber
    {
        void Publish();
    }
    
    public interface ISubscriber<in T>
    {
        void Publish(T value);
    }
    
#if BINDI_UNITASK_ENABLED
    public interface IAsyncPublisher
    {
        IDisposable Subscribe(IAsyncSubscriber asyncSubscriber);
    }
    
    public interface IAsyncPublisher<out T>
    {
        IDisposable Subscribe(IAsyncSubscriber<T> asyncSubscriber);
    }
    
    public interface IAsyncSubscriber
    {
        UniTask PublishAsync();
    }
    
    public interface IAsyncSubscriber<in T>
    {
        UniTask PublishAsync(T value);
    }
#endif
    
    public interface IRegistrationAttribute
    {
        Type ScopeType { get; }
        bool TryRegister(IContainerBuilder builder, Type concreteType);
    }
    
    public interface IConnectionAttribute
    {
        IDisposable TryConnect<T>(IObjectResolver scope, T publisherOrSubscriber, ConnectionService connectionService);
    }
    
    #endregion
    
    #region Extensions
    
    public static class SubscribeExtensions
    {
#if BINDI_R3_ENABLED
        public static IDisposable Subscribe<T>(this Observable<T> observable, ISubscriber subscriber)
        {
            return observable.Subscribe(subscriber, static (_, s) => s.Publish());
        }
        
        public static IDisposable Subscribe<T>(this Observable<T> observable, ISubscriber<T> subscriber)
        {
            return observable.Subscribe(subscriber, static (v, s) => s.Publish(v));
        }
        
        public static IDisposable SubscribeWithState<TValue, TState>(this Observable<TValue> observable, TState state, Action<TValue, TState> onNext)
        {
            return observable.Subscribe((state, onNext), static (value, t) => t.onNext(value, t.state));
        }
#else
        public static IDisposable Subscribe<T>(this IObservable<T> observable, ISubscriber subscriber)
        {
            return observable.SubscribeWithState(subscriber, static (_, s) => s.Publish());
        }
        
        public static IDisposable Subscribe<T>(this IObservable<T> observable, ISubscriber<T> subscriber)
        {
            return observable.SubscribeWithState(subscriber, static (v, s) => s.Publish(v));
        }
        
        public static IDisposable Subscribe<TValue, TState>(this IObservable<TValue> observable, TState state, Action<TValue, TState> onNext)
        {
            return observable.SubscribeWithState((state, onNext), static (value, t) => t.onNext(value, t.state));
        }
#endif
        
        public static IDisposable Subscribe(this IPublisher publisher, Action publish)
        {
            return publisher.Subscribe(new ActionSubscriber(publish));
        }
        
        public static IDisposable Subscribe<T>(this IPublisher<T> publisher, Action<T> publish)
        {
            return publisher.Subscribe(new ActionSubscriber<T>(publish));
        }
    }
    
    public static class BinDiInstaller
    {
        public static ContainerBuilder RegisterBinDi(this ContainerBuilder builder)
        {
            PrefabBuilder.TryInstall(builder);
            return builder;
        }
        
        public static IContainerBuilder RegisterBinDi(this IContainerBuilder builder)
        {
            PrefabBuilder.TryInstall(builder);
            return builder;
        }
    }
    
    #endregion
    
    #region Attributes
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RegisterToAttribute : Attribute, IRegistrationAttribute
    {
        public Type ScopeType { get; }
        readonly Lifetime _lifetime;
        
        public RegisterToAttribute(Type scopeType = null, Lifetime lifetime = Lifetime.Singleton)
        {
            ScopeType = scopeType ?? typeof( GlobalScope );
            _lifetime = lifetime;
        }
        
        public bool TryRegister(IContainerBuilder builder, Type concreteType)
        {
            if (IsGlobalScope && IsAlreadyRegistered(builder, concreteType)) return false;
            RegisterDomain(builder, concreteType);
            return true;
        }
        
        bool IsGlobalScope => ScopeType == typeof( GlobalScope );
        
        static bool IsAlreadyRegistered(IContainerBuilder builder, Type concreteType) => builder.Exists(concreteType, findParentScopes: true);
        
        void RegisterDomain(IContainerBuilder builder, Type concreteType)
        {
            builder.Register(concreteType, _lifetime).AsSelf().AsImplementedInterfaces();
        }
    }
    
#if BINDI_ADDRESSABLE_ENABLED
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RegisterAddressableAttribute : Attribute, IRegistrationAttribute
    {
        public Type ScopeType { get; }
        readonly string _address;
        
        public RegisterAddressableAttribute(Type scopeType = null, string address = null)
        {
            ScopeType = scopeType ?? typeof( GlobalScope );
            _address = address;
        }
        
        public bool TryRegister(IContainerBuilder builder, Type concreteType)
        {
            if (IsGlobalScope && IsAlreadyRegistered(builder, concreteType)) return false;
            var operation = LoadOperation(GetAddress(concreteType));
            OperationAddToScope(builder, operation);
            if (IsComponent(concreteType)) RegisterComponent(builder, concreteType, operation);
            else RegisterAsset(builder, operation);
            return true;
        }
        
        bool IsGlobalScope => ScopeType == typeof( GlobalScope );
        
        static bool IsAlreadyRegistered(IContainerBuilder builder, Type concreteType) => builder.Exists(concreteType, findParentScopes: true);
        
        static AsyncOperationHandle<UnityObject> LoadOperation(string address)
        {
            var operation = Addressables.LoadAssetAsync<UnityObject>(address);
            operation.WaitForCompletion();
            return operation;
        }
        
        string GetAddress(MemberInfo concreteType) => _address ?? concreteType.Name;
        
        static void OperationAddToScope(IContainerBuilder builder, AsyncOperationHandle<UnityObject> operation)
        {
            builder.RegisterDisposeCallback(_ => Addressables.Release(operation));
        }
        
        static bool IsComponent(Type concreteType)
        {
            return concreteType.IsSubclassOf(typeof( Component ));
        }
        
        static void RegisterComponent(IContainerBuilder builder, Type concreteType, AsyncOperationHandle<UnityObject> operation)
        {
            var gameObject = (GameObject)operation.Result;
            var component = gameObject.GetComponent(concreteType);
            builder.RegisterInstance(component).AsSelf().AsImplementedInterfaces();
        }
        
        static void RegisterAsset(IContainerBuilder builder, AsyncOperationHandle<UnityObject> operation)
        {
            builder.RegisterInstance(operation.Result).AsSelf().AsImplementedInterfaces();
        }
    }
#endif
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class PublishToAttribute : Attribute, IConnectionAttribute
    {
        readonly Type _subscriberType;
        
        public PublishToAttribute(Type subscriberType)
        {
            _subscriberType = subscriberType;
        }
        
        public IDisposable TryConnect<T>(IObjectResolver scope, T publisherOrSubscriber, ConnectionService connectionService)
        {
            if (! scope.TryResolve(_subscriberType, out var resolvedSubscriber)) return null;
            if (publisherOrSubscriber is IPublisher publisher && resolvedSubscriber is ISubscriber subscriber) return publisher.Subscribe(subscriber);
            if (connectionService.TryGetGenericArgument(_subscriberType, typeof( ISubscriber<> ), out var valueType)) return connectionService.ConnectValuePubSub(valueType, publisherOrSubscriber, resolvedSubscriber);
#if BINDI_UNITASK_ENABLED
            if (publisherOrSubscriber is IAsyncPublisher asyncPublisher && resolvedSubscriber is IAsyncSubscriber asyncSubscriber) return asyncPublisher.Subscribe(asyncSubscriber);
            if (connectionService.TryGetGenericArgument(_subscriberType, typeof( IAsyncSubscriber<> ), out var asyncValueType)) return connectionService.ConnectAsyncValuePubSub(asyncValueType, publisherOrSubscriber, resolvedSubscriber);
#endif
            return null;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class SubscribeFromAttribute : Attribute, IConnectionAttribute
    {
        readonly Type _publisherType;
        
        public SubscribeFromAttribute(Type publisherType)
        {
            _publisherType = publisherType;
        }
        
        public IDisposable TryConnect<T>(IObjectResolver scope, T publisherOrSubscriber, ConnectionService connectionService)
        {
            if (! scope.TryResolve(_publisherType, out var resolvedPublisher)) return null;
            if (resolvedPublisher is IPublisher publisher && publisherOrSubscriber is ISubscriber subscriber) return publisher.Subscribe(subscriber);
            if (connectionService.TryGetGenericArgument(_publisherType, typeof( IPublisher<> ), out var valueType)) return connectionService.ConnectValuePubSub(valueType, resolvedPublisher, publisherOrSubscriber);
#if BINDI_UNITASK_ENABLED
            if (resolvedPublisher is IAsyncPublisher asyncPublisher && publisherOrSubscriber is IAsyncSubscriber asyncSubscriber) return asyncPublisher.Subscribe(asyncSubscriber);
            if (connectionService.TryGetGenericArgument(_publisherType, typeof( IAsyncPublisher<> ), out var asyncValueType)) return connectionService.ConnectAsyncValuePubSub(asyncValueType, resolvedPublisher, publisherOrSubscriber);
#endif
            return null;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ScopeAttribute : Attribute { }
    
    #endregion
    
    #region BaseTypes
    
    public class Channel : IPublisher, ISubscriber, IDisposable
    {
#if BINDI_R3_ENABLED
        Observer<Unit> _observer;
        public Observable<Unit> AsObservable => _subject;
        public Observer<Unit> AsObserver => _observer ??= _subject.AsObserver();
#else
        public IObservable<Unit> AsObservable => _subject;
        public IObserver<Unit> AsObserver => _subject;
#endif
        readonly Subject<Unit> _subject = new();
        bool _disposed;
        
        public void Publish()
        {
            if (_disposed) return;
            _subject.OnNext(Unit.Default);
        }
        
        public IDisposable Subscribe(ISubscriber subscriber)
        {
            return ! _disposed
                ? SubscribeImpl()
                : Disposable.Empty;
            
            IDisposable SubscribeImpl()
            {
#if BINDI_R3_ENABLED
                return _subject.Subscribe(subscriber, static (_, p) => p.Publish());
#else
                return _subject.SubscribeWithState(subscriber, static (_, p) => p.Publish());
#endif
            }
        }
        
        public void Dispose()
        {
#if BINDI_R3_ENABLED
            _observer?.Dispose();
#endif
            _subject?.Dispose();
            _disposed = true;
        }
    }
    
    public class Channel<T> : IPublisher, IPublisher<T>, ISubscriber<T>, IDisposable
    {
#if BINDI_R3_ENABLED
        Observer<T> _observer;
        public Observable<T> AsObservable => _subject;
        public Observer<T> AsObserver => _observer ??= _subject.AsObserver();
#else
        public IObservable<T> AsObservable => _subject;
        public IObserver<T> AsObserver => _subject;
#endif
        readonly Subject<T> _subject = new();
        bool _disposed;
        
        public void Publish(T value)
        {
            if (_disposed) return;
            _subject.OnNext(value);
        }
        
        public IDisposable Subscribe(ISubscriber subscriber)
        {
            return ! _disposed
                ? SubscribeImpl()
                : Disposable.Empty;
            
            IDisposable SubscribeImpl()
            {
#if BINDI_R3_ENABLED
                return _subject.Subscribe(subscriber, static (_, p) => p.Publish());
#else
                return _subject.SubscribeWithState(subscriber, static (_, p) => p.Publish());
#endif
            }
        }
        
        public IDisposable Subscribe(ISubscriber<T> subscriber)
        {
            return ! _disposed
                ? SubscribeImpl()
                : Disposable.Empty;
            
            IDisposable SubscribeImpl()
            {
#if BINDI_R3_ENABLED
                return _subject.Subscribe(subscriber, static (value, p) => p.Publish(value));
#else
                return _subject.SubscribeWithState(subscriber, static (value, p) => p.Publish(value));
#endif
            }
        }
        
        public void Dispose()
        {
#if BINDI_R3_ENABLED
            _observer?.Dispose();
#endif
            _subject?.Dispose();
            _disposed = true;
        }
    }
    
#if BINDI_UNITASK_ENABLED
    public class AsyncChannel : IAsyncPublisher, IAsyncSubscriber, IDisposable
    {
        static readonly Stack<List<IAsyncSubscriber>> _subscriberListPool = new();
        readonly List<IAsyncSubscriber> _subscribers = RentAsyncSubscriberList();
        bool _disposed;
        
        static List<IAsyncSubscriber> RentAsyncSubscriberList()
        {
            return _subscriberListPool.TryPop(out var list)
                ? list
                : new List<IAsyncSubscriber>(16);
        }
        
        public UniTask PublishAsync()
        {
            return ! _disposed
                ? UniTask.WhenAll(_subscribers.Select(subscriber => subscriber.PublishAsync()))
                : UniTask.CompletedTask;
        }
        
        public IDisposable Subscribe(IAsyncSubscriber asyncSubscriber)
        {
            if (_disposed) return Disposable.Empty;
            _subscribers.Add(asyncSubscriber);
            return Disposable.Create(() => _subscribers.Remove(asyncSubscriber));
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscribers.Clear();
            _subscriberListPool.Push(_subscribers);
        }
    }
    
    public class AsyncChannel<T> : IAsyncSubscriber<T>, IAsyncPublisher<T>, IDisposable
    {
        readonly List<IAsyncSubscriber<T>> _subscribers = new();
        bool _disposed;
        
        public UniTask PublishAsync(T value)
        {
            return ! _disposed
                ? UniTask.WhenAll(_subscribers.Select(subscriber => subscriber.PublishAsync(value)))
                : UniTask.CompletedTask;
        }
        
        public IDisposable Subscribe(IAsyncSubscriber<T> asyncSubscriber)
        {
            if (_disposed) return Disposable.Empty;
            _subscribers.Add(asyncSubscriber);
            return Disposable.Create(() => _subscribers.Remove(asyncSubscriber));
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscribers.Clear();
        }
    }
#endif
    
    public class Property<T> : IPublisher, IBufferedPublisher<T>, ISubscriber<T>, IDisposable
    {
#if BINDI_R3_ENABLED
        Observer<T> _observer;
        public Observable<T> AsObservable => _property;
        public Observer<T> AsObserver => _observer ??= _property.AsObserver();
#else
        IObserver<T> _observer;
        public IObservable<T> AsObservable => _property;
        public IObserver<T> AsObserver => _observer ??= Observer.Create<T>(value => _property.Value = value);
#endif
        readonly ReactiveProperty<T> _property = new();
        bool _disposed;
        
        public bool HasValue { get; private set; }
        public T Value => _property.Value;
        
        public void Publish(T value)
        {
            if (_disposed) return;
            HasValue = true;
            _property.Value = value;
        }
        
        public IDisposable Subscribe(ISubscriber subscriber)
        {
            return ! _disposed
                ? SubscribeImpl()
                : Disposable.Empty;
            
            IDisposable SubscribeImpl()
            {
#if BINDI_R3_ENABLED
                return _property.Subscribe(subscriber, static (_, p) => p.Publish());
#else
                return _property.SubscribeWithState(subscriber, static (_, p) => p.Publish());
#endif
            }
        }
        
        public IDisposable Subscribe(ISubscriber<T> subscriber)
        {
            return ! _disposed
                ? SubscribeImpl()
                : Disposable.Empty;
            
            IDisposable SubscribeImpl()
            {
#if BINDI_R3_ENABLED
                return _property.Subscribe(subscriber, static (value, p) => p.Publish(value));
#else
                return _property.SubscribeWithState(subscriber, static (value, p) => p.Publish(value));
#endif
            }
        }
        
        public void Dispose()
        {
            _property?.Dispose();
            _disposed = true;
        }
    }
    
    public abstract class ReadOnlyProperty<T> : IPublisher, IBufferedPublisher<T>, IInitializable, IDisposable
    {
#if BINDI_R3_ENABLED
        public Observable<T> AsObservable => _property;
#else
        public IObservable<T> AsObservable => _property;
#endif
        readonly ReactiveProperty<T> _property = new();
        IDisposable _subscription;
        bool _disposed;
        
        public bool HasValue { get; private set; }
        public T Value => _property.Value;
        
        void IInitializable.Initialize()
        {
            _subscription = Setup(SetValue);
        }
        
        protected abstract IDisposable Setup(Action<T> setValue);
        
        void SetValue(T value)
        {
            if (_disposed) return;
            HasValue = true;
            _property.Value = value;
        }
        
        public IDisposable Subscribe(ISubscriber subscriber)
        {
            return ! _disposed
                ? SubscribeImpl()
                : Disposable.Empty;
            
            IDisposable SubscribeImpl()
            {
#if BINDI_R3_ENABLED
                return _property.Subscribe(subscriber, static (_, p) => p.Publish());
#else
                return _property.SubscribeWithState(subscriber, static (_, p) => p.Publish());
#endif
            }
        }
        
        public IDisposable Subscribe(ISubscriber<T> subscriber)
        {
            if (_disposed) return Disposable.Empty;
            if (HasValue) subscriber.Publish(Value);
            return SubscribeImpl();
            
            IDisposable SubscribeImpl()
            {
#if BINDI_R3_ENABLED
                return _property.Subscribe(subscriber, static (value, p) => p.Publish(value));
#else
                return _property.SubscribeWithState(subscriber, static (value, p) => p.Publish(value));
#endif
            }
        }
        
        void IDisposable.Dispose()
        {
            _property?.Dispose();
            _subscription?.Dispose();
            _disposed = true;
        }
    }
    
    #endregion
    
    #region DataTypes
    
    public sealed class GlobalScope { }
    
    public sealed class DomainRegistration
    {
        readonly Type _concreteType;
        readonly IRegistrationAttribute _registrationAttribute;
        
        public DomainRegistration(Type concreteType, IRegistrationAttribute registrationAttribute)
        {
            _concreteType = concreteType;
            _registrationAttribute = registrationAttribute;
        }
        
        public bool TryRegister(IContainerBuilder builder)
        {
            return _registrationAttribute.TryRegister(builder, _concreteType);
        }
    }
    
    public sealed class ActionSubscriber : ISubscriber
    {
        readonly Action _publish;
        
        public ActionSubscriber(Action publish)
        {
            _publish = publish;
        }
        
        public void Publish()
        {
            _publish();
        }
    }
    
    public sealed class ActionSubscriber<T> : ISubscriber<T>
    {
        readonly Action<T> _publish;
        
        public ActionSubscriber(Action<T> publish)
        {
            _publish = publish;
        }
        
        public void Publish(T value)
        {
            _publish(value);
        }
    }
    
    #endregion
    
    #region Managers
    
    public sealed class BinDiOptions
    {
        public bool CollectAssemblyLogEnabled = false;
        public bool DomainRegistrationLogEnabled = false;
        public bool PubSubConnectionLogEnabled = false;
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( BinDiOptions ), findParentScopes: true)) return;
            builder.Register<BinDiOptions>(Lifetime.Singleton);
        }
    }
    
    public sealed class WithoutAssemblies
    {
        readonly string[] _defaultWithoutAssemblyNames = new[]
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
        };
        
        readonly string[] _assemblyNames;
        
        public WithoutAssemblies(IEnumerable<string> customWithoutAssemblyNames)
        {
            _assemblyNames = _defaultWithoutAssemblyNames.Concat(customWithoutAssemblyNames).ToArray();
        }
        
        WithoutAssemblies()
        {
            _assemblyNames = _defaultWithoutAssemblyNames;
        }
        
        public bool Contains(string assemblyFullName)
        {
            return _assemblyNames.Any(assemblyFullName.StartsWith);
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( WithoutAssemblies ), findParentScopes: true)) return;
            builder.RegisterInstance(new WithoutAssemblies());
        }
    }
    
    public sealed class AppDomainProvider
    {
        readonly BinDiOptions _binDiOptions;
        readonly WithoutAssemblies _withoutAssemblies;
        readonly Type[] _concreteClasses;
        
        public AppDomainProvider(BinDiOptions binDiOptions, WithoutAssemblies withoutAssemblies)
        {
            _binDiOptions = binDiOptions;
            _withoutAssemblies = withoutAssemblies;
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
            if (_withoutAssemblies.Contains(assembly.FullName)) return Array.Empty<Type>();
            if (_binDiOptions.CollectAssemblyLogEnabled) Debug.Log($"BinDI collect assembly concrete types from {assembly.FullName}.");
            return assembly.DefinedTypes.Where(type => type.IsClass && ! type.IsAbstract);
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( AppDomainProvider ))) return;
            builder.Register<AppDomainProvider>(Lifetime.Singleton);
            BinDiOptions.TryInstall(builder);
            WithoutAssemblies.TryInstall(builder);
        }
    }
    
    public sealed class DomainRegistrationProvider
    {
        static readonly ReadOnlyCollection<DomainRegistration> EmptyRegistrations = new( Array.Empty<DomainRegistration>() );
        readonly Dictionary<Type, List<DomainRegistration>> _scopedRegistrationListSourceMap = new() { { typeof( GlobalScope ), new List<DomainRegistration>() } };
        readonly Dictionary<Type, ReadOnlyCollection<DomainRegistration>> _scopedRegistrationListMap;
        
        public DomainRegistrationProvider(AppDomainProvider appDomainProvider)
        {
            for (var i = 0; i < appDomainProvider.ConcreteClassCount; i++) TryCollectRegistrations(appDomainProvider.GetConcreteClass(i));
            _scopedRegistrationListMap = _scopedRegistrationListSourceMap.ToDictionary(kv => kv.Key, kv => new ReadOnlyCollection<DomainRegistration>(kv.Value));
        }
        
        public ReadOnlyCollection<DomainRegistration> GetRegistrations(Type scopeType)
        {
            return _scopedRegistrationListMap.GetValueOrDefault(scopeType, EmptyRegistrations);
        }
        
        void TryCollectRegistrations(Type concreteType)
        {
            foreach (var registrationAttribute in GetRegistrationAttributes(concreteType)) CollectRegistrations(concreteType, registrationAttribute);
        }
        
        static IEnumerable<IRegistrationAttribute> GetRegistrationAttributes(MemberInfo concreteType)
        {
            return concreteType.GetCustomAttributes().OfType<IRegistrationAttribute>();
        }
        
        void CollectRegistrations(Type concreteType, IRegistrationAttribute registrationAttribute)
        {
            var scopedRegistrationList = GetScopedRegistrationList(registrationAttribute.ScopeType);
            scopedRegistrationList.Add(new DomainRegistration(concreteType, registrationAttribute));
        }
        
        List<DomainRegistration> GetScopedRegistrationList(Type scopeType)
        {
            if (_scopedRegistrationListSourceMap.TryGetValue(scopeType, out var registrationList)) return registrationList;
            registrationList = new List<DomainRegistration>();
            _scopedRegistrationListSourceMap.Add(scopeType, registrationList);
            return registrationList;
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( DomainRegistrationProvider ), findParentScopes: true)) return;
            builder.Register<DomainRegistrationProvider>(Lifetime.Singleton);
            AppDomainProvider.TryInstall(builder);
        }
    }
    
    public sealed class RegistrationBinder
    {
        readonly List<MonoBehaviour> _getComponentsBuffer = new( 16 );
        readonly BinDiOptions _binDiOptions;
        readonly DomainRegistrationProvider _registrationProvider;
        readonly FieldInfo _concreteTypeField = typeof( DomainRegistration ).GetField("_concreteType");
        
        public RegistrationBinder(BinDiOptions binDiOptions, DomainRegistrationProvider registrationProvider)
        {
            _binDiOptions = binDiOptions;
            _registrationProvider = registrationProvider;
        }
        
        public void TryBind(IContainerBuilder builder)
        {
            EntryPointsBuilder.EnsureDispatcherRegistered(builder);
            RegisterGlobalScopeModules(builder, "Global");
        }
        
        public void TryBind<T>(IContainerBuilder builder, T scope)
        {
            if (scope == null) return;
            EntryPointsBuilder.EnsureDispatcherRegistered(builder);
            RegisterGlobalScopeModules(builder, _binDiOptions.DomainRegistrationLogEnabled ? scope.ToString() : default);
            TryRegisterScopedModules(builder, scope);
        }
        
        public void TryBind(IContainerBuilder builder, GameObject scopedGameObject)
        {
            if (scopedGameObject == null) return;
            EntryPointsBuilder.EnsureDispatcherRegistered(builder);
            RegisterGlobalScopeModules(builder, _binDiOptions.DomainRegistrationLogEnabled ? scopedGameObject.name : default);
            scopedGameObject.GetComponents(_getComponentsBuffer);
            for (var i = 0; i < _getComponentsBuffer.Count; i++) TryRegisterScopedModules(builder, _getComponentsBuffer[i]);
        }
        
        void RegisterGlobalScopeModules(IContainerBuilder builder, string scopeName)
        {
            var registrations = _registrationProvider.GetRegistrations(typeof( GlobalScope ));
            for (var i = 0; i < registrations.Count; i++) TryRegister(builder, registrations[i], scopeName);
        }
        
        void TryRegisterScopedModules<T>(IContainerBuilder builder, T scope)
        {
            var registrations = _registrationProvider.GetRegistrations(scope.GetType());
            for (var i = 0; i < registrations.Count; i++) TryRegister(builder, registrations[i], _binDiOptions.DomainRegistrationLogEnabled ? scope.ToString() : default);
        }
        
        void TryRegister(IContainerBuilder builder, DomainRegistration registration, string scopeName)
        {
            if (! registration.TryRegister(builder)) return;
            if (_binDiOptions.DomainRegistrationLogEnabled) Debug.Log($"{nameof( BinDI )} registered [{_concreteTypeField.GetValue(registration)}] to [{scopeName}].");
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( RegistrationBinder ), findParentScopes: true)) return;
            builder.Register<RegistrationBinder>(Lifetime.Singleton);
            BinDiOptions.TryInstall(builder);
            DomainRegistrationProvider.TryInstall(builder);
        }
    }
    
    public sealed class DomainConnectionProvider
    {
        readonly Dictionary<Type, List<PublishToAttribute>> _publishToListMap = new();
        readonly Dictionary<Type, List<SubscribeFromAttribute>> _subscribeFromListMap = new();
        
        public DomainConnectionProvider(AppDomainProvider appDomainProvider)
        {
            CollectDomainConnections(appDomainProvider);
        }
        
        public int GetPublishToConnectionCount(Type publisherOrSubscriberType)
        {
            return _publishToListMap.TryGetValue(publisherOrSubscriberType, out var attributes)
                ? attributes.Count
                : 0;
        }
        
        public PublishToAttribute GetPublishToConnection(Type subscriberType, int index)
        {
            return _publishToListMap[subscriberType][index];
        }
        
        public int GetSubscribeFromConnectionCount(Type publisherType)
        {
            return _subscribeFromListMap.TryGetValue(publisherType, out var attributes)
                ? attributes.Count
                : 0;
        }
        
        public SubscribeFromAttribute GetSubscribeFromConnection(Type publisherType, int index)
        {
            return _subscribeFromListMap[publisherType][index];
        }
        
        void CollectDomainConnections(AppDomainProvider appDomainProvider)
        {
            for (var i = 0; i < appDomainProvider.ConcreteClassCount; i++) CollectConnections(appDomainProvider.GetConcreteClass(i));
        }
        
        void CollectConnections(Type concreteClass)
        {
            foreach (var connectionAttribute in concreteClass.GetCustomAttributes().OfType<IConnectionAttribute>()) CollectConnection(concreteClass, connectionAttribute);
        }
        
        void CollectConnection(Type concreteClass, IConnectionAttribute connectionAttribute)
        {
            if (connectionAttribute is PublishToAttribute publishToAttribute) CollectPublishToConnection(concreteClass, publishToAttribute);
            if (connectionAttribute is SubscribeFromAttribute subscribeFromAttribute) CollectSubscribeFromConnection(concreteClass, subscribeFromAttribute);
        }
        
        void CollectPublishToConnection(Type concreteClass, PublishToAttribute publishToAttribute)
        {
            _publishToListMap.TryAdd(concreteClass, new List<PublishToAttribute>());
            _publishToListMap[concreteClass].Add(publishToAttribute);
        }
        
        void CollectSubscribeFromConnection(Type concreteClass, SubscribeFromAttribute subscribeFromAttribute)
        {
            _subscribeFromListMap.TryAdd(concreteClass, new List<SubscribeFromAttribute>());
            _subscribeFromListMap[concreteClass].Add(subscribeFromAttribute);
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( DomainConnectionProvider ), findParentScopes: true)) return;
            builder.Register<DomainConnectionProvider>(Lifetime.Singleton);
            AppDomainProvider.TryInstall(builder);
        }
    }
    
    public sealed class ConnectionService
    {
        readonly Type[] _genericParameterArguments = new Type[1];
        readonly object[] _subscribeArguments = new object[1];
        readonly BinDiOptions _binDiOptions;
        
        public ConnectionService(BinDiOptions binDiOptions)
        {
            _binDiOptions = binDiOptions;
        }
        
        public bool TryGetGenericArgument(Type scopeType, Type genericDefinition, out Type genericArgument)
        {
            foreach (var instanceInterface in scopeType.GetInterfaces())
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
        
        public IDisposable ConnectValuePubSub<TPublisher, TSubscriber>(Type valueType, TPublisher publisher, TSubscriber subscriber)
        {
            _genericParameterArguments[0] = valueType;
            var publisherType = typeof( IPublisher<> ).MakeGenericType(_genericParameterArguments);
            var subscribeMethod = publisherType.GetMethod("Subscribe");
            return subscribeMethod != null
                ? Connect(publisher, subscriber, subscribeMethod)
                : null;
        }
        
#if BINDI_UNITASK_ENABLED
        public IDisposable ConnectAsyncValuePubSub<TPublisher, TSubscriber>(Type valueType, TPublisher publisher, TSubscriber subscriber)
        {
            _genericParameterArguments[0] = valueType;
            var publisherType = typeof( IAsyncPublisher<> ).MakeGenericType(_genericParameterArguments);
            var subscribeMethod = publisherType.GetMethod("Subscribe");
            return subscribeMethod != null
                ? Connect(publisher, subscriber, subscribeMethod)
                : null;
        }
#endif
        
        IDisposable Connect<TPublisher, TSubscriber>(TPublisher publisher, TSubscriber subscriber, MethodInfo subscribeMethod)
        {
            _subscribeArguments[0] = subscriber;
            var connection = (IDisposable)subscribeMethod.Invoke(publisher, _subscribeArguments);
            if (_binDiOptions.PubSubConnectionLogEnabled) Debug.Log($"{nameof( BinDI )} connected [{publisher}] to [{subscriber}].");
            return connection;
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( ConnectionService ), findParentScopes: true)) return;
            builder.Register<ConnectionService>(Lifetime.Singleton);
            BinDiOptions.TryInstall(builder);
        }
    }
    
    public sealed class ConnectionBinder
    {
        readonly DomainConnectionProvider _domainConnectionProvider;
        readonly ConnectionService _connectionService;
        
        public ConnectionBinder(DomainConnectionProvider domainConnectionProvider, ConnectionService connectionService)
        {
            _domainConnectionProvider = domainConnectionProvider;
            _connectionService = connectionService;
        }
        
        public void TryBind<T>(IContainerBuilder builder, T publisherOrSubscriber)
        {
            builder.RegisterBuildCallback(parentScope => CreateConnectionScope(publisherOrSubscriber, parentScope));
        }
        
        void CreateConnectionScope<T>(T publisherOrSubscriber, IObjectResolver parentScope)
        {
            parentScope.CreateScope(connectionScopeBuilder => ConfigureConnectionScope(publisherOrSubscriber, parentScope, connectionScopeBuilder));
        }
        
        void ConfigureConnectionScope<T>(T publisherOrSubscriber, IObjectResolver parentScope, IContainerBuilder connectionScopeBuilder)
        {
            var publisherOrSubscriberType = publisherOrSubscriber.GetType();
            var connections = GetPublishToConnections(parentScope, publisherOrSubscriber, publisherOrSubscriberType)
                .Concat(GetSubscribeFromConnections(parentScope, publisherOrSubscriber, publisherOrSubscriberType))
                .Where(connection => connection != null)
                .ToArray();
            if (connections.Length <= 0) return;
            connectionScopeBuilder.RegisterDisposeCallback(_ => Disconnect(connections));
        }
        
        IEnumerable<IDisposable> GetPublishToConnections<T>(IObjectResolver parentScope, T publisherOrSubscriber, Type publisherOrSubscriberType)
        {
            var connectionCount = _domainConnectionProvider.GetPublishToConnectionCount(publisherOrSubscriberType);
            for (var i = 0; i < connectionCount; i++) yield return TryConnect(i);
            yield break;
            
            IDisposable TryConnect(int i)
            {
                return _domainConnectionProvider
                    .GetPublishToConnection(publisherOrSubscriberType, i)
                    .TryConnect(parentScope, publisherOrSubscriber, _connectionService);
            }
        }
        
        IEnumerable<IDisposable> GetSubscribeFromConnections<T>(IObjectResolver parentScope, T publisherOrSubscriber, Type publisherOrSubscriberType)
        {
            var connectionCount = _domainConnectionProvider.GetSubscribeFromConnectionCount(publisherOrSubscriberType);
            for (var i = 0; i < connectionCount; i++) yield return TryConnect(i);
            yield break;
            
            IDisposable TryConnect(int i)
            {
                return _domainConnectionProvider
                    .GetSubscribeFromConnection(publisherOrSubscriberType, i)
                    .TryConnect(parentScope, publisherOrSubscriber, _connectionService);
            }
        }
        
        static void Disconnect(IDisposable[] connections)
        {
            foreach (var connection in connections) connection.Dispose();
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( ConnectionBinder ), findParentScopes: true)) return;
            builder.Register<ConnectionBinder>(Lifetime.Singleton);
            DomainConnectionProvider.TryInstall(builder);
            ConnectionService.TryInstall(builder);
        }
    }
    
    public sealed class GameObjectScopeBuilder
    {
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
        
        public void TryBuild(GameObject gameObject)
        {
            if (gameObject == null) return;
            _scope.CreateScope(builder => TryBuild(builder, gameObject)).AddTo(gameObject);
        }
        
        public void TryBuild(IContainerBuilder builder, GameObject gameObject)
        {
            if (gameObject == null) return;
            gameObject.GetComponents(_getComponentsBuffer);
            for (var i = 0; i < _getComponentsBuffer.Count; i++) _registrationBinder.TryBind(builder, _getComponentsBuffer[i]);
            for (var i = 0; i < _getComponentsBuffer.Count; i++) _connectionBinder.TryBind(builder, _getComponentsBuffer[i]);
            var transform = gameObject.transform;
            for (var i = 0; i < transform.childCount; i++) TryBuildChild(builder, transform.GetChild(i));
        }
        
        void TryBuildChild(IContainerBuilder builder, Component child)
        {
            if (child == null) return;
            child.GetComponents(_getComponentsBuffer);
            var isScope = _getComponentsBuffer.Any(component => component.GetType().GetCustomAttribute<ScopeAttribute>() != null);
            if (isScope) BuildNewScope(builder, child.gameObject);
            else TryBuild(builder, child.gameObject);
        }
        
        void BuildNewScope(IContainerBuilder builder, GameObject gameObject)
        {
            builder.RegisterBuildCallback(CreateNewScope);
            return;
            void CreateNewScope(IObjectResolver scope) => scope.CreateScope(ConfigureNewScope);
            void ConfigureNewScope(IContainerBuilder newScopeBuilder) => TryBuild(newScopeBuilder, gameObject);
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( GameObjectScopeBuilder ))) return;
            builder.Register<GameObjectScopeBuilder>(Lifetime.Scoped);
            RegistrationBinder.TryInstall(builder);
            ConnectionBinder.TryInstall(builder);
        }
    }
    
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
            _scope.CreateScope(builder => ConfigureScope(builder, install, instance.gameObject)).AddTo(instance);
            return instance;
        }
        
        public GameObject Build(GameObject prefab, Transform parent = null, Action<IContainerBuilder> install = null)
        {
            if (prefab == null) return null;
            var instance = UnityObject.Instantiate(prefab, parent);
            _scope.CreateScope(builder => ConfigureScope(builder, install, instance)).AddTo(instance);
            return instance;
        }
        
        void ConfigureScope(IContainerBuilder builder, Action<IContainerBuilder> install, GameObject gameObject)
        {
            install?.Invoke(builder);
            _gameObjectScopeBuilder.TryBuild(builder, gameObject);
        }
        
        public static void TryInstall(IContainerBuilder builder)
        {
            if (builder.Exists(typeof( PrefabBuilder ))) return;
            builder.Register<PrefabBuilder>(Lifetime.Scoped);
            GameObjectScopeBuilder.TryInstall(builder);
        }
    }
    
    #endregion
    
    #region EditorWindow
    
#if UNITY_EDITOR
    
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
        readonly FieldInfo _scopedRegistrationListMapField = typeof( DomainRegistrationProvider ).GetField("_scopedRegistrationListMap", BindingFlags.Instance | BindingFlags.NonPublic);
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
            var domainRegistrationProvider = refreshScope.Resolve<DomainRegistrationProvider>();
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
    
    #endregion
    
    #region Lisence
    
/*
https://github.com/Chichiche/BinDI
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
    
    #endregion
}
