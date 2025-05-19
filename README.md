# Unity Component Utils

## Installation
1. Open the Unity package manage from `Window -> Package Manager`
2. Click `+` and select `Install package from git URL...`
3. Enter `https://github.com/NonpartisanChaos/ComponentUtils.git` and click `Install`

## Overview

### RequireComponentGetters
Create cached getters for each RequireComponent type on a MonoBehaviour. Each getter will lazily cache component references.

The MonoBehaviour class must be marked with `partial`.
```csharp
using ComponentUtils;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponentGetters]
public partial class MyBehaviour : MonoBehaviour
{
    // Generates:
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
public partial class MyBehaviour : MonoBehaviour
{
    // Generates:
    // private MeshFilter _meshFilter;
    // protected internal MeshFilter MeshFilter => _meshFilter ??= GetComponent<MeshFilter>();
}
```
