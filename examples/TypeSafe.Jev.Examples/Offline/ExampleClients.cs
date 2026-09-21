namespace TypeSafe.Jev.Examples;

/// <summary>
/// Builds the clients the examples use. Online this is exactly <c>new JevClient(options)</c>;
/// offline it plugs in <see cref="FakeJevHandler"/> so the same example code runs without a key.
/// </summary>
internal static class ExampleClients
{
    /// <summary>Set once at startup by <c>Program</c>.</summary>
    public static bool Offline { get; set; }

    public static JevClient Create(JevClientOptions? options = null)
    {
        options ??= new JevClientOptions();
        if (!Offline) return new JevClient(options);

        options.ApiKey = "offline";
        return new JevClient(new HttpClient(new FakeJevHandler()), options);
    }
}
