using Mirror;
using UnityEngine;
/// <summary>
/// Only used for cleaning the field from units.
/// </summary>
[DisallowMultipleComponent]
public class FriendlyUnit : NetworkBehaviour {}

[DisallowMultipleComponent]
public class EnemyUnit : NetworkBehaviour {}
