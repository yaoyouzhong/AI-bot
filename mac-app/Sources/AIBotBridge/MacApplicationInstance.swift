import Darwin
import Foundation

/// All installed copies share one per-user lock before starting any bridge.
/// Keep the inode after exit: removing it could create two independently locked files.
final class MacApplicationInstance {
    enum Failure: Error { case alreadyRunning, unavailable }
    private let descriptor: Int32

    init(directory: URL = FileManager.default.homeDirectoryForCurrentUser
        .appendingPathComponent("Library/Application Support/AI-bot", isDirectory: true)) throws {
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true,
                                                    attributes: [.posixPermissions: 0o700])
        } catch { throw Failure.unavailable }
        let path = directory.appendingPathComponent("bridge-instance.lock").path
        let fd = Darwin.open(path, O_RDWR | O_CREAT | O_CLOEXEC | O_NOFOLLOW, mode_t(0o600))
        guard fd >= 0 else { throw Failure.unavailable }
        var info = stat()
        guard fstat(fd, &info) == 0, info.st_mode & S_IFMT == S_IFREG, info.st_uid == geteuid() else {
            Darwin.close(fd); throw Failure.unavailable
        }
        guard flock(fd, LOCK_EX | LOCK_NB) == 0 else {
            let busy = errno == EWOULDBLOCK
            Darwin.close(fd); throw busy ? Failure.alreadyRunning : Failure.unavailable
        }
        descriptor = fd
    }

    deinit { Darwin.close(descriptor) }
}
