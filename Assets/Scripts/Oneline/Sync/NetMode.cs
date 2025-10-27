public static class NetMode
{
    public static bool IsOnline        => Mirror.NetworkServer.active || Mirror.NetworkClient.active;
    public static bool IsServer        => Mirror.NetworkServer.active;             // host tai dedicated
    public static bool IsClient        => Mirror.NetworkClient.active;             // host + remote client
    public static bool IsHost          => Mirror.NetworkServer.active && Mirror.NetworkClient.active;
    public static bool IsRemoteClient  => Mirror.NetworkClient.active && !Mirror.NetworkServer.active;
    public static bool IsDedicated => Mirror.NetworkServer.active && !Mirror.NetworkClient.active;
    public static bool ServerOrOff => Mirror.NetworkServer.active || !Mirror.NetworkClient.isConnected; // Server or offline.

}
