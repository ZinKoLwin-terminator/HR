/// A browser cannot stat a path, so callers must treat a null size as
/// "unknown" rather than as "missing".
const bool canInspectLocalFiles = false;

int? localFileSizeOrNull(String path) => null;
