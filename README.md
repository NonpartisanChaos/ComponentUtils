# Unity Component Utils

## Installation
1. Open the Unity package manage from `Window -> Package Manager`
2. Click `+` and select `Install package from git URL...`
3. Enter `https://github.com/NonpartisanChaos/ComponentUtils.git` and click `Install`

## Overview

### RequireComponentGetter
Apply to any `MonoBehaviour` class. Equivalent to `[RequireComponent(...)]` but with a cached getter that uses the specified name.

The MonoBehaviour class must be marked with `partial`.
```csharp
using ComponentUtils;
using UnityEngine;

[RequireComponentGetter(typeof(Rigidbody), "MyRigidbody")]
public partial class MyBehaviour : MonoBehaviour {
    public void SomeMethod() {
        MyRigidbody.centerOfMass = ...;
    }
    
    // Equivalent to:
    // private Rigidbody _myRigidbody;
    // public Rigidbody MyRigidbody => _myRigidbody ??= GetComponent<Rigidbody>();
}
```

### RequireComponentGetters
Create cached getters for each RequireComponent type on a MonoBehaviour. Each getter will lazily cache component references.
The getter names will exactly match the type of the field.

The MonoBehaviour class must be marked with `partial`.
```csharp
using ComponentUtils;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponentGetters]
public partial class MyBehaviour : MonoBehaviour {
    public void SomeMethod() {
        Rigidbody.centerOfMass = ...;
    }
    
    // Equivalent to:
    // private Rigidbody _rigidbody;
    // public Rigidbody Rigidbody => _rigidbody ??= GetComponent<Rigidbody>();
}
```
Getter visibility can also be specified (as a literal string):
```csharp
using ComponentUtils;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponentGetters("protected internal")]
public partial class MyBehaviour : MonoBehaviour {
    // Generates:
    // private MeshFilter _meshFilter;
    // protected internal MeshFilter MeshFilter => _meshFilter ??= GetComponent<MeshFilter>();
}
```
