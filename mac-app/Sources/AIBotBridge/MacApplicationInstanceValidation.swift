import Darwin
import Foundation

enum MacApplicationInstanceValidation {
    static func run() throws -> Int {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: folder) }
        var count = 0
        func check(_ label: String, _ value: Bool) throws {
            guard value else { throw MacTab5LocalError.invalid("Application instance: " + label) }
            count += 1
        }
        var first: MacApplicationInstance? = try MacApplicationInstance(directory: folder)
        let path = folder.appendingPathComponent("bridge-instance.lock")
        let before = try FileManager.default.attributesOfItem(atPath: path.path)
        try check("private empty lock", (before[.posixPermissions] as? NSNumber)?.intValue == 0o600 &&
                  (before[.size] as? NSNumber)?.intValue == 0)
        do {
            _ = try MacApplicationInstance(directory: folder)
            try check("second descriptor must fail", false)
        } catch MacApplicationInstance.Failure.alreadyRunning { count += 1 }
        withExtendedLifetime(first) {}
        first = nil
        try check("exit retains inode", FileManager.default.fileExists(atPath: path.path))
        let next = try MacApplicationInstance(directory: folder)
        let after = try FileManager.default.attributesOfItem(atPath: path.path)
        try check("released lock can reopen same inode", (before[.systemFileNumber] as? NSNumber) ==
                  (after[.systemFileNumber] as? NSNumber))
        withExtendedLifetime(next) {}
        let target = folder.appendingPathComponent("preserved.txt")
        try Data("fixture".utf8).write(to: target)
        let other = folder.appendingPathComponent("symlink", isDirectory: true)
        try FileManager.default.createDirectory(at: other, withIntermediateDirectories: true)
        try FileManager.default.createSymbolicLink(at: other.appendingPathComponent("bridge-instance.lock"), withDestinationURL: target)
        do {
            _ = try MacApplicationInstance(directory: other)
            try check("symlink must fail", false)
        } catch MacApplicationInstance.Failure.unavailable { count += 1 }
        try check("symlink target preserved", try Data(contentsOf: target) == Data("fixture".utf8))
        return count
    }
}
