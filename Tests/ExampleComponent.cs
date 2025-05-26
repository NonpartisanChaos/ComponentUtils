using ComponentUtils;
using UnityEngine;
using UnityEngine.Tilemaps;


//single component requirement:
[RequireComponent(typeof(MeshFilter))]
//can support multiple types in a single RequireComponent:
[RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(MeshRenderer))]
//duplicates have no impact:
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
//this will be ignored because it has a custom name declared below:
[RequireComponent(typeof(Animator))]
//custom getter name:
[RequireComponentGetter(typeof(TilemapRenderer), "MyTilemapRenderer")]
//with named parameters:
[RequireComponentGetter(visibility: "private", name: "MyAudioSource", type: typeof(AudioSource))]
//with positional and named parameters (this will overwrite the default-named RequireComponent above):
[RequireComponentGetter(typeof(Animator), name: "MyAnimator")]
[RequireComponentGetters]
public partial class ExampleComponent : MonoBehaviour {
    public void ExampleMethod() {
        //verify that RequireComponentGetters fields are generated as expected
        _ = MeshFilter?.ToString();
        _ = Rigidbody?.ToString();
        _ = Collider?.ToString();
        _ = MeshRenderer?.ToString();

        //verify that RequireComponentGetter fields are generated as expected
        _ = MyTilemapRenderer?.ToString();
        _ = MyAudioSource?.ToString();
        _ = MyAnimator?.ToString();
    }
}
