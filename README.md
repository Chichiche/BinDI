# BinDI (Bindy)
Provides automatic binding of dependency registration and pub/sub connection for Unity.

## Overview
The primary feature of BinDI is automatic dependency registration.  
By adding scope information to the domain, you can omit the implementation of installers.  
This eliminates the need to write low cohesive and tightly coupled installers, thereby improving maintainability.

Additionally, BinDI offers a secondary feature of automatic Pub/Sub event connections.  
By adding view information to the domain, you can omit the implementation of presenters.  
This facilitates a component-oriented design with single responsibility, enhancing the reusability of components.

These features work by reading the information added via attributes,  
so the domain implementation does not depend on the scope or view.

## Dependencies
- [UnityEngine](https://unity.com/)
- (Optional) [VContainer](https://github.com/hadashiA/VContainer) for `domain registration`
- (Optional) [AddressableSystem](https://docs.unity3d.com/Packages/com.unity.addressables@1.21) for `asset registration`
- (Optional) [R3](https://github.com/Cysharp/R3) or [UniRx](https://github.com/neuecc/UniRx) for `pub/sub`
- (Optional) [UniTask](https://github.com/Cysharp/UniTask) for `async pub/sub`

## Installation

### Using the Package Manager
1. Open Package Manager: `Window/Package Manager`
2. Click the `+` button and select `Add package from git URL`
3. Enter the git URL: `https://github.com/Chichiche/BinDI.git`

### Using Manifest.json
1. Open `Packages/manifest.json`
2. Add the git URL to the dependencies block.
```json
{
  "dependencies": {
    "com.chichiche.bindi": "https://github.com/Chichiche/BinDI.git"
  }
}
```

## Basic Dependency Registration Setup
- Required: [VContainer](https://github.com/hadashiA/VContainer)

### 1. Register BinDI to ContainerBuilder
```csharp
var builder = new ContainerBuilder();
builder.RegisterBinDi();
```

### 2. Register used module
```csharp
[RegisterTo("scope")]
public class UsedModule
{
    public readonly int Value = 10;    
}
```

### 3. Register using module
```csharp
[RegisterTo("scope")]
public class UsingModule : IInitializable
{ 
    [Inject] UsedModule _usedModule;
    
    public void Initialize()
    {
        Debug.Log($"UsedModuleValue is {_usedModule.Value}.");
    }
}
```

### 4. Build Scope
```csharp
var scope = builder.Build();
scope.CreateBinDiScope("scope");
```

### 5. Results
The following output will be displayed in the log:
```plaintext
UsedModuleValue is 10.
```

## Basic Pub/Sub Connection Setup
- Required: [VContainer](https://github.com/hadashiA/VContainer)
- Optional: [R3](https://github.com/Cysharp/R3) or [UniRx](https://github.com/neuecc/UniRx)

A simple implementation is provided by default,  
but if `R3` or `UniRx` is installed, they will be used instead,  
allowing for improved performance and the use of powerful operators.

### Output to View

#### 1. Register Source Property
```csharp
[RegisterToGlobal]
public class CountProperty : Property<int> { }
```

#### 2. Register Output View
```csharp
[PublishFrom(typeof(CountProperty))]
public class CountView : MonoBehaviour, IPublishable<int>
{
    [SerializeField] Text _text;

    public void Publish(int value)
    {
        _text.text = value.ToString();
    }
}
```

### Input from View

#### 1. Register Operation Handler
```csharp
[RegisterToGlobal]
public class IncrementOperation : IPublishable
{
    [Inject] CountProperty _countProperty;

    public void Publish()
    {
        _countProperty.Publish(_countProperty.Value + 1);
    }
}
```

#### 2. Register Input View
```csharp
[SubscribeTo(typeof(IncrementOperation))]
public class IncrementButton : MonoBehaviour, ISubscribable
{
    [SerializeField] Button _button;
    
    public IDisposable Subscribe(IPublisher publisher)
    {
        return _button.OnClickAsObservable().Subscribe(_ => publisher.Publish());
    }
}
```

## Checking in the Editor Window
- Required: [VContainer](https://github.com/hadashiA/VContainer)
- Optional: [AddressableSystem](https://docs.unity3d.com/Packages/com.unity.addressables@1.21)

You can open the BinDI window from the `Window/BinDI` menu.

![BinDIWindow](https://github.com/Chichiche/BinDI/assets/165566396/c1459f23-9b65-4852-893d-e8ac94b6f85b)

## Feature Modules

The following sections are currently being written and are presented in the author's native language, Japanese.  
If you are using machine translation, please set the source language to `Japanese`.

## Dependency registration modules

### `[RegisterToGlobal]`
- Required: [VContainer](https://github.com/hadashiA/VContainer)

クラスに `[RegisterToGlobal]` 属性を付与することで、グローバルスコープに登録されます。  
グローバルスコープとは、 `RegisterBinDi` メソッドによって `BinDI` がインストールされたスコープを指します。
```csharp
// Register
[RegisterToGlobal]
public class Module { }
```
```csharp
// Resolve
var builder = new ContainerBuilder();
builder.RegisterBinDi();
var scope = builder.Build();
var module = builder.Resolve<Module>();
```

### `[RegisterTo(Scope, Lifetime)]`
- Required: [VContainer](https://github.com/hadashiA/VContainer)

コンパイル時の定数をスコープに指定することができます。
```csharp
// Register Int Scope
[RegisterTo(1)]
public class Module { }
```
```csharp
// Register String Scope
[RegisterTo("Scope")]
public class Module { }
```
```csharp
enum Sopce
{
    Scope1,
    Scope2,
}

// Register Enum Scope
[RegisterTo(Sopce.Scope1)]
public class DomainModule { }
```
第二引数で生存期間を指定することができます。  
生存期間を指定しなかった場合のデフォルト値は `Lifetime.Singleton` です。
```csharp
// Register Singleton (default)
[RegisterTo(scope, Lifetime.Singleton)]
public class Module { }
```
```csharp
// Register Scoped
[RegisterTo(scope, Lifetime.Scoped)]
public class Module { }
```
```csharp
// Register Transient
[RegisterTo(scope, Lifetime.Transient)]
public class Module { }
```
１つのクラスを複数のスコープに登録することもできます。
```csharp
// Multiple Registration
[RegisterToGlobal]
[RegisterTo(1)]
[RegisterTo("ScopeName")]
[RegisterTo(typeof(ScopedComponent))]
[RegisterTo(scope, Lifetime.Scoped)]
public class DomainModule { }
```
登録されたスコープは、 `CreateBinDiScope` メソッドでビルドすることができます。
```csharp
// Build Root Scope
var builder = new ContainerBuilder();
builder.RegisterBinDi();
var rootScope = builder.Build();

// Build Scope
var intScope = rootScope.CreateBinDiScope(1);
var stringScope = rootScope.CreateBinDiScope("Scope");
var enumScope = rootScope.CreateBinDiScope(Scope.Scope1);
```
コンポーネントの型をスコープにすることで、様々な機能を利用することができます。  
コンポーネントの型をスコープにした場合のスコープのビルド方法については、`GameObjectScopeBuilder` および `PrefabBuilder` の項目で詳しく説明します。  
ここでは、コンポーネントの型をスコープにする際の基本形である、 `M-V-P` パターンの定義方法のみを記載します。
```csharp
// Model
[RegisterToGlobal]
public class SharedModel
{
    public readonly int Value = 10;
}

[RegisterTo(typeof(View))]
public class LocalModel
{
    public readonly int Value = 20;
}

// View
public class View : MonoBehaviour
{
    [SerializeField] Text _text;
    
    public void SetText(string text)
    {
        _text.text = text;
    }
}

// Presenter
[RegisterTo(typeof(View))]
public class Presenter : IInitializable
{
    [Inject] readonly SharedModel _sharedModel;
    [Inject] readonly LocalModel _localModel;
    [Inject] readonly View _view;
    
    public void Initialize()
    {
        _view.SetText( (_sharedModel.Value + _localModel.Value).ToString());
    }
}
```

### `[InstallToGlobal]` `[InstallTo(Scope)]` `IInstallable`
- Required: [VContainer](https://github.com/hadashiA/VContainer)

`[InstallToGlobal]` 属性もしくは `[InstallTo(Scope)]` 属性を付与し、 `IInstallable` インターフェースを実装することで、インストーラーを定義することができます。    
基本的には `[RegisterToGlobal]` もしくは `[RegisterTo(Scope)]` を用いた登録の自動化を推奨していますが、複雑な登録処理を疎結合に行う際にはインストーラーを利用することができます。  
`IInstallable` インターフェースに定義された `TryInstall` メソッドは、登録処理が行われた場合に `true` を返すようにしてください。
```csharp
// Register Installer
[InstallToGlobal] or [InstallTo(Scope)]
public class ModuleInstaller : IInstallable
{
    [Inject] AnyCondition _condition;
    
    public bool TryInstall(IContainerBuilder builder)
    {
        if(! _condition.CanInstall) return false;
        builder.Register<Module>(LifeTime.Singleton);
        return true;
    }
}
```

### `[RegisterAddressableToGlobal(Address)]` `[RegisterAddressableTo(Scope, Address)]`
- Required: [VContainer](https://github.com/hadashiA/VContainer)
- Required [AddressableSystem](https://docs.unity3d.com/Packages/com.unity.addressables@1.21)

`[RegisterAddressableToGlobal]` 属性もしくは `[RegisterAddressableTo(Scope)]` 属性を付与することで、 `AddressableSystem` を通して `Prefab` と `ScriptableObject` のアセットを登録することができます。  
`Address` を省略した場合はクラス名をアドレスとみなしてアセットが読み込まれます。  
スコープが破棄された際はアセットの参照カウンタが１つ減少します。
```csharp
// Register Prefab Asset
[RegisterAddressableToGlobal]
public class PrefabAsset : MonoBehaviour { }
```
```csharp
// Register Prefab Asset with Address
[RegisterAddressableToGlobal("Address")]
public class PrefabAsset : MonoBehaviour { }
```
```csharp
// Register Prefab Asset to Scope
[RegisterAddressableTo(Scope)]
public class PrefabAsset : MonoBehaviour { }
```
```csharp
// Register Prefab Asset to Scope with Address
[RegisterAddressableTo(Scope, "Address")]
public class PrefabAsset : MonoBehaviour { }
```
```csharp
// Register ScriptableObject Asset
[RegisterAddressableToGlobal]
public class ScriptableObjectAsset : ScriptableObject { }
```
```csharp
// Register ScriptableObject Asset with Address
[RegisterAddressableToGlobal("Address")]
public class ScriptableObjectAsset : ScriptableObject { }
```
```csharp
// Register ScriptableObject Asset to Scope
[RegisterAddressableTo(Scope)]
public class ScriptableObjectAsset : ScriptableObject { }
```
```csharp
// Register ScriptableObject Asset to Scope with Address
[RegisterAddressableTo(Scope, "Address")]
public class ScriptableObjectAsset : ScriptableObject { }
```

# Scope building modules
### `GameObjectScopeBuilder` `[ScopedComponent]`
- Required: [VContainer](https://github.com/hadashiA/VContainer)

`GameObjectScopeBuilder` は直接使用することは少ないモジュールです。  
基本的に他モジュールが内部的に使用するか、拡張メソッド `BuildGameObjectScope(gameObject)` 経由で使用されます。
```csharp
// Build BinDI Scope
var builder = new ContainerBuilder();
builder.RegisterBinDi();
var scope = builder.Build();

// Build GameObject Scope
scope.BuildGameObjectScope(gameObject);
```
すでにインスタンス化されたGameObjectに対して `BuildGameObjectScope(gameObject)` を実行すると以下の特性を持つスコープが生成されます。
- 子階層を含むすべてのコンポーネントに `RegisterTo` されたモジュールがスコープに登録されます。基本的にスコープは分割されず、１つにまとめられます。
- 子階層を含むすべてのコンポーネントに、スコープに登録されたモジュールが注入されます。
- 子階層に `[ScopedComponent]` がアタッチされたコンポーネントが存在した場合、そのコンポーネント以下は別スコープに分割されます。  
  これは、コンポーネント毎に異なるドメインのインスタンスを注入したい場合に役立ちます。実例については最下部のサンプルプロジェクトの項目を参照ください。

### `PrefabBuilder`
- Required: [VContainer](https://github.com/hadashiA/VContainer)

`PrefabBuilder.Build(Prefab, Transform, Installation)` を通してGameObjectをインスタンス化すると、内部的に `GameObjectScopeBuilder` が使用され、生成されたインスタンスのスコープが生成され、必要な依存が注入されます。  
`Build` メソッドの第２引数には親トラスフォームを指定することができます。省略した場合はシーンルートに生成されます。  
`Build` メソッドの第３引数にはスコープを生成する際のインストールデリゲートを指定することができ、ビューのスコープに動的な値を注入する際に役立ちます。
```csharp
// Using PrefabBuilder Example
[RegisterToGlobal]
public class ViewSpawner
{
    [Inject] readonly PrefabBuilder _prefabBuilder;
    [Inject] readonly View _viewPrefab;
    [Inject] readonly ViewParentTransformProperty _viewParentTransformProperty;
    
    public View Spawn(ViewId id)
    {
        return _prefabBuilder.Build(_viewPrefab, _viewParentTransformProperty.Value, builder =>
        {
            builder.RegisterInstance(id);
        });
    }
}
```

## Pub/Sub connection modules

### `IPublishable` `IPublishable<T>`
他から配信 `される` モジュールに実装させるインターフェースです。  
ジェネリック型のないものは `void Publish()` メソッドを実装します。  
ジェネリック型のあるものは `void Publish(T value)` メソッドを実装します。  
オブザーバーパターンにおける `Observer` に相当し、本来の `Pub/Sub` パターンにおいては `Subscriber` に相当し、[MessagePipe](https://github.com/Cysharp/MessagePipe) においては `IPublisher` に相当します。  
命名については少し直感的ではないかもしれませんが、現在は以下の理由でこの名前を採用しています。
- メソッド `Publish` を持つインターフェースに `Publish` の名前を含めることで混乱を避けたい (`Subscriber` が `Publish` メソッドを持つと混乱する可能性がある)
- [MessagePipe](https://github.com/Cysharp/MessagePipe)を導入している場合に命名の衝突を避けるため、 `IPublisher` は避けたい
- 利用者から見て、 `Publish` メソッドを呼び出すことができるという意図で、 `IPublishable` としました。
- 後述する `ISubscribable` も同様の理由で命名しています。
```csharp
// Void Publishable
public class Publishable : IPublishable
{
    public void Publish()
    {
        Debug.Log("Published");
    }
}
```
```csharp
// Value Publishable
public class Publishable : IPublishable<int>
{
    public void Publish(int value)
    {
        Debug.Log($"Published: {value}");
    }
}
```

### `ISubscribable` `ISubscribable<T>`
他から `購読可能な` モジュールに実装させるインターフェースです。  
ジェネリック型のないものは `IDisposable Subscribe(IPublishable publishable)` メソッドを実装します。  
ジェネリック型のないものは `IDisposable Subscribe(IPublishable<T> publishable)` メソッドを実装します。  
オブザーバーパターンにおける `Observable` に相当し、本来の `Pub/Sub` パターンにおいては `Publisher` に相当し、[MessagePipe](https://github.com/Cysharp/MessagePipe) においては `ISubscriber` に相当します。
```csharp
// Void Subscribable
public class Subscribable : ISubscribable
{    
    readonly Subject<Unit> _subject = new();
    
    public IDisposable Subscribe(IPublishable publishable)
    {
        return _subject.Subscribe(_ => publishable.Publish());
    }
}
```
```csharp
// Value Subscribable
public class Subscribable : ISubscribable<int>
{    
    readonly Subject<int> _subject = new();
    
    public IDisposable Subscribe(IPublishable<int> publishable)
    {
        return _subject.Subscribe(publishable.Publish);
    }
}
```

### `[PublishFrom(SubscribableType)]`
- Required: [VContainer](https://github.com/hadashiA/VContainer)

`IPublishable` もしくは `IPublishable<T>` を実装したクラスに `[PublishFrom(SubscribableType)]` 属性を付与することで、  
自身が登録されたスコープに登録された `Subscribable` が自動的に接続されます。  
制約として、 `Publishable` `Subscribable` の少なくとも片方が `Component` である必要があります。  
これは、ドメイン同士の接続であれば依存性注入によって詳細な制御が簡単に行えるためです。  
`[PublishFrom(SubscribableType)]` 属性はドメイン、コンポーネントのどちらに付与しても動作しますが、プロジェクトによって統一することを推奨します。
```csharp
// Void Publish From View
[RegisterToGlobal]
[PublishFrom(typeof(Button))]
public class Publishable : IPublishable
{
    public void Publish()
    {
        Debug.Log("Published");
    }
}
```
```csharp
// Value Publish From View
[RegisterToGlobal]
[PublishFrom(typeof(Slider))]
public class Publishable : IPublishable<int>
{
    public void Publish(int value)
    {
        Debug.Log($"Published: {value}");
    }
}
```
```csharp
// Void Publish From Domain
[PublishFrom(typeof(DestroyEvent))]
public class Publishable : MonoBehaviour, IPublishable
{
    public void Publish()
    {
        Destroy(gameObject);
    }
}
```
```csharp
// Value Publish From Domain
[PublishFrom(typeof(ScoreProperty))]
public class Publishable : MonoBehaviour, IPublishable<int>
{
    [SerializeField] Text _text;
    
    public void Publish(int scopre)
    {
        _text.text = score.ToString();
    }
}
```

### `[SubscribeTo(PublishableType)]`
- Required: [VContainer](https://github.com/hadashiA/VContainer)

`ISubscribable` もしくは `ISubscribable<T>` を実装したクラスに `[SubscribeTo(PublishableType)]` 属性を付与することで、  
自身が登録されたスコープに登録された `Publishable` が自動的に接続されます。  
制約として、 `Publishable` `Subscribable` の少なくとも片方が `Component` である必要があります。  
これは、ドメイン同士の接続であれば依存性注入によって詳細な制御が簡単に行えるためです。  
`[SubscribeTo(PublishableType)]` 属性はドメイン、コンポーネントのどちらに付与しても動作しますが、プロジェクトによって統一することを推奨します。
```csharp
// Void Subscribe To View
[RegisterToGlobal]
[SubscribeTo(typeof(View))]
public class Subscribable : ISubscribable
{
    readonly Subject<Unit> _subject = new();
    
    public IDisposable Subscribe(IPublishable publishable)
    {
        return _subject.Subscribe(_ => publishable.Publish());
    }
}
```
```csharp
// Value Subscribe To View
[RegisterToGlobal]
[SubscribeTo(typeof(View))]
public class Subscribable : ISubscribable<int>
{
    readonly Subject<int> _subject = new();
    
    public IDisposable Subscribe(IPublishable<int> publishable)
    {
        return _subject.Subscribe(publishable.Publish);
    }
}
```
```csharp
// Void Subscribe To Domain
[RegisterToGlobal]
[SubscribeTo(typeof(Operation))]
public class Subscribable : MonoBehaviour, ISubscribable
{
    [SerializeField] Button _button;
    
    public IDisposable Subscribe(IPublishable publishable)
    {
        return _button.OnClickAsObservable().Subscribe(_ => publishable.Publish());
    }
}
```
```csharp
// Value Subscribe To Domain
[RegisterToGlobal]
[SubscribeTo(typeof(SelectedLevel))]
public class Subscribable : MonoBehaviour, ISubscribable<int>
{
    [SerializeField] Slider _slider;
    
    public IDisposable Subscribe(IPublishable publishable)
    {
        return _slider.OnValueChangedAsObservable().Subscribe(value => publishable.Publish((int)value));
    }
}
```

### `Property<T>` `ReadOnlyProperty<T>`
- Optional: [R3](https://github.com/Cysharp/R3) or [UniRx](https://github.com/neuecc/UniRx)

リアクティブプログラミングにおける `ReactiveProperty` に相当します。  
`IPublishable<T>` と `ISubscribable<T>` を実装しているため、 `[PublishFrom]` `[SubscribeTo]` の対象にすることができます。  
`AsObservable()` メソッドも提供されているため、 [R3](https://github.com/Cysharp/R3) や [UniRx](https://github.com/neuecc/UniRx) を用いた `Select` や `Where` などのオペレーターも使用することができます。  
主に継承して利用することを想定しています。
```csharp
// Inherit Property
[RegisterToGlobal]
public class ScoreProperty : Property<int> { }
```
```csharp
// Inherit ReadOnlyProperty
public class CompositeProperty : ReadOnlyProperty<int>
{
    public CompositeProperty(ReadOnlyProperty<int> propertyA, ReadOnlyProperty<int> propertyB)
    {
        propertyA.AsObservable().CombineLatest(propertyB.AsObservable(), (a, b) => a + b).Subscribe(Publish);
    }
}
```

### `Broker` `Broker<T>`
- Optional: [R3](https://github.com/Cysharp/R3) or [UniRx](https://github.com/neuecc/UniRx)

リアクティブプログラミングにおける `Subject` に相当します。  
主な特徴としては `IPublishable` と `ISubscribable` を実装しているという点ですが、  
[R3](https://github.com/Cysharp/R3) や [UniRx](https://github.com/neuecc/UniRx) の `Subject` との違いとして、 `sealed` されておらず、継承できる点、`void` 版も用意されているという点があります。  
主に継承するためのクラスですが、設計として `Broker` の継承が本当に必要かは考慮する必要があります。  
`IPublishable` もしくは `ISubscribable` の片方の実装で表現できないかどうか、 `Broker` を介さずに直接接続できないかを常に検討してください。
```csharp
// Void Broker
[RegsitertoGlobal]
[PublishFrom(typeof(Button))]
[SubscribeTo(typeof(Operation))]
public void VoidBroker : Broker { }
```
```csharp
// Value Broker
[RegsitertoGlobal]
[PublishFrom(typeof(Slider))]
[SubscribeTo(typeof(SelectedLevel))]
public void IntBroker : Broker<int> { }
```

### `IAsyncPublishable` `IAsyncPublishable<T>` `IAsyncSubscribable` `IAsyncSubscribable<T>` `AsyncBroker` `AsyncBroker<T>`
- Required: [UniTask](https://github.com/Cysharp/UniTask)

`Publishable` `Subscribable` の非同期版です。  
`PublishAsync()` が呼び出された場合、 `SubscribeAsync()` で登録されたすべての `Func<UniTask>` が並列で実行され、すべて完了するまで待機します。  
以下のコードでは `SubscribeAsync` で登録されたすべての `Task` が完了する `30` フレーム経過後に `Publish End` のログが表示されます。
```csharp
// Create Broker
var asyncBroker = new AsyncBroker();

// Subscribe
asyncBroker.SubscribeAsync(async ()=> 
{
    await UniTask.DelayFrame(10);
    Debug.Log("10 frames have elapsed.");
});

asyncBroker.SubscribeAsync(async ()=> 
{
    await UniTask.DelayFrame(20);
    Debug.Log("20 frames have elapsed.");
});

asyncBroker.SubscribeAsync(async ()=> 
{
    await UniTask.DelayFrame(30);
    Debug.Log("30 frames have elapsed.");
});

// Publish
Debug.Log("Publish Start");
await asyncBroker.PublishAsync();
Debug.Log("Publish End");
```

## Setup option modules

### `BinDiOptions`
`RegisterBinDi` メソッドによって `BinDI` をインストールする際、引数に `BinDiOptions` のインスタンスを渡すことができます。  
`BinDiOptions` は主に `BinDI` のデバッグ機能を有効にするために使用されます。  
`BinDiOptions` が未指定もしくは `null` の場合、デバッグ機能は無効化されます。
```csharp
var builder = new ContainerBuilder();
var binDiOptions = new BinDiOptions()
{
    CollectAssemblyLogEnabled = true,
    DomainRegistrationLogEnabled = true,
    PubSubConnectionLogEnabled = true,
};
builder.RegisterBinDi(binDiOptions);
```

### `IAssemblyFilter` `AssemblyWhiteListFilter` `AssemblyBlackListFilter`
`RegisterBinDi` メソッドによって `BinDI` をインストールする際、引数に `IAssemblyFilter` のインスタンスを渡すことができます。  
`IAssemblyFilter` には `bool CanCollect(string assemblyFullName)` が実装されており、 `BinDI` がスキャンするアセンブリを制御するために使用されます。  
`AssemblyFilter` が未指定もしくは `null` の場合、デフォルトの `AssemblyBlackListFilter` が使用されます。これには `UnityEngine` のアセンブリや `BinDI` とその依存モジュールのアセンブリをスキャンから除外する設定が含まれます。  
明示的に `AssemblyFilter` を指定する場合、ほとんどの場合は `AssemblyWhiteListFilter` のインスタンスを渡すと効率的です。このクラスはスキャンの対象にするアセンブリを指定するために使用されます。
```csharp
var builder = new ContainerBuilder();
var assemblyWhiteListFilter = new AssemblyWhiteListFilter("Assembly-CSharp");
builder.RegisterBinDi(options: default, assemblyWhiteListFilter);
```

## Sample Project
Repository is [here](https://github.com/Chichiche/BinDISample)
