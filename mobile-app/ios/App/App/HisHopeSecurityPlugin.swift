import Capacitor
import CommonCrypto
import Foundation
import Security
import UIKit
import WebKit
import AuthenticationServices

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
        CAPPluginMethod(name: "request", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "openPinnedAuthBrowser", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "isPasskeySupported", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "createPasskey", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "authenticatePasskey", returnType: CAPPluginReturnPromise)
    ]

    private let defaults = UserDefaults.standard
    private var passkeyCall: CAPPluginCall?

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
        let rawPins = call.getArray("pins") ?? []
        let pins = rawPins.compactMap { value -> [String: Any]? in
            guard let entry = value as? [String: Any],
                  let host = entry["host"] as? String,
                  !host.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                  let spki = entry["sha256Spki"] as? String,
                  spki.hasPrefix("sha256/"),
                  let digest = Data(base64Encoded: String(spki.dropFirst("sha256/".count))),
                  digest.count == Int(CC_SHA256_DIGEST_LENGTH) else { return nil }
            return ["host": host, "sha256Spki": spki]
        }
        guard !rawPins.isEmpty, pins.count == rawPins.count,
              !bundledPins().isEmpty,
              canonicalPins(pins) == canonicalPins(bundledPins()) else {
            call.reject("At least one valid certificate pin is required")
            return
        }
        call.resolve()
    }

    @objc func isPinConfigured(_ call: CAPPluginCall) {
        call.resolve(["configured": !bundledPins().isEmpty])
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

    @objc func isPasskeySupported(_ call: CAPPluginCall) {
        if #available(iOS 16.0, *) {
            call.resolve(["supported": true])
        } else {
            call.resolve(["supported": false])
        }
    }

    @available(iOS 16.0, *)
    @objc func createPasskey(_ call: CAPPluginCall) {
        guard let raw = call.getString("requestJson"), let data = raw.data(using: .utf8),
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let challenge = base64UrlData(json["challenge"] as? String),
              let rp = json["rp"] as? [String: Any], let rpId = rp["id"] as? String,
              let user = json["user"] as? [String: Any], let userId = base64UrlData(user["id"] as? String),
              let name = user["name"] as? String else {
            call.reject("Invalid passkey creation options"); return
        }
        let provider = ASAuthorizationPlatformPublicKeyCredentialProvider(relyingPartyIdentifier: rpId)
        let request = provider.createCredentialRegistrationRequest(challenge: challenge, name: name, userID: userId)
        passkeyCall = call
        let controller = ASAuthorizationController(authorizationRequests: [request])
        controller.delegate = self
        controller.presentationContextProvider = self
        controller.performRequests()
    }

    @available(iOS 16.0, *)
    @objc func authenticatePasskey(_ call: CAPPluginCall) {
        guard let raw = call.getString("requestJson"), let data = raw.data(using: .utf8),
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let challenge = base64UrlData(json["challenge"] as? String),
              let rpId = json["rpId"] as? String else {
            call.reject("Invalid passkey request options"); return
        }
        let provider = ASAuthorizationPlatformPublicKeyCredentialProvider(relyingPartyIdentifier: rpId)
        let request = provider.createCredentialAssertionRequest(challenge: challenge)
        if let allowed = json["allowCredentials"] as? [[String: Any]] {
            request.allowedCredentials = allowed.compactMap { item in
                guard let id = base64UrlData(item["id"] as? String) else { return nil }
                return ASAuthorizationPlatformPublicKeyCredentialDescriptor(credentialID: id)
            }
        }
        passkeyCall = call
        let controller = ASAuthorizationController(authorizationRequests: [request])
        controller.delegate = self
        controller.presentationContextProvider = self
        controller.performRequests()
    }

    @objc func request(_ call: CAPPluginCall) {
        guard let rawURL = call.getString("url"), let url = URL(string: rawURL),
              url.scheme?.lowercased() == "https",
              let method = call.getString("method"),
              !pins(for: url.host ?? "").isEmpty else {
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

    @objc func openPinnedAuthBrowser(_ call: CAPPluginCall) {
        guard let rawURL = call.getString("url"),
              let url = URL(string: rawURL),
              url.scheme?.lowercased() == "https",
              let host = url.host,
              !pins(for: host).isEmpty else {
            call.reject("Pinned HTTPS authorization URL is required")
            return
        }

        DispatchQueue.main.async { [weak self] in
            guard let self, let presenter = self.bridge?.viewController else {
                call.reject("Unable to present secure authorization browser")
                return
            }
            let controller = PinnedAuthViewController(
                url: url,
                pins: { [weak self] host in self?.pins(for: host) ?? [] },
                onCallback: { callback in
                    guard callback.scheme?.lowercased() == "hishope",
                          callback.host?.lowercased() == "auth",
                          callback.path == "/callback" || callback.path == "/logout-callback" else { return }
                    DispatchQueue.main.async {
                        UIApplication.shared.open(callback)
                    }
                })
            presenter.present(controller, animated: true)
            call.resolve()
        }
    }

    private func pins(for host: String) -> [String] {
        let entries = bundledPins()
        return entries.compactMap { entry in
            guard (entry["host"] as? String)?.caseInsensitiveCompare(host) == .orderedSame else { return nil }
            return entry["sha256Spki"] as? String
        }
    }

    private func bundledPins() -> [[String: Any]] {
        guard let url = Bundle.main.url(forResource: "HisHopeCertificatePins", withExtension: "plist"),
              let data = try? Data(contentsOf: url),
              let value = try? PropertyListSerialization.propertyList(from: data, format: nil),
              let entries = value as? [[String: Any]] else { return [] }
        return entries
    }

    private func canonicalPins(_ entries: [[String: Any]]) -> String {
        entries.compactMap { entry in
            guard let host = entry["host"] as? String,
                  let pin = entry["sha256Spki"] as? String else { return nil }
            return "\(host.lowercased())=\(pin)"
        }.sorted().joined(separator: "|")
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

    @available(iOS 16.0, *)
    private func base64UrlData(_ value: String?) -> Data? {
        guard let value else { return nil }
        var encoded = value.replacingOccurrences(of: "-", with: "+").replacingOccurrences(of: "_", with: "/")
        encoded += String(repeating: "=", count: (4 - encoded.count % 4) % 4)
        return Data(base64Encoded: encoded)
    }

    @available(iOS 16.0, *)
    private func base64Url(_ data: Data) -> String {
        data.base64EncodedString().replacingOccurrences(of: "+", with: "-").replacingOccurrences(of: "/", with: "_").replacingOccurrences(of: "=", with: "")
    }

    @available(iOS 16.0, *)
    private func resolvePasskey(_ credential: ASAuthorizationCredential) {
        guard let call = passkeyCall else { return }
        passkeyCall = nil
        if let registration = credential as? ASAuthorizationPlatformPublicKeyCredentialRegistration {
            call.resolve(["responseJson": [
                "id": base64Url(registration.credentialID),
                "rawId": base64Url(registration.credentialID),
                "type": "public-key",
                "response": [
                    "clientDataJSON": base64Url(registration.rawClientDataJSON),
                    "attestationObject": registration.rawAttestationObject.map(base64Url) ?? ""
                ]
            ]])
        } else if let assertion = credential as? ASAuthorizationPlatformPublicKeyCredentialAssertion {
            call.resolve(["responseJson": [
                "id": base64Url(assertion.credentialID),
                "rawId": base64Url(assertion.credentialID),
                "type": "public-key",
                "response": [
                    "clientDataJSON": base64Url(assertion.rawClientDataJSON),
                    "authenticatorData": base64Url(assertion.rawAuthenticatorData),
                    "signature": base64Url(assertion.signature),
                    "userHandle": assertion.userID.map(base64Url)
                ]
            ]])
        } else { call.reject("Unsupported passkey credential") }
    }
}

@available(iOS 16.0, *)
extension HisHopeSecurityPlugin: ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding {
    public func authorizationController(controller: ASAuthorizationController, didCompleteWithAuthorization authorization: ASAuthorization) {
        resolvePasskey(authorization.credential)
    }

    public func authorizationController(controller: ASAuthorizationController, didCompleteWithError error: Error) {
        let call = passkeyCall
        passkeyCall = nil
        call?.reject("Passkey operation failed", error.localizedDescription)
    }

    public func presentationAnchor(for controller: ASAuthorizationController) -> ASPresentationAnchor {
        bridge?.viewController?.view.window ?? ASPresentationAnchor()
    }
}

private final class PinnedAuthViewController: UIViewController, WKNavigationDelegate {
    private let initialURL: URL
    private let pinProvider: (String) -> [String]
    private let onCallback: (URL) -> Void
    private var webView: WKWebView!

    init(url: URL, pins: @escaping (String) -> [String], onCallback: @escaping (URL) -> Void) {
        self.initialURL = url
        self.pinProvider = pins
        self.onCallback = onCallback
        super.init(nibName: nil, bundle: nil)
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = .systemBackground
        let configuration = WKWebViewConfiguration()
        webView = WKWebView(frame: .zero, configuration: configuration)
        webView.navigationDelegate = self
        webView.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(webView)
        NSLayoutConstraint.activate([
            webView.topAnchor.constraint(equalTo: view.safeAreaLayoutGuide.topAnchor),
            webView.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            webView.trailingAnchor.constraint(equalTo: view.trailingAnchor),
            webView.bottomAnchor.constraint(equalTo: view.bottomAnchor)
        ])
        navigationItem.title = "His.Hope sign in"
        navigationItem.leftBarButtonItem = UIBarButtonItem(
            barButtonSystemItem: .cancel,
            target: self,
            action: #selector(close)
        )
        webView.load(URLRequest(url: initialURL))
    }

    @objc private func close() { dismiss(animated: true) }

    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction,
                 decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        guard let url = navigationAction.request.url else {
            decisionHandler(.cancel)
            return
        }
        if url.scheme?.lowercased() == "hishope" {
            onCallback(url)
            dismiss(animated: true)
            decisionHandler(.cancel)
            return
        }
        guard url.scheme?.lowercased() == "https",
              let host = url.host,
              !pinProvider(host).isEmpty else {
            decisionHandler(.cancel)
            return
        }
        decisionHandler(.allow)
    }

    func webView(_ webView: WKWebView, didReceive challenge: URLAuthenticationChallenge,
                 completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void) {
        guard challenge.protectionSpace.authenticationMethod == NSURLAuthenticationMethodServerTrust,
              let trust = challenge.protectionSpace.serverTrust,
              let host = challenge.protectionSpace.host as String? else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }
        let pins = pinProvider(host)
        guard !pins.isEmpty,
              SecTrustEvaluateWithError(trust, nil),
              PinnedAuthViewController.isPinned(trust, pins: pins) else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }
        completionHandler(.useCredential, URLCredential(trust: trust))
    }

    static func isPinned(_ trust: SecTrust, pins: [String]) -> Bool {
        guard let pin = spkiPin(for: trust) else { return false }
        return pins.contains(pin)
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
        guard let pin = spkiPin(for: trust) else { return false }
        return pins.contains(pin)
    }
}

/// Returns the RFC 7469/Android-compatible hash of the leaf certificate's
/// DER SubjectPublicKeyInfo. Hashing SecKeyCopyExternalRepresentation directly
/// would hash raw key bytes and would not match an SPKI pin.
private func spkiPin(for trust: SecTrust) -> String? {
    guard let certificate = SecTrustGetCertificateAtIndex(trust, 0),
          let certificateData = SecCertificateCopyData(certificate) as Data?,
          let spki = subjectPublicKeyInfo(in: certificateData) else { return nil }

    var digest = [UInt8](repeating: 0, count: Int(CC_SHA256_DIGEST_LENGTH))
    spki.withUnsafeBytes { _ = CC_SHA256($0.baseAddress, CC_LONG(spki.count), &digest) }
    return "sha256/" + Data(digest).base64EncodedString()
}

private struct DerElement {
    let start: Int
    let contentStart: Int
    let end: Int
}

private func subjectPublicKeyInfo(in data: Data) -> Data? {
    let bytes = [UInt8](data)
    guard let certificate = derElement(in: bytes, at: 0),
          let tbs = derElement(in: bytes, at: certificate.contentStart),
          bytes[tbs.start] == 0x30 else { return nil }

    var cursor = tbs.contentStart
    if cursor < tbs.end, bytes[cursor] == 0xA0 {
        guard let version = derElement(in: bytes, at: cursor) else { return nil }
        cursor = version.end
    }

    // TBSCertificate: serialNumber, signature, issuer, validity, subject,
    // then subjectPublicKeyInfo.
    for _ in 0..<5 {
        guard let field = derElement(in: bytes, at: cursor) else { return nil }
        cursor = field.end
    }
    guard let spki = derElement(in: bytes, at: cursor), bytes[spki.start] == 0x30 else { return nil }
    return Data(bytes[spki.start..<spki.end])
}

private func derElement(in bytes: [UInt8], at offset: Int) -> DerElement? {
    guard offset >= 0, offset + 2 <= bytes.count else { return nil }
    var headerLength = 2
    var length = Int(bytes[offset + 1])
    if length & 0x80 != 0 {
        let count = length & 0x7F
        guard count > 0, count <= 4, offset + 2 + count <= bytes.count else { return nil }
        length = 0
        for index in 0..<count {
            length = (length << 8) | Int(bytes[offset + 2 + index])
        }
        headerLength += count
    }
    let contentStart = offset + headerLength
    let end = contentStart + length
    guard contentStart <= end, end <= bytes.count else { return nil }
    return DerElement(start: offset, contentStart: contentStart, end: end)
}
