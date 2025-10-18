public static class NetMode
{
    public static bool IsOnline       => Mirror.NetworkServer.active || Mirror.NetworkClient.active;
    public static bool IsServer       => Mirror.NetworkServer.active;              // host & dedicated
    public static bool IsRemoteClient => Mirror.NetworkClient.active && !Mirror.NetworkServer.active;
}
