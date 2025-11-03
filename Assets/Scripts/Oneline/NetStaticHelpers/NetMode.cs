public static class NetMode
{
    public static bool IsOnline        => Mirror.NetworkServer.active || Mirror.NetworkClient.active;
    public static bool IsServer        => Mirror.NetworkServer.active;             // host tai dedicated
    public static bool IsClient        => Mirror.NetworkClient.active;             // host + remote client
    public static bool IsHost          => Mirror.NetworkServer.active && Mirror.NetworkClient.active;
    public static bool IsRemoteClient  => Mirror.NetworkClient.active && !Mirror.NetworkServer.active;
    public static bool IsDedicatedServer => Mirror.NetworkServer.active && !Mirror.NetworkClient.active;
    public static bool ServerOrOff => Mirror.NetworkServer.active || !Mirror.NetworkClient.isConnected; // Server or offline.
    public static bool Offline => !Mirror.NetworkClient.active && !Mirror.NetworkServer.active; // offline

}
