/// Browsers surface a failed connection through Dio's own
/// `DioExceptionType.connectionError`, which the caller has already handled
/// by the time this is reached, so there is no platform error left to
/// recognise.
bool isHostUnreachableError(Object? error) => false;
