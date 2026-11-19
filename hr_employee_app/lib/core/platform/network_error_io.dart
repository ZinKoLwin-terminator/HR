import 'dart:io';

/// True when [error] is the platform's "host unreachable" error.
bool isHostUnreachableError(Object? error) => error is SocketException;
