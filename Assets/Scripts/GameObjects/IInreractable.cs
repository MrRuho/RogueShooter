using System;
using UnityEngine;

public interface IInteractable
{
   void Interact(Action onInteractComplete);
}
