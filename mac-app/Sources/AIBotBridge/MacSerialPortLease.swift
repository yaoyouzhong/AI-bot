import Foundation

// Both device scanners must own a port before opening it or sending a probe.
enum MacSerialPortLease {
    private static let lock = NSLock()
    private static var paths = Set<String>()

    static func acquire(_ path: String) -> Bool {
        lock.lock(); defer { lock.unlock() }
        return paths.insert(path).inserted
    }

    static func release(_ path: String) {
        lock.lock(); defer { lock.unlock() }
        paths.remove(path)
    }
}
