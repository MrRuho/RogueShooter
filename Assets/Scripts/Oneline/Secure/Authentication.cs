using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// This class is responsible for handling the authentication process.
/// It initializes the Unity Services and signs in the user anonymously.
/// Required when using Unity Relay, as it provides player authentication 
/// and enables online multiplayer without port forwarding or direct IP connections.
/// </summary>
public class Authentication : MonoBehaviour
{ 
    public async Task SingInPlayerToUnityServerAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Logged into Unity, player ID: " + AuthenticationService.Instance.PlayerId);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    public void SignOutPlayerFromUnityServer()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut();
            Debug.Log("Player signed out of Unity Services");
        }
    }
}
