namespace PDiffy;

/// <summary>
/// Thrown when a snapshot check fails: the snapshot differs, its folder can't be found, or JSON can't be read. The
/// message says why.
/// </summary>
public sealed class SnapshotException(string message) : Exception(message);
