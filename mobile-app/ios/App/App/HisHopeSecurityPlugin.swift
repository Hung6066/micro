import Capacitor
import CommonCrypto
import Foundation
import Security

@objc(HisHopeSecurityPlugin)
public class HisHopeSecurityPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "HisHopeSecurityPlugin"
    public let jsName = "HisHopeSecurity"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "deviceSecurity", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "configureCertificatePins", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "isPinConfigured", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "setAppPin", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "verifyAppPin", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "clearAppPin", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "request", returnType: CAPPluginReturnPromise)
    ]

    private let defaults = UserDefaults.standard

    @objc func deviceSecurity(_ call: CAPPluginCall) {
        let rooted = ["/Applications/Cydia.app", "/usr/sbin/sshd", "/bin/bash", "/private/var/lib/apt"].contains { FileManager.default.fileExists(atPath: $0) }
        call.resolve([
            "status": rooted ? "compromised" : "secure",
            "rootedOrJailbroken": rooted,
            "emulator": false,
            "reason": rooted ? "native_jailbreak_indicator" : NSNull()
        ])
    }

    @objc func configureCertificatePins(_ call: CAPPluginCall) {
        defaults.set(call.getArray("pins") ?? [], forKey: "certificate_pins")
        call.resolve()
    }

    @objc func isPinConfigured(_ call: CAPPluginCall) {
        call.resolve(["configured": defaults.data(forKey: "pin_hash") != nil])
    }

    @objc func setAppPin(_ call: CAPPluginCall) {
        guard let pin = call.getString("pin"), pin.range(of: "^[0-9]{6,12}$", options: .regularExpression) != nil else {
            call.reject("PIN must contain 6-12 digits")
            return
        }
        var salt = Data(count: 16)
        _ = salt.withUnsafeMutableBytes { SecRandomCopyBytes(kSecRandomDefault, 16, $0.baseAddress!) }
        defaults.set(derive(pin, salt: salt), forKey: "pin_hash")
        defaults.set(salt, forKey: "pin_salt")
        call.resolve()
    }

    @objc func verifyAppPin(_ call: CAPPluginCall) {
        guard let pin = call.getString("pin"), let salt = defaults.data(forKey: "pin_salt"), let expected = defaults.data(forKey: "pin_hash") else {
            call.resolve(["valid": false]); return
        }
        call.resolve(["valid": derive(pin, salt: salt) == expected])
    }

    @objc func clearAppPin(_ call: CAPPluginCall) {
        defaults.removeObject(forKey: "pin_hash")
        defaults.removeObject(forKey: "pin_salt")
        call.resolve()
    }

    @objc func request(_ call: CAPPluginCall) {
        guard let rawURL = call.getString("url"), let url = URL(string: rawURL),
              let method = call.getString("method") else {
            call.reject("Invalid native HTTP request")
            return
        }
        var request = URLRequest(url: url)
        request.httpMethod = method
        if let headers = call.getObject("headers") as? [String: String] {
            headers.forEach { request.setValue($1, forHTTPHeaderField: $0) }
        }
        if let body = call.getString("body") { request.httpBody = body.data(using: .utf8) }
        let delegate = PinningDelegate(host: url.host ?? "", pins: pins(for: url.host ?? ""))
        URLSession(configuration: .ephemeral, delegate: delegate, delegateQueue: nil)
            .dataTask(with: request) { data, response, error in
                if let error { call.reject("Native HTTP request failed", error.localizedDescription); return }
                let http = response as? HTTPURLResponse
                let body = data.flatMap { String(data: $0, encoding: .utf8) } ?? ""
                var headers: [String: String] = [:]
                http?.allHeaderFields.forEach { key, value in headers[String(describing: key)] = String(describing: value) }
                call.resolve(["status": http?.statusCode ?? 0, "body": body, "headers": headers])
            }.resume()
    }

    private func pins(for host: String) -> [String] {
        guard let entries = defaults.array(forKey: "certificate_pins") as? [[String: Any]] else { return [] }
        return entries.compactMap { entry in
            guard (entry["host"] as? String)?.caseInsensitiveCompare(host) == .orderedSame else { return nil }
            return entry["sha256Spki"] as? String
        }
    }

    private func derive(_ pin: String, salt: Data) -> Data {
        var output = Data(count: 32)
        let password = Array(pin.utf8)
        output.withUnsafeMutableBytes { outputBytes in
            salt.withUnsafeBytes { saltBytes in
                _ = CCKeyDerivationPBKDF(CCPBKDFAlgorithm(kCCPBKDF2), password, password.count, saltBytes.bindMemory(to: UInt8.self).baseAddress, salt.count, CCPseudoRandomAlgorithm(kCCPRFHmacAlgSHA256), 120000, outputBytes.bindMemory(to: UInt8.self).baseAddress, 32)
            }
        }
        return output
    }
}

private final class PinningDelegate: NSObject, URLSessionDelegate {
    private let host: String
    private let pins: [String]

    init(host: String, pins: [String]) {
        self.host = host
        self.pins = pins
    }

    func urlSession(_ session: URLSession, didReceive challenge: URLAuthenticationChallenge,
                    completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void) {
        guard challenge.protectionSpace.authenticationMethod == NSURLAuthenticationMethodServerTrust,
              let trust = challenge.protectionSpace.serverTrust else {
            completionHandler(.performDefaultHandling, nil)
            return
        }
        guard !pins.isEmpty,
              challenge.protectionSpace.host.caseInsensitiveCompare(host) == .orderedSame,
              SecTrustEvaluateWithError(trust, nil), isPinned(trust) else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }
        completionHandler(.useCredential, URLCredential(trust: trust))
    }

    private func isPinned(_ trust: SecTrust) -> Bool {
        guard let key = SecTrustCopyKey(trust), let external = SecKeyCopyExternalRepresentation(key, nil) as Data? else { return false }
        var digest = [UInt8](repeating: 0, count: Int(CC_SHA256_DIGEST_LENGTH))
        external.withUnsafeBytes { _ = CC_SHA256($0.baseAddress, CC_LONG(external.count), &digest) }
        return pins.contains("sha256/" + Data(digest).base64EncodedString())
    }
}
